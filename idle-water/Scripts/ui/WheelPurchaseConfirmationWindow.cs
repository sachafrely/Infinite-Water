using System;
using Godot;

/// <summary>
/// Full-screen modal confirmation dialog for a specific wheel purchase.
/// The root Control owns the entire input area so controls underneath can never
/// receive the same touch/click while this dialog is open.
/// </summary>
public partial class WheelPurchaseConfirmationWindow : Control
{
    private int wheelIndex;
    private FluidSimulator simulator;
    private Action<int> confirmed;
    private Action cancelled;
    private PanelContainer panel;

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
        panel.CustomMinimumSize = new Vector2(300, 120);
        panel.Size = new Vector2(300, 120);
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
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        Label message = new Label();
        message.Text = "Do you want to buy this wheel for 10$";
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        content.AddChild(message);

        HBoxContainer buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);

        Button yes = new Button { Text = "Yes" };
        yes.CustomMinimumSize = new Vector2(90, 32);
        yes.FocusMode = Control.FocusModeEnum.None;
        yes.MouseFilter = MouseFilterEnum.Stop;
        yes.Pressed += Confirm;
        buttons.AddChild(yes);

        Button no = new Button { Text = "No" };
        no.CustomMinimumSize = new Vector2(90, 32);
        no.FocusMode = Control.FocusModeEnum.None;
        no.MouseFilter = MouseFilterEnum.Stop;
        no.Pressed += Cancel;
        buttons.AddChild(no);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            if (panel != null && !panel.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
                Cancel();

            GetViewport().SetInputAsHandled();
            return;
        }

        // Android can deliver a tap as InputEventScreenTouch. Handle it explicitly
        // so tapping empty space outside the confirmation also closes the modal.
        if (@event is InputEventScreenTouch screenTouch && screenTouch.Pressed)
        {
            if (panel != null && !panel.GetGlobalRect().HasPoint(screenTouch.Position))
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
