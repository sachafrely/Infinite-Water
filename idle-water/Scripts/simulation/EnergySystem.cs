using Godot;

public class EnergySystem
{
	public float EnergyPerRadian = 1.0f;
	public const double EnergyPerDollar = 10.0;
	public const double WheelPurchaseCost = 10.0;
	private double dollars = 200.0;
	public static EnergySystem Instance { get; private set; }
	private double energy = 0.0;
	private double totalGenerated = 0.0;
	public double Energy => energy;
	public double Dollars => dollars;
	public double TotalGenerated => totalGenerated;

	public EnergySystem()
	{
		Instance = this;
		CreateEconomyUiDeferred();
		CreateWheelPurchaseUiDeferred();
		CreateWheelUpgradeUiDeferred();
	}

	public void AddEnergy(double amount) { if (amount <= 0.0) return; energy += amount; totalGenerated += amount; }
	public bool TrySpendEnergy(double amount) { if (amount <= 0.0) return true; if (energy < amount) return false; energy -= amount; return true; }
	public bool TrySpendDollars(double amount) { if (amount <= 0.0) return true; if (dollars < amount) return false; dollars -= amount; return true; }
	public int SellAllAvailableEnergy() { int chunks = (int)System.Math.Floor(energy / EnergyPerDollar); if (chunks <= 0) return 0; energy -= chunks * EnergyPerDollar; dollars += chunks; return chunks; }
	public bool TrySellEnergyChunk() { if (energy < EnergyPerDollar) return false; energy -= EnergyPerDollar; dollars += 1.0; return true; }
	public void Reset() { energy = 0.0; dollars = 200.0; totalGenerated = 0.0; }

	private void CreateEconomyUiDeferred()
	{
		SceneTree tree = Engine.GetMainLoop() as SceneTree; Node currentScene = tree?.CurrentScene; if (currentScene == null) return;
		if (currentScene.FindChild("EconomyUi", true, false) != null) return;
		EconomyUi economyUi = new EconomyUi { Name = "EconomyUi" }; currentScene.CallDeferred(Node.MethodName.AddChild, economyUi);
	}

	private void CreateWheelPurchaseUiDeferred()
	{
		SceneTree tree = Engine.GetMainLoop() as SceneTree; Node currentScene = tree?.CurrentScene; if (currentScene == null) return;
		if (currentScene.FindChild("WheelPurchaseUi", true, false) != null) return;
		WheelPurchaseUi wheelPurchaseUi = new WheelPurchaseUi { Name = "WheelPurchaseUi" }; currentScene.CallDeferred(Node.MethodName.AddChild, wheelPurchaseUi);
	}

	private void CreateWheelUpgradeUiDeferred()
	{
		SceneTree tree = Engine.GetMainLoop() as SceneTree; Node currentScene = tree?.CurrentScene; if (currentScene == null) return;
		if (currentScene.FindChild("WheelUpgradeUi", true, false) != null) return;
		WheelUpgradeUi wheelUpgradeUi = new WheelUpgradeUi { Name = "WheelUpgradeUi" }; currentScene.CallDeferred(Node.MethodName.AddChild, wheelUpgradeUi);
	}
}