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

		WheelPurchaseSystem purchaseSystem =
			new WheelPurchaseSystem(
				manager,
				EnergySystem.Instance,
				managerOwner: FindOwner(manager)
			);

		purchaseSystem.Initialize();
		manager.InitializeWheelEnergyTracking();
	}

	private static Node2D FindOwner(
		WaterWheelManager manager)
	{
		// The manager intentionally exposes no owner dependency publicly. The
		// purchase system is created by the simulator path below through the
		// manager's existing runtime owner.
		return manager.GetRuntimeOwner();
	}
}
