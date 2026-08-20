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

		SceneTree tree =
			Engine.GetMainLoop() as SceneTree;

		Node currentScene =
			tree?.CurrentScene;

		// Purchase controls belong in the GameView overlay. GameView is the
		// visible 720x1160 screen-space container for the simulation, while the
		// FluidSimulation node lives inside a SubViewport and cannot host the
		// normal UI controls we need here.
		Node uiOwner =
			currentScene?.FindChild(
				"GameView",
				true,
				false
			);

		if (uiOwner == null)
			uiOwner = currentScene;

		if (uiOwner == null || EnergySystem.Instance == null)
		{
			GD.PushWarning(
				"WaterWheelManager: Could not initialize wheel purchase UI because the GameView or economy is missing."
			);
			return;
		}

		WheelPurchaseSystem purchaseSystem =
			new WheelPurchaseSystem(
				manager,
				EnergySystem.Instance,
				uiOwner
			);

		purchaseSystem.Initialize();
		manager.InitializeWheelEnergyTracking();
	}
}
