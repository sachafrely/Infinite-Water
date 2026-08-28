using Godot;

/// <summary>
/// Owns the six WheelDisplay instances already present under WheelUi.
/// WheelUi is the single source of truth for logical wheel numbering.
/// </summary>
public partial class WheelUi : Control
{
    private const int WheelCount = 6;

    private readonly WheelDisplay[] wheelDisplays = new WheelDisplay[WheelCount];

    public override void _Ready()
    {
        for (int index = 0; index < WheelCount; index++)
        {
            int wheelNumber = index + 1;
            WheelDisplay display = GetNodeOrNull<WheelDisplay>($"Wheel{wheelNumber}");

            if (display == null)
            {
                GD.PushWarning($"WheelUi: Wheel{wheelNumber} is missing or is not a WheelDisplay instance.");
                continue;
            }

            wheelDisplays[index] = display;
            display.SetWheelNumber(wheelNumber);
            display.BuyRequested += OnBuyRequested;
            display.UpgradeRequested += OnUpgradeRequested;
        }
    }

    public WheelDisplay GetWheelDisplay(int wheelNumber)
    {
        if (wheelNumber < 1 || wheelNumber > WheelCount)
            return null;

        return wheelDisplays[wheelNumber - 1];
    }

    public int GetWheelNumber(WheelDisplay display)
    {
        if (display == null)
            return 0;

        for (int index = 0; index < wheelDisplays.Length; index++)
        {
            if (wheelDisplays[index] == display)
                return index + 1;
        }

        return 0;
    }

    private void OnBuyRequested(int wheelNumber)
    {
        GD.Print($"WheelUi: Buy requested for Wheel {wheelNumber}.");
        // BuyWindow migration will consume this request next.
    }

    private void OnUpgradeRequested(int wheelNumber)
    {
        GD.Print($"WheelUi: Upgrade requested for Wheel {wheelNumber}.");
        // UpgradeWindow migration will consume this request next.
    }
}
