using Godot;
using System;


public class SpatialHash
{
	private readonly float cellSize;
	private readonly float invCellSize;

	private readonly int[] head;
	private readonly int[] next;

	private readonly int[] results;

	private int resultCount;


	private readonly int width;
	private readonly int height;



	public SpatialHash(int maxParticles, float cellSize, int width, int height)
	{
		this.cellSize = cellSize;
		this.invCellSize = 1.0f / cellSize;

		this.width = width;
		this.height = height;


		head = new int[width * height];
		next = new int[maxParticles];

		results = new int[maxParticles];
	}



	public void Clear()
	{
		Array.Fill(head, -1);
	}



	private int Hash(int x, int y)
	{
		if (x < 0 || y < 0 ||
		   x >= width ||
		   y >= height)
			return -1;


		return y * width + x;
	}



	// Compatibility overloads: keep Vector2-based API.
	public void Insert(int id, Vector2 position)
	{
		Insert(id, position.x, position.y);
	}

	public int Query(Vector2 position, float radius)
	{
		return Query(position.x, position.y, radius);
	}

	// New faster, allocation-free API using raw floats.
	public void Insert(int id, float posX, float posY)
	{
		int x = (int)(posX * invCellSize);
		int y = (int)(posY * invCellSize);

		if (x < 0 || y < 0 || x >= width || y >= height)
		{
			next[id] = -1;
			return;
		}

		int cell = y * width + x;
		next[id] = head[cell];
		head[cell] = id;
	}

	public int Query(float posX, float posY, float radius)
	{
		resultCount = 0;

		int minX = (int)((posX - radius) * invCellSize);
		int maxX = (int)((posX + radius) * invCellSize);

		int minY = (int)((posY - radius) * invCellSize);
		int maxY = (int)((posY + radius) * invCellSize);

		if (minX < 0) minX = 0;
		if (minY < 0) minY = 0;
		if (maxX >= width) maxX = width - 1;
		if (maxY >= height) maxY = height - 1;

		for (int y = minY; y <= maxY; y++)
		{
			int baseIdx = y * width;
			for (int x = minX; x <= maxX; x++)
			{
				int cell = baseIdx + x;
				int particle = head[cell];

				while (particle != -1)
				{
					results[resultCount++] = particle;
					particle = next[particle];
					if (resultCount == results.Length)
						return resultCount; // early exit if results buffer full
				}
			}
		}

		return resultCount;
	}


	public int GetResult(int index)
	{
		return results[index];
	}
}
