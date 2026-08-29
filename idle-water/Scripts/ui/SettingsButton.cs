using Godot;

/// <summary>
/// Opens or closes the SettingsWindow through the central window manager.
/// </summary>
public partial class SettingsButton : Button
{
    public override void _Ready()
    {
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        UiWindowManager manager = GetTree().CurrentScene?.FindChild("UiWindowManager", true, false) as UiWindowManager;
        manager?.ToggleSettings();
    }
}
