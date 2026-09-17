using Godot;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen;

public sealed class CivilizationEpochEvent
{
    public required int Epoch { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required int ImpactLevel { get; init; }
}

public sealed class CivilizationSimulationResult
{
    public required float[,] Influence { get; init; }
    public required int[,] PolityId { get; init; }
    public required bool[,] BorderMask { get; init; }
    public required bool[,] TradeRouteMask { get; init; }
    public required float[,] TradeFlow { get; init; }
    public required int PolityCount { get; init; }
    public required int HamletCount { get; init; }
    public required int TownCount { get; init; }
    public required int CityStateCount { get; init; }
    public required int TradeRouteCells { get; init; }
    public required float ControlledLandPercent { get; init; }
    public required float CoreCellPercent { get; init; }
    public required float DominantPolitySharePercent { get; init; }
    public required float ConnectedHubPercent { get; init; }
    public required float ConflictHeatPercent { get; init; }
    public required float AllianceCohesionPercent { get; init; }
    public required float BorderVolatilityPercent { get; init; }
    public required CivilizationEpochEvent[] RecentEvents { get; init; }
}

public sealed class CivilizationSimulator
{
    private enum PolityArchetype
    {
        Generic,
        Naval,
        River,
        Highland,
        Nomadic
    }

    private readonly struct PolitySeed
    {
        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public float Strength { get; }
        public float Expansionism { get; }
        public BiomeType NativeBiome { get; }
        public PolityArchetype Archetype { get; }

        public PolitySeed(int id, int x, int y, float strength, float expansionism, BiomeType nativeBiome, PolityArchetype archetype)
        {
            Id = id;
            X = x;
            Y = y;
            Strength = strength;
            Expansionism = expansionism;
            NativeBiome = nativeBiome;
            Archetype = archetype;
        }
    }

    private readonly struct CityHub
    {
        public int X { get; }
        public int Y { get; }
        public int Tier { get; }
        public int PolityId { get; }
        public float Strength { get; }

        public CityHub(int x, int y, int tier, int polityId, float strength)
        {
            X = x;
            Y = y;
            Tier = tier;
            PolityId = polityId;
            Strength = strength;
        }
    }

    public CivilizationSimulationResult Simulate(
        int width,
        int height,
        int seed,
        int epoch,
        int civilAggression,
        int speciesDiversity,
        float seaLevel,
        float[,] elevation,
        float[,] river,
        BiomeType[,] biome,
        List<CityInfo> cities,
        float[,] civilizationPotential)
    {
        var influence = new float[width, height];
        var polityIdMap = new int[width, height];
        var borderMask = new bool[width, height];
        var tradeRouteMask = new bool[width, height];
        var tradeFlow = new float[width, height];

        var aggressionNorm = Mathf.Clamp(civilAggression / 100f, 0f, 1f);
        var diversityNorm = Mathf.Clamp(speciesDiversity / 100f, 0f, 1f);
        var epochFactor = 1f - Mathf.Exp(-Mathf.Max(epoch, 0) * (0.008f + 0.012f * diversityNorm));
        var expansionRange = Mathf.Lerp(10f, 48f, epochFactor) * Mathf.Lerp(0.82f, 1.2f, diversityNorm);
        var claimThreshold = Mathf.Lerp(0.20f, 0.34f, aggressionNorm);

        var seeds = BuildSeeds(width, height, seed, seaLevel, elevation, river, biome, cities, civilizationPotential, diversityNorm, aggressionNorm);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                polityIdMap[x, y] = -1;
                if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
                {
                    continue;
                }

                var potential = Mathf.Clamp(civilizationPotential[x, y], 0f, 1f);
                var riverFactor = Mathf.Clamp(Mathf.Sqrt(Mathf.Clamp(river[x, y], 0f, 1f)), 0f, 1f);
                var terrainPenalty = Mathf.Clamp((elevation[x, y] - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
                var localSupport = Mathf.Clamp(potential * 0.72f + riverFactor * 0.28f, 0f, 1f);
                var localCoastal = IsAdjacentToOcean(x, y, width, height, biome);
                var localBiome = biome[x, y];

                var bestScore = 0f;
                var bestPolity = -1;

                for (var i = 0; i < seeds.Count; i++)
                {
                    var polity = seeds[i];
                    var dx = Math.Abs(x - polity.X);
                    dx = Math.Min(dx, width - dx);
                    var dy = Math.Abs(y - polity.Y);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    var effectiveRange = expansionRange * polity.Strength * polity.Expansionism;
                    var distanceFactor = 1f / (1f + Mathf.Pow(distance / Mathf.Max(effectiveRange, 0.0001f), 1.35f) * 2.8f);
                    var terrainCost = ComputeExpansionPenalty(
                        polity.Archetype,
                        polity.NativeBiome,
                        localBiome,
                        terrainPenalty,
                        riverFactor,
                        localCoastal);
                    var terrainAdaptation = 1f / (1f + terrainCost * 0.55f);
                    var nativeBiomeBonus = polity.NativeBiome == localBiome ? 1.07f : 1f;

                    var score = localSupport
                        * polity.Strength
                        * terrainAdaptation
                        * nativeBiomeBonus
                        * distanceFactor
                        * (1f - terrainPenalty * 0.42f)
                        * (0.85f + 0.15f * epochFactor)
                        * (0.72f + 0.28f * (1f - aggressionNorm * 0.45f));

                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestPolity = polity.Id;
                }

                var finalInfluence = Mathf.Clamp(bestScore, 0f, 1f);
                influence[x, y] = finalInfluence;
                if (bestPolity >= 0 && finalInfluence >= claimThreshold)
                {
                    polityIdMap[x, y] = bestPolity;
                }
            }
        }

        ApplyEpochDynamics(
            width,
            height,
            seed,
            epoch,
            aggressionNorm,
            diversityNorm,
            seaLevel,
            elevation,
            biome,
            influence,
            polityIdMap,
            out var conflictHeatPercent,
            out var allianceCohesionPercent,
            out var borderVolatilityPercent);

        var polityCellCount = new Dictionary<int, int>();
        var landCells = 0;
        var claimedCells = 0;
        var coreCells = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
                {
                    continue;
                }

                landCells++;
                var polityId = polityIdMap[x, y];
                if (polityId < 0)
                {
                    continue;
                }

                claimedCells++;
                if (influence[x, y] >= 0.64f)
                {
                    coreCells++;
                }

                if (!polityCellCount.TryAdd(polityId, 1))
                {
                    polityCellCount[polityId]++;
                }

                if (HasForeignNeighbor(x, y, polityId, polityIdMap, width, height))
                {
                    borderMask[x, y] = true;
                }
            }
        }

        var maxPolityCells = 0;
        foreach (var pair in polityCellCount)
        {
            if (pair.Value > maxPolityCells)
            {
                maxPolityCells = pair.Value;
            }
        }

        var hubs = BuildCityHubs(
            width,
            height,
            seaLevel,
            elevation,
            biome,
            cities,
            influence,
            polityIdMap,
            epochFactor,
            diversityNorm,
            aggressionNorm,
            out var hamletCount,
            out var townCount,
            out var cityStateCount);

        var connectedHubPercent = BuildTradeRoutes(
            width,
            height,
            seaLevel,
            elevation,
            biome,
            epochFactor,
            aggressionNorm,
            hubs,
            tradeRouteMask,
            tradeFlow);

        var tradeRouteCells = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (tradeRouteMask[x, y])
                {
                    tradeRouteCells++;
                }
            }
        }

        var recentEvents = BuildEpochEventLog(
            seed,
            epoch,
            aggressionNorm,
            diversityNorm,
            conflictHeatPercent,
            allianceCohesionPercent,
            borderVolatilityPercent,
            polityCellCount.Count,
            connectedHubPercent);

        return new CivilizationSimulationResult
        {
            Influence = influence,
            PolityId = polityIdMap,
            BorderMask = borderMask,
            TradeRouteMask = tradeRouteMask,
            TradeFlow = tradeFlow,
            PolityCount = polityCellCount.Count,
            HamletCount = hamletCount,
            TownCount = townCount,
            CityStateCount = cityStateCount,
            TradeRouteCells = tradeRouteCells,
            ControlledLandPercent = landCells > 0 ? 100f * claimedCells / landCells : 0f,
            CoreCellPercent = landCells > 0 ? 100f * coreCells / landCells : 0f,
            DominantPolitySharePercent = claimedCells > 0 ? 100f * maxPolityCells / claimedCells : 0f,
            ConnectedHubPercent = connectedHubPercent,
            ConflictHeatPercent = conflictHeatPercent,
            AllianceCohesionPercent = allianceCohesionPercent,
            BorderVolatilityPercent = borderVolatilityPercent,
            RecentEvents = recentEvents
        };
    }

    private static CivilizationEpochEvent[] BuildEpochEventLog(
        int seed,
        int epoch,
        float aggressionNorm,
        float diversityNorm,
        float conflictHeatPercent,
        float allianceCohesionPercent,
        float borderVolatilityPercent,
        int polityCount,
        float connectedHubPercent)
    {
        if (epoch <= 0)
        {
            return Array.Empty<CivilizationEpochEvent>();
        }

        const int lookback = 6;
        var startEpoch = Math.Max(1, epoch - lookback + 1);
        var events = new List<CivilizationEpochEvent>(lookback);

        for (var currentEpoch = startEpoch; currentEpoch <= epoch; currentEpoch++)
        {
            var phase = (currentEpoch - startEpoch) / (float)Math.Max(1, epoch - startEpoch);
            var noise = HashNoise01(seed ^ unchecked((int)0x27d4eb2d), currentEpoch, polityCount + Mathf.RoundToInt(connectedHubPercent));

            var warScore = aggressionNorm * 0.58f
                + conflictHeatPercent / 170f
                + borderVolatilityPercent / 230f
                + noise * 0.18f;
            var allianceScore = (1f - aggressionNorm) * (0.50f + diversityNorm * 0.24f)
                + allianceCohesionPercent / 180f
                + (1f - noise) * 0.12f;
            var tradeScore = connectedHubPercent / 150f
                + (1f - aggressionNorm) * 0.20f
                + phase * 0.16f
                + noise * 0.06f;

            string category;
            string summary;
            float dominantScore;

            if (warScore >= allianceScore && warScore >= tradeScore)
            {
                category = "战争";
                dominantScore = warScore;
                summary = warScore > 0.95f
                    ? "边境冲突升级，多处要塞易手。"
                    : "边境摩擦加剧，前线发生局部推进。";
            }
            else if (allianceScore >= tradeScore)
            {
                category = "联盟";
                dominantScore = allianceScore;
                summary = allianceScore > 0.90f
                    ? "多政体缔结互保公约，边境趋稳。"
                    : "区域协约扩张，防务协同增强。";
            }
            else
            {
                category = "贸易";
                dominantScore = tradeScore;
                summary = tradeScore > 0.92f
                    ? "贸易走廊扩容，跨域物资流显著增长。"
                    : "商路维持畅通，城镇交换网络稳步扩张。";
            }

            var impact = Mathf.Clamp(Mathf.RoundToInt(1f + dominantScore * 3.8f + (noise - 0.5f) * 1.2f), 1, 5);

            events.Add(new CivilizationEpochEvent
            {
                Epoch = currentEpoch,
                Category = category,
                Summary = summary,
                ImpactLevel = impact
            });
        }

        return events.ToArray();
    }

    private static void ApplyEpochDynamics(
        int width,
        int height,
        int seed,
        int epoch,
        float aggressionNorm,
        float diversityNorm,
        float seaLevel,
        float[,] elevation,
        BiomeType[,] biome,
        float[,] influence,
        int[,] polityIdMap,
        out float conflictHeatPercent,
        out float allianceCohesionPercent,
        out float borderVolatilityPercent)
    {
        var epochFactor = 1f - Mathf.Exp(-Mathf.Max(epoch, 0) * 0.0095f);
        var turns = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 4f, epochFactor)), 1, 4);

        double conflictAccum = 0d;
        double allianceAccum = 0d;
        var borderSampleCount = 0;
        var changedEvents = 0;

        for (var turn = 0; turn < turns; turn++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
                    {
                        continue;
                    }

                    var polity = polityIdMap[x, y];
                    if (polity < 0)
                    {
                        continue;
                    }

                    var sameCount = 0;
                    var foreignCount = 0;
                    var bestForeignPolity = -1;
                    var bestForeignInfluence = 0f;

                    for (var dir = 0; dir < 4; dir++)
                    {
                        var nx = x;
                        var ny = y;

                        if (dir == 0)
                        {
                            nx = x == 0 ? width - 1 : x - 1;
                        }
                        else if (dir == 1)
                        {
                            nx = x == width - 1 ? 0 : x + 1;
                        }
                        else if (dir == 2)
                        {
                            ny = y - 1;
                            if (ny < 0)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            ny = y + 1;
                            if (ny >= height)
                            {
                                continue;
                            }
                        }

                        var neighborPolity = polityIdMap[nx, ny];
                        if (neighborPolity < 0)
                        {
                            continue;
                        }

                        if (neighborPolity == polity)
                        {
                            sameCount++;
                            continue;
                        }

                        foreignCount++;
                        var neighborInfluence = influence[nx, ny];
                        if (neighborInfluence > bestForeignInfluence)
                        {
                            bestForeignInfluence = neighborInfluence;
                            bestForeignPolity = neighborPolity;
                        }
                    }

                    if (foreignCount == 0)
                    {
                        continue;
                    }

                    borderSampleCount++;
                    var borderPressure = foreignCount / (float)(sameCount + foreignCount);
                    var noise = HashNoise01(seed ^ unchecked((turn + 1) * 0x45d9f3b), x, y);

                    var localConflict = aggressionNorm
                        * (0.68f + 0.42f * noise)
                        * (0.55f + 0.45f * epochFactor)
                        * borderPressure;

                    var localAlliance = (1f - aggressionNorm)
                        * (0.46f + 0.54f * diversityNorm)
                        * (0.74f + 0.26f * (1f - noise))
                        * (1f - borderPressure * 0.58f);

                    conflictAccum += localConflict;
                    allianceAccum += localAlliance;

                    var currentInfluence = influence[x, y];
                    var nextInfluence = Mathf.Clamp(
                        currentInfluence
                        + localAlliance * 0.12f
                        - localConflict * 0.16f,
                        0f,
                        1f);

                    var conquestGap = Mathf.Lerp(0.09f, 0.04f, aggressionNorm);
                    if (bestForeignPolity >= 0 && bestForeignInfluence > nextInfluence + conquestGap)
                    {
                        if (localConflict > localAlliance * 0.72f)
                        {
                            polityIdMap[x, y] = bestForeignPolity;
                            nextInfluence = Mathf.Clamp(bestForeignInfluence * 0.90f, 0f, 1f);
                            changedEvents++;
                        }
                    }
                    else if (nextInfluence < 0.16f && borderPressure > 0.64f && localConflict > 0.18f)
                    {
                        polityIdMap[x, y] = -1;
                        nextInfluence *= 0.72f;
                        changedEvents++;
                    }

                    influence[x, y] = nextInfluence;
                }
            }
        }

        conflictHeatPercent = borderSampleCount > 0
            ? Mathf.Clamp((float)(100d * conflictAccum / borderSampleCount), 0f, 100f)
            : 0f;
        allianceCohesionPercent = borderSampleCount > 0
            ? Mathf.Clamp((float)(100d * allianceAccum / borderSampleCount), 0f, 100f)
            : 0f;
        borderVolatilityPercent = borderSampleCount > 0
            ? Mathf.Clamp(100f * changedEvents / borderSampleCount, 0f, 100f)
            : 0f;
    }

    private static List<PolitySeed> BuildSeeds(
        int width,
        int height,
        int seed,
        float seaLevel,
        float[,] elevation,
        float[,] river,
        BiomeType[,] biome,
        List<CityInfo> cities,
        float[,] civilizationPotential,
        float diversityNorm,
        float aggressionNorm)
    {
        var seeds = new List<PolitySeed>();
        var seen = new HashSet<int>();
        var nextId = 0;

        var weightedCities = new List<(CityInfo City, float Importance)>(cities.Count);
        for (var i = 0; i < cities.Count; i++)
        {
            var city = cities[i];
            var x = Mathf.Clamp(city.Position.X, 0, width - 1);
            var y = Mathf.Clamp(city.Position.Y, 0, height - 1);
            if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
            {
                continue;
            }

            var populationWeight = city.Population switch
            {
                CityPopulation.Small => 0.78f,
                CityPopulation.Medium => 1.00f,
                _ => 1.22f
            };
            var importance = populationWeight * 0.70f + Mathf.Clamp(city.Score, 0f, 1f) * 0.30f;
            weightedCities.Add((city, importance));
        }

        weightedCities.Sort((left, right) => right.Importance.CompareTo(left.Importance));

        var cityBudget = Mathf.Clamp(
            4 + Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(weightedCities.Count, 1)) * 1.4f + diversityNorm * 6f - aggressionNorm * 1.2f),
            4,
            22);
        cityBudget = Math.Min(cityBudget, weightedCities.Count);

        if (cityBudget > 0)
        {
            var spacing = Mathf.Max(6f, (width + height) * 0.5f / MathF.Max(2.2f * MathF.Sqrt(cityBudget), 1f));
            var minSpacing = Mathf.Max(3f, spacing * 0.45f);

            while (seeds.Count < cityBudget && spacing >= minSpacing)
            {
                var addedThisRound = false;
                for (var i = 0; i < weightedCities.Count && seeds.Count < cityBudget; i++)
                {
                    var city = weightedCities[i].City;
                    var x = Mathf.Clamp(city.Position.X, 0, width - 1);
                    var y = Mathf.Clamp(city.Position.Y, 0, height - 1);
                    var key = y * width + x;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var farEnough = true;
                    for (var j = 0; j < seeds.Count; j++)
                    {
                        if (ToroidalDistance(width, x, y, seeds[j].X, seeds[j].Y) < spacing)
                        {
                            farEnough = false;
                            break;
                        }
                    }

                    if (!farEnough)
                    {
                        seen.Remove(key);
                        continue;
                    }

                    var archetype = DeterminePolityArchetype(x, y, width, height, seaLevel, elevation, river, biome);
                    var expansionism = archetype switch
                    {
                        PolityArchetype.Naval => 1.18f,
                        PolityArchetype.River => 1.10f,
                        PolityArchetype.Highland => 0.92f,
                        PolityArchetype.Nomadic => 1.12f,
                        _ => 1.00f
                    };

                    var cityStrength = city.Population switch
                    {
                        CityPopulation.Small => 0.86f,
                        CityPopulation.Medium => 1.02f,
                        _ => 1.24f
                    };
                    cityStrength += Mathf.Clamp(city.Score, 0f, 1f) * 0.24f;
                    cityStrength = Mathf.Clamp(cityStrength, 0.72f, 1.58f);

                    seeds.Add(new PolitySeed(nextId++, x, y, cityStrength, expansionism, biome[x, y], archetype));
                    addedThisRound = true;
                }

                spacing *= addedThisRound ? 0.90f : 0.76f;
            }
        }

        var fallbackBudget = Mathf.Clamp(6 + Mathf.RoundToInt(diversityNorm * 12f), 6, 20);
        var strideX = Math.Max(1, width / 18);
        var strideY = Math.Max(1, height / 10);
        for (var y = 0; y < height && seeds.Count < fallbackBudget; y += strideY)
        {
            for (var x = 0; x < width && seeds.Count < fallbackBudget; x += strideX)
            {
                if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
                {
                    continue;
                }

                if (civilizationPotential[x, y] < 0.62f)
                {
                    continue;
                }

                var key = y * width + x;
                if (!seen.Add(key))
                {
                    continue;
                }

                var jitter = HashNoise01(seed, x, y);
                var strength = Mathf.Clamp(0.86f + jitter * 0.36f - aggressionNorm * 0.08f, 0.72f, 1.34f);
                var archetype = DeterminePolityArchetype(x, y, width, height, seaLevel, elevation, river, biome);
                var expansionism = archetype switch
                {
                    PolityArchetype.Naval => 1.16f,
                    PolityArchetype.River => 1.08f,
                    PolityArchetype.Highland => 0.94f,
                    PolityArchetype.Nomadic => 1.12f,
                    _ => 1.00f
                };
                seeds.Add(new PolitySeed(nextId++, x, y, strength, expansionism, biome[x, y], archetype));
            }
        }

        if (seeds.Count > 0)
        {
            return seeds;
        }

        var centerX = width / 2;
        var centerY = height / 2;
        var centerBiome = biome[Mathf.Clamp(centerX, 0, width - 1), Mathf.Clamp(centerY, 0, height - 1)];
        seeds.Add(new PolitySeed(0, centerX, centerY, 1f, 1f, centerBiome, PolityArchetype.Generic));
        return seeds;
    }

    private static List<CityHub> BuildCityHubs(
        int width,
        int height,
        float seaLevel,
        float[,] elevation,
        BiomeType[,] biome,
        List<CityInfo> cities,
        float[,] influence,
        int[,] polityIdMap,
        float epochFactor,
        float diversityNorm,
        float aggressionNorm,
        out int hamletCount,
        out int townCount,
        out int cityStateCount)
    {
        var hubs = new List<CityHub>();
        hamletCount = 0;
        townCount = 0;
        cityStateCount = 0;

        for (var i = 0; i < cities.Count; i++)
        {
            var city = cities[i];
            var x = Mathf.Clamp(city.Position.X, 0, width - 1);
            var y = Mathf.Clamp(city.Position.Y, 0, height - 1);
            if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
            {
                continue;
            }

            var localInfluence = Mathf.Clamp(influence[x, y], 0f, 1f);
            var polityId = polityIdMap[x, y];
            var populationBase = city.Population switch
            {
                CityPopulation.Small => 0.26f,
                CityPopulation.Medium => 0.50f,
                _ => 0.74f
            };

            var tierScore = localInfluence * 0.88f
                + Mathf.Clamp(city.Score, 0f, 1f) * 0.36f
                + epochFactor * 0.58f
                + diversityNorm * 0.24f
                + populationBase
                - aggressionNorm * 0.20f;

            if (polityId < 0)
            {
                tierScore -= 0.12f;
            }

            var tier = tierScore switch
            {
                < 1.05f => 0,
                < 1.58f => 1,
                _ => 2
            };

            if (tier == 0)
            {
                hamletCount++;
            }
            else if (tier == 1)
            {
                townCount++;
            }
            else
            {
                cityStateCount++;
            }

            var strength = Mathf.Clamp(0.72f + tier * 0.30f + localInfluence * 0.42f + populationBase * 0.18f, 0.65f, 1.75f);
            hubs.Add(new CityHub(x, y, tier, polityId, strength));
        }

        hubs.Sort((left, right) => right.Strength.CompareTo(left.Strength));
        return hubs;
    }

    private static float BuildTradeRoutes(
        int width,
        int height,
        float seaLevel,
        float[,] elevation,
        BiomeType[,] biome,
        float epochFactor,
        float aggressionNorm,
        List<CityHub> hubs,
        bool[,] tradeRouteMask,
        float[,] tradeFlow)
    {
        if (hubs.Count < 2)
        {
            return 0f;
        }

        var maxLinksPerHub = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, epochFactor) - aggressionNorm * 0.6f), 1, 3);
        var baseRange = Mathf.Lerp(16f, 58f, epochFactor);

        var linkedPairs = new HashSet<long>();
        var connectedHub = new bool[hubs.Count];

        for (var i = 0; i < hubs.Count; i++)
        {
            var fromHub = hubs[i];
            var links = 0;

            for (var attempt = 0; attempt < 8 && links < maxLinksPerHub; attempt++)
            {
                var bestIndex = -1;
                var bestDistance = float.MaxValue;

                for (var j = 0; j < hubs.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    var toHub = hubs[j];
                    var samePolity = fromHub.PolityId >= 0 && fromHub.PolityId == toHub.PolityId;
                    var diplomaticBridge = fromHub.Tier >= 2 || toHub.Tier >= 2 || aggressionNorm < 0.55f;
                    if (!samePolity && !diplomaticBridge)
                    {
                        continue;
                    }

                    var pairKey = BuildPairKey(i, j);
                    if (linkedPairs.Contains(pairKey))
                    {
                        continue;
                    }

                    var distance = ToroidalDistance(width, fromHub.X, fromHub.Y, toHub.X, toHub.Y);
                    var maxDistance = baseRange * Mathf.Lerp(0.92f, 1.35f, (fromHub.Tier + toHub.Tier) * 0.25f);
                    if (distance > maxDistance)
                    {
                        continue;
                    }

                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestIndex = j;
                }

                if (bestIndex < 0)
                {
                    break;
                }

                linkedPairs.Add(BuildPairKey(i, bestIndex));
                var toHubSelected = hubs[bestIndex];
                var drewAny = RasterizeTradeRoute(
                    width,
                    height,
                    seaLevel,
                    elevation,
                    biome,
                    fromHub,
                    toHubSelected,
                    tradeRouteMask,
                    tradeFlow);

                if (drewAny)
                {
                    connectedHub[i] = true;
                    connectedHub[bestIndex] = true;
                }

                links++;
            }
        }

        var connectedCount = 0;
        for (var i = 0; i < connectedHub.Length; i++)
        {
            if (connectedHub[i])
            {
                connectedCount++;
            }
        }

        return hubs.Count > 0 ? 100f * connectedCount / hubs.Count : 0f;
    }

    private static bool RasterizeTradeRoute(
        int width,
        int height,
        float seaLevel,
        float[,] elevation,
        BiomeType[,] biome,
        CityHub fromHub,
        CityHub toHub,
        bool[,] tradeRouteMask,
        float[,] tradeFlow)
    {
        var dx = toHub.X - fromHub.X;
        if (Math.Abs(dx) > width / 2)
        {
            dx += dx > 0 ? -width : width;
        }

        var dy = toHub.Y - fromHub.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        steps = Math.Max(steps, 1);

        var drewAny = false;
        for (var step = 0; step <= steps; step++)
        {
            var t = step / (float)steps;
            var xFloat = fromHub.X + dx * t;
            var yFloat = fromHub.Y + dy * t;

            var x = WrapX(Mathf.RoundToInt(xFloat), width);
            var y = Mathf.RoundToInt(yFloat);
            if (y < 0 || y >= height)
            {
                continue;
            }

            if (elevation[x, y] <= seaLevel || biome[x, y] == BiomeType.Ocean || biome[x, y] == BiomeType.ShallowOcean)
            {
                continue;
            }

            var corridorStrength = Mathf.Clamp(0.35f + fromHub.Strength * 0.25f + toHub.Strength * 0.25f - Mathf.Abs(t - 0.5f) * 0.32f, 0f, 1f);
            tradeRouteMask[x, y] = true;
            tradeFlow[x, y] = Mathf.Max(tradeFlow[x, y], corridorStrength);
            drewAny = true;
        }

        return drewAny;
    }

    private static int WrapX(int x, int width)
    {
        if (x >= 0)
        {
            return x % width;
        }

        var wrapped = x % width;
        return wrapped == 0 ? 0 : wrapped + width;
    }

    private static float ToroidalDistance(int width, int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        dx = Math.Min(dx, width - dx);
        var dy = Math.Abs(y1 - y0);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static long BuildPairKey(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static PolityArchetype DeterminePolityArchetype(
        int x,
        int y,
        int width,
        int height,
        float seaLevel,
        float[,] elevation,
        float[,] river,
        BiomeType[,] biome)
    {
        if (elevation[x, y] > seaLevel + 0.36f && !IsAdjacentToRiver(x, y, width, height, river))
        {
            return PolityArchetype.Highland;
        }

        if (river[x, y] > 0.24f || IsAdjacentToRiver(x, y, width, height, river))
        {
            return PolityArchetype.River;
        }

        if (IsAdjacentToOcean(x, y, width, height, biome))
        {
            return PolityArchetype.Naval;
        }

        if (IsAridBiome(biome[x, y]))
        {
            return PolityArchetype.Nomadic;
        }

        return PolityArchetype.Generic;
    }

    private static float ComputeExpansionPenalty(
        PolityArchetype archetype,
        BiomeType nativeBiome,
        BiomeType localBiome,
        float terrainPenalty,
        float riverFactor,
        bool localCoastal)
    {
        var biomePenalty = localBiome == nativeBiome ? 0.08f : 0.24f;
        var terrainCost = archetype switch
        {
            PolityArchetype.Naval => localCoastal ? 0.10f : 0.34f,
            PolityArchetype.River => riverFactor > 0.25f ? 0.06f : 0.20f,
            PolityArchetype.Highland => terrainPenalty > 0.60f ? 0.06f : 0.28f,
            PolityArchetype.Nomadic => IsAridBiome(localBiome) ? 0.10f : 0.30f,
            _ => 0.18f
        };

        return Mathf.Clamp(terrainCost + biomePenalty + terrainPenalty * 0.24f, 0f, 1.2f);
    }

    private static bool IsAdjacentToOcean(int x, int y, int width, int height, BiomeType[,] biome)
    {
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
                if (biome[nx, ny] == BiomeType.Ocean || biome[nx, ny] == BiomeType.ShallowOcean)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAdjacentToRiver(int x, int y, int width, int height, float[,] river)
    {
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
                if (river[nx, ny] > 0.16f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAridBiome(BiomeType biome)
    {
        return biome == BiomeType.Steppe
            || biome == BiomeType.TemperateDesert
            || biome == BiomeType.TropicalDesert;
    }

    private static bool HasForeignNeighbor(int x, int y, int polityId, int[,] polityMap, int width, int height)
    {
        var left = x == 0 ? width - 1 : x - 1;
        var right = x == width - 1 ? 0 : x + 1;
        if (polityMap[left, y] >= 0 && polityMap[left, y] != polityId)
        {
            return true;
        }

        if (polityMap[right, y] >= 0 && polityMap[right, y] != polityId)
        {
            return true;
        }

        if (y > 0 && polityMap[x, y - 1] >= 0 && polityMap[x, y - 1] != polityId)
        {
            return true;
        }

        if (y + 1 < height && polityMap[x, y + 1] >= 0 && polityMap[x, y + 1] != polityId)
        {
            return true;
        }

        return false;
    }

    private static float HashNoise01(int seed, int x, int y)
    {
        uint hash = (uint)seed;
        hash ^= (uint)x * 1597334677u;
        hash ^= (uint)y * 3812015801u;
        hash = (hash ^ (hash >> 16)) * 2246822519u;
        hash ^= hash >> 13;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }
}
