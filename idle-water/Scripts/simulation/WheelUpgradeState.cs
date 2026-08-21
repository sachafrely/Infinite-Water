public sealed class WheelUpgradeState
{
    public const int MaxLevel = 3;

    public int BiggerPaddlesLevel { get; private set; }
    public int LessFrictionLevel { get; private set; }
    public int MoreEfficientLevel { get; private set; }

    public int GetLevel(WheelUpgradeType type)
    {
        return type switch
        {
            WheelUpgradeType.BiggerPaddles => BiggerPaddlesLevel,
            WheelUpgradeType.LessFriction => LessFrictionLevel,
            WheelUpgradeType.MoreEfficient => MoreEfficientLevel,
            _ => 0
        };
    }

    public bool IsMaxed(WheelUpgradeType type) => GetLevel(type) >= MaxLevel;

    public bool HasAvailableUpgrade =>
        BiggerPaddlesLevel < MaxLevel ||
        LessFrictionLevel < MaxLevel ||
        MoreEfficientLevel < MaxLevel;

    public int GetPrice(WheelUpgradeType type)
    {
        int level = GetLevel(type);
        return level switch
        {
            0 => 20,
            1 => 30,
            2 => 40,
            _ => 0
        };
    }

    public bool TryIncrease(WheelUpgradeType type)
    {
        if (IsMaxed(type))
            return false;

        switch (type)
        {
            case WheelUpgradeType.BiggerPaddles:
                BiggerPaddlesLevel++;
                break;
            case WheelUpgradeType.LessFriction:
                LessFrictionLevel++;
                break;
            case WheelUpgradeType.MoreEfficient:
                MoreEfficientLevel++;
                break;
            default:
                return false;
        }

        return true;
    }
}
