// THIS FILE SHALL BE GONE SOON //

using System.Reflection;
using Godot;

public partial class EconomyUi : Control
{
    // Legacy runtime-built economy UI.
    // The replacement is TopUi + RainDisplay + EnergyDisplay + MoneyDisplay.
    // Do not add new functionality here.

    private const int ResourceDisplayPadding = 10;
    private const float ResourceRightMargin = 16.0f;
    private const float AntiLagVerticalOffset = 90.0f;
    private Label energyLabel;
    private Label dollarsLabel;
    private HBoxContainer resourceDisplay;
    private Button sellEnergyButton;
    private Control rainDisplay;
    private Label antiLagLabel;
    private bool attachedToTopUi;
    private FieldInfo rainSystemField;
    private PropertyInfo rainPercentProperty;
    private FieldInfo antiLagControllerField;
    private PropertyInfo antiLagIsActiveProperty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = false;
        ZIndex = 1000;
        CreateDisplay();
        CreateRainDisplay();
        CreateAntiLagDisplay();
        CacheRainAccess();
        CallDeferred(nameof(AttachToTopUi));
        CallDeferred(nameof(EnsureSellEnergyButton));
    }

    public override void _Process(double delta)
    {
        if (!attachedToTopUi) AttachToTopUi();
        EnsureSellEnergyButton();
        UpdateDisplay();
        UpdateRainDisplay();
        UpdateAntiLagDisplay();
        ApplyGlobalUiBorders();
        SetDisplayPosition();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0.0f || Size.Y <= 0.0f) return;
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, Size.Y)), UiSettings.WindowColor, true);
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, Size.Y)), UiSettings.BorderColor, false, UiSettings.BorderSize);
    }

    private void CreateDisplay()
    {
        MarginContainer margin = new MarginContainer { Name = "ResourceDisplayMargin", MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", ResourceDisplayPadding);
        margin.AddThemeConstantOverride("margin_right", ResourceDisplayPadding);
        AddChild(margin);
        resourceDisplay = new HBoxContainer { Name = "ResourceDisplay", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        resourceDisplay.AddThemeConstantOverride("separation", 16);
        margin.AddChild(resourceDisplay);
        energyLabel = new Label { Name = "EnergyLabel", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(180.0f, 0.0f) };
        energyLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        energyLabel.AddThemeColorOverride("font_color", UiSettings.FontColorEnergy);
        dollarsLabel = new Label { Name = "DollarsLabel", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(160.0f, 0.0f) };
        dollarsLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
        dollarsLabel.AddThemeColorOverride("font_color", UiSettings.FontColorBasic);
        resourceDisplay.AddChild(energyLabel);
        resourceDisplay.AddChild(dollarsLabel);
    }

    private void CreateRainDisplay()
    {
        rainDisplay = new RainAmountDisplay { Name = "RainAmountDisplay", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(270.0f, 51.0f) };
        AddChild(rainDisplay);
    }

    private void CreateAntiLagDisplay()
    {
        antiLagLabel = new Label { Name = "AntiLagCleanupLabel", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = "FPS low, cleaning up simulation...", Visible = false };
        antiLagLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        antiLagLabel.AddThemeColorOverride("font_color", UiSettings.FontColorEnabled);
        AddChild(antiLagLabel);
    }

    private void CacheRainAccess()
    {
        try
        {
            rainSystemField = typeof(FluidSimulator).GetField("rainSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rainSystemField != null) rainPercentProperty = rainSystemField.FieldType.GetProperty("CurrentRainPercent");
            antiLagControllerField = typeof(FluidSimulator).GetField("antiLagController", BindingFlags.Instance | BindingFlags.NonPublic);
            if (antiLagControllerField != null) antiLagIsActiveProperty = antiLagControllerField.FieldType.GetProperty("IsActive");
        }
        catch { rainSystemField = null; rainPercentProperty = null; antiLagControllerField = null; antiLagIsActiveProperty = null; }
    }

    private FluidSimulator FindSimulator() => GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;

    private float GetCurrentRainPercent()
    {
        FluidSimulator simulator = FindSimulator();
        if (simulator == null || rainSystemField == null || rainPercentProperty == null) return 0.0f;
        try
        {
            object rainSystem = rainSystemField.GetValue(simulator);
            object value = rainSystem == null ? null : rainPercentProperty.GetValue(rainSystem);
            return value is float percent ? percent : 0.0f;
        }
        catch { return 0.0f; }
    }

    private bool IsAntiLagCleanupActive()
    {
        FluidSimulator simulator = FindSimulator();
        if (simulator == null || antiLagControllerField == null || antiLagIsActiveProperty == null) return false;
        try
        {
            object controller = antiLagControllerField.GetValue(simulator);
            object value = controller == null ? null : antiLagIsActiveProperty.GetValue(controller);
            return value is bool active && active;
        }
        catch { return false; }
    }

    private void UpdateRainDisplay() { if (rainDisplay is RainAmountDisplay display) display.RainPercent = GetCurrentRainPercent(); }
    private void UpdateAntiLagDisplay() { if (antiLagLabel != null) antiLagLabel.Visible = IsAntiLagCleanupActive(); }

    private void SetDisplayPosition()
    {
        if (!IsInsideTree()) return;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (resourceDisplay != null) resourceDisplay.Position = new Vector2(Mathf.Max(0.0f, Size.X - resourceDisplay.Size.X - ResourceRightMargin), Mathf.Max(0.0f, (Size.Y - resourceDisplay.Size.Y) * 0.5f));
        if (rainDisplay != null) rainDisplay.Position = new Vector2(16.0f, Mathf.Max(0.0f, (Size.Y - rainDisplay.Size.Y) * 0.5f));
        if (antiLagLabel != null) { antiLagLabel.Position = new Vector2(0.0f, AntiLagVerticalOffset); antiLagLabel.Size = new Vector2(Size.X, Size.Y); }
    }

    private void AttachToTopUi()
    {
        if (!IsInsideTree()) return;
        Node currentScene = GetTree().CurrentScene;
        if (currentScene == null) return;
        Node topUi = currentScene.FindChild("TopUI", true, false) ?? currentScene.FindChild("TopUi", true, false);
        if (topUi == null) return;
        if (GetParent() != topUi) { GetParent()?.RemoveChild(this); topUi.AddChild(this); }
        attachedToTopUi = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetDisplayPosition();
    }

    private void EnsureSellEnergyButton()
    {
        if (sellEnergyButton != null && IsInstanceValid(sellEnergyButton)) return;
        Node root = GetTree().CurrentScene;
        if (root == null) return;
        foreach (Node node in root.FindChildren("*", "Button", true, false))
        {
            if (node is not Button button) continue;
            string text = button.Text.Trim().ToLowerInvariant().Replace(" ", "");
            string name = button.Name.ToString().Trim().ToLowerInvariant().Replace(" ", "");
            if (text == "sellenergy" || name.Contains("sellenergy")) { sellEnergyButton = button; if (!sellEnergyButton.IsConnected(Button.SignalName.Pressed, Callable.From(OnSellEnergyPressed))) sellEnergyButton.Pressed += OnSellEnergyPressed; return; }
        }
    }

    private void OnSellEnergyPressed() { EnergySystem.Instance?.SellAllAvailableEnergy(); UpdateDisplay(); }

    private void ApplyGlobalUiBorders()
    {
        Node root = GetTree().CurrentScene;
        if (root == null) return;
        foreach (Node node in root.FindChildren("*", "Button", true, false)) if (node is Button button) { button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonColor)); button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonColor)); button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.WindowColor)); button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonColor)); button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(new Color(0.10f, 0.10f, 0.10f, 1.0f), UiSettings.BorderColor)); }
        foreach (Node node in root.FindChildren("*", "Panel", true, false)) if (node is Panel panel) panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor));
    }

    private void UpdateDisplay()
    {
        if (energyLabel == null || dollarsLabel == null) return;
        EnergySystem economy = EnergySystem.Instance;
        if (economy == null) { energyLabel.Text = "Energy: 0"; dollarsLabel.Text = "Dollars: $0"; return; }
        energyLabel.Text = "Energy: " + System.Math.Floor(economy.Energy).ToString("F0");
        dollarsLabel.Text = "Dollars: $" + System.Math.Floor(economy.Dollars).ToString("F0");
        if (sellEnergyButton != null) sellEnergyButton.Disabled = economy.Energy < EnergySystem.EnergyPerDollar;
    }
}

internal sealed partial class RainAmountDisplay : Control
{
    private const int SegmentCount = 10;
    private const float SegmentWidth = 21.0f;
    private const float SegmentHeight = 30.0f;
    private const float SegmentGap = 4.5f;
    private float rainPercent;
    public float RainPercent { get => rainPercent; set { rainPercent = Mathf.Clamp(value, 0.0f, 100.0f); QueueRedraw(); } }
    public override void _Draw()
    {
        int activeSegments = Mathf.RoundToInt(rainPercent / 10.0f);
        float totalWidth = SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentGap;
        float startX = Mathf.Max(0.0f, (Size.X - totalWidth) * 0.5f);
        float startY = Mathf.Max(0.0f, (Size.Y - SegmentHeight) * 0.5f);
        for (int i = 0; i < SegmentCount; i++) { bool active = i < activeSegments; Color fill = active ? UiSettings.FontColorWater : new Color(0.30f, 0.30f, 0.30f, 1.0f); Rect2 rect = new Rect2(startX + i * (SegmentWidth + SegmentGap), startY, SegmentWidth, SegmentHeight); DrawRect(rect, fill, true); DrawRect(rect, UiSettings.BorderColor, false, 1.0f); }
    }
}
