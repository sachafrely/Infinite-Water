using Godot;

/// <summary>
/// Central configuration for the visual style of the game's UI.
/// UI scripts should reference these values instead of defining their own
/// copies of shared colors, borders, or font sizes.
/// </summary>
public static class UiSettings
{
	public static readonly Color BorderColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
	public static readonly Color WindowColor = new Color(0.16f, 0.16f, 0.17f, 0.50f);
	public static readonly Color ButtonColor = new Color(0.07f, 0.07f, 0.08f, 1.0f);
	public static readonly Color DisplayBackgroundColor = new Color(0.30f, 0.30f, 0.30f, 0.96f);
	public const float BorderSize = 2.0f;
	public const int FontSizeBig = 28;
	public const int FontSizeMedium = 22;
	public const int FontSizeSmall = 18;
	public static readonly Color FontColorBasic = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	public static readonly Color FontColorEnergy = new Color(1.0f, 0.75f, 0.25f, 1.0f);
	public static readonly Color FontColorWater = new Color(0.05f, 0.40f, 0.75f, 1.0f);
	public static readonly Color FontColorFps = new Color(0.40f, 0.40f, 0.40f, 1.0f);

	public static StyleBoxFlat CreateBox(Color background, Color? border = null, int borderWidth = 2)
	{
		StyleBoxFlat box = new StyleBoxFlat();
		box.BgColor = background;
		box.BorderColor = border ?? BorderColor;
		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(0);
		return box;
	}
}
