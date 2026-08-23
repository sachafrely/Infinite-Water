using Godot;

/// <summary>
/// Five small purchase windows, one for every locked wheel position.
/// Every wheel can be purchased independently; there is no sequential order.
/// </summary>
public partial class WheelPurchaseUi : Control
{
    private const int MaxWheelCount = 6;
    private const float WindowWidth = 77.0f;
    private const float WindowHeight = 54.0f;

    private readonly PanelContainer[] windows = new PanelContainer[MaxWheelCount];
    private readonly Button[] buttons = new Button[MaxWheelCount];
    private FluidSimulator simulator;
    private WheelPurchaseConfirmationWindow confirmationWindow;

    public override void _Ready()
    {
        ZIndex = 900;
        ZAsRelative = false;
        MouseFilter = MouseFilterEnum.Pass;
        simulator = GetParent() as FluidSimulator;
        if (simulator == null)
            simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
        BuildWindows();
        CallDeferred(nameof(Refresh));
    }

    public override void _Process(double delta) => Refresh();

    private void BuildWindows()
    {
        if (simulator == null) return;
        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            if (simulator.IsWheelUnlocked(wheelIndex)) continue;
            PanelContainer panel = new PanelContainer();
            panel.Name = "BuyWheelWindow_" + (wheelIndex + 1);
            panel.CustomMinimumSize = new Vector2(WindowWidth, WindowHeight);
            panel.Size = new Vector2(WindowWidth, WindowHeight);
            panel.MouseFilter = MouseFilterEnum.Stop;
            panel.ZIndex = 901;
            panel.ZAsRelative = false;
            // The outer panel is only the layout/input container. Its border is removed
            // so the visible button has exactly one UiSettings.BorderSize border.
            panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor, UiSettings.WindowColor, 0));

            VBoxContainer content = new VBoxContainer();
            content.Alignment = BoxContainer.AlignmentMode.Center;
            content.AddThemeConstantOverride("separation", 0);
            panel.AddChild(content);

            Button button = new Button();
            button.Name = "BuyButton";
            button.Text = "Buy";
            button.CustomMinimumSize = new Vector2(WindowWidth - 4.0f, WindowHeight - 4.0f);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            button.FocusMode = Control.FocusModeEnum.None;
            ApplyButtonStyle(button, true);
            int capturedIndex = wheelIndex;
            button.Pressed += () => OnBuyPressed(capturedIndex);
            content.AddChild(button);
            buttons[wheelIndex] = button;
            AddChild(panel);
            windows[wheelIndex] = panel;
        }
    }

    private void ApplyButtonStyle(Button button, bool available)
    {
        button.Disabled = false;
        Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
        button.AddThemeColorOverride("font_focus_color", textColor);
        button.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
        button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor));
        button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor));
        button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor));
        button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor));
        button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor));
    }

    private void OnBuyPressed(int wheelIndex)
    {
        if (simulator == null || simulator.IsWheelUnlocked(wheelIndex)) return;
        CloseSettingsAndStatisticsWindow();
        WheelUpgradeUi upgradeUi = GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi;
        upgradeUi?.CloseUpgradeWindow();
        OpenConfirmation(wheelIndex);
    }

    private void CloseSettingsAndStatisticsWindow()
    {
        UiWindowManager windowManager = GetTree().Root.FindChild("UiWindowManager", true, false) as UiWindowManager;
        windowManager?.CloseActiveWindow();
    }

    private void OpenConfirmation(int wheelIndex)
    {
        ClosePurchaseWindow();
        WheelUpgradeUi upgradeUi = GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi;
        upgradeUi?.SetPurchaseModalInputBlocked(true);
        confirmationWindow = new WheelPurchaseConfirmationWindow();
        confirmationWindow.Name = "WheelPurchaseConfirmationWindow";
        confirmationWindow.ZIndex = 2000;
        confirmationWindow.ZAsRelative = false;
        confirmationWindow.Setup(wheelIndex, simulator, OnPurchaseConfirmed, OnPurchaseCancelled);
        AddChild(confirmationWindow);
    }

    private void OnPurchaseConfirmed(int wheelIndex)
    {
        bool purchased = simulator != null && simulator.TryPurchaseWheel(wheelIndex);
        Refresh();
        ReleaseUpgradeInputBlock();
        confirmationWindow = null;
    }

    private void OnPurchaseCancelled()
    {
        ReleaseUpgradeInputBlock();
        confirmationWindow = null;
    }

    private void ReleaseUpgradeInputBlock()
    {
        WheelUpgradeUi upgradeUi = GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi;
        upgradeUi?.SetPurchaseModalInputBlocked(false);
    }

    public void ClosePurchaseWindow()
    {
        if (confirmationWindow != null && IsInstanceValid(confirmationWindow))
            confirmationWindow.QueueFree();
        ReleaseUpgradeInputBlock();
        confirmationWindow = null;
    }

    private void Refresh()
    {
        if (simulator == null || !IsInsideTree()) return;
        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            PanelContainer panel = windows[wheelIndex];
            if (panel == null) continue;
            if (simulator.IsWheelUnlocked(wheelIndex)) { panel.Visible = false; continue; }
            panel.Visible = true;
            Vector2 wheelPosition = simulator.GetWheelPosition(wheelIndex);
            panel.Position = new Vector2(wheelPosition.X - WindowWidth * 0.5f, wheelPosition.Y - WindowHeight * 0.5f);
            Button button = buttons[wheelIndex];
            if (button != null)
            {
                bool available = EnergySystem.Instance != null && EnergySystem.Instance.Dollars >= EnergySystem.WheelPurchaseCost;
                ApplyButtonStyle(button, available);
            }
        }
    }
}
