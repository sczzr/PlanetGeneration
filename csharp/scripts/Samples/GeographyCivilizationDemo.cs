using Godot;
using PlanetGeneration.LLM;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.Samples
{
    /// <summary>
    /// 地理文明生成器使用示例
    /// </summary>
    public partial class GeographyCivilizationDemo : Node
    {
        private LLamaInferenceService _llmService;
        private GeographyCivilizationGenerator _generator;
        private CancellationTokenSource _cts;

        public override void _Ready()
        {
            InitializeServices();
            GenerateSampleWorld();
        }

        private void InitializeServices()
        {
            _llmService = new LLamaInferenceService();
            _generator = new GeographyCivilizationGenerator(_llmService);
            _cts = new CancellationTokenSource();
        }

        private async void GenerateSampleWorld()
        {
            try
            {
                GD.Print("开始生成虚构世界内容...");
                
                // 设置生成参数
                var parameters = new WorldGenerationParameters
                {
                    ContinentCount = 4,
                    WorldSize = 150000000,
                    ClimateDiversity = 85,
                    GeologicalActivity = 60,
                    OceanCoverage = 71,
                    PoliticalComplexity = 70,
                    CulturalDiversity = 90
                };

                // 生成世界内容
                var worldContent = await _generator.GenerateWorldContentAsync(parameters, _cts.Token);
                
                DisplayWorldContent(worldContent);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"生成世界内容时出错: {ex.Message}");
            }
        }

        private void DisplayWorldContent(GeneratedWorldContent content)
        {
            GD.Print("=== 生成的虚构世界 ===");
            
            foreach (var continent in content.Continents)
            {
                GD.Print($"\n--- {continent.name} ---");
                GD.Print($"大小: {continent.size}");
                GD.Print($"面积: {continent.area:N0} 平方公里");
                GD.Print($"气候: {continent.climate}");
                GD.Print($"地形: {continent.dominantTerrain}");
                GD.Print($"地质特征: {string.Join(", ", continent.geologicalFeatures)}");
                GD.Print($"自然资源: {string.Join(", ", continent.naturalResources)}");
                GD.Print($"描述: {continent.description}");

                foreach (var country in continent.Countries)
                {
                    GD.Print($"\n  国家: {country.name}");
                    GD.Print($"  首都: {country.capital}");
                    GD.Print($"  政府: {country.government}");
                    GD.Print($"  人口: {country.population:N0}");
                    GD.Print($"  领土: {country.territory}");
                    GD.Print($"  主要资源: {country.primaryResource}");
                    GD.Print($"  军事实力: {country.militaryStrength}");

                    if (country.ethnicGroups.Count > 0)
                    {
                        GD.Print("  文化族群:");
                        foreach (var group in country.ethnicGroups)
                        {
                            GD.Print($"    - {group.name} | 语言: {group.languageFamily} | 生计: {group.livelihood}");
                            GD.Print($"      起源: {group.origin}");
                            GD.Print($"      节庆: {string.Join(", ", group.festivals)}");
                            GD.Print($"      禁忌: {string.Join(", ", group.taboos)}");
                            GD.Print($"      环境适应: {string.Join(", ", group.environmentAdaptations)}");
                        }
                    }

                    foreach (var city in country.Cities)
                    {
                        GD.Print($"    城市: {city.name} ({city.type})");
                        GD.Print($"    人口: {city.population:N0}");
                        GD.Print($"    特色: {city.specialFeature}");
                        GD.Print($"    经济: {city.economy}");
                    }

                    DisplayCivilization(country.Civilization);
                }
            }
        }

        private void DisplayCivilization(Civilization civ)
        {
            GD.Print("    --- 文明特色 ---");
            GD.Print($"    经济基础: {civ.economicBase}");
            GD.Print($"    聚落格局: {civ.settlementPattern}");
            GD.Print($"    风险应对: {civ.riskResponse}");
            GD.Print($"    主要宗教: {civ.religion.mainFaith}");
            GD.Print($"    核心教义: {string.Join(", ", civ.religion.beliefs)}");
            GD.Print($"    日常实践: {string.Join(", ", civ.religion.practices)}");
            
            GD.Print($"    社会结构: {civ.socialStructure.hierarchy}");
            GD.Print($"    家庭结构: {civ.socialStructure.family}");
            GD.Print($"    教育体系: {civ.socialStructure.education}");
            
            GD.Print($"    日常习俗: {string.Join(", ", civ.customs.dailyLife)}");
            GD.Print($"    重要典礼: {string.Join(", ", civ.customs.ceremonies)}");
            GD.Print($"    禁忌事项: {string.Join(", ", civ.customs.taboos)}");
            
            GD.Print($"    艺术形式: {string.Join(", ", civ.artsAndCulture.artForms)}");
            GD.Print($"    文学特色: {civ.artsAndCulture.literature}");
            GD.Print($"    音乐风格: {civ.artsAndCulture.music}");
            
            GD.Print($"    技术专长: {string.Join(", ", civ.technology.specialties)}");
            GD.Print($"    重要发明: {string.Join(", ", civ.technology.innovations)}");
            GD.Print($"    与自然关系: {civ.interactionWithNature}");
        }

        public override void _Process(double delta)
        {
            // 可以在这里添加实时交互逻辑
        }

        public override void _ExitTree()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _llmService?.Dispose();
        }
    }
}
