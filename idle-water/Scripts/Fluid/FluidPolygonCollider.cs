
using Godot;
using System;

public class FluidPolygonCollider
{
	private readonly Vector2[] vertices;

	// Small extra separation from the solid.
	private const float CollisionMargin = 1.0f;

	private const float Epsilon =
		0.000001f;

	// Polygon winding:
	// positive = counter-clockwise
	// negative = clockwise
	private readonly bool counterClockwise;


	public FluidPolygonCollider(
		Vector2[] polygon)
	{
		if (polygon == null ||
			polygon.Length < 3)
		{
			throw new ArgumentException(
				"Polygon collider requires at least 3 vertices."
			);
		}

		vertices =
			new Vector2[polygon.Length];

		Array.Copy(
			polygon,
			vertices,
			polygon.Length
		);

		counterClockwise =
			CalculateSignedArea() > 0.0f;
	}


	// ============================================================
	// Collision resolution
	//
	// Keeps the particle completely outside the polygon.
	//
	// Works for:
	// - particles inside the polygon
	// - particles touching an edge
	// - particles touching a vertex
	// - particles very close to the surface
	// ============================================================

	public bool ResolveCollision(
		Vector2 position,
		float particleRadius,
		out Vector2 correctedPosition,
		out Vector2 normal)
	{
		correctedPosition =
			position;

		normal =
			Vector2.Zero;

		int vertexCount =
			vertices.Length;

		float closestDistanceSquared =
			float.MaxValue;

		Vector2 closestPoint =
			Vector2.Zero;

		Vector2 closestEdgeNormal =
			Vector2.Zero;

		// --------------------------------------------------------
		// Find closest edge.
		// --------------------------------------------------------

		for (int i = 0;
			 i < vertexCount;
			 i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 b =
				vertices[
					(i + 1) %
					vertexCount
				];

			Vector2 edge =
				b - a;

			float edgeLengthSquared =
				edge.LengthSquared();

			if (edgeLengthSquared <=
				Epsilon)
			{
				continue;
			}

			float t =
				(position - a)
				.Dot(edge) /
				edgeLengthSquared;

			t =
				Mathf.Clamp(
					t,
					0.0f,
					1.0f
				);

			Vector2 point =
				a +
				edge * t;

			Vector2 difference =
				position -
				point;

			float distanceSquared =
				difference.LengthSquared();

			if (distanceSquared <
				closestDistanceSquared)
			{
				closestDistanceSquared =
					distanceSquared;

				closestPoint =
					point;

				// ------------------------------------------------
				// Calculate the ACTUAL outward normal of the edge.
				//
				// This does not depend on where the particle is.
				// ------------------------------------------------

				Vector2 edgeNormal;

				if (counterClockwise)
				{
					// For CCW polygons, outward is right side.
					edgeNormal =
						new Vector2(
							edge.Y,
							-edge.X
						);
				}
				else
				{
					// For CW polygons, outward is left side.
					edgeNormal =
						new Vector2(
							-edge.Y,
							edge.X
						);
				}

				float normalLengthSquared =
					edgeNormal.LengthSquared();

				if (normalLengthSquared >
					Epsilon)
				{
					closestEdgeNormal =
						edgeNormal.Normalized();
				}
			}
		}

		// --------------------------------------------------------
		// Safety check.
		// --------------------------------------------------------

		if (closestDistanceSquared ==
			float.MaxValue)
		{
			return false;
		}

		// --------------------------------------------------------
		// Is particle center inside polygon?
		// --------------------------------------------------------

		bool inside =
			IsPointInside(position);

		float particleRadiusWithMargin =
			particleRadius +
			CollisionMargin;

		float collisionDistance =
			Mathf.Sqrt(
				Mathf.Max(
					closestDistanceSquared,
					0.0f
				)
			);

		// --------------------------------------------------------
		// If particle is outside and farther than its radius,
		// there is no collision.
		// --------------------------------------------------------

		if (!inside &&
			collisionDistance >
			particleRadiusWithMargin)
		{
			return false;
		}

		// --------------------------------------------------------
		// Use the actual polygon edge normal.
		// --------------------------------------------------------

		normal =
			closestEdgeNormal;

		if (normal.LengthSquared() <=
			Epsilon)
		{
			return false;
		}

		// ========================================================
		// Particle is INSIDE the polygon.
		//
		// Push it completely through the nearest edge.
		//
		// This is the important fix for trapped particles.
		// ========================================================

		if (inside)
		{
			correctedPosition =
				closestPoint +
				normal *
				particleRadiusWithMargin;

			return true;
		}

		// ========================================================
		// Particle is OUTSIDE but intersects the collision radius.
		// ========================================================

		float penetration =
			particleRadiusWithMargin -
			collisionDistance;

		if (penetration > 0.0f)
		{
			correctedPosition =
				position +
				normal *
				penetration;
		}

		return true;
	}


	// ============================================================
	// Calculate polygon signed area.
	//
	// Positive  = counter-clockwise
	// Negative  = clockwise
	// ============================================================

	private float CalculateSignedArea()
	{
		float area =
			0.0f;

		int count =
			vertices.Length;

		for (int i = 0;
			 i < count;
			 i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 b =
				vertices[
					(i + 1) %
					count
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area * 0.5f;
	}


	// ============================================================
	// Point inside convex polygon.
	//
	// Works regardless of winding direction.
	// ============================================================

	private bool IsPointInside(
		Vector2 point)
	{
		bool hasPositive =
			false;

		bool hasNegative =
			false;

		int count =
			vertices.Length;

		for (int i = 0;
			 i < count;
			 i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 b =
				vertices[
					(i + 1) %
					count
				];

			Vector2 edge =
				b - a;

			Vector2 toPoint =
				point - a;

			float cross =
				edge.X * toPoint.Y -
				edge.Y * toPoint.X;

			if (cross >
				Epsilon)
			{
				hasPositive =
					true;
			}
			else if (cross <
					 -Epsilon)
			{
				hasNegative =
					true;
			}

			if (hasPositive &&
				hasNegative)
			{
				return false;
			}
		}

		return true;
	}
}
