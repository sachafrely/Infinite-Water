using Godot;
using System;

public class FluidPolygonCollider
{
// ============================================================
// Precomputed polygon data
// ============================================================


private readonly Vector2[] vertices;

private readonly Vector2[] edges;
private readonly Vector2[] edgeNormals;
private readonly float[] edgeLengthSquared;

private readonly bool counterClockwise;

// ============================================================
// Collision
// ============================================================

private const float CollisionMargin = 1.0f;

private const float Epsilon =
	0.000001f;

// ============================================================
// Polygon AABB
//
// Used as a very cheap broad-phase rejection.
// ============================================================

private float minX;
private float maxX;
private float minY;
private float maxY;

// ============================================================
// Constructor
// ============================================================

public FluidPolygonCollider(
	Vector2[] polygon)
{
	if (
		polygon == null ||
		polygon.Length < 3)
	{
		throw new ArgumentException(
            "Polygon collider requires at least 3 vertices."
		);
	}

	int count =
		polygon.Length;

	vertices =
		new Vector2[count];

	edges =
		new Vector2[count];

	edgeNormals =
		new Vector2[count];

	edgeLengthSquared =
		new float[count];

	Array.Copy(
		polygon,
		vertices,
		count
	);

	counterClockwise =
		CalculateSignedArea() > 0.0f;

	// --------------------------------------------------------
	// Calculate polygon bounds.
	// --------------------------------------------------------

	minX =
		vertices[0].X;

	maxX =
		vertices[0].X;

	minY =
		vertices[0].Y;

	maxY =
		vertices[0].Y;

	for (int i = 1; i < count; i++)
	{
		Vector2 v =
			vertices[i];

		if (v.X < minX)
			minX = v.X;

		if (v.X > maxX)
			maxX = v.X;

		if (v.Y < minY)
			minY = v.Y;

		if (v.Y > maxY)
			maxY = v.Y;
	}

	// --------------------------------------------------------
	// Precompute all edge information.
	// --------------------------------------------------------

	for (int i = 0; i < count; i++)
	{
		Vector2 a =
			vertices[i];

		Vector2 b =
			vertices[
				(i + 1) % count
			];

		Vector2 edge =
			b - a;

		edges[i] =
			edge;

		float lengthSquared =
			edge.LengthSquared();

		edgeLengthSquared[i] =
			lengthSquared;

		if (
			lengthSquared <=
			Epsilon)
		{
			edgeNormals[i] =
				Vector2.Zero;

			continue;
		}

		Vector2 normal;

		if (counterClockwise)
		{
			// CCW polygon:
			// outward is right side.
			normal =
				new Vector2(
					edge.Y,
					-edge.X
				);
		}
		else
		{
			// CW polygon:
			// outward is left side.
			normal =
				new Vector2(
					-edge.Y,
					edge.X
				);
		}

		edgeNormals[i] =
			normal.Normalized();
	}
}

// ============================================================
// Collision resolution
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

	float collisionRadius =
		particleRadius +
		CollisionMargin;

	// ========================================================
	// BROAD PHASE
	//
	// Most particles are nowhere near the polygon.
	//
	// This avoids:
	//
	// - edge loop
	// - square roots
	// - point-inside test
	//
	// for those particles.
	// ========================================================

	if (
		position.X <
		minX - collisionRadius ||

		position.X >
		maxX + collisionRadius ||

		position.Y <
		minY - collisionRadius ||

		position.Y >
		maxY + collisionRadius)
	{
		return false;
	}

	// ========================================================
	// Find closest edge
	// ========================================================

	int vertexCount =
		vertices.Length;

	float closestDistanceSquared =
		float.MaxValue;

	Vector2 closestPoint =
		Vector2.Zero;

	Vector2 closestEdgeNormal =
		Vector2.Zero;

	for (int i = 0; i < vertexCount; i++)
	{
		Vector2 a =
			vertices[i];

		Vector2 edge =
			edges[i];

		float edgeLengthSq =
			edgeLengthSquared[i];

		if (
			edgeLengthSq <=
			Epsilon)
		{
			continue;
		}

		Vector2 toPoint =
			position - a;

		// IMPORTANT:
		// Use the scalar for THIS edge.
		float t =
			toPoint.Dot(edge) /
			edgeLengthSq;

		if (t < 0.0f)
			t = 0.0f;
		else if (t > 1.0f)
			t = 1.0f;

		Vector2 point =
			a +
			edge * t;

		float dx =
			position.X -
			point.X;

		float dy =
			position.Y -
			point.Y;

		float distanceSquared =
			dx * dx +
			dy * dy;

		if (
			distanceSquared <
			closestDistanceSquared)
		{
			closestDistanceSquared =
				distanceSquared;

			closestPoint =
				point;

			closestEdgeNormal =
				edgeNormals[i];
		}
	}

	// ========================================================
	// Safety
	// ========================================================

	if (
		closestDistanceSquared ==
		float.MaxValue)
	{
		return false;
	}

	if (
		closestEdgeNormal.LengthSquared() <=
		Epsilon)
	{
		return false;
	}

	// ========================================================
	// Determine whether the particle is inside.
	// ========================================================

	bool inside =
		IsPointInside(position);

	// ========================================================
	// Outside and too far away.
	// ========================================================

	float collisionDistanceSquared =
		closestDistanceSquared;

	float collisionRadiusSquared =
		collisionRadius *
		collisionRadius;

	if (
		!inside &&
		collisionDistanceSquared >
		collisionRadiusSquared)
	{
		return false;
	}

	normal =
		closestEdgeNormal;

	// ========================================================
	// Particle inside polygon
	// ========================================================

	if (inside)
	{
		correctedPosition =
			closestPoint +
			normal *
			collisionRadius;

		return true;
	}

	// ========================================================
	// Particle outside but intersecting polygon radius
	// ========================================================

	float collisionDistance =
		Mathf.Sqrt(
			collisionDistanceSquared
		);

	float penetration =
		collisionRadius -
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
// Signed polygon area
// ============================================================

private float CalculateSignedArea()
{
	float area =
		0.0f;

	int count =
		vertices.Length;

	for (int i = 0; i < count; i++)
	{
		Vector2 a =
			vertices[i];

		Vector2 b =
			vertices[
				(i + 1) % count
			];

		area +=
			a.X * b.Y -
			b.X * a.Y;
	}

	return area * 0.5f;
}

// ============================================================
// Point inside convex polygon
//
// Works with either winding direction.
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

	for (int i = 0; i < count; i++)
	{
		Vector2 a =
			vertices[i];

		Vector2 edge =
			edges[i];

		Vector2 toPoint =
			point - a;

		float cross =
			edge.X *
			toPoint.Y -
			edge.Y *
			toPoint.X;

		if (
			cross >
			Epsilon)
		{
			hasPositive =
				true;
		}
		else if (
			cross <
			-Epsilon)
		{
			hasNegative =
				true;
		}

		if (
			hasPositive &&
			hasNegative)
		{
			return false;
		}
	}

	return true;
}

}
