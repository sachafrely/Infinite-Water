public partial class FluidSimulator
{
    public int GetStatisticsWheelCount()
    {
        return waterWheelManager == null ? 0 : waterWheelManager.WheelCount;
    }

    public double GetStatisticsWheelEnergyThisFrame(int wheelIndex)
    {
        return waterWheelManager == null ? 0.0 : waterWheelManager.GetWheelEnergyThisFrame(wheelIndex);
    }
}
