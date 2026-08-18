using Godot;

/// <summary>
/// Reads the device accelerometer and converts its tilt into a 2D gravity
/// direction for the water simulation.
///
/// The configured TiltSettings.TiltInfluenceRatio blends the measured device
/// direction with normal downward gravity. At 0% the simulation is unchanged.
/// At 100% the measured device tilt is used at full strength.
/// </summary>
public sealed class TiltController
{
	private const float GravityMagnitude = 300.0f;
	private const float SensorMinimumMagnitude = 1.0f;
	private const float SensorSmoothing = 0.15f;

	private Vector3 smoothedAccelerometer = Vector3.Zero;
	private bool hasSensorSample;

	public Vector2 GravityDirection { get; private set; } = Vector2.Down;

	public void Update(float delta)
	{
		float influence = TiltSettings.TiltInfluenceRatio;

		if (influence <= 0.0001f)
		{
			GravityDirection = Vector2.Down;
			return;
		}

		Vector3 accelerometer = Input.GetAccelerometer();

		if (accelerometer.LengthSquared() >= SensorMinimumMagnitude * SensorMinimumMagnitude)
		{
			if (!hasSensorSample)
			{
				smoothedAccelerometer = accelerometer;
				hasSensorSample = true;
			}
			else
			{
				float smoothing = Mathf.Clamp(SensorSmoothing * delta * 60.0f, 0.0f, 1.0f);
				smoothedAccelerometer = smoothedAccelerometer.Lerp(
					accelerometer,
					smoothing
				);
			}
		}

		if (!hasSensorSample)
		{
			GravityDirection = Vector2.Down;
			return;
		}

		// Android's default sensor axes use +X to the right and +Y upward.
		// For the portrait game, gravity therefore maps to +X and -Y in the
		// simulation's 2D coordinates.
		Vector2 sensorDirection = new Vector2(
			smoothedAccelerometer.X,
			-smoothedAccelerometer.Y
		);

		if (sensorDirection.LengthSquared() < 0.0001f)
		{
			// When the phone is nearly flat, the screen-plane projection is
			// undefined. Keep the normal downward direction in that case.
			sensorDirection = Vector2.Down;
		}
		else
		{
			sensorDirection = sensorDirection.Normalized();
		}

		GravityDirection = Vector2.Down.Lerp(
			sensorDirection,
			influence
		).Normalized();
	}

	/// <summary>
	/// Applies the current tilt gravity as an acceleration delta before the
	/// normal PBF solver applies its existing downward gravity.
	/// </summary>
	public void ApplyToParticles(ParticleData particles, float delta)
	{
		if (particles.Count <= 0 || delta <= 0.0f)
			return;

		Vector2 desiredGravity = GravityDirection * GravityMagnitude;

		// PbfSolver already applies (0, GravityMagnitude). Add only the
		// difference so normal gravity is not applied twice.
		float deltaGravityX = desiredGravity.X;
		float deltaGravityY = desiredGravity.Y - GravityMagnitude;

		for (int i = 0; i < particles.Count; i++)
		{
			particles.VelX[i] += deltaGravityX * delta;
			particles.VelY[i] += deltaGravityY * delta;
		}
	}
}
