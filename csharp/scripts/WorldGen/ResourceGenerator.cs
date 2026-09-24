using Godot;
using PlanetGeneration.Core.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

/// <summary>
/// 资源生成全要素输出结果（包含主显图层与三层垂直叠加数据）。
/// </summary>
public sealed class ResourceGenerationResult
{
	/// <summary>主显矿产（用于底图配色及宏观显示，由高品阶天/地/灵/凡与富集度综合裁定）。</summary>
	public required OreType[,] PrimaryOre { get; init; }

	/// <summary>地下浅层工业矿产（11种：石材、黏土、石灰石、煤、铁、铜、铝、硅、石油、天然气、稀有金属）。</summary>
	public required OreType[,] IndustrialOre { get; init; }

	/// <summary>地下深层超自然矿产（10种：灵晶、炎曜、寒魄、雷髓、生灵、幽冥、空冥、星髓、律纹、源质）。</summary>
	public required OreType[,] SupernaturalOre { get; init; }

	/// <summary>地表文明遗迹卡牌资源（7种：忆晶砂、灵纹矿、共鸣晶、回响石、定序金、界匣晶、因律石）。</summary>
	public required OreType[,] CardOre { get; init; }

	/// <summary>灵脉强度场 (0..255)。</summary>
	public required byte[,] Leyline { get; init; }
}

/// <summary>
/// 资源生成上下文环境参数集合。
/// </summary>
public sealed class ResourceContext
{
	public required int Width { get; init; }
	public required int Height { get; init; }
	public required int Seed { get; init; }
	public required RockType[,] Rocks { get; init; }
	public required float[] Anomaly { get; init; }
	public required float[,] Elevation { get; init; }
	public required float SeaLevel { get; init; }
	public required float MagicDensity { get; init; }
	public float[,]? Temperature { get; init; }
	public float[,]? Moisture { get; init; }
	public float[,]? River { get; init; }
	public BiomeType[,]? Biome { get; init; }
	public PlateBoundaryType[,]? Boundaries { get; init; }
	public IReadOnlyList<CityInfo>? Cities { get; init; }
}

/// <summary>
/// 矿产与自然/超凡/卡牌资源生成器。
/// 严格依据《游戏矿产资源与地形分布设计.md》构建：
/// 工业看地质（连续矿脉走向）、超凡看灵脉（网络汇聚与环境异常）、卡牌看文明（历史遗迹节点）。
/// </summary>
public sealed class ResourceGenerator
{
	private const float OreAnomalyAmplitude = 0.4375f;
	private const int AnomalyBins = 4096;
	private const double DepositCoverage = 0.16; // 陆地及近海矿带适度丰满

	/// <summary>
	/// 生成岩性与基础异常度场（与历史管线第一阶段保持纯计算兼容）。
	/// </summary>
	public (RockType[,] Rock, float[] Anomaly) Generate(
		int width,
		int height,
		int seed,
		PlateBoundaryType[,] boundaries)
	{
		var rocks = new RockType[width, height];
		var anomaly = new float[width * height];

		Parallel.For(0, height, y =>
		{
			var rockNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x6a09e667),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 4,
				Frequency = 0.012f
			};

			var oreNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0xbb67ae85),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 5,
				Frequency = 0.35f
			};

			for (var x = 0; x < width; x++)
			{
				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
				var ny = 4f * y / height;

				var rockValue = rockNoise.GetNoise3D(nx, ny, nz);
				rockValue = (rockValue + 1f) * 0.5f;
				rockValue *= 0.7f;
				rockValue = Mathf.Pow(rockValue, 2f);

				var isMetamorphic = boundaries[x, y] == PlateBoundaryType.Transform && rockValue > 0.18f;
				var isIgneous = rockValue > 0.4f || boundaries[x, y] == PlateBoundaryType.Convergent;

				rocks[x, y] = isMetamorphic
					? RockType.Metamorphic
					: isIgneous
						? RockType.Igneous
						: RockType.Sedimentary;

				var oreValue =
					0.25f * oreNoise.GetNoise3D(4f * nx, 4f * ny, 4f * nz) +
					0.125f * oreNoise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
					0.0625f * oreNoise.GetNoise3D(16f * nx, 16f * ny, 16f * nz);

				anomaly[y * width + x] = Mathf.Clamp((oreValue + OreAnomalyAmplitude) / (2f * OreAnomalyAmplitude), 0f, 1f);
			}
		});

		return (rocks, anomaly);
	}

	/// <summary>
	/// 兼容型单图层落矿接口：返回裁定后的主显矿产阵列。
	/// </summary>
	public OreType[,] PlaceDeposits(
		RockType[,] rocks,
		float[] anomaly,
		float[,] elevation,
		float seaLevel,
		float magicDensity)
	{
		var width = rocks.GetLength(0);
		var height = rocks.GetLength(1);
		var ctx = new ResourceContext
		{
			Width = width,
			Height = height,
			Seed = 123456,
			Rocks = rocks,
			Anomaly = anomaly,
			Elevation = elevation,
			SeaLevel = seaLevel,
			MagicDensity = magicDensity
		};

		return GenerateAllDeposits(ctx).PrimaryOre;
	}

	/// <summary>
	/// 完整资源生成流水线：生成工业、超自然、卡牌三层垂直叠加与主显矿产。
	/// </summary>
	public ResourceGenerationResult GenerateAllDeposits(ResourceContext ctx)
	{
		var width = ctx.Width;
		var height = ctx.Height;
		var seaLevel = ctx.SeaLevel;
		var magicDensity = Math.Clamp(ctx.MagicDensity / 100f, 0f, 1f);

		var primaryOre = new OreType[width, height];
		var industrialOre = new OreType[width, height];
		var supernaturalOre = new OreType[width, height];
		var cardOre = new OreType[width, height];
		var leylineBytes = new byte[width, height];

		// 1. 灵脉网络与场强度生成
		var leylineField = GenerateLeylineField(ctx);

		// 2. 连续工业矿脉骨架噪声
		var veinNoiseField = GenerateVeinNoiseField(ctx);

		// 3. 计算陆地与近海矿化门槛（分位数统计）
		var rank = BuildLandRank(ctx.Anomaly, ctx.Elevation, width, height, seaLevel, out var effectivePixels);
		var thresholdBin = effectivePixels > 0 ? FindPercentileBin(rank, effectivePixels, DepositCoverage) : 0;
		var belowDeposits = thresholdBin == 0 ? 0 : rank[thresholdBin - 1];
		var depositCount = Math.Max(effectivePixels - belowDeposits, 1);

		// 4. 文明与遗迹辐射节点图
		var relicField = GenerateRelicField(ctx);

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				var elev = ctx.Elevation[x, y];
				var rock = ctx.Rocks[x, y];
				var temp = ctx.Temperature != null ? ctx.Temperature[x, y] : EstimateTemperature(y, height, elev, seaLevel);
				var moist = ctx.Moisture != null ? ctx.Moisture[x, y] : 0.5f;
				var boundary = ctx.Boundaries != null ? ctx.Boundaries[x, y] : PlateBoundaryType.None;
				var biome = ctx.Biome != null ? ctx.Biome[x, y] : BiomeType.Grassland;

				var anomBin = AnomalyBin(ctx.Anomaly[y * width + x]);
				var grade = anomBin >= thresholdBin && depositCount > 0
					? Math.Clamp((rank[anomBin] - belowDeposits) / (float)depositCount, 0f, 1f)
					: 0f;

				var veinVal = veinNoiseField[x, y];
				var leylineVal = leylineField[x, y];
				leylineBytes[x, y] = (byte)Math.Clamp((int)(leylineVal * 255f), 0, 255);

				// ──────── A. 基础工业矿产 ────────
				var ind = ResolveIndustrialOre(elev, seaLevel, rock, temp, moist, boundary, grade, veinVal);
				industrialOre[x, y] = ind;

				// ──────── B. 超自然矿产 ────────
				var sup = ResolveSupernaturalOre(elev, seaLevel, leylineVal, magicDensity, temp, moist, boundary, biome);
				supernaturalOre[x, y] = sup;

				// ──────── C. 卡牌资源 ────────
				var crd = ResolveCardOre(relicField[x, y], elev, seaLevel, leylineVal, boundary, magicDensity);
				cardOre[x, y] = crd;

				// ──────── D. 裁定主显矿产（PrimaryOre） ────────
				primaryOre[x, y] = ResolveDominantOre(ind, sup, crd);
			}
		});

		return new ResourceGenerationResult
		{
			PrimaryOre = primaryOre,
			IndustrialOre = industrialOre,
			SupernaturalOre = supernaturalOre,
			CardOre = cardOre,
			Leyline = leylineBytes
		};
	}

	/// <summary>
	/// 工业矿产生成规则：地质与岩性驱动，形成有方向的连续矿脉。
	/// 涵盖山脉、丘陵、平原、沙漠、火山、海岸大陆架与海底裂谷。
	/// </summary>
	private static OreType ResolveIndustrialOre(
		float elevation,
		float seaLevel,
		RockType rock,
		float temperature,
		float moisture,
		PlateBoundaryType boundary,
		float grade,
		float vein)
	{
		// 海洋地貌
		if (elevation <= seaLevel)
		{
			var depth = seaLevel - elevation;

			// 大陆架浅海：石油、天然气、石灰石
			if (depth < 0.12f && vein > 0.68f)
			{
				if (vein > 0.86f) return OreType.Oil;
				if (vein > 0.76f) return OreType.NaturalGas;
				return OreType.Limestone;
			}

			// 海底裂谷（构造带深海沟）：稀有金属
			if (depth > 0.35f && boundary != PlateBoundaryType.None && vein > 0.74f)
			{
				return OreType.RareMetal;
			}

			return OreType.None;
		}

		// 陆地：矿脉或成矿异常必须达到富集门槛
		if (grade <= 0f && vein < 0.64f)
		{
			return OreType.None;
		}

		var relElev = elevation - seaLevel;
		var isArid = moisture < 0.28f && temperature > 0.55f; // 干旱沙漠
		var isVolcanic = boundary == PlateBoundaryType.Convergent && relElev > 0.35f;

		// 1. 火山与强造山核心：铁、铜、稀有金属、石材
		if (isVolcanic)
		{
			if (vein > 0.88f || grade > 0.85f) return OreType.RareMetal;
			if (vein > 0.72f) return OreType.Copper;
			if (vein > 0.58f) return OreType.Iron;
			return OreType.Stone;
		}

		// 2. 干燥沙漠环境：铝、硅、石油、天然气、稀有金属
		if (isArid)
		{
			if (vein > 0.87f) return OreType.RareMetal;
			if (vein > 0.77f) return OreType.Oil;
			if (vein > 0.68f) return OreType.Silicon;
			if (vein > 0.58f) return OreType.Aluminum;
			return OreType.NaturalGas;
		}

		// 3. 高山/深山脉：铁、铜、煤、铝、稀有金属、石材
		if (relElev > 0.32f)
		{
			if (rock == RockType.Igneous)
			{
				if (vein > 0.86f || grade > 0.88f) return OreType.RareMetal;
				if (vein > 0.70f) return OreType.Copper;
				if (vein > 0.52f) return OreType.Iron;
				return OreType.Stone;
			}
			if (rock == RockType.Metamorphic)
			{
				if (vein > 0.84f) return OreType.Silicon;
				if (vein > 0.68f) return OreType.Aluminum;
				if (vein > 0.52f) return OreType.Iron;
				return OreType.Stone;
			}
			// 沉积山地
			if (vein > 0.75f) return OreType.Coal;
			if (vein > 0.58f) return OreType.Iron;
			return OreType.Stone;
		}

		// 4. 丘陵：铁、铜、煤、石材
		if (relElev > 0.14f)
		{
			if (rock == RockType.Sedimentary && vein > 0.60f) return OreType.Coal;
			if (vein > 0.75f) return OreType.Copper;
			if (vein > 0.58f) return OreType.Iron;
			return OreType.Stone;
		}

		// 5. 平原与低地盆地：黏土、石灰石、沉积煤
		if (rock == RockType.Sedimentary)
		{
			if (vein > 0.82f) return OreType.Coal;
			if (vein > 0.68f) return OreType.Limestone;
			return OreType.Clay;
		}

		return vein > 0.70f ? OreType.Limestone : OreType.Clay;
	}

	/// <summary>
	/// 超自然矿产生成规则：灵脉驱动，环境异常定性。
	/// 结合温度、极端寒热、雷暴降水、原始森林生态、空间断裂与极高峰顶。
	/// </summary>
	private static OreType ResolveSupernaturalOre(
		float elevation,
		float seaLevel,
		float leyline,
		float magicDensity,
		float temperature,
		float moisture,
		PlateBoundaryType boundary,
		BiomeType biome)
	{
		// 零魔世界无超凡矿产
		if (magicDensity <= 0.02f)
		{
			return OreType.None;
		}

		// 灵脉强度门槛随魔法丰度动态调整：高魔世界灵脉更容易溢出成矿
		var threshold = Mathf.Lerp(0.88f, 0.60f, magicDensity);
		if (leyline < threshold)
		{
			return OreType.None;
		}

		var isLand = elevation > seaLevel;
		var relElev = elevation - seaLevel;

		// ──────── 天阶超自然矿产（世界本源与法则） ────────
		// 源质矿 [天]：顶尖灵脉核心 + 高魔世界极值
		if (leyline > 0.94f && magicDensity > 0.70f && (relElev > 0.40f || boundary == PlateBoundaryType.Convergent))
		{
			return OreType.GenesisOre;
		}

		// 律纹石 [天]：世界法则结构节点
		if (leyline > 0.89f && magicDensity > 0.55f)
		{
			return OreType.LawStone;
		}

		// ──────── 地阶超自然矿产 ────────
		// 星髓 [地]：极高海拔峰顶天象高暴露
		if (isLand && relElev > 0.48f && leyline > 0.78f)
		{
			return OreType.AstralPith;
		}

		// 空冥晶 [地]：空间断层剪切带与海底深裂谷
		if ((boundary == PlateBoundaryType.Transform || (!isLand && boundary != PlateBoundaryType.None)) && leyline > 0.74f)
		{
			return OreType.VoidCrystal;
		}

		// 幽冥晶 [地]：极阴湿冷沼泽、阴暗洼地死地
		if (moisture > 0.65f && temperature < 0.42f && relElev < 0.12f && leyline > 0.75f)
		{
			return OreType.NetherCrystal;
		}

		// ──────── 灵阶超自然矿产（环境属性直接映射） ────────
		// 炎曜晶 [灵]：高温环境或火山熔岩
		if (temperature > 0.72f || boundary == PlateBoundaryType.Convergent)
		{
			return OreType.SunfireCrystal;
		}

		// 寒魄晶 [灵]：极低温或冰雪冻土
		if (temperature < 0.22f || biome == BiomeType.Ice || biome == BiomeType.Tundra || biome == BiomeType.SnowyMountain)
		{
			return OreType.FrostSoulCrystal;
		}

		// 雷髓矿 [灵]：迎风大风暴与强对流峡谷
		if (moisture > 0.68f && relElev > 0.22f)
		{
			return OreType.ThunderMarrow;
		}

		// 生灵髓 [灵]：原始密林与高生态生机湿地
		if (biome == BiomeType.TropicalRainForest || biome == BiomeType.TropicalSeasonalForest || biome == BiomeType.TemperateRainForest || biome == BiomeType.Taiga)
		{
			return OreType.LifePith;
		}

		// 基础通用超凡能量：灵晶 [灵]
		return OreType.SpiritCrystal;
	}

	/// <summary>
	/// 卡牌资源生成规则：文明历史遗物驱动。
	/// 古城学院、宗门法阵、英雄战迹、古代工坊、先民核心与因果空间裂隙。
	/// </summary>
	private static OreType ResolveCardOre(
		float relicPower,
		float elevation,
		float seaLevel,
		float leyline,
		PlateBoundaryType boundary,
		float magicDensity)
	{
		// 遗迹影响度必须达到成物门槛
		if (relicPower < 0.66f)
		{
			return OreType.None;
		}

		// ──────── 天阶卡牌资源 ────────
		// 因律石 [天]：神魔古战场与规则冲突极值点
		if (relicPower > 0.94f && boundary != PlateBoundaryType.None && magicDensity > 0.45f)
		{
			return OreType.KarmaStone;
		}

		// 界匣晶 [天]：古代跨界传送门与空间奇点
		if (relicPower > 0.90f && (boundary == PlateBoundaryType.Transform || elevation <= seaLevel))
		{
			return OreType.RealmCasketCrystal;
		}

		// ──────── 地阶卡牌资源 ────────
		// 定序金 [地]：古代超级文明高阶工坊与炼金核心
		if (relicPower > 0.85f && leyline > 0.55f)
		{
			return OreType.OrderedGold;
		}

		// 共鸣晶 [地]：圣殿遗址、英雄誓约之所
		if (relicPower > 0.79f)
		{
			return OreType.ResonanceCrystal;
		}

		// 回响石 [地]：古代机关防御设施与工厂遗迹
		if (relicPower > 0.73f)
		{
			return OreType.EchoStone;
		}

		// ──────── 灵阶卡牌资源 ────────
		// 灵纹矿 [灵]：宗门法师塔与法阵节点（灵脉与遗迹交汇）
		if (leyline > 0.50f)
		{
			return OreType.RuneOre;
		}

		// 忆晶砂 [灵]：古代城市、学院与知识图书馆遗存
		return OreType.MemorySand;
	}

	/// <summary>
	/// 裁定多层资源下的主显矿产（Dominant Ore）：
	/// 天阶（S级）必定霸屏彰显奇观；地阶（A级）优先超凡与卡牌；灵阶次之；凡阶工业作为大地背景底色。
	/// </summary>
	private static OreType ResolveDominantOre(OreType industrial, OreType supernatural, OreType card)
	{
		var indTier = industrial.GetTier();
		var supTier = supernatural.GetTier();
		var crdTier = card.GetTier();

		var maxTier = (ResourceTier)Math.Max((byte)indTier, Math.Max((byte)supTier, (byte)crdTier));
		if (maxTier == ResourceTier.None)
		{
			return OreType.None;
		}

		// 天阶（S级）奇观绝对优先
		if (supTier == ResourceTier.Tian) return supernatural;
		if (crdTier == ResourceTier.Tian) return card;

		// 地阶（A级）高阶异象优先
		if (supTier == ResourceTier.Di) return supernatural;
		if (crdTier == ResourceTier.Di) return card;
		if (indTier == ResourceTier.Di) return industrial;

		// 灵阶（B级）
		if (supTier == ResourceTier.Ling) return supernatural;
		if (crdTier == ResourceTier.Ling) return card;
		if (indTier == ResourceTier.Ling) return industrial;

		// 凡阶（C/D级）基础工业
		if (indTier == ResourceTier.Fan) return industrial;

		return OreType.None;
	}

	/// <summary>
	/// 生成灵脉网络流向场与汇聚强度（0.0 ~ 1.0）。
	/// </summary>
	private static float[,] GenerateLeylineField(ResourceContext ctx)
	{
		var width = ctx.Width;
		var height = ctx.Height;
		var field = new float[width, height];
		var seed = ctx.Seed;

		Parallel.For(0, height, y =>
		{
			var leylineNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x3d7b819f),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
				FractalOctaves = 4,
				Frequency = 0.28f
			};

			var branchNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x9e3779b9),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 3,
				Frequency = 0.55f
			};

			for (var x = 0; x < width; x++)
			{
				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
				var ny = 4f * y / height;

				var ridge = (leylineNoise.GetNoise3D(nx, ny, nz) + 1f) * 0.5f;
				var branch = (branchNoise.GetNoise3D(nx * 2f, ny * 2f, nz * 2f) + 1f) * 0.5f;

				var elev = ctx.Elevation[x, y];
				var boundary = ctx.Boundaries != null ? ctx.Boundaries[x, y] : PlateBoundaryType.None;

				// 灵脉发源于板块边界构造带与高山深层
				var structuralBonus = (boundary != PlateBoundaryType.None ? 0.25f : 0f) + Math.Max(elev - ctx.SeaLevel, 0f) * 0.35f;
				var combined = ridge * 0.60f + branch * 0.25f + structuralBonus * 0.35f;

				field[x, y] = Math.Clamp(combined, 0f, 1f);
			}
		});

		return field;
	}

	/// <summary>
	/// 生成工业矿脉有向连续矿带场（0.0 ~ 1.0）。
	/// </summary>
	private static float[,] GenerateVeinNoiseField(ResourceContext ctx)
	{
		var width = ctx.Width;
		var height = ctx.Height;
		var field = new float[width, height];
		var seed = ctx.Seed;

		Parallel.For(0, height, y =>
		{
			var veinNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x517cc1b7),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
				FractalOctaves = 4,
				Frequency = 0.42f
			};

			for (var x = 0; x < width; x++)
			{
				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
				var ny = 4f * y / height;

				var val = (veinNoise.GetNoise3D(nx * 1.5f, ny * 1.5f, nz * 1.5f) + 1f) * 0.5f;
				field[x, y] = Math.Clamp(val, 0f, 1f);
			}
		});

		return field;
	}

	/// <summary>
	/// 生成文明遗迹与卡牌历史影响度场（0.0 ~ 1.0）。
	/// </summary>
	private static float[,] GenerateRelicField(ResourceContext ctx)
	{
		var width = ctx.Width;
		var height = ctx.Height;
		var field = new float[width, height];
		var seed = ctx.Seed;

		Parallel.For(0, height, y =>
		{
			var histNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x27d4eb2f),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
				CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Manhattan,
				CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Div,
				Frequency = 0.38f
			};

			for (var x = 0; x < width; x++)
			{
				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
				var ny = 4f * y / height;

				var val = (histNoise.GetNoise3D(nx, ny, nz) + 1f) * 0.5f;
				field[x, y] = Math.Clamp(val, 0f, 1f);
			}
		});

		// 若存在聚落城镇，给周边增添文明历史富集辐射
		if (ctx.Cities != null && ctx.Cities.Count > 0)
		{
			var radius = Math.Max(width / 32, 4);
			var rSq = radius * radius;

			foreach (var city in ctx.Cities)
			{
				var cx = city.Position.X;
				var cy = city.Position.Y;

				for (var dy = -radius; dy <= radius; dy++)
				{
					var py = cy + dy;
					if (py < 0 || py >= height) continue;

					for (var dx = -radius; dx <= radius; dx++)
					{
						var dSq = dx * dx + dy * dy;
						if (dSq > rSq) continue;

						var px = (cx + dx + width) % width;
						var falloff = 1f - (float)Math.Sqrt(dSq) / radius;
						field[px, py] = Math.Clamp(field[px, py] + falloff * 0.35f, 0f, 1f);
					}
				}
			}
		}

		return field;
	}

	private static float EstimateTemperature(int y, int height, float elevation, float seaLevel)
	{
		var lat = Math.Abs((y / (float)height) - 0.5f) * 2f;
		var temp = 1f - lat;
		var lapse = Math.Max(elevation - seaLevel, 0f) * 0.5f;
		return Math.Clamp(temp - lapse, 0f, 1f);
	}

	private static int AnomalyBin(float anomaly) => Math.Min((int)(anomaly * AnomalyBins), AnomalyBins - 1);

	private static int[] BuildLandRank(
		float[] anomaly,
		float[,] elevation,
		int width,
		int height,
		float seaLevel,
		out int landPixels)
	{
		var rank = new int[AnomalyBins];
		landPixels = 0;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				// 包括陆地与浅海大陆架
				if (elevation[x, y] <= seaLevel - 0.12f)
				{
					continue;
				}

				rank[AnomalyBin(anomaly[y * width + x])]++;
				landPixels++;
			}
		}

		var running = 0;
		for (var b = 0; b < AnomalyBins; b++)
		{
			running += rank[b];
			rank[b] = running;
		}

		return rank;
	}

	private static int FindPercentileBin(int[] rank, int landPixels, double coverage)
	{
		var target = landPixels * (1.0 - coverage);
		for (var b = 0; b < AnomalyBins; b++)
		{
			if (rank[b] >= target)
			{
				return b;
			}
		}

		return AnomalyBins - 1;
	}
}
