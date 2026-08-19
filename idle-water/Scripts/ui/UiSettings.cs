using Godot;

/// <summary>
/// Central configuration for the visual style of the game's UI.
/// UI scripts should reference these values instead of defining their own
/// copies of shared colors, borders, or font sizes.
/// </summary>
public static class UiSettings
{
	// ============================================================
	// SHARED UI COLORS
	// ============================================================

	/// <summary>
	/// Shared border color for windows and buttons.
	/// </summary>
	public static readonly Color BorderColor =
		new Color(0.75f, 0.75f, 0.75f, 1.0f);

	/// <summary>
	/// Background color of an open window.
	/// The corresponding open window button uses this same color.
	/// </summary>
	public static readonly Color WindowColor =
		new Color(0.16f, 0.16f, 0.17f, 0.97f);

	/// <summary>
	/// Background color of a closed/inactive button.
	/// </summary>
	public static readonly Color ButtonColor =
		new Color(0.07f, 0.07f, 0.08f, 1.0f);

	// ============================================================
	// SHARED UI DIMENSIONS
	// ============================================================

	public const float BorderSize = 2.0f;

	// ============================================================
	// SHARED FONT SIZES
	// ============================================================

	public const int FontSizeBig = 28;
	public const int FontSizeMedium = 22;
	public const int FontSizeSmall = 18;

	// ============================================================
	// SHARED FONT COLORS
	// ============================================================

	public static readonly Color FontColorBasic =
		new Color(1.0f, 1.0f, 1.0f, 1.0f);

	public static readonly Color FontColorEnergy =
		new Color(1.0f, 0.75f, 0.25f, 1.0f);

	public static readonly Color FontColorWater =
		new Color(0.05f, 0.40f, 0.75f, 1.0f);

	public static readonly Color FontColorFps =
		new Color(0.40f, 0.40f, 0.40f, 1.0f);

	// ============================================================
	// DERIVED UI COLORS
	// ============================================================

	/// <summary>
	/// Hover state for a closed button.
	/// The open button does not use this color; it uses WindowColor.
	/// </summary>
	public static Color ButtonHoverColor =>
		ButtonColor.Lightened(0.35f);
}
