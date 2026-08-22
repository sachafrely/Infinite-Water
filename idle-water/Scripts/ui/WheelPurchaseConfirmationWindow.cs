using System;
using Godot;

/// <summary>
/// Full-screen modal confirmation dialog for a specific wheel purchase.
/// The root Control owns the entire input area so controls underneath can never
/// receive the same touch/click while this dialog is open.
/// </summary>
public partial class WheelPurchaseConfirmationWindow : Control
{
    private const float WindowWidth = 360.0f;
    private const float WindowHeight = 160.0f;

    private int wheelIndex;
    private FluidSimulator simulator;
    private Action<int> confirmed;
    private Action cancelled;
    private PanelContainer panel;
    private Button yesButton;
    private Button noButton;

    public void Setup(int index, FluidSimulator fluidSimulator, Action<int> onConfirmed, Action onCancelled)
    {
        wheelIndex = index;
        simulator = fluidSimulator;
        confirmed = onConfirmed;
        cancelled = onCancelled;
    }

    public override void _Ready()
    {
        ZIndex = 2000;
        ZAsRelative = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        panel = new PanelContainer();
        panel.Name = "ConfirmationPanel";
        panel.CustomMinimumSize = new Vector2(WindowWidth, WindowHeight);
        panel.Size = new Vector2(WindowWidth, WindowHeight);
        panel.Position = GetViewportRect().Size * 0.5f - panel.Size * 0.5f;
        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.ZIndex = 1;

        StyleBoxFlat style = UiSettings.CreateBox(
            UiSettings.WindowColor,
            UiSettings.BorderColor,
            (int)UiSettings.BorderSize
        );
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        VBoxContainer content = new VBoxContainer();
        content.Alignment = BoxContainer.AlignmentMode.Center;
        content.AddThemeConstantOverride("separation", 12);
        panel.AddChild(content);

        Label message = new Label();
        message.Text = "Do you want to buy this wheel for 10$";
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        content.AddChild(message);

        HBoxContainer buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 16);
        content.AddChild(buttons);

        yesButton = new Button { Text = "Yes" };
        yesButton.CustomMinimumSize = new Vector2(100, 40);
        yesButton.FocusMode = Control.FocusModeEnum.None;
        // The modal handles Yes/No input explicitly in _Input. Ignoring GUI
        // hit-testing here prevents a lower sibling control from ever competing
        // with the confirmation buttons while preserving their visual appearance.
        yesButton.MouseFilter = MouseFilterEnum.Ignore;
        yesButton.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        yesButton.Pressed += Confirm;
        buttons.AddChild(yesButton);

        noButton = new Button { Text = "No" };
        noButton.CustomMinimumSize = new Vector2(100, 40);
        noButton.FocusMode = Control.FocusModeEnum.None;
        noButton.MouseFilter = MouseFilterEnum.Ignore;
        noButton.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        noButton.Pressed += Cancel;
        buttons.AddChild(noButton);
    }

    public override void _Input(InputEvent @event)
    {
        bool pressed = false;
        Vector2 position = Vector2.Zero;

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            pressed = true;
            position = mouseButton.GlobalPosition;
        }
        else if (@event is InputEventScreenTouch screenTouch && screenTouch.Pressed)
        {
            pressed = true;
            position = screenTouch.Position;
        }

        if (!pressed || panel == null)
            return;

        // Handle the modal buttons at the _Input stage. This is deliberately
        // before Control GUI hit-testing, so a sibling Upgrade button underneath
        // the modal can never block the Yes/No action.
        if (yesButton != null && yesButton.GetGlobalRect().HasPoint(position))
        {
            Confirm();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (noButton != null && noButton.GetGlobalRect().HasPoint(position))
        {
            Cancel();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Only consume outside clicks. Events inside the dialog that are not on
        // Yes/No simply keep the dialog open.
        if (!panel.GetGlobalRect().HasPoint(position))
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Confirm()
    {
        if (simulator == null || !simulator.IsWheelUnlocked(wheelIndex))
        {
            confirmed?.Invoke(wheelIndex);
            QueueFree();
        }
        else
        {
            Cancel();
        }
    }

    private void Cancel()
    {
        cancelled?.Invoke();
        QueueFree();
    }
}
