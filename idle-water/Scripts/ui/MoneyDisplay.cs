using Godot;

/// <summary>
/// Displays the current Dollars value using the Label authored in the TopUi scene.
/// </summary>
public partial class MoneyDisplay : Control
{
    private Label moneyLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        moneyLabel = GetNodeOrNull<Label>("DollarsLabel")
            ?? GetNodeOrNull<Label>("MoneyLabel")
            ?? FindChild("DollarsLabel", true, false) as Label
            ?? FindChild("MoneyLabel", true, false) as Label
            ?? FindFirstLabel();

        if (moneyLabel == null)
            GD.PushWarning("MoneyDisplay: No Dollars/Money Label was found in the authored scene.");
    }

    public override void _Process(double delta)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (moneyLabel == null)
            return;

        EnergySystem economy = EnergySystem.Instance;
        double dollars = economy?.Dollars ?? 0.0;

        moneyLabel.Text = "Dollars: $" + System.Math.Floor(dollars).ToString("F0");
        moneyLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        moneyLabel.AddThemeColorOverride("font_color", UiSettings.FontColorBasic);
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
