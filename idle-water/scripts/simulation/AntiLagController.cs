using Godot;

/// <summary>
/// Owns the anti-lag cleanup state machine.
/// </summary>
internal sealed class AntiLagController
{
	// ============================================================
	// State
	// ============================================================

	private enum AntiLagState
	{
		Normal,
		ReducingRain,
		Draining,
		Evaporating,
		Recovering
	}

	private const double AntiLagFpsThreshold = 25.0;

	private const int AntiLagRequiredLowProfilerResults = 4;

	private const float AntiLagRainReductionDuration = 10.0f;

	private const float AntiLagDrainDuration = 20.0f;

	private const float AntiLagEvaporationDuration = 10.0f;

	private const float AntiLagRecoveryDuration = 10.0f;

	private AntiLagState antiLagState =
		AntiLagState.Normal;

	private float antiLagStateTimer = 0.0f;

	private float antiLagStateStartRainPercent = 0.0f;

	private float antiLagRecoveryTargetRainPercent = 0.0f;

	private int consecutiveLowProfilerResults = 0;

	private long totalEvaporatedParticles = 0;

	private long evaporatedParticlesThisCleanup = 0;

	private int antiLagCleanupCount = 0;

	private int antiLagEvaporationStartParticleCount = 0;

	private int antiLagEvaporationParticlesRemoved = 0;

	// ============================================================
	// Properties
	// ============================================================

	/// <summary>
	/// Gets whether anti-lag cleanup is active.
	/// </summary>
	public bool IsActive =>
		antiLagState != AntiLagState.Normal;

	/// <summary>
	/// Gets whether anti-lag is in the drain or evaporation phases.
	/// </summary>
	public bool IsDrainingOrEvaporating =>
		antiLagState == AntiLagState.Draining ||
		antiLagState == AntiLagState.Evaporating;

	/// <summary>
	/// Gets the total number of evaporated particles.
	/// </summary>
	public long TotalEvaporatedParticles =>
		totalEvaporatedParticles;

	/// <summary>
	/// Gets the number of particles evaporated in the current cleanup.
	/// </summary>
	public long EvaporatedParticlesThisCleanup =>
		evaporatedParticlesThisCleanup;

	/// <summary>
	/// Gets the number of completed anti-lag cleanups started so far.
	/// </summary>
	public int AntiLagCleanupCount =>
		antiLagCleanupCount;

	// ============================================================
	// Public entry points
	// ============================================================

	/// <summary>
	/// Evaluates one profiler result against the anti-lag threshold.
	/// </summary>
	public void EvaluateAntiLagProfilerResult(
		double profilerFps,
		ParticleData particles,
		RainSystem rainSystem)
	{
		if (
			antiLagState !=
			AntiLagState.Normal)
		{
			return;
		}

		if (
			profilerFps <
			AntiLagFpsThreshold)
		{
			consecutiveLowProfilerResults++;

			GD.Print(
				"ANTI-LAG CHECK: low profiler FPS " +
				profilerFps.ToString("F1") +
				" (" +
				consecutiveLowProfilerResults +
				"/" +
				AntiLagRequiredLowProfilerResults +
				")"
			);

			if (
				consecutiveLowProfilerResults >=
				AntiLagRequiredLowProfilerResults)
			{
				StartAntiLagCleanup(
					profilerFps,
					particles,
					rainSystem
				);
			}
		}
		else
		{
			if (
				consecutiveLowProfilerResults > 0)
			{
				GD.Print(
					"ANTI-LAG CHECK: FPS recovered to " +
					profilerFps.ToString("F1") +
					", resetting consecutive low-FPS count."
				);
			}

			consecutiveLowProfilerResults = 0;
		}
	}

	/// <summary>
	/// Advances the anti-lag state machine.
	/// </summary>
	public void UpdateAntiLagCleanup(
		float dt,
		ParticleData particles,
		RainSystem rainSystem)
	{
		if (
			antiLagState ==
			AntiLagState.Normal)
		{
			return;
		}

		antiLagStateTimer +=
			Mathf.Max(
				dt,
				0.0f
			);

		switch (
			antiLagState)
		{
			case AntiLagState.ReducingRain:
				UpdateAntiLagRainReduction(
					rainSystem
				);
				break;

			case AntiLagState.Draining:
				rainSystem.CurrentRainPercent = 0.0f;

				rainSystem.TargetRainPercent = 0.0f;

				rainSystem.RainPhaseTimer =
					Mathf.Max(
						AntiLagDrainDuration -
						antiLagStateTimer,
						0.0f
					);

				if (
					antiLagStateTimer >=
					AntiLagDrainDuration)
				{
					BeginAntiLagEvaporation(
						particles
					);
				}

				break;

			case AntiLagState.Evaporating:
				UpdateAntiLagEvaporation(
					dt,
					particles,
					rainSystem
				);
				break;

			case AntiLagState.Recovering:
				UpdateAntiLagRecovery(
					rainSystem
				);
				break;
		}
	}

	// ============================================================
	// Phases
	// ============================================================

	private void UpdateAntiLagRainReduction(
		RainSystem rainSystem)
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRainReductionDuration,
				0.0f,
				1.0f
			);

		rainSystem.CurrentRainPercent =
			Mathf.Lerp(
				antiLagStateStartRainPercent,
				0.0f,
				progress
			);

		rainSystem.TargetRainPercent =
			0.0f;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagRainReductionDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagStateTimer >=
			AntiLagRainReductionDuration)
		{
			antiLagState =
				AntiLagState.Draining;

			antiLagStateTimer =
				0.0f;

			rainSystem.CurrentRainPercent =
				0.0f;

			GD.Print(
				"ANTI-LAG: Rain reached 0%. Starting 20s natural drain."
			);
		}
	}

	private void BeginAntiLagEvaporation(
		ParticleData particles)
	{
		antiLagState =
			AntiLagState.Evaporating;

		antiLagStateTimer =
			0.0f;

		antiLagEvaporationStartParticleCount =
			particles.Count;

		antiLagEvaporationParticlesRemoved =
			0;

		GD.Print(
			"ANTI-LAG: Natural drain complete. Remaining particles=" +
			particles.Count +
			". Starting 10s evaporation."
		);

		GD.Print(
			"ANTI-LAG: Evaporation target=" +
			antiLagEvaporationStartParticleCount +
			" particles over " +
			AntiLagEvaporationDuration.ToString("F0") +
			"s."
		);
	}

	private void UpdateAntiLagEvaporation(
		float dt,
		ParticleData particles,
		RainSystem rainSystem)
	{
		rainSystem.CurrentRainPercent =
			0.0f;

		rainSystem.TargetRainPercent =
			0.0f;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagEvaporationDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagEvaporationStartParticleCount >
			0)
		{
			float progress =
				Mathf.Clamp(
					antiLagStateTimer /
					AntiLagEvaporationDuration,
					0.0f,
					1.0f
				);

			int targetRemoved =
				Mathf.FloorToInt(
					antiLagEvaporationStartParticleCount *
					progress
				);

			int particlesToRemove =
				targetRemoved -
				antiLagEvaporationParticlesRemoved;

			if (
				particlesToRemove > 0 &&
				particles.Count > 0)
			{
				particlesToRemove =
					System.Math.Min(
						particlesToRemove,
						particles.Count
					);

				for (
					int i = 0;
					i < particlesToRemove;
					i++)
				{
					particles.RemoveParticle(
						particles.Count - 1
					);

					evaporatedParticlesThisCleanup++;

					totalEvaporatedParticles++;

					antiLagEvaporationParticlesRemoved++;
				}
			}
		}

		if (
			antiLagStateTimer >=
			AntiLagEvaporationDuration)
		{
			while (
				particles.Count > 0)
			{
				particles.RemoveParticle(
					particles.Count - 1
				);

				evaporatedParticlesThisCleanup++;

				totalEvaporatedParticles++;
			}

			GD.Print(
				"========================================"
			);

			GD.Print(
				"ANTI-LAG EVAPORATION COMPLETE"
			);

			GD.Print(
				"Evaporation Start Particles=" +
				antiLagEvaporationStartParticleCount
			);

			GD.Print(
				"Evaporated Particles=" +
				evaporatedParticlesThisCleanup
			);

			GD.Print(
				"Total Evaporated Particles=" +
				totalEvaporatedParticles
			);

			GD.Print(
				"Remaining Particles=" +
				particles.Count
			);

			BeginAntiLagRecovery(
				rainSystem
			);
		}
	}

	private void BeginAntiLagRecovery(
		RainSystem rainSystem)
	{
		antiLagState =
			AntiLagState.Recovering;

		antiLagStateTimer =
			0.0f;

		antiLagRecoveryTargetRainPercent =
			rainSystem.GetRandomRainPercent();

		rainSystem.CurrentRainPercent =
			0.0f;

		rainSystem.TargetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainSystem.RainPhaseTimer =
			AntiLagRecoveryDuration;

		GD.Print(
			"ANTI-LAG: Recovery started. Target rain=" +
			antiLagRecoveryTargetRainPercent.ToString("F0") +
			"% over " +
			AntiLagRecoveryDuration.ToString("F0") +
			"s."
		);
	}

	private void UpdateAntiLagRecovery(
		RainSystem rainSystem)
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRecoveryDuration,
				0.0f,
				1.0f
			);

		rainSystem.CurrentRainPercent =
			Mathf.Lerp(
				0.0f,
				antiLagRecoveryTargetRainPercent,
				progress
			);

		rainSystem.TargetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagRecoveryDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagStateTimer >=
			AntiLagRecoveryDuration)
		{
			rainSystem.CurrentRainPercent =
				antiLagRecoveryTargetRainPercent;

			rainSystem.TargetRainPercent =
				antiLagRecoveryTargetRainPercent;

			antiLagState =
				AntiLagState.Normal;

			antiLagStateTimer =
				0.0f;

			rainSystem.RainPhaseTimer =
				rainSystem.GetRandomPhaseDuration();

			rainSystem.RainTransitionStartPercent =
				rainSystem.CurrentRainPercent;

			rainSystem.RainTransitionTimer =
				RainSystem.RainTransitionDuration;

			GD.Print(
				"ANTI-LAG CLEANUP COMPLETE. Returning to normal rain."
			);

			GD.Print(
				"========================================"
			);
		}
	}

	private void StartAntiLagCleanup(
		double triggerProfilerFps,
		ParticleData particles,
		RainSystem rainSystem)
	{
		antiLagCleanupCount++;

		consecutiveLowProfilerResults = 0;

		antiLagState =
			AntiLagState.ReducingRain;

		antiLagStateTimer =
			0.0f;

		antiLagStateStartRainPercent =
			rainSystem.CurrentRainPercent;

		rainSystem.ResetSpawnAccumulator();

		evaporatedParticlesThisCleanup =
			0;

		antiLagEvaporationStartParticleCount =
			0;

		antiLagEvaporationParticlesRemoved =
			0;

		GD.Print(
			"========================================"
		);

		GD.Print(
			"ANTI-LAG CLEANUP #" +
			antiLagCleanupCount +
			" STARTED"
		);

		GD.Print(
			"Trigger profiler Frame FPS=" +
			triggerProfilerFps.ToString("F1")
		);

		GD.Print(
			"Starting ActiveParticles=" +
			particles.Count
		);

		GD.Print(
			"Rain reducing from " +
			antiLagStateStartRainPercent.ToString("F1") +
			"% to 0% over " +
			AntiLagRainReductionDuration.ToString("F0") +
			"s."
		);
	}
}
