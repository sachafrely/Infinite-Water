using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Discovers wheel locations and owns active wheel physics, visuals and energy tracking.
/// Persistent ownership is handled by WheelPurchaseSystem.
/// </summary>
internal sealed class WaterWheelManager
{
	public const int MaxWheelCount = 6;
	public const int WheelTileAtlasX = 7;
	public const int WheelTileAtlasY = 6;
	public const float WheelOuterRadius = 45.0f;
	public const float WheelInnerRadius = 12.5f;
	public const int WheelBladeCount = 8;
	public const float WheelBladeWidth = 7.5f;

	private const float CurrentGenerationThreshold = 0.002f;

	private sealed class WheelLocation
	{
		public int Id;
		public Vector2I TileMapCell;
		public Vector2 SimulationPosition;
	}

	private readonly PbfSolver solver;
	private readonly EnergySystem energySystem;
	private readonly Node2D owner;

	private readonly List<WheelLocation> wheelLocations =
		new List<WheelLocation>();
	private readonly List<FluidWheelState> wheelStates =
		new List<FluidWheelState>();
	private readonly List<int> activeWheelIds =
		new List<int>();
	private readonly List<WaterWheelVisual> wheelVisuals =
		new List<WaterWheelVisual>();

	private float[] previousWheelAngles = Array.Empty<float>();
	private double[] wheelEnergyGeneratedThisFrame = Array.Empty<double>();
	private double energyGeneratedThisFrame = 0.0;

	public int WheelCount => wheelStates.Count;
	public int WheelLocationCount => wheelLocations.Count;
	public double EnergyGeneratedThisFrame => energyGeneratedThisFrame;

	public WaterWheelManager(
		PbfSolver solver,
		EnergySystem energySystem,
		Node2D owner)
	{
		this.solver = solver;
		this.energySystem = energySystem;
		this.owner = owner;
	}

	/// <summary>
	/// Discovers all wheel marker locations without creating runtime wheels.
	/// Locations are sorted by Y first and X second so wheel IDs are deterministic.
	/// The map's fourth location in this stable top-to-bottom/left-to-right order
	/// is the intended starting wheel.
	/// </summary>
	public void DiscoverWheelLocations(
		TileMapLayer environment,
		Func<Vector2, Vector2> toSimulationSpace)
	{
		wheelLocations.Clear();

		if (environment == null)
		{
			GD.PushWarning(
				"FluidSimulator: Environment TileMapLayer could not be found. No wheels discovered."
			);
			return;
		}

		if (toSimulationSpace == null)
		{
			GD.PushWarning(
				"FluidSimulator: Could not establish viewport mapping. No wheels discovered."
			);
			return;
		}

		foreach (Vector2I cell in environment.GetUsedCells())
		{
			if (wheelLocations.Count >= MaxWheelCount)
				break;

			int sourceId = environment.GetCellSourceId(cell);
			if (sourceId < 0)
				continue;

			Vector2I atlasCoords = environment.GetCellAtlasCoords(cell);
			if (atlasCoords.X != WheelTileAtlasX || atlasCoords.Y != WheelTileAtlasY)
				continue;

			Vector2 tileCenterLocal = environment.MapToLocal(cell);
			Vector2 tileCenterGlobal = environment.ToGlobal(tileCenterLocal);

			wheelLocations.Add(
				new WheelLocation
				{
					TileMapCell = cell,
					SimulationPosition = toSimulationSpace(tileCenterGlobal)
				}
			);
		}

		wheelLocations.Sort(
			(a, b) =>
			{
				int y = a.TileMapCell.Y.CompareTo(b.TileMapCell.Y);
				return y != 0
					? y
					: a.TileMapCell.X.CompareTo(b.TileMapCell.X);
			}
		);

		for (int i = 0; i < wheelLocations.Count; i++)
		{
			wheelLocations[i].Id = i + 1;

			GD.Print(
				"Wheel location " +
				wheelLocations[i].Id +
				" -> tile " +
				wheelLocations[i].TileMapCell +
				" -> simulation " +
				wheelLocations[i].SimulationPosition
			);
		}

		GD.Print(
			"Wheel locations discovered: " +
			wheelLocations.Count +
			"/" +
			MaxWheelCount +
			". Starting wheel ID=" +
			WheelPurchaseSystem.StartingWheelId
		);
	}

	public bool HasWheelLocation(int wheelId)
	{
		return FindLocation(wheelId) != null;
	}

	public Vector2 GetWheelSimulationPosition(int wheelId)
	{
		WheelLocation location = FindLocation(wheelId);
		return location != null
			? location.SimulationPosition
			: Vector2.Zero;
	}

	public bool TryActivateWheel(int wheelId)
	{
		WheelLocation location = FindLocation(wheelId);
		if (location == null)
			return false;

		if (activeWheelIds.Contains(wheelId))
			return true;

		// Wheel 4 is initialized first and therefore owns the solver's primary
		// wheel slot. Every later purchase uses a standalone FluidWheelState.
		FluidWheelState wheelState;
		if (wheelStates.Count == 0)
		{
			if (wheelId != WheelPurchaseSystem.StartingWheelId)
			{
				GD.PushWarning(
					"WaterWheelManager: Refusing to make Wheel " +
					wheelId +
					" the primary wheel. Starting Wheel 4 must be activated first."
				);
				return false;
			}

			wheelState = solver.CreateWheel(location.SimulationPosition);
		}
		else
		{
			wheelState = new FluidWheelState(location.SimulationPosition);
		}

		wheelStates.Add(wheelState);
		activeWheelIds.Add(wheelId);
		CreateWheelCollidersAndVisual(location.SimulationPosition, wheelState);

		EnsureEnergyTrackingCapacity();
		previousWheelAngles[wheelStates.Count - 1] = wheelState.Angle;
		wheelEnergyGeneratedThisFrame[wheelStates.Count - 1] = 0.0;

		GD.Print("Wheel activated: Wheel " + wheelId);
		return true;
	}

	private void CreateWheelCollidersAndVisual(
		Vector2 center,
		FluidWheelState wheelState)
	{
		for (int i = 0; i < WheelBladeCount; i++)
		{
			float angle = Mathf.Tau * i / WheelBladeCount;
			Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 tangent = new Vector2(-direction.Y, direction.X);
			Vector2 innerCenter = direction * WheelInnerRadius;
			Vector2 outerCenter = direction * WheelOuterRadius;

			Vector2[] blade =
			{
				innerCenter + tangent * WheelBladeWidth,
				outerCenter + tangent * WheelBladeWidth,
				outerCenter - tangent * WheelBladeWidth,
				innerCenter - tangent * WheelBladeWidth
			};

			FluidPolygonCollider collider = new FluidPolygonCollider(blade);
			collider.ConfigureAsWheel(wheelState);
			solver.AddPolygonCollider(collider);
		}

		const int hubSegments = 16;
		Vector2[] hub = new Vector2[hubSegments];
		for (int i = 0; i < hubSegments; i++)
		{
			float angle = Mathf.Tau * i / hubSegments;
			hub[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * WheelInnerRadius;
		}

		solver.AddPolygonCollider(new FluidPolygonCollider(hub));

		WaterWheelVisual visual = new WaterWheelVisual();
		visual.Position = center;
		visual.OuterRadius = WheelOuterRadius;
		visual.InnerRadius = WheelInnerRadius;
		visual.BladeCount = WheelBladeCount;
		visual.BladeWidth = WheelBladeWidth;
		owner.AddChild(visual);
		visual.SetWheelAngle(wheelState.Angle);
		wheelVisuals.Add(visual);
	}

	private WheelLocation FindLocation(int wheelId)
	{
		for (int i = 0; i < wheelLocations.Count; i++)
		{
			if (wheelLocations[i].Id == wheelId)
				return wheelLocations[i];
		}

		return null;
	}

	private void EnsureEnergyTrackingCapacity()
	{
		if (previousWheelAngles.Length == wheelStates.Count &&
			wheelEnergyGeneratedThisFrame.Length == wheelStates.Count)
			return;

		float[] previous = new float[wheelStates.Count];
		double[] energy = new double[wheelStates.Count];

		Array.Copy(
			previousWheelAngles,
			previous,
			Math.Min(previousWheelAngles.Length, previous.Length)
		);
		Array.Copy(
			wheelEnergyGeneratedThisFrame,
			energy,
			Math.Min(wheelEnergyGeneratedThisFrame.Length, energy.Length)
		);

		previousWheelAngles = previous;
		wheelEnergyGeneratedThisFrame = energy;
	}

	public void InitializeWheelEnergyTracking()
	{
		previousWheelAngles = new float[wheelStates.Count];
		wheelEnergyGeneratedThisFrame = new double[wheelStates.Count];

		for (int i = 0; i < wheelStates.Count; i++)
			previousWheelAngles[i] = wheelStates[i].Angle;
	}

	public void ResetFrameEnergy()
	{
		energyGeneratedThisFrame = 0.0;
		EnsureEnergyTrackingCapacity();

		for (int i = 0; i < wheelEnergyGeneratedThisFrame.Length; i++)
			wheelEnergyGeneratedThisFrame[i] = 0.0;
	}

	public bool UpdateEnergyFromWheelRotation()
	{
		int wheelCount = wheelStates.Count;
		if (wheelCount <= 0)
			return false;

		EnsureEnergyTrackingCapacity();
		bool currentGenerated = false;

		for (int i = 0; i < wheelCount; i++)
		{
			float currentAngle = wheelStates[i].Angle;
			float angularMovement = Mathf.Abs(
				Mathf.AngleDifference(previousWheelAngles[i], currentAngle)
			);

			if (angularMovement > 0.0f)
			{
				double frameEnergy = angularMovement * energySystem.EnergyPerRadian;
				energySystem.AddEnergy(frameEnergy);
				energyGeneratedThisFrame += frameEnergy;
				wheelEnergyGeneratedThisFrame[i] += frameEnergy;
			}

			if (angularMovement > CurrentGenerationThreshold)
				currentGenerated = true;

			previousWheelAngles[i] = currentAngle;
		}

		return currentGenerated;
	}

	public double GetWheelEnergyThisFrame(int wheelIndex)
	{
		if (wheelIndex < 0 || wheelIndex >= wheelEnergyGeneratedThisFrame.Length)
			return 0.0;

		return wheelEnergyGeneratedThisFrame[wheelIndex];
	}

	public double GetWheelEnergyPerSecond(int wheelIndex, float delta)
	{
		if (delta <= 0.000001f)
			return 0.0;

		return GetWheelEnergyThisFrame(wheelIndex) / delta;
	}

	public double[] CopyWheelEnergyGeneratedThisFrame()
	{
		double[] copy = new double[wheelEnergyGeneratedThisFrame.Length];
		Array.Copy(wheelEnergyGeneratedThisFrame, copy, wheelEnergyGeneratedThisFrame.Length);
		return copy;
	}

	public void StepAdditionalWheels(float dt)
	{
		// Runtime index 0 is always the explicitly designated primary Wheel 4.
		for (int i = 1; i < wheelStates.Count; i++)
			wheelStates[i].Step(dt);
	}

	public void StepPrimaryWheel(float dt)
	{
		if (wheelStates.Count > 0)
			wheelStates[0].Step(dt);
	}

	public void UpdateWheelVisuals()
	{
		int count = Math.Min(wheelStates.Count, wheelVisuals.Count);
		for (int i = 0; i < count; i++)
			wheelVisuals[i].SetWheelAngle(wheelStates[i].Angle);
	}
}
