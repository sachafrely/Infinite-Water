using Godot;

/// <summary>
/// Central configuration for the visual style of the game's UI.
/// UI scripts should reference these values instead of defining their own
/// copies of shared colors, borders, or font sizes.
/// </summary>
public static class UiSettings
{
	// Shared window styling.
	public static readonly Color BorderColor = new Color(0.30f, 0.30f, 0.30f, 1.0f);
	public static readonly Color WindowBackgroundColor = new Color(0.50f, 0.50f, 0.50f, 1.00f);
	public static readonly Color ButtonPressedColor = new Color(0.60f, 0.60f, 0.60f, 1.0f);
	public static readonly Color ButtonUnpressedColor = new Color(0.40f, 0.40f, 0.40f, 1.0f);
	public static readonly Color FontColorEnabled = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	public static readonly Color FontColorDisabled = new Color(0.30f, 0.30f, 0.30f, 1.0f);

	// Compatibility aliases for existing UI code. These keep the first pass
	// safe while callers are migrated to the more explicit names.
	public static readonly Color WindowColor = WindowBackgroundColor;
	public static readonly Color ButtonColor = ButtonUnpressedColor;
	public static readonly Color FontColorBasic = FontColorEnabled;

	public static readonly Color DisplayBackgroundColor = new Color(0.30f, 0.30f, 0.30f, 0.96f);
	public const float BorderSize = 3.0f;
	public const int ButtonBorderSize = 3;
	public const int FontSizeBig = 30;
	public const int FontSizeMedium = 24;
	public const int FontSizeSmall = 20;
	public static readonly Color FontColorEnergy = new Color(1.0f, 0.75f, 0.25f, 1.0f);
	public static readonly Color FontColorWater = new Color(0.05f, 0.40f, 0.75f, 1.0f);
	public static readonly Color FontColorFps = new Color(0.40f, 0.40f, 0.40f, 1.0f);

	public static StyleBoxFlat CreateBox(Color background, Color? border = null, int borderWidth = (int)BorderSize)
	{
		StyleBoxFlat box = new StyleBoxFlat();
		box.BgColor = background;
		box.BorderColor = border ?? BorderColor;
		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(0);
		return box;
	}
}
