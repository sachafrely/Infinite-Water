// I WANT THIS FILE GONE. REPLACE IT WITH ANY OTHER FILE.
// THIS DOESN'T BELONG HERE.

using Godot;

public partial class FluidSimulator
{
	public int ActiveWheelCount => waterWheelManager?.WheelCount ?? 0;
	public int UnlockedWheelCount => waterWheelManager?.UnlockedWheelCount ?? 0;
	public bool IsWheelUnlocked(int wheelIndex) => waterWheelManager != null && waterWheelManager.IsWheelUnlocked(wheelIndex);
	public bool CanUnlockNextWheel() => waterWheelManager != null && waterWheelManager.CanUnlockNextWheel();
	public bool TryPurchaseNextWheel() => waterWheelManager != null && waterWheelManager.TryUnlockNextWheel();

	public bool TryPurchaseWheel(int wheelIndex)
	{
		return waterWheelManager != null && waterWheelManager.TryUnlockWheel(wheelIndex);
	}

	public int GetNextLockedWheelIndex() => waterWheelManager?.GetNextLockedWheelIndex() ?? -1;
	public Vector2 GetWheelPosition(int wheelIndex) => waterWheelManager?.GetWheelPosition(wheelIndex) ?? Vector2.Zero;

	public bool HasAvailableWheelUpgrades(int wheelIndex) =>
		waterWheelManager != null && waterWheelManager.HasAvailableUpgrades(wheelIndex);

	public int GetWheelUpgradeLevel(int wheelIndex, WheelUpgradeType type) =>
		waterWheelManager?.GetUpgradeLevel(wheelIndex, type) ?? 0;

	public int GetWheelUpgradePrice(int wheelIndex, WheelUpgradeType type) =>
		waterWheelManager?.GetUpgradePrice(wheelIndex, type) ?? 0;

	public bool CanPurchaseWheelUpgrade(int wheelIndex, WheelUpgradeType type) =>
		waterWheelManager != null && waterWheelManager.CanPurchaseUpgrade(wheelIndex, type);

	public bool PurchaseWheelUpgrade(int wheelIndex, WheelUpgradeType type) =>
		waterWheelManager != null && waterWheelManager.PurchaseUpgrade(wheelIndex, type);

	/// <summary>
	/// Converts a wheel's simulation-space position into the root Main scene's
	/// screen-space coordinates. GameView and its SubViewport currently have the
	/// same size, but the scale calculation keeps this correct if that changes.
	/// </summary>
	public Vector2 GetWheelUiPosition(int wheelIndex)
	{
		Vector2 simulationPosition = GetWheelPosition(wheelIndex);
		GameViewMapping mapping = CreateGameViewMapping();
		if (!mapping.IsValid)
			return simulationPosition;

		Vector2 viewportSize = new Vector2(mapping.SimulationViewport.Size.X, mapping.SimulationViewport.Size.Y);
		Vector2 gameViewSize = mapping.GameView.Size;
		Vector2 cameraCenter = mapping.Camera.GetScreenCenterPosition();
		Vector2 viewportPoint = simulationPosition - cameraCenter + viewportSize * 0.5f;
		Vector2 scale = new Vector2(
			viewportSize.X > 0.0f ? gameViewSize.X / viewportSize.X : 1.0f,
			viewportSize.Y > 0.0f ? gameViewSize.Y / viewportSize.Y : 1.0f
		);

		return mapping.GameView.Position + viewportPoint * scale;
	}
}
