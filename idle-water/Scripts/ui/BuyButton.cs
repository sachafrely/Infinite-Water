using Godot;

/// <summary>
/// Controls the BuyButton already present in WheelDisplay.tscn.
/// Window opening/purchase behavior is supplied by the owning wheel/UI layer.
/// </summary>
public partial class BuyButton : Button
{
    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        WheelDisplay wheelDisplay = GetParent()?.GetParent() as WheelDisplay;
        if (wheelDisplay == null)
        {
            GD.PushWarning("BuyButton: Could not find owning WheelDisplay.");
            return;
        }

        wheelDisplay.GetNodeOrNull<WheelUi>("../..");
        wheelDisplay.EmitSignal(WheelDisplay.SignalName.BuyRequested);
    }

    [Signal]
    public delegate void BuyRequestedEventHandler();
}
