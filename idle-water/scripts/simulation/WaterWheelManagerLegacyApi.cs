using System;
using Godot;

/// <summary>
/// Keeps the existing FluidSimulator bootstrap call compatible while moving
/// wheel ownership and purchase behavior into WheelPurchaseSystem.
/// </summary>
internal static class WaterWheelManagerLegacyApi
{
	public static void CreateWaterWheelsFromEnvironment(
		this WaterWheelManager manager,
		TileMapLayer environment,
		Func<Vector2, Vector2> toSimulationSpace)
	{
		manager.DiscoverWheelLocations(
			environment,
			toSimulationSpace
		);

		Node2D owner =
			(Engine.GetMainLoop() as SceneTree)?.CurrentScene as Node2D;

		if (owner == null || EnergySystem.Instance == null)
		{
			GD.PushWarning(
				"WaterWheelManager: Could not initialize wheel purchase UI because the current scene or economy is missing."
			);
			return;
		}

		WheelPurchaseSystem purchaseSystem =
			new WheelPurchaseSystem(
				manager,
				EnergySystem.Instance,
				owner
			);

		purchaseSystem.Initialize();
		manager.InitializeWheelEnergyTracking();
	}
}
