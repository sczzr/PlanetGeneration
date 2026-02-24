using Godot;
using PlanetGeneration;
using PlanetGeneration.LLM;
using PlanetGeneration.Integration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.UI
{
    /// <summary>
    /// 地理文明生成器的UI控制器
    /// </summary>
    public partial class GeographyGeneratorUI : Control
    {
        [Export] private Button _generateButton;
        [Export] private Button _exportJsonButton;
        [Export] private ProgressBar _progressBar;
        [Export] private Label _statusLabel;
        [Export] private TextEdit _outputText;
        [Export] private FileDialog _saveFileDialog;
        [Export] private SpinBox _continentCountSpinBox;
        [Export] private SpinBox _worldSizeSpinBox;
        [Export] private SpinBox _climateDiversitySpinBox;
        [Export] private SpinBox _cultureDiversitySpinBox;

        private LLamaInferenceService _llmService;
        private IntegratedWorldGenerator _worldGenerator;
        private CancellationTokenSource _cancellationTokenSource;
        private IntegratedWorldResult? _lastResult;

        public override void _Ready()
        {
            ResolveNodeReferences();
            SetupUI();
            InitializeServices();
        }

        private void ResolveNodeReferences()
        {
            _generateButton ??= FindChild("GenerateButton", true, false) as Button;
            _exportJsonButton ??= FindChild("ExportJsonButton", true, false) as Button;
            _progressBar ??= FindChild("ProgressBar", true, false) as ProgressBar;
            _statusLabel ??= FindChild("Status", true, false) as Label;
            _outputText ??= FindChild("OutputText", true, false) as TextEdit;
            _saveFileDialog ??= FindChild("SaveFileDialog", true, false) as FileDialog;
            _continentCountSpinBox ??= FindChild("ContinentSpinBox", true, false) as SpinBox;
            _worldSizeSpinBox ??= FindChild("WorldSizeSpinBox", true, false) as SpinBox;
            _climateDiversitySpinBox ??= FindChild("ClimateSpinBox", true, false) as SpinBox;
            _cultureDiversitySpinBox ??= FindChild("CultureSpinBox", true, false) as SpinBox;
        }

        private void SetupUI()
        {
            if (_generateButton != null)
            {
                _generateButton.Pressed += OnGeneratePressed;
            }

            if (_exportJsonButton != null)
            {
                _exportJsonButton.Pressed += OnExportJsonPressed;
                _exportJsonButton.Disabled = true;
            }

            if (_saveFileDialog != null)
            {
                _saveFileDialog.FileSelected += OnExportFileSelected;
            }

            UpdateStatus("就绪 - 点击生成按钮开始创建世界");
        }

        private void InitializeServices()
        {
            try
            {
                _llmService = new LLamaInferenceService();
                _worldGenerator = new IntegratedWorldGenerator(_llmService);
                _cancellationTokenSource = new CancellationTokenSource();
                
                UpdateStatus("服务初始化完成");
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化失败: {ex.Message}");
            }
        }

        private async void OnGeneratePressed()
        {
            if (!ValidateInputs())
                return;

            try
            {
                ToggleGenerationState(true);
                ClearOutput();
                
                UpdateStatus("正在加载模型...");
                await LoadModelIfNeeded();

                UpdateStatus("开始生成世界...");
                var config = CreateGenerationConfig();
                
                var result = await _worldGenerator.GenerateIntegratedWorldAsync(config, _cancellationTokenSource.Token);
                _lastResult = result;
                DisplayResults(result);
                if (_exportJsonButton != null)
                {
                    _exportJsonButton.Disabled = false;
                }
                UpdateStatus("世界生成完成！");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("生成已取消");
            }
            catch (Exception ex)
            {
                UpdateStatus($"生成失败: {ex.Message}");
                GD.PrintErr(ex);
            }
            finally
            {
                ToggleGenerationState(false);
            }
        }

        private async Task LoadModelIfNeeded()
        {
            if (_llmService.IsLoaded)
            {
                return;
            }

            UpdateStatus("正在寻找并加载语言模型...");

            var runtimeProfile = LlmRuntimeSelector.BuildRuntimeProfile(SystemRequirements.GetTotalMemoryGB());
            var candidates = LlmRuntimeSelector.DiscoverModelCandidates(BuildModelSearchPaths());
            var selected = LlmRuntimeSelector.SelectBestModel(candidates, runtimeProfile);
            if (selected == null)
            {
                UpdateStatus("未找到可用模型，使用回退生成方案");
                return;
            }

            var loaded = await _llmService.LoadModelAsync(
                selected.FullPath,
                runtimeProfile.ToLoadOptions(),
                _cancellationTokenSource.Token);

            if (!loaded)
            {
                UpdateStatus("模型加载失败，使用回退生成方案");
                return;
            }

            var runtimeSummary = LlmRuntimeSelector.BuildProfileSummary(runtimeProfile);
            UpdateStatus($"模型已加载: {selected.FileName} | {runtimeSummary}");
        }

        private IEnumerable<string> BuildModelSearchPaths()
        {
            return new[]
            {
                ProjectSettings.GlobalizePath("res://models"),
                Path.Combine(OS.GetUserDataDir(), "..", "..", "models"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
                "E:\\source\\PlanetGeneration\\procgenesis_local\\csharp\\models\\"
            };
        }

        private WorldGenerationConfig CreateGenerationConfig()
        {
            return new WorldGenerationConfig
            {
                Parameters = new WorldGenerationParameters
                {
                    ContinentCount = (int)_continentCountSpinBox.Value,
                    WorldSize = (long)_worldSizeSpinBox.Value,
                    ClimateDiversity = (int)_climateDiversitySpinBox.Value,
                    CulturalDiversity = (int)_cultureDiversitySpinBox.Value,
                    GeologicalActivity = 50,
                    OceanCoverage = 71,
                    PoliticalComplexity = 60
                },
                MapWidth = 256,
                MapHeight = 256,
                Seed = new Random().Next(),
                Epoch = 100,
                CivilAggression = 50,
                SpeciesDiversity = 70,
                SeaLevel = 0.3f
            };
        }

        private void DisplayResults(IntegratedWorldResult result)
        {
            var output = new System.Text.StringBuilder();
            
            output.AppendLine("=== 世界生成报告 ===");
            output.AppendLine($"生成时间: {result.GenerationTimestamp:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine();

            // 显示地理内容
            output.AppendLine("--- 地理结构 ---");
            foreach (var continent in result.GeographyContent.Continents)
            {
                output.AppendLine($"\n【{continent.name}】");
                output.AppendLine($"  类型: {continent.size}大陆");
                output.AppendLine($"  面积: {continent.area:N0} km²");
                output.AppendLine($"  气候: {continent.climate}");
                output.AppendLine($"  地形: {continent.dominantTerrain}");
                output.AppendLine($"  描述: {continent.description}");

                foreach (var country in continent.Countries)
                {
                    var civilization = country.Civilization ?? new Civilization();
                    output.AppendLine($"\n  ▌{country.name}");
                    output.AppendLine($"    首都: {country.capital}");
                    output.AppendLine($"    政体: {country.government}");
                    output.AppendLine($"    人口: {country.population:N0}");
                    output.AppendLine($"    资源: {country.primaryResource}");

                    if (country.ethnicGroups.Count > 0)
                    {
                        output.AppendLine("    文化族群:");
                        foreach (var group in country.ethnicGroups)
                        {
                            var adaptation = group.environmentAdaptations.Count > 0
                                ? group.environmentAdaptations[0]
                                : "暂无环境适应描述";
                            output.AppendLine($"      - {group.name} | 生计: {group.livelihood} | 适应: {adaptation}");
                        }
                    }

                    foreach (var city in country.Cities)
                    {
                        output.AppendLine($"    • {city.name} ({city.type}) - {city.population:N0}人");
                    }

                    output.AppendLine($"    文明经济: {civilization.economicBase}");
                    output.AppendLine($"    聚落格局: {civilization.settlementPattern}");
                    output.AppendLine($"    风险应对: {civilization.riskResponse}");
                    output.AppendLine($"    自然互动: {civilization.interactionWithNature}");
                }
            }

            // 显示文明模拟结果
            output.AppendLine("\n--- 文明发展状况 ---");
            var sim = result.CivilizationSimulation;
            output.AppendLine($"政体数量: {sim.PolityCount}");
            output.AppendLine($"聚落统计: {sim.HamletCount}村, {sim.TownCount}镇, {sim.CityStateCount}城邦");
            output.AppendLine($"控制领土: {sim.ControlledLandPercent:F1}%");
            output.AppendLine($"冲突热度: {sim.ConflictHeatPercent:F1}%");
            output.AppendLine($"联盟凝聚: {sim.AllianceCohesionPercent:F1}%");

            if (sim.RecentEvents.Length > 0)
            {
                output.AppendLine("\n近期重大事件:");
                foreach (var evt in sim.RecentEvents)
                {
                    output.AppendLine($"  • 第{evt.Epoch}纪元 [{evt.Category}]: {evt.Summary}");
                }
            }

            _outputText.Text = output.ToString();
        }

        private bool ValidateInputs()
        {
            if (_continentCountSpinBox.Value < 1 || _continentCountSpinBox.Value > 10)
            {
                UpdateStatus("大陆数量必须在1-10之间");
                return false;
            }

            if (_worldSizeSpinBox.Value < 1000000 || _worldSizeSpinBox.Value > 1000000000)
            {
                UpdateStatus("世界大小必须在100万-10亿km²之间");
                return false;
            }

            return true;
        }

        private void ToggleGenerationState(bool isGenerating)
        {
            if (_generateButton != null)
            {
                _generateButton.Disabled = isGenerating;
                _generateButton.Text = isGenerating ? "生成中..." : "生成世界";
            }

            if (_progressBar != null)
            {
                _progressBar.Visible = isGenerating;
                _progressBar.Value = isGenerating ? 0 : 100;
            }
        }

        private void UpdateStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
            GD.Print($"[地理生成器] {message}");
        }

        private void ClearOutput()
        {
            if (_outputText != null)
            {
                _outputText.Clear();
            }
        }

        private void OnExportJsonPressed()
        {
            if (_lastResult == null)
            {
                UpdateStatus("暂无可导出的报告，请先生成世界");
                return;
            }

            EnsureSaveDialog();
            if (_saveFileDialog == null)
            {
                UpdateStatus("导出失败：保存对话框不可用");
                return;
            }

            _saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            _saveFileDialog.Title = "导出世界报告(JSON)";
            _saveFileDialog.CurrentFile = $"world_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            _saveFileDialog.PopupCentered(new Vector2I(900, 560));
        }

        private void EnsureSaveDialog()
        {
            if (_saveFileDialog != null)
            {
                return;
            }

            _saveFileDialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.SaveFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Title = "导出世界报告(JSON)",
                Size = new Vector2I(900, 560)
            };
            _saveFileDialog.FileSelected += OnExportFileSelected;
            AddChild(_saveFileDialog);
        }

        private void OnExportFileSelected(string path)
        {
            if (_lastResult == null)
            {
                UpdateStatus("导出失败：没有可用的世界数据");
                return;
            }

            try
            {
                var payload = BuildExportPayload(_lastResult);
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);
                UpdateStatus($"报告已导出: {path}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"导出失败: {ex.Message}");
                GD.PrintErr(ex);
            }
        }

        private object BuildExportPayload(IntegratedWorldResult result)
        {
            return new
            {
                generatedAt = result.GenerationTimestamp,
                summary = new
                {
                    continentCount = result.GeographyContent.Continents.Count,
                    polityCount = result.CivilizationSimulation.PolityCount,
                    controlledLandPercent = result.CivilizationSimulation.ControlledLandPercent,
                    conflictHeatPercent = result.CivilizationSimulation.ConflictHeatPercent,
                    allianceCohesionPercent = result.CivilizationSimulation.AllianceCohesionPercent
                },
                continents = result.GeographyContent.Continents.Select(continent => new
                {
                    name = continent.name,
                    size = continent.size,
                    area = continent.area,
                    climate = continent.climate,
                    dominantTerrain = continent.dominantTerrain,
                    geologicalFeatures = continent.geologicalFeatures,
                    naturalResources = continent.naturalResources,
                    description = continent.description,
                    countries = (continent.Countries ?? new List<Country>()).Select(country =>
                    {
                        var civ = country.Civilization ?? new Civilization();
                        return new
                        {
                            name = country.name,
                            capital = country.capital,
                            government = country.government,
                            population = country.population,
                            territory = country.territory,
                            primaryResource = country.primaryResource,
                            militaryStrength = country.militaryStrength,
                            relations = country.relations,
                            ethnicGroups = (country.ethnicGroups ?? new List<EthnicGroup>()).Select(group => new
                            {
                                name = group.name,
                                languageFamily = group.languageFamily,
                                livelihood = group.livelihood,
                                origin = group.origin,
                                customs = group.customs,
                                festivals = group.festivals,
                                taboos = group.taboos,
                                environmentAdaptations = group.environmentAdaptations
                            }),
                            cities = (country.Cities ?? new List<City>()).Select(city => new
                            {
                                name = city.name,
                                type = city.type,
                                population = city.population,
                                founded = city.founded,
                                specialFeature = city.specialFeature,
                                economy = city.economy,
                                culture = city.culture
                            }),
                            civilization = new
                            {
                                economicBase = civ.economicBase,
                                settlementPattern = civ.settlementPattern,
                                riskResponse = civ.riskResponse,
                                religion = civ.religion,
                                socialStructure = civ.socialStructure,
                                customs = civ.customs,
                                artsAndCulture = civ.artsAndCulture,
                                technology = civ.technology,
                                interactionWithNature = civ.interactionWithNature
                            }
                        };
                    })
                }),
                recentEvents = result.CivilizationSimulation.RecentEvents.Select(evt => new
                {
                    epoch = evt.Epoch,
                    category = evt.Category,
                    summary = evt.Summary
                })
            };
        }

        public override void _ExitTree()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _llmService?.Dispose();
        }

        // 公共方法供外部调用
        public async Task<IntegratedWorldResult> GenerateWorldProgrammatically(WorldGenerationConfig config)
        {
            return await _worldGenerator.GenerateIntegratedWorldAsync(config, _cancellationTokenSource.Token);
        }

        public void CancelGeneration()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }
    }
}
