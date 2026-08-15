using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates PBF collision geometry from the visual Environment TileMapLayer.
///
/// Pipeline:
///
///     Environment TileMapLayer
///              ↓
///     solid pixel mask
///              ↓
///     horizontal solid runs
///              ↓
///     vertical run merging
///              ↓
///     adjacent rectangle merging
///              ↓
///     simulation-space rectangles
///              ↓
///     FluidPolygonCollider
///
/// Important:
///
/// The Environment TileMapLayer lives in the main scene canvas.
/// The simulation lives inside a SubViewport.
///
/// Coordinate conversion:
///
///     Environment local
///          ↓
///     main canvas/global coordinates
///          ↓
///     GameView local
///          ↓
///     SubViewport coordinates
///          ↓
///     Camera/simulation coordinates
///          ↓
///     PBF simulation coordinates
///
/// This version uses Godot 4 Transform2D multiplication instead of
/// the non-existent TransformPoint() method.
///
/// Collision geometry is generated only once unless Rebuild() or
/// GenerateColliders() is explicitly called.
///
/// No FluidPolygonCollider.Polygon property is required.
/// </summary>
[Tool]
public partial class TileMapPhysics : Node2D
{
	// ============================================================
	// Configuration
	// ============================================================

	[Export]
	public NodePath EnvironmentPath { get; set; } =
		new NodePath("../Environment");

	[Export]
	public bool GenerateOnReady { get; set; } = true;

	[Export]
	public bool DebugOutput { get; set; } = true;

	[Export]
	public bool ShowDebugGeometry { get; set; } = false;

	// ============================================================
	// Diagnostics
	// ============================================================

	[Export]
	public bool DiagnosticOutput { get; set; } = true;

	[Export]
	public int DiagnosticXBuckets { get; set; } = 10;

	[Export]
	public int DiagnosticLeftRectangleCount { get; set; } = 20;

	[Export]
	public float DiagnosticWorldMinX { get; set; } = 260.0f;

	[Export]
	public float DiagnosticWorldMaxX { get; set; } = 1180.0f;

	[Export]
	public float DiagnosticWorldMinY { get; set; } = -200.0f;

	[Export]
	public float DiagnosticWorldMaxY { get; set; } = 820.0f;

	[Export]
	public Color DebugColor { get; set; } =
		new Color(
			1.0f,
			0.2f,
			0.1f,
			0.9f
		);

	// ============================================================
	// Viewport references
	// ============================================================

	[Export]
	public NodePath GameViewPath { get; set; } =
		new NodePath("../GameView");

	[Export]
	public NodePath SimulationViewportPath { get; set; } =
		new NodePath("../GameView/SimulationViewport");

	[Export]
	public NodePath CameraPath { get; set; } =
		new NodePath("../GameView/SimulationViewport/Camera2D");

	// ============================================================
	// Texture classification
	// ============================================================

	[Export]
	public float AlphaThreshold { get; set; } = 0.01f;

	[Export]
	public Color EmptyColor { get; set; } =
		new Color(
			34.0f / 255.0f,
			42.0f / 255.0f,
			92.0f / 255.0f,
			1.0f
		);

	[Export]
	public float EmptyColorTolerance { get; set; } = 0.04f;

	[Export]
	public bool UseEmptyColorKey { get; set; } = true;

	// ============================================================
	// Solid-region generation
	// ============================================================

	[Export]
	public int MinimumRectangleWidth { get; set; } = 1;

	[Export]
	public int MinimumRectangleHeight { get; set; } = 1;

	[Export]
	public int MaximumSolidColliders { get; set; } = 5000;

	[Export]
	public bool MergeSolidRegions { get; set; } = true;

	// ============================================================
	// Runtime references
	// ============================================================

	private TileMapLayer environment;
	private FluidSimulator simulator;
	private PbfSolver solver;

	private SubViewportContainer gameView;
	private SubViewport simulationViewport;
	private Camera2D simulationCamera;

	// ============================================================
	// Generated collision
	// ============================================================

	private readonly List<FluidPolygonCollider> generatedColliders =
		new List<FluidPolygonCollider>();

	private readonly List<DebugEdge> debugEdges =
		new List<DebugEdge>();

	private bool generated;
	private bool generating;

	// ============================================================
	// Debug edge
	// ============================================================

	private readonly struct DebugEdge
	{
		public readonly Vector2 A;
		public readonly Vector2 B;

		public DebugEdge(
			Vector2 a,
			Vector2 b)
		{
			A = a;
			B = b;
		}
	}

	// ============================================================
	// Solid rectangle
	// ============================================================

	private struct SolidRectangle
	{
		public int X;
		public int Y;
		public int Width;
		public int Height;

		public SolidRectangle(
			int x,
			int y,
			int width,
			int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}
	}

	// ============================================================
	// Horizontal run
	// ============================================================

	private readonly struct SolidRun
	{
		public readonly int X;
		public readonly int Width;

		public SolidRun(
			int x,
			int width)
		{
			X = x;
			Width = width;
		}
	}

	// ============================================================
	// Run key
	// ============================================================

	private readonly struct RunKey :
		IEquatable<RunKey>
	{
		public readonly int X;
		public readonly int Width;

		public RunKey(
			int x,
			int width)
		{
			X = x;
			Width = width;
		}

		public bool Equals(
			RunKey other)
		{
			return
				X == other.X &&
				Width == other.Width;
		}

		public override bool Equals(
			object obj)
		{
			return
				obj is RunKey &&
				Equals((RunKey)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return
					(X * 397) ^
					Width;
			}
		}
	}

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		GD.Print(
			"========== TILEMAP PHYSICS READY =========="
		);

		GD.Print(
			"TileMapPhysics node: " +
			GetPath()
		);

		GD.Print(
			"Process mode: " +
			ProcessMode
		);

		GD.Print(
			"ShowDebugGeometry: " +
			ShowDebugGeometry
		);

		GD.Print(
			"DiagnosticOutput: " +
			DiagnosticOutput
		);

		GD.Print(
			"Collision mode: SOLID REGION / RUN MERGING"
		);

		CallDeferred(
			nameof(Initialize)
		);
	}

	// ============================================================
	// Initialize
	// ============================================================

	private void Initialize()
	{
		if (generating)
			return;

		environment = GetEnvironment();

		if (environment == null)
		{
			GD.PushError(
				"TileMapPhysics: Environment TileMapLayer " +
				"could not be found."
			);

			return;
		}

		environment.ZIndex = 20;

		simulator =
			FindFluidSimulator(
				GetTree().Root
			);

		if (simulator == null)
		{
			GD.PushError(
				"TileMapPhysics: FluidSimulator " +
				"could not be found."
			);

			return;
		}

		solver =
			GetSolver(
				simulator
			);

		if (solver == null)
		{
			CallDeferred(
				nameof(Initialize)
			);

			return;
		}

		FindViewportMapping();

		if (!HasValidViewportMapping())
		{
			GD.PushError(
				"TileMapPhysics: Could not establish " +
				"viewport mapping."
			);

			return;
		}

		if (GenerateOnReady)
		{
			GenerateColliders();
		}
	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(
		double delta)
	{
		if (ShowDebugGeometry)
		{
			QueueRedraw();
		}
	}

	// ============================================================
	// Draw
	// ============================================================

	public override void _Draw()
	{
		if (!ShowDebugGeometry)
			return;

		if (!HasValidViewportMapping())
			return;

		foreach (DebugEdge edge in debugEdges)
		{
			Vector2 a =
				SimulationToThisLocal(
					edge.A
				);

			Vector2 b =
				SimulationToThisLocal(
					edge.B
				);

			DrawLine(
				a,
				b,
				DebugColor,
				1.0f,
				true
			);
		}
	}

	// ============================================================
	// Environment
	// ============================================================

	private TileMapLayer GetEnvironment()
	{
		if (
			EnvironmentPath != null &&
			!EnvironmentPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					EnvironmentPath
				);

			if (node is TileMapLayer)
			{
				return (TileMapLayer)node;
			}
		}

		return FindTileMapLayer(
			GetTree().Root
		);
	}

	private static TileMapLayer FindTileMapLayer(
		Node node)
	{
		if (node is TileMapLayer)
		{
			TileMapLayer layer =
				(TileMapLayer)node;

			if (layer.Name == "Environment")
			{
				return layer;
			}
		}

		foreach (Node child in node.GetChildren())
		{
			TileMapLayer result =
				FindTileMapLayer(child);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Viewport mapping
	// ============================================================

	private void FindViewportMapping()
	{
		gameView = null;
		simulationViewport = null;
		simulationCamera = null;

		// --------------------------------------------------------
		// GameView
		// --------------------------------------------------------

		if (
			GameViewPath != null &&
			!GameViewPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					GameViewPath
				);

			if (node is SubViewportContainer)
			{
				gameView =
					(SubViewportContainer)node;
			}
		}

		if (gameView == null)
		{
			gameView =
				FindNodeOfType<SubViewportContainer>(
					GetTree().Root
				);
		}

		// --------------------------------------------------------
		// Simulation viewport
		// --------------------------------------------------------

		if (
			SimulationViewportPath != null &&
			!SimulationViewportPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					SimulationViewportPath
				);

			if (node is SubViewport)
			{
				simulationViewport =
					(SubViewport)node;
			}
		}

		if (
			simulationViewport == null &&
			gameView != null)
		{
			foreach (Node child in gameView.GetChildren())
			{
				if (child is SubViewport)
				{
					simulationViewport =
						(SubViewport)child;

					break;
				}
			}
		}

		// --------------------------------------------------------
		// Camera
		// --------------------------------------------------------

		if (
			CameraPath != null &&
			!CameraPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					CameraPath
				);

			if (node is Camera2D)
			{
				simulationCamera =
					(Camera2D)node;
			}
		}

		if (
			simulationCamera == null &&
			simulationViewport != null)
		{
			simulationCamera =
				FindNodeOfType<Camera2D>(
					simulationViewport
				);
		}

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics viewport mapping:"
			);

			GD.Print(
				"  GameView: " +
				(
					gameView != null
						? gameView.GetPath().ToString()
						: "NULL"
				)
			);

			if (gameView != null)
			{
				GD.Print(
					"  GameView GlobalPosition: " +
					gameView.GlobalPosition
				);

				GD.Print(
					"  GameView Size: " +
					gameView.Size
				);

				GD.Print(
					"  GameView Scale: " +
					gameView.Scale
				);
			}

			GD.Print(
				"  SimulationViewport: " +
				(
					simulationViewport != null
						? simulationViewport.GetPath().ToString()
						: "NULL"
				)
			);

			if (simulationViewport != null)
			{
				GD.Print(
					"  SimulationViewport Size: " +
					simulationViewport.Size
				);
			}

			GD.Print(
				"  Camera: " +
				(
					simulationCamera != null
						? simulationCamera.GetPath().ToString()
						: "NULL"
				)
			);

			if (simulationCamera != null)
			{
				GD.Print(
					"  Camera Screen Center: " +
					simulationCamera.GetScreenCenterPosition()
				);
			}
		}
	}

	private bool HasValidViewportMapping()
	{
		return
			gameView != null &&
			simulationViewport != null &&
			simulationCamera != null &&
			gameView.Size.X > 0.0f &&
			gameView.Size.Y > 0.0f &&
			simulationViewport.Size.X > 0 &&
			simulationViewport.Size.Y > 0;
	}

	private static T FindNodeOfType<T>(
		Node node)
		where T : Node
	{
		if (node is T)
		{
			return (T)node;
		}

		foreach (Node child in node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(child);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Solver
	// ============================================================

	private static PbfSolver GetSolver(
		FluidSimulator fluidSimulator)
	{
		PbfSolver result =
			fluidSimulator?.GetPbfSolver();

		if (result == null)
		{
			GD.PushWarning(
				"TileMapPhysics: PbfSolver is not " +
				"available on FluidSimulator yet."
			);
		}

		return result;
	}

	// ============================================================
	// Generate colliders
	// ============================================================

	public void GenerateColliders()
	{
		if (generating)
			return;

		GD.Print(
			"========== GENERATE SOLID COLLIDERS =========="
		);

		// --------------------------------------------------------
		// Resolve references
		// --------------------------------------------------------

		if (environment == null)
		{
			environment =
				GetEnvironment();
		}

		if (simulator == null)
		{
			simulator =
				FindFluidSimulator(
					GetTree().Root
				);
		}

		if (
			solver == null &&
			simulator != null)
		{
			solver =
				GetSolver(
					simulator
				);
		}

		if (
			environment == null ||
			simulator == null ||
			solver == null)
		{
			GD.PushWarning(
				"TileMapPhysics: Required nodes are not ready."
			);

			return;
		}

		FindViewportMapping();

		if (!HasValidViewportMapping())
		{
			GD.PushError(
				"TileMapPhysics: Viewport mapping is invalid."
			);

			return;
		}

		// --------------------------------------------------------
		// Begin generation
		// --------------------------------------------------------

		generating = true;
		generated = false;

		solver.ClearPolygonColliders();

		generatedColliders.Clear();
		debugEdges.Clear();

		// --------------------------------------------------------
		// Build mask
		// --------------------------------------------------------

		ulong maskStart =
			Time.GetTicksMsec();

		HashSet<Vector2I> solidPixels =
			BuildGlobalSolidMask();

		ulong maskEnd =
			Time.GetTicksMsec();

		if (solidPixels.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No collision pixels found."
			);

			generating = false;

			return;
		}

		if (DiagnosticOutput)
		{
			PrintSolidMaskDiagnostics(
				solidPixels
			);
		}

		// --------------------------------------------------------
		// Build rectangles
		// --------------------------------------------------------

		ulong rectangleStart =
			Time.GetTicksMsec();

		List<SolidRectangle> rectangles =
			BuildSolidRectangles(
				solidPixels
			);

		ulong rectangleEnd =
			Time.GetTicksMsec();

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics: solid pixels = " +
				solidPixels.Count
			);

			GD.Print(
				"TileMapPhysics: generated solid rectangles = " +
				rectangles.Count
			);
		}

		if (DiagnosticOutput)
		{
			PrintRectangleDiagnostics(
				rectangles
			);

			PrintSimulationBoundsDiagnostics(
				rectangles
			);
		}

		// --------------------------------------------------------
		// Create colliders
		// --------------------------------------------------------

		ulong colliderStart =
			Time.GetTicksMsec();

		int generatedCount = 0;

		foreach (SolidRectangle rectangle in rectangles)
		{
			if (
				generatedCount >=
				MaximumSolidColliders)
			{
				GD.PushWarning(
					"TileMapPhysics: MaximumSolidColliders reached."
				);

				break;
			}

			if (
				rectangle.Width <
				MinimumRectangleWidth ||
				rectangle.Height <
				MinimumRectangleHeight)
			{
				continue;
			}

			if (
				!TryCreateSimulationRectangle(
					rectangle,
					out Vector2 min,
					out Vector2 max
				))
			{
				continue;
			}

			Vector2[] polygon =
			{
				new Vector2(min.X, min.Y),
				new Vector2(min.X, max.Y),
				new Vector2(max.X, max.Y),
				new Vector2(max.X, min.Y)
			};

			// FluidPolygonCollider expects clockwise polygons.
			if (PolygonArea(polygon) > 0.0f)
			{
				Array.Reverse(polygon);
			}

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					polygon
				);

			solver.AddPolygonCollider(
				collider
			);

			generatedColliders.Add(
				collider
			);

			generatedCount++;
		}

		ulong colliderEnd =
			Time.GetTicksMsec();

		// --------------------------------------------------------
		// Debug boundary
		// --------------------------------------------------------

		if (ShowDebugGeometry)
		{
			BuildDebugBoundary(
				solidPixels
			);
		}

		generated =
			generatedColliders.Count > 0;

		generating = false;

		// --------------------------------------------------------
		// Result
		// --------------------------------------------------------

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics SOLID COLLISION RESULT"
			);

			GD.Print(
				"Used cells: " +
				environment.GetUsedCells().Count
			);

			GD.Print(
				"Solid pixels: " +
				solidPixels.Count
			);

			GD.Print(
				"Solid rectangles: " +
				rectangles.Count
			);

			GD.Print(
				"Generated colliders: " +
				generatedColliders.Count
			);

			GD.Print(
				"Collision thickness: NOT USED"
			);

			GD.Print(
				"Collision extension: NOT USED"
			);

			GD.Print(
				"Dilation: NOT USED"
			);

			GD.Print(
				"========================================"
			);
		}

		if (DiagnosticOutput)
		{
			ulong total =
				(maskEnd - maskStart) +
				(rectangleEnd - rectangleStart) +
				(colliderEnd - colliderStart);

			GD.Print(
				"========== TILEMAP PHYSICS DIAGNOSTICS =========="
			);

			GD.Print(
				"Mask build: " +
				(maskEnd - maskStart) +
				" ms"
			);

			GD.Print(
				"Rectangle build: " +
				(rectangleEnd - rectangleStart) +
				" ms"
			);

			GD.Print(
				"Collider creation: " +
				(colliderEnd - colliderStart) +
				" ms"
			);

			GD.Print(
				"TOTAL GENERATION: " +
				total +
				" ms"
			);

			PrintRectangleStatistics(
				rectangles,
				solidPixels.Count
			);

			GD.Print(
				"================================================="
			);
		}

		QueueRedraw();
	}

	// ============================================================
	// Simulation rectangle conversion
	// ============================================================

	private bool TryCreateSimulationRectangle(
		SolidRectangle rectangle,
		out Vector2 min,
		out Vector2 max)
	{
		min = Vector2.Zero;
		max = Vector2.Zero;

		Vector2 topLeft =
			new Vector2(
				rectangle.X,
				rectangle.Y
			);

		Vector2 bottomRight =
			new Vector2(
				rectangle.X + rectangle.Width,
				rectangle.Y + rectangle.Height
			);

		Vector2 a =
			ToSimulationSpace(topLeft);

		Vector2 b =
			ToSimulationSpace(bottomRight);

		min = new Vector2(
			Mathf.Min(a.X, b.X),
			Mathf.Min(a.Y, b.Y)
		);

		max = new Vector2(
			Mathf.Max(a.X, b.X),
			Mathf.Max(a.Y, b.Y)
		);

		if (
			max.X - min.X <= 0.001f ||
			max.Y - min.Y <= 0.001f)
		{
			return false;
		}

		return true;
	}

	// ============================================================
	// Solid mask diagnostics
	// ============================================================

	private void PrintSolidMaskDiagnostics(
		HashSet<Vector2I> solidPixels)
	{
		if (
			solidPixels == null ||
			solidPixels.Count == 0)
		{
			return;
		}

		int minX = int.MaxValue;
		int maxX = int.MinValue;
		int minY = int.MaxValue;
		int maxY = int.MinValue;

		foreach (Vector2I p in solidPixels)
		{
			minX = Mathf.Min(minX, p.X);
			maxX = Mathf.Max(maxX, p.X);
			minY = Mathf.Min(minY, p.Y);
			maxY = Mathf.Max(maxY, p.Y);
		}

		Vector2 simulationMin =
			ToSimulationSpace(
				new Vector2(
					minX,
					minY
				)
			);

		Vector2 simulationMax =
			ToSimulationSpace(
				new Vector2(
					maxX + 1,
					maxY + 1
				)
			);

		float simulationMinX =
			Mathf.Min(
				simulationMin.X,
				simulationMax.X
			);

		float simulationMaxX =
			Mathf.Max(
				simulationMin.X,
				simulationMax.X
			);

		float simulationMinY =
			Mathf.Min(
				simulationMin.Y,
				simulationMax.Y
			);

		float simulationMaxY =
			Mathf.Max(
				simulationMin.Y,
				simulationMax.Y
			);

		GD.Print(
			"========== SOLID MASK DIAGNOSTIC =========="
		);

		GD.Print(
			"TileMap solid bounds: " +
			"X=" +
			minX +
			".." +
			maxX +
			" Y=" +
			minY +
			".." +
			maxY
		);

		GD.Print(
			"Simulation solid bounds: " +
			"X=" +
			simulationMinX.ToString("F1") +
			".." +
			simulationMaxX.ToString("F1") +
			" Y=" +
			simulationMinY.ToString("F1") +
			".." +
			simulationMaxY.ToString("F1")
		);

		GD.Print(
			"Simulation viewport X: 0.." +
			simulationViewport.Size.X
		);

		GD.Print(
			"Simulation viewport Y: 0.." +
			simulationViewport.Size.Y
		);

		GD.Print(
			"Configured simulation world: " +
			"X=" +
			DiagnosticWorldMinX.ToString("F1") +
			".." +
			DiagnosticWorldMaxX.ToString("F1") +
			" Y=" +
			DiagnosticWorldMinY.ToString("F1") +
			".." +
			DiagnosticWorldMaxY.ToString("F1")
		);

		GD.Print(
			"Solid pixel count: " +
			solidPixels.Count
		);

		PrintSolidPixelXBuckets(
			solidPixels,
			minX,
			maxX
		);

		int leftInspectionMax =
			minX +
			Mathf.Max(
				16,
				(maxX - minX) / 10
			);

		int leftCount = 0;

		foreach (Vector2I p in solidPixels)
		{
			if (p.X <= leftInspectionMax)
			{
				leftCount++;
			}
		}

		float leftPercent =
			solidPixels.Count > 0
				? 100.0f *
				  leftCount /
				  solidPixels.Count
				: 0.0f;

		GD.Print(
			"Left terrain region: X=" +
			minX +
			".." +
			leftInspectionMax
		);

		GD.Print(
			"Left terrain pixels: " +
			leftCount +
			" (" +
			leftPercent.ToString("F2") +
			"%)"
		);

		Vector2 leftSimulation =
			ToSimulationSpace(
				new Vector2(
					minX,
					(minY + maxY) * 0.5f
				)
			);

		GD.Print(
			"Left terrain representative simulation X: " +
			leftSimulation.X.ToString("F2")
		);

		if (
			leftSimulation.X <
			DiagnosticWorldMinX)
		{
			GD.Print(
				"WARNING: Left terrain is outside the configured " +
				"simulation-world minimum X."
			);
		}

		GD.Print(
			"============================================"
		);
	}

	// ============================================================
	// Solid X distribution
	// ============================================================

	private void PrintSolidPixelXBuckets(
		HashSet<Vector2I> solidPixels,
		int minX,
		int maxX)
	{
		int bucketCount =
			Mathf.Clamp(
				DiagnosticXBuckets,
				2,
				32
			);

		int[] buckets =
			new int[bucketCount];

		int range =
			Mathf.Max(
				1,
				maxX - minX + 1
			);

		foreach (Vector2I p in solidPixels)
		{
			int index =
				(int)(
					(long)(p.X - minX) *
					bucketCount /
					range
				);

			index =
				Mathf.Clamp(
					index,
					0,
					bucketCount - 1
				);

			buckets[index]++;
		}

		GD.Print(
			"Solid pixel X distribution:"
		);

		for (
			int i = 0;
			i < bucketCount;
			i++)
		{
			int bucketMin =
				minX +
				range *
				i /
				bucketCount;

			int bucketMax =
				minX +
				range *
				(i + 1) /
				bucketCount -
				1;

			GD.Print(
				"  X " +
				bucketMin +
				".." +
				bucketMax +
				": " +
				buckets[i] +
				" pixels"
			);
		}
	}

	// ============================================================
	// Rectangle diagnostics
	// ============================================================

	private void PrintRectangleDiagnostics(
		List<SolidRectangle> rectangles)
	{
		if (
			rectangles == null ||
			rectangles.Count == 0)
		{
			return;
		}

		GD.Print(
			"========== COLLISION RECTANGLE DIAGNOSTIC =========="
		);

		List<int> leftIndices =
			new List<int>(
				rectangles.Count
			);

		for (
			int i = 0;
			i < rectangles.Count;
			i++)
		{
			leftIndices.Add(i);
		}

		leftIndices.Sort(
			(a, b) =>
			{
				int result =
					rectangles[a].X.CompareTo(
						rectangles[b].X
					);

				if (result != 0)
					return result;

				return rectangles[a].Y.CompareTo(
					rectangles[b].Y
				);
			}
		);

		int printCount =
			Mathf.Min(
				DiagnosticLeftRectangleCount,
				leftIndices.Count
			);

		GD.Print(
			"Leftmost generated rectangles:"
		);

		for (
			int n = 0;
			n < printCount;
			n++)
		{
			SolidRectangle r =
				rectangles[leftIndices[n]];

			Vector2 a =
				ToSimulationSpace(
					new Vector2(
						r.X,
						r.Y
					)
				);

			Vector2 b =
				ToSimulationSpace(
					new Vector2(
						r.X + r.Width,
						r.Y + r.Height
					)
				);

			float minX = Mathf.Min(a.X, b.X);
			float maxX = Mathf.Max(a.X, b.X);
			float minY = Mathf.Min(a.Y, b.Y);
			float maxY = Mathf.Max(a.Y, b.Y);

			GD.Print(
				"  #" +
				n +
				" Tile=(" +
				r.X +
				"," +
				r.Y +
				") size=(" +
				r.Width +
				"x" +
				r.Height +
				") Simulation=(" +
				minX.ToString("F1") +
				".." +
				maxX.ToString("F1") +
				"," +
				minY.ToString("F1") +
				".." +
				maxY.ToString("F1") +
				")"
			);
		}

		int minRectX = int.MaxValue;
		int maxRectX = int.MinValue;

		foreach (SolidRectangle r in rectangles)
		{
			minRectX =
				Mathf.Min(
					minRectX,
					r.X
				);

			maxRectX =
				Mathf.Max(
					maxRectX,
					r.X + r.Width
				);
		}

		int rectRange =
			Mathf.Max(
				1,
				maxRectX - minRectX
			);

		int leftThreshold =
			minRectX +
			Mathf.Max(
				16,
				rectRange / 10
			);

		int leftRectangles = 0;
		float leftArea = 0.0f;
		float totalArea = 0.0f;

		foreach (SolidRectangle r in rectangles)
		{
			float area =
				r.Width *
				r.Height;

			totalArea += area;

			if (r.X <= leftThreshold)
			{
				leftRectangles++;
				leftArea += area;
			}
		}

		float leftRectanglePercent =
			rectangles.Count > 0
				? 100.0f *
				  leftRectangles /
				  rectangles.Count
				: 0.0f;

		float leftAreaPercent =
			totalArea > 0.0f
				? 100.0f *
				  leftArea /
				  totalArea
				: 0.0f;

		GD.Print(
			"Rectangle tile X bounds: " +
			minRectX +
			".." +
			maxRectX
		);

		GD.Print(
			"Left-side threshold: X <= " +
			leftThreshold
		);

		GD.Print(
			"Left-side rectangles: " +
			leftRectangles +
			" (" +
			leftRectanglePercent.ToString("F2") +
			"%)"
		);

		GD.Print(
			"Left-side rectangle area: " +
			leftArea.ToString("F0") +
			" (" +
			leftAreaPercent.ToString("F2") +
			"%)"
		);

		int verticalLikeRectangles = 0;

		foreach (SolidRectangle r in rectangles)
		{
			if (
				r.Height >= 4 * r.Width &&
				r.X <= leftThreshold)
			{
				verticalLikeRectangles++;
			}
		}

		GD.Print(
			"Left-side tall/vertical rectangles: " +
			verticalLikeRectangles
		);

		GD.Print(
			"===================================================="
		);
	}

	// ============================================================
	// Simulation bounds diagnostics
	// ============================================================

	private void PrintSimulationBoundsDiagnostics(
		List<SolidRectangle> rectangles)
	{
		if (
			rectangles == null ||
			rectangles.Count == 0)
		{
			return;
		}

		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minY = float.MaxValue;
		float maxY = float.MinValue;

		int outsideCount = 0;
		int outsideLeft = 0;
		int outsideRight = 0;
		int outsideTop = 0;
		int outsideBottom = 0;

		foreach (SolidRectangle r in rectangles)
		{
			Vector2 a =
				ToSimulationSpace(
					new Vector2(
						r.X,
						r.Y
					)
				);

			Vector2 b =
				ToSimulationSpace(
					new Vector2(
						r.X + r.Width,
						r.Y + r.Height
					)
				);

			float rectMinX = Mathf.Min(a.X, b.X);
			float rectMaxX = Mathf.Max(a.X, b.X);
			float rectMinY = Mathf.Min(a.Y, b.Y);
			float rectMaxY = Mathf.Max(a.Y, b.Y);

			minX = Mathf.Min(minX, rectMinX);
			maxX = Mathf.Max(maxX, rectMaxX);
			minY = Mathf.Min(minY, rectMinY);
			maxY = Mathf.Max(maxY, rectMaxY);

			bool left =
				rectMinX <
				DiagnosticWorldMinX;

			bool right =
				rectMaxX >
				DiagnosticWorldMaxX;

			bool top =
				rectMinY <
				DiagnosticWorldMinY;

			bool bottom =
				rectMaxY >
				DiagnosticWorldMaxY;

			if (
				left ||
				right ||
				top ||
				bottom)
			{
				outsideCount++;

				if (left)
					outsideLeft++;

				if (right)
					outsideRight++;

				if (top)
					outsideTop++;

				if (bottom)
					outsideBottom++;
			}
		}

		GD.Print(
			"========== SIMULATION COLLISION BOUNDS =========="
		);

		GD.Print(
			"Generated collider simulation bounds: " +
			"X=" +
			minX.ToString("F1") +
			".." +
			maxX.ToString("F1") +
			" Y=" +
			minY.ToString("F1") +
			".." +
			maxY.ToString("F1")
		);

		GD.Print(
			"Expected simulation bounds: " +
			"X=" +
			DiagnosticWorldMinX.ToString("F1") +
			".." +
			DiagnosticWorldMaxX.ToString("F1") +
			" Y=" +
			DiagnosticWorldMinY.ToString("F1") +
			".." +
			DiagnosticWorldMaxY.ToString("F1")
		);

		GD.Print(
			"Rectangles outside expected world: " +
			outsideCount +
			"/" +
			rectangles.Count
		);

		GD.Print(
			"  Outside left: " +
			outsideLeft
		);

		GD.Print(
			"  Outside right: " +
			outsideRight
		);

		GD.Print(
			"  Outside top: " +
			outsideTop
		);

		GD.Print(
			"  Outside bottom: " +
			outsideBottom
		);

		GD.Print(
			"================================================="
		);
	}

	// ============================================================
	// Rectangle statistics
	// ============================================================

	private void PrintRectangleStatistics(
		List<SolidRectangle> rectangles,
		int solidPixelCount)
	{
		if (
			rectangles == null ||
			rectangles.Count == 0)
		{
			return;
		}

		float minWidth = float.MaxValue;
		float maxWidth = float.MinValue;
		float minHeight = float.MaxValue;
		float maxHeight = float.MinValue;

		float totalWidth = 0.0f;
		float totalHeight = 0.0f;
		float totalArea = 0.0f;

		foreach (SolidRectangle r in rectangles)
		{
			float width = r.Width;
			float height = r.Height;
			float area = width * height;

			minWidth = Mathf.Min(minWidth, width);
			maxWidth = Mathf.Max(maxWidth, width);

			minHeight = Mathf.Min(minHeight, height);
			maxHeight = Mathf.Max(maxHeight, height);

			totalWidth += width;
			totalHeight += height;
			totalArea += area;
		}

		float count = rectangles.Count;

		GD.Print(
			"Collider width min/avg/max: " +
			minWidth.ToString("F2") +
			" / " +
			(totalWidth / count).ToString("F2") +
			" / " +
			maxWidth.ToString("F2")
		);

		GD.Print(
			"Collider height min/avg/max: " +
			minHeight.ToString("F2") +
			" / " +
			(totalHeight / count).ToString("F2") +
			" / " +
			maxHeight.ToString("F2")
		);

		GD.Print(
			"Approx collider area: " +
			totalArea.ToString("F0")
		);

		GD.Print(
			"Solid pixels / rectangle: " +
			(
				solidPixelCount /
				count
			).ToString("F2")
		);
	}

	// ============================================================
	// Build global solid mask
	// ============================================================

	private HashSet<Vector2I>
		BuildGlobalSolidMask()
	{
		HashSet<Vector2I> solidPixels =
			new HashSet<Vector2I>();

		if (
			environment == null ||
			environment.TileSet == null)
		{
			return solidPixels;
		}

		TileSet tileSet =
			environment.TileSet;

		Vector2 tileSize =
			GetTileSize();

		Godot.Collections.Array<Vector2I> cells =
			environment.GetUsedCells();

		Dictionary<int, Image> imageCache =
			new Dictionary<int, Image>();

		int processedCells = 0;
		int collisionPixels = 0;
		int ignoredBackgroundPixels = 0;
		int ignoredTransparentPixels = 0;

		foreach (Vector2I cell in cells)
		{
			int sourceId =
				environment.GetCellSourceId(cell);

			if (sourceId < 0)
				continue;

			Vector2I atlasCoords =
				environment.GetCellAtlasCoords(cell);

			if (
				atlasCoords.X < 0 ||
				atlasCoords.Y < 0)
			{
				continue;
			}

			TileSetSource source =
				tileSet.GetSource(sourceId);

			if (!(source is TileSetAtlasSource))
				continue;

			TileSetAtlasSource atlas =
				(TileSetAtlasSource)source;

			if (!atlas.HasTile(atlasCoords))
				continue;

			Texture2D texture =
				atlas.Texture;

			if (texture == null)
				continue;

			Image image;

			if (
				!imageCache.TryGetValue(
					sourceId,
					out image
				))
			{
				image =
					texture.GetImage();

				if (image == null)
					continue;

				imageCache[sourceId] = image;
			}

			Rect2I region =
				atlas.GetTileTextureRegion(
					atlasCoords
				);

			Rect2I textureRect =
				new Rect2I(
					0,
					0,
					image.GetWidth(),
					image.GetHeight()
				);

			Rect2I clipped =
				region.Intersection(
					textureRect
				);

			if (
				clipped.Size.X <= 0 ||
				clipped.Size.Y <= 0)
			{
				continue;
			}

			int width =
				clipped.Size.X;

			int height =
				clipped.Size.Y;

			float scaleX =
				tileSize.X /
				Mathf.Max(1, width);

			float scaleY =
				tileSize.Y /
				Mathf.Max(1, height);

			Vector2 cellCenter =
				environment.MapToLocal(cell);

			Vector2 tileTopLeft =
				cellCenter -
				tileSize * 0.5f;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Color pixel =
						image.GetPixel(
							clipped.Position.X + x,
							clipped.Position.Y + y
						);

					if (
						pixel.A <
						AlphaThreshold)
					{
						ignoredTransparentPixels++;
						continue;
					}

					if (
						UseEmptyColorKey &&
						IsEmptyBackgroundPixel(pixel))
					{
						ignoredBackgroundPixels++;
						continue;
					}

					int worldX =
						Mathf.FloorToInt(
							tileTopLeft.X +
							x * scaleX
						);

					int worldY =
						Mathf.FloorToInt(
							tileTopLeft.Y +
							y * scaleY
						);

					solidPixels.Add(
						new Vector2I(
							worldX,
							worldY
						)
					);

					collisionPixels++;
				}
			}

			processedCells++;
		}

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics: processed " +
				processedCells +
				" cells."
			);

			GD.Print(
				"TileMapPhysics: collision pixels = " +
				collisionPixels
			);

			GD.Print(
				"TileMapPhysics: ignored background pixels = " +
				ignoredBackgroundPixels
			);

			GD.Print(
				"TileMapPhysics: ignored transparent pixels = " +
				ignoredTransparentPixels
			);
		}

		return solidPixels;
	}

	// ============================================================
	// Empty background
	// ============================================================

	private bool IsEmptyBackgroundPixel(
		Color pixel)
	{
		float dr =
			pixel.R -
			EmptyColor.R;

		float dg =
			pixel.G -
			EmptyColor.G;

		float db =
			pixel.B -
			EmptyColor.B;

		float distanceSquared =
			dr * dr +
			dg * dg +
			db * db;

		float tolerance =
			Mathf.Max(
				0.0f,
				EmptyColorTolerance
			);

		return
			distanceSquared <=
			tolerance * tolerance;
	}

	// ============================================================
	// Solid rectangle decomposition
	// ============================================================

	private List<SolidRectangle>
		BuildSolidRectangles(
			HashSet<Vector2I> solid)
	{
		List<SolidRectangle> rectangles =
			new List<SolidRectangle>();

		if (
			solid == null ||
			solid.Count == 0)
		{
			return rectangles;
		}

		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;

		foreach (Vector2I p in solid)
		{
			minX = Mathf.Min(minX, p.X);
			minY = Mathf.Min(minY, p.Y);
			maxX = Mathf.Max(maxX, p.X);
			maxY = Mathf.Max(maxY, p.Y);
		}

		int width =
			maxX -
			minX +
			1;

		int height =
			maxY -
			minY +
			1;

		bool[,] occupied =
			new bool[
				width,
				height
			];

		foreach (Vector2I p in solid)
		{
			occupied[
				p.X - minX,
				p.Y - minY
			] = true;
		}

		Dictionary<RunKey, int> active =
			new Dictionary<RunKey, int>();

		for (int y = 0; y < height; y++)
		{
			List<SolidRun> runs =
				BuildHorizontalRuns(
					occupied,
					y,
					width
				);

			HashSet<RunKey> continued =
				new HashSet<RunKey>();

			foreach (SolidRun run in runs)
			{
				RunKey key =
					new RunKey(
						run.X,
						run.Width
					);

				if (
					active.TryGetValue(
						key,
						out int rectangleIndex
					))
				{
					SolidRectangle rectangle =
						rectangles[rectangleIndex];

					rectangle.Height++;

					rectangles[rectangleIndex] =
						rectangle;

					continued.Add(key);
				}
				else
				{
					SolidRectangle rectangle =
						new SolidRectangle(
							minX + run.X,
							minY + y,
							run.Width,
							1
						);

					rectangles.Add(rectangle);

					active[key] =
						rectangles.Count - 1;

					continued.Add(key);
				}
			}

			if (active.Count > 0)
			{
				List<RunKey> expired =
					new List<RunKey>();

				foreach (
					KeyValuePair<RunKey, int> pair
					in active)
				{
					if (!continued.Contains(pair.Key))
					{
						expired.Add(pair.Key);
					}
				}

				foreach (RunKey key in expired)
				{
					active.Remove(key);
				}
			}
		}

		if (MergeSolidRegions)
		{
			rectangles =
				MergeAdjacentRectangles(
					rectangles
				);
		}

		return rectangles;
	}

	// ============================================================
	// Build horizontal runs
	// ============================================================

	private static List<SolidRun>
		BuildHorizontalRuns(
			bool[,] occupied,
			int y,
			int width)
	{
		List<SolidRun> runs =
			new List<SolidRun>();

		int x = 0;

		while (x < width)
		{
			if (!occupied[x, y])
			{
				x++;
				continue;
			}

			int start = x;

			while (
				x < width &&
				occupied[x, y])
			{
				x++;
			}

			runs.Add(
				new SolidRun(
					start,
					x - start
				)
			);
		}

		return runs;
	}

	// ============================================================
	// Merge adjacent rectangles
	// ============================================================

	private static List<SolidRectangle>
		MergeAdjacentRectangles(
			List<SolidRectangle> source)
	{
		if (
			source == null ||
			source.Count <= 1)
		{
			return source;
		}

		List<SolidRectangle> working =
			new List<SolidRectangle>(
				source
			);

		bool changed = true;

		while (changed)
		{
			changed = false;

			// ----------------------------------------------------
			// Horizontal merge
			//
			// Rectangles can merge when:
			//
			//     same Y
			//     same Height
			//     left.X + left.Width == right.X
			//
			// We sort by Y, Height, X first, which makes adjacent
			// rectangles naturally appear next to one another.
			// ----------------------------------------------------

			working.Sort(
				(a, b) =>
				{
					int result =
						a.Y.CompareTo(b.Y);

					if (result != 0)
						return result;

					result =
						a.Height.CompareTo(
							b.Height
						);

					if (result != 0)
						return result;

					return a.X.CompareTo(b.X);
				}
			);

			List<SolidRectangle> horizontal =
				new List<SolidRectangle>(
					working.Count
				);

			for (
				int i = 0;
				i < working.Count;
				i++)
			{
				SolidRectangle current =
					working[i];

				if (horizontal.Count > 0)
				{
					int lastIndex =
						horizontal.Count - 1;

					SolidRectangle previous =
						horizontal[lastIndex];

					if (
						previous.Y == current.Y &&
						previous.Height == current.Height &&
						previous.X + previous.Width == current.X)
					{
						previous.Width +=
							current.Width;

						horizontal[lastIndex] =
							previous;

						changed = true;

						continue;
					}
				}

				horizontal.Add(current);
			}

			working = horizontal;

			// ----------------------------------------------------
			// Vertical merge
			//
			// Rectangles can merge when:
			//
			//     same X
			//     same Width
			//     top.Y + top.Height == bottom.Y
			//
			// Sort by X, Width, Y first.
			// ----------------------------------------------------

			working.Sort(
				(a, b) =>
				{
					int result =
						a.X.CompareTo(b.X);

					if (result != 0)
						return result;

					result =
						a.Width.CompareTo(
							b.Width
						);

					if (result != 0)
						return result;

					return a.Y.CompareTo(b.Y);
				}
			);

			List<SolidRectangle> vertical =
				new List<SolidRectangle>(
					working.Count
				);

			for (
				int i = 0;
				i < working.Count;
				i++)
			{
				SolidRectangle current =
					working[i];

				if (vertical.Count > 0)
				{
					int lastIndex =
						vertical.Count - 1;

					SolidRectangle previous =
						vertical[lastIndex];

					if (
						previous.X == current.X &&
						previous.Width == current.Width &&
						previous.Y + previous.Height == current.Y)
					{
						previous.Height +=
							current.Height;

						vertical[lastIndex] =
							previous;

						changed = true;

						continue;
					}
				}

				vertical.Add(current);
			}

			working = vertical;
		}

		return working;
	}

	// ============================================================
	// Debug boundary
	// ============================================================

	private void BuildDebugBoundary(
		HashSet<Vector2I> solid)
	{
		debugEdges.Clear();

		if (solid == null)
			return;

		foreach (Vector2I p in solid)
		{
			int x = p.X;
			int y = p.Y;

			if (
				!solid.Contains(
					new Vector2I(
						x,
						y - 1
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x,
						y
					),
					new Vector2(
						x + 1,
						y
					)
				);
			}

			if (
				!solid.Contains(
					new Vector2I(
						x + 1,
						y
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x + 1,
						y
					),
					new Vector2(
						x + 1,
						y + 1
					)
				);
			}

			if (
				!solid.Contains(
					new Vector2I(
						x,
						y + 1
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x + 1,
						y + 1
					),
					new Vector2(
						x,
						y + 1
					)
				);
			}

			if (
				!solid.Contains(
					new Vector2I(
						x - 1,
						y
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x,
						y + 1
					),
					new Vector2(
						x,
						y
					)
				);
			}
		}
	}

	private void AddDebugEdge(
		Vector2 a,
		Vector2 b)
	{
		if (!ShowDebugGeometry)
			return;

		debugEdges.Add(
			new DebugEdge(
				ToSimulationSpace(a),
				ToSimulationSpace(b)
			)
		);
	}

	// ============================================================
	// Polygon area
	// ============================================================

	private static float PolygonArea(
		Vector2[] polygon)
	{
		if (
			polygon == null ||
			polygon.Length < 3)
		{
			return 0.0f;
		}

		float area = 0.0f;

		for (
			int i = 0;
			i < polygon.Length;
			i++)
		{
			Vector2 a =
				polygon[i];

			Vector2 b =
				polygon[
					(i + 1) %
					polygon.Length
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area * 0.5f;
	}

	// ============================================================
	// Tile size
	// ============================================================

	private Vector2 GetTileSize()
	{
		if (
			environment != null &&
			environment.TileSet != null)
		{
			Vector2I size =
				environment.TileSet.TileSize;

			if (
				size.X > 0 &&
				size.Y > 0)
			{
				return new Vector2(
					size.X,
					size.Y
				);
			}
		}

		return new Vector2(
			32.0f,
			32.0f
		);
	}

	// ============================================================
	// Environment → main canvas
	// ============================================================

	private Vector2 EnvironmentToGlobal(
		Vector2 tileMapLocal)
	{
		return environment.ToGlobal(
			tileMapLocal
		);
	}

	// ============================================================
	// Main canvas → GameView local
	// ============================================================

	private Vector2 GlobalToGameView(
		Vector2 globalPoint)
	{
		if (gameView == null)
			return globalPoint;

		// Godot 4 C#:
		//
		// Transform2D does not provide TransformPoint().
		//
		// Transforming a Vector2 is done with:
		//
		//     transform * point
		//
		Transform2D inverse =
			gameView
				.GetGlobalTransformWithCanvas()
				.AffineInverse();

		return inverse * globalPoint;
	}

	// ============================================================
	// GameView → SubViewport
	// ============================================================

	private Vector2 GameViewToViewport(
		Vector2 gameViewPoint)
	{
		Vector2 gameSize =
			gameView.Size;

		Vector2 viewportSize =
			new Vector2(
				simulationViewport.Size.X,
				simulationViewport.Size.Y
			);

		if (
			gameSize.X <= 0.001f ||
			gameSize.Y <= 0.001f)
		{
			return gameViewPoint;
		}

		float normalizedX =
			gameViewPoint.X /
			gameSize.X;

		float normalizedY =
			gameViewPoint.Y /
			gameSize.Y;

		return new Vector2(
			normalizedX * viewportSize.X,
			normalizedY * viewportSize.Y
		);
	}

	// ============================================================
	// TileMap → Simulation
	// ============================================================

	/// <summary>
	/// Converts Environment TileMap local coordinates into the
	/// simulation's PBF coordinate system.
	///
	/// This is the critical mapping used by collision generation.
	/// </summary>
	private Vector2 ToSimulationSpace(
		Vector2 tileMapLocal)
	{
		if (!HasValidViewportMapping())
		{
			return tileMapLocal;
		}

		// --------------------------------------------------------
		// 1. Environment local → main canvas/global.
		// --------------------------------------------------------

		Vector2 globalPoint =
			EnvironmentToGlobal(
				tileMapLocal
			);

		// --------------------------------------------------------
		// 2. Main canvas/global → GameView local.
		// --------------------------------------------------------

		Vector2 gameViewPoint =
			GlobalToGameView(
				globalPoint
			);

		// --------------------------------------------------------
		// 3. GameView local → SubViewport coordinates.
		// --------------------------------------------------------

		Vector2 viewportPoint =
			GameViewToViewport(
				gameViewPoint
			);

		// --------------------------------------------------------
		// 4. SubViewport screen coordinates → simulation world.
		// --------------------------------------------------------

		Vector2 viewportSize =
			new Vector2(
				simulationViewport.Size.X,
				simulationViewport.Size.Y
			);

		Vector2 screenCenter =
			viewportSize *
			0.5f;

		Vector2 cameraCenter =
			simulationCamera.GetScreenCenterPosition();

		return
			cameraCenter +
			(
				viewportPoint -
				screenCenter
			);
	}

	// ============================================================
	// Simulation → this node local
	// ============================================================

	private Vector2
		SimulationToThisLocal(
			Vector2 simulationPoint)
	{
		if (!HasValidViewportMapping())
		{
			return ToLocal(simulationPoint);
		}

		Vector2 viewportSize =
			new Vector2(
				simulationViewport.Size.X,
				simulationViewport.Size.Y
			);

		Vector2 screenCenter =
			viewportSize *
			0.5f;

		Vector2 cameraCenter =
			simulationCamera.GetScreenCenterPosition();

		Vector2 viewportPoint =
			screenCenter +
			(
				simulationPoint -
				cameraCenter
			);

		Vector2 gameSize =
			gameView.Size;

		Vector2 gameViewPoint =
			new Vector2(
				viewportSize.X > 0.0f
					? viewportPoint.X *
					  gameSize.X /
					  viewportSize.X
					: viewportPoint.X,

				viewportSize.Y > 0.0f
					? viewportPoint.Y *
					  gameSize.Y /
					  viewportSize.Y
					: viewportPoint.Y
			);

		Transform2D gameTransform =
			gameView.GetGlobalTransformWithCanvas();

		// Godot 4 C# Transform2D → Vector2:
		//
		//     transform * point
		//
		// NOT TransformPoint().
		Vector2 globalPoint =
			gameTransform *
			gameViewPoint;

		return ToLocal(
			globalPoint
		);
	}

	// ============================================================
	// Find FluidSimulator
	// ============================================================

	private static FluidSimulator
		FindFluidSimulator(
			Node node)
	{
		if (node is FluidSimulator)
		{
			return (FluidSimulator)node;
		}

		foreach (Node child in node.GetChildren())
		{
			FluidSimulator result =
				FindFluidSimulator(child);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Rebuild
	// ============================================================

	public void Rebuild()
	{
		if (generating)
			return;

		GenerateColliders();
	}

	// ============================================================
	// Information
	// ============================================================

	public int GeneratedColliderCount
	{
		get
		{
			return generatedColliders.Count;
		}
	}

	public bool IsGenerated
	{
		get
		{
			return generated;
		}
	}

	// ============================================================
	// Exit
	// ============================================================

	public override void _ExitTree()
	{
		generated = false;
		generating = false;

		generatedColliders.Clear();
		debugEdges.Clear();

		environment = null;
		simulator = null;
		solver = null;
		gameView = null;
		simulationViewport = null;
		simulationCamera = null;
	}
}
