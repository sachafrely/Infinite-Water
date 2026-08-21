using System;
using Godot;

/// <summary>
/// Confirmation dialog for a specific wheel purchase.
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
        ZIndex = 1000;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        PanelContainer panel = new PanelContainer();
        panel.Name = "ConfirmationPanel";
        panel.CustomMinimumSize = new Vector2(300, 120);
        panel.Size = new Vector2(300, 120);
        panel.Position = GetViewportRect().Size * 0.5f - panel.Size * 0.5f;

        StyleBoxFlat style = new StyleBoxFlat();
        style.BgColor = new Color(0.035f, 0.055f, 0.055f, 0.98f);
        style.BorderColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
        style.SetBorderWidthAll(2);
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        VBoxContainer content = new VBoxContainer();
        content.Alignment = BoxContainer.AlignmentMode.Center;
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        Label message = new Label();
        message.Text = "Do you want to buy this wheel for 10$";
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(message);

        HBoxContainer buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);

        Button yes = new Button { Text = "Yes" };
        yes.CustomMinimumSize = new Vector2(90, 32);
        yes.Pressed += Confirm;
        buttons.AddChild(yes);

        Button no = new Button { Text = "No" };
        no.CustomMinimumSize = new Vector2(90, 32);
        no.Pressed += Cancel;
        buttons.AddChild(no);
    }

    private void Confirm()
    {
        if (simulator == null || !simulator.IsWheelUnlocked(wheelIndex))
            confirmed?.Invoke(wheelIndex);
        else
            Cancel();
    }

    private void Cancel()
    {
        cancelled?.Invoke();
        QueueFree();
    }
}
