using System;
using System.Collections.Generic;
using Godot;

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
	private const int PreferredInitialWheelSortedIndex = 2;

	private readonly PbfSolver solver;
	private readonly EnergySystem energySystem;
	private readonly Node2D owner;
	private readonly List<FluidWheelState> wheelStates = new List<FluidWheelState>();
	private readonly List<WaterWheelVisual> wheelVisuals = new List<WaterWheelVisual>();
	private readonly List<Vector2> wheelPositions = new List<Vector2>();
	private readonly bool[] wheelUnlocked = new bool[MaxWheelCount];

	private float[] previousWheelAngles = Array.Empty<float>();
	private double[] wheelEnergyGeneratedThisFrame = Array.Empty<double>();
	private double energyGeneratedThisFrame;

	public int WheelCount => wheelStates.Count;
	public int UnlockedWheelCount => wheelStates.Count;
	public double EnergyGeneratedThisFrame => energyGeneratedThisFrame;
	public IReadOnlyList<Vector2> WheelPositions => wheelPositions;

	public WaterWheelManager(PbfSolver solver, EnergySystem energySystem, Node2D owner)
	{
		this.solver = solver;
		this.energySystem = energySystem;
		this.owner = owner;
	}

	public bool IsWheelUnlocked(int index)
	{
		return index >= 0 && index < MaxWheelCount && wheelUnlocked[index];
	}

	public void CreateWaterWheelsFromEnvironment(TileMapLayer environment, Func<Vector2, Vector2> toSimulationSpace)
	{
		wheelPositions.Clear();
		Array.Clear(wheelUnlocked, 0, wheelUnlocked.Length);

		if (environment == null)
		{
			GD.PushWarning("FluidSimulator: Environment TileMapLayer could not be found. No wheels created.");
			return;
		}

		if (toSimulationSpace == null)
		{
			GD.PushWarning("FluidSimulator: Could not establish viewport mapping. No wheels created.");
			return;
		}

		foreach (Vector2I cell in environment.GetUsedCells())
		{
			if (environment.GetCellSourceId(cell) < 0)
				continue;

			Vector2I atlasCoords = environment.GetCellAtlasCoords(cell);
			if (atlasCoords.X != WheelTileAtlasX || atlasCoords.Y != WheelTileAtlasY)
				continue;

			Vector2 tileCenterGlobal = environment.ToGlobal(environment.MapToLocal(cell));
			wheelPositions.Add(toSimulationSpace(tileCenterGlobal));
		}

		wheelPositions.Sort((a, b) =>
		{
			int y = a.Y.CompareTo(b.Y);
			return y != 0 ? y : a.X.CompareTo(b.X);
		});

		if (wheelPositions.Count > MaxWheelCount)
			wheelPositions.RemoveRange(MaxWheelCount, wheelPositions.Count - MaxWheelCount);

		if (wheelPositions.Count == 0)
		{
			GD.Print("Water wheel marker tiles discovered: 0/" + MaxWheelCount);
			return;
		}

		int initialPositionIndex = Math.Min(PreferredInitialWheelSortedIndex, wheelPositions.Count - 1);
		Vector2 initialPosition = wheelPositions[initialPositionIndex];

		// Slot 0 is the first owned wheel. Its physical position is explicitly
		// the third marker from the top in the current level.
		wheelUnlocked[0] = true;
		CreateWaterWheel(initialPosition);

		GD.Print(
			"Water wheel markers discovered: " + wheelPositions.Count + "/" + MaxWheelCount +
			". Active wheel: slot 0 at simulation " + initialPosition
		);
	}

	public void CreateWaterWheel(Vector2 center)
	{
		if (wheelStates.Count >= MaxWheelCount)
			return;

		FluidWheelState wheelState = wheelStates.Count == 0
			? solver.CreateWheel(center)
			: new FluidWheelState(center);

		wheelStates.Add(wheelState);

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

		WaterWheelVisual visual = new WaterWheelVisual
		{
			Position = center,
			OuterRadius = WheelOuterRadius,
			InnerRadius = WheelInnerRadius,
			BladeCount = WheelBladeCount,
			BladeWidth = WheelBladeWidth
		};

		owner.AddChild(visual);
		visual.SetWheelAngle(wheelState.Angle);
		wheelVisuals.Add(visual);
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
		if (wheelEnergyGeneratedThisFrame.Length != wheelStates.Count)
			wheelEnergyGeneratedThisFrame = new double[wheelStates.Count];
		Array.Clear(wheelEnergyGeneratedThisFrame, 0, wheelEnergyGeneratedThisFrame.Length);
	}

	public bool UpdateEnergyFromWheelRotation()
	{
		int wheelCount = wheelStates.Count;
		if (wheelCount <= 0)
			return false;

		if (previousWheelAngles.Length != wheelCount)
		{
			InitializeWheelEnergyTracking();
			return false;
		}

		if (wheelEnergyGeneratedThisFrame.Length != wheelCount)
			wheelEnergyGeneratedThisFrame = new double[wheelCount];

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
