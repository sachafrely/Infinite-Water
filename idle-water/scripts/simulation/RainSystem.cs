using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns dynamic rain state, HUD, and spawn planning.
/// </summary>
internal sealed class RainSystem
{
	/// <summary>
	/// Planned spawn data for one rain particle.
	/// </summary>
	internal readonly struct RainSpawnRequest
	{
		public readonly float X;

		public readonly float Y;

		public readonly float VelocityX;

		public readonly float VelocityY;

		public readonly int PixelIndex;

		public RainSpawnRequest(
			float x,
			float y,
			float velocityX,
			float velocityY,
			int pixelIndex)
		{
			X = x;
			Y = y;
			VelocityX = velocityX;
			VelocityY = velocityY;
			PixelIndex = pixelIndex;
		}
	}

	// ============================================================
	// Constants
	// ============================================================

	public const float RainAmount = 120.0f;

	public const int RainMinimumPercent = 0;

	public const int RainMaximumPercent = 100;

	public const int RainPercentStep = 10;

	public const float RainMinimumDuration = 13.0f;

	public const float RainMaximumDuration = 29.0f;

	public const float RainTransitionDuration = 10.0f;

	public const float RainSpawnY = -40.0f;

	public const float RainVelocityX = 0.0f;

	public const float RainVelocityY = 400.0f;

	// ============================================================
	// Dependencies
	// ============================================================

	private readonly Node owner;

	private readonly float worldMinX;

	private readonly float worldMaxX;

	// ============================================================
	// Dynamic rain state
	// ============================================================

	private float currentRainPercent;
	private float targetRainPercent;
	private float rainTransitionStartPercent;
	private float rainTransitionTimer;
	private float rainPhaseTimer;
	private float rainSpawnAccumulator = 0.0f;

	private readonly RandomNumberGenerator rainRandom =
		new RandomNumberGenerator();

	private long totalRainSpawns = 0;

	private long rainRejectedByCapacity = 0;

	private long rainRejectedByDensity = 0;

	// ============================================================
	// HUD
	// ============================================================

	private CanvasLayer rainHudLayer;

	private Label rainHudLabel;

	// ============================================================
	// Coordination
	// ============================================================

	/// <summary>
	/// Gets or sets the anti-lag controller observed by the rain system.
	/// </summary>
	public AntiLagController AntiLagController
	{
		private get;
		set;
	}

	/// <summary>
	/// Gets the current rain percentage.
	/// </summary>
	public float CurrentRainPercent
	{
		get
		{
			return currentRainPercent;
		}
		internal set
		{
			currentRainPercent =
				value;
		}
	}

	/// <summary>
	/// Gets the target rain percentage.
	/// </summary>
	public float TargetRainPercent
	{
		get
		{
			return targetRainPercent;
		}
		internal set
		{
			targetRainPercent =
				value;
		}
	}

	/// <summary>
	/// Gets or sets the transition start percentage.
	/// </summary>
	public float RainTransitionStartPercent
	{
		get
		{
			return rainTransitionStartPercent;
		}
		internal set
		{
			rainTransitionStartPercent =
				value;
		}
	}

	/// <summary>
	/// Gets or sets the transition timer.
	/// </summary>
	public float RainTransitionTimer
	{
		get
		{
			return rainTransitionTimer;
		}
		internal set
		{
			rainTransitionTimer =
				value;
		}
	}

	/// <summary>
	/// Gets or sets the rain phase timer.
	/// </summary>
	public float RainPhaseTimer
	{
		get
		{
			return rainPhaseTimer;
		}
		internal set
		{
			rainPhaseTimer =
				value;
		}
	}

	/// <summary>
	/// Gets the total number of accepted rain spawns.
	/// </summary>
	public long TotalRainSpawns =>
		totalRainSpawns;

	/// <summary>
	/// Gets the number of rain spawns rejected by capacity.
	/// </summary>
	public long RainRejectedByCapacity =>
		rainRejectedByCapacity;

	/// <summary>
	/// Gets the number of rain spawns rejected by density.
	/// </summary>
	public long RainRejectedByDensity =>
		rainRejectedByDensity;

	// ============================================================
	// Construction
	// ============================================================

	/// <summary>
	/// Creates a rain system for the simulation world.
	/// </summary>
	public RainSystem(
		Node owner,
		float worldMinX,
		float worldMaxX)
	{
		this.owner =
			owner;

		this.worldMinX =
			worldMinX;

		this.worldMaxX =
			worldMaxX;

		rainRandom.Randomize();
	}

	// ============================================================
	// Setup
	// ============================================================

	/// <summary>
	/// Initializes the first rain phase.
	/// </summary>
	public void InitializeDynamicRain()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		float initialRainPercent =
			RainMinimumPercent +
			randomStep *
			RainPercentStep;

		currentRainPercent =
			initialRainPercent;

		targetRainPercent =
			initialRainPercent;

		rainTransitionStartPercent =
			initialRainPercent;

		rainTransitionTimer =
			RainTransitionDuration;

		rainPhaseTimer =
			rainRandom.RandfRange(
				RainMinimumDuration,
				RainMaximumDuration
			);

		GD.Print(
			"RAIN CHANGE -> " +
			currentRainPercent.ToString("F0") +
			"% for " +
			rainPhaseTimer.ToString("F1") +
			"s"
		);
	}

	/// <summary>
	/// Creates the rain HUD nodes.
	/// </summary>
	public void SetupRainHud()
	{
		rainHudLayer =
			new CanvasLayer();

		rainHudLayer.Layer =
			20;

		rainHudLabel =
			new Label();

		rainHudLabel.Position =
			new Vector2(
				20.0f,
				20.0f
			);

		rainHudLabel.AddThemeFontSizeOverride(
			"font_size",
			22
		);

		rainHudLabel.Text =
			"RAIN  --%\nNEXT CHANGE --s";

		rainHudLayer.AddChild(
			rainHudLabel
		);

		owner.AddChild(
			rainHudLayer
		);
	}

	/// <summary>
	/// Updates the rain HUD text.
	/// </summary>
	public void UpdateRainHud()
	{
		if (
			rainHudLabel == null)
		{
			return;
		}

		float remaining =
			Mathf.Max(
				rainPhaseTimer,
				0.0f
			);

		float currentRainAmount =
			RainAmount *
			(currentRainPercent / 50.0f);

		rainHudLabel.Text =
			"RAIN  " +
			currentRainPercent.ToString("F0") +
			"%\nRATE  " +
			currentRainAmount.ToString("F0") +
			" / sec\nNEXT CHANGE  " +
			remaining.ToString("F0") +
			"s";
	}

	// ============================================================
	// Dynamic rain control
	// ============================================================

	/// <summary>
	/// Selects a new rain phase.
	/// </summary>
	public void SelectNewRainPhase()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		rainTransitionStartPercent =
			currentRainPercent;

		targetRainPercent =
			RainMinimumPercent +
			randomStep *
			RainPercentStep;

		rainTransitionTimer =
			0.0f;

		rainPhaseTimer =
			rainRandom.RandfRange(
				RainMinimumDuration,
				RainMaximumDuration
			);

		GD.Print(
			"RAIN CHANGE -> " +
			targetRainPercent.ToString("F0") +
			"% for " +
			rainPhaseTimer.ToString("F1") +
			"s (transition " +
			RainTransitionDuration.ToString("F1") +
			"s)"
		);
	}

	/// <summary>
	/// Updates rain phase progression when anti-lag is not active.
	/// </summary>
	public void UpdateDynamicRain(
		float dt)
	{
		if (
			AntiLagController != null &&
			AntiLagController.IsActive)
		{
			return;
		}

		rainPhaseTimer -=
			dt;

		if (
			rainPhaseTimer <=
			0.0f)
		{
			SelectNewRainPhase();
		}

		if (
			rainTransitionTimer <
			RainTransitionDuration)
		{
			rainTransitionTimer +=
				dt;

			if (
				rainTransitionTimer >
				RainTransitionDuration)
			{
				rainTransitionTimer =
					RainTransitionDuration;
			}

			float transitionProgress =
				rainTransitionTimer /
				RainTransitionDuration;

			currentRainPercent =
				Mathf.Lerp(
					rainTransitionStartPercent,
					targetRainPercent,
					transitionProgress
				);
		}
		else
		{
			currentRainPercent =
				targetRainPercent;
		}
	}

	/// <summary>
	/// Returns a random rain percent on the configured step grid.
	/// </summary>
	public int GetRandomRainPercent()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		return
			RainMinimumPercent +
			randomStep *
			RainPercentStep;
	}

	/// <summary>
	/// Returns a random phase duration using the original rain range.
	/// </summary>
	public float GetRandomPhaseDuration()
	{
		return rainRandom.RandfRange(
			RainMinimumDuration,
			RainMaximumDuration
		);
	}

	/// <summary>
	/// Clears the spawn accumulator.
	/// </summary>
	public void ResetSpawnAccumulator()
	{
		rainSpawnAccumulator =
			0.0f;
	}

	/// <summary>
	/// Records a capacity rejection.
	/// </summary>
	public void RegisterCapacityRejection()
	{
		rainRejectedByCapacity++;
	}

	/// <summary>
	/// Plans rain spawns for the current frame.
	/// </summary>
	public void PrepareRainSpawnRequests(
		float dt,
		int particleCount,
		int particleCapacity,
		PixelOccupancyGrid occupancyGrid,
		List<RainSpawnRequest> spawnRequests)
	{
		spawnRequests.Clear();

		UpdateDynamicRain(
			dt
		);

		if (
			AntiLagController != null &&
			AntiLagController.IsDrainingOrEvaporating)
		{
			return;
		}

		float currentRainAmount =
			RainAmount *
			(currentRainPercent / 50.0f);

		if (
			particleCount >=
			particleCapacity)
		{
			rainRejectedByCapacity++;

			return;
		}

		rainSpawnAccumulator +=
			currentRainAmount *
			dt;

		int spawnCount =
			(int)rainSpawnAccumulator;

		if (
			spawnCount <= 0)
		{
			return;
		}

		rainSpawnAccumulator -=
			spawnCount;

		int simulatedParticleCount =
			particleCount;

		for (
			int i = 0;
			i < spawnCount;
			i++)
		{
			if (
				simulatedParticleCount >=
				particleCapacity)
			{
				rainRejectedByCapacity++;

				break;
			}

			float x =
				rainRandom.RandfRange(
					worldMinX,
					worldMaxX
				);

			float y =
				RainSpawnY;

			int pixelIndex;

			if (
				!occupancyGrid.CanSpawnAtPixel(
					x,
					y,
					out pixelIndex
				))
			{
				rainRejectedByDensity++;

				continue;
			}

			spawnRequests.Add(
				new RainSpawnRequest(
					x,
					y,
					RainVelocityX,
					RainVelocityY,
					pixelIndex
				)
			);

			simulatedParticleCount++;
		}
	}

	/// <summary>
	/// Commits one successful rain spawn.
	/// </summary>
	public void RegisterSuccessfulRainSpawn(
		int pixelIndex,
		PixelOccupancyGrid occupancyGrid)
	{
		totalRainSpawns++;

		if (
			pixelIndex >= 0)
		{
			occupancyGrid.RegisterParticlePixel(
				pixelIndex
			);
		}
	}
}
