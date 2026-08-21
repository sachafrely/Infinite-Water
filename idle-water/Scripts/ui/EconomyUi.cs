using System.Reflection;
using Godot;

/// <summary>
/// Small economy and rain display for the existing TopUI plus the Sell Energy action.
/// This is intentionally isolated from the fluid renderer and simulation code.
/// </summary>
public partial class EconomyUi : Control
{
	private const float RightMargin = 16.0f;
	private const float TopMargin = 8.0f;

	private Label energyLabel;
	private Label dollarsLabel;
	private Button sellEnergyButton;
	private Control rainDisplay;
	private bool attachedToTopUi;
	private FieldInfo rainSystemField;
	private PropertyInfo rainPercentProperty;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		TopLevel = true;
		ZIndex = 1000;

		CreateDisplay();
		CreateRainDisplay();
		CacheRainAccess();
		CallDeferred(nameof(AttachToTopUi));
		CallDeferred(nameof(EnsureSellEnergyButton));
	}

	public override void _Process(double delta)
	{
		if (!attachedToTopUi)
			AttachToTopUi();

		EnsureSellEnergyButton();
		UpdateDisplay();
		UpdateRainDisplay();
		HideLegacyRainText();
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

	private void CreateRainDisplay()
	{
		rainDisplay = new RainAmountDisplay();
		rainDisplay.Name = "RainAmountDisplay";
		rainDisplay.MouseFilter = MouseFilterEnum.Ignore;
		// 150% of the original 180x34 display.
		rainDisplay.CustomMinimumSize = new Vector2(270.0f, 51.0f);
		AddChild(rainDisplay);
	}

	private void CacheRainAccess()
	{
		try
		{
			rainSystemField = typeof(FluidSimulator).GetField("rainSystem", BindingFlags.Instance | BindingFlags.NonPublic);
			if (rainSystemField != null)
				rainPercentProperty = rainSystemField.FieldType.GetProperty("CurrentRainPercent");
		}
		catch
		{
			rainSystemField = null;
			rainPercentProperty = null;
		}
	}

	private float GetCurrentRainPercent()
	{
		Node root = GetTree().CurrentScene;
		if (root == null || rainSystemField == null || rainPercentProperty == null)
			return 0.0f;

		FluidSimulator simulator = root.FindChild("FluidSimulation", true, false) as FluidSimulator;
		if (simulator == null)
			return 0.0f;

		try
		{
			object rainSystem = rainSystemField.GetValue(simulator);
			if (rainSystem == null)
				return 0.0f;
			object value = rainPercentProperty.GetValue(rainSystem);
			return value is float percent ? percent : 0.0f;
		}
		catch
		{
			return 0.0f;
		}
	}

	private void UpdateRainDisplay()
	{
		if (rainDisplay is RainAmountDisplay display)
			display.RainPercent = GetCurrentRainPercent();
	}

	private void HideLegacyRainText()
	{
		Node root = GetTree().CurrentScene;
		if (root == null)
			return;

		foreach (Node node in root.FindChildren("*", "Label", true, false))
		{
			if (node is not Label label)
				continue;
			if (label == energyLabel || label == dollarsLabel)
				continue;

			string text = label.Text.ToUpperInvariant();
			if (text.Contains("RAIN") && text.Contains("NEXT CHANGE"))
				label.Visible = false;
		}
	}

	private void SetDisplayPosition()
	{
		if (!IsInsideTree())
			return;

		Control container = GetNodeOrNull<Control>("ResourceDisplay");
		if (container != null)
		{
			container.ResetSize();
			Vector2 viewportSize = GetViewportRect().Size;
			container.Position = new Vector2(viewportSize.X - container.Size.X - RightMargin, TopMargin);
		}

		if (rainDisplay != null)
			rainDisplay.Position = new Vector2(16.0f, 12.0f);
	}

	private void AttachToTopUi()
	{
		if (!IsInsideTree())
			return;

		Node currentScene = GetTree().CurrentScene;
		if (currentScene == null)
			return;

		Node topUi = currentScene.FindChild("TopUI", true, false) ?? currentScene.FindChild("TopUi", true, false);
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

		Node bottomUi = root.FindChild("BottomUI", true, false) ?? root.FindChild("BottomUi", true, false);
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
		EnergySystem.Instance?.SellAllAvailableEnergy();
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		if (energyLabel == null || dollarsLabel == null)
			return;

		EnergySystem economy = EnergySystem.Instance;
		if (economy == null)
		{
			energyLabel.Text = "Energy: 0";
			dollarsLabel.Text = "Dollars: $0";
			return;
		}

		energyLabel.Text = "Energy: " + System.Math.Floor(economy.Energy).ToString("F0");
		dollarsLabel.Text = "Dollars: $" + System.Math.Floor(economy.Dollars).ToString("F0");

		if (sellEnergyButton != null)
			sellEnergyButton.Disabled = economy.Energy < EnergySystem.EnergyPerDollar;
	}
}

/// <summary>
/// Ten-segment graphical rain amount indicator.
/// 0% = all grey, 10% = one blue segment, ... 100% = ten blue segments.
/// </summary>
internal sealed partial class RainAmountDisplay : Control
{
	private const int SegmentCount = 10;
	private const float SegmentWidth = 21.0f;
	private const float SegmentHeight = 42.0f;
	private const float SegmentGap = 4.5f;
	private float rainPercent;

	public float RainPercent
	{
		get => rainPercent;
		set
		{
			rainPercent = Mathf.Clamp(value, 0.0f, 100.0f);
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		int activeSegments = Mathf.RoundToInt(rainPercent / 10.0f);
		float totalWidth = SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentGap;
		float startX = Mathf.Max(0.0f, (Size.X - totalWidth) * 0.5f);
		float startY = Mathf.Max(0.0f, (Size.Y - SegmentHeight) * 0.5f);

		for (int i = 0; i < SegmentCount; i++)
		{
			bool active = i < activeSegments;
			Color fill = active ? UiSettings.FontColorWater : new Color(0.30f, 0.30f, 0.30f, 1.0f);
			Rect2 rect = new Rect2(startX + i * (SegmentWidth + SegmentGap), startY, SegmentWidth, SegmentHeight);
			DrawRect(rect, fill, true);
			DrawRect(rect, UiSettings.BorderColor, false, 1.0f);
		}
	}
}
