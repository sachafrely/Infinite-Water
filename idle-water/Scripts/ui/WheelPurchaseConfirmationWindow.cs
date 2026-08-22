using System;
using Godot;

/// <summary>
/// Confirmation dialog for a specific wheel purchase.
/// The full-screen control is intentionally modal so clicks cannot reach UI behind it.
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
        ZIndex = 2000;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        ColorRect blocker = new ColorRect();
        blocker.Name = "ModalInputBlocker";
        blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        blocker.Color = new Color(0, 0, 0, 0);
        blocker.MouseFilter = MouseFilterEnum.Stop;
        AddChild(blocker);

        PanelContainer panel = new PanelContainer();
        panel.Name = "ConfirmationPanel";
        panel.CustomMinimumSize = new Vector2(340, 150);
        panel.Size = new Vector2(340, 150);
        panel.Position = GetViewportRect().Size * 0.5f - panel.Size * 0.5f;
        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.ZIndex = 1;

        panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor));
        blocker.AddChild(panel);

        VBoxContainer content = new VBoxContainer();
        content.Alignment = BoxContainer.AlignmentMode.Center;
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        Label message = new Label();
        message.Text = "Do you want to buy this wheel for 10$";
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
        message.AddThemeColorOverride("font_color", UiSettings.FontColorEnabled);
        content.AddChild(message);

        HBoxContainer buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);

        Button yes = new Button { Text = "Yes" };
        yes.CustomMinimumSize = new Vector2(106, 40);
        yes.FocusMode = Control.FocusModeEnum.None;
        UiSettings.ApplyButtonTheme(yes);
        yes.Pressed += Confirm;
        buttons.AddChild(yes);

        Button no = new Button { Text = "No" };
        no.CustomMinimumSize = new Vector2(106, 40);
        no.FocusMode = Control.FocusModeEnum.None;
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
