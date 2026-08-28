using Godot;

/// <summary>
/// Draws the shared rectangular background used by rendered UI containers.
/// Styling comes exclusively from UiSettings.
/// </summary>
public partial class RenderedButtonBackground : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0.0f || Size.Y <= 0.0f)
            return;

        Rect2 rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, UiSettings.WindowBackgroundColor, true);
        DrawRect(rect, UiSettings.BorderColor, false, UiSettings.BorderSize);
    }
}
