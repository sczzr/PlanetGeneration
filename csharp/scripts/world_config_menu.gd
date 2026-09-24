extends Control
class_name WorldConfigMenu

## 创建世界向导（守卫模式第一级页面）：空灵诗意晨雾艺术画风。
## 采用平衡左右双栏通透呼吸布局（Balanced Floating Cards），界面表现全面使用程序化 StyleBoxFlat 与水墨微色块。
## 严格保留 master 分支朴实严谨的游戏用语规范（杜绝修真仙侠生造词），与标准 i18n 键值对接。
## 输出的 config 字典与 WorldGuard.WorldGen.WorldGenParams.FromConfig 兼容。

signal closed
## 点击"生成世界"时发出（先于关闭动画），携带两步配置的汇总字典。
signal confirmed(config: Dictionary)

## 向导步骤：地形配置 / 初始资源配置。
enum Step { TERRAIN, RESOURCES }

const STEP_COUNT := 2

## 弹出/收回动画时长。
const PANEL_POP_DURATION := 0.22
const PANEL_CLOSE_DURATION := 0.16

## 地图大小档位：config 中 map_size 的取值与界面选择一一对应。
const MAP_SIZES: Array[StringName] = [&"Mini", &"Small", &"Medium", &"Large", &"Huge", &"Extreme"]
const MAP_SIZE_KEYS: Array[String] = [
	"WORLD_MAP_SIZE_MINI", "WORLD_MAP_SIZE_SMALL", "WORLD_MAP_SIZE_MEDIUM",
	"WORLD_MAP_SIZE_LARGE", "WORLD_MAP_SIZE_HUGE", "WORLD_MAP_SIZE_EXTREME",
]
## 默认选中"小型"(索引 1)：实际尺寸 512×256。迷你档 256×128 仅 3.2 万格，
## 山脉、河流与群系过渡的细节量不足以支撑手绘地图的符号密度。
const DEFAULT_MAP_SIZE_INDEX := 1

## 世界类型：config 取值与翻译 key。
const WORLD_TYPES: Array[StringName] = [
	&"Continents", &"Supercontinent", &"Balanced", &"InlandSea",
	&"ClassicArchipelago", &"BrokenIslandChain", &"ShallowSea",
	&"RiftHighlands", &"PolarIcelands", &"AtollChain",
]
const WORLD_TYPE_KEYS: Array[String] = [
	"WORLD_TERRAIN_PRESET_CONTINENTS_NAME", "WORLD_TERRAIN_PRESET_SUPERCONTINENT_NAME",
	"WORLD_TERRAIN_PRESET_BALANCED_NAME", "WORLD_TERRAIN_PRESET_INLAND_SEA_NAME",
	"WORLD_TERRAIN_PRESET_ARCHIPELAGO_NAME", "WORLD_TERRAIN_PRESET_BROKEN_ISLANDS_NAME",
	"WORLD_TERRAIN_PRESET_SHALLOW_SEA_NAME", "WORLD_TERRAIN_PRESET_RIFT_HIGHLANDS_NAME",
	"WORLD_TERRAIN_PRESET_POLAR_ICELANDS_NAME", "WORLD_TERRAIN_PRESET_ATOLL_CHAIN_NAME",
]
## 海陆梯度五档档位名称的翻译 key（与 _add_gradient_stepper 的分段按钮一一对应）。
const GRADIENT_KEYS: Array[String] = [
	"WORLD_CONFIG_GRADIENT_GENTLE", "WORLD_CONFIG_GRADIENT_HILLY", "WORLD_CONFIG_GRADIENT_STANDARD",
	"WORLD_CONFIG_GRADIENT_STEEP", "WORLD_CONFIG_GRADIENT_PEAKS",
]

## 地形一键预设清单：通俗严谨的地貌形态预设，点击一键联动设置宏观世界形态及推荐地貌参数
const TERRAIN_PRESETS: Array[Dictionary] = [
	{
		"id": "continents",
		"name_key": "WORLD_TERRAIN_PRESET_CONTINENTS_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_CONTINENTS_DESC",
		"world_type_idx": 0,
		"continent_count": 5,
		"values": {
			"sea_level": 0.48, "heat": 0.50, "moisture": 1.00,
			"oceanic_ratio": 0.45, "continent_bias": 0.50, "continent_count": 5.0,
			"river_density": 1.00, "wetland_abundance": 1.00, "delta_scale": 1.00, "floodplain_scale": 1.00,
			"canyon_depth": 1.00, "karst_frequency": 1.00, "badlands_frequency": 1.00,
			"desert_dune_scale": 1.00, "glacier_extent": 1.00, "fjord_depth": 1.00,
			"peak_frequency": 1.00, "island_density": 1.00, "basin_sensitivity": 1.00,
			"volcano_frequency": 1.00, "rift_frequency": 1.00, "plateau_extent": 1.00,
		}
	},
	{
		"id": "supercontinent",
		"name_key": "WORLD_TERRAIN_PRESET_SUPERCONTINENT_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_SUPERCONTINENT_DESC",
		"world_type_idx": 1,
		"continent_count": 1,
		"values": {
			"sea_level": 0.38, "heat": 0.58, "moisture": 0.70,
			"oceanic_ratio": 0.28, "continent_bias": 0.58, "continent_count": 1.0,
			"river_density": 0.75, "wetland_abundance": 0.60, "delta_scale": 0.70, "floodplain_scale": 0.80,
			"canyon_depth": 1.40, "karst_frequency": 0.80, "badlands_frequency": 1.60,
			"desert_dune_scale": 1.80, "glacier_extent": 0.80, "fjord_depth": 0.50,
			"peak_frequency": 1.30, "island_density": 0.40, "basin_sensitivity": 1.50,
			"volcano_frequency": 0.90, "rift_frequency": 1.40, "plateau_extent": 1.60,
		}
	},
	{
		"id": "balanced",
		"name_key": "WORLD_TERRAIN_PRESET_BALANCED_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_BALANCED_DESC",
		"world_type_idx": 2,
		"continent_count": 2,
		"values": {
			"sea_level": 0.50, "heat": 0.50, "moisture": 1.00,
			"oceanic_ratio": 0.48, "continent_bias": 0.22, "continent_count": 2.0,
			"river_density": 1.00, "wetland_abundance": 1.00, "delta_scale": 1.00, "floodplain_scale": 1.00,
			"canyon_depth": 1.00, "karst_frequency": 1.00, "badlands_frequency": 1.00,
			"desert_dune_scale": 1.00, "glacier_extent": 1.00, "fjord_depth": 1.00,
			"peak_frequency": 1.00, "island_density": 1.00, "basin_sensitivity": 1.00,
			"volcano_frequency": 1.00, "rift_frequency": 1.00, "plateau_extent": 1.00,
		}
	},
	{
		"id": "inland_sea",
		"name_key": "WORLD_TERRAIN_PRESET_INLAND_SEA_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_INLAND_SEA_DESC",
		"world_type_idx": 3,
		"continent_count": 2,
		"values": {
			"sea_level": 0.42, "heat": 0.56, "moisture": 1.15,
			"oceanic_ratio": 0.32, "continent_bias": 0.55, "continent_count": 2.0,
			"river_density": 1.40, "wetland_abundance": 1.40, "delta_scale": 1.80, "floodplain_scale": 1.60,
			"canyon_depth": 0.90, "karst_frequency": 1.80, "badlands_frequency": 0.70,
			"desert_dune_scale": 0.60, "glacier_extent": 0.50, "fjord_depth": 0.60,
			"peak_frequency": 1.10, "island_density": 0.90, "basin_sensitivity": 1.80,
			"volcano_frequency": 0.80, "rift_frequency": 1.10, "plateau_extent": 1.10,
		}
	},
	{
		"id": "archipelago",
		"name_key": "WORLD_TERRAIN_PRESET_ARCHIPELAGO_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_ARCHIPELAGO_DESC",
		"world_type_idx": 4,
		"continent_count": 4,
		"values": {
			"sea_level": 0.66, "heat": 0.62, "moisture": 1.40,
			"oceanic_ratio": 0.68, "continent_bias": 0.10, "continent_count": 4.0,
			"river_density": 0.60, "wetland_abundance": 1.20, "delta_scale": 1.30, "floodplain_scale": 0.60,
			"canyon_depth": 0.60, "karst_frequency": 1.40, "badlands_frequency": 0.40,
			"desert_dune_scale": 0.30, "glacier_extent": 0.40, "fjord_depth": 0.80,
			"peak_frequency": 0.90, "island_density": 2.20, "basin_sensitivity": 0.60,
			"volcano_frequency": 1.80, "rift_frequency": 0.80, "plateau_extent": 0.50,
		}
	},
	{
		"id": "broken_islands",
		"name_key": "WORLD_TERRAIN_PRESET_BROKEN_ISLANDS_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_BROKEN_ISLANDS_DESC",
		"world_type_idx": 5,
		"continent_count": 4,
		"values": {
			"sea_level": 0.60, "heat": 0.48, "moisture": 1.20,
			"oceanic_ratio": 0.62, "continent_bias": 0.12, "continent_count": 4.0,
			"river_density": 0.80, "wetland_abundance": 0.90, "delta_scale": 0.90, "floodplain_scale": 0.70,
			"canyon_depth": 1.20, "karst_frequency": 1.20, "badlands_frequency": 0.80,
			"desert_dune_scale": 0.40, "glacier_extent": 0.90, "fjord_depth": 1.50,
			"peak_frequency": 1.40, "island_density": 2.00, "basin_sensitivity": 0.80,
			"volcano_frequency": 1.90, "rift_frequency": 1.70, "plateau_extent": 0.70,
		}
	},
	{
		"id": "shallow_sea",
		"name_key": "WORLD_TERRAIN_PRESET_SHALLOW_SEA_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_SHALLOW_SEA_DESC",
		"world_type_idx": 6,
		"continent_count": 3,
		"values": {
			"sea_level": 0.52, "heat": 0.55, "moisture": 1.30,
			"oceanic_ratio": 0.50, "continent_bias": 0.25, "continent_count": 3.0,
			"river_density": 1.30, "wetland_abundance": 2.10, "delta_scale": 2.20, "floodplain_scale": 2.00,
			"canyon_depth": 0.50, "karst_frequency": 1.10, "badlands_frequency": 0.50,
			"desert_dune_scale": 0.40, "glacier_extent": 0.50, "fjord_depth": 0.50,
			"peak_frequency": 0.60, "island_density": 1.50, "basin_sensitivity": 1.20,
			"volcano_frequency": 0.60, "rift_frequency": 0.60, "plateau_extent": 0.60,
		}
	},
	{
		"id": "rift_highlands",
		"name_key": "WORLD_TERRAIN_PRESET_RIFT_HIGHLANDS_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_RIFT_HIGHLANDS_DESC",
		"world_type_idx": 7,
		"continent_count": 2,
		"values": {
			"sea_level": 0.36, "heat": 0.52, "moisture": 0.85,
			"oceanic_ratio": 0.30, "continent_bias": 0.55, "continent_count": 2.0,
			"river_density": 0.90, "wetland_abundance": 0.70, "delta_scale": 0.80, "floodplain_scale": 0.70,
			"canyon_depth": 2.20, "karst_frequency": 1.00, "badlands_frequency": 1.50,
			"desert_dune_scale": 0.90, "glacier_extent": 0.70, "fjord_depth": 0.80,
			"peak_frequency": 1.70, "island_density": 0.50, "basin_sensitivity": 1.90,
			"volcano_frequency": 2.00, "rift_frequency": 2.40, "plateau_extent": 2.10,
		}
	},
	{
		"id": "polar_icelands",
		"name_key": "WORLD_TERRAIN_PRESET_POLAR_ICELANDS_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_POLAR_ICELANDS_DESC",
		"world_type_idx": 8,
		"continent_count": 2,
		"values": {
			"sea_level": 0.50, "heat": 0.28, "moisture": 1.10,
			"oceanic_ratio": 0.50, "continent_bias": 0.50, "continent_count": 2.0,
			"river_density": 0.85, "wetland_abundance": 0.90, "delta_scale": 0.80, "floodplain_scale": 0.60,
			"canyon_depth": 1.50, "karst_frequency": 0.60, "badlands_frequency": 0.60,
			"desert_dune_scale": 0.20, "glacier_extent": 2.30, "fjord_depth": 2.10,
			"peak_frequency": 1.60, "island_density": 1.20, "basin_sensitivity": 0.90,
			"volcano_frequency": 0.80, "rift_frequency": 1.00, "plateau_extent": 1.30,
		}
	},
	{
		"id": "atoll_chain",
		"name_key": "WORLD_TERRAIN_PRESET_ATOLL_CHAIN_NAME",
		"desc_key": "WORLD_TERRAIN_PRESET_ATOLL_CHAIN_DESC",
		"world_type_idx": 9,
		"continent_count": 4,
		"values": {
			"sea_level": 0.68, "heat": 0.68, "moisture": 1.50,
			"oceanic_ratio": 0.65, "continent_bias": 0.40, "continent_count": 4.0,
			"river_density": 0.70, "wetland_abundance": 1.50, "delta_scale": 1.40, "floodplain_scale": 0.70,
			"canyon_depth": 0.70, "karst_frequency": 1.60, "badlands_frequency": 0.40,
			"desert_dune_scale": 0.30, "glacier_extent": 0.30, "fjord_depth": 0.60,
			"peak_frequency": 1.10, "island_density": 2.40, "basin_sensitivity": 0.70,
			"volcano_frequency": 1.90, "rift_frequency": 1.20, "plateau_extent": 0.60,
		}
	},
]

## 地形参数行定义：包含参数 key、翻译 key、分节标识、范围、步长、默认值，
## 以及滑条主题图标（对应 SLIDER_ICON_SETS 的键）。
const TERRAIN_ROWS: Array[Dictionary] = [
	{"key": "sea_level", "label": "WORLD_CONFIG_SEA_LEVEL", "section": "ocean", "icon": "water",
		"min": 0.1, "max": 0.85, "step": 0.01, "default": 0.5, "fmt": "%.2f", "color": Color(0.30, 0.52, 0.70)},
	{"key": "heat", "label": "WORLD_CONFIG_HEAT", "section": "ocean", "icon": "acorn",
		"min": 0.01, "max": 1.0, "step": 0.01, "default": 0.5, "fmt": "%.2f", "color": Color(0.85, 0.45, 0.25)},
	{"key": "moisture", "label": "WORLD_CONFIG_MOISTURE", "section": "ocean", "icon": "water",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.25, 0.60, 0.75)},
	{"key": "elevation", "label": "WORLD_CONFIG_ELEVATION", "section": "ocean", "icon": "mountain",
		"min": 0.0, "max": 1.0, "step": 0.01, "default": 0.5, "fmt": "%.2f", "color": Color(0.28, 0.58, 0.54)},
	{"key": "plate_count", "label": "WORLD_CONFIG_PLATE_COUNT", "section": "mountain", "icon": "mountain",
		"min": 6.0, "max": 60.0, "step": 1.0, "default": 20.0, "fmt": "%d", "int": true, "color": Color(0.38, 0.52, 0.42)},
	{"key": "oceanic_ratio", "label": "WORLD_CONFIG_OCEANIC_RATIO", "section": "mountain", "icon": "water",
		"min": 0.1, "max": 0.9, "step": 0.01, "default": 0.48, "fmt": "%.2f", "color": Color(0.30, 0.50, 0.65)},
	{"key": "continent_bias", "label": "WORLD_CONFIG_CONTINENT_BIAS", "section": "mountain", "icon": "rock",
		"min": 0.0, "max": 0.6, "step": 0.01, "default": 0.18, "fmt": "%.2f", "color": Color(0.55, 0.45, 0.32)},
	{"key": "continent_count", "label": "WORLD_CONFIG_CONTINENT_COUNT", "section": "mountain", "icon": "rock",
		"min": 1.0, "max": 7.0, "step": 1.0, "default": 5.0, "fmt": "%d", "int": true, "color": Color(0.55, 0.45, 0.32)},
	{"key": "mountain_range_count", "label": "WORLD_CONFIG_MOUNTAIN_RANGE_COUNT", "section": "mountain", "icon": "mountain",
		"min": 0.0, "max": 10.0, "step": 1.0, "default": 4.0, "fmt": "%d", "int": true, "color": Color(0.32, 0.54, 0.38)},
	{"key": "volcano_count", "label": "WORLD_CONFIG_VOLCANO_COUNT", "section": "mountain", "icon": "mountain",
		"min": 0.0, "max": 6.0, "step": 1.0, "default": 1.0, "fmt": "%d", "int": true, "color": Color(0.66, 0.36, 0.28)},
	{"key": "hill_count", "label": "WORLD_CONFIG_HILL_COUNT", "section": "mountain", "icon": "rock",
		"min": 0.0, "max": 12.0, "step": 1.0, "default": 5.0, "fmt": "%d", "int": true, "color": Color(0.48, 0.55, 0.42)},
	{"key": "plateau_count", "label": "WORLD_CONFIG_PLATEAU_COUNT", "section": "mountain", "icon": "mountain",
		"min": 0.0, "max": 6.0, "step": 1.0, "default": 2.0, "fmt": "%d", "int": true, "color": Color(0.56, 0.50, 0.36)},
	{"key": "plain_count", "label": "WORLD_CONFIG_PLAIN_COUNT", "section": "mountain", "icon": "acorn",
		"min": 0.0, "max": 8.0, "step": 1.0, "default": 3.0, "fmt": "%d", "int": true, "color": Color(0.62, 0.60, 0.30)},
	{"key": "basin_count", "label": "WORLD_CONFIG_BASIN_COUNT", "section": "mountain", "icon": "water",
		"min": 0.0, "max": 8.0, "step": 1.0, "default": 2.0, "fmt": "%d", "int": true, "color": Color(0.38, 0.50, 0.58)},
	{"key": "rift_valley_count", "label": "WORLD_CONFIG_RIFT_VALLEY_COUNT", "section": "mountain", "icon": "rock",
		"min": 0.0, "max": 6.0, "step": 1.0, "default": 1.0, "fmt": "%d", "int": true, "color": Color(0.44, 0.42, 0.50)},
	{"key": "mountain_gradient", "label": "WORLD_CONFIG_MOUNTAIN_GRADIENT", "section": "mountain",
		"kind": "segmented_gradient", "min": 1.0, "max": 5.0, "step": 1.0, "default": 3.0, "int": true},
	{"key": "landform_relief", "label": "WORLD_CONFIG_LANDFORM_RELIEF", "section": "terrain", "icon": "mountain",
		"min": 0.3, "max": 1.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.68, 0.52, 0.32)},
	{"key": "mountain_range_scale", "label": "WORLD_CONFIG_MOUNTAIN_RANGE_SCALE", "section": "terrain", "icon": "mountain",
		"min": 0.5, "max": 1.5, "step": 0.05, "default": 1.0, "fmt": "%.2f", "color": Color(0.70, 0.46, 0.28)},
	{"key": "rock_debris_frequency", "label": "WORLD_CONFIG_ROCK_DEBRIS", "section": "terrain", "icon": "rock",
		"min": 0.0, "max": 20.0, "step": 1.0, "default": 5.0, "fmt": "%d", "int": true, "color": Color(0.48, 0.55, 0.52)},

	# ---- 高级地貌表格 1：流水与湿地水网 ----
	{"key": "river_density", "label": "WORLD_CONFIG_RIVER_DENSITY", "section": "terrain_water", "table": "fluvial", "icon": "water",
		"min": 0.4, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.28, 0.54, 0.64)},
	{"key": "wetland_abundance", "label": "WORLD_CONFIG_WETLAND_ABUNDANCE", "section": "terrain_water", "table": "fluvial", "icon": "water",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.32, 0.62, 0.44)},
	{"key": "delta_scale", "label": "WORLD_CONFIG_DELTA_SCALE", "section": "terrain_water", "table": "fluvial", "icon": "water",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.38, 0.65, 0.58)},
	{"key": "floodplain_scale", "label": "WORLD_CONFIG_FLOODPLAIN_SCALE", "section": "terrain_water", "table": "fluvial", "icon": "acorn",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.42, 0.68, 0.48)},

	# ---- 高级地貌表格 2：溶蚀与峡谷地貌 ----
	{"key": "canyon_depth", "label": "WORLD_CONFIG_CANYON_DEPTH", "section": "terrain_water", "table": "erosion", "icon": "mountain",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.72, 0.48, 0.28)},
	{"key": "karst_frequency", "label": "WORLD_CONFIG_KARST_FREQUENCY", "section": "terrain_water", "table": "erosion", "icon": "rock",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.52, 0.60, 0.48)},
	{"key": "badlands_frequency", "label": "WORLD_CONFIG_BADLANDS_FREQUENCY", "section": "terrain_water", "table": "erosion", "icon": "rock",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.74, 0.44, 0.34)},

	# ---- 高级地貌表格 3：气候风成与极地冰川 ----
	{"key": "desert_dune_scale", "label": "WORLD_CONFIG_DESERT_DUNE_SCALE", "section": "terrain_water", "table": "aeolian_glacial", "icon": "acorn",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.82, 0.66, 0.32)},
	{"key": "glacier_extent", "label": "WORLD_CONFIG_GLACIER_EXTENT", "section": "terrain_water", "table": "aeolian_glacial", "icon": "water",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.55, 0.75, 0.88)},
	{"key": "fjord_depth", "label": "WORLD_CONFIG_FJORD_DEPTH", "section": "terrain_water", "table": "aeolian_glacial", "icon": "mountain",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.36, 0.60, 0.72)},

	# ---- 高级地貌表格 4：地质构造与高山岛屿 ----
	{"key": "peak_frequency", "label": "WORLD_CONFIG_PEAK_FREQUENCY", "section": "terrain_water", "table": "tectonic_alpine", "icon": "mountain",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.78, 0.70, 0.58)},
	{"key": "island_density", "label": "WORLD_CONFIG_ISLAND_DENSITY", "section": "terrain_water", "table": "tectonic_alpine", "icon": "water",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.30, 0.58, 0.62)},
	{"key": "basin_sensitivity", "label": "WORLD_CONFIG_BASIN_SENSITIVITY", "section": "terrain_water", "table": "tectonic_alpine", "icon": "water",
		"min": 0.5, "max": 2.0, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.40, 0.55, 0.65)},
	{"key": "volcano_frequency", "label": "WORLD_CONFIG_VOLCANO_FREQUENCY", "section": "terrain_water", "table": "tectonic_alpine", "icon": "mountain",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.76, 0.40, 0.30)},
	{"key": "rift_frequency", "label": "WORLD_CONFIG_RIFT_FREQUENCY", "section": "terrain_water", "table": "tectonic_alpine", "icon": "rock",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.58, 0.45, 0.40)},
	{"key": "plateau_extent", "label": "WORLD_CONFIG_PLATEAU_EXTENT", "section": "terrain_water", "table": "tectonic_alpine", "icon": "mountain",
		"min": 0.2, "max": 2.5, "step": 0.01, "default": 1.0, "fmt": "%.2f", "color": Color(0.65, 0.55, 0.38)},
]

## 初始资源定义：严谨清晰的标准游戏资源名称
const RESOURCE_SPECS: Array[Dictionary] = [
	{"id": "food", "key": "RESOURCE_FOOD", "cost": 1, "max": 99, "default": 15, "color": Color(0.68, 0.52, 0.24)},
	{"id": "wood", "key": "RESOURCE_WOOD", "cost": 1, "max": 99, "default": 10, "color": Color(0.46, 0.36, 0.22)},
	{"id": "stone", "key": "RESOURCE_STONE", "cost": 2, "max": 99, "default": 8, "color": Color(0.48, 0.52, 0.52)},
	{"id": "iron", "key": "RESOURCE_IRON", "cost": 3, "max": 99, "default": 3, "color": Color(0.38, 0.45, 0.56)},
	{"id": "gold", "key": "RESOURCE_GOLD", "cost": 2, "max": 99, "default": 5, "color": Color(0.78, 0.62, 0.20)},
	{"id": "supply", "key": "RESOURCE_SUPPLY", "cost": 4, "max": 99, "default": 4, "color": Color(0.32, 0.56, 0.44)},
	{"id": "knowledge", "key": "RESOURCE_KNOWLEDGE", "cost": 5, "max": 99, "default": 2, "color": Color(0.36, 0.48, 0.68)},
]
## 初始资源总价值点上限。
const RESOURCE_BUDGET := 100

## 资源配置预设清单：通俗朴实的预设方案（名称与描述走 i18n）
const RESOURCE_PRESETS: Array[Dictionary] = [
	{
		"name_key": "WORLD_CONFIG_PRESET_BALANCED_NAME",
		"desc_key": "WORLD_CONFIG_PRESET_BALANCED_DESC",
		"values": {"food": 15, "wood": 10, "stone": 8, "iron": 3, "gold": 5, "supply": 4, "knowledge": 2}
	},
	{
		"name_key": "WORLD_CONFIG_PRESET_BUILDER_NAME",
		"desc_key": "WORLD_CONFIG_PRESET_BUILDER_DESC",
		"values": {"food": 20, "wood": 16, "stone": 12, "iron": 5, "gold": 2, "supply": 4, "knowledge": 0}
	},
	{
		"name_key": "WORLD_CONFIG_PRESET_HARSH_NAME",
		"desc_key": "WORLD_CONFIG_PRESET_HARSH_DESC",
		"values": {"food": 10, "wood": 6, "stone": 4, "iron": 1, "gold": 1, "supply": 2, "knowledge": 0}
	},
	{
		"name_key": "WORLD_CONFIG_PRESET_EXPLORER_NAME",
		"desc_key": "WORLD_CONFIG_PRESET_EXPLORER_DESC",
		"values": {"food": 12, "wood": 8, "stone": 6, "iron": 4, "gold": 4, "supply": 3, "knowledge": 8}
	},
]

## 页面字体：霞鹜文楷（手写楷体，贴合水彩手绘风格）
const HEADER_FONT := preload("res://assets/fonts/ui_handwritten_bold.tres")
const BODY_FONT := preload("res://assets/fonts/ui_handwritten.tres")

const SLIDER_TRACK_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_track.png")
const SLIDER_FILL_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_fill.png")

## 滑条主题把手图标集（96x96 超采样，ThemedSliderSkin 按 24 设计像素绘制）
## 与配套的拖拽粒子主题：water=水滴粒子，mountain=砂石坠落的厚重感。
const SLIDER_ICON_SETS := {
	"acorn": {
		"normal": preload("res://assets/textures/ui/worldgen_slider_grabber.png"),
		"hover": preload("res://assets/textures/ui/worldgen_slider_grabber_hl.png"),
		"particles": "",
	},
	"water": {
		"normal": preload("res://assets/textures/ui/worldgen_icon_water.png"),
		"hover": preload("res://assets/textures/ui/worldgen_icon_water_hl.png"),
		"particles": "water",
	},
	"mountain": {
		"normal": preload("res://assets/textures/ui/worldgen_icon_mountain.png"),
		"hover": preload("res://assets/textures/ui/worldgen_icon_mountain_hl.png"),
		"particles": "mountain",
	},
	"rock": {
		"normal": preload("res://assets/textures/ui/worldgen_icon_rock.png"),
		"hover": preload("res://assets/textures/ui/worldgen_icon_rock_hl.png"),
		"particles": "rock",
	},
}

## 守卫风按钮素材：regular/pressed 两态九宫格贴图（3x3 等分网格、单元 54px =
## 设计切缝 12/9 的 3 倍超采样密度）。由 generate_worldgen_ui.py 按设计对齐
## 切缝离线烘焙；素材重新生成后重跑该脚本即可，运行时零处理。
const GUARD_BTN_REGULAR_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_button_regular_9.png")
const GUARD_BTN_PRESSED_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_button_pressed_9.png")
## 设计空间切缝：左右 12px、上下 9px；中部文字面由剩余高度拉伸。
const GUARD_BTN_MARGIN_X := 12.0
const GUARD_BTN_MARGIN_Y := 9.0
## 悬浮提亮（复用常态纹理）；选中/按下态文字色。
const GUARD_BTN_HOVER_TINT := Color(1.07, 1.07, 1.05, 1.0)
const GUARD_BTN_ACTIVE_FONT := Color(0.98, 0.97, 0.92, 1.0)

## 卡片框九宫格贴图（3x3 等分网格、单元 66px = 设计切缝 22px 的 3 倍超采样密度），
## 由 generate_worldgen_ui.py 离线烘焙。
const CARD_FRAME_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_card_frame_9.png")
const CARD_FRAME_MARGIN := 22.0

## 顶部页签水彩贴图（两态九宫格，单元 66px = 设计切缝 11/6 的整数倍）
## 与关闭按钮的圆形木牌叉号图标，均由 generate_worldgen_ui.py 离线产出。
const TAB_ACTIVE_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_tab_active_9.png")
const TAB_INACTIVE_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_tab_inactive_9.png")
const TAB_MARGIN_X := 11.0
const TAB_MARGIN_Y := 6.0
const CLOSE_ICON_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_icon_close.png")

## 诗意自然色彩规范（微雾水墨风格）
const COLOR_INK_PRIMARY := Color(0.13, 0.18, 0.15, 1.0)       # 晨雾深墨色文本
const COLOR_INK_MUTED := Color(0.35, 0.44, 0.40, 1.0)         # 次级水墨文本
const COLOR_INK_DULL := Color(0.52, 0.60, 0.56, 1.0)          # 细微注释与占位水墨
const COLOR_ACCENT_FOREST := Color(0.16, 0.26, 0.20, 1.0)     # 沉稳森林墨绿
const COLOR_ACCENT_MOSS := Color(0.28, 0.50, 0.36, 1.0)       # 阳光微苔绿
const COLOR_ACCENT_GOLD := Color(0.76, 0.55, 0.20, 1.0)       # 暖阳麦草金
const COLOR_BORDER_HAIRLINE := Color(0.18, 0.25, 0.22, 0.16)  # 极细雅致墨线
const COLOR_BORDER_FOCUS := Color(0.25, 0.45, 0.35, 0.65)     # 聚焦微翠光晕
const COLOR_CARD_MIST := Color(0.98, 0.99, 0.98, 0.82)        # 通透晨雾浮动卡片底
const COLOR_CARD_PARCHMENT := Color(0.97, 0.95, 0.90, 0.88)   # 温润羊皮纸数值标签底
const COLOR_FIELD_BG := Color(1.0, 1.0, 1.0, 0.65)            # 极简玉白微透托底
const COLOR_FIELD_HOVER := Color(1.0, 1.0, 1.0, 0.92)         # 悬浮明亮玉白
const COLOR_DANGER := Color(0.78, 0.25, 0.20, 1.0)            # 陶土赭红（超限警示）

@onready var _backdrop: TextureRect = %Backdrop
@onready var _dim: ColorRect = %Dim
@onready var _frame: Control = %Frame
@onready var _title: Label = %Title
@onready var _subtitle: Label = %Subtitle
@onready var _close_button: Button = %CloseButton
@onready var _terrain_page: HBoxContainer = %TerrainPage
@onready var _resources_page: HBoxContainer = %ResourcesPage
@onready var _step_label: Label = %StepLabel
@onready var _prev_button: Button = %PrevButton
@onready var _next_button: Button = %NextButton
@onready var _generate_button: Button = %GenerateButton
@onready var _step1_btn: Button = %Step1Btn
@onready var _step2_btn: Button = %Step2Btn
@onready var _step_switcher: SmoothTabSwitch = %StepSwitcher

var _show_tween: Tween
var _close_tween: Tween
var _step_tween: Tween
var _closing := false

## 当前所在步骤。
var _current_step := Step.TERRAIN
## 已应用的种子；-1 表示留空随机。
var _committed_seed := -1

## 地形参数控件表 key -> Range (Slider or SpinBox)
var _terrain_controls := {}
## 种子输入框。
var _seed_edit: LineEdit
## 选中的地图规模与世界类型索引。
var _selected_map_size_index := DEFAULT_MAP_SIZE_INDEX
var _selected_world_type_index := 0
var _selected_continent_count := 5
var _map_size_buttons: Array[Button] = []
var _world_type_buttons: Array[Button] = []
var _gradient_buttons: Array[Button] = []
var _continent_count_buttons: Array[Button] = []
## 地形一键预设按钮列表与当前选中索引。
var _terrain_preset_buttons: Array[Button] = []
var _selected_terrain_preset_index := 0
## 高级配置折叠表格列表与主折叠状态。
var _collapsible_tables: Array[Dictionary] = []
var _advanced_terrain_content: VBoxContainer
var _advanced_master_arrow: Label
var _is_advanced_terrain_expanded := false
## 预设卡片按钮列表
var _preset_buttons: Array[Button] = []
## 生成河流开关。
var _rivers_check: CheckButton

## 守卫按钮两态贴图（超采样九宫格，运行时由图集拼接；悬浮态用提亮 tint 复用常态图）。
var _guard_tex_wood: Texture2D
var _guard_tex_down: Texture2D
## 已挂守卫皮肤的按钮 -> 背景控件 / 选中态 / 按压态（分段选项按钮用，状态变化时刷新背景）。
## 注意：Button.pressed 是信号而非属性（属性为 button_pressed），按压态须自行追踪。
var _guard_bg: Dictionary = {}
var _guard_selected: Dictionary = {}
var _guard_pressing: Dictionary = {}
## 生成世界按钮背景（深色木牌常态）
var _generate_bg: NineSliceBg
## 卡片框九宫格贴图（超采样，所有浮动卡片共用）。
var _card_frame_tex: Texture2D

## 资源行控件与数值。
var _resource_spins := {}
var _resource_subtotal_labels := {}
## 预算指示看板控件。
var _budget_used_label: RollingNumberLabel
var _budget_over_label: Label
var _budget_remain_label: Label
var _budget_progress_bar: ProgressBar
var _budget_card: NineSlicePanel

const TILE_MODE_KEYS: Array[String] = [
	"WORLD_TILE_MODE_CELLS", "WORLD_TILE_MODE_OUTLINED", "WORLD_TILE_MODE_HYBRID", "WORLD_TILE_MODE_RASTER"
]

## 以太法则与文明生态控件。
var _magic_slider: Range
var _aggression_slider: Range
var _diversity_slider: Range
var _epoch_slider: Range
var _epoch_badge_label: Label
var _selected_tile_mode_index := 0
var _tile_mode_buttons: Array[Button] = []

## 当前步骤的标题/介绍文案键（随页签切换，见 _update_step_indicator）。
var _title_key := "WORLD_CONFIG_STEP_TERRAIN_TITLE"
var _subtitle_key := "WORLD_CONFIG_STEP_TERRAIN_DESC"

## 文案国际化登记表。
var _localized: Array[Dictionary] = []


func _ready() -> void:
	_init_guard_button_styles()
	_backdrop.modulate.a = 0.0
	_dim.modulate.a = 0.0
	_build_terrain_page()
	_build_resources_page()
	_close_button.pressed.connect(close)
	_next_button.pressed.connect(_on_next_pressed)
	_prev_button.pressed.connect(_on_prev_pressed)
	_generate_button.pressed.connect(_on_generate_pressed)
	_step1_btn.pressed.connect(func() -> void: _show_step(Step.TERRAIN))
	_step2_btn.pressed.connect(func() -> void: _show_step(Step.RESOURCES))
	_dim.gui_input.connect(_on_dim_input)

	# 场景内节点文案登记（tscn 仅存 key，运行时统一翻译刷新）。
	# 标题/介绍随步骤切换（见 _apply_step_texts），不走静态登记。
	_register_localized(_step1_btn, "WORLD_CONFIG_STEP_TERRAIN")
	_register_localized(_step2_btn, "WORLD_CONFIG_STEP_RESOURCES")
	_register_localized(_prev_button, "WORLD_CONFIG_BACK_STEP")
	_register_localized(_next_button, "WORLD_CONFIG_NEXT")
	_register_localized(_generate_button, "WORLD_CONFIG_GENERATE")

	# 水彩皮肤装配：顶部页签（丝滑滑动切换组件）/ 关闭按钮 / 底栏动作按钮
	_step_switcher.setup(TAB_ACTIVE_TEX, TAB_INACTIVE_TEX, TAB_MARGIN_X, TAB_MARGIN_Y,
		COLOR_INK_PRIMARY, COLOR_INK_MUTED, GUARD_BTN_HOVER_TINT, Vector2(18.0, 8.0))
	_setup_close_button()
	_skin_guard_button(_prev_button)
	_skin_guard_button(_next_button)
	_skin_generate_button()
	_refresh_texts()


func open() -> void:
	if not is_node_ready():
		await ready
	_closing = false
	if _close_tween != null and _close_tween.is_valid():
		_close_tween.kill()
	visible = true
	_refresh_texts()
	_reset_values()
	_show_step(Step.TERRAIN, false)
	_play_show()


func close() -> void:
	if _closing or not visible:
		return
	_closing = true
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _close_tween != null and _close_tween.is_valid():
		_close_tween.kill()
	_close_tween = create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	_close_tween.tween_property(_frame, "scale", Vector2(0.96, 0.96), PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_frame, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_backdrop, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_dim, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.chain().tween_callback(_finish_close)


func _finish_close() -> void:
	visible = false
	_frame.scale = Vector2.ONE
	_frame.modulate.a = 1.0
	_backdrop.modulate.a = 0.0
	_dim.modulate.a = 0.0
	_closing = false
	closed.emit()


func _play_show() -> void:
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	_frame.scale = Vector2(0.96, 0.96)
	_frame.modulate.a = 0.0
	_frame.pivot_offset = _frame.size * 0.5
	_backdrop.modulate.a = 0.0
	_dim.modulate.a = 0.0

	_show_tween = create_tween().set_parallel(true).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_show_tween.tween_property(_frame, "scale", Vector2.ONE, PANEL_POP_DURATION)
	_show_tween.tween_property(_frame, "modulate:a", 1.0, PANEL_POP_DURATION)
	_show_tween.tween_property(_backdrop, "modulate:a", 1.0, PANEL_POP_DURATION * 0.8)
	_show_tween.tween_property(_dim, "modulate:a", 1.0, PANEL_POP_DURATION * 0.6)
	_grab_edge_focus(false)


func _unhandled_input(event: InputEvent) -> void:
	if not visible or _closing:
		return
	if event.is_action_pressed(&"ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
		return
	if event.is_action_pressed(&"ui_focus_next"):
		var f := get_viewport().gui_get_focus_owner()
		if f != null and _is_last_focusable(f):
			get_viewport().set_input_as_handled()
			_grab_edge_focus(false)
			return
	if event.is_action_pressed(&"ui_focus_prev"):
		var f := get_viewport().gui_get_focus_owner()
		if f != null and _is_first_focusable(f):
			get_viewport().set_input_as_handled()
			_grab_edge_focus(true)
			return


func _grab_edge_focus(from_end: bool) -> void:
	var list: Array[Control] = []
	_collect_focusables(self, list)
	if list.is_empty():
		return
	if from_end:
		list.back().grab_focus()
	else:
		list.front().grab_focus()


func _collect_focusables(node: Node, out: Array[Control]) -> void:
	if node is Control:
		var c := node as Control
		if c.is_visible_in_tree() and c.focus_mode != Control.FOCUS_NONE and not (c is Panel or c is PanelContainer or c is Label):
			out.append(c)
	for child in node.get_children():
		_collect_focusables(child, out)


func _is_first_focusable(c: Control) -> bool:
	var list: Array[Control] = []
	_collect_focusables(self, list)
	return not list.is_empty() and list.front() == c


func _is_last_focusable(c: Control) -> bool:
	var list: Array[Control] = []
	_collect_focusables(self, list)
	return not list.is_empty() and list.back() == c


func _clean_label(lbl: Control) -> Control:
	lbl.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	lbl.add_theme_constant_override("shadow_offset_x", 0)
	lbl.add_theme_constant_override("shadow_offset_y", 0)
	lbl.add_theme_constant_override("shadow_outline_size", 0)
	return lbl


func _show_step(step: Step, animate: bool = true) -> void:
	_current_step = step
	var is_terrain := (_current_step == Step.TERRAIN)

	# 页签滑块与文字墨色在点击瞬间起滑（0.5s 三次缓出），与内容过渡并行。
	_step_switcher.set_active(0 if is_terrain else 1, animate)

	if not animate:
		_terrain_page.visible = is_terrain
		_resources_page.visible = not is_terrain
		_terrain_page.modulate.a = 1.0 if is_terrain else 0.0
		_resources_page.modulate.a = 1.0 if not is_terrain else 0.0
		_terrain_page.scale = Vector2.ONE
		_resources_page.scale = Vector2.ONE
		_update_step_indicator(is_terrain)
		return

	if _step_tween != null and _step_tween.is_valid():
		_step_tween.kill()
	_step_tween = create_tween()
	_step_tween.set_parallel(true).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)

	var outgoing: Control = _resources_page if is_terrain else _terrain_page
	var incoming: Control = _terrain_page if is_terrain else _resources_page

	# 旧页快速淡出微缩让位；随后新页以缩放浮现方式升起（参考方案切换组件
	# 子菜单展开：中心缩放 + 淡入，急出缓停）。
	outgoing.pivot_offset = outgoing.size * 0.5
	_step_tween.tween_property(outgoing, "modulate:a", 0.0, 0.15)
	_step_tween.tween_property(outgoing, "scale", Vector2(0.985, 0.985), 0.15)
	_step_tween.chain().tween_callback(func() -> void:
		outgoing.visible = false
		incoming.visible = true
		incoming.pivot_offset = incoming.size * 0.5
		incoming.modulate.a = 0.0
		incoming.scale = Vector2(0.96, 0.96)
		_update_step_indicator(is_terrain)
	)
	_step_tween.tween_property(incoming, "modulate:a", 1.0, 0.40)
	_step_tween.parallel().tween_property(incoming, "scale", Vector2.ONE, 0.45)


func _update_step_indicator(is_terrain: bool) -> void:
	_prev_button.visible = not is_terrain
	_next_button.visible = is_terrain
	_generate_button.visible = not is_terrain

	# 左上角标题与介绍跟随步骤切换，描述当前页的作用
	_title_key = "WORLD_CONFIG_STEP_TERRAIN_TITLE" if is_terrain \
			else "WORLD_CONFIG_STEP_RESOURCES_TITLE"
	_subtitle_key = "WORLD_CONFIG_STEP_TERRAIN_DESC" if is_terrain \
			else "WORLD_CONFIG_STEP_RESOURCES_DESC"
	_apply_step_texts()

	_update_step_label()
	if not is_terrain:
		_refresh_budget()


func _on_next_pressed() -> void:
	_apply_seed_text()
	_show_step(Step.RESOURCES)


func _on_prev_pressed() -> void:
	_show_step(Step.TERRAIN)


func _on_generate_pressed() -> void:
	if not _is_budget_valid():
		return
	_apply_seed_text()
	var config := _build_config()
	confirmed.emit(config)
	close()


## 创建优雅的晨雾浮动卡片（超采样九宫格背景，任意尺寸不变形、高分屏不糊）。
## 内容统一加到 card.content（MarginContainer，内边距 18/14）。
func _create_floating_card(parent: Container, stretch_ratio: float = 1.0) -> NineSlicePanel:
	var card := NineSlicePanel.new(_card_frame_tex, CARD_FRAME_MARGIN, CARD_FRAME_MARGIN, 18.0, 14.0)
	card.bg.tint = Color(1.0, 1.0, 1.0, 0.94)
	if stretch_ratio > 0.0:
		card.size_flags_vertical = Control.SIZE_EXPAND_FILL
		card.size_flags_stretch_ratio = stretch_ratio
	else:
		card.size_flags_vertical = Control.SIZE_FILL
	parent.add_child(card)
	return card


# ------------------------------------------------------------- 守卫风按钮与卡片框九宫格贴图

## 九宫格贴图由工具离线烘焙（见 tools/gemini_media/generate_worldgen_ui.py），
## 此处只做引用装配：常态/按下两态按钮贴图 + 卡片框贴图。
func _init_guard_button_styles() -> void:
	_guard_tex_wood = GUARD_BTN_REGULAR_TEX
	_guard_tex_down = GUARD_BTN_PRESSED_TEX
	_card_frame_tex = CARD_FRAME_TEX


## 关闭动态构建按钮的文字投影（主题默认投影在浅色贴图上发糊）。
func _clean_button(btn: Button) -> void:
	btn.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	btn.add_theme_constant_override("shadow_offset_x", 0)
	btn.add_theme_constant_override("shadow_offset_y", 0)


## 给按钮挂守卫木牌皮肤：背景 NineSliceBg 画在按钮之下（show_behind_parent），
## 按钮自身样式盒置空只负责文字；背景按常态/悬浮/按下/选中/焦点五态切换。
func _skin_guard_button(btn: Button) -> void:
	for state_name in ["normal", "hover", "pressed", "focus"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 14
		empty.content_margin_right = 14
		empty.content_margin_top = 5
		empty.content_margin_bottom = 5
		btn.add_theme_stylebox_override(state_name, empty)
	var bg := NineSliceBg.new(_guard_tex_wood, GUARD_BTN_MARGIN_X, GUARD_BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	btn.add_child(bg)
	_guard_bg[btn] = bg
	_guard_selected[btn] = false
	_guard_pressing[btn] = false
	btn.mouse_entered.connect(_refresh_guard_bg.bind(btn))
	btn.mouse_exited.connect(_refresh_guard_bg.bind(btn))
	btn.button_down.connect(func() -> void:
		_guard_pressing[btn] = true
		_refresh_guard_bg(btn))
	btn.button_up.connect(func() -> void:
		_guard_pressing[btn] = false
		_refresh_guard_bg(btn))
	btn.focus_entered.connect(_refresh_guard_bg.bind(btn))
	btn.focus_exited.connect(_refresh_guard_bg.bind(btn))
	_refresh_guard_bg(btn)


## 按当前状态刷新守卫按钮背景：按压或选中用蓝面，悬浮或焦点用提亮木面。
func _refresh_guard_bg(btn: Button) -> void:
	var bg: NineSliceBg = _guard_bg.get(btn)
	if bg == null:
		return
	if _guard_pressing.get(btn, false) or _guard_selected.get(btn, false):
		bg.texture = _guard_tex_down
		bg.tint = Color.WHITE
	elif btn.is_hovered() or btn.has_focus():
		bg.texture = _guard_tex_wood
		bg.tint = GUARD_BTN_HOVER_TINT
	else:
		bg.texture = _guard_tex_wood
		bg.tint = Color.WHITE


## 生成世界主按钮：常态即深色木牌（按下态贴图）以突出主操作，
## 悬浮/按压仅微调亮度，文字用浅色。
func _skin_generate_button() -> void:
	for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 22
		empty.content_margin_right = 22
		empty.content_margin_top = 6
		empty.content_margin_bottom = 6
		_generate_button.add_theme_stylebox_override(state_name, empty)
	var bg := NineSliceBg.new(_guard_tex_down, GUARD_BTN_MARGIN_X, GUARD_BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	_generate_button.add_child(bg)
	_generate_bg = bg
	_generate_button.mouse_entered.connect(_refresh_generate_bg)
	_generate_button.mouse_exited.connect(_refresh_generate_bg)
	_generate_button.button_down.connect(_refresh_generate_bg)
	_generate_button.button_up.connect(_refresh_generate_bg)
	_generate_button.focus_entered.connect(_refresh_generate_bg)
	_generate_button.focus_exited.connect(_refresh_generate_bg)
	_refresh_generate_bg()


func _refresh_generate_bg() -> void:
	if _generate_bg == null:
		return
	var tint := Color.WHITE
	if _generate_button.button_pressed:
		tint = Color(0.90, 0.90, 0.90, 1.0)
	elif _generate_button.is_hovered() or _generate_button.has_focus():
		tint = Color(1.06, 1.06, 1.04, 1.0)
	_generate_bg.tint = tint


## 关闭按钮：圆形水彩木牌叉号图标；样式盒置空，悬浮反馈交给 menu_fx 弹动。
func _setup_close_button() -> void:
	for state_name in ["normal", "hover", "pressed", "focus"]:
		_close_button.add_theme_stylebox_override(state_name, StyleBoxEmpty.new())
	_close_button.flat = true
	_close_button.text = ""
	_close_button.icon = CLOSE_ICON_TEX
	_close_button.expand_icon = true


# ------------------------------------------------------------- 第一步：地形配置构建

func _build_terrain_page() -> void:
	for child in _terrain_page.get_children():
		child.queue_free()

	# ---- 左栏：三大浮动卡片群 (stretch 1.0) ----
	var left_scroll := ScrollContainer.new()
	left_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_scroll.size_flags_stretch_ratio = 1.0
	left_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_style_scrollbar(left_scroll)
	_terrain_page.add_child(left_scroll)

	var left_col := VBoxContainer.new()
	left_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left_col.add_theme_constant_override("separation", 12)
	left_scroll.add_child(left_col)

	# 卡片 1：世界种子 (World Seed)
	var seed_card := _create_floating_card(left_col, 0.0)
	var seed_box := VBoxContainer.new()
	seed_box.add_theme_constant_override("separation", 8)
	seed_card.content.add_child(seed_box)

	_add_card_title(seed_box, "WORLD_CONFIG_SEED")

	var seed_row := HBoxContainer.new()
	seed_row.add_theme_constant_override("separation", 8)
	seed_box.add_child(seed_row)

	_seed_edit = LineEdit.new()
	_seed_edit.placeholder_text = tr("WORLD_CONFIG_SEED_PLACEHOLDER")
	_seed_edit.custom_minimum_size = Vector2(0, 34)
	_seed_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_seed_edit.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_style_seed_edit(_seed_edit)
	seed_row.add_child(_seed_edit)
	# 占位提示走 i18n，语言切换时由 _refresh_texts 刷新（LineEdit 登记占位文案）。
	_register_localized(_seed_edit, "WORLD_CONFIG_SEED_PLACEHOLDER")

	var dice_btn := _make_pill_button("🎲 " + tr("WORLD_CONFIG_SEED_RANDOM"))
	dice_btn.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	dice_btn.pressed.connect(_on_random_seed_pressed)
	seed_row.add_child(dice_btn)
	_register_localized(dice_btn, "WORLD_CONFIG_SEED_RANDOM", [], "🎲 ")

	var apply_btn := _make_pill_button(tr("WORLD_CONFIG_SEED_APPLY"))
	apply_btn.custom_minimum_size = Vector2(60, 34)
	apply_btn.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	apply_btn.pressed.connect(_on_apply_seed_pressed)
	seed_row.add_child(apply_btn)
	_register_localized(apply_btn, "WORLD_CONFIG_SEED_APPLY")

	var seed_note := Label.new()
	_clean_label(seed_note)
	seed_note.text = tr("WORLD_CONFIG_SEED_NOTE")
	seed_note.add_theme_font_override("font", BODY_FONT)
	seed_note.add_theme_font_size_override("font_size", 11)
	seed_note.add_theme_color_override("font_color", COLOR_INK_DULL)
	seed_box.add_child(seed_note)
	_register_localized(seed_note, "WORLD_CONFIG_SEED_NOTE")

	# 卡片 2：地图规模 (Map Size - 分段胶囊选择器)
	var scale_card := _create_floating_card(left_col, 0.0)
	var scale_box := VBoxContainer.new()
	scale_box.add_theme_constant_override("separation", 8)
	scale_card.content.add_child(scale_box)

	_add_card_title(scale_box, "WORLD_CONFIG_MAP_SIZE")

	var scale_btn_row := HBoxContainer.new()
	scale_btn_row.add_theme_constant_override("separation", 6)
	scale_box.add_child(scale_btn_row)

	_map_size_buttons.clear()
	for i in range(MAP_SIZES.size()):
		var size_btn := Button.new()
		_clean_button(size_btn)
		size_btn.text = tr(MAP_SIZE_KEYS[i])
		size_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		size_btn.custom_minimum_size = Vector2(0, 36)
		size_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		size_btn.add_theme_font_override("font", BODY_FONT)
		size_btn.add_theme_font_size_override("font_size", 12)
		var idx := i
		_skin_guard_button(size_btn)
		size_btn.pressed.connect(func() -> void: _on_map_size_selected(idx))
		scale_btn_row.add_child(size_btn)
		_map_size_buttons.append(size_btn)
		_register_localized(size_btn, MAP_SIZE_KEYS[i])
	_update_map_size_buttons()

	# 卡片 3：地貌与水系 (Terrain & Rivers)
	var rules_card := _create_floating_card(left_col, 1.0)
	var rules_box := VBoxContainer.new()
	rules_box.add_theme_constant_override("separation", 10)
	rules_card.content.add_child(rules_box)

	_add_card_title(rules_box, "WORLD_CONFIG_SECTION_TERRAIN_WATER")

	# 一键地貌预设选择区 (One-Click Terrain Presets)
	var p_sub_row := HBoxContainer.new()
	p_sub_row.add_theme_constant_override("separation", 8)
	rules_box.add_child(p_sub_row)

	var p_sub_lbl := Label.new()
	_clean_label(p_sub_lbl)
	p_sub_lbl.text = "◈ " + tr("WORLD_CONFIG_QUICK_PRESETS")
	p_sub_lbl.add_theme_font_override("font", HEADER_FONT)
	p_sub_lbl.add_theme_font_size_override("font_size", 12)
	p_sub_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	p_sub_row.add_child(p_sub_lbl)
	_register_localized(p_sub_lbl, "WORLD_CONFIG_QUICK_PRESETS", [], "◈ ")

	var p_hint := Label.new()
	_clean_label(p_hint)
	p_hint.text = tr("WORLD_CONFIG_QUICK_PRESETS_HINT")
	p_hint.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	p_hint.add_theme_font_override("font", BODY_FONT)
	p_hint.add_theme_font_size_override("font_size", 10)
	p_hint.add_theme_color_override("font_color", COLOR_INK_DULL)
	p_sub_row.add_child(p_hint)
	_register_localized(p_hint, "WORLD_CONFIG_QUICK_PRESETS_HINT")

	var preset_grid := GridContainer.new()
	preset_grid.columns = 2
	preset_grid.add_theme_constant_override("h_separation", 6)
	preset_grid.add_theme_constant_override("v_separation", 6)
	rules_box.add_child(preset_grid)

	_terrain_preset_buttons.clear()
	for i in range(TERRAIN_PRESETS.size()):
		var preset: Dictionary = TERRAIN_PRESETS[i]
		var p_btn := Button.new()
		_clean_button(p_btn)
		p_btn.custom_minimum_size = Vector2(0, 42)
		p_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		p_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

		var inner_vbox := VBoxContainer.new()
		inner_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
		inner_vbox.add_theme_constant_override("separation", 2)
		inner_vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
		inner_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
		p_btn.add_child(inner_vbox)

		var name_lbl := Label.new()
		_clean_label(name_lbl)
		name_lbl.text = tr(preset.name_key)
		name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		name_lbl.add_theme_font_override("font", HEADER_FONT)
		name_lbl.add_theme_font_size_override("font_size", 12)
		name_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
		inner_vbox.add_child(name_lbl)
		_register_localized(name_lbl, preset.name_key)

		var desc_lbl := Label.new()
		_clean_label(desc_lbl)
		desc_lbl.text = tr(preset.desc_key)
		desc_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		desc_lbl.add_theme_font_override("font", BODY_FONT)
		desc_lbl.add_theme_font_size_override("font_size", 10)
		desc_lbl.add_theme_color_override("font_color", COLOR_INK_MUTED)
		inner_vbox.add_child(desc_lbl)
		_register_localized(desc_lbl, preset.desc_key)

		var s := StyleBoxFlat.new()
		s.bg_color = Color(0.97, 0.96, 0.93, 0.65)
		s.border_color = COLOR_BORDER_HAIRLINE
		s.set_border_width_all(1)
		s.set_corner_radius_all(8)
		p_btn.add_theme_stylebox_override("normal", s)
		p_btn.add_theme_stylebox_override("hover", s)
		p_btn.add_theme_stylebox_override("pressed", s)
		p_btn.add_theme_stylebox_override("focus", s)

		var idx := i
		p_btn.pressed.connect(func() -> void:
			_selected_terrain_preset_index = idx
			_apply_terrain_preset(preset)
			_highlight_terrain_preset_button(p_btn)
		)

		preset_grid.add_child(p_btn)
		_terrain_preset_buttons.append(p_btn)

	# 大陆数量选择区 (Continent Count Selector - 分段胶囊按钮)
	var count_vbox := VBoxContainer.new()
	count_vbox.add_theme_constant_override("separation", 6)
	rules_box.add_child(count_vbox)

	var count_title_row := HBoxContainer.new()
	count_title_row.add_theme_constant_override("separation", 8)
	count_vbox.add_child(count_title_row)

	var count_lbl := Label.new()
	_clean_label(count_lbl)
	count_lbl.text = "✦ " + tr("WORLD_CONFIG_CONTINENT_COUNT")
	count_lbl.add_theme_font_override("font", HEADER_FONT)
	count_lbl.add_theme_font_size_override("font_size", 12)
	count_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	count_title_row.add_child(count_lbl)
	_register_localized(count_lbl, "WORLD_CONFIG_CONTINENT_COUNT", [], "✦ ")

	var count_desc := Label.new()
	_clean_label(count_desc)
	count_desc.text = tr("WORLD_CONFIG_CONTINENT_COUNT_HINT")
	count_desc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	count_desc.add_theme_font_override("font", BODY_FONT)
	count_desc.add_theme_font_size_override("font_size", 10)
	count_desc.add_theme_color_override("font_color", COLOR_INK_DULL)
	count_title_row.add_child(count_desc)
	_register_localized(count_desc, "WORLD_CONFIG_CONTINENT_COUNT_HINT")

	var count_btn_row := HBoxContainer.new()
	count_btn_row.add_theme_constant_override("separation", 6)
	count_vbox.add_child(count_btn_row)

	_continent_count_buttons.clear()
	var supported_counts: Array[int] = [2, 3, 4, 5, 6, 7]
	for c_num: int in supported_counts:
		var c_btn := Button.new()
		_clean_button(c_btn)
		c_btn.text = tr("WORLD_CONFIG_CONTINENT_UNIT") % c_num
		c_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		c_btn.custom_minimum_size = Vector2(0, 32)
		c_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		c_btn.add_theme_font_override("font", BODY_FONT)
		c_btn.add_theme_font_size_override("font_size", 11)
		_skin_guard_button(c_btn)
		var this_count := c_num
		c_btn.pressed.connect(func() -> void:
			_on_continent_count_selected(this_count)
		)
		count_btn_row.add_child(c_btn)
		_continent_count_buttons.append(c_btn)
		_register_localized(c_btn, "WORLD_CONFIG_CONTINENT_UNIT", [c_num])
	_update_continent_count_buttons()

	var adv_sep := HSeparator.new()
	var sep_style := StyleBoxLine.new()
	sep_style.color = COLOR_BORDER_HAIRLINE
	adv_sep.add_theme_stylebox_override("separator", sep_style)
	rules_box.add_child(adv_sep)

	# 高级配置主工具栏 (Master Advanced Header)
	var adv_toolbar := HBoxContainer.new()
	adv_toolbar.add_theme_constant_override("separation", 8)
	rules_box.add_child(adv_toolbar)

	var master_toggle_btn := Button.new()
	_clean_button(master_toggle_btn)
	master_toggle_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	master_toggle_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	master_toggle_btn.custom_minimum_size = Vector2(0, 32)
	var tb_empty := StyleBoxEmpty.new()
	master_toggle_btn.add_theme_stylebox_override("normal", tb_empty)
	master_toggle_btn.add_theme_stylebox_override("hover", tb_empty)
	master_toggle_btn.add_theme_stylebox_override("pressed", tb_empty)
	master_toggle_btn.add_theme_stylebox_override("focus", tb_empty)

	var tb_hbox := HBoxContainer.new()
	tb_hbox.add_theme_constant_override("separation", 6)
	tb_hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	master_toggle_btn.add_child(tb_hbox)

	_advanced_master_arrow = Label.new()
	_clean_label(_advanced_master_arrow)
	_advanced_master_arrow.text = "▶"
	_advanced_master_arrow.add_theme_font_override("font", HEADER_FONT)
	_advanced_master_arrow.add_theme_font_size_override("font_size", 12)
	_advanced_master_arrow.add_theme_color_override("font_color", COLOR_ACCENT_FOREST)
	tb_hbox.add_child(_advanced_master_arrow)

	tb_hbox.add_child(_make_dot_indicator(Color(0.40, 0.45, 0.50)))

	var master_lbl := Label.new()
	_clean_label(master_lbl)
	master_lbl.text = "⚙ " + tr("WORLD_CONFIG_ADVANCED_TERRAIN")
	master_lbl.add_theme_font_override("font", HEADER_FONT)
	master_lbl.add_theme_font_size_override("font_size", 12)
	master_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	tb_hbox.add_child(master_lbl)
	_register_localized(master_lbl, "WORLD_CONFIG_ADVANCED_TERRAIN", [], "⚙ ")

	master_toggle_btn.pressed.connect(_toggle_master_advanced)
	adv_toolbar.add_child(master_toggle_btn)

	var expand_all_btn := _make_pill_button(tr("WORLD_CONFIG_EXPAND_ALL"))
	expand_all_btn.custom_minimum_size = Vector2(58, 26)
	expand_all_btn.add_theme_font_size_override("font_size", 10)
	expand_all_btn.pressed.connect(_expand_all_tables)
	adv_toolbar.add_child(expand_all_btn)
	_register_localized(expand_all_btn, "WORLD_CONFIG_EXPAND_ALL")

	var collapse_all_btn := _make_pill_button(tr("WORLD_CONFIG_COLLAPSE_ALL"))
	collapse_all_btn.custom_minimum_size = Vector2(58, 26)
	collapse_all_btn.add_theme_font_size_override("font_size", 10)
	collapse_all_btn.pressed.connect(_collapse_all_tables)
	adv_toolbar.add_child(collapse_all_btn)
	_register_localized(collapse_all_btn, "WORLD_CONFIG_COLLAPSE_ALL")

	var reset_adv_btn := _make_pill_button(tr("WORLD_CONFIG_RESET_DEFAULTS"))
	reset_adv_btn.custom_minimum_size = Vector2(52, 26)
	reset_adv_btn.add_theme_font_size_override("font_size", 10)
	reset_adv_btn.pressed.connect(_reset_advanced_terrain_defaults)
	adv_toolbar.add_child(reset_adv_btn)
	_register_localized(reset_adv_btn, "WORLD_CONFIG_RESET_DEFAULTS")

	# 高级配置独立折叠表格容器
	_advanced_terrain_content = VBoxContainer.new()
	_advanced_terrain_content.add_theme_constant_override("separation", 8)
	_advanced_terrain_content.visible = false
	_advanced_terrain_content.modulate.a = 0.0
	rules_box.add_child(_advanced_terrain_content)

	_collapsible_tables.clear()

	# 表格 1：流水与湿地水网 (Fluvial & Wetland Systems)
	var tbl1 := _create_collapsible_table(_advanced_terrain_content, "WORLD_CONFIG_TABLE_FLUVIAL", 5, Color(0.28, 0.54, 0.64), true)
	var rivers_row := HBoxContainer.new()
	rivers_row.add_theme_constant_override("separation", 10)
	tbl1.content.add_child(rivers_row)
	rivers_row.add_child(_make_dot_indicator(Color(0.28, 0.54, 0.64)))

	var river_label := Label.new()
	_clean_label(river_label)
	river_label.text = tr("WORLD_CONFIG_GENERATE_RIVERS")
	river_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	river_label.add_theme_font_override("font", BODY_FONT)
	river_label.add_theme_font_size_override("font_size", 12)
	river_label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	rivers_row.add_child(river_label)
	_register_localized(river_label, "WORLD_CONFIG_GENERATE_RIVERS")

	_rivers_check = CheckButton.new()
	_rivers_check.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_rivers_check.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_rivers_check.toggled.connect(_on_rivers_toggled)
	rivers_row.add_child(_rivers_check)

	for spec: Dictionary in TERRAIN_ROWS:
		if spec.get("table") == "fluvial":
			var slider := _add_artistic_slider(tbl1.content, spec)
			if spec.key == "river_density":
				slider.editable = false

	# 表格 2：溶蚀与峡谷地貌 (Erosion & Canyon Landforms)
	var tbl2 := _create_collapsible_table(_advanced_terrain_content, "WORLD_CONFIG_TABLE_EROSION", 3, Color(0.72, 0.48, 0.28), false)
	for spec: Dictionary in TERRAIN_ROWS:
		if spec.get("table") == "erosion":
			_add_artistic_slider(tbl2.content, spec)

	# 表格 3：气候风成与极地冰川 (Aeolian & Glacial Landforms)
	var tbl3 := _create_collapsible_table(_advanced_terrain_content, "WORLD_CONFIG_TABLE_AEOLIAN_GLACIAL", 3, Color(0.40, 0.65, 0.85), false)
	for spec: Dictionary in TERRAIN_ROWS:
		if spec.get("table") == "aeolian_glacial":
			_add_artistic_slider(tbl3.content, spec)

	# 表格 4：地质构造与高山岛屿 (Tectonic, Alpine & Islands)
	var tbl4 := _create_collapsible_table(_advanced_terrain_content, "WORLD_CONFIG_TABLE_TECTONIC_ALPINE", 6, Color(0.45, 0.60, 0.45), false)
	for spec: Dictionary in TERRAIN_ROWS:
		if spec.get("table") == "tectonic_alpine":
			_add_artistic_slider(tbl4.content, spec)

	# ---- 右栏：地形地貌参数浮动卡片群 (stretch 1.22) ----
	var right_scroll := ScrollContainer.new()
	right_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_scroll.size_flags_stretch_ratio = 1.22
	right_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_style_scrollbar(right_scroll)
	_terrain_page.add_child(right_scroll)

	var right_box := VBoxContainer.new()
	right_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_box.add_theme_constant_override("separation", 12)
	right_scroll.add_child(right_box)

	# 卡片 4：海洋与气候 (Ocean & Climate)
	var ocean_card := _create_floating_card(right_box, 0.0)
	var ocean_box := VBoxContainer.new()
	ocean_box.add_theme_constant_override("separation", 8)
	ocean_card.content.add_child(ocean_box)

	_add_card_title(ocean_box, "WORLD_CONFIG_SECTION_OCEAN_CLIMATE")
	for spec: Dictionary in TERRAIN_ROWS:
		if spec.section == "ocean":
			_add_artistic_slider(ocean_box, spec)

	# 卡片 5：山脉与地貌 (Mountains & Topography)
	var mountain_card := _create_floating_card(right_box, 0.0)
	var mountain_box := VBoxContainer.new()
	mountain_box.add_theme_constant_override("separation", 8)
	mountain_card.content.add_child(mountain_box)

	_add_card_title(mountain_box, "WORLD_CONFIG_SECTION_MOUNTAINS")
	for spec: Dictionary in TERRAIN_ROWS:
		if spec.section in ["mountain", "terrain"]:
			if spec.get("kind") == "segmented_gradient":
				_add_gradient_stepper(mountain_box, spec)
			else:
				_add_artistic_slider(mountain_box, spec)


func _on_map_size_selected(idx: int) -> void:
	_selected_map_size_index = idx
	_update_map_size_buttons()


func _update_map_size_buttons() -> void:
	for i in range(_map_size_buttons.size()):
		var btn := _map_size_buttons[i]
		_apply_pill_toggle_style(btn, i == _selected_map_size_index)


func _on_world_type_selected(idx: int) -> void:
	_selected_world_type_index = idx
	_update_world_type_buttons()


func _update_world_type_buttons() -> void:
	for i in range(_world_type_buttons.size()):
		var btn := _world_type_buttons[i]
		_apply_pill_toggle_style(btn, i == _selected_world_type_index)


func _on_continent_count_selected(cnt: int) -> void:
	_selected_continent_count = cnt
	if _terrain_controls.has("continent_count"):
		_terrain_controls["continent_count"].value = cnt
	_update_continent_count_buttons()


func _update_continent_count_buttons() -> void:
	var supported_counts: Array[int] = [2, 3, 4, 5, 6, 7]
	for i in range(_continent_count_buttons.size()):
		var btn: Button = _continent_count_buttons[i]
		_apply_pill_toggle_style(btn, supported_counts[i] == _selected_continent_count)


func _apply_terrain_preset(preset: Dictionary) -> void:
	var w_idx: int = preset.get("world_type_idx", 0)
	_on_world_type_selected(w_idx)
	_selected_continent_count = preset.get("continent_count", 5)
	_update_continent_count_buttons()

	var vals: Dictionary = preset.get("values", {})
	for k: String in vals:
		if _terrain_controls.has(k):
			var ctrl: Range = _terrain_controls[k]
			ctrl.value = vals[k]


func _highlight_terrain_preset_button(active_btn: Button) -> void:
	for btn: Button in _terrain_preset_buttons:
		var is_sel := (btn == active_btn)
		var s := StyleBoxFlat.new()
		s.set_border_width_all(1)
		s.set_corner_radius_all(8)
		if is_sel:
			s.bg_color = Color(0.98, 0.97, 0.92, 0.95)
			s.border_color = Color(0.72, 0.54, 0.20, 0.70)
			s.shadow_color = Color(0.72, 0.54, 0.20, 0.15)
			s.shadow_size = 4
		else:
			s.bg_color = Color(0.97, 0.96, 0.93, 0.65)
			s.border_color = COLOR_BORDER_HAIRLINE
		btn.add_theme_stylebox_override("normal", s)
		btn.add_theme_stylebox_override("hover", s)
		btn.add_theme_stylebox_override("pressed", s)
		btn.add_theme_stylebox_override("focus", s)


func _toggle_master_advanced() -> void:
	_is_advanced_terrain_expanded = not _is_advanced_terrain_expanded
	if _advanced_master_arrow != null:
		_advanced_master_arrow.text = "▼" if _is_advanced_terrain_expanded else "▶"
	if _advanced_terrain_content != null:
		if _is_advanced_terrain_expanded:
			_advanced_terrain_content.visible = true
			_advanced_terrain_content.modulate.a = 0.0
			create_tween().tween_property(_advanced_terrain_content, "modulate:a", 1.0, 0.18)
		else:
			var tw := create_tween()
			tw.tween_property(_advanced_terrain_content, "modulate:a", 0.0, 0.12)
			tw.tween_callback(func() -> void:
				_advanced_terrain_content.visible = false
			)


func _create_collapsible_table(parent: Container, title_key: String, count: int, dot_color: Color, default_expanded: bool) -> Dictionary:
	var table_panel := PanelContainer.new()
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.96, 0.97, 0.95, 0.65)
	panel_style.border_color = COLOR_BORDER_HAIRLINE
	panel_style.set_border_width_all(1)
	panel_style.set_corner_radius_all(8)
	panel_style.content_margin_left = 10
	panel_style.content_margin_right = 10
	panel_style.content_margin_top = 8
	panel_style.content_margin_bottom = 8
	table_panel.add_theme_stylebox_override("panel", panel_style)
	parent.add_child(table_panel)

	var table_vbox := VBoxContainer.new()
	table_vbox.add_theme_constant_override("separation", 6)
	table_panel.add_child(table_vbox)

	var header_btn := Button.new()
	_clean_button(header_btn)
	header_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	header_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header_btn.custom_minimum_size = Vector2(0, 28)

	var h_empty := StyleBoxEmpty.new()
	header_btn.add_theme_stylebox_override("normal", h_empty)
	header_btn.add_theme_stylebox_override("hover", h_empty)
	header_btn.add_theme_stylebox_override("pressed", h_empty)
	header_btn.add_theme_stylebox_override("focus", h_empty)

	var header_hbox := HBoxContainer.new()
	header_hbox.add_theme_constant_override("separation", 8)
	header_hbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header_hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	header_btn.add_child(header_hbox)

	var arrow_lbl := Label.new()
	_clean_label(arrow_lbl)
	arrow_lbl.text = "▼" if default_expanded else "▶"
	arrow_lbl.add_theme_font_override("font", HEADER_FONT)
	arrow_lbl.add_theme_font_size_override("font_size", 11)
	arrow_lbl.add_theme_color_override("font_color", COLOR_ACCENT_FOREST)
	header_hbox.add_child(arrow_lbl)

	header_hbox.add_child(_make_dot_indicator(dot_color))

	var title_lbl := Label.new()
	_clean_label(title_lbl)
	title_lbl.text = tr(title_key)
	title_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_lbl.add_theme_font_override("font", HEADER_FONT)
	title_lbl.add_theme_font_size_override("font_size", 12)
	title_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	header_hbox.add_child(title_lbl)
	_register_localized(title_lbl, title_key)

	var count_badge := _make_parchment_badge(tr("WORLD_CONFIG_ITEMS_COUNT") % count, Vector2(46, 20))
	header_hbox.add_child(count_badge)

	table_vbox.add_child(header_btn)

	var content_vbox := VBoxContainer.new()
	content_vbox.add_theme_constant_override("separation", 6)
	content_vbox.visible = default_expanded
	content_vbox.modulate.a = 1.0 if default_expanded else 0.0
	table_vbox.add_child(content_vbox)

	var table_dict := {
		"container": table_panel,
		"header": header_btn,
		"arrow": arrow_lbl,
		"content": content_vbox,
		"is_expanded": default_expanded,
		"tween": null,
	}

	header_btn.pressed.connect(func() -> void:
		_toggle_table(table_dict)
	)

	_collapsible_tables.append(table_dict)
	return table_dict


func _toggle_table(table_dict: Dictionary) -> void:
	var is_exp: bool = not table_dict.is_expanded
	_set_table_expanded(table_dict, is_exp, true)


func _set_table_expanded(table_dict: Dictionary, is_exp: bool, animate: bool = true) -> void:
	table_dict.is_expanded = is_exp
	var arrow: Label = table_dict.arrow
	arrow.text = "▼" if is_exp else "▶"
	var content: VBoxContainer = table_dict.content

	if table_dict.tween != null and (table_dict.tween as Tween).is_valid():
		(table_dict.tween as Tween).kill()

	if not animate:
		content.visible = is_exp
		content.modulate.a = 1.0 if is_exp else 0.0
		return

	var tw := create_tween().set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	table_dict.tween = tw
	if is_exp:
		content.visible = true
		content.modulate.a = 0.0
		tw.tween_property(content, "modulate:a", 1.0, 0.16)
	else:
		tw.tween_property(content, "modulate:a", 0.0, 0.12)
		tw.tween_callback(func() -> void:
			content.visible = false
		)


func _expand_all_tables() -> void:
	if not _is_advanced_terrain_expanded:
		_toggle_master_advanced()
	for tbl: Dictionary in _collapsible_tables:
		_set_table_expanded(tbl, true, true)


func _collapse_all_tables() -> void:
	for tbl: Dictionary in _collapsible_tables:
		_set_table_expanded(tbl, false, true)


func _select_terrain_preset_by_index(idx: int) -> void:
	if idx >= 0 and idx < TERRAIN_PRESETS.size():
		_selected_terrain_preset_index = idx
		_apply_terrain_preset(TERRAIN_PRESETS[idx])
		if idx < _terrain_preset_buttons.size():
			_highlight_terrain_preset_button(_terrain_preset_buttons[idx])


func _reset_advanced_terrain_defaults() -> void:
	_select_terrain_preset_by_index(0)


func _apply_pill_toggle_style(btn: Button, is_active: bool) -> void:
	# 守卫按钮素材：选中态用按下蓝面（金底边）配浅色文字，未选用常规木面配墨色文字。
	_guard_selected[btn] = is_active
	_refresh_guard_bg(btn)
	if is_active:
		btn.add_theme_color_override("font_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_hover_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_pressed_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_focus_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_font_override("font", HEADER_FONT)
	else:
		btn.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
		btn.add_theme_color_override("font_hover_color", COLOR_INK_PRIMARY)
		btn.add_theme_color_override("font_pressed_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_focus_color", COLOR_INK_PRIMARY)
		btn.add_theme_font_override("font", BODY_FONT)


func _on_rivers_toggled(pressed: bool) -> void:
	if _terrain_controls.has("river_density"):
		_terrain_controls["river_density"].editable = pressed


func _on_random_seed_pressed() -> void:
	var rand_val := randi() % 100000000
	_seed_edit.text = str(rand_val)
	_committed_seed = rand_val


func _on_apply_seed_pressed() -> void:
	_apply_seed_text()


func _apply_seed_text() -> void:
	if _seed_edit == null:
		return
	var raw := _seed_edit.text.strip_edges()
	if raw.is_empty():
		_committed_seed = -1
	elif raw.is_valid_int():
		_committed_seed = raw.to_int()
	else:
		_committed_seed = hash(raw)


# ------------------------------------------------------------- 第二步：初始资源配置构建

func _build_resources_page() -> void:
	for child in _resources_page.get_children():
		child.queue_free()

	# ---- 左栏：资源预设与预算总览 (stretch 1.0) ----
	var left_col := VBoxContainer.new()
	left_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left_col.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_col.size_flags_stretch_ratio = 1.0
	left_col.add_theme_constant_override("separation", 12)
	_resources_page.add_child(left_col)

	# 资源预设卡片
	var preset_card := _create_floating_card(left_col, 0.0)
	var preset_box := VBoxContainer.new()
	preset_box.add_theme_constant_override("separation", 8)
	preset_card.content.add_child(preset_box)

	_add_card_title(preset_box, "WORLD_CONFIG_SECTION_PRESETS")

	var p_grid := GridContainer.new()
	p_grid.columns = 2
	p_grid.add_theme_constant_override("h_separation", 8)
	p_grid.add_theme_constant_override("v_separation", 8)
	preset_box.add_child(p_grid)

	_preset_buttons.clear()
	for preset: Dictionary in RESOURCE_PRESETS:
		var p_btn := Button.new()
		_clean_button(p_btn)
		p_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		p_btn.custom_minimum_size = Vector2(0, 52)
		p_btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

		var p_content := VBoxContainer.new()
		p_content.alignment = BoxContainer.ALIGNMENT_CENTER
		p_content.add_theme_constant_override("separation", 2)
		p_btn.add_child(p_content)

		var t_lbl := Label.new()
		_clean_label(t_lbl)
		t_lbl.text = "✦ " + tr(preset.name_key)
		t_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		t_lbl.add_theme_font_override("font", HEADER_FONT)
		t_lbl.add_theme_font_size_override("font_size", 12)
		t_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
		p_content.add_child(t_lbl)
		_register_localized(t_lbl, preset.name_key, [], "✦ ")

		var d_lbl := Label.new()
		_clean_label(d_lbl)
		d_lbl.text = tr(preset.desc_key)
		d_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		d_lbl.add_theme_font_override("font", BODY_FONT)
		d_lbl.add_theme_font_size_override("font_size", 10)
		d_lbl.add_theme_color_override("font_color", COLOR_INK_DULL)
		p_content.add_child(d_lbl)
		_register_localized(d_lbl, preset.desc_key)

		var p_values: Dictionary = preset.values
		p_btn.pressed.connect(func() -> void:
			_apply_resource_preset(p_values)
			_highlight_preset_button(p_btn)
		)

		var s := StyleBoxFlat.new()
		s.bg_color = Color(1.0, 1.0, 1.0, 0.60)
		s.border_color = COLOR_BORDER_HAIRLINE
		s.set_border_width_all(1)
		s.set_corner_radius_all(10)
		p_btn.add_theme_stylebox_override("normal", s)
		p_btn.add_theme_stylebox_override("hover", s)
		p_btn.add_theme_stylebox_override("pressed", s)
		p_btn.add_theme_stylebox_override("focus", s)

		p_grid.add_child(p_btn)
		_preset_buttons.append(p_btn)

	# 预算指示卡片 (Budget Card)
	_budget_card = _create_floating_card(left_col, 1.0)
	var b_box := VBoxContainer.new()
	b_box.add_theme_constant_override("separation", 10)
	_budget_card.content.add_child(b_box)

	_add_card_title(b_box, "WORLD_CONFIG_SECTION_BUDGET")

	var b_header := HBoxContainer.new()
	b_header.alignment = BoxContainer.ALIGNMENT_CENTER
	b_box.add_child(b_header)

	_budget_used_label = RollingNumberLabel.new()
	_budget_used_label.number_format = "%d"
	_clean_label(_budget_used_label)
	_budget_used_label.add_theme_font_override("font", HEADER_FONT)
	_budget_used_label.add_theme_font_size_override("font_size", 34)
	_budget_used_label.add_theme_color_override("font_color", COLOR_ACCENT_FOREST)
	b_header.add_child(_budget_used_label)

	var slash_lbl := Label.new()
	_clean_label(slash_lbl)
	slash_lbl.text = " / %d" % RESOURCE_BUDGET
	slash_lbl.add_theme_font_override("font", HEADER_FONT)
	slash_lbl.add_theme_font_size_override("font_size", 18)
	slash_lbl.add_theme_color_override("font_color", COLOR_INK_MUTED)
	b_header.add_child(slash_lbl)

	# 预算进度条 (StyleBoxFlat 自绘，干净精致)
	_budget_progress_bar = ProgressBar.new()
	_budget_progress_bar.max_value = RESOURCE_BUDGET
	_budget_progress_bar.show_percentage = false
	_budget_progress_bar.custom_minimum_size = Vector2(0, 14)

	var bg_bar := StyleBoxFlat.new()
	bg_bar.bg_color = Color(0.18, 0.25, 0.22, 0.10)
	bg_bar.set_corner_radius_all(7)
	_budget_progress_bar.add_theme_stylebox_override("background", bg_bar)

	var fg_bar := StyleBoxFlat.new()
	fg_bar.bg_color = COLOR_ACCENT_MOSS
	fg_bar.set_corner_radius_all(7)
	_budget_progress_bar.add_theme_stylebox_override("fill", fg_bar)
	b_box.add_child(_budget_progress_bar)

	var b_status_row := HBoxContainer.new()
	b_box.add_child(b_status_row)

	_budget_remain_label = Label.new()
	_clean_label(_budget_remain_label)
	_budget_remain_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_budget_remain_label.add_theme_font_override("font", BODY_FONT)
	_budget_remain_label.add_theme_font_size_override("font_size", 12)
	_budget_remain_label.add_theme_color_override("font_color", COLOR_INK_MUTED)
	b_status_row.add_child(_budget_remain_label)

	_budget_over_label = Label.new()
	_clean_label(_budget_over_label)
	_budget_over_label.text = "WORLD_CONFIG_BUDGET_OVER"
	_budget_over_label.add_theme_font_override("font", HEADER_FONT)
	_budget_over_label.add_theme_font_size_override("font_size", 12)
	_budget_over_label.add_theme_color_override("font_color", COLOR_DANGER)
	_budget_over_label.visible = false
	_register_localized(_budget_over_label, "WORLD_CONFIG_BUDGET_OVER")
	b_status_row.add_child(_budget_over_label)

	var b_rule_lbl := Label.new()
	_clean_label(b_rule_lbl)
	b_rule_lbl.text = tr("WORLD_CONFIG_BUDGET_RULE")
	b_rule_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	b_rule_lbl.add_theme_font_override("font", BODY_FONT)
	b_rule_lbl.add_theme_font_size_override("font_size", 11)
	b_rule_lbl.add_theme_color_override("font_color", COLOR_INK_DULL)
	b_box.add_child(b_rule_lbl)
	_register_localized(b_rule_lbl, "WORLD_CONFIG_BUDGET_RULE")

	# ---- 右栏：资源与文明法则浮动卡片群 (stretch 1.22) ----
	var right_scroll := ScrollContainer.new()
	right_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_scroll.size_flags_stretch_ratio = 1.22
	right_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_style_scrollbar(right_scroll)
	_resources_page.add_child(right_scroll)

	var right_box := VBoxContainer.new()
	right_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_box.add_theme_constant_override("separation", 12)
	right_scroll.add_child(right_box)

	# 卡片 1：战略资源明细
	var res_card := _create_floating_card(right_box, 0.0)
	var res_card_box := VBoxContainer.new()
	res_card_box.add_theme_constant_override("separation", 10)
	res_card.content.add_child(res_card_box)

	_add_card_title(res_card_box, "WORLD_CONFIG_SECTION_RESOURCE_DETAIL")

	var res_box := VBoxContainer.new()
	res_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	res_box.add_theme_constant_override("separation", 6)
	res_card_box.add_child(res_box)

	_resource_spins.clear()
	_resource_subtotal_labels.clear()

	for spec: Dictionary in RESOURCE_SPECS:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)
		res_box.add_child(row)

		# 雅致的水墨色点指示标（完全使用纯代码 StyleBoxFlat 绘制，无碎片贴图依赖）
		row.add_child(_make_dot_indicator(spec.get("color", Color(0.4, 0.5, 0.45))))

		var name_lbl := Label.new()
		_clean_label(name_lbl)
		name_lbl.custom_minimum_size = Vector2(80, 0)
		name_lbl.add_theme_font_override("font", HEADER_FONT)
		name_lbl.add_theme_font_size_override("font_size", 13)
		name_lbl.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
		row.add_child(name_lbl)
		_register_localized(name_lbl, spec.key)

		var cost_lbl := Label.new()
		_clean_label(cost_lbl)
		var cost_fmt := tr("WORLD_CONFIG_UNIT_COST")
		cost_lbl.text = (cost_fmt % int(spec.cost)) if "%" in cost_fmt else (cost_fmt + " " + str(spec.cost))
		cost_lbl.custom_minimum_size = Vector2(65, 0)
		cost_lbl.add_theme_font_override("font", BODY_FONT)
		cost_lbl.add_theme_font_size_override("font_size", 11)
		cost_lbl.add_theme_color_override("font_color", COLOR_INK_MUTED)
		row.add_child(cost_lbl)
		_register_localized(cost_lbl, "WORLD_CONFIG_UNIT_COST", [int(spec.cost)])

		var spacer := Control.new()
		spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(spacer)

		var spin := SpinBox.new()
		spin.min_value = 0
		spin.max_value = spec.max
		spin.step = 1
		spin.value = spec.default
		spin.custom_minimum_size = Vector2(84, 30)
		_style_spin(spin)
		spin.value_changed.connect(_on_resource_value_changed)
		row.add_child(spin)
		_resource_spins[spec.id] = spin

		var subtotal_badge := _make_parchment_badge("0 pt", Vector2(60, 24))
		var subtotal_lbl: Label = subtotal_badge.get_child(0)
		row.add_child(subtotal_badge)
		_resource_subtotal_labels[spec.id] = subtotal_lbl

	# 卡片 2：以太法则与文明生态 (Aether & World Laws)
	var laws_card := _create_floating_card(right_box, 0.0)
	var laws_box := VBoxContainer.new()
	laws_box.add_theme_constant_override("separation", 8)
	laws_card.content.add_child(laws_box)

	_add_card_title(laws_box, "WORLD_CONFIG_SECTION_LAWS")

	_magic_slider = _add_artistic_slider(laws_box, {
		"key": "magic_density",
		"label": "WORLD_CONFIG_MAGIC_DENSITY",
		"icon": "water",
		"min": 0.0,
		"max": 100.0,
		"step": 1.0,
		"default": 75.0,
		"fmt": "%d",
		"int": true,
		"color": Color(0.55, 0.40, 0.72)
	})

	_aggression_slider = _add_artistic_slider(laws_box, {
		"key": "civil_aggression",
		"label": "WORLD_CONFIG_CIVIL_AGGRESSION",
		"icon": "mountain",
		"min": 0.0,
		"max": 100.0,
		"step": 1.0,
		"default": 42.0,
		"fmt": "%d",
		"int": true,
		"color": Color(0.72, 0.35, 0.28)
	})

	_diversity_slider = _add_artistic_slider(laws_box, {
		"key": "species_diversity",
		"label": "WORLD_CONFIG_SPECIES_DIVERSITY",
		"icon": "acorn",
		"min": 0.0,
		"max": 100.0,
		"step": 1.0,
		"default": 68.0,
		"fmt": "%d",
		"int": true,
		"color": Color(0.36, 0.58, 0.42)
	})

	_build_epoch_row(laws_box)
	_build_tile_mode_row(laws_box)


func _build_epoch_row(parent: Container) -> void:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	parent.add_child(box)

	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 8)
	box.add_child(top_row)

	top_row.add_child(_make_dot_indicator(Color(0.70, 0.52, 0.24)))

	var label := Label.new()
	_clean_label(label)
	label.text = tr("WORLD_CONFIG_STARTING_EPOCH")
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_override("font", BODY_FONT)
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	top_row.add_child(label)
	_register_localized(label, "WORLD_CONFIG_STARTING_EPOCH")

	var val_badge := _make_parchment_badge("450 (" + tr("WORLD_EPOCH_ERA_CITY") + ")", Vector2(130, 22))
	_epoch_badge_label = val_badge.get_child(0)
	top_row.add_child(val_badge)

	var slider := HSlider.new()
	slider.min_value = 50.0
	slider.max_value = 1000.0
	slider.step = 10.0
	slider.value = 450.0
	slider.custom_minimum_size = Vector2(0, 20)
	slider.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

	var empty := StyleBoxEmpty.new()
	slider.add_theme_stylebox_override("slider", empty)
	slider.add_theme_stylebox_override("grabber_area", empty)
	slider.add_theme_stylebox_override("grabber_area_highlight", empty)
	var stub := _grabber_stub_tex()
	slider.add_theme_icon_override("grabber", stub)
	slider.add_theme_icon_override("grabber_highlight", stub)
	slider.add_theme_icon_override("grabber_disabled", stub)

	var icon_set: Dictionary = SLIDER_ICON_SETS["acorn"]
	var skin := ThemedSliderSkin.new()
	skin.slider = slider
	skin.track_tex = SLIDER_TRACK_TEX
	skin.fill_tex = SLIDER_FILL_TEX
	skin.grabber_tex = icon_set.normal
	skin.grabber_hl_tex = icon_set.hover
	skin.particle_theme = icon_set.particles
	slider.add_child(skin)

	slider.value_changed.connect(func(val: float) -> void:
		var ep := int(round(val))
		var era_key := "WORLD_EPOCH_ERA_PRIMITIVE"
		if ep > 500:
			era_key = "WORLD_EPOCH_ERA_EMPIRE"
		elif ep > 100:
			era_key = "WORLD_EPOCH_ERA_CITY"
		_epoch_badge_label.text = "%d (%s)" % [ep, tr(era_key)]
	)

	box.add_child(slider)
	_epoch_slider = slider


func _build_tile_mode_row(parent: Container) -> void:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	parent.add_child(box)

	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 8)
	box.add_child(top_row)

	top_row.add_child(_make_dot_indicator(COLOR_ACCENT_FOREST))

	var label := Label.new()
	_clean_label(label)
	label.text = tr("WORLD_CONFIG_TILE_MODE")
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_override("font", BODY_FONT)
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	top_row.add_child(label)
	_register_localized(label, "WORLD_CONFIG_TILE_MODE")

	var btn_row := HBoxContainer.new()
	btn_row.add_theme_constant_override("separation", 6)
	box.add_child(btn_row)

	_tile_mode_buttons.clear()
	for i in range(TILE_MODE_KEYS.size()):
		var btn := Button.new()
		_clean_button(btn)
		btn.text = tr(TILE_MODE_KEYS[i])
		btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn.custom_minimum_size = Vector2(0, 32)
		btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		btn.add_theme_font_override("font", BODY_FONT)
		btn.add_theme_font_size_override("font_size", 11)
		var idx := i
		_skin_guard_button(btn)
		btn.pressed.connect(func() -> void: _on_tile_mode_selected(idx))
		btn_row.add_child(btn)
		_tile_mode_buttons.append(btn)
		_register_localized(btn, TILE_MODE_KEYS[i])
	_update_tile_mode_buttons()


func _on_tile_mode_selected(idx: int) -> void:
	_selected_tile_mode_index = idx
	_update_tile_mode_buttons()


func _update_tile_mode_buttons() -> void:
	for i in range(_tile_mode_buttons.size()):
		var btn := _tile_mode_buttons[i]
		_apply_pill_toggle_style(btn, i == _selected_tile_mode_index)


func _highlight_preset_button(active_btn: Button) -> void:
	for btn: Button in _preset_buttons:
		var is_sel := (btn == active_btn)
		var s := StyleBoxFlat.new()
		s.set_border_width_all(1)
		s.set_corner_radius_all(10)
		if is_sel:
			s.bg_color = Color(0.98, 0.97, 0.92, 0.95)
			s.border_color = Color(0.72, 0.54, 0.20, 0.6)
			s.shadow_color = Color(0.72, 0.54, 0.20, 0.15)
			s.shadow_size = 4
		else:
			s.bg_color = Color(1.0, 1.0, 1.0, 0.60)
			s.border_color = COLOR_BORDER_HAIRLINE
		btn.add_theme_stylebox_override("normal", s)
		btn.add_theme_stylebox_override("hover", s)
		btn.add_theme_stylebox_override("pressed", s)
		btn.add_theme_stylebox_override("focus", s)


func _apply_resource_preset(values: Dictionary) -> void:
	for id: String in values:
		if _resource_spins.has(id):
			_resource_spins[id].value = values[id]
	_refresh_budget()


func _on_resource_value_changed(_value: float) -> void:
	_refresh_budget()


func _refresh_budget() -> void:
	var total := 0
	for spec: Dictionary in RESOURCE_SPECS:
		var count: int = int(_resource_spins[spec.id].value) if _resource_spins.has(spec.id) else 0
		var sub := count * int(spec.cost)
		total += sub
		if _resource_subtotal_labels.has(spec.id):
			_resource_subtotal_labels[spec.id].text = "%d pt" % sub

		if _budget_used_label != null:
			_budget_used_label.value = float(total)
	if _budget_progress_bar != null:
		_budget_progress_bar.value = min(total, RESOURCE_BUDGET)

	var is_over := total > RESOURCE_BUDGET
	if _budget_over_label != null:
		_budget_over_label.visible = is_over
	if _budget_remain_label != null:
		var remain := RESOURCE_BUDGET - total
		_budget_remain_label.text = tr("WORLD_CONFIG_BUDGET_REMAIN") % max(0, remain)
		_budget_remain_label.visible = not is_over

		if _generate_button != null:
			_generate_button.disabled = is_over


func _is_budget_valid() -> bool:
	var total := 0
	for spec: Dictionary in RESOURCE_SPECS:
		var count: int = int(_resource_spins[spec.id].value) if _resource_spins.has(spec.id) else 0
		total += count * int(spec.cost)
	return total <= RESOURCE_BUDGET


func _build_config() -> Dictionary:
	var dims := {
		&"Mini": Vector2i(256, 128),
		&"Small": Vector2i(512, 256),
		&"Medium": Vector2i(1024, 512),
		&"Large": Vector2i(2048, 1024),
		&"Huge": Vector2i(3072, 1536),
		&"Extreme": Vector2i(4096, 2048),
	}
	var cur_size: StringName = MAP_SIZES[_selected_map_size_index]
	var dim: Vector2i = dims.get(cur_size, Vector2i(2048, 1024))

	var cfg := {
		"map_size": cur_size,
		"width": dim.x,
		"height": dim.y,
		"world_type": WORLD_TYPES[_selected_world_type_index],
		"continent_count": int(_terrain_controls["continent_count"].value) if _terrain_controls.has("continent_count") else _selected_continent_count,
		"seed": _committed_seed,
		"generate_rivers": _rivers_check.button_pressed if _rivers_check != null else true,
		"enable_rivers": _rivers_check.button_pressed if _rivers_check != null else true,
	}

	for spec: Dictionary in TERRAIN_ROWS:
		if _terrain_controls.has(spec.key):
			var ctrl: Range = _terrain_controls[spec.key]
			if spec.get("int", false):
				cfg[spec.key] = int(round(ctrl.value))
			else:
				cfg[spec.key] = ctrl.value

	var res_dict := {}
	for spec: Dictionary in RESOURCE_SPECS:
		if _resource_spins.has(spec.id):
			res_dict[spec.id] = int(_resource_spins[spec.id].value)
	cfg["resources"] = res_dict

	cfg["magic_density"] = int(_magic_slider.value) if _magic_slider != null else 75
	cfg["civil_aggression"] = int(_aggression_slider.value) if _aggression_slider != null else 42
	cfg["species_diversity"] = int(_diversity_slider.value) if _diversity_slider != null else 68
	cfg["initial_epoch"] = int(_epoch_slider.value) if _epoch_slider != null else 450
	cfg["polygon_tile_mode"] = _selected_tile_mode_index
	return cfg


func _reset_values() -> void:
	_selected_map_size_index = DEFAULT_MAP_SIZE_INDEX
	_selected_world_type_index = 0
	_committed_seed = -1
	if _seed_edit != null:
		_seed_edit.text = ""

	for spec: Dictionary in TERRAIN_ROWS:
		if _terrain_controls.has(spec.key):
			_terrain_controls[spec.key].value = spec.default

	if _rivers_check != null:
		_rivers_check.button_pressed = true

	if _magic_slider != null:
		_magic_slider.value = 75.0
	if _aggression_slider != null:
		_aggression_slider.value = 42.0
	if _diversity_slider != null:
		_diversity_slider.value = 68.0
	if _epoch_slider != null:
		_epoch_slider.value = 450.0
	_selected_tile_mode_index = 0
	_update_tile_mode_buttons()

	_update_map_size_buttons()
	_update_world_type_buttons()

	# 默认应用首个地形预设
	_select_terrain_preset_by_index(0)

	# 默认应用首个资源预设
	if not RESOURCE_PRESETS.is_empty():
		_apply_resource_preset(RESOURCE_PRESETS[0].values)
		if not _preset_buttons.is_empty():
			_highlight_preset_button(_preset_buttons[0])


func _update_step_label() -> void:
	if _step_label == null:
		return
	var fmt: String = tr("WORLD_CONFIG_STEP_OF")
	_step_label.text = fmt % [(_current_step + 1), STEP_COUNT]


func _refresh_texts() -> void:
	for entry: Dictionary in _localized:
		var node: Control = entry.node
		if is_instance_valid(node) and entry.has("key"):
			var key: String = entry.key
			# 装饰前缀拼接在译文之前；LineEdit 登记的是占位提示文案。
			var text: String = entry.get("prefix", "") + tr(key)
			if entry.has("args") and not entry.args.is_empty():
				if "%" in text:
					text = text % entry.args
			if node is Label:
				(node as Label).text = text
			elif node is Button:
				(node as Button).text = text
			elif node is LineEdit:
				(node as LineEdit).placeholder_text = text
	_apply_step_texts()
	_update_step_label()
	# 页签文字宽度随译文变化，切换组件需要重排滑块与纸签位置。
	_step_switcher.request_relayout()
	# 预算剩余文案带数值占位，语言刷新后按当前数值重写。
	_refresh_budget()
	if _epoch_badge_label != null and _epoch_slider != null:
		var ep := int(round(_epoch_slider.value))
		var era_key := "WORLD_EPOCH_ERA_PRIMITIVE"
		if ep > 500:
			era_key = "WORLD_EPOCH_ERA_EMPIRE"
		elif ep > 100:
			era_key = "WORLD_EPOCH_ERA_CITY"
		_epoch_badge_label.text = "%d (%s)" % [ep, tr(era_key)]


## 应用当前步骤的标题与介绍（语言切换时由 _refresh_texts 重新调用）。
func _apply_step_texts() -> void:
	if _title != null:
		_title.text = tr(_title_key)
	if _subtitle != null:
		_subtitle.text = tr(_subtitle_key)


func _register_localized(node: Control, key: String, args: Array = [], prefix: String = "") -> void:
	# prefix 为不参与翻译的装饰前缀（如卡片标题的 ✦、骰子按钮的 🎲）。
	var entry := {"node": node, "key": key, "prefix": prefix}
	if not args.is_empty():
		entry["args"] = args
	_localized.append(entry)


# ------------------------------------------------------------- 控件构建与程序化水墨样式

## 纯代码绘制的水墨色微圆点（代替已被废弃的碎切片图标，无任何外置贴图依赖）
func _make_dot_indicator(color: Color) -> Control:
	var dot_box := CenterContainer.new()
	dot_box.custom_minimum_size = Vector2(16, 16)

	var dot := Panel.new()
	dot.custom_minimum_size = Vector2(7, 7)
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_corner_radius_all(4)
	dot.add_theme_stylebox_override("panel", s)
	dot.mouse_filter = Control.MOUSE_FILTER_IGNORE

	dot_box.add_child(dot)
	return dot_box


func _add_card_title(parent: Container, key: String) -> void:
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 10)
	parent.add_child(hbox)

	var t_label := Label.new()
	_clean_label(t_label)
	# 标题文案走 i18n（key），✦ 为统一装饰前缀不参与翻译。
	t_label.text = "✦ " + tr(key)
	t_label.add_theme_font_override("font", HEADER_FONT)
	t_label.add_theme_font_size_override("font_size", 14)
	t_label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	hbox.add_child(t_label)
	_register_localized(t_label, key, [], "✦ ")


func _add_card_subtitle(parent: Container, key: String) -> void:
	var sep := HSeparator.new()
	var sep_style := StyleBoxLine.new()
	sep_style.color = COLOR_BORDER_HAIRLINE
	sep.add_theme_stylebox_override("separator", sep_style)
	parent.add_child(sep)

	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 8)
	parent.add_child(hbox)

	var sub_label := Label.new()
	_clean_label(sub_label)
	sub_label.text = "◈ " + tr(key)
	sub_label.add_theme_font_override("font", HEADER_FONT)
	sub_label.add_theme_font_size_override("font_size", 12)
	sub_label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	hbox.add_child(sub_label)
	_register_localized(sub_label, key, [], "◈ ")


## 透明占位把手图标：屏蔽主题默认把手绘制，真实把手由 ThemedSliderSkin 自绘。
var _grabber_stub: ImageTexture


func _grabber_stub_tex() -> ImageTexture:
	if _grabber_stub == null:
		var img := Image.create(24, 24, false, Image.FORMAT_RGBA8)
		img.fill(Color(0, 0, 0, 0))
		_grabber_stub = ImageTexture.create_from_image(img)
	return _grabber_stub


## 构建带有数值水墨徽章与平滑滑块的参数行
func _add_artistic_slider(parent: Container, spec: Dictionary) -> Range:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	parent.add_child(box)

	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 8)
	box.add_child(top_row)

	var row_color: Color = spec.get("color", COLOR_ACCENT_MOSS)
	top_row.add_child(_make_dot_indicator(row_color))

	var label := Label.new()
	_clean_label(label)
	label.text = spec.label
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_override("font", BODY_FONT)
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	top_row.add_child(label)
	_register_localized(label, spec.label)

	var fmt: String = spec.get("fmt", "%.2f")
	var val_badge := _make_parchment_badge(fmt % spec.default, Vector2(56, 22))
	var val_lbl: Label = val_badge.get_child(0)
	top_row.add_child(val_badge)

	var slider := HSlider.new()
	slider.min_value = spec.min
	slider.max_value = spec.max
	slider.step = spec.step
	slider.value = spec.default
	slider.custom_minimum_size = Vector2(0, 20)
	slider.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

	# 主题水彩皮肤：超采样自绘轨道/填充/把手 + 按主题的拖拽粒子。
	# StyleBoxTexture 无法超采样、Slider 的 grabber 图标按原始尺寸绘制，
	# 因此滑条自身的样式盒/图标全部置空，视觉完全交给 ThemedSliderSkin。
	var empty := StyleBoxEmpty.new()
	slider.add_theme_stylebox_override("slider", empty)
	slider.add_theme_stylebox_override("grabber_area", empty)
	slider.add_theme_stylebox_override("grabber_area_highlight", empty)
	var stub := _grabber_stub_tex()
	slider.add_theme_icon_override("grabber", stub)
	slider.add_theme_icon_override("grabber_highlight", stub)
	slider.add_theme_icon_override("grabber_disabled", stub)

	var icon_set: Dictionary = SLIDER_ICON_SETS.get(spec.get("icon", "acorn"), SLIDER_ICON_SETS["acorn"])
	var skin := ThemedSliderSkin.new()
	skin.slider = slider
	skin.track_tex = SLIDER_TRACK_TEX
	skin.fill_tex = SLIDER_FILL_TEX
	skin.grabber_tex = icon_set.normal
	skin.grabber_hl_tex = icon_set.hover
	skin.particle_theme = icon_set.particles
	slider.add_child(skin)

	slider.value_changed.connect(func(val: float) -> void:
		val_lbl.text = fmt % val
		if spec.key == "continent_count":
			_selected_continent_count = int(round(val))
			_update_continent_count_buttons()
	)

	box.add_child(slider)
	_terrain_controls[spec.key] = slider
	return slider


## 构建梯度分段步进器（山脉梯度）
func _add_gradient_stepper(parent: Container, spec: Dictionary) -> Range:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	parent.add_child(box)

	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 8)
	box.add_child(top_row)

	top_row.add_child(_make_dot_indicator(Color(0.35, 0.55, 0.40)))

	var label := Label.new()
	_clean_label(label)
	label.text = spec.label
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_override("font", BODY_FONT)
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	top_row.add_child(label)
	_register_localized(label, spec.label)

	var val_badge := _make_parchment_badge(str(int(spec.default)), Vector2(40, 22))
	var val_lbl: Label = val_badge.get_child(0)
	top_row.add_child(val_badge)

	var btn_row := HBoxContainer.new()
	btn_row.add_theme_constant_override("separation", 6)
	box.add_child(btn_row)

	var dummy_range := HSlider.new()
	dummy_range.min_value = spec.min
	dummy_range.max_value = spec.max
	dummy_range.step = spec.step
	dummy_range.value = spec.default
	dummy_range.visible = false
	box.add_child(dummy_range)

	_gradient_buttons.clear()
	for i in range(int(spec.min), int(spec.max) + 1):
		var b := Button.new()
		_clean_button(b)
		b.text = tr(GRADIENT_KEYS[i - 1])
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.custom_minimum_size = Vector2(0, 32)
		b.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		b.add_theme_font_override("font", BODY_FONT)
		b.add_theme_font_size_override("font_size", 11)
		var val := float(i)
		b.pressed.connect(func() -> void:
			dummy_range.value = val
			val_lbl.text = str(int(val))
			_update_gradient_buttons(val)
		)
		_skin_guard_button(b)
		btn_row.add_child(b)
		_gradient_buttons.append(b)
		_register_localized(b, GRADIENT_KEYS[i - 1])

	_update_gradient_buttons(spec.default)
	_terrain_controls[spec.key] = dummy_range
	return dummy_range


func _update_gradient_buttons(current_val: float) -> void:
	for i in range(_gradient_buttons.size()):
		var b := _gradient_buttons[i]
		_apply_pill_toggle_style(b, (i + 1) == int(current_val))


func _make_parchment_badge(text: String, min_size: Vector2) -> PanelContainer:
	var pc := PanelContainer.new()
	pc.custom_minimum_size = min_size
	pc.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var s := StyleBoxFlat.new()
	s.bg_color = COLOR_CARD_PARCHMENT
	s.border_color = COLOR_BORDER_HAIRLINE
	s.set_border_width_all(1)
	s.set_corner_radius_all(6)
	s.content_margin_left = 6
	s.content_margin_right = 6
	s.content_margin_top = 2
	s.content_margin_bottom = 2
	pc.add_theme_stylebox_override("panel", s)

	var l := Label.new()
	_clean_label(l)
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	l.add_theme_font_override("font", HEADER_FONT)
	l.add_theme_font_size_override("font_size", 11)
	l.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	pc.add_child(l)
	return pc


func _style_spin(spin: SpinBox) -> void:
	spin.alignment = HORIZONTAL_ALIGNMENT_CENTER
	var line_edit := spin.get_line_edit()
	if line_edit != null:
		_style_seed_edit(line_edit)


func _style_seed_edit(edit: LineEdit) -> void:
	var s := StyleBoxFlat.new()
	s.bg_color = COLOR_FIELD_BG
	s.border_color = COLOR_BORDER_HAIRLINE
	s.set_border_width_all(1)
	s.set_corner_radius_all(8)
	s.content_margin_left = 10
	s.content_margin_right = 10
	s.content_margin_top = 4
	s.content_margin_bottom = 4

	var s_focus := StyleBoxFlat.new()
	s_focus.bg_color = COLOR_FIELD_HOVER
	s_focus.border_color = COLOR_BORDER_FOCUS
	s_focus.set_border_width_all(1)
	s_focus.set_corner_radius_all(8)
	s_focus.content_margin_left = 10
	s_focus.content_margin_right = 10
	s_focus.content_margin_top = 4
	s_focus.content_margin_bottom = 4

	edit.add_theme_stylebox_override("normal", s)
	edit.add_theme_stylebox_override("focus", s_focus)
	edit.add_theme_font_override("font", BODY_FONT)
	edit.add_theme_font_size_override("font_size", 12)
	edit.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	edit.add_theme_color_override("font_placeholder_color", COLOR_INK_DULL)


func _make_pill_button(label_text: String) -> Button:
	# 守卫按钮素材三态：木面常态/提亮悬浮/按下蓝面（超采样皮肤）。
	var b := Button.new()
	_clean_button(b)
	b.text = label_text
	b.custom_minimum_size = Vector2(0, 34)
	b.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_skin_guard_button(b)
	b.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	b.add_theme_color_override("font_hover_color", COLOR_INK_PRIMARY)
	b.add_theme_color_override("font_pressed_color", GUARD_BTN_ACTIVE_FONT)
	b.add_theme_color_override("font_focus_color", COLOR_INK_PRIMARY)
	b.add_theme_font_override("font", HEADER_FONT)
	b.add_theme_font_size_override("font_size", 12)
	return b


func _on_dim_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		close()


func _style_scrollbar(scroll: ScrollContainer) -> void:
	var v_bar := scroll.get_v_scroll_bar()
	if v_bar != null:
		var grabber_sb := StyleBoxFlat.new()
		grabber_sb.bg_color = Color(0.20, 0.30, 0.25, 0.25)
		grabber_sb.set_corner_radius_all(3)
		v_bar.add_theme_stylebox_override("grabber", grabber_sb)
		v_bar.add_theme_stylebox_override("grabber_highlight", grabber_sb)
		v_bar.add_theme_stylebox_override("grabber_pressed", grabber_sb)
