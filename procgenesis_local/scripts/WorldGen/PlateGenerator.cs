using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class PlateGenerator
{
	public PlateResult Generate(int width, int height, int plateCount, int seed, float oceanicRatio)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)seed;

		var sites = CreateSites(width, height, plateCount, oceanicRatio, rng);
		var plateIds = AssignPlateIds(width, height, sites, seed);
		var baseElevation = BuildBaseElevationMap(width, height, plateIds, sites);

		var (boundaryTypes, stressMap) = BuildBoundaryStressMap(width, height, plateIds, sites);
		var (neighbors, borderPoints) = BuildNeighborAndBorderData(width, height, stressMap, sites, plateIds);

		return new PlateResult
		{
			PlateIds = plateIds,
			PlateBaseElevation = baseElevation,
			BoundaryTypes = boundaryTypes,
			StressMap = stressMap,
			Neighbors = neighbors,
			BorderPoints = borderPoints,
			Sites = sites
		};
	}

	private List<PlateSite> CreateSites(int width, int height, int plateCount, float oceanicRatio, RandomNumberGenerator rng)
	{
		var sites = new List<PlateSite>(plateCount);

		for (var i = 0; i < plateCount; i++)
		{
			var position = new Vector2I(rng.RandiRange(0, width - 1), rng.RandiRange(0, height - 1));
			var motion = new Vector2(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f));
			if (motion.LengthSquared() < 0.0001f)
			{
				motion = new Vector2(1f, 0f);
			}
			else
			{
				motion = motion.Normalized();
			}

			var isOceanic = rng.Randf() < oceanicRatio;
			var baseElevation = isOceanic ? 0.55f * rng.Randf() : 0.45f + 0.55f * rng.Randf();

			sites.Add(new PlateSite
			{
				Id = i,
				Position = position,
				Motion = motion,
				IsOceanic = isOceanic,
				BaseElevation = baseElevation,
				DebugColor = Color.FromHsv(rng.Randf(), 0.65f, 0.95f)
			});
		}

		return sites;
	}

	private int[,] AssignPlateIds(int width, int height, List<PlateSite> sites, int seed)
	{
		var basePlateIds = new int[width, height];

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				var closest = 0;
				var closestDistance = float.MaxValue;

				for (var i = 0; i < sites.Count; i++)
				{
					var distance = WrappedDistanceSquared(width, x, y, sites[i].Position.X, sites[i].Position.Y);

					if (distance < closestDistance)
					{
						closestDistance = distance;
						closest = i;
					}
				}

				basePlateIds[x, y] = closest;
			}
		});

		var turbulenceScale = Mathf.Clamp(Mathf.RoundToInt(width * (56f / 1024f)), 12, 88);
		return AddVoronoiTurbulence(basePlateIds, width, height, seed ^ unchecked((int)0x4f1bbcdc), turbulenceScale);
	}

	private int[,] AddVoronoiTurbulence(int[,] basePlateIds, int width, int height, int seed, int scaleFactor)
	{
		var turbulence = new int[width, height];

		Parallel.For(0, height, y =>
		{
			var noiseX = new FastNoiseLite
			{
				Seed = seed,
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				Frequency = 1f
			};

			var noiseY = new FastNoiseLite
			{
				Seed = seed ^ unchecked((int)0x9e3779b9),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				Frequency = 1f
			};

			for (var x = 0; x < width; x++)
			{
				var sx = 8f * x / Mathf.Max(width, 1);
				var sy = 8f * y / Mathf.Max(height, 1);

				var tx = SampleLegacyTurbulence(noiseX, sx, sy);
				var ty = SampleLegacyTurbulence(noiseY, sx, sy);

				var px = WrapX(Mathf.FloorToInt(x + scaleFactor * tx), width);
				var py = Mathf.Clamp(Mathf.FloorToInt(y + scaleFactor * ty), 0, height - 1);

				turbulence[x, y] = basePlateIds[px, py];
			}
		});

		return turbulence;
	}

	private float SampleLegacyTurbulence(FastNoiseLite noise, float x, float y)
	{
		return
			noise.GetNoise2D(x, y) +
			0.5f * noise.GetNoise2D(2f * x, 2f * y) +
			0.25f * noise.GetNoise2D(4f * x, 4f * y) +
			0.125f * noise.GetNoise2D(8f * x, 8f * y) +
			0.0625f * noise.GetNoise2D(16f * x, 16f * y);
	}

	private int WrapX(int x, int width)
	{
		if (width <= 0)
		{
			return 0;
		}

		var result = x % width;
		return result < 0 ? result + width : result;
	}

	private float[,] BuildBaseElevationMap(int width, int height, int[,] plateIds, List<PlateSite> sites)
	{
		var elevation = new float[width, height];

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				elevation[x, y] = sites[plateIds[x, y]].BaseElevation;
			}
		});

		return elevation;
	}

	private (PlateBoundaryType[,], PlateStressCell[,]) BuildBoundaryStressMap(int width, int height, int[,] plateIds, List<PlateSite> sites)
	{
		var boundary = new PlateBoundaryType[width, height];
		var stress = new PlateStressCell[width, height];

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				var plateId = plateIds[x, y];
				var found = false;

				foreach (var neighbor in GetNeighborPositions(width, height, x, y))
				{
					var neighborPlate = plateIds[neighbor.X, neighbor.Y];
					if (neighborPlate == plateId)
					{
						continue;
					}

					var currentSite = sites[plateId];
					var neighborSite = sites[neighborPlate];

					var relativeMotion = currentSite.Motion - neighborSite.Motion;
					var slope = new Vector2(
						neighborSite.Position.X - currentSite.Position.X,
						neighborSite.Position.Y - currentSite.Position.Y);

					var slopeLength = slope.Length();
					if (slopeLength < 0.0001f)
					{
						slope = new Vector2(1f, 0f);
						slopeLength = 1f;
					}

					var parallel = relativeMotion.Dot(slope) / slopeLength;
					var parallelProjection = slope * (parallel / slopeLength);
					var perpendicularProjection = relativeMotion - parallelProjection;
					var shear = perpendicularProjection.Length();

					var type = shear > Mathf.Abs(parallel)
						? PlateBoundaryType.Transform
						: parallel > 0f
							? PlateBoundaryType.Convergent
							: PlateBoundaryType.Divergent;

					boundary[x, y] = type;
					stress[x, y] = new PlateStressCell
					{
						IsBorder = true,
						DirectForce = parallel,
						ShearForce = shear,
						Type = type,
						Id0 = plateId,
						Id1 = neighborPlate,
						Neighbor = neighbor
					};

					found = true;
					break;
				}

				if (found)
				{
					continue;
				}

				boundary[x, y] = PlateBoundaryType.None;
				stress[x, y] = new PlateStressCell
				{
					IsBorder = false,
					DirectForce = 0f,
					ShearForce = 0f,
					Type = PlateBoundaryType.None,
					Id0 = plateId,
					Id1 = -1,
					Neighbor = new Vector2I(-1, -1)
				};
			}
		});

		return (boundary, stress);
	}

	private (List<PlateNeighborInfo>, List<PlateEdgePoint>) BuildNeighborAndBorderData(int width, int height, PlateStressCell[,] stressMap, List<PlateSite> sites, int[,] plateIds)
	{
		var neighbors = new List<PlateNeighborInfo>();
		var borderPoints = new List<PlateEdgePoint>();
		var neighborSet = new HashSet<long>();

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var cell = stressMap[x, y];
				if (!cell.IsBorder)
				{
					continue;
				}

				borderPoints.Add(new PlateEdgePoint
				{
					X = x,
					Y = y,
					Id = cell.Id0,
					NeighborId = cell.Id1,
					Type = cell.Type,
					IsOceanic = sites[plateIds[x, y]].IsOceanic
				});

				var key = ((long)cell.Id0 << 32) | (uint)cell.Id1;
				if (neighborSet.Contains(key))
				{
					continue;
				}

				neighborSet.Add(key);
				neighbors.Add(new PlateNeighborInfo
				{
					Id = cell.Id0,
					NeighborId = cell.Id1,
					DirectForce = cell.DirectForce,
					ShearForce = cell.ShearForce,
					Type = cell.Type
				});
			}
		}

		neighbors.Sort((a, b) =>
		{
			var idCompare = a.Id.CompareTo(b.Id);
			return idCompare != 0 ? idCompare : a.NeighborId.CompareTo(b.NeighborId);
		});

		borderPoints.Sort((a, b) =>
		{
			var idCompare = a.Id.CompareTo(b.Id);
			if (idCompare != 0)
			{
				return idCompare;
			}

			var neighborCompare = a.NeighborId.CompareTo(b.NeighborId);
			if (neighborCompare != 0)
			{
				return neighborCompare;
			}

			var yCompare = a.Y.CompareTo(b.Y);
			return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
		});

		return (neighbors, borderPoints);
	}

	private float WrappedDistanceSquared(int width, int x0, int y0, int x1, int y1)
	{
		var dx = Mathf.Abs(x0 - x1);
		if (dx > width / 2f)
		{
			dx = width - dx;
		}

		var dy = y0 - y1;
		return dx * dx + dy * dy;
	}

	private IEnumerable<Vector2I> GetNeighborPositions(int width, int height, int x, int y)
	{
		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = x + ox;
				var ny = y + oy;

				if (nx < 0)
				{
					nx += width;
				}
				else if (nx >= width)
				{
					nx -= width;
				}

				if (ny < 0 || ny >= height)
				{
					continue;
				}

				yield return new Vector2I(nx, ny);
			}
		}
	}
}
