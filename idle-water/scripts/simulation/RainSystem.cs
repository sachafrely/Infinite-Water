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
	public const float RainVelocityY = 300.0f;

	// ============================================================
	// Diagnostics
	// ============================================================

	private const int DiagnosticMaxSpawnPrints = 10;

	private int diagnosticSpawnPrintCount = 0;

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

	public AntiLagController AntiLagController
	{
		private get;
		set;
	}

	public float CurrentRainPercent
	{
		get
		{
			return currentRainPercent;
		}
		internal set
		{
			currentRainPercent = value;
		}
	}

	public float TargetRainPercent
	{
		get
		{
			return targetRainPercent;
		}
		internal set
		{
			targetRainPercent = value;
		}
	}

	public float RainTransitionStartPercent
	{
		get
		{
			return rainTransitionStartPercent;
		}
		internal set
		{
			rainTransitionStartPercent = value;
		}
	}

	public float RainTransitionTimer
	{
		get
		{
			return rainTransitionTimer;
		}
		internal set
		{
			rainTransitionTimer = value;
		}
	}

	public float RainPhaseTimer
	{
		get
		{
			return rainPhaseTimer;
		}
		internal set
		{
			rainPhaseTimer = value;
		}
	}

	public long TotalRainSpawns =>
		totalRainSpawns;

	public long RainRejectedByCapacity =>
		rainRejectedByCapacity;

	public long RainRejectedByDensity =>
		rainRejectedByDensity;

	// ============================================================
	// Construction
	// ============================================================

	public RainSystem(
		Node owner,
		float worldMinX,
		float worldMaxX)
	{
		this.owner = owner;

		this.worldMinX = worldMinX;
		this.worldMaxX = worldMaxX;

		rainRandom.Randomize();

		// --------------------------------------------------------
		// Diagnostic: rain coordinate space.
		// --------------------------------------------------------

		GD.Print(
			"========== RAIN SPACE DIAGNOSTIC =========="
		);

		GD.Print(
			"Rain spawn X range: " +
			worldMinX.ToString("F1") +
			" -> " +
			worldMaxX.ToString("F1")
		);

		GD.Print(
			"Rain spawn Y: " +
			RainSpawnY.ToString("F1")
		);

		GD.Print(
			"Rain velocity: " +
			RainVelocityX.ToString("F1") +
			", " +
			RainVelocityY.ToString("F1")
		);

		GD.Print(
			"============================================"
		);
	}

	// ============================================================
	// Setup
	// ============================================================

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

		currentRainPercent = initialRainPercent;
		targetRainPercent = initialRainPercent;
		rainTransitionStartPercent = initialRainPercent;
		rainTransitionTimer = RainTransitionDuration;

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

	public void SetupRainHud()
	{
		rainHudLayer =
			new CanvasLayer();

		rainHudLayer.Layer = 20;

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

	public void UpdateRainHud()
	{
		if (rainHudLabel == null)
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

		rainTransitionTimer = 0.0f;

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

	public void UpdateDynamicRain(
		float dt)
	{
		if (
			AntiLagController != null &&
			AntiLagController.IsActive)
		{
			return;
		}

		rainPhaseTimer -= dt;

		if (rainPhaseTimer <= 0.0f)
		{
			SelectNewRainPhase();
		}

		if (
			rainTransitionTimer <
			RainTransitionDuration)
		{
			rainTransitionTimer += dt;

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

	public float GetRandomPhaseDuration()
	{
		return rainRandom.RandfRange(
			RainMinimumDuration,
			RainMaximumDuration
		);
	}

	public void ResetSpawnAccumulator()
	{
		rainSpawnAccumulator = 0.0f;
	}

	public void RegisterCapacityRejection()
	{
		rainRejectedByCapacity++;
	}

	// ============================================================
	// Spawn planning
	// ============================================================

	public void PrepareRainSpawnRequests(
		float dt,
		int particleCount,
		int particleCapacity,
		PixelOccupancyGrid occupancyGrid,
		List<RainSpawnRequest> spawnRequests)
	{
		spawnRequests.Clear();

		UpdateDynamicRain(dt);

		if (
			AntiLagController != null &&
			AntiLagController.IsDrainingOrEvaporating)
		{
			return;
		}

		float currentRainAmount =
			RainAmount *
			(currentRainPercent / 50.0f);

		if (particleCount >= particleCapacity)
		{
			rainRejectedByCapacity++;
			return;
		}

		rainSpawnAccumulator +=
			currentRainAmount *
			dt;

		int spawnCount =
			(int)rainSpawnAccumulator;

		if (spawnCount <= 0)
		{
			return;
		}

		rainSpawnAccumulator -= spawnCount;

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

			// ----------------------------------------------------
			// Diagnostic: actual spawn coordinates.
			// ----------------------------------------------------

			if (
				diagnosticSpawnPrintCount <
				DiagnosticMaxSpawnPrints)
			{
				GD.Print(
					"RAIN SPAWN DIAGNOSTIC #" +
					(diagnosticSpawnPrintCount + 1) +
					": X=" +
					x.ToString("F1") +
					" Y=" +
					y.ToString("F1") +
					" Range=" +
					worldMinX.ToString("F1") +
					" -> " +
					worldMaxX.ToString("F1")
				);

				diagnosticSpawnPrintCount++;
			}

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

	// ============================================================
	// Successful spawn
	// ============================================================

	public void RegisterSuccessfulRainSpawn(
		int pixelIndex,
		PixelOccupancyGrid occupancyGrid)
	{
		totalRainSpawns++;

		if (pixelIndex >= 0)
		{
			occupancyGrid.RegisterParticlePixel(
				pixelIndex
			);
		}
	}
}
