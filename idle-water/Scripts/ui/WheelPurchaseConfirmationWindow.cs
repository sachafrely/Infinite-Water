using System;
using Godot;

/// <summary>
/// Confirmation dialog for a specific wheel purchase.
/// The dialog is attached to the main scene and uses a full-screen modal input
/// blocker so clicks cannot reach Buy or Upgrade UI behind it.
/// </summary>
public partial class WheelPurchaseConfirmationWindow : Control
{
    private int wheelIndex;
    private FluidSimulator simulator;
    private Action<int> confirmed;
    private Action cancelled;

    public void Setup(int index, FluidSimulator fluidSimulator, Action<int> onConfirmed, Action onCancelled)
    {
        wheelIndex = index;
        simulator = fluidSimulator;
        confirmed = onConfirmed;
        cancelled = onCancelled;
    }

    public override void _Ready()
    {
        ZIndex = 5000;
        ZAsRelative = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        ColorRect blocker = new ColorRect
        {
            Name = "ModalInputBlocker",
            Color = new Color(0, 0, 0, 0),
            MouseFilter = MouseFilterEnum.Stop
        };
        blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        PanelContainer panel = new PanelContainer
        {
            Name = "ConfirmationPanel",
            CustomMinimumSize = new Vector2(340, 150),
            Size = new Vector2(340, 150),
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 1
        };
        panel.Position = GetViewportRect().Size * 0.5f - panel.Size * 0.5f;
        panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor));
        AddChild(panel);

        VBoxContainer content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        Label message = new Label
        {
            Text = "Do you want to buy this wheel for 10$",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        message.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        message.AddThemeColorOverride("font_color", UiSettings.FontColorEnabled);
        content.AddChild(message);

        HBoxContainer buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);

        Button yes = new Button { Text = "Yes", CustomMinimumSize = new Vector2(106, 40), FocusMode = Control.FocusModeEnum.None };
        UiSettings.ApplyButtonTheme(yes);
        yes.Pressed += Confirm;
        buttons.AddChild(yes);

        Button no = new Button { Text = "No", CustomMinimumSize = new Vector2(106, 40), FocusMode = Control.FocusModeEnum.None };
        UiSettings.ApplyButtonTheme(no);
        no.Pressed += Cancel;
        buttons.AddChild(no);
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
