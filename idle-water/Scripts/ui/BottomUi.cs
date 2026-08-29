using Godot;

/// <summary>
/// Coordinates the persistent bottom UI.
/// Layout is authored in Godot; this script handles button setup and styling.
/// </summary>
public partial class BottomUi : Control
{
	private Button sellEnergyButton;
	private Button statisticsButton;
	private Button settingsButton;
	private Button prestigeButton;

	public override void _Ready()
	{
		sellEnergyButton = FindButton("SellEnergy");
		statisticsButton = FindButton("Statistics");
		settingsButton = FindButton("Settings");
		prestigeButton = FindButton("Prestige");

		StyleButton(sellEnergyButton);
		StyleButton(statisticsButton);
		StyleButton(settingsButton);
		StyleButton(prestigeButton);

		if (prestigeButton != null)
			prestigeButton.Disabled = true;
	}

	private Button FindButton(string nodeName)
	{
		return FindChild(nodeName, true, false) as Button;
	}

	private void StyleButton(Button button)
	{
		if (button == null)
			return;

		RenderedButtonBackground.Apply(button);
	}
}
