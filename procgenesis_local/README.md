# 行星生成 (Planet Generation) - Godot 4.6 C#

## 目录拆分

已将仓库内容按项目类型拆分为两个目录：

- `csharp/`：Godot 4.6 C# 项目（当前主项目）
- `web/`：原网页版本及静态资源（用于对照迁移）

当前工程已迁移为 **Godot 4.6 + C#**，并完成 `WorldGen` 的核心模块迁移：

- 板块（Plate IDs + 边界类型：汇聚/离散/转换）
- 温度（纬度带 + 3D 噪声 + 海拔降温）
- 湿度（海洋源 + 风场扩散）
- 河流（高海拔湿润区源头 + 邻域寻低流动）
- 生物群系分类（与原 JS 规则对齐）
- 侵蚀（Erosion）迭代
- 岩石与矿石分布（Rock/Ore）
- 城市选址（Cities）
- 多图层渲染（Satellite / Plates / Temperature / Rivers / Moisture / Wind / Elevation / Rock Types / Ores / Cities）
- 导出能力（PNG 图像、JSON 元数据）
- Cities 图层支持城市名称清单展示
- 对齐预设（`Legacy` / `Balanced`）
- 统计指标（海洋覆盖率、河流覆盖率、平均温度、平均湿度）
- A/B 同屏对比（同 seed 下双预设并行）

## 如何打开

1. 安装 **Godot 4.6 (.NET)**
2. 在 Godot 中导入目录：`procgenesis_local/csharp`
3. 首次打开等待 C# 项目初始化
4. 运行主场景（已配置 `res://scenes/Main.tscn`）

网页版如需本地运行，请进入 `procgenesis_local/web` 后启动 `server.py` 或 `server.js`。

## 当前功能入口

- 主场景：`scenes/Main.tscn`
- 主流程控制：`scripts/Main.cs`
- WorldGen 模块：`scripts/WorldGen/`

可在 UI 中调参：`Seed / Plates / Wind Cells / Map Size(固定2:1) / Sea / Heat / Erosion / Rivers / Random Heat`，并切换图层查看结果。
`Map Size` 提供球体贴图友好的等距矩形分辨率（2:1），默认最小分辨率 `256x128`，并包含超高分辨率 `4096x2048`。
InfoLabel 会显示 `HeatEff` 与 `WarmBand` 预估信息（随 Seed 与 Random Heat 实时变化）。
选择 `4096x2048` 时 UI 会显示性能警告。
可通过 `Export PNG` 与 `Export JSON` 导出当前结果（输出到 `user://exports/`）。
`TuningOption` 可在 `Legacy` 与 `Balanced` 两套参数之间切换，便于做同 seed 对比。
`A/B Compare` 打开后：左侧为当前 preset（A），右侧为另一个 preset（B），并显示差异统计。

## 模块结构

- `scripts/WorldGen/PlateGenerator.cs`: 板块划分与边界应力分类
- `scripts/WorldGen/TemperatureGenerator.cs`: 温度场
- `scripts/WorldGen/MoistureGenerator.cs`: 基础湿度、风场与湿度输运
- `scripts/WorldGen/RiverGenerator.cs`: 河流网络
- `scripts/WorldGen/ErosionSimulator.cs`: 侵蚀迭代
- `scripts/WorldGen/ResourceGenerator.cs`: 岩石与矿物分布
- `scripts/WorldGen/CityGenerator.cs`: 城市生成
- `scripts/WorldGen/BiomeGenerator.cs`: 生物群系判定
- `scripts/WorldGen/WorldRenderer.cs`: 图层渲染
- `scripts/WorldGen/Types.cs`: 共享类型定义

## 备注

- 原网页资源与脚本位于 `web/`，便于后续继续对照迁移。
- 若需“逐行级”复刻原 JS 的所有细节（城市、语言树、矿石、岩石、侵蚀迭代等），可在此结构上继续扩展。

海平面参数 `Sea` 现在直接控制海陆分界阈值，不再固定目标海洋覆盖率。 当前目标曲线采用贝塞尔插值并锚定：`Sea=0.1 -> Ocean≈30%`, `Sea=0.5 -> Ocean≈72%`, `Sea=0.9 -> Ocean≈95%`。
