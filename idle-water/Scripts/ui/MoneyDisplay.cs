using Godot;

/// <summary>
/// Displays the current Dollars value in the Label node this script is attached to.
/// The Label remains authored directly in Main.tscn; no extra child node is required.
/// </summary>
public partial class MoneyDisplay : Label
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
        double dollars = economy?.Dollars ?? 0.0;

        Text = "Dollars: $" + System.Math.Floor(dollars).ToString("F0");
        AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        AddThemeColorOverride("font_color", UiSettings.FontColorBasic);
    }
}
