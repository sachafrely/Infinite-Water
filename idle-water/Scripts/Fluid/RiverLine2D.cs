using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Editable river wall.
///
/// Add this node to the simulation viewport and edit its points directly
/// with Godot's Line2D handles. The same line is rendered in-game and
/// converted into thin polygon colliders for the PBF solver.
///
/// Each segment becomes its own convex rectangle.
/// </summary>
[Tool]
public partial class RiverLine2D : Line2D
{
	// ============================================================
	// Collision
	// ============================================================

	[Export]
	public float CollisionThickness { get; set; } = 20.0f;

	[Export]
	public bool RegisterAsCollision { get; set; } = true;

	// ============================================================
	// Visual
	// ============================================================

	[Export]
	public bool ShowInGame { get; set; } = true;

	[Export]
	public Color LineColor { get; set; } =
		new Color(
			0.22f,
			0.15f,
			0.09f,
			1.0f
		);

	// ============================================================
	// Runtime state
	// ============================================================

	private readonly List<FluidPolygonCollider>
		registeredColliders =
			new List<FluidPolygonCollider>();

	private bool registered;

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		ApplyVisualSettings();

		if (
			!Engine.IsEditorHint() &&
			RegisterAsCollision)
		{
			CallDeferred(
				nameof(RegisterWithFluidSolver)
			);
		}
	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(
		double delta)
	{
		if (Engine.IsEditorHint())
		{
			ApplyVisualSettings();

			QueueRedraw();
		}
	}

	// ============================================================
	// Visual settings
	// ============================================================

	private void ApplyVisualSettings()
	{
		Width =
			Mathf.Max(
				1.0f,
				CollisionThickness
			);

		DefaultColor =
			LineColor;

		JointMode =
			Line2D.LineJointMode.Round;

		BeginCapMode =
			Line2D.LineCapMode.Round;

		EndCapMode =
			Line2D.LineCapMode.Round;

		Antialiased =
			false;

		// Draw the river wall above the water.
		ZIndex =
			50;

		if (Engine.IsEditorHint())
		{
			Modulate =
				new Color(
					1.0f,
					1.0f,
					1.0f,
					1.0f
				);
		}
		else
		{
			Modulate =
				ShowInGame
					? new Color(
						1.0f,
						1.0f,
						1.0f,
						1.0f
					)
					: new Color(
						1.0f,
						1.0f,
						1.0f,
						0.0f
					);
		}
	}

	// ============================================================
	// Register collision with PBF solver
	// ============================================================

	private void RegisterWithFluidSolver()
	{
		if (
			registered ||
			!IsInsideTree())
		{
			return;
		}

		// Vector2[] uses Length, not Count.
		if (
			Points == null ||
			Points.Length < 2)
		{
			GD.PushWarning(
				$"RiverLine2D '{Name}' " +
				"needs at least two points."
			);

			return;
		}

		FluidSimulator simulator =
			FindFluidSimulator(
				GetTree().Root
			);

		if (simulator == null)
		{
			GD.PushWarning(
				$"RiverLine2D '{Name}' " +
				"could not find a FluidSimulator."
			);

			return;
		}

		// ========================================================
		// Access the existing solver.
		//
		// We deliberately don't modify FluidSimulator just for
		// this feature. The existing solver already exposes
		// AddPolygonCollider().
		// ========================================================

		FieldInfo solverField =
			typeof(FluidSimulator).GetField(
				"solver",
				BindingFlags.Instance |
				BindingFlags.NonPublic
			);

		if (solverField == null)
		{
			GD.PushError(
				"RiverLine2D could not access " +
				"FluidSimulator.solver."
			);

			return;
		}

		PbfSolver solver =
			solverField.GetValue(
				simulator
			) as PbfSolver;

		if (solver == null)
		{
			GD.PushWarning(
				$"RiverLine2D '{Name}' found the " +
				"FluidSimulator, but its solver " +
				"is not initialized yet."
			);

			CallDeferred(
				nameof(RegisterWithFluidSolver)
			);

			return;
		}

		// ========================================================
		// Build one convex collider per line segment.
		// ========================================================

		ClearRegisteredColliders();

		for (
			int i = 0;
			i < Points.Length - 1;
			i++)
		{
			Vector2 a =
				ToSimulatorSpace(
					simulator,
					Points[i]
				);

			Vector2 b =
				ToSimulatorSpace(
					simulator,
					Points[i + 1]
				);

			if (
				(b - a).LengthSquared() <
				0.0001f)
			{
				continue;
			}

			Vector2[] polygon =
				BuildSegmentPolygon(
					a,
					b,
					CollisionThickness
				);

			if (
				polygon.Length < 3)
			{
				continue;
			}

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					polygon
				);

			solver.AddPolygonCollider(
				collider
			);

			registeredColliders.Add(
				collider
			);
		}

		registered =
			true;

		GD.Print(
			$"RiverLine2D '{Name}' registered " +
			$"{registeredColliders.Count} " +
			"collision segments."
		);
	}

	// ============================================================
	// Convert line point into FluidSimulator coordinates
	// ============================================================

	private Vector2 ToSimulatorSpace(
		FluidSimulator simulator,
		Vector2 localPoint)
	{
		Vector2 globalPoint =
			ToGlobal(
				localPoint
			);

		return simulator.ToLocal(
			globalPoint
		);
	}

	// ============================================================
	// Build thick rectangular collision
	// ============================================================

	private static Vector2[] BuildSegmentPolygon(
		Vector2 a,
		Vector2 b,
		float thickness)
	{
		Vector2 direction =
			b - a;

		float length =
			direction.Length();

		if (length <= 0.0001f)
		{
			return Array.Empty<Vector2>();
		}

		direction /=
			length;

		Vector2 normal =
			new Vector2(
				-direction.Y,
				direction.X
			);

		float halfThickness =
			Mathf.Max(
				1.0f,
				thickness
			) * 0.5f;

		Vector2 offset =
			normal *
			halfThickness;

		return new[]
		{
			a + offset,
			b + offset,
			b - offset,
			a - offset
		};
	}

	// ============================================================
	// Clear local collider references
	// ============================================================

	private void ClearRegisteredColliders()
	{
		registeredColliders.Clear();
	}

	// ============================================================
	// Find FluidSimulator anywhere in scene
	// ============================================================

	private static FluidSimulator FindFluidSimulator(
		Node node)
	{
		if (
			node is FluidSimulator simulator)
		{
			return simulator;
		}

		foreach (
			Node child in node.GetChildren())
		{
			FluidSimulator result =
				FindFluidSimulator(
					child
				);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Exit
	// ============================================================

	public override void _ExitTree()
	{
		registered =
			false;

		registeredColliders.Clear();
	}
}
