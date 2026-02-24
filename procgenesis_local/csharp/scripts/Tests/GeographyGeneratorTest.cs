using Godot;
using PlanetGeneration.LLM;
using PlanetGeneration.Integration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlanetGeneration.Tests
{
    /// <summary>
    /// 地理文明生成系统的简单测试
    /// </summary>
    public partial class GeographyGeneratorTest : Node
    {
        private LLamaInferenceService _llmService;
        private IntegratedWorldGenerator _generator;

        public override void _Ready()
        {
            GD.Print("开始地理文明生成系统测试...");
            RunTests();
        }

        private async void RunTests()
        {
            try
            {
                // 初始化服务
                _llmService = new LLamaInferenceService();
                _generator = new IntegratedWorldGenerator(_llmService);

                // 测试1: 基础地理内容生成
                GD.Print("\n=== 测试1: 基础地理内容生成 ===");
                await TestBasicGeneration();

                // 测试2: 集成世界生成
                GD.Print("\n=== 测试2: 集成世界生成 ===");
                await TestIntegratedGeneration();

                GD.Print("\n所有测试完成！");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"测试过程中出现错误: {ex.Message}");
                GD.PrintErr(ex.StackTrace);
            }
            finally
            {
                Cleanup();
            }
        }

        private async Task TestBasicGeneration()
        {
            var parameters = new WorldGenerationParameters
            {
                ContinentCount = 3,
                WorldSize = 50000000,
                ClimateDiversity = 70,
                CulturalDiversity = 80
            };

            // 使用回退方案测试（不依赖LLM模型）
            var geoGenerator = new GeographyCivilizationGenerator(_llmService);
            
            // 这里我们直接测试回退方案，因为可能没有模型文件
            GD.Print("使用程序化回退方案生成内容...");
            
            var continents = geoGenerator.GetType()
                .GetMethod("GenerateFallbackContinents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(geoGenerator, new object[] { parameters }) as System.Collections.Generic.List<Continent>;

            if (continents != null && continents.Count > 0)
            {
                GD.Print($"✓ 成功生成 {continents.Count} 个大陆");
                foreach (var continent in continents)
                {
                    GD.Print($"  - {continent.name}: {continent.size}大陆, {continent.climate}气候, {continent.dominantTerrain}地形");
                }
            }
            else
            {
                GD.Print("✗ 大陆生成失败");
            }
        }

        private async Task TestIntegratedGeneration()
        {
            var config = new WorldGenerationConfig
            {
                Parameters = new WorldGenerationParameters
                {
                    ContinentCount = 2,
                    WorldSize = 30000000,
                    ClimateDiversity = 60,
                    CulturalDiversity = 70
                },
                MapWidth = 128,
                MapHeight = 128,
                Seed = 12345,
                Epoch = 50
            };

            try
            {
                var result = await _generator.GenerateIntegratedWorldAsync(config);
                
                GD.Print("✓ 集成世界生成成功");
                GD.Print($"  - 大陆数量: {result.GeographyContent.Continents.Count}");
                GD.Print($"  - 政体数量: {result.CivilizationSimulation.PolityCount}");
                GD.Print($"  - 控制领土: {result.CivilizationSimulation.ControlledLandPercent:F1}%");
                GD.Print($"  - 生成时间: {result.GenerationTimestamp}");

                AssertOrThrow(result.GeographyContent.Continents.Count > 0, "未生成大陆");

                foreach (var continent in result.GeographyContent.Continents)
                {
                    AssertOrThrow(!continent.name.StartsWith("第", StringComparison.Ordinal), $"大陆命名仍为占位格式: {continent.name}");

                    foreach (var country in continent.Countries)
                    {
                        AssertOrThrow(country.ethnicGroups.Count >= 2, $"国家{country.name}文化族群数量不足");
                        AssertOrThrow(country.ethnicGroups.All(g => g.environmentAdaptations.Count > 0), $"国家{country.name}存在缺少环境适应信息的族群");
                        AssertOrThrow(country.Cities.Count >= 2, $"国家{country.name}城市数量不足");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.Print($"✗ 集成生成失败: {ex.Message}");
            }
        }

        private static void AssertOrThrow(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private void Cleanup()
        {
            _llmService?.Dispose();
            GD.Print("测试资源清理完成");
        }

        public override void _Process(double delta)
        {
            // 测试运行完成后退出
            if (Input.IsKeyPressed(Key.Escape))
            {
                GetTree().Quit();
            }
        }
    }
}
