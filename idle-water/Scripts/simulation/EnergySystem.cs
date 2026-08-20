using Godot;

public class EnergySystem
{
	// ============================================================
	// Energy production
	// ============================================================

	// Energy generated for every radian a wheel turns.
	public float EnergyPerRadian =
		1.0f;

	// ============================================================
	// Economy
	// ============================================================

	// One sale is exactly 10 energy for 1 dollar.
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
		Instance = this;
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

		energy += amount;
		totalGenerated += amount;
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

		energy -= amount;
		return true;
	}

	// ============================================================
	// Sell energy
	// ============================================================

	/// <summary>
	/// Sells exactly one 10-energy chunk for 1 dollar.
	/// Any remainder below 10 energy is kept.
	/// </summary>
	public bool TrySellEnergyChunk()
	{
		if (energy < EnergyPerDollar)
			return false;

		energy -= EnergyPerDollar;
		dollars += 1.0;
		return true;
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
		SceneTree tree = Engine.GetMainLoop() as SceneTree;
		Node currentScene = tree?.CurrentScene;

		if (currentScene == null)
			return;

		if (currentScene.FindChild("EconomyUi", true, false) != null)
			return;

		EconomyUi economyUi = new EconomyUi();
		economyUi.Name = "EconomyUi";

		currentScene.CallDeferred(
			Node.MethodName.AddChild,
			economyUi
		);
	}
}
