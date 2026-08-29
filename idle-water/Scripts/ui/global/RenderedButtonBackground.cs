using Godot;

/// <summary>
/// Shared button styling used by the UI.
/// Visual settings come from UiSettings so buttons keep one consistent style.
/// </summary>
public static class RenderedButtonBackground
{
    public static void Apply(Button button)
    {
        if (button == null)
            return;

        button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));

        button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        button.AddThemeColorOverride("font_color", UiSettings.FontColorEnabled);
        button.AddThemeColorOverride("font_hover_color", UiSettings.FontColorEnabled);
        button.AddThemeColorOverride("font_pressed_color", UiSettings.FontColorEnabled);
        button.AddThemeColorOverride("font_focus_color", UiSettings.FontColorEnabled);
        button.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
    }
}
