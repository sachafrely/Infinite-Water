using Godot;

/// <summary>
/// Visual configuration and fallback creation for the BottomUi Sell Energy button.
/// If the scene already contains the button, it is reused. If the scene is missing
/// the visible button, one is created under BottomUI so the economy action is usable.
/// </summary>
public partial class SellEnergyButton : Node
{
	private Button button;

	public override void _Ready()
	{
		button = FindButton();

		if (button == null)
			button = CreateFallbackButton();

		if (button == null)
			return;

		button.Visible = true;
		button.ZIndex = 1000;
		button.CustomMinimumSize = new Vector2(150.0f, 44.0f);
		ApplyButtonStyle();
	}

	private Button FindButton()
	{
		Node current = GetParent();
		while (current != null)
		{
			if (current is Button parentButton)
				return parentButton;

			current = current.GetParent();
		}

		foreach (Node node in FindChildren("*", "Button", true, false))
		{
			if (node is Button childButton)
				return childButton;
		}

		Node root = GetTree().CurrentScene;
		if (root == null)
			return null;

		foreach (Node node in root.FindChildren("*", "Button", true, false))
		{
			if (node is not Button candidate)
				continue;

			string text = candidate.Text.Trim().ToLowerInvariant();
			string name = candidate.Name.ToString().Trim().ToLowerInvariant();

			if (text == "sell energy" ||
				text.Replace(" ", "") == "sellenergy" ||
				name.Replace(" ", "").Contains("sellenergy"))
			{
				return candidate;
			}
		}

		return null;
	}

	private Button CreateFallbackButton()
	{
		Node root = GetTree().CurrentScene;
		if (root == null)
			return null;

		Node bottomUi = root.FindChild("BottomUI", true, false);
		if (bottomUi == null)
			bottomUi = root.FindChild("BottomUi", true, false);

		Node parent = bottomUi ?? root;

		Button fallback = new Button();
		fallback.Name = "SellEnergyButton";
		fallback.Text = "Sell Energy";
		fallback.Size = new Vector2(150.0f, 44.0f);
		fallback.CustomMinimumSize = new Vector2(150.0f, 44.0f);
		fallback.ZIndex = 1000;
		fallback.MouseFilter = Control.MouseFilterEnum.Stop;
		fallback.Position = new Vector2(16.0f, -60.0f);
		parent.AddChild(fallback);

		GD.Print("SellEnergyButton: Created fallback visible Sell Energy button.");
		return fallback;
	}

	private void ApplyButtonStyle()
	{
		button.AddThemeStyleboxOverride(
			"normal",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"pressed",
			CreateStyle(UiSettings.WindowColor)
		);

		button.AddThemeStyleboxOverride(
			"hover",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"focus",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"disabled",
			CreateStyle(UiSettings.ButtonColor)
		);
	}

	private StyleBoxFlat CreateStyle(Color backgroundColor)
	{
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = backgroundColor;
		style.BorderColor = UiSettings.BorderColor;
		style.SetBorderWidthAll((int)UiSettings.BorderSize);
		return style;
	}
}
