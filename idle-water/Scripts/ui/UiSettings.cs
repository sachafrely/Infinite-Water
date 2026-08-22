using Godot;

/// <summary>
/// Central configuration for the visual style of the game's UI.
/// UI scripts should reference these values instead of defining their own
/// copies of shared colors, borders, or font sizes.
/// </summary>
public static class UiSettings
{
	// Central UI overhaul palette.
	public static readonly Color BorderColor = new Color(0.92f, 0.96f, 1.0f, 1.0f);
	public static readonly Color WindowColor = new Color(0.82f, 0.82f, 0.84f, 0.80f);
	public static readonly Color ButtonPressedColor = new Color(0.94f, 0.94f, 0.95f, 1.0f);
	public static readonly Color ButtonUnpressedColor = new Color(0.52f, 0.52f, 0.54f, 1.0f);
	public static readonly Color ButtonWindowOpenColor = new Color(0.86f, 0.86f, 0.88f, 1.0f);
	public static readonly Color FontColorEnabled = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	public static readonly Color FontColorDisabled = new Color(0.25f, 0.25f, 0.27f, 1.0f);

	// Compatibility aliases for existing UI code.
	public static readonly Color ButtonColor = ButtonUnpressedColor;
	public static readonly Color DisplayBackgroundColor = new Color(0.30f, 0.30f, 0.30f, 0.96f);

	public const float BorderSize = 3.0f;
	public const int FontSizeBig = 30;
	public const int FontSizeMedium = 24;
	public const int FontSizeSmall = 20;
	public static readonly Color FontColorBasic = FontColorEnabled;
	public static readonly Color FontColorEnergy = new Color(1.0f, 0.75f, 0.25f, 1.0f);
	public static readonly Color FontColorWater = new Color(0.05f, 0.40f, 0.75f, 1.0f);
	public static readonly Color FontColorFps = new Color(0.40f, 0.40f, 0.40f, 1.0f);

	public static StyleBoxFlat CreateBox(Color background, Color? border = null, int borderWidth = -1)
	{
		StyleBoxFlat box = new StyleBoxFlat();
		box.BgColor = background;
		box.BorderColor = border ?? BorderColor;
		box.SetBorderWidthAll(borderWidth < 0 ? (int)BorderSize : borderWidth);
		box.SetCornerRadiusAll(0);
		return box;
	}

	public static void ApplyButtonTheme(Button button, bool windowOpen = false)
	{
		if (button == null)
			return;

		button.AddThemeStyleboxOverride("normal", CreateBox(windowOpen ? ButtonWindowOpenColor : ButtonUnpressedColor));
		button.AddThemeStyleboxOverride("hover", CreateBox(ButtonPressedColor));
		button.AddThemeStyleboxOverride("pressed", CreateBox(ButtonPressedColor));
		button.AddThemeStyleboxOverride("focus", CreateBox(ButtonPressedColor));
		button.AddThemeStyleboxOverride("disabled", CreateBox(ButtonUnpressedColor));
		button.AddThemeColorOverride("font_color", FontColorEnabled);
		button.AddThemeColorOverride("font_hover_color", FontColorEnabled);
		button.AddThemeColorOverride("font_pressed_color", FontColorEnabled);
		button.AddThemeColorOverride("font_focus_color", FontColorEnabled);
		button.AddThemeColorOverride("font_disabled_color", FontColorDisabled);
	}
}
