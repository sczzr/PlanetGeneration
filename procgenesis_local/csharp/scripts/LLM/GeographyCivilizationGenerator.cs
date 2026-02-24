using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.LLM
{
    public class GeographyCivilizationGenerator
    {
        private readonly LLamaInferenceService _llmService;
        private readonly Random _rng = new();
        
        public GeographyCivilizationGenerator(LLamaInferenceService llmService)
        {
            _llmService = llmService;
        }

        /// <summary>
        /// 生成完整的地理和文明内容
        /// </summary>
        public async Task<GeneratedWorldContent> GenerateWorldContentAsync(
            WorldGenerationParameters parameters,
            CancellationToken ct = default)
        {
            var content = new GeneratedWorldContent();
            
            // 1. 生成大陆信息
            content.Continents = await GenerateContinentsAsync(parameters, ct);
            
            // 2. 为每个大陆生成国家
            foreach (var continent in content.Continents)
            {
                continent.Countries = await GenerateCountriesAsync(continent, parameters, ct);
                
                // 3. 为每个国家生成城市
                foreach (var country in continent.Countries)
                {
                    country.Cities = await GenerateCitiesAsync(country, parameters, ct);
                    
                    // 4. 生成该国的文明特色
                    country.Civilization = await GenerateCivilizationAsync(country, parameters, ct);
                }
            }
            
            return content;
        }

        /// <summary>
        /// 生成大陆列表
        /// </summary>
        private async Task<List<Continent>> GenerateContinentsAsync(
            WorldGenerationParameters parameters, 
            CancellationToken ct)
        {
            var systemPrompt = @"你是一个世界构建专家。根据给定的参数，生成一组具有独特地理特征的大陆。
要求：
1. 每个大陆都有独特的名称、大小、气候类型和地理特征
2. 输出严格的JSON格式
3. 大陆数量在参数指定范围内";

            var prompt = $@"生成一个包含{parameters.ContinentCount}个大陆的世界：

世界参数：
- 总面积：{parameters.WorldSize}平方公里
- 气候多样性：{parameters.ClimateDiversity}/100
- 地质活跃度：{parameters.GeologicalActivity}/100
- 海洋覆盖：{parameters.OceanCoverage}/100%

请按照以下JSON格式输出大陆信息：
{{
  ""continents"": [
    {{
      ""name"": ""大陆名称"",
      ""size"": ""大型/中型/小型"",
      ""area"": 1000000,
      ""climate"": ""热带/温带/寒带/沙漠/极地"",
      ""dominantTerrain"": ""山脉/平原/丘陵/盆地/高原"",
      ""geologicalFeatures"": [""火山"", ""裂谷"", ""冰川""],
      ""naturalResources"": [""矿石"", ""宝石"", ""木材""],
      ""description"": ""该大陆的独特地理描述""
    }}
  ]
}}";

            var result = await _llmService.InferJsonAsync<ContinentListResponse>(
                prompt, 
                systemPrompt, 
                maxTokens: 800, 
                temperature: 0.7f,
                validator: ValidateContinents,
                ct: ct);

            if (result.IsSuccess && result.Value != null)
            {
                return result.Value.continents ?? new List<Continent>();
            }

            // 回退方案
            return GenerateFallbackContinents(parameters);
        }

        /// <summary>
        /// 为大陆生成国家
        /// </summary>
        private async Task<List<Country>> GenerateCountriesAsync(
            Continent continent, 
            WorldGenerationParameters parameters, 
            CancellationToken ct)
        {
            var systemPrompt = @"你是一个政治地理学专家。根据大陆的地理特征，生成合理的国家分布。
要求：
1. 国家数量与大陆大小匹配
2. 考虑地形对国家边界的影响
3. 每个国家都有独特的文化和政治特点
4. 每个国家至少包含2个文化族群，并说明其对自然环境的适应方式";

            var prompt = $@"为大陆'{continent.name}'生成国家：

大陆信息：
- 面积：{continent.area}平方公里
- 气候：{continent.climate}
- 主要地形：{continent.dominantTerrain}
- 地质特征：{string.Join(", ", continent.geologicalFeatures)}

世界参数：
- 政治复杂度：{parameters.PoliticalComplexity}/100
- 文化多样性：{parameters.CulturalDiversity}/100

请生成3-7个国家，按照以下JSON格式：
{{
  ""countries"": [
    {{
      ""name"": ""国家名称"",
      ""capital"": ""首都名称"",
      ""government"": ""君主制/共和制/部落联盟/神权政体"",
      ""population"": 5000000,
      ""territory"": ""沿海/内陆/山地/平原"",
      ""primaryResource"": ""农业/矿业/贸易/手工业"",
      ""militaryStrength"": ""强大/中等/弱小"",
      ""relations"": [""与邻国关系描述""],
      ""ethnicGroups"": [
        {{
          ""name"": ""族群名称"",
          ""languageFamily"": ""语言家族"",
          ""livelihood"": ""渔业/农耕/游牧/矿业/商贸"",
          ""origin"": ""迁徙与定居来源"",
          ""customs"": [""日常习俗1"", ""日常习俗2""],
          ""festivals"": [""节庆1"", ""节庆2""],
          ""taboos"": [""禁忌1"", ""禁忌2""],
          ""environmentAdaptations"": [""环境适应方式1"", ""环境适应方式2""]
        }}
      ]
    }}
  ]
}}";

            var result = await _llmService.InferJsonAsync<CountryListResponse>(
                prompt, 
                systemPrompt, 
                maxTokens: 1000, 
                temperature: 0.6f,
                validator: ValidateCountries,
                ct: ct);

            if (result.IsSuccess && result.Value != null)
            {
                return result.Value.countries ?? new List<Country>();
            }

            return GenerateFallbackCountries(continent, parameters);
        }

        /// <summary>
        /// 为国家生成城市
        /// </summary>
        private async Task<List<City>> GenerateCitiesAsync(
            Country country, 
            WorldGenerationParameters parameters, 
            CancellationToken ct)
        {
            var systemPrompt = @"你是一个城市规划专家。根据国家特点生成合理的城市体系。
要求：
1. 城市规模和数量与国家实力匹配
2. 考虑地理和经济因素
3. 每个城市都有独特的功能定位";

            var prompt = $@"为国家'{country.name}'生成主要城市：

国家信息：
- 首都：{country.capital}
- 政府形式：{country.government}
- 人口：{country.population}
- 主要资源：{country.primaryResource}
- 军事实力：{country.militaryStrength}

请生成3-5个重要城市，按照以下JSON格式：
{{
  ""cities"": [
    {{
      ""name"": ""城市名称"",
      ""type"": ""首都/港口/工业中心/宗教圣地/贸易枢纽"",
      ""population"": 500000,
      ""founded"": ""古代/中世纪/近现代"",
      ""specialFeature"": ""著名建筑/特产/历史事件"",
      ""economy"": ""商业/工业/农业/服务业"",
      ""culture"": ""艺术/学术/宗教/军事""
    }}
  ]
}}";

            var result = await _llmService.InferJsonAsync<CityListResponse>(
                prompt, 
                systemPrompt, 
                maxTokens: 800, 
                temperature: 0.6f,
                validator: ValidateCities,
                ct: ct);

            if (result.IsSuccess && result.Value != null)
            {
                return result.Value.cities ?? new List<City>();
            }

            return GenerateFallbackCities(country, parameters);
        }

        /// <summary>
        /// 生成文明特色内容
        /// </summary>
        private async Task<Civilization> GenerateCivilizationAsync(
            Country country, 
            WorldGenerationParameters parameters, 
            CancellationToken ct)
        {
            var systemPrompt = @"你是一个人类学和文化研究专家。根据地理环境和历史背景，生成独特的文明特色。
要求：
1. 文明特色与地理环境高度相关
2. 包含宗教信仰、社会习俗、艺术风格等
3. 体现与自然环境的互动关系";

            var prompt = $@"分析国家'{country.name}'的文明特色：

国家背景：
- 地理位置：{country.territory}
- 主要资源：{country.primaryResource}
- 气候条件：{(country.territory.Contains("沿海") ? "海洋性" : "大陆性")}
- 历史传统：{country.founded ?? "悠久"}
- 文化族群：{BuildEthnicGroupContext(country)}

请生成详细的文明分析，按照以下JSON格式：
{{
  ""economicBase"": ""文明主要生计结构和资源依赖"",
  ""settlementPattern"": ""聚落分布与地形关系"",
  ""riskResponse"": ""应对灾害与资源波动的制度"",
  ""religion"": {{
    ""mainFaith"": ""主要宗教名称"",
    ""beliefs"": [""核心教义1"", ""核心教义2""],
    ""practices"": [""日常仪式"", ""节日庆典""]
  }},
  ""socialStructure"": {{
    ""hierarchy"": ""等级制度描述"",
    ""family"": ""家庭结构特点"",
    ""education"": ""教育体系""
  }},
  ""customs"": {{
    ""dailyLife"": [""日常生活习俗""],
    ""ceremonies"": [""重要典礼仪式""],
    ""taboos"": [""禁忌事项""]
  }},
  ""artsAndCulture"": {{
    ""artForms"": [""主要艺术形式""],
    ""literature"": ""文学特色"",
    ""music"": ""音乐风格""
  }},
  ""technology"": {{
    ""specialties"": [""技术专长""],
    ""innovations"": [""重要发明""]
  }},
  ""interactionWithNature"": ""与自然环境的关系和适应方式""
}}";

            var result = await _llmService.InferJsonAsync<Civilization>(
                prompt, 
                systemPrompt, 
                maxTokens: 1200, 
                temperature: 0.7f,
                validator: ValidateCivilization,
                ct: ct);

            if (result.IsSuccess && result.Value != null)
            {
                return result.Value;
            }

            return GenerateFallbackCivilization(country, parameters);
        }

        #region 回退方案方法
        
        private List<Continent> GenerateFallbackContinents(WorldGenerationParameters parameters)
        {
            var continents = new List<Continent>();
            var baseArea = parameters.WorldSize / parameters.ContinentCount;
            
            for (int i = 0; i < parameters.ContinentCount; i++)
            {
                var climate = GetRandomClimate();
                var terrain = GetRandomTerrain();
                var continentName = BuildName("大陆", i);

                continents.Add(new Continent
                {
                    name = continentName,
                    size = i < 2 ? "大型" : (i < 4 ? "中型" : "小型"),
                    area = baseArea,
                    climate = climate,
                    dominantTerrain = terrain,
                    geologicalFeatures = GetGeologicalFeatures(climate, terrain),
                    naturalResources = GetNaturalResources(climate, terrain),
                    description = $"{continentName}以{terrain}为主，形成了与{climate}环境相匹配的生态圈和聚落带"
                });
            }
            
            return continents;
        }

        private List<Country> GenerateFallbackCountries(Continent continent, WorldGenerationParameters parameters)
        {
            var countries = new List<Country>();
            var countryCount = continent.size == "大型" ? 5 : (continent.size == "中型" ? 3 : 2);
            
            for (int i = 0; i < countryCount; i++)
            {
                var countryName = BuildName("国", i);
                var capitalName = BuildName("都", i);
                var territory = InferTerritory(continent);

                countries.Add(new Country
                {
                    name = countryName,
                    capital = capitalName,
                    government = GetRandomGovernment(),
                    population = 1000000 * (i + 1),
                    territory = territory,
                    primaryResource = GetResourceByTerrain(continent.dominantTerrain),
                    militaryStrength = "中等",
                    relations = new List<string> { "与邻国保持贸易与边境协商" },
                    founded = GetFoundingEra(continent.climate),
                    ethnicGroups = GenerateFallbackEthnicGroups(continent, territory, i)
                });
            }
            
            return countries;
        }

        private List<City> GenerateFallbackCities(Country country, WorldGenerationParameters parameters)
        {
            var cities = new List<City>();
            
            // 首都
            cities.Add(new City
            {
                name = country.capital,
                type = "首都",
                population = country.population / 3,
                founded = "古代",
                specialFeature = "政治中心",
                economy = "服务业",
                culture = "政治"
            });

            // 其他城市
            var cityTypes = new[] { "港口", "工业中心", "宗教圣地", "贸易枢纽" };
            for (int i = 0; i < 3; i++)
            {
                var cityType = cityTypes[i % cityTypes.Length];
                cities.Add(new City
                {
                    name = BuildName("城", i),
                    type = cityType,
                    population = country.population / 6,
                    founded = "中世纪",
                    specialFeature = GetSpecialFeatureByType(cityType),
                    economy = GetEconomyByType(cityType),
                    culture = "多元"
                });
            }
            
            return cities;
        }

        private Civilization GenerateFallbackCivilization(Country country, WorldGenerationParameters parameters)
        {
            var adaptation = BuildAdaptationNarrative(country);

            return new Civilization
            {
                economicBase = $"以{country.primaryResource}为支柱，辅以区域互市和季节性手工业",
                settlementPattern = $"{country.territory}地带形成核心聚落，交通节点发展为城镇网络",
                riskResponse = "建立粮仓轮换、河道维护和山口哨站制度，应对灾害与边境压力",
                religion = new Religion
                {
                    mainFaith = "自然崇拜",
                    beliefs = new List<string> { "山海有灵", "祖灵护佑" },
                    practices = new List<string> { "季风祭", "播种誓仪" }
                },
                socialStructure = new SocialStructure
                {
                    hierarchy = "议会领袖-行会-聚落家户",
                    family = "扩展家户与工坊共同体并存",
                    education = "口述传统与行会学徒制结合"
                },
                customs = new Customs
                {
                    dailyLife = new List<string> { "按季节轮作或迁牧", "集市日交换盐粮和工具" },
                    ceremonies = new List<string> { "成年航誓礼", "河谷盟誓节" },
                    taboos = new List<string> { "旱季污染水源", "破坏祖先界碑" }
                },
                artsAndCulture = new ArtsAndCulture
                {
                    artForms = new List<string> { "木石雕刻", "纹样织造" },
                    literature = "迁徙史诗与航路谚语",
                    music = "鼓笛与合唱并重"
                },
                technology = new Technology
                {
                    specialties = new List<string> { "蓄水工程", "金属与木工复合工艺" },
                    innovations = new List<string> { "坡地渠网", "耐候仓储" }
                },
                interactionWithNature = adaptation
            };
        }

        #endregion

        #region 辅助方法

        private string GetRandomClimate()
        {
            var climates = new[] { "热带", "温带", "寒带", "沙漠", "极地" };
            return climates[_rng.Next(climates.Length)];
        }

        private string GetRandomTerrain()
        {
            var terrains = new[] { "山脉", "平原", "丘陵", "盆地", "高原" };
            return terrains[_rng.Next(terrains.Length)];
        }

        private string GetRandomGovernment()
        {
            var governments = new[] { "君主制", "共和制", "部落联盟", "神权政体" };
            return governments[_rng.Next(governments.Length)];
        }

        private string BuildName(string category, int index)
        {
            var prefixes = new[] { "阿", "洛", "卡", "瑟", "塔", "维", "诺", "哈", "泽", "岚" };
            var roots = new[] { "兰", "图", "泽", "尔", "纳", "索", "隆", "维", "希", "岗" };
            var suffixes = new[] { "亚", "恩", "斯", "姆", "特", "纳", "里", "达", "堡", "湾" };

            var a = prefixes[(index + _rng.Next(prefixes.Length)) % prefixes.Length];
            var b = roots[(index * 2 + _rng.Next(roots.Length)) % roots.Length];
            var c = suffixes[(index * 3 + _rng.Next(suffixes.Length)) % suffixes.Length];
            return $"{a}{b}{c}{category}";
        }

        private string InferTerritory(Continent continent)
        {
            if (continent.climate == "沙漠")
            {
                return "内陆";
            }

            if (continent.climate == "热带")
            {
                return _rng.NextDouble() > 0.5 ? "沿海" : "平原";
            }

            return continent.dominantTerrain switch
            {
                "山脉" => "山地",
                "平原" => "平原",
                _ => _rng.NextDouble() > 0.5 ? "沿海" : "内陆"
            };
        }

        private string GetFoundingEra(string climate)
        {
            return climate switch
            {
                "极地" => "近现代",
                "沙漠" => "中世纪",
                _ => "古代"
            };
        }

        private List<EthnicGroup> GenerateFallbackEthnicGroups(Continent continent, string territory, int seedOffset)
        {
            var groups = new List<EthnicGroup>();
            var groupCount = 2 + _rng.Next(2);

            for (int i = 0; i < groupCount; i++)
            {
                var name = BuildName("族", seedOffset + i);
                var livelihood = InferLivelihoodByContext(continent.climate, territory, continent.dominantTerrain, i);
                groups.Add(new EthnicGroup
                {
                    name = name,
                    languageFamily = InferLanguageFamily(continent.climate, i),
                    livelihood = livelihood,
                    origin = InferOrigin(continent, territory, i),
                    customs = new List<string>
                    {
                        $"在{GetSeasonKeyword(continent.climate)}举行公共集市",
                        $"以{livelihood}生产周期安排婚盟与工役"
                    },
                    festivals = new List<string>
                    {
                        $"{name}迁徙纪念节",
                        $"{name}丰潮庆典"
                    },
                    taboos = new List<string>
                    {
                        "枯水季私占水源",
                        "毁坏祖灵标记"
                    },
                    environmentAdaptations = new List<string>
                    {
                        InferAdaptation(continent.climate, territory, continent.dominantTerrain),
                        "通过季节性储备和互助工坊分散资源风险"
                    }
                });
            }

            return groups;
        }

        private string BuildEthnicGroupContext(Country country)
        {
            if (country.ethnicGroups == null || country.ethnicGroups.Count == 0)
            {
                return "暂无族群资料";
            }

            var chunks = new List<string>();
            foreach (var group in country.ethnicGroups.Take(3))
            {
                var adaptation = group.environmentAdaptations.FirstOrDefault() ?? "适应信息缺失";
                chunks.Add($"{group.name}({group.livelihood}; {adaptation})");
            }

            return string.Join("；", chunks);
        }

        private static string? ValidateContinents(ContinentListResponse response)
        {
            if (response.continents == null || response.continents.Count == 0)
            {
                return "大陆列表为空";
            }

            if (response.continents.Any(c => string.IsNullOrWhiteSpace(c.name) || string.IsNullOrWhiteSpace(c.climate) || string.IsNullOrWhiteSpace(c.dominantTerrain)))
            {
                return "大陆字段不完整";
            }

            return null;
        }

        private static string? ValidateCountries(CountryListResponse response)
        {
            if (response.countries == null || response.countries.Count == 0)
            {
                return "国家列表为空";
            }

            foreach (var country in response.countries)
            {
                if (string.IsNullOrWhiteSpace(country.name) || string.IsNullOrWhiteSpace(country.capital))
                {
                    return "国家名称或首都缺失";
                }

                if (country.ethnicGroups == null || country.ethnicGroups.Count < 2)
                {
                    return "国家文化族群数量不足";
                }

                if (country.ethnicGroups.Any(g => g.environmentAdaptations == null || g.environmentAdaptations.Count == 0))
                {
                    return "文化族群缺少环境适应信息";
                }
            }

            return null;
        }

        private static string? ValidateCities(CityListResponse response)
        {
            if (response.cities == null || response.cities.Count < 2)
            {
                return "城市数量不足";
            }

            if (response.cities.Any(city => string.IsNullOrWhiteSpace(city.name) || string.IsNullOrWhiteSpace(city.type)))
            {
                return "城市字段不完整";
            }

            return null;
        }

        private static string? ValidateCivilization(Civilization civ)
        {
            if (string.IsNullOrWhiteSpace(civ.interactionWithNature))
            {
                return "缺少自然互动信息";
            }

            if (string.IsNullOrWhiteSpace(civ.economicBase) || string.IsNullOrWhiteSpace(civ.settlementPattern) || string.IsNullOrWhiteSpace(civ.riskResponse))
            {
                return "文明核心结构字段缺失";
            }

            if (civ.customs == null || civ.customs.dailyLife == null || civ.customs.dailyLife.Count == 0)
            {
                return "习俗信息不足";
            }

            return null;
        }

        private static List<string> GetGeologicalFeatures(string climate, string terrain)
        {
            var features = new List<string>();
            if (terrain == "山脉" || terrain == "高原")
            {
                features.Add("褶皱山系");
            }

            if (climate == "寒带" || climate == "极地")
            {
                features.Add("冰川侵蚀谷");
            }

            if (climate == "沙漠")
            {
                features.Add("风蚀盆地");
            }

            if (features.Count == 0)
            {
                features.Add("河流冲积带");
            }

            return features;
        }

        private static List<string> GetNaturalResources(string climate, string terrain)
        {
            if (terrain == "山脉")
            {
                return new List<string> { "金属矿石", "石材", "药草" };
            }

            if (climate == "热带")
            {
                return new List<string> { "硬木", "香料", "渔获" };
            }

            if (climate == "沙漠")
            {
                return new List<string> { "盐", "铜矿", "绿洲农产" };
            }

            return new List<string> { "木材", "谷物", "陶土" };
        }

        private static string InferLivelihoodByContext(string climate, string territory, string terrain, int index)
        {
            if (territory == "沿海")
            {
                return index % 2 == 0 ? "渔业商贸" : "盐业与造船";
            }

            if (terrain == "高原" || terrain == "山脉")
            {
                return index % 2 == 0 ? "山地牧养" : "矿冶与驿路贸易";
            }

            if (climate == "沙漠")
            {
                return "绿洲农耕与驼队贸易";
            }

            return index % 2 == 0 ? "河谷农耕" : "手工业与区域集市";
        }

        private static string InferLanguageFamily(string climate, int index)
        {
            var family = climate switch
            {
                "寒带" => "北缘语系",
                "热带" => "海陆语系",
                "沙漠" => "旱原语系",
                _ => "中陆语系"
            };

            return index % 2 == 0 ? family : $"{family}-支系";
        }

        private static string InferOrigin(Continent continent, string territory, int index)
        {
            if (territory == "沿海")
            {
                return index % 2 == 0 ? "由海湾群岛迁入并定居河口" : "沿季风航线扩散到沿岸平原";
            }

            if (continent.dominantTerrain == "山脉")
            {
                return "从山口商道向高地盆地迁徙并形成聚落";
            }

            return "由内陆河谷扩展，在资源节点形成定居点";
        }

        private static string GetSeasonKeyword(string climate)
        {
            return climate switch
            {
                "热带" => "雨季",
                "寒带" => "短夏",
                "沙漠" => "凉季",
                _ => "丰收季"
            };
        }

        private static string InferAdaptation(string climate, string territory, string terrain)
        {
            if (territory == "沿海")
            {
                return "采用潮汐历法和防风船坞，规避风暴季风险";
            }

            if (climate == "沙漠")
            {
                return "围绕地下水脉建蓄水井网，实行迁商与定耕并行";
            }

            if (terrain == "山脉")
            {
                return "沿山脊和谷口设置驿站，利用高差发展梯田";
            }

            return "通过河堤和仓储制度稳定粮食与手工业供给";
        }

        private static string BuildAdaptationNarrative(Country country)
        {
            var hints = (country.ethnicGroups ?? new List<EthnicGroup>())
                .SelectMany(g => g.environmentAdaptations)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Take(2)
                .ToList();

            if (hints.Count == 0)
            {
                return "通过季节迁移与储备制度维持与自然环境的平衡";
            }

            return string.Join("；", hints);
        }

        private string GetResourceByTerrain(string terrain)
        {
            return terrain switch
            {
                "山脉" => "矿业",
                "平原" => "农业",
                "丘陵" => "林业",
                "盆地" => "农业",
                "高原" => "畜牧业",
                _ => "混合经济"
            };
        }

        private string GetSpecialFeatureByType(string type)
        {
            return type switch
            {
                "港口" => "优良港湾",
                "工业中心" => "制造业发达",
                "宗教圣地" => "古老寺庙",
                "贸易枢纽" => "繁华集市",
                _ => "地方特色"
            };
        }

        private string GetEconomyByType(string type)
        {
            return type switch
            {
                "港口" => "贸易",
                "工业中心" => "工业",
                "宗教圣地" => "服务业",
                "贸易枢纽" => "商业",
                _ => "多元化"
            };
        }

        #endregion
    }

    #region 数据结构定义

    public class WorldGenerationParameters
    {
        public int ContinentCount { get; set; } = 5;
        public long WorldSize { get; set; } = 100000000; // 平方公里
        public int ClimateDiversity { get; set; } = 70;
        public int GeologicalActivity { get; set; } = 50;
        public int OceanCoverage { get; set; } = 71;
        public int PoliticalComplexity { get; set; } = 60;
        public int CulturalDiversity { get; set; } = 80;
    }

    public class GeneratedWorldContent
    {
        public List<Continent> Continents { get; set; } = new();
    }

    public class Continent
    {
        public string name { get; set; } = "";
        public string size { get; set; } = "";
        public long area { get; set; }
        public string climate { get; set; } = "";
        public string dominantTerrain { get; set; } = "";
        public List<string> geologicalFeatures { get; set; } = new();
        public List<string> naturalResources { get; set; } = new();
        public string description { get; set; } = "";
        public List<Country> Countries { get; set; } = new();
    }

    public class Country
    {
        public string name { get; set; } = "";
        public string capital { get; set; } = "";
        public string government { get; set; } = "";
        public long population { get; set; }
        public string territory { get; set; } = "";
        public string primaryResource { get; set; } = "";
        public string militaryStrength { get; set; } = "";
        public List<string> relations { get; set; } = new();
        public List<EthnicGroup> ethnicGroups { get; set; } = new();
        public List<City> Cities { get; set; } = new();
        public Civilization Civilization { get; set; } = new();
        public string? founded { get; set; }
    }

    public class EthnicGroup
    {
        public string name { get; set; } = "";
        public string languageFamily { get; set; } = "";
        public string livelihood { get; set; } = "";
        public string origin { get; set; } = "";
        public List<string> customs { get; set; } = new();
        public List<string> festivals { get; set; } = new();
        public List<string> taboos { get; set; } = new();
        public List<string> environmentAdaptations { get; set; } = new();
    }

    public class City
    {
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public long population { get; set; }
        public string founded { get; set; } = "";
        public string specialFeature { get; set; } = "";
        public string economy { get; set; } = "";
        public string culture { get; set; } = "";
    }

    public class Civilization
    {
        public string economicBase { get; set; } = "";
        public string settlementPattern { get; set; } = "";
        public string riskResponse { get; set; } = "";
        public Religion religion { get; set; } = new();
        public SocialStructure socialStructure { get; set; } = new();
        public Customs customs { get; set; } = new();
        public ArtsAndCulture artsAndCulture { get; set; } = new();
        public Technology technology { get; set; } = new();
        public string interactionWithNature { get; set; } = "";
    }

    public class Religion
    {
        public string mainFaith { get; set; } = "";
        public List<string> beliefs { get; set; } = new();
        public List<string> practices { get; set; } = new();
    }

    public class SocialStructure
    {
        public string hierarchy { get; set; } = "";
        public string family { get; set; } = "";
        public string education { get; set; } = "";
    }

    public class Customs
    {
        public List<string> dailyLife { get; set; } = new();
        public List<string> ceremonies { get; set; } = new();
        public List<string> taboos { get; set; } = new();
    }

    public class ArtsAndCulture
    {
        public List<string> artForms { get; set; } = new();
        public string literature { get; set; } = "";
        public string music { get; set; } = "";
    }

    public class Technology
    {
        public List<string> specialties { get; set; } = new();
        public List<string> innovations { get; set; } = new();
    }

    // JSON响应类
    public class ContinentListResponse
    {
        public List<Continent> continents { get; set; } = new();
    }

    public class CountryListResponse
    {
        public List<Country> countries { get; set; } = new();
    }

    public class CityListResponse
    {
        public List<City> cities { get; set; } = new();
    }

    #endregion
}
