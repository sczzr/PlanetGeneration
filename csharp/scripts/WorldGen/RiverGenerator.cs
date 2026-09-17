using Godot;

namespace PlanetGeneration.WorldGen;

public sealed class RiverGenerator
{
	private readonly Vector2I[] _neighborOffsets =
	{
		new Vector2I(-1, -1),
		new Vector2I(0, -1),
		new Vector2I(1, -1),
		new Vector2I(-1, 0),
		new Vector2I(1, 0),
		new Vector2I(-1, 1),
		new Vector2I(0, 1),
		new Vector2I(1, 1)
	};

	public float[,] Generate(int width, int height, int seed, float seaLevel, float[,] elevation, float[,] moisture, WorldTuning tuning, float densityFactor = 1f)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(seed ^ 0x27d4eb2d);
		var density = Mathf.Clamp(densityFactor, 0.4f, 2.5f);

		var riverLayer = Array2D.Create(width, height, 0f);
		var sourceCount = Mathf.Max(2, Mathf.RoundToInt(width * height * tuning.RiverSourceChance * 0.35f * density));
		var attempts = Mathf.Max(sourceCount * 8, 128);

		for (var i = 0; i < attempts; i++)
		{
			if (sourceCount <= 0)
			{
				break;
			}

			var x = rng.RandiRange(0, width - 1);
			var y = rng.RandiRange(0, height - 1);

			if (!CanBeRiverSource(x, y, width, height, seaLevel, elevation, moisture, tuning))
			{
				continue;
			}

			var baseFlow = 0.22f + 0.58f * moisture[x, y] + rng.RandfRange(0f, 0.2f);
			TraceRiverFlow(riverLayer, elevation, moisture, seaLevel, x, y, width, height, baseFlow, rng);
			sourceCount--;
		}

		return riverLayer;
	}

	private bool CanBeRiverSource(int x, int y, int width, int height, float seaLevel, float[,] elevation, float[,] moisture, WorldTuning tuning)
	{
		if (elevation[x, y] <= tuning.RiverSourceElevationThreshold - 0.03f)
		{
			return false;
		}

		if (moisture[x, y] <= tuning.RiverSourceMoistureThreshold)
		{
			return false;
		}

		if (elevation[x, y] <= seaLevel + 0.05f)
		{
			return false;
		}

		var localRelief = ComputeLocalRelief(elevation, x, y, width, height);
		if (localRelief < 0.012f)
		{
			return false;
		}

		return true;
	}

	private static float ComputeLocalRelief(float[,] elevation, int x, int y, int width, int height)
	{
		var minValue = elevation[x, y];
		var maxValue = elevation[x, y];

		for (var oy = -1; oy <= 1; oy++)
		{
			var ny = y + oy;
			if (ny < 0 || ny >= height)
			{
				continue;
			}

			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var value = elevation[nx, ny];
				if (value < minValue)
				{
					minValue = value;
				}

				if (value > maxValue)
				{
					maxValue = value;
				}
			}
		}

		return maxValue - minValue;
	}

	private void TraceRiverFlow(
		float[,] riverLayer,
		float[,] elevation,
		float[,] moisture,
		float seaLevel,
		int sourceX,
		int sourceY,
		int width,
		int height,
		float sourceFlow,
		RandomNumberGenerator rng)
	{
		var currentX = sourceX;
		var currentY = sourceY;
		var flow = Mathf.Clamp(sourceFlow, 0.08f, 1.4f);
		var directionX = 0;
		var directionY = 0;
		var stagnation = 0;

		var maxSteps = width + height;
		maxSteps *= 3;

		for (var step = 0; step < maxSteps; step++)
		{
			if (currentY < 0 || currentY >= height)
			{
				break;
			}

			var currentElevation = elevation[currentX, currentY];
			if (currentElevation <= seaLevel)
			{
				break;
			}

			riverLayer[currentX, currentY] += flow;
			if (riverLayer[currentX, currentY] > 2.4f)
			{
				riverLayer[currentX, currentY] = 2.4f;
			}

			var next = SelectNextCell(
				currentX,
				currentY,
				directionX,
				directionY,
				width,
				height,
				seaLevel,
				elevation,
				moisture,
				riverLayer,
				rng);

			if (!next.HasValue)
			{
				stagnation++;
				if (stagnation >= 2)
				{
					break;
				}

				flow *= 0.82f;
				continue;
			}

			var (nextX, nextY) = next.Value;
			var dx = WrapDx(nextX - currentX, width);
			var dy = nextY - currentY;

			directionX = dx;
			directionY = dy;
			currentX = nextX;
			currentY = nextY;
			stagnation = 0;

			var nextElevation = elevation[currentX, currentY];
			var slope = Mathf.Max(currentElevation - nextElevation, 0f);
			var moistureBoost = moisture[currentX, currentY] * 0.16f;
			flow = Mathf.Clamp(flow * (0.94f + slope * 1.35f) + moistureBoost, 0.06f, 1.6f);

			if (nextElevation <= seaLevel)
			{
				riverLayer[currentX, currentY] += flow;
				break;
			}
		}
	}

	private (int X, int Y)? SelectNextCell(
		int x,
		int y,
		int directionX,
		int directionY,
		int width,
		int height,
		float seaLevel,
		float[,] elevation,
		float[,] moisture,
		float[,] riverLayer,
		RandomNumberGenerator rng)
	{
		var currentElevation = elevation[x, y];
		var bestScore = float.NegativeInfinity;
		var found = false;
		var bestX = x;
		var bestY = y;

		for (var i = 0; i < _neighborOffsets.Length; i++)
		{
			var offset = _neighborOffsets[i];
			var nx = WrapX(x + offset.X, width);
			var ny = y + offset.Y;

			if (ny < 0 || ny >= height)
			{
				continue;
			}

			var nextElevation = elevation[nx, ny];
			var drop = currentElevation - nextElevation;
			var canClimb = drop > -0.005f;
			if (!canClimb)
			{
				continue;
			}

			var directionDot = (directionX * offset.X) + (directionY * offset.Y);
			var directionBias = directionDot > 0 ? 0.035f : directionDot < 0 ? -0.025f : 0f;
			var downhillScore = drop * 3.4f;
			var moistureBias = moisture[nx, ny] * 0.12f;
			var existingRiverBias = riverLayer[nx, ny] > 0f ? 0.06f : 0f;
			var seaBonus = nextElevation <= seaLevel ? 1.0f : 0f;
			var jitter = rng.RandfRange(-0.02f, 0.02f);

			var score = downhillScore + directionBias + moistureBias + existingRiverBias + seaBonus + jitter;
			if (score <= bestScore)
			{
				continue;
			}

			bestScore = score;
			bestX = nx;
			bestY = ny;
			found = true;
		}

		if (!found)
		{
			return null;
		}

		return (bestX, bestY);
	}

	private static int WrapX(int x, int width)
	{
		if (x >= width)
		{
			return x % width;
		}

		if (x < 0)
		{
			return width + x;
		}

		return x;
	}

	private static int WrapDx(int dx, int width)
	{
		if (dx > width / 2)
		{
			return dx - width;
		}

		if (dx < -(width / 2))
		{
			return dx + width;
		}

		return dx;
	}
}
