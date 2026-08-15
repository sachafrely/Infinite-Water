using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns water wheel creation, visuals, and energy tracking.
/// </summary>
internal sealed class WaterWheelManager
{
	// ============================================================
	// Wheel constants
	// ============================================================

	public const int MaxWheelCount = 6;

	public const int WheelTileAtlasX = 7;

	public const int WheelTileAtlasY = 6;

	public const float WheelOuterRadius = 45.0f;

	public const float WheelInnerRadius = 12.5f;

	public const int WheelBladeCount = 8;

	public const float WheelBladeWidth = 7.5f;

	private const float CurrentGenerationThreshold = 0.0005f;

	// ============================================================
	// Dependencies
	// ============================================================

	private readonly PbfSolver solver;

	private readonly EnergySystem energySystem;

	private readonly Node2D owner;

	// ============================================================
	// State
	// ============================================================

	private readonly List<FluidWheelState> wheelStates =
		new List<FluidWheelState>();

	private readonly List<WaterWheelVisual> wheelVisuals =
		new List<WaterWheelVisual>();

	private float[] previousWheelAngles =
		Array.Empty<float>();

	private double[] wheelEnergyGeneratedThisFrame =
		Array.Empty<double>();

	private double energyGeneratedThisFrame = 0.0;

	// ============================================================
	// Properties
	// ============================================================

	/// <summary>
	/// Gets the number of active wheels.
	/// </summary>
	public int WheelCount =>
		wheelStates.Count;

	/// <summary>
	/// Gets the total energy generated this frame across all wheels.
	/// </summary>
	public double EnergyGeneratedThisFrame =>
		energyGeneratedThisFrame;

	// ============================================================
	// Construction
	// ============================================================

	/// <summary>
	/// Creates a new wheel manager.
	/// </summary>
	public WaterWheelManager(
		PbfSolver solver,
		EnergySystem energySystem,
		Node2D owner)
	{
		this.solver =
			solver;

		this.energySystem =
			energySystem;

		this.owner =
			owner;
	}

	// ============================================================
	// Wheel creation
	// ============================================================

	/// <summary>
	/// Creates wheels from environment marker tiles.
	/// </summary>
	public void CreateWaterWheelsFromEnvironment(
		TileMapLayer environment,
		Func<Vector2, Vector2> toSimulationSpace)
	{
		if (
			environment == null)
		{
			GD.PushWarning(
				"FluidSimulator: Environment TileMapLayer " +
				"could not be found. No wheels created."
			);

			return;
		}

		if (
			toSimulationSpace == null)
		{
			GD.PushWarning(
				"FluidSimulator: Could not establish " +
				"viewport mapping. No wheels created."
			);

			return;
		}

		IEnumerable<Vector2I> usedCells =
			environment.GetUsedCells();

		foreach (
			Vector2I cell in
			usedCells)
		{
			if (
				wheelStates.Count >=
				MaxWheelCount)
			{
				break;
			}

			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (
				sourceId < 0)
			{
				continue;
			}

			Vector2I atlasCoords =
				environment.GetCellAtlasCoords(
					cell
				);

			if (
				atlasCoords.X !=
				WheelTileAtlasX ||
				atlasCoords.Y !=
				WheelTileAtlasY)
			{
				continue;
			}

			Vector2 tileCenterLocal =
				environment.MapToLocal(
					cell
				);

			Vector2 tileCenterGlobal =
				environment.ToGlobal(
					tileCenterLocal
				);

			Vector2 simulationPosition =
				toSimulationSpace(
					tileCenterGlobal
				);

			CreateWaterWheel(
				simulationPosition
			);

			GD.Print(
				"Water wheel placed on Environment tile " +
				cell +
				" atlas " +
				atlasCoords +
				" -> simulation " +
				simulationPosition
			);
		}

		GD.Print(
			"Water wheels created from marker tiles: " +
			wheelStates.Count +
			"/" +
			MaxWheelCount
		);
	}

	/// <summary>
	/// Creates one wheel at the requested position.
	/// </summary>
	public void CreateWaterWheel(
		Vector2 center)
	{
		FluidWheelState wheelState;

		if (
			wheelStates.Count == 0)
		{
			wheelState =
				solver.CreateWheel(
					center
				);
		}
		else
		{
			wheelState =
				new FluidWheelState(
					center
				);
		}

		wheelStates.Add(
			wheelState
		);

		for (
			int i = 0;
			i < WheelBladeCount;
			i++)
		{
			float angle =
				Mathf.Tau *
				i /
				WheelBladeCount;

			Vector2 direction =
				new Vector2(
					Mathf.Cos(angle),
					Mathf.Sin(angle)
				);

			Vector2 tangent =
				new Vector2(
					-direction.Y,
					direction.X
				);

			Vector2 innerCenter =
				direction *
				WheelInnerRadius;

			Vector2 outerCenter =
				direction *
				WheelOuterRadius;

			Vector2[] blade =
			{
				innerCenter +
				tangent *
				WheelBladeWidth,

				outerCenter +
				tangent *
				WheelBladeWidth,

				outerCenter -
				tangent *
				WheelBladeWidth,

				innerCenter -
				tangent *
				WheelBladeWidth
			};

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					blade
				);

			collider.ConfigureAsWheel(
				wheelState
			);

			solver.AddPolygonCollider(
				collider
			);
		}

		const int hubSegments = 16;

		Vector2[] hub =
			new Vector2[
				hubSegments
			];

		for (
			int i = 0;
			i < hubSegments;
			i++)
		{
			float angle =
				Mathf.Tau *
				i /
				hubSegments;

			hub[i] =
				new Vector2(
					Mathf.Cos(angle),
					Mathf.Sin(angle)
				) *
				WheelInnerRadius;
		}

		FluidPolygonCollider hubCollider =
			new FluidPolygonCollider(
				hub
			);

		solver.AddPolygonCollider(
			hubCollider
		);

		WaterWheelVisual visual =
			new WaterWheelVisual();

		visual.Position =
			center;

		visual.OuterRadius =
			WheelOuterRadius;

		visual.InnerRadius =
			WheelInnerRadius;

		visual.BladeCount =
			WheelBladeCount;

		visual.BladeWidth =
			WheelBladeWidth;

		owner.AddChild(
			visual
		);

		visual.SetWheelAngle(
			wheelState.Angle
		);

		wheelVisuals.Add(
			visual
		);
	}

	// ============================================================
	// Energy tracking
	// ============================================================

	/// <summary>
	/// Initializes wheel energy tracking arrays.
	/// </summary>
	public void InitializeWheelEnergyTracking()
	{
		previousWheelAngles =
			new float[
				wheelStates.Count
			];

		wheelEnergyGeneratedThisFrame =
			new double[
				wheelStates.Count
			];

		for (
			int i = 0;
			i < wheelStates.Count;
			i++)
		{
			previousWheelAngles[i] =
				wheelStates[i].Angle;

			wheelEnergyGeneratedThisFrame[i] =
				0.0;
		}
	}

	/// <summary>
	/// Resets per-frame wheel energy counters.
	/// </summary>
	public void ResetFrameEnergy()
	{
		energyGeneratedThisFrame =
			0.0;

		if (
			wheelEnergyGeneratedThisFrame.Length !=
			wheelStates.Count)
		{
			wheelEnergyGeneratedThisFrame =
				new double[
					wheelStates.Count
				];
		}

		for (
			int i = 0;
			i < wheelEnergyGeneratedThisFrame.Length;
			i++)
		{
			wheelEnergyGeneratedThisFrame[i] =
				0.0;
		}
	}

	/// <summary>
	/// Updates energy generation from wheel rotation.
	/// </summary>
	public bool UpdateEnergyFromWheelRotation()
	{
		int wheelCount =
			wheelStates.Count;

		if (
			wheelCount <= 0)
		{
			return false;
		}

		if (
			previousWheelAngles.Length !=
			wheelCount)
		{
			InitializeWheelEnergyTracking();

			return false;
		}

		if (
			wheelEnergyGeneratedThisFrame.Length !=
			wheelCount)
		{
			wheelEnergyGeneratedThisFrame =
				new double[
					wheelCount
				];
		}

		bool currentGenerated =
			false;

		for (
			int i = 0;
			i < wheelCount;
			i++)
		{
			float currentAngle =
				wheelStates[i].Angle;

			float previousAngle =
				previousWheelAngles[i];

			float angularMovement =
				Mathf.Abs(
					Mathf.AngleDifference(
						previousAngle,
						currentAngle
					)
				);

			if (
				angularMovement >
				0.0f)
			{
				double frameEnergy =
					angularMovement *
					energySystem.EnergyPerRadian;

				energySystem.AddEnergy(
					frameEnergy
				);

				energyGeneratedThisFrame +=
					frameEnergy;

				wheelEnergyGeneratedThisFrame[i] +=
					frameEnergy;
			}

			if (
				angularMovement >
				CurrentGenerationThreshold)
			{
				currentGenerated =
					true;
			}

			previousWheelAngles[i] =
				currentAngle;
		}

		return currentGenerated;
	}

	/// <summary>
	/// Returns wheel energy for one wheel this frame.
	/// </summary>
	public double GetWheelEnergyThisFrame(
		int wheelIndex)
	{
		if (
			wheelIndex < 0 ||
			wheelIndex >=
			wheelEnergyGeneratedThisFrame.Length)
		{
			return 0.0;
		}

		return wheelEnergyGeneratedThisFrame[
			wheelIndex
		];
	}

	/// <summary>
	/// Returns wheel energy production per second for one wheel.
	/// </summary>
	public double GetWheelEnergyPerSecond(
		int wheelIndex,
		float delta)
	{
		if (
			delta <= 0.000001f)
		{
			return 0.0;
		}

		return
			GetWheelEnergyThisFrame(
				wheelIndex
			) /
			delta;
	}

	/// <summary>
	/// Copies the per-wheel frame energy array.
	/// </summary>
	public double[] CopyWheelEnergyGeneratedThisFrame()
	{
		double[] copy =
			new double[
				wheelEnergyGeneratedThisFrame.Length
			];

		Array.Copy(
			wheelEnergyGeneratedThisFrame,
			copy,
			wheelEnergyGeneratedThisFrame.Length
		);

		return copy;
	}

	// ============================================================
	// Wheel stepping and visuals
	// ============================================================

	/// <summary>
	/// Advances any non-primary wheels.
	/// </summary>
	public void StepAdditionalWheels(
		float dt)
	{
		for (
			int i = 1;
			i < wheelStates.Count;
			i++)
		{
			wheelStates[i].Step(
				dt
			);
		}
	}

	/// <summary>
	/// Advances the primary wheel when the solver is not stepping particles.
	/// </summary>
	public void StepPrimaryWheel(
		float dt)
	{
		if (
			wheelStates.Count > 0)
		{
			wheelStates[0].Step(
				dt
			);
		}
	}

	/// <summary>
	/// Updates wheel visual angles.
	/// </summary>
	public void UpdateWheelVisuals()
	{
		int count =
			Math.Min(
				wheelStates.Count,
				wheelVisuals.Count
			);

		for (
			int i = 0;
			i < count;
			i++)
		{
			wheelVisuals[i].SetWheelAngle(
				wheelStates[i].Angle
			);
		}
	}
}
