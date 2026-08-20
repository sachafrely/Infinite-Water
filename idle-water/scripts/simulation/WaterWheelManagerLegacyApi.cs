using System;
using Godot;

/// <summary>
/// Keeps the existing FluidSimulator bootstrap call compatible while moving
/// wheel ownership and purchase behavior into WheelPurchaseSystem.
/// </summary>
internal static class WaterWheelManagerLegacyApi
{
	private const int WheelPurchaseUiLayer = 200;

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

		if (currentScene == null || EnergySystem.Instance == null)
		{
			GD.PushWarning(
				"WaterWheelManager: Could not initialize wheel purchase UI because the current scene or economy is missing."
			);
			return;
		}

		// Do not attach the purchase controls to GameView itself.
		// GameView is the simulation display and may be a TextureRect/viewport
		// hierarchy with its own clipping and input behavior. A dedicated
		// CanvasLayer gives the purchase controls a guaranteed screen-space
		// overlay while preserving the existing 720x1160 GameView coordinates.
		CanvasLayer uiLayer =
			currentScene.GetNodeOrNull<CanvasLayer>("WheelPurchaseUiLayer");

		if (uiLayer == null)
		{
			uiLayer = new CanvasLayer();
			uiLayer.Name = "WheelPurchaseUiLayer";
			uiLayer.Layer = WheelPurchaseUiLayer;
			currentScene.AddChild(uiLayer);
		}

		WheelPurchaseSystem purchaseSystem =
			new WheelPurchaseSystem(
				manager,
				EnergySystem.Instance,
				uiLayer
			);

		purchaseSystem.Initialize();
		manager.InitializeWheelEnergyTracking();

		GD.Print(
			"WaterWheelManager: Wheel purchase UI attached to dedicated CanvasLayer '" +
			uiLayer.GetPath() +
			"' at layer " +
			WheelPurchaseUiLayer + "."
		);
	}
}
