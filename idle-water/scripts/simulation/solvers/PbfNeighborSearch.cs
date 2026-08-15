using System;
using System.Runtime.CompilerServices;

internal static class PbfNeighborSearch
{
	public static void BuildNeighborIndexCache(
		SpatialHash hash,
		float[] predX,
		float[] predY,
		int count,
		int stride,
		int[] neighborCounts,
		int[] neighborBuffer,
		int maxNeighbors)
	{
		for (
			int i = 0;
			i < count;
			i++)
		{
			int start =
				i * stride;

			neighborCounts[i] =
				hash.QueryPbf(
					predX[i],
					predY[i],
					predX,
					predY,
					neighborBuffer,
					start,
					maxNeighbors
				);
		}
	}

	public static void UpdateAllNeighborGeometry(
		float[] predX,
		float[] predY,
		int count,
		int stride,
		int[] neighborCounts,
		int[] neighborBuffer,
		float[] neighborDx,
		float[] neighborDy,
		float[] neighborQ,
		float[] neighborGradientScale,
		float inverseSmoothingRadius,
		float inverseRestDensity)
	{
		for (
			int i = 0;
			i < count;
			i++)
		{
			float px =
				predX[i];

			float py =
				predY[i];

			int start =
				i * stride;

			int end =
				start +
				neighborCounts[i];

			UpdateNeighborGeometryRange(
				start,
				end,
				px,
				py,
				predX,
				predY,
				neighborBuffer,
				neighborDx,
				neighborDy,
				neighborQ,
				neighborGradientScale,
				inverseSmoothingRadius,
				inverseRestDensity
			);
		}
	}

	public static void UpdateNeighborCache(
		float[] predX,
		float[] predY,
		int count,
		int stride,
		int[] neighborCounts,
		int[] neighborBuffer,
		float[] neighborDx,
		float[] neighborDy,
		float[] neighborQ,
		float[] neighborGradientScale,
		float inverseSmoothingRadius,
		float inverseRestDensity)
	{
		UpdateAllNeighborGeometry(
			predX,
			predY,
			count,
			stride,
			neighborCounts,
			neighborBuffer,
			neighborDx,
			neighborDy,
			neighborQ,
			neighborGradientScale,
			inverseSmoothingRadius,
			inverseRestDensity
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void UpdateNeighborGeometryRange(
		int start,
		int end,
		float px,
		float py,
		float[] predX,
		float[] predY,
		int[] neighborBuffer,
		float[] neighborDx,
		float[] neighborDy,
		float[] neighborQ,
		float[] neighborGradientScale,
		float inverseSmoothingRadius,
		float inverseRestDensity)
	{
		for (
			int index = start;
			index < end;
			index++)
		{
			int j =
				neighborBuffer[index];

			float dx =
				px -
				predX[j];

			float dy =
				py -
				predY[j];

			float distanceSquared =
				dx * dx +
				dy * dy;

			neighborDx[index] =
				dx;

			neighborDy[index] =
				dy;

			if (
				distanceSquared <=
				0.000001f)
			{
				neighborQ[index] =
					1.0f;

				neighborGradientScale[index] =
					0.0f;

				continue;
			}

			float inverseDistance =
				1.0f /
				MathF.Sqrt(
					distanceSquared
				);

			float q =
				1.0f -
				distanceSquared *
				inverseDistance *
				inverseSmoothingRadius;

			float q2 =
				q * q;

			neighborQ[index] =
				q;

			neighborGradientScale[index] =
				-3.0f *
				q2 *
				inverseSmoothingRadius *
				inverseDistance *
				inverseRestDensity;
		}
	}
}
