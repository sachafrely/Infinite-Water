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
            panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor));

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
            button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
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

        confirmationWindow = new WheelPurchaseConfirmationWindow();
        confirmationWindow.Name = "WheelPurchaseConfirmationWindow";
        confirmationWindow.ZIndex = 2000;
        confirmationWindow.Setup(wheelIndex, simulator, OnPurchaseConfirmed, OnPurchaseCancelled);
        AddChild(confirmationWindow);
    }

    private void OnPurchaseConfirmed(int wheelIndex)
    {
        bool purchased = simulator != null && simulator.TryPurchaseWheel(wheelIndex);
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
            Button button = buttons[wheelIndex];
            if (button != null)
            {
                UiSettings.ApplyButtonTheme(button, false);
                button.Disabled = false;
            }
        }
    }
}
