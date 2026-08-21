using Godot;

public partial class FluidSimulator
{
	public int ActiveWheelCount => waterWheelManager?.WheelCount ?? 0;
	public int UnlockedWheelCount => waterWheelManager?.UnlockedWheelCount ?? 0;
	public bool IsWheelUnlocked(int wheelIndex) => waterWheelManager != null && waterWheelManager.IsWheelUnlocked(wheelIndex);
	public bool CanUnlockNextWheel() => waterWheelManager != null && waterWheelManager.CanUnlockNextWheel();
	public bool TryPurchaseNextWheel() => waterWheelManager != null && waterWheelManager.TryUnlockNextWheel();

	/// <summary>Purchases the specific wheel represented by a Buy Wheel window.</summary>
	public bool TryPurchaseWheel(int wheelIndex)
	{
		return waterWheelManager != null && waterWheelManager.TryUnlockWheel(wheelIndex);
	}

	public int GetNextLockedWheelIndex() => waterWheelManager?.GetNextLockedWheelIndex() ?? -1;
	public Vector2 GetWheelPosition(int wheelIndex) => waterWheelManager?.GetWheelPosition(wheelIndex) ?? Vector2.Zero;
}
