using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Generates PBF collision geometry from the visual Environment TileMapLayer.
///
/// Design goals:
///
/// 1. Use the actual visual alpha/color mask.
/// 2. Produce watertight boundary geometry.
/// 3. Preserve meaningful terrain angles.
/// 4. Merge long straight runs into single segments.
/// 5. Keep every collider convex.
/// 6. Use overlapping/thickened segment colliders to prevent
///    high velocity particles from tunneling through corners.
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

	/// <summary>
	/// Physical thickness of the collision wall.
	///
	/// 16 px is intentional. The visible debug line is still
	/// only 2 px wide; this controls the actual PBF collision.
	/// </summary>
	[Export]
	public float CollisionThickness { get; set; } = 16.0f;

	/// <summary>
	/// Extends every segment beyond its endpoints.
	///
	/// This makes neighboring segment colliders overlap at
	/// corners and removes tiny gaps that high velocity particles
	/// could otherwise cross.
	/// </summary>
	[Export]
	public float CollisionEndExtension { get; set; } = 8.0f;

	[Export]
	public bool GenerateOnReady { get; set; } = true;

	[Export]
	public bool RebuildWhenChanged { get; set; } = false;

	[Export]
	public bool DebugOutput { get; set; } = true;

	[Export]
	public bool ShowDebugGeometry { get; set; } = true;

	[Export]
	public Color DebugColor { get; set; } =
		new Color(
			1.0f,
			0.2f,
			0.1f,
			0.9f
		);

	[Export]
	public NodePath GameViewPath { get; set; } =
		new NodePath("../GameView");

	[Export]
	public NodePath SimulationViewportPath { get; set; } =
		new NodePath(
			"../GameView/SimulationViewport"
		);

	[Export]
	public NodePath CameraPath { get; set; } =
		new NodePath(
			"../GameView/SimulationViewport/Camera2D"
		);

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
	// Boundary processing
	// ============================================================

	/// <summary>
	/// Small dilation closes single-pixel holes and microscopic
	/// discontinuities in the source artwork.
	/// </summary>
	[Export]
	public int CollisionSealPixels { get; set; } = 1;

	/// <summary>
	/// Distance used for removing redundant points.
	///
	/// IMPORTANT:
	/// The simplifier below never removes actual direction
	/// changes. Therefore this can be larger than 1 without
	/// destroying terrain corners.
	/// </summary>
	[Export]
	public float ContourSimplification { get; set; } = 2.0f;

	/// <summary>
	/// Minimum useful contour area.
	/// </summary>
	[Export]
	public float MinimumContourArea { get; set; } = 4.0f;

	/// <summary>
	/// Maximum number of generated segment colliders.
	///
	/// This is a safety limit, not the normal target.
	/// </summary>
	[Export]
	public int MaximumContourSegments { get; set; } = 2500;

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

	private readonly List<FluidPolygonCollider>
		generatedColliders =
			new List<FluidPolygonCollider>();

	private readonly List<DebugEdge>
		debugEdges =
			new List<DebugEdge>();

	private bool generated;
	private bool generating;

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
	// Grid edge
	// ============================================================

	private struct GridEdge : IEquatable<GridEdge>
	{
		public Vector2I A;
		public Vector2I B;

		public GridEdge(
			Vector2I a,
			Vector2I b)
		{
			A = a;
			B = b;
		}

		public bool Equals(
			GridEdge other)
		{
			return
				A == other.A &&
				B == other.B;
		}

		public override bool Equals(
			object obj)
		{
			return
				obj is GridEdge &&
				Equals(
					(GridEdge)obj
				);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				A,
				B
			);
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

		environment =
			GetEnvironment();

		if (environment == null)
		{
			GD.PushError(
				"TileMapPhysics: Environment TileMapLayer " +
				"could not be found."
			);

			return;
		}

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
			GD.PushWarning(
				"TileMapPhysics: PbfSolver is not ready yet."
			);

			CallDeferred(
				nameof(Initialize)
			);

			return;
		}

		FindViewportMapping();

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
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

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			return;
		}

		foreach (
			DebugEdge edge in debugEdges)
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
				2.0f,
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
	// Viewport mapping
	// ============================================================

	private void FindViewportMapping()
	{
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
			foreach (
				Node child in gameView.GetChildren())
			{
				if (child is SubViewport)
				{
					simulationViewport =
						(SubViewport)child;

					break;
				}
			}
		}

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

			GD.Print(
				"  SimulationViewport: " +
				(
					simulationViewport != null
						? simulationViewport.GetPath().ToString()
						: "NULL"
				)
			);

			GD.Print(
				"  Camera: " +
				(
					simulationCamera != null
						? simulationCamera.GetPath().ToString()
						: "NULL"
				)
			);
		}
	}

	private static T FindNodeOfType<T>(
		Node node)
		where T : Node
	{
		if (node is T)
		{
			return (T)node;
		}

		foreach (
			Node child in node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(
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
	// Solver
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
				"TileMapPhysics: Could not access " +
				"FluidSimulator.solver."
			);

			return null;
		}

		return solverField.GetValue(
			fluidSimulator
		) as PbfSolver;
	}

	// ============================================================
	// Generate colliders
	// ============================================================

	public void GenerateColliders()
	{
		GD.Print(
			"========== GENERATE COLLIDERS CALLED =========="
		);

		if (generating)
			return;

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

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			GD.PushError(
				"TileMapPhysics: Viewport mapping is invalid."
			);

			return;
		}

		generating = true;
		generated = false;

		solver.ClearPolygonColliders();

		generatedColliders.Clear();
		debugEdges.Clear();

		GD.Print(
			"TileMapPhysics: Generating collision..."
		);

		// --------------------------------------------------------
		// Build solid pixel mask.
		// --------------------------------------------------------

		HashSet<Vector2I> solidPixels =
			BuildGlobalSolidMask();

		if (solidPixels.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No collision pixels found."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Seal microscopic holes.
		// --------------------------------------------------------

		if (CollisionSealPixels > 0)
		{
			solidPixels =
				DilateMask(
					solidPixels,
					CollisionSealPixels
				);
		}

		// --------------------------------------------------------
		// Extract complete closed contours.
		// --------------------------------------------------------

		List<List<Vector2I>> loops =
			ExtractBoundaryLoops(
				solidPixels
			);

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics: boundary loops = " +
				loops.Count
			);

			for (
				int i = 0;
				i < loops.Count;
				i++)
			{
				GD.Print(
					"  Loop " +
					i +
					": " +
					loops[i].Count +
					" points"
				);
			}
		}

		if (loops.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No boundary loops found."
			);

			generating = false;

			return;
		}

		int totalSegments = 0;
		int rejectedContours = 0;

		// --------------------------------------------------------
		// Process every contour.
		// --------------------------------------------------------

		foreach (
			List<Vector2I> loop in loops)
		{
			if (loop.Count < 3)
			{
				rejectedContours++;
				continue;
			}

			List<Vector2> contour =
				new List<Vector2>(
					loop.Count
				);

			foreach (
				Vector2I point in loop)
			{
				contour.Add(
					GridToTileMapLocal(
						point
					)
				);
			}

			contour =
				RemoveDuplicatePoints(
					contour
				);

			if (contour.Count < 3)
			{
				rejectedContours++;
				continue;
			}

			// ----------------------------------------------------
			// First remove ONLY redundant collinear points.
			//
			// This is the important difference from the previous
			// simplifier: actual corners are never removed here.
			// ----------------------------------------------------

			contour =
				RemoveCollinearPoints(
					contour
				);

			// ----------------------------------------------------
			// Second pass:
			//
			// Remove only tiny points that are genuinely
			// redundant AND do not represent a meaningful turn.
			// ----------------------------------------------------

			if (ContourSimplification > 0.0f)
			{
				contour =
					SimplifyContourPreservingCorners(
						contour,
						ContourSimplification
					);
			}

			// ----------------------------------------------------
			// Run collinear cleanup again.
			// ----------------------------------------------------

			contour =
				RemoveCollinearPoints(
					contour
				);

			if (contour.Count < 3)
			{
				rejectedContours++;
				continue;
			}

			float area =
				PolygonArea(
					contour
				);

			if (
				Mathf.Abs(area) <
				MinimumContourArea)
			{
				if (DebugOutput)
				{
					GD.Print(
						"TileMapPhysics: contour rejected " +
						"because area is too small: " +
						area
					);
				}

				rejectedContours++;
				continue;
			}

			// ----------------------------------------------------
			// Normalize clockwise.
			// ----------------------------------------------------

			if (area > 0.0f)
			{
				contour.Reverse();
			}

			int before =
				totalSegments;

			// ----------------------------------------------------
			// Convert every meaningful boundary edge into one
			// convex thick collider.
			// ----------------------------------------------------

			for (
				int i = 0;
				i < contour.Count;
				i++)
			{
				if (
					totalSegments >=
					MaximumContourSegments)
				{
					GD.PushWarning(
						"TileMapPhysics: MaximumContourSegments " +
						"reached."
					);

					break;
				}

				Vector2 localA =
					contour[i];

				Vector2 localB =
					contour[
						(i + 1) %
						contour.Count
					];

				Vector2 simulationA =
					ToSimulationSpace(
						localA
					);

				Vector2 simulationB =
					ToSimulationSpace(
						localB
					);

				Vector2 difference =
					simulationB -
					simulationA;

				float length =
					difference.Length();

				if (length < 0.01f)
				{
					continue;
				}

				// ------------------------------------------------
				// The key anti-leak improvement:
				//
				// extend the segment at BOTH ends.
				//
				// Adjacent segments therefore overlap around
				// every corner.
				// ------------------------------------------------

				Vector2 direction =
					difference /
					length;

				float extension =
					Mathf.Max(
						0.0f,
						CollisionEndExtension
					);

				Vector2 extendedA =
					simulationA -
					direction *
					extension;

				Vector2 extendedB =
					simulationB +
					direction *
					extension;

				Vector2[] polygon =
					BuildSegmentPolygon(
						extendedA,
						extendedB,
						Mathf.Max(
							CollisionThickness,
							12.0f
						)
					);

				if (
					polygon == null ||
					polygon.Length < 4)
				{
					continue;
				}

				float polygonArea =
					PolygonArea(
						polygon
					);

				if (
					Mathf.Abs(
						polygonArea
					) < 0.001f)
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

				totalSegments++;

				if (ShowDebugGeometry)
				{
					// Draw the actual terrain line, not the
					// extended collision strip.
					debugEdges.Add(
						new DebugEdge(
							simulationA,
							simulationB
						)
					);
				}
			}

			if (DebugOutput)
			{
				GD.Print(
					"  Contour: " +
					contour.Count +
					" points -> " +
					(totalSegments - before) +
					" colliders"
				);
			}
		}

		generated =
			generatedColliders.Count > 0;

		generating = false;

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics COLLISION RESULT"
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
				"Boundary loops: " +
				loops.Count
			);

			GD.Print(
				"Rejected contours: " +
				rejectedContours
			);

			GD.Print(
				"Generated collision segments: " +
				totalSegments
			);

			GD.Print(
				"PBF colliders: " +
				generatedColliders.Count
			);

			GD.Print(
				"Collision thickness: " +
				Mathf.Max(
					CollisionThickness,
					12.0f
				)
			);

			GD.Print(
				"Collision end extension: " +
				CollisionEndExtension
			);

			GD.Print(
				"Contour simplification: " +
				ContourSimplification
			);

			GD.Print(
				"========================================"
			);
		}

		QueueRedraw();
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

		foreach (
			Vector2I cell in cells)
		{
			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (sourceId < 0)
				continue;

			Vector2I atlasCoords =
				environment.GetCellAtlasCoords(
					cell
				);

			if (
				atlasCoords.X < 0 ||
				atlasCoords.Y < 0)
			{
				continue;
			}

			TileSetSource source =
				tileSet.GetSource(
					sourceId
				);

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

				imageCache[sourceId] =
					image;
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
				Mathf.Max(
					1,
					width
				);

			float scaleY =
				tileSize.Y /
				Mathf.Max(
					1,
					height
				);

			Vector2 cellCenter =
				environment.MapToLocal(
					cell
				);

			Vector2 tileTopLeft =
				cellCenter -
				tileSize *
				0.5f;

			for (
				int y = 0;
				y < height;
				y++)
			{
				for (
					int x = 0;
					x < width;
					x++)
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
						IsEmptyBackgroundPixel(
							pixel
						))
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
	// Dilate mask
	// ============================================================

	private static HashSet<Vector2I>
		DilateMask(
			HashSet<Vector2I> source,
			int radius)
	{
		if (
			radius <= 0 ||
			source.Count == 0)
		{
			return source;
		}

		HashSet<Vector2I> result =
			new HashSet<Vector2I>(
				source
			);

		foreach (
			Vector2I p in source)
		{
			for (
				int y = -radius;
				y <= radius;
				y++)
			{
				for (
					int x = -radius;
					x <= radius;
					x++)
				{
					result.Add(
						new Vector2I(
							p.X + x,
							p.Y + y
						)
					);
				}
			}
		}

		return result;
	}

	// ============================================================
	// Extract boundary loops
	// ============================================================

	private List<List<Vector2I>>
		ExtractBoundaryLoops(
			HashSet<Vector2I> solid)
	{
		HashSet<GridEdge> edges =
			new HashSet<GridEdge>();

		foreach (
			Vector2I p in solid)
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
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x,
						y
					),
					new Vector2I(
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
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x + 1,
						y
					),
					new Vector2I(
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
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x + 1,
						y + 1
					),
					new Vector2I(
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
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x,
						y + 1
					),
					new Vector2I(
						x,
						y
					)
				);
			}
		}

		Dictionary<Vector2I, List<Vector2I>>
			nextMap =
				new Dictionary<
					Vector2I,
					List<Vector2I>
				>();

		foreach (
			GridEdge edge in edges)
		{
			List<Vector2I> list;

			if (
				!nextMap.TryGetValue(
					edge.A,
					out list
				))
			{
				list =
					new List<Vector2I>();

				nextMap[edge.A] =
					list;
			}

			list.Add(
				edge.B
			);
		}

		// Deterministic ordering.
		foreach (
			KeyValuePair<
				Vector2I,
				List<Vector2I>
			> pair in nextMap)
		{
			Vector2I origin =
				pair.Key;

			pair.Value.Sort(
				(a, b) =>
				{
					Vector2 da =
						new Vector2(
							a.X - origin.X,
							a.Y - origin.Y
						);

					Vector2 db =
						new Vector2(
							b.X - origin.X,
							b.Y - origin.Y
						);

					float aa =
						Mathf.Atan2(
							da.Y,
							da.X
						);

					float ab =
						Mathf.Atan2(
							db.Y,
							db.X
						);

					return aa.CompareTo(ab);
				}
			);
		}

		HashSet<GridEdge> remaining =
			new HashSet<GridEdge>(
				edges
			);

		List<List<Vector2I>> loops =
			new List<List<Vector2I>>();

		while (remaining.Count > 0)
		{
			GridEdge first =
				FindDeterministicFirstEdge(
					remaining
				);

			List<Vector2I> loop =
				TraceSingleBoundary(
					first,
					remaining,
					nextMap
				);

			if (
				loop != null &&
				loop.Count >= 3)
			{
				loops.Add(
					loop
				);
			}
		}

		return loops;
	}

	// ============================================================
	// Deterministic first edge
	// ============================================================

	private static GridEdge
		FindDeterministicFirstEdge(
			HashSet<GridEdge> edges)
	{
		GridEdge best =
			default;

		bool haveBest =
			false;

		foreach (
			GridEdge edge in edges)
		{
			if (!haveBest)
			{
				best = edge;
				haveBest = true;
				continue;
			}

			if (
				edge.A.Y < best.A.Y ||
				(
					edge.A.Y == best.A.Y &&
					edge.A.X < best.A.X
				) ||
				(
					edge.A == best.A &&
					edge.B.Y < best.B.Y
				) ||
				(
					edge.A == best.A &&
					edge.B.Y == best.B.Y &&
					edge.B.X < best.B.X
				))
			{
				best = edge;
			}
		}

		return best;
	}

	// ============================================================
	// Trace boundary
	// ============================================================

	private static List<Vector2I>
		TraceSingleBoundary(
			GridEdge first,
			HashSet<GridEdge> remaining,
			Dictionary<
				Vector2I,
				List<Vector2I>
			> nextMap)
	{
		List<Vector2I> loop =
			new List<Vector2I>();

		Vector2I start =
			first.A;

		Vector2I previous =
			first.A;

		Vector2I current =
			first.B;

		RemoveEdge(
			remaining,
			first.A,
			first.B
		);

		loop.Add(
			start
		);

		int safety =
			Mathf.Max(
				1000,
				remaining.Count * 2 + 100
			);

		for (
			int iteration = 0;
			iteration < safety;
			iteration++)
		{
			if (current == start)
			{
				return loop;
			}

			loop.Add(
				current
			);

			List<Vector2I> candidates;

			if (
				!nextMap.TryGetValue(
					current,
					out candidates
				))
			{
				return null;
			}

			Vector2 incoming =
				new Vector2(
					current.X - previous.X,
					current.Y - previous.Y
				);

			Vector2I next =
				default;

			bool found =
				false;

			float bestScore =
				float.MaxValue;

			foreach (
				Vector2I candidate in candidates)
			{
				GridEdge edge =
					new GridEdge(
						current,
						candidate
					);

				if (
					!remaining.Contains(
						edge
					))
				{
					continue;
				}

				Vector2 outgoing =
					new Vector2(
						candidate.X - current.X,
						candidate.Y - current.Y
					);

				float cross =
					incoming.X *
					outgoing.Y -
					incoming.Y *
					outgoing.X;

				float dot =
					incoming.Dot(
						outgoing
					);

				float score;

				if (
					Mathf.Abs(cross) <
					0.001f &&
					dot > 0.0f)
				{
					score = 0.0f;
				}
				else if (cross < 0.0f)
				{
					score = 1.0f;
				}
				else
				{
					score = 2.0f;
				}

				score +=
					Mathf.Atan2(
						outgoing.Y,
						outgoing.X
					) *
					0.0001f;

				if (score < bestScore)
				{
					bestScore = score;
					next = candidate;
					found = true;
				}
			}

			if (!found)
			{
				return null;
			}

			previous =
				current;

			current =
				next;

			RemoveEdge(
				remaining,
				previous,
				current
			);
		}

		return null;
	}

	// ============================================================
	// Add boundary edge
	// ============================================================

	private static void AddBoundaryEdge(
		HashSet<GridEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		GridEdge edge =
			new GridEdge(
				a,
				b
			);

		GridEdge reverse =
			new GridEdge(
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

	private static void RemoveEdge(
		HashSet<GridEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		edges.Remove(
			new GridEdge(
				a,
				b
			)
		);
	}

	// ============================================================
	// Remove duplicate points
	// ============================================================

	private static List<Vector2>
		RemoveDuplicatePoints(
			List<Vector2> polygon)
	{
		if (polygon.Count <= 1)
			return polygon;

		List<Vector2> result =
			new List<Vector2>();

		for (
			int i = 0;
			i < polygon.Count;
			i++)
		{
			Vector2 current =
				polygon[i];

			Vector2 previous =
				polygon[
					(i - 1 + polygon.Count) %
					polygon.Count
				];

			if (
				current.DistanceSquaredTo(
					previous
				) > 0.000001f)
			{
				result.Add(
					current
				);
			}
		}

		return result;
	}

	// ============================================================
	// Remove collinear points
	// ============================================================

	private static List<Vector2>
		RemoveCollinearPoints(
			List<Vector2> polygon)
	{
		if (polygon.Count <= 3)
			return polygon;

		List<Vector2> result =
			new List<Vector2>(
				polygon
			);

		bool changed = true;

		int safety =
			result.Count * 2;

		while (
			changed &&
			result.Count > 3 &&
			safety-- > 0)
		{
			changed = false;

			for (
				int i = 0;
				i < result.Count;
				i++)
			{
				Vector2 previous =
					result[
						(i - 1 + result.Count) %
						result.Count
					];

				Vector2 current =
					result[i];

				Vector2 next =
					result[
						(i + 1) %
						result.Count
					];

				Vector2 a =
					current -
					previous;

				Vector2 b =
					next -
					current;

				float cross =
					a.X * b.Y -
					a.Y * b.X;

				if (
					Mathf.Abs(cross) >
					0.0001f)
				{
					continue;
				}

				float dot =
					a.Dot(b);

				// Only remove points sitting between the
				// neighboring points.
				if (dot < 0.0f)
				{
					continue;
				}

				result.RemoveAt(i);

				changed = true;
				break;
			}
		}

		return result;
	}

	// ============================================================
	// Corner-preserving simplification
	// ============================================================

	private static List<Vector2>
		SimplifyContourPreservingCorners(
			List<Vector2> polygon,
			float tolerance)
	{
		if (
			polygon.Count <= 3 ||
			tolerance <= 0.0f)
		{
			return polygon;
		}

		List<Vector2> result =
			new List<Vector2>(
				polygon
			);

		float toleranceSquared =
			tolerance *
			tolerance;

		bool changed = true;

		int safety =
			result.Count * 2;

		while (
			changed &&
			result.Count > 3 &&
			safety-- > 0)
		{
			changed = false;

			for (
				int i = 0;
				i < result.Count;
				i++)
			{
				Vector2 previous =
					result[
						(i - 1 + result.Count) %
						result.Count
					];

				Vector2 current =
					result[i];

				Vector2 next =
					result[
						(i + 1) %
						result.Count
					];

				Vector2 incoming =
					(current - previous).Normalized();

				Vector2 outgoing =
					(next - current).Normalized();

				if (
					incoming.LengthSquared() <
					0.5f ||
					outgoing.LengthSquared() <
					0.5f)
				{
					continue;
				}

				float cross =
					incoming.X *
					outgoing.Y -
					incoming.Y *
					outgoing.X;

				// ------------------------------------------------
				// NEVER remove a meaningful corner.
				//
				// This is the important difference from a plain
				// RDP-style simplifier.
				// ------------------------------------------------

				if (
					Mathf.Abs(cross) >
					0.01f)
				{
					continue;
				}

				float distance =
					DistancePointToSegmentSquared(
						current,
						previous,
						next
					);

				if (
					distance <=
					toleranceSquared)
				{
					result.RemoveAt(i);

					changed = true;
					break;
				}
			}
		}

		return result;
	}

	// ============================================================
	// Distance point -> segment
	// ============================================================

	private static float
		DistancePointToSegmentSquared(
			Vector2 p,
			Vector2 a,
			Vector2 b)
	{
		Vector2 ab =
			b - a;

		float lengthSquared =
			ab.LengthSquared();

		if (
			lengthSquared <=
			0.000001f)
		{
			return p.DistanceSquaredTo(a);
		}

		float t =
			(p - a).Dot(ab) /
			lengthSquared;

		t =
			Mathf.Clamp(
				t,
				0.0f,
				1.0f
			);

		Vector2 closest =
			a +
			ab * t;

		return p.DistanceSquaredTo(
			closest
		);
	}

	// ============================================================
	// Polygon area
	// ============================================================

	private static float PolygonArea(
		List<Vector2> polygon)
	{
		if (
			polygon == null ||
			polygon.Count < 3)
		{
			return 0.0f;
		}

		float area = 0.0f;

		for (
			int i = 0;
			i < polygon.Count;
			i++)
		{
			Vector2 a =
				polygon[i];

			Vector2 b =
				polygon[
					(i + 1) %
					polygon.Count
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area * 0.5f;
	}

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
	// Grid -> TileMap local
	// ============================================================

	private static Vector2
		GridToTileMapLocal(
			Vector2I point)
	{
		return new Vector2(
			point.X,
			point.Y
		);
	}

	// ============================================================
	// Build thick convex segment
	// ============================================================

	private static Vector2[]
		BuildSegmentPolygon(
			Vector2 a,
			Vector2 b,
			float thickness)
	{
		Vector2 direction =
			b - a;

		float lengthSquared =
			direction.LengthSquared();

		if (
			lengthSquared <=
			0.000001f)
		{
			return Array.Empty<Vector2>();
		}

		float inverseLength =
			1.0f /
			Mathf.Sqrt(
				lengthSquared
			);

		direction *=
			inverseLength;

		Vector2 normal =
			new Vector2(
				-direction.Y,
				direction.X
			);

		float halfThickness =
			Mathf.Max(
				6.0f,
				thickness * 0.5f
			);

		Vector2 offset =
			normal *
			halfThickness;

		Vector2[] polygon =
		{
			a - offset,
			a + offset,
			b + offset,
			b - offset
		};

		// FluidPolygonCollider expects its winding to define
		// its collision normal direction.
		if (
			PolygonArea(polygon) >
			0.0f)
		{
			Array.Reverse(
				polygon
			);
		}

		return polygon;
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
	// TileMap -> Simulation
	// ============================================================

	private Vector2
		ToSimulationSpace(
			Vector2 tileMapLocal)
	{
		Vector2 mainViewportPoint =
			environment.ToGlobal(
				tileMapLocal
			);

		Vector2 viewportPoint =
			mainViewportPoint -
			gameView.GlobalPosition;

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
	// Simulation -> local
	// ============================================================

	private Vector2
		SimulationToThisLocal(
			Vector2 simulationPoint)
	{
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

		Vector2 mainViewportPoint =
			viewportPoint +
			gameView.GlobalPosition;

		return ToLocal(
			mainViewportPoint
		);
	}

	// ============================================================
	// Find fluid simulator
	// ============================================================

	private static FluidSimulator
		FindFluidSimulator(
			Node node)
	{
		if (node is FluidSimulator)
		{
			return (FluidSimulator)node;
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
