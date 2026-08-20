using Godot;

/// <summary>
/// Displays the player's Energy and Dollars in the existing top UI area.
/// The BottomUI Sell Energy control owns its own click action; this script only
/// discovers that button so it can enable/disable it based on available energy.
/// </summary>
public partial class EconomyUi : Control
{
	private const float RightMargin = 16.0f;
	private const float TopMargin = 8.0f;

	private Label energyLabel;
	private Label dollarsLabel;
	private Button sellEnergyButton;
	private bool attachedToTopUi = false;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		TopLevel = true;
		ZIndex = 1000;

		CreateDisplay();

		CallDeferred(nameof(AttachToTopUi));
		CallDeferred(nameof(SetDisplayPosition));
	}

	public override void _Process(double delta)
	{
		if (!attachedToTopUi)
			AttachToTopUi();

		FindSellEnergyButton();
		UpdateDisplay();
		SetDisplayPosition();
	}

	private void CreateDisplay()
	{
		HBoxContainer container = new HBoxContainer();
		container.Name = "ResourceDisplay";
		container.MouseFilter = MouseFilterEnum.Ignore;
		container.Alignment = BoxContainer.AlignmentMode.End;
		container.AddThemeConstantOverride("separation", 16);

		energyLabel = new Label();
		energyLabel.Name = "EnergyLabel";
		energyLabel.MouseFilter = MouseFilterEnum.Ignore;
		energyLabel.HorizontalAlignment = HorizontalAlignment.Right;
		energyLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
		energyLabel.AddThemeColorOverride("font_color", UiSettings.FontColorEnergy);

		dollarsLabel = new Label();
		dollarsLabel.Name = "DollarsLabel";
		dollarsLabel.MouseFilter = MouseFilterEnum.Ignore;
		dollarsLabel.HorizontalAlignment = HorizontalAlignment.Right;
		dollarsLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeBig);
		dollarsLabel.AddThemeColorOverride("font_color", UiSettings.FontColorBasic);

		container.AddChild(energyLabel);
		container.AddChild(dollarsLabel);
		AddChild(container);

		SetDisplayPosition();
	}

	private void SetDisplayPosition()
	{
		if (!IsInsideTree())
			return;

		Control container = GetNodeOrNull<Control>("ResourceDisplay");
		if (container == null)
			return;

		container.ResetSize();
		Vector2 viewportSize = GetViewportRect().Size;
		container.Position = new Vector2(
			viewportSize.X - container.Size.X - RightMargin,
			TopMargin
		);
	}

	private void AttachToTopUi()
	{
		if (!IsInsideTree())
			return;

		Node currentScene = GetTree().CurrentScene;
		if (currentScene == null)
			return;

		Node topUi = FindTopUi(currentScene);
		if (topUi == null)
			return;

		if (GetParent() != topUi)
		{
			Node oldParent = GetParent();
			oldParent?.RemoveChild(this);
			topUi.AddChild(this);
		}

		attachedToTopUi = true;
		SetDisplayPosition();
	}

	private Node FindTopUi(Node root)
	{
		Node topUi = root.FindChild("TopUI", true, false);
		if (topUi != null)
			return topUi;

		topUi = root.FindChild("TopUi", true, false);
		if (topUi != null)
			return topUi;

		Node rainDisplay = root.FindChild("GraphicalRainDisplay", true, false);
		return rainDisplay?.GetParent();
	}

	private void FindSellEnergyButton()
	{
		if (sellEnergyButton != null && IsInstanceValid(sellEnergyButton))
			return;

		Node root = GetTree().CurrentScene;
		if (root == null)
			return;

		foreach (Node node in root.FindChildren("*", "Button", true, false))
		{
			if (node is not Button button)
				continue;

			string text = button.Text.Trim().ToLowerInvariant();
			string nodeName = button.Name.ToString().Trim().ToLowerInvariant();

			if (
				text == "sell energy" ||
				text.Replace(" ", "") == "sellenergy" ||
				nodeName.Replace(" ", "").Contains("sellenergy")
			)
			{
				sellEnergyButton = button;
				return;
			}
		}
	}

	private void UpdateDisplay()
	{
		if (energyLabel == null || dollarsLabel == null)
			return;

		if (EnergySystem.Instance == null)
		{
			energyLabel.Text = "Energy: 0.00";
			dollarsLabel.Text = "Dollars: $0.00";
			return;
		}

		EnergySystem economy = EnergySystem.Instance;
		energyLabel.Text = "Energy: " + economy.Energy.ToString("F2");
		dollarsLabel.Text = "Dollars: $" + economy.Dollars.ToString("F2");

		if (sellEnergyButton != null)
			sellEnergyButton.Disabled = economy.Energy < EnergySystem.EnergyPerDollar;
	}
}
