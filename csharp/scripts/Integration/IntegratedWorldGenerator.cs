using Godot;
using PlanetGeneration.LLM;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.Integration
{
    /// <summary>
    /// 将LLM生成的地理文明内容与现有世界生成系统集成
    /// </summary>
    public class IntegratedWorldGenerator
    {
        private readonly LLamaInferenceService _llmService;
        private readonly GeographyCivilizationGenerator _geoGenerator;
        private readonly CivilizationSimulator _civilizationSimulator;

        public IntegratedWorldGenerator(LLamaInferenceService llmService)
        {
            _llmService = llmService;
            _geoGenerator = new GeographyCivilizationGenerator(llmService);
            _civilizationSimulator = new CivilizationSimulator();
        }

        /// <summary>
        /// 生成完整的世界，包括地形和文明内容
        /// </summary>
        public async Task<IntegratedWorldResult> GenerateIntegratedWorldAsync(
            WorldGenerationConfig config,
            CancellationToken ct = default)
        {
            GD.Print("开始生成集成世界...");

            // 1. 生成地理文明内容
            var geoContent = await _geoGenerator.GenerateWorldContentAsync(config.Parameters, ct);
            
            // 2. 基于生成的内容创建地形数据
            var terrainData = GenerateTerrainFromGeoContent(geoContent, config);
            
            // 3. 运行文明模拟器
            var simulationResult = _civilizationSimulator.Simulate(
                terrainData.Width,
                terrainData.Height,
                config.Seed,
                config.Epoch,
                config.CivilAggression,
                config.SpeciesDiversity,
                terrainData.SeaLevel,
                terrainData.Elevation,
                terrainData.River,
                terrainData.Biome,
                terrainData.Cities,
                terrainData.CivilizationPotential
            );

            // 4. 整合所有数据
            var result = new IntegratedWorldResult
            {
                GeographyContent = geoContent,
                TerrainData = terrainData,
                CivilizationSimulation = simulationResult,
                GenerationTimestamp = DateTime.Now
            };

            GD.Print("集成世界生成完成！");
            return result;
        }

        /// <summary>
        /// 根据地理内容生成地形数据
        /// </summary>
        private TerrainData GenerateTerrainFromGeoContent(
            GeneratedWorldContent geoContent, 
            WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            
            var elevation = new float[width, height];
            var river = new float[width, height];
            var biome = new BiomeType[width, height];
            var cities = new List<CityInfo>();
            var civilizationPotential = new float[width, height];

            // 初始化基础地形
            InitializeBaseTerrain(elevation, river, biome, width, height, config.SeaLevel);

            // 根据大陆分布调整地形
            ApplyContinentDistribution(geoContent, elevation, biome, width, height);

            // 根据国家和城市信息放置城市
            PlaceCitiesFromCountries(geoContent, cities, width, height);

            // 计算文明潜力
            CalculateCivilizationPotential(civilizationPotential, elevation, river, biome, width, height, config.SeaLevel);

            return new TerrainData
            {
                Width = width,
                Height = height,
                SeaLevel = config.SeaLevel,
                Elevation = elevation,
                River = river,
                Biome = biome,
                Cities = cities,
                CivilizationPotential = civilizationPotential
            };
        }

        private void InitializeBaseTerrain(
            float[,] elevation, 
            float[,] river, 
            BiomeType[,] biome,
            int width, 
            int height, 
            float seaLevel)
        {
            var rng = new Random();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 生成基础高程噪声
                    var noise = SimplexNoise(x * 0.02f, y * 0.02f, 0) * 0.5f +
                               SimplexNoise(x * 0.05f, y * 0.05f, 1) * 0.3f +
                               SimplexNoise(x * 0.1f, y * 0.1f, 2) * 0.2f;
                    
                    elevation[x, y] = Mathf.Clamp(noise, -0.5f, 1.0f);
                    river[x, y] = Mathf.Clamp(SimplexNoise(x * 0.03f, y * 0.03f, 3), 0f, 1f);
                    biome[x, y] = elevation[x, y] > seaLevel ? BiomeType.Grassland : BiomeType.Ocean;
                }
            }
        }

        private void ApplyContinentDistribution(
            GeneratedWorldContent geoContent,
            float[,] elevation,
            BiomeType[,] biome,
            int width,
            int height)
        {
            var rng = new Random();
            var continentCount = geoContent.Continents.Count;
            
            for (int i = 0; i < continentCount; i++)
            {
                var continent = geoContent.Continents[i];
                var centerX = rng.Next(width);
                var centerY = rng.Next(height);
                var radius = Mathf.Min(width, height) / (4 + continentCount);
                
                // 根据大陆气候调整地形
                ApplyClimateEffects(continent, centerX, centerY, radius, elevation, biome, width, height);
            }
        }

        private void ApplyClimateEffects(
            Continent continent,
            int centerX, 
            int centerY, 
            int radius,
            float[,] elevation,
            BiomeType[,] biome,
            int width, 
            int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var dx = Math.Abs(x - centerX);
                    var dy = Math.Abs(y - centerY);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    if (distance <= radius)
                    {
                        var influence = 1.0f - (distance / radius);
                        
                        // 根据气候类型调整地形
                        switch (continent.climate)
                        {
                            case "热带":
                                elevation[x, y] = Mathf.Max(elevation[x, y], influence * 0.3f);
                                biome[x, y] = BiomeType.TropicalRainForest;
                                break;
                            case "沙漠":
                                elevation[x, y] = Mathf.Max(elevation[x, y], influence * 0.2f);
                                biome[x, y] = BiomeType.TemperateDesert;
                                break;
                            case "寒带":
                                elevation[x, y] = Mathf.Min(elevation[x, y], influence * -0.2f);
                                biome[x, y] = BiomeType.Tundra;
                                break;
                            default:
                                biome[x, y] = BiomeType.Grassland;
                                break;
                        }
                    }
                }
            }
        }

        private void PlaceCitiesFromCountries(
            GeneratedWorldContent geoContent,
            List<CityInfo> cities,
            int width,
            int height)
        {
            var rng = new Random();
            
            foreach (var continent in geoContent.Continents)
            {
                foreach (var country in continent.Countries)
                {
                    foreach (var city in country.Cities)
                    {
                        var x = rng.Next(width);
                        var y = rng.Next(height);
                        var population = city.population;
                        
                        cities.Add(new CityInfo
                        {
                            Position = new Vector2I(x, y),
                            Name = city.name,
                            Population = population > 1000000 ? CityPopulation.Large :
                                       population > 100000 ? CityPopulation.Medium : 
                                       CityPopulation.Small,
                            Score = Mathf.Clamp(population / 10000000.0f, 0f, 1f)
                        });
                    }
                }
            }
        }

        private void CalculateCivilizationPotential(
            float[,] potential,
            float[,] elevation,
            float[,] river,
            BiomeType[,] biome,
            int width,
            int height,
            float seaLevel)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (elevation[x, y] <= seaLevel)
                    {
                        potential[x, y] = 0f;
                        continue;
                    }

                    // 计算文明潜力因子
                    var elevationFactor = 1.0f - Mathf.Clamp(elevation[x, y], 0f, 1f);
                    var riverFactor = Mathf.Clamp(river[x, y], 0f, 1f);
                    var biomeFactor = GetBiomeSuitability(biome[x, y]);
                    
                    potential[x, y] = elevationFactor * 0.4f + riverFactor * 0.4f + biomeFactor * 0.2f;
                }
            }
        }

        private float GetBiomeSuitability(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Grassland => 1.0f,
                BiomeType.TemperateSeasonalForest => 0.9f,
                BiomeType.TropicalRainForest => 0.7f,
                BiomeType.Steppe => 0.8f,
                BiomeType.TemperateDesert => 0.3f,
                BiomeType.Tundra => 0.4f,
                BiomeType.RockyMountain => 0.5f,
                _ => 0.1f
            };
        }

        // 简单的噪声函数（实际项目中应该使用更好的噪声算法）
        private float SimplexNoise(float x, float y, int seed)
        {
            var hash = seed * 73856093 ^ (int)(x * 16777216) ^ (int)(y * 16777216);
            return ((hash & 0x7FFFFFFF) % 1000) / 1000.0f * 2.0f - 1.0f;
        }
    }

    #region 数据结构

    public class WorldGenerationConfig
    {
        public WorldGenerationParameters Parameters { get; set; } = new();
        public int MapWidth { get; set; } = 256;
        public int MapHeight { get; set; } = 256;
        public int Seed { get; set; } = 42;
        public int Epoch { get; set; } = 100;
        public int CivilAggression { get; set; } = 50;
        public int SpeciesDiversity { get; set; } = 70;
        public float SeaLevel { get; set; } = 0.3f;
    }

    public class IntegratedWorldResult
    {
        public GeneratedWorldContent GeographyContent { get; set; }
        public TerrainData TerrainData { get; set; }
        public CivilizationSimulationResult CivilizationSimulation { get; set; }
        public DateTime GenerationTimestamp { get; set; }
    }

    public class TerrainData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public float SeaLevel { get; set; }
        public float[,] Elevation { get; set; }
        public float[,] River { get; set; }
        public BiomeType[,] Biome { get; set; }
        public List<CityInfo> Cities { get; set; }
        public float[,] CivilizationPotential { get; set; }
    }

    #endregion
}