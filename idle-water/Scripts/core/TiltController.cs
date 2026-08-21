using Godot;

/// <summary>
/// Reads the device accelerometer and converts its tilt into a 2D gravity
/// acceleration for the water simulation.
///
/// The configured TiltSettings.TiltInfluenceRatio controls the maximum
/// simulated tilt angle. At 0% gravity is straight down. At 100% the
/// measured tilt can reach the configured maximum of 50 degrees.
/// </summary>
public sealed class TiltController
{
	private const float GravityMagnitude = 300.0f;
	private const float MaximumTiltDegrees = 50.0f;
	private const float SensorMinimumMagnitude = 1.0f;
	private const float SensorSmoothing = 0.15f;

	// GravityIndicator artwork points right at 0 degrees.
	// -270 degrees is therefore the authored rotation for pointing down.
	private const float GravityIndicatorRotationOffset = -Mathf.Pi * 1.5f;

	private Vector3 smoothedAccelerometer = Vector3.Zero;
	private bool hasSensorSample;

	/// <summary>
	/// Gravity acceleration calculated from the latest tilt sample.
	/// The PBF coordinator reads this instead of receiving a velocity change.
	/// </summary>
	public static Vector2 CurrentGravityAcceleration { get; private set; } =
		Vector2.Down * GravityMagnitude;

	public Vector2 GravityAcceleration =>
		CurrentGravityAcceleration;

	public Vector2 GravityDirection { get; private set; } =
		Vector2.Down;

	public void Update(float delta)
	{
		float influence = TiltSettings.TiltInfluenceRatio;

		if (influence <= 0.0001f)
		{
			GravityDirection = Vector2.Down;
			CurrentGravityAcceleration = Vector2.Down * GravityMagnitude;
			UpdateGravityIndicatorVisual();
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
				float smoothing = Mathf.Clamp(
					SensorSmoothing * delta * 60.0f,
					0.0f,
					1.0f
				);

				smoothedAccelerometer =
					smoothedAccelerometer.Lerp(accelerometer, smoothing);
			}
		}

		if (!hasSensorSample)
		{
			GravityDirection = Vector2.Down;
			CurrentGravityAcceleration = Vector2.Down * GravityMagnitude;
			UpdateGravityIndicatorVisual();
			return;
		}

		Vector2 sensorDirection = new Vector2(
			smoothedAccelerometer.X,
			-smoothedAccelerometer.Y
		);

		if (sensorDirection.LengthSquared() < 0.0001f)
		{
			GravityDirection = Vector2.Down;
			CurrentGravityAcceleration = Vector2.Down * GravityMagnitude;
			UpdateGravityIndicatorVisual();
			return;
		}

		sensorDirection = sensorDirection.Normalized();

		float sensorAngle = Mathf.Atan2(
			sensorDirection.X,
			sensorDirection.Y
		);

		float maximumTiltRadians =
			Mathf.DegToRad(MaximumTiltDegrees);

		float simulatedAngle = Mathf.Clamp(
			sensorAngle * influence,
			-maximumTiltRadians,
			maximumTiltRadians
		);

		GravityDirection = new Vector2(
			Mathf.Sin(simulatedAngle),
			Mathf.Cos(simulatedAngle)
		);

		CurrentGravityAcceleration =
			GravityDirection * GravityMagnitude;

		UpdateGravityIndicatorVisual();
	}

	/// <summary>
	/// Rotates the Sprite2D named "GravityIndicator" to match the actual
	/// gravity vector used by the simulation. The lookup is recursive so the
	/// indicator can live anywhere in the UI hierarchy.
	/// </summary>
	private void UpdateGravityIndicatorVisual()
	{
		SceneTree tree =
			Engine.GetMainLoop() as SceneTree;

		if (tree == null || tree.Root == null)
		{
			return;
		}

		Sprite2D indicator =
			FindGravityIndicator(tree.Root);

		if (indicator == null)
		{
			return;
		}

		if (GravityDirection.LengthSquared() < 0.0001f)
		{
			return;
		}

		// The artwork points right at 0 degrees and down at -270 degrees.
		// Add the gravity direction angle to that authored down rotation.
		indicator.Rotation =
			GravityDirection.Angle() +
			GravityIndicatorRotationOffset;
	}

	private static Sprite2D FindGravityIndicator(Node node)
	{
		if (node is Sprite2D sprite && sprite.Name == "GravityIndicator")
		{
			return sprite;
		}

		foreach (Node child in node.GetChildren())
		{
			Sprite2D result =
				FindGravityIndicator(child);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}
}
