using System;
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
	// Economy
	// ============================================================

	// Selling is intentionally limited to complete 10-energy chunks.
	public const double EnergyPerDollar =
		10.0;

	private double dollars = 0.0;

	public static EnergySystem Instance {
		get;
		private set;
	}

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

	public double Dollars =>
		dollars;

	public double TotalGenerated =>
		totalGenerated;

	// ============================================================
	// Construction
	// ============================================================

	public EnergySystem()
	{
		Instance =
			this;

		CreateEconomyUiDeferred();
	}

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
	// Sell energy
	// ============================================================

	/// <summary>
	/// Sells every complete 10-energy chunk currently available.
	/// Any remainder below 10 energy is kept.
	/// Returns the number of dollars earned.
	/// </summary>
	public int SellFullEnergyChunks()
	{
		int chunks =
			(int)Math.Floor(
				energy /
				EnergyPerDollar
			);

		if (chunks <= 0)
			return 0;

		double energySold =
			chunks *
			EnergyPerDollar;

		energy -=
			energySold;

		dollars +=
			chunks;

		return chunks;
	}

	// ============================================================
	// Reset
	// ============================================================

	public void Reset()
	{
		energy = 0.0;

		dollars = 0.0;

		totalGenerated = 0.0;
	}

	// ============================================================
	// Economy UI bootstrap
	// ============================================================

	private void CreateEconomyUiDeferred()
	{
		SceneTree tree =
			Engine.GetMainLoop() as SceneTree;

		Node currentScene =
		tree?.CurrentScene;

		if (currentScene == null)
			return;

		if (currentScene.FindChild(
			"EconomyUi",
			true,
			false
		) != null)
		{
			return;
		}

		EconomyUi economyUi =
			new EconomyUi();

		economyUi.Name =
			"EconomyUi";

		currentScene.CallDeferred(
			Node.MethodName.AddChild,
			economyUi
		);
	}
}
