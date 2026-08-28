using Godot;

/// <summary>
/// Displays the current Energy value using the Label authored in the TopUi scene.
/// </summary>
public partial class EnergyDisplay : Control
{
    private Label energyLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        energyLabel = GetNodeOrNull<Label>("EnergyLabel")
            ?? FindChild("EnergyLabel", true, false) as Label
            ?? FindFirstLabel();

        if (energyLabel == null)
            GD.PushWarning("EnergyDisplay: No Energy Label was found in the authored scene.");
    }

    public override void _Process(double delta)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (energyLabel == null)
            return;

        EnergySystem economy = EnergySystem.Instance;
        double energy = economy?.Energy ?? 0.0;

        energyLabel.Text = "Energy: " + System.Math.Floor(energy).ToString("F0");
        energyLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        energyLabel.AddThemeColorOverride("font_color", UiSettings.FontColorEnergy);
    }

    private Label FindFirstLabel()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Label directLabel)
                return directLabel;

            Godot.Collections.Array<Node> labels = child.FindChildren("*", "Label", true, false);
            if (labels.Count > 0 && labels[0] is Label label)
                return label;
        }

        return null;
    }
}
