/// <summary>
/// Shared runtime settings for device tilt.
///
/// The UI writes the accelerometer influence ratio here and the simulation
/// reads it from the same location.  This keeps the setting independent from
/// the visual SettingsContent control.
/// </summary>
public static class TiltSettings
{
	private static float tiltInfluenceRatio = 0.0f;

	/// <summary>
	/// Accelerometer influence from 0.0 (disabled) to 1.0 (full influence).
	/// </summary>
	public static float TiltInfluenceRatio
	{
		get => tiltInfluenceRatio;
		set => tiltInfluenceRatio = Godot.Mathf.Clamp(value, 0.0f, 1.0f);
	}

	public static void Reset()
	{
		TiltInfluenceRatio = 0.0f;
	}
}
