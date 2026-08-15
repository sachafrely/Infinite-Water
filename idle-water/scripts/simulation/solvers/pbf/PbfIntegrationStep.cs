using Godot;
using System.Runtime.CompilerServices;

/// <summary>
/// PbfIntegrationStep — finalizes the PBF tick by deriving velocities from
/// predicted positions and committing those positions as authoritative.
///
/// Responsibilities:
/// <list type="bullet">
///   <item>Derive velocity: v = (predPos − oldPos) / dt × damping.</item>
///   <item>Apply world-boundary velocity effects (restitution, friction).</item>
///   <item>Apply surface-flow gravity projection on impact normals.</item>
///   <item>Apply impact damping and ground drag.</item>
///   <item>Apply sleep behaviour (progressive velocity damping → zero).</item>
///   <item>Write final vel and pos back to <see cref="ParticleData"/>.</item>
/// </list>
/// </summary>
internal static class PbfIntegrationStep
{
	/// <summary>
	/// Runs the full velocity-integration and position-commit pass for all
	/// particles.
	/// </summary>
	public static void Finalize(
		ParticleData particles,
		float dt,
		int count,
		PbfState state)
	{
		float[] posX = particles.PosX;
		float[] posY = particles.PosY;
		float[] velX = particles.VelX;
		float[] velY = particles.VelY;
		float[] predX = particles.PredX;
		float[] predY = particles.PredY;

		bool[] impacted = state.Impacted;
		float[] impactNormalX = state.ImpactNormalX;
		float[] impactNormalY = state.ImpactNormalY;
		float[] sleepProgress = state.SleepProgress;
		bool[] sleeping = state.Sleeping;

		float inverseDt =
			1.0f / dt;

		float boundaryLeft =
			PbfSolver.MinX +
			PbfSolver.BoundarySkin;

		float boundaryRight =
			PbfSolver.MaxX -
			PbfSolver.BoundarySkin;

		float boundaryTop =
			PbfSolver.MinY +
			PbfSolver.BoundarySkin;

		float boundaryBottom =
			PbfSolver.MaxY -
			PbfSolver.BoundarySkin;

		float damping =
			PbfSolver.VelocityDamping;

		float inverseBoundaryFriction =
			1.0f - PbfSolver.BoundaryFriction;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float oldX = posX[i];
			float oldY = posY[i];

			float finalVelocityX =
				(predX[i] - oldX) *
				inverseDt *
				damping;

			float finalVelocityY =
				(predY[i] - oldY) *
				inverseDt *
				damping;

			float x = predX[i];
			float y = predY[i];

			// --------------------------------------------------------
			// World boundary velocity effects
			// --------------------------------------------------------

			if (x <= boundaryLeft + 0.001f)
			{
				if (finalVelocityX < 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityX) <
						PbfSolver.BoundaryVelocityEpsilon)
					{
						finalVelocityX = 0.0f;
					}
					else
					{
						finalVelocityX =
							-finalVelocityX *
							PbfSolver.BoundaryRestitution;
					}
				}

				finalVelocityY *=
					inverseBoundaryFriction;
			}
			else if (x >= boundaryRight)
			{
				if (finalVelocityX > 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityX) <
						PbfSolver.BoundaryVelocityEpsilon)
					{
						finalVelocityX = 0.0f;
					}
					else
					{
						finalVelocityX =
							-finalVelocityX *
							PbfSolver.BoundaryRestitution;
					}
				}

				finalVelocityY *=
					inverseBoundaryFriction;
			}

			if (y <= boundaryTop + 0.001f)
			{
				if (finalVelocityY < 0.0f)
				{
					finalVelocityY =
						-finalVelocityY *
						PbfSolver.BoundaryRestitution;
				}

				finalVelocityX *=
					inverseBoundaryFriction;
			}
			else if (y >= boundaryBottom - 0.001f)
			{
				if (finalVelocityY > 0.0f)
				{
					finalVelocityY =
						-finalVelocityY *
						PbfSolver.BoundaryRestitution;
				}

				finalVelocityX *=
					inverseBoundaryFriction;
			}

			// --------------------------------------------------------
			// Surface flow and impact damping
			// --------------------------------------------------------

			if (impacted[i])
			{
				ApplySurfaceFlow(
					i,
					dt,
					impactNormalX,
					impactNormalY,
					ref finalVelocityX,
					ref finalVelocityY
				);

				ApplyImpactDamping(
					i,
					impactNormalX,
					impactNormalY,
					ref finalVelocityX,
					ref finalVelocityY
				);
			}

			// --------------------------------------------------------
			// Sleep
			// --------------------------------------------------------

			ApplySleepBehavior(
				i,
				dt,
				impacted,
				impactNormalY,
				sleepProgress,
				sleeping,
				ref finalVelocityX,
				ref finalVelocityY
			);

			velX[i] = finalVelocityX;
			velY[i] = finalVelocityY;
			posX[i] = predX[i];
			posY[i] = predY[i];
		}
	}

	// ============================================================
	// Surface flow
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ApplySurfaceFlow(
		int i,
		float dt,
		float[] impactNormalX,
		float[] impactNormalY,
		ref float velocityX,
		ref float velocityY)
	{
		float normalX = impactNormalX[i];
		float normalY = impactNormalY[i];

		float normalLengthSquared =
			normalX * normalX +
			normalY * normalY;

		if (
			normalLengthSquared <=
			PbfSolver.ImpactNormalEpsilon)
		{
			return;
		}

		float inverseLength =
			1.0f /
			Mathf.Sqrt(normalLengthSquared);

		normalX *= inverseLength;
		normalY *= inverseLength;

		float gravityNormal =
			PbfSolver.Gravity * normalY;

		float tangentGravityX =
			-normalX * gravityNormal;

		float tangentGravityY =
			PbfSolver.Gravity -
			normalY * gravityNormal;

		float scale =
			dt *
			PbfSolver.SurfaceGravityRetention;

		velocityX += tangentGravityX * scale;
		velocityY += tangentGravityY * scale;
	}

	// ============================================================
	// Impact damping
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ApplyImpactDamping(
		int i,
		float[] impactNormalX,
		float[] impactNormalY,
		ref float velocityX,
		ref float velocityY)
	{
		float normalX = impactNormalX[i];
		float normalY = impactNormalY[i];

		float normalVelocity =
			velocityX * normalX +
			velocityY * normalY;

		if (normalVelocity > 0.0f)
		{
			float velocityChange =
				normalVelocity *
				PbfSolver.ImpactDamping;

			velocityX -= normalX * velocityChange;
			velocityY -= normalY * velocityChange;
		}

		// GroundStick is 0.0f — skip ground-stick pass.

		float tangentX = -normalY;
		float tangentY =  normalX;

		float tangentialVelocity =
			velocityX * tangentX +
			velocityY * tangentY;

		float drag =
			tangentialVelocity *
			PbfSolver.GroundDrag;

		velocityX -= tangentX * drag;
		velocityY -= tangentY * drag;
	}

	// ============================================================
	// Sleep behaviour
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ApplySleepBehavior(
		int i,
		float dt,
		bool[] impacted,
		float[] impactNormalY,
		float[] sleepProgress,
		bool[] sleeping,
		ref float velocityX,
		ref float velocityY)
	{
		float velocitySquared =
			velocityX * velocityX +
			velocityY * velocityY;

		if (
			velocitySquared >=
			PbfSolver.WakeVelocityThresholdSquared)
		{
			sleepProgress[i] = 0.0f;
			sleeping[i] = false;
			return;
		}

		if (impacted[i])
		{
			float normalY =
				Mathf.Abs(impactNormalY[i]);

			if (
				normalY <
				PbfSolver.HorizontalSurfaceNormalY)
			{
				sleepProgress[i] = 0.0f;
				sleeping[i] = false;
				return;
			}
		}

		if (
			velocitySquared <
			PbfSolver.SleepVelocityThresholdSquared)
		{
			float progress =
				sleepProgress[i] +
				dt / PbfSolver.SleepTime;

			if (progress > 1.0f)
				progress = 1.0f;

			sleepProgress[i] = progress;

			float dampingFactor =
				1.0f -
				PbfSolver.SleepDampingStrength *
				progress *
				dt;

			if (dampingFactor < 0.0f)
				dampingFactor = 0.0f;

			velocityX *= dampingFactor;
			velocityY *= dampingFactor;

			if (progress >= 1.0f)
			{
				sleeping[i] = true;
				velocityX = 0.0f;
				velocityY = 0.0f;
			}

			return;
		}

		float newProgress =
			sleepProgress[i] -
			dt / PbfSolver.SleepTime;

		if (newProgress < 0.0f)
			newProgress = 0.0f;

		sleepProgress[i] = newProgress;
		sleeping[i] = false;
	}
}
