using Godot;

/// <summary>
/// Displays the current Energy value in the Label node this script is attached to.
/// The Label remains authored directly in Main.tscn; no extra child node is required.
/// </summary>
public partial class EnergyDisplay : Label
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UpdateDisplay();
    }

    public override void _Process(double delta)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        EnergySystem economy = EnergySystem.Instance;
        double energy = economy?.Energy ?? 0.0;

        Text = "Energy: " + System.Math.Floor(energy).ToString("F0");
        AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        AddThemeColorOverride("font_color", UiSettings.FontColorEnergy);
    }
}
