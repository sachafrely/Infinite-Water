
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Generates PBF collision geometry directly from a TileMapLayer.
///
/// Current project setup:
/// - Environment is a TileMapLayer.
/// - Environment is scaled 4x.
/// - Tiles are 16x16.
/// - PBF simulation operates in the same coordinate space as
///   FluidSimulator.
/// 
/// Every occupied tile is considered SOLID.
///
/// Only exposed tile edges are converted into collision polygons.
/// Internal edges between neighboring solid tiles are ignored.
///
/// This means the physical collision follows the actual tiles
/// instead of a separately-maintained RiverLine2D.
///
/// IMPORTANT:
/// The TileMapLayer itself remains purely visual.
/// This class only reads its occupied cells.
/// </summary>
[Tool]
public partial class TileMapPhysics : Node2D
{
	// ============================================================
	// Configuration
	// ============================================================

	/// <summary>
	/// Path to the TileMapLayer containing the environment.
	///
	/// Leave this as "Environment" for the current Main scene.
	/// </summary>
	[Export]
	public NodePath EnvironmentPath { get; set; } =
		new NodePath("../Environment");

	/// <summary>
	/// Thickness of the generated collision geometry.
	///
	/// PBF particles already have their own collision radius, so
	/// this should normally remain fairly small.
	/// </summary>
	[Export]
	public float CollisionThickness { get; set; } = 4.0f;

	/// <summary>
	/// Automatically generate the collision when the game starts.
	/// </summary>
	[Export]
	public bool GenerateOnReady { get; set; } = true;

	/// <summary>
	/// Rebuild the collision if the TileMap changes.
	///
	/// Leave false for best performance because your environment
	/// is currently static.
	/// </summary>
	[Export]
	public bool RebuildWhenChanged { get; set; } = false;

	/// <summary>
	/// Print information about generated collision geometry.
	/// Useful while setting this system up.
	/// </summary>
	[Export]
	public bool DebugOutput { get; set; } = true;

	/// <summary>
	/// Draw generated collision edges visually.
	///
	/// This is only a debug visualization and does not affect
	/// the PBF simulation.
	/// </summary>
	[Export]
	public bool ShowDebugGeometry { get; set; } = false;

	/// <summary>
	/// Color used for debug geometry.
	/// </summary>
	[Export]
	public Color DebugColor { get; set; } =
		new Color(
			1.0f,
			0.2f,
			0.1f,
			0.8f
		);

	// ============================================================
	// Tile information
	// ============================================================

	/// <summary>
	/// Your current tiles are 16x16.
	///
	/// We normally obtain the real TileSet tile size automatically,
	/// but this value is retained as a fallback.
	/// </summary>
	private static readonly Vector2 DefaultTileSize =
		new Vector2(
			16.0f,
			16.0f
		);

	// ============================================================
	// Runtime state
	// ============================================================

	private TileMapLayer environment;

	private FluidSimulator simulator;

	private PbfSolver solver;

	private readonly List<FluidPolygonCollider>
		generatedColliders =
			new List<FluidPolygonCollider>();

	private readonly List<DebugEdge>
		debugEdges =
			new List<DebugEdge>();

	private bool generated;

	private bool generating;

	// ============================================================
	// Edge structure
	// ============================================================

	private struct TileEdge : IEquatable<TileEdge>
	{
		public Vector2I Start;
		public Vector2I End;

		public TileEdge(
			Vector2I start,
			Vector2I end)
		{
			Start = start;
			End = end;
		}

		public bool Equals(
			TileEdge other)
		{
			return Start == other.Start &&
				End == other.End;
		}

		public override bool Equals(
			object obj)
		{
			return obj is TileEdge &&
				Equals(
					(TileEdge)obj
				);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				Start,
				End
			);
		}
	}

	// ============================================================
	// Debug edge
	// ============================================================

	private struct DebugEdge
	{
		public Vector2 A;
		public Vector2 B;

		public DebugEdge(
			Vector2 a,
			Vector2 b)
		{
			A = a;
			B = b;
		}
	}

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		CallDeferred(
			nameof(Initialize)
		);
	}

	// ============================================================
	// Initialization
	// ============================================================

	private void Initialize()
	{
		if (generating)
		{
			return;
		}

		generating = true;

		// --------------------------------------------------------
		// Find Environment.
		// --------------------------------------------------------

		environment =
			GetEnvironment();

		if (environment == null)
		{
			GD.PushError(
				"TileMapPhysics could not find " +
				"the Environment TileMapLayer."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Find FluidSimulator.
		// --------------------------------------------------------

		simulator =
			FindFluidSimulator(
				GetTree().Root
			);

		if (simulator == null)
		{
			GD.PushError(
				"TileMapPhysics could not find " +
				"FluidSimulator."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Get PBF solver.
		// --------------------------------------------------------

		solver =
			GetSolver(
				simulator
			);

		if (solver == null)
		{
			GD.PushWarning(
				"TileMapPhysics found the FluidSimulator " +
				"but its PbfSolver is not initialized yet."
			);

			generating = false;

			CallDeferred(
				nameof(Initialize)
			);

			return;
		}

		// --------------------------------------------------------
		// Generate.
		// --------------------------------------------------------

		if (GenerateOnReady)
		{
			GenerateColliders();
		}

		generating = false;
	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(
		double delta)
	{
		if (
			ShowDebugGeometry &&
			Engine.IsEditorHint())
		{
			QueueRedraw();
		}
	}

	// ============================================================
	// Get environment
	// ============================================================

	private TileMapLayer GetEnvironment()
	{
		// --------------------------------------------------------
		// First try the configured path.
		// --------------------------------------------------------

		if (
			EnvironmentPath != null &&
			!EnvironmentPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					EnvironmentPath
				);

			if (node is TileMapLayer layer)
			{
				return layer;
			}
		}

		// --------------------------------------------------------
		// Then search the scene.
		// --------------------------------------------------------

		return FindTileMapLayer(
			GetTree().Root
		);
	}

	// ============================================================
	// Find TileMapLayer
	// ============================================================

	private static TileMapLayer FindTileMapLayer(
		Node node)
	{
		if (node is TileMapLayer layer)
		{
			if (layer.Name == "Environment")
			{
				return layer;
			}
		}

		foreach (
			Node child in node.GetChildren())
		{
			TileMapLayer result =
				FindTileMapLayer(
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
	// Get PBF solver
	// ============================================================

	private static PbfSolver GetSolver(
		FluidSimulator fluidSimulator)
	{
		FieldInfo solverField =
			typeof(FluidSimulator).GetField(
				"solver",
				BindingFlags.Instance |
				BindingFlags.NonPublic
			);

		if (solverField == null)
		{
			GD.PushError(
				"TileMapPhysics could not access " +
				"FluidSimulator.solver."
			);

			return null;
		}

		return solverField.GetValue(
			fluidSimulator
		) as PbfSolver;
	}

	// ============================================================
	// Generate collision
	// ============================================================

	public void GenerateColliders()
	{
		if (generating)
		{
			return;
		}

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

		if (solver == null && simulator != null)
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
				"TileMapPhysics cannot generate " +
				"colliders because the required " +
				"nodes are not ready."
			);

			return;
		}

		generating = true;

		// --------------------------------------------------------
		// Remove our previous colliders.
		//
		// IMPORTANT:
		// We cannot call solver.ClearPolygonColliders()
		// because that would also remove the wheel collider.
		//
		// Therefore this class is intended to be the owner of
		// the environment colliders and should be generated once.
		// --------------------------------------------------------

		generatedColliders.Clear();
		debugEdges.Clear();

		// --------------------------------------------------------
		// Read occupied cells.
		// --------------------------------------------------------

		Dictionary<Vector2I, bool> occupied =
			BuildOccupiedCellSet();

		if (occupied.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics found no occupied " +
				"cells in the Environment TileMapLayer."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Find exposed edges.
		// --------------------------------------------------------

		HashSet<TileEdge> exposedEdges =
			BuildExposedEdges(
				occupied
			);

		// --------------------------------------------------------
		// Merge adjacent collinear edges.
		//
		// This is important for performance.
		//
		// Instead of:
		//
		// 100 tiles = 100 colliders
		//
		// a long straight river bank becomes:
		//
		// 1 collider
		// --------------------------------------------------------

		List<TileEdge> mergedEdges =
			MergeEdges(
				exposedEdges
			);

		// --------------------------------------------------------
		// Convert every edge into a small convex polygon.
		// --------------------------------------------------------

		for (
			int i = 0;
			i < mergedEdges.Count;
			i++)
		{
			TileEdge edge =
				mergedEdges[i];

			Vector2 localA =
				CellCornerToTileMapLocal(
					edge.Start
				);

			Vector2 localB =
				CellCornerToTileMapLocal(
					edge.End
				);

			Vector2 simulatorA =
				ToSimulatorSpace(
					localA
				);

			Vector2 simulatorB =
				ToSimulatorSpace(
					localB
				);

			if (
				(simulatorB - simulatorA)
				.LengthSquared() <
				0.0001f)
			{
				continue;
			}

			Vector2[] polygon =
				BuildSegmentPolygon(
					simulatorA,
					simulatorB,
					CollisionThickness
				);

			if (polygon.Length < 3)
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

			generatedColliders.Add(
				collider
			);

			debugEdges.Add(
				new DebugEdge(
					simulatorA,
					simulatorB
				)
			);
		}

		generated = true;

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics"
			);

			GD.Print(
				$"Occupied tiles: {occupied.Count}"
			);

			GD.Print(
				$"Exposed edges: {exposedEdges.Count}"
			);

			GD.Print(
				$"Merged edges: {mergedEdges.Count}"
			);

			GD.Print(
				$"PBF colliders: " +
				$"{generatedColliders.Count}"
			);

			GD.Print(
				$"TileMap scale: " +
				$"{environment.GlobalScale}"
			);

			GD.Print(
				$"Tile size: " +
				$"{GetTileSize()}"
			);

			GD.Print(
				"========================================"
			);
		}

		generating = false;

		QueueRedraw();
	}

	// ============================================================
	// Build occupied cell set
	// ============================================================

	private Dictionary<Vector2I, bool>
		BuildOccupiedCellSet()
	{
		Dictionary<Vector2I, bool>
			occupied =
				new Dictionary<Vector2I, bool>();

		// --------------------------------------------------------
		// Godot returns all cells containing tiles.
		// --------------------------------------------------------

		Godot.Collections.Array<Vector2I>
			cells =
				environment.GetUsedCells();

		foreach (
			Vector2I cell in cells)
		{
			// ----------------------------------------------------
			// Verify that a tile actually exists.
			// ----------------------------------------------------

			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (sourceId < 0)
			{
				continue;
			}

			occupied[cell] = true;
		}

		return occupied;
	}

	// ============================================================
	// Build exposed edges
	// ============================================================

	private HashSet<TileEdge>
		BuildExposedEdges(
			Dictionary<Vector2I, bool> occupied)
	{
		HashSet<TileEdge> edges =
			new HashSet<TileEdge>();

		foreach (
			KeyValuePair<Vector2I, bool> pair
			in occupied)
		{
			Vector2I cell =
				pair.Key;

			// ----------------------------------------------------
			// Top
			//
			// Clockwise perimeter:
			//
			// top-left ---- top-right
			//    |              |
			//    |    TILE      |
			//    |              |
			// bottom-left -- bottom-right
			// ----------------------------------------------------

			Vector2I topLeft =
				new Vector2I(
					cell.X,
					cell.Y
				);

			Vector2I topRight =
				new Vector2I(
					cell.X + 1,
					cell.Y
				);

			Vector2I bottomRight =
				new Vector2I(
					cell.X + 1,
					cell.Y + 1
				);

			Vector2I bottomLeft =
				new Vector2I(
					cell.X,
					cell.Y + 1
				);

			// ----------------------------------------------------
			// Add only if neighboring tile does not exist.
			// ----------------------------------------------------

			if (
				!occupied.ContainsKey(
					new Vector2I(
						cell.X,
						cell.Y - 1
					)
				))
			{
				AddEdge(
					edges,
					topLeft,
					topRight
				);
			}

			// ----------------------------------------------------
			// Right
			// ----------------------------------------------------

			if (
				!occupied.ContainsKey(
					new Vector2I(
						cell.X + 1,
						cell.Y
					)
				))
			{
				AddEdge(
					edges,
					topRight,
					bottomRight
				);
			}

			// ----------------------------------------------------
			// Bottom
			// ----------------------------------------------------

			if (
				!occupied.ContainsKey(
					new Vector2I(
						cell.X,
						cell.Y + 1
					)
				))
			{
				AddEdge(
					edges,
					bottomRight,
					bottomLeft
				);
			}

			// ----------------------------------------------------
			// Left
			// ----------------------------------------------------

			if (
				!occupied.ContainsKey(
					new Vector2I(
						cell.X - 1,
						cell.Y
					)
				))
			{
				AddEdge(
					edges,
					bottomLeft,
					topLeft
				);
			}
		}

		return edges;
	}

	// ============================================================
	// Add edge
	// ============================================================

	private static void AddEdge(
		HashSet<TileEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		// --------------------------------------------------------
		// Normalize orientation for duplicate detection.
		//
		// This is useful if the tile layout contains unusual
		// overlapping cells.
		// --------------------------------------------------------

		TileEdge edge =
			new TileEdge(
				a,
				b
			);

		TileEdge reverse =
			new TileEdge(
				b,
				a
			);

		if (edges.Contains(reverse))
		{
			edges.Remove(reverse);

			return;
		}

		edges.Add(edge);
	}

	// ============================================================
	// Merge edges
	// ============================================================

	private List<TileEdge> MergeEdges(
		HashSet<TileEdge> input)
	{
		List<TileEdge> horizontal =
			new List<TileEdge>();

		List<TileEdge> vertical =
			new List<TileEdge>();

		// --------------------------------------------------------
		// Separate horizontal / vertical edges.
		// --------------------------------------------------------

		foreach (
			TileEdge edge in input)
		{
			if (
				edge.Start.Y ==
				edge.End.Y)
			{
				horizontal.Add(
					NormalizeHorizontal(
						edge
					)
				);
			}
			else
			{
				vertical.Add(
					NormalizeVertical(
						edge
					)
				);
			}
		}

		List<TileEdge> result =
			new List<TileEdge>();

		// --------------------------------------------------------
		// Merge each direction.
		// --------------------------------------------------------

		result.AddRange(
			MergeHorizontalEdges(
				horizontal
			)
		);

		result.AddRange(
			MergeVerticalEdges(
				vertical
			)
		);

		return result;
	}

	// ============================================================
	// Normalize horizontal
	// ============================================================

	private static TileEdge NormalizeHorizontal(
		TileEdge edge)
	{
		if (
			edge.Start.X <=
			edge.End.X)
		{
			return edge;
		}

		return new TileEdge(
			edge.End,
			edge.Start
		);
	}

	// ============================================================
	// Normalize vertical
	// ============================================================

	private static TileEdge NormalizeVertical(
		TileEdge edge)
	{
		if (
			edge.Start.Y <=
			edge.End.Y)
		{
			return edge;
		}

		return new TileEdge(
			edge.End,
			edge.Start
		);
	}

	// ============================================================
	// Merge horizontal
	// ============================================================

	private static List<TileEdge>
		MergeHorizontalEdges(
			List<TileEdge> edges)
	{
		List<TileEdge> result =
			new List<TileEdge>();

		// --------------------------------------------------------
		// Sort by Y, then X.
		// --------------------------------------------------------

		edges.Sort(
			(a, b) =>
			{
				int y =
					a.Start.Y.CompareTo(
						b.Start.Y
					);

				if (y != 0)
				{
					return y;
				}

				return a.Start.X.CompareTo(
					b.Start.X
				);
			}
		);

		foreach (
			TileEdge edge in edges)
		{
			if (result.Count == 0)
			{
				result.Add(
					edge
				);

				continue;
			}

			int lastIndex =
				result.Count - 1;

			TileEdge last =
				result[lastIndex];

			// ----------------------------------------------------
			// Same row and touching.
			// ----------------------------------------------------

			if (
				last.Start.Y ==
					edge.Start.Y &&
				last.End.X ==
					edge.Start.X)
			{
				result[lastIndex] =
					new TileEdge(
						last.Start,
						edge.End
					);
			}
			else
			{
				result.Add(
					edge
				);
			}
		}

		return result;
	}

	// ============================================================
	// Merge vertical
	// ============================================================

	private static List<TileEdge>
		MergeVerticalEdges(
			List<TileEdge> edges)
	{
		List<TileEdge> result =
			new List<TileEdge>();

		// --------------------------------------------------------
		// Sort by X, then Y.
		// --------------------------------------------------------

		edges.Sort(
			(a, b) =>
			{
				int x =
					a.Start.X.CompareTo(
						b.Start.X
					);

				if (x != 0)
				{
					return x;
				}

				return a.Start.Y.CompareTo(
					b.Start.Y
				);
			}
		);

		foreach (
			TileEdge edge in edges)
		{
			if (result.Count == 0)
			{
				result.Add(
					edge
				);

				continue;
			}

			int lastIndex =
				result.Count - 1;

			TileEdge last =
				result[lastIndex];

			// ----------------------------------------------------
			// Same column and touching.
			// ----------------------------------------------------

			if (
				last.Start.X ==
					edge.Start.X &&
				last.End.Y ==
					edge.Start.Y)
			{
				result[lastIndex] =
					new TileEdge(
						last.Start,
						edge.End
					);
			}
			else
			{
				result.Add(
					edge
				);
			}
		}

		return result;
	}

	// ============================================================
	// Cell corner -> TileMap local
	// ============================================================

	private Vector2 CellCornerToTileMapLocal(
		Vector2I corner)
	{
		Vector2 tileSize =
			GetTileSize();

		// --------------------------------------------------------
		// TileMapLayer's local grid.
		//
		// For a 16x16 tile:
		//
		// cell (0,0) corner = (0,0)
		// cell (1,0) corner = (16,0)
		// --------------------------------------------------------

		Vector2 local =
			new Vector2(
				corner.X *
				tileSize.X,

				corner.Y *
				tileSize.Y
			);

		return local;
	}

	// ============================================================
	// Get TileSet tile size
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

		return DefaultTileSize;
	}

	// ============================================================
	// Convert TileMap local -> PBF coordinates
	// ============================================================

	private Vector2 ToSimulatorSpace(
		Vector2 tileMapLocal)
	{
		// --------------------------------------------------------
		// TileMap local
		//       ↓
		// Global scene
		//       ↓
		// FluidSimulator local
		//
		// This automatically respects:
		//
		// Environment.scale = Vector2(4,4)
		//
		// and any future position/scale changes.
		// --------------------------------------------------------

		Vector2 globalPoint =
			environment.ToGlobal(
				tileMapLocal
			);

		return simulator.ToLocal(
			globalPoint
		);
	}

	// ============================================================
	// Build segment polygon
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
				0.5f,
				thickness
			) *
			0.5f;

		Vector2 offset =
			normal *
			halfThickness;

		// --------------------------------------------------------
		// Counter-clockwise rectangle.
		// --------------------------------------------------------

		return new[]
		{
			a - offset,
			a + offset,
			b + offset,
			b - offset
		};
	}

	// ============================================================
	// Find FluidSimulator
	// ============================================================

	private static FluidSimulator
		FindFluidSimulator(
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
	// Public rebuild
	// ============================================================

	public void Rebuild()
	{
		if (generating)
		{
			return;
		}

		GenerateColliders();
	}

	// ============================================================
	// Public information
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
		generated =
			false;

		generatedColliders.Clear();

		debugEdges.Clear();

		environment =
			null;

		simulator =
			null;

		solver =
			null;
	}
}
