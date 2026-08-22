using Godot;

/// <summary>
/// Small purchase buttons for locked wheel positions.
/// The button always opens the modal purchase confirmation; it never performs
/// the purchase directly.
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
            PanelContainer panel = new PanelContainer
            {
                Name = "BuyWheelWindow_" + (wheelIndex + 1),
                CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
                Size = new Vector2(WindowWidth, WindowHeight),
                MouseFilter = MouseFilterEnum.Stop,
                ZIndex = 901,
                ZAsRelative = false
            };
            panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor));

            VBoxContainer content = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            content.AddThemeConstantOverride("separation", 0);
            panel.AddChild(content);

            Button button = new Button
            {
                Name = "BuyButton",
                Text = "Buy",
                CustomMinimumSize = new Vector2(WindowWidth - 4.0f, WindowHeight - 4.0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop
            };
            button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
            UiSettings.ApplyButtonTheme(button);
            int capturedIndex = wheelIndex;
            button.Pressed += () => OnBuyPressed(capturedIndex);
            content.AddChild(button);
            buttons[wheelIndex] = button;
            AddChild(panel);
            windows[wheelIndex] = panel;
        }
    }

    private void OnBuyPressed(int wheelIndex)
    {
        if (simulator == null || simulator.IsWheelUnlocked(wheelIndex)) return;
        OpenConfirmation(wheelIndex);
    }

    private void OpenConfirmation(int wheelIndex)
    {
        if (confirmationWindow != null && IsInstanceValid(confirmationWindow))
            confirmationWindow.QueueFree();

        confirmationWindow = new WheelPurchaseConfirmationWindow
        {
            Name = "WheelPurchaseConfirmationWindow",
            ZIndex = 5000,
            ZAsRelative = false
        };
        confirmationWindow.Setup(wheelIndex, simulator, OnPurchaseConfirmed, OnPurchaseCancelled);

        // Attach the modal to the main scene rather than this UI controller.
        // This guarantees the full-screen blocker is above both Buy and Upgrade
        // controllers, which are separate siblings created by EnergySystem.
        Node host = GetTree().CurrentScene ?? GetTree().Root;
        host.AddChild(confirmationWindow);
    }

    private void OnPurchaseConfirmed(int wheelIndex)
    {
        simulator.TryPurchaseWheel(wheelIndex);
        Refresh();
        confirmationWindow = null;
    }

    private void OnPurchaseCancelled()
    {
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
            panel.Position = new Vector2(wheelPosition.X - WindowWidth * 0.5f - 6.0f, wheelPosition.Y - WindowHeight * 0.5f);
            if (buttons[wheelIndex] != null)
            {
                UiSettings.ApplyButtonTheme(buttons[wheelIndex]);
                buttons[wheelIndex].Disabled = false;
            }
        }
    }
}
