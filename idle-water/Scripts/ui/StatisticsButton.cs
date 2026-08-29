using Godot;

/// <summary>
/// Opens or closes the StatisticsWindow through the central window manager.
/// </summary>
public partial class StatisticsButton : Button
{
    public override void _Ready()
    {
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        UiWindowManager manager = GetTree().CurrentScene?.FindChild("UiWindowManager", true, false) as UiWindowManager;
        manager?.ToggleStatistics();
    }
}
