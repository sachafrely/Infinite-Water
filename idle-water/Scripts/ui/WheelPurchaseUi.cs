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
        if (simulator == null) simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
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
            PanelContainer panel = new PanelContainer
            {
                Name = "BuyWheelWindow_" + (wheelIndex + 1),
                CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
                Size = new Vector2(WindowWidth, WindowHeight),
                MouseFilter = MouseFilterEnum.Stop,
                ZIndex = 901,
                ZAsRelative = false
            };
            panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor, UiSettings.WindowColor, 0));

            VBoxContainer content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            content.AddThemeConstantOverride("separation", 0);
            panel.AddChild(content);

            Button button = new Button
            {
                Name = "BuyButton",
                Text = "Buy",
                CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
                Size = new Vector2(WindowWidth, WindowHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
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
        button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
        button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
    }

    private void OnBuyPressed(int wheelIndex)
    {
        if (simulator == null || simulator.IsWheelUnlocked(wheelIndex)) return;
        CloseSettingsAndStatisticsWindow();
        (GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi)?.CloseUpgradeWindow();
        OpenConfirmation(wheelIndex);
    }

    private void CloseSettingsAndStatisticsWindow()
    {
        (GetTree().Root.FindChild("UiWindowManager", true, false) as UiWindowManager)?.CloseActiveWindow();
    }

    private void OpenConfirmation(int wheelIndex)
    {
        ClosePurchaseWindow();
        (GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi)?.SetPurchaseModalInputBlocked(true);

        confirmationWindow = new WheelPurchaseConfirmationWindow
        {
            Name = "WheelPurchaseConfirmationWindow",
            ZIndex = 2000,
            ZAsRelative = false
        };
        confirmationWindow.Setup(wheelIndex, simulator, OnPurchaseConfirmed, OnPurchaseCancelled);
        AddChild(confirmationWindow);

        Vector2 anchor = simulator.GetWheelUiPosition(wheelIndex);
        Vector2 size = confirmationWindow.Size;
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 position = anchor - size * 0.5f;
        position.X = Mathf.Clamp(position.X, 4.0f, Mathf.Max(4.0f, viewportSize.X - size.X - 4.0f));
        position.Y = Mathf.Clamp(position.Y, 4.0f, Mathf.Max(4.0f, viewportSize.Y - size.Y - 4.0f));
        confirmationWindow.Position = position;
    }

    private void OnPurchaseConfirmed(int wheelIndex)
    {
        simulator?.TryPurchaseWheel(wheelIndex);
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
        (GetTree().Root.FindChild("WheelUpgradeUi", true, false) as WheelUpgradeUi)?.SetPurchaseModalInputBlocked(false);
    }

    public void ClosePurchaseWindow()
    {
        if (confirmationWindow != null && IsInstanceValid(confirmationWindow)) confirmationWindow.QueueFree();
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
                // The Buy button is intentionally always available. The actual
                // affordability check happens inside the Buy Wheel window.
                ApplyButtonStyle(button, true);
            }
        }
    }
}
