using Godot;

/// <summary>
/// Shared drawing functions for window-style UI backgrounds.
/// A UI-specific script such as TopUiBackground calls Draw() from its _Draw().
/// This file is intentionally not attached to a scene node.
/// </summary>
public static class RenderedWindowBackground
{
    public static void Draw(Control target)
    {
        if (target == null || target.Size.X <= 0.0f || target.Size.Y <= 0.0f)
            return;

        target.DrawRect(
            new Rect2(Vector2.Zero, target.Size),
            UiSettings.WindowBackgroundColor,
            true
        );

        float borderSize = UiSettings.BorderSize;
        if (borderSize <= 0.0f)
            return;

        Color borderColor = UiSettings.BorderColor;

        target.DrawRect(new Rect2(0, 0, target.Size.X, borderSize), borderColor, true);
        target.DrawRect(new Rect2(0, target.Size.Y - borderSize, target.Size.X, borderSize), borderColor, true);
        target.DrawRect(new Rect2(0, 0, borderSize, target.Size.Y), borderColor, true);
        target.DrawRect(new Rect2(target.Size.X - borderSize, 0, borderSize, target.Size.Y), borderColor, true);
    }
}
