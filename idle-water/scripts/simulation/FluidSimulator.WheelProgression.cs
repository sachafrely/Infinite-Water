using Godot;

public partial class FluidSimulator
{
	/// <summary>
	/// Number of wheel runtimes currently active in the running simulation.
	/// </summary>
	public int ActiveWheelCount =>
		waterWheelManager?.WheelCount ?? 0;

	/// <summary>
	/// Number of wheel locations currently owned by the player.
	/// </summary>
	public int UnlockedWheelCount =>
		waterWheelManager?.UnlockedWheelCount ?? 0;

	public bool IsWheelUnlocked(int wheelIndex)
	{
		return waterWheelManager != null &&
			waterWheelManager.IsWheelUnlocked(wheelIndex);
	}

	public bool CanUnlockNextWheel()
	{
		return waterWheelManager != null &&
			waterWheelManager.CanUnlockNextWheel();
	}

	/// <summary>
	/// Purchases the next locked wheel for the fixed wheel purchase price.
	/// The purchase activates one wheel in the existing simulation only.
	/// </summary>
	public bool TryPurchaseNextWheel()
	{
		if (waterWheelManager == null)
			return false;

		return waterWheelManager.TryUnlockNextWheel();
	}

	public int GetNextLockedWheelIndex()
	{
		return waterWheelManager?.GetNextLockedWheelIndex() ?? -1;
	}

	public Vector2 GetWheelPosition(int wheelIndex)
	{
		return waterWheelManager?.GetWheelPosition(wheelIndex) ?? Vector2.Zero;
	}
}
