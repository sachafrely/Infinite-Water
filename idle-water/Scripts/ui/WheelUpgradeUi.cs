using Godot;

public partial class WheelUpgradeUi : Control
{
    private const int MaxWheelCount = 6;
    private const float WindowWidth = 90.0f;
    private const float WindowHeight = 38.0f;

    private readonly PanelContainer[] windows = new PanelContainer[MaxWheelCount];
    private readonly Button[] buttons = new Button[MaxWheelCount];

    private FluidSimulator simulator;
    private WheelUpgradeWindow upgradeWindow;
    private bool inputBlockedByPurchaseWindow;

    public override void _Ready()
    {
        ZIndex = 900;
        ZAsRelative = false;
        MouseFilter = MouseFilterEnum.Pass;

        simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
        if (simulator == null)
            simulator = GetTree().Root.FindChild("FluidSimulation", true, false) as FluidSimulator;

        BuildWindows();
        CallDeferred(nameof(Refresh));
    }

    public override void _Process(double delta) => Refresh();

    private void BuildWindows()
    {
        if (simulator == null)
            return;

        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            PanelContainer panel = new PanelContainer
            {
                Name = "UpgradeWheelWindow_" + (wheelIndex + 1),
                CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
                Size = new Vector2(WindowWidth, WindowHeight),
                MouseFilter = MouseFilterEnum.Stop,
                ZIndex = 901,
                ZAsRelative = false
            };

            panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));

            VBoxContainer content = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            content.AddThemeConstantOverride("separation", 0);
            panel.AddChild(content);

            Button button = new Button
            {
                Name = "UpgradeButton",
                Text = "Upgrade",
                CustomMinimumSize = new Vector2(WindowWidth - 4.0f, WindowHeight - 4.0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
            ApplyButtonStyle(button, true);

            int capturedIndex = wheelIndex;
            button.Pressed += () => OnUpgradePressed(capturedIndex);

            content.AddChild(button);
            AddChild(panel);
            windows[wheelIndex] = panel;
            buttons[wheelIndex] = button;
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

        button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
        button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
        button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
        button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
        button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
    }

    private void OnUpgradePressed(int wheelIndex)
    {
        if (inputBlockedByPurchaseWindow)
            return;

        if (simulator == null || !simulator.IsWheelUnlocked(wheelIndex))
            return;
        if (!simulator.HasAvailableWheelUpgrades(wheelIndex))
            return;

        // UPGRADE explicitly closes Settings/Statistics, but does not change
        // their normal outside-click behavior. The Upgrade action continues.
        CloseSettingsAndStatisticsWindow();

        WheelPurchaseUi purchaseUi = GetTree().Root.FindChild("WheelPurchaseUi", true, false) as WheelPurchaseUi;
        purchaseUi?.ClosePurchaseWindow();

        OpenUpgradeWindow(wheelIndex);
    }

    private void CloseSettingsAndStatisticsWindow()
    {
        UiWindowManager windowManager = GetTree().Root.FindChild("UiWindowManager", true, false) as UiWindowManager;
        windowManager?.CloseActiveWindow();
    }

    private void OpenUpgradeWindow(int wheelIndex)
    {
        CloseUpgradeWindow();

        upgradeWindow = new WheelUpgradeWindow();
        upgradeWindow.Name = "WheelUpgradeWindow";
        upgradeWindow.ZIndex = 2000;
        upgradeWindow.ZAsRelative = false;
        AddChild(upgradeWindow);
        upgradeWindow.Setup(wheelIndex, simulator, () => upgradeWindow = null);

        Vector2 anchor = simulator.GetWheelUiPosition(wheelIndex);
        Vector2 size = upgradeWindow.Size;
        Vector2 viewportSize = GetViewportRect().Size;

        Vector2 position = anchor - size * 0.5f;
        position.X = Mathf.Clamp(
            position.X,
            4.0f,
            Mathf.Max(4.0f, viewportSize.X - size.X - 4.0f)
        );
        position.Y = Mathf.Clamp(
            position.Y,
            4.0f,
            Mathf.Max(4.0f, viewportSize.Y - size.Y - 4.0f)
        );

        upgradeWindow.Position = position;
    }

    public void CloseUpgradeWindow()
    {
        if (upgradeWindow != null && IsInstanceValid(upgradeWindow))
            upgradeWindow.QueueFree();

        upgradeWindow = null;
    }

    /// <summary>
    /// Blocks the complete underlying Upgrade control hierarchy while the Buy
    /// confirmation is open. Both the button and its containing PanelContainer
    /// must ignore hit-testing; blocking only the Button still lets the parent
    /// PanelContainer intercept the click before the confirmation can receive it.
    /// No visual state is changed.
    /// </summary>
    public void SetPurchaseModalInputBlocked(bool blocked)
    {
        inputBlockedByPurchaseWindow = blocked;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].MouseFilter = blocked
                    ? Control.MouseFilterEnum.Ignore
                    : Control.MouseFilterEnum.Stop;
            }

            if (windows[i] != null)
            {
                windows[i].MouseFilter = blocked
                    ? Control.MouseFilterEnum.Ignore
                    : Control.MouseFilterEnum.Stop;
            }
        }
    }

    private void Refresh()
    {
        if (simulator == null || !IsInsideTree())
            return;

        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            PanelContainer panel = windows[wheelIndex];
            if (panel == null)
                continue;

            bool visible = simulator.IsWheelUnlocked(wheelIndex) && simulator.HasAvailableWheelUpgrades(wheelIndex);
            panel.Visible = visible;
            if (!visible)
                continue;

            Vector2 wheelPosition = simulator.GetWheelUiPosition(wheelIndex);
            panel.Position = new Vector2(
                wheelPosition.X - WindowWidth * 0.5f - 4.0f,
                wheelPosition.Y - WindowHeight * 0.5f
            );

            if (buttons[wheelIndex] != null)
            {
                bool available = simulator.HasAvailableWheelUpgrades(wheelIndex);
                ApplyButtonStyle(buttons[wheelIndex], available);
                buttons[wheelIndex].MouseFilter = inputBlockedByPurchaseWindow
                    ? Control.MouseFilterEnum.Ignore
                    : Control.MouseFilterEnum.Stop;
            }

            panel.MouseFilter = inputBlockedByPurchaseWindow
                ? Control.MouseFilterEnum.Ignore
                : Control.MouseFilterEnum.Stop;
        }

        if (upgradeWindow != null && IsInstanceValid(upgradeWindow))
        {
            if (!simulator.IsWheelUnlocked(upgradeWindow.WheelIndex) || !simulator.HasAvailableWheelUpgrades(upgradeWindow.WheelIndex))
            {
                CloseUpgradeWindow();
            }
        }
    }
}
