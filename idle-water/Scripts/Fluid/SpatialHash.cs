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
		if(x < 0 || y < 0 ||
		   x >= width ||
		   y >= height)
			return -1;


		return y * width + x;
	}



	public void Insert(
		int id,
		Vector2 position)
	{
		int x = (int)(position.X * invCellSize);
		int y = (int)(position.Y * invCellSize);


		int cell = Hash(x,y);

		if(cell < 0)
		{
			next[id] = -1;
			return;
		}


		next[id] = head[cell];
		head[cell] = id;
	}




	public int Query(
		Vector2 position,
		float radius)
	{
		resultCount = 0;


		int minX = (int)((position.X-radius)*invCellSize);
		int maxX = (int)((position.X+radius)*invCellSize);

		int minY = (int)((position.Y-radius)*invCellSize);
		int maxY = (int)((position.Y+radius)*invCellSize);



		for(int y=minY;y<=maxY;y++)
		{
			for(int x=minX;x<=maxX;x++)
			{
				int cell = Hash(x,y);

				if(cell < 0)
					continue;


				int particle = head[cell];


				while(particle != -1)
				{
					results[resultCount] = particle;
					resultCount++;

					particle = next[particle];
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
