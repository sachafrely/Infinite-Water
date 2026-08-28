using System.Reflection;
using Godot;

/// <summary>
/// Displays the current rain amount using the Nodes authored in Main.tscn.
/// No rain UI hierarchy is created at runtime here.
/// </summary>
public partial class RainDisplay : Node
{
    private FieldInfo rainSystemField;
    private PropertyInfo rainPercentProperty;
    private Label rainLabel;

    public override void _Ready()
    {
        CacheRainAccess();
        rainLabel = GetNodeOrNull<Label>("RainLabel")
            ?? FindChild("RainLabel", true, false) as Label
            ?? FindFirstLabel();
    }

    public override void _Process(double delta)
    {
        float rainPercent = GetCurrentRainPercent();

        if (rainLabel != null)
        {
            rainLabel.Text = $"Rain: {Mathf.RoundToInt(rainPercent)}%";
            rainLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        }

        UpdateProgressBars(rainPercent);
    }

    private void CacheRainAccess()
    {
        try
        {
            rainSystemField = typeof(FluidSimulator).GetField(
                "rainSystem",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (rainSystemField != null)
                rainPercentProperty = rainSystemField.FieldType.GetProperty("CurrentRainPercent");
        }
        catch
        {
            rainSystemField = null;
            rainPercentProperty = null;
        }
    }

    private FluidSimulator FindSimulator()
    {
        Node root = GetTree().CurrentScene;
        return root?.FindChild("FluidSimulation", true, false) as FluidSimulator;
    }

    private float GetCurrentRainPercent()
    {
        FluidSimulator simulator = FindSimulator();
        if (simulator == null || rainSystemField == null || rainPercentProperty == null)
            return 0.0f;

        try
        {
            object rainSystem = rainSystemField.GetValue(simulator);
            object value = rainSystem == null ? null : rainPercentProperty.GetValue(rainSystem);
            return value is float percent ? Mathf.Clamp(percent, 0.0f, 100.0f) : 0.0f;
        }
        catch
        {
            return 0.0f;
        }
    }

    private void UpdateProgressBars(float rainPercent)
    {
        foreach (Node node in GetChildren())
        {
            UpdateProgressBarRecursive(node, rainPercent);
        }
    }

    private void UpdateProgressBarRecursive(Node node, float rainPercent)
    {
        if (node is ProgressBar progressBar)
        {
            progressBar.Value = rainPercent;
            return;
        }

        foreach (Node child in node.GetChildren())
            UpdateProgressBarRecursive(child, rainPercent);
    }

    private Label FindFirstLabel()
    {
        foreach (Node child in GetChildren())
        {
            Label label = child as Label ?? child.FindChild("*", "Label", true, false) as Label;
            if (label != null)
                return label;
        }

        return null;
    }
}
