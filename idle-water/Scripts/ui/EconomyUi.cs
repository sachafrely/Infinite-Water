using Godot;

/// <summary>
/// Small economy display for the existing TopUI plus the Sell Energy action.
/// This is intentionally isolated from the fluid renderer and simulation code.
/// </summary>
public partial class EconomyUi : Control
{
	private const float RightMargin = 16.0f;
	private const float TopMargin = 8.0f;

	private Label energyLabel;
	private Label dollarsLabel;
	private Button sellEnergyButton;
	private bool attachedToTopUi;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		TopLevel = true;
		ZIndex = 1000;

		CreateDisplay();
		CallDeferred(nameof(AttachToTopUi));
		CallDeferred(nameof(EnsureSellEnergyButton));
	}

	public override void _Process(double delta)
	{
		if (!attachedToTopUi)
			AttachToTopUi();

		EnsureSellEnergyButton();
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

		Node topUi = currentScene.FindChild("TopUI", true, false)
			?? currentScene.FindChild("TopUi", true, false);

		if (topUi == null)
			return;

		if (GetParent() != topUi)
		{
			GetParent()?.RemoveChild(this);
			topUi.AddChild(this);
		}

		attachedToTopUi = true;
		SetDisplayPosition();
	}

	private void EnsureSellEnergyButton()
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

			string text = button.Text.Trim().ToLowerInvariant().Replace(" ", "");
			string name = button.Name.ToString().Trim().ToLowerInvariant().Replace(" ", "");

			if (text == "sellenergy" || name.Contains("sellenergy"))
			{
				sellEnergyButton = button;
				if (!sellEnergyButton.IsConnected(Button.SignalName.Pressed, Callable.From(OnSellEnergyPressed)))
					sellEnergyButton.Pressed += OnSellEnergyPressed;
				return;
			}
		}

		Node bottomUi = root.FindChild("BottomUI", true, false)
			?? root.FindChild("BottomUi", true, false);

		if (bottomUi is Control bottomControl)
		{
			Button button = new Button();
			button.Name = "SellEnergyButton";
			button.Text = "Sell Energy";
			button.CustomMinimumSize = new Vector2(120, 40);
			button.Pressed += OnSellEnergyPressed;
			bottomControl.AddChild(button);
			sellEnergyButton = button;
		}
	}

	private void OnSellEnergyPressed()
	{
		EnergySystem.Instance?.TrySellEnergyChunk();
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		if (energyLabel == null || dollarsLabel == null)
			return;

		EnergySystem economy = EnergySystem.Instance;
		if (economy == null)
		{
			energyLabel.Text = "Energy: 0.00";
			dollarsLabel.Text = "Dollars: $0.00";
			return;
		}

		energyLabel.Text = "Energy: " + economy.Energy.ToString("F2");
		dollarsLabel.Text = "Dollars: $" + economy.Dollars.ToString("F2");

		if (sellEnergyButton != null)
			sellEnergyButton.Disabled = economy.Energy < EnergySystem.EnergyPerDollar;
	}
}
