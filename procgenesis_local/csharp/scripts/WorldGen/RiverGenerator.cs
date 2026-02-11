using Godot;

namespace PlanetGeneration.WorldGen;

public sealed class RiverGenerator
{
	public float[,] Generate(int width, int height, int seed, float seaLevel, float[,] elevation, float[,] moisture, WorldTuning tuning)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(seed ^ 0x27d4eb2d);

		var riverLayer = Array2D.Create(width, height, 0f);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (elevation[x, y] <= tuning.RiverSourceElevationThreshold)
				{
					continue;
				}

				if (moisture[x, y] <= tuning.RiverSourceMoistureThreshold)
				{
					continue;
				}

				if (rng.Randf() >= tuning.RiverSourceChance)
				{
					continue;
				}

				riverLayer[x, y] = rng.RandfRange(0.1f, 1f);
				TraceLegacyRiver(riverLayer, elevation, seaLevel, x, y, width, height);
			}
		}

		return riverLayer;
	}

	private void TraceLegacyRiver(float[,] riverLayer, float[,] elevation, float seaLevel, int sourceX, int sourceY, int width, int height)
	{
		if (riverLayer[sourceX, sourceY] <= 0f)
		{
			return;
		}

		var currentElevation = elevation[sourceX, sourceY];
		var currentX = sourceX;
		var currentY = sourceY;

		var maxSteps = width * height;
		var step = 0;

		while (currentElevation >= seaLevel && step < maxSteps)
		{
			var smallestElevation = currentElevation;
			var lowX = currentX;
			var lowY = currentY;
			var foundLower = false;

			for (var oy = -1; oy <= 1; oy++)
			{
				for (var ox = -1; ox <= 1; ox++)
				{
					if (ox == 0 && oy == 0)
					{
						continue;
					}

					var ny = currentY + oy;
					if (ny < 0 || ny >= height)
					{
						continue;
					}

					var nx = currentX + ox;
					if (nx >= width)
					{
						nx %= width;
					}
					else if (nx < 0)
					{
						nx = width + ox;
					}

					if (riverLayer[nx, ny] > 0f)
					{
						continue;
					}

					if (elevation[nx, ny] >= smallestElevation)
					{
						continue;
					}

					smallestElevation = elevation[nx, ny];
					lowX = nx;
					lowY = ny;
					foundLower = true;
				}
			}

			if (!foundLower)
			{
				break;
			}

			riverLayer[lowX, lowY] += riverLayer[sourceX, sourceY] * 0.5f;
			riverLayer[sourceX, sourceY] *= 0.5f;

			currentX = lowX;
			currentY = lowY;

			if (elevation[lowX, lowY] < seaLevel)
			{
				break;
			}

			currentElevation -= 0.000001f;
			step++;
		}
	}
}
