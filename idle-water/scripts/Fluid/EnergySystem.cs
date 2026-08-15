
using Godot;

public class EnergySystem
{
	// ============================================================
	// Energy production
	// ============================================================

	// Energy generated for every radian a wheel turns.
	//
	// Example:
	// 1.0 radian/sec = 1.0 energy/sec
	// 2.0 radian/sec = 2.0 energy/sec
	//
	// This is intentionally easy to tune.
	public float EnergyPerRadian =
		1.0f;

	// ============================================================
	// Resource
	// ============================================================

	private double energy = 0.0;

	private double totalGenerated =
		0.0;

	// ============================================================
	// Properties
	// ============================================================

	public double Energy =>
		energy;

	public double TotalGenerated =>
		totalGenerated;

	// ============================================================
	// Add energy
	// ============================================================

	public void AddEnergy(
		double amount)
	{
		if (amount <= 0.0)
			return;

		energy +=
			amount;

		totalGenerated +=
			amount;
	}

	// ============================================================
	// Spend energy
	// ============================================================

	public bool TrySpendEnergy(
		double amount)
	{
		if (amount <= 0.0)
			return true;

		if (energy < amount)
			return false;

		energy -=
			amount;

		return true;
	}

	// ============================================================
	// Reset
	// ============================================================

	public void Reset()
	{
		energy = 0.0;

		totalGenerated = 0.0;
	}
}
