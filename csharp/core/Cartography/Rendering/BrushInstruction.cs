using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 幻想制图笔刷类型枚举。
/// </summary>
public enum BrushType
{
    MountainFar,        // 远山浅黛 / 极目远峰
    MountainMain,       // 主峰耸立 / 巍峨主脊
    MountainSecondary,  // 伴峰 / 拱卫副峰
    MountainRidge,      // 连绵横卧山脊 / 山身躯干
    Hill,               // 缓丘 / 山麓余脉
    SnowCap,            // 积雪冰帽 / 霜峰绝顶
    ForestCluster,      // 宏观水墨林海林冠簇
    TreeGroup,          // 疏朗双木交柯与三木微丛
    RiverStroke,        // 水墨江河书法笔触折线
    DesertDune,         // 金沙大漠新月沙丘流线
    Fog,                // 山麓流岚与低空烟霭
    CityIcon,           // 古建城池与村落图腾
    PlateauCliff,       // 高原红砂桌状断崖
    WetlandReeds,       // 湿地蒹葭芦荡小品
    GrassTussock,       // 草甸草纹 / 丛生风草
    LakePond,           // 内陆清池水泽 / 浅渚小池
    SeaWave,            // 沧海细密水波细纹
    FieldTerraced,      // 农田水网 / 水浇田梯田肌理 (////)
    // ── Map Effects (https://www.mapeffects.co/learn) 专属手绘地图要素 ──
    MountainHachure,    // 托尔金山脉背光侧阴影斜向排线 (Tolkien Hachures)
    OxbowLake,          // 平原蛇曲截弯取直残存牛轭湖 (Oxbow Lake)
    DeltaIsland,        // 河流入海口树根状冲积沙洲群岛 (River Delta Islands)
    ChasmAbyss,         // 大地深渊裂谷黑底渐变 (Chasm Abyss Base)
    ChasmCliff,         // 裂谷断崖崖壁与垂直落差排线 (Chasm Ragged Cliff & Strata)
    CoastlineWave,      // 智能海岸多阶等距同心水波纹 (Smart Coastline Wave Buffer)
    SeaArch,            // 岬角海蚀石拱奇观 (Promontory Sea Arch)
    SwampPool,          // 积水泥沼水潭与死水面 (Swamp Pool)
    SpanishMoss         // 湿地树冠倒挂西班牙垂苔 (Hanging Spanish Moss)
}

/// <summary>
/// 笔刷绘制指令（BrushInstruction）。
/// 
/// 作为制图表现层 Generator 与 Renderer 之间的权威抽象，
/// 决定每一个图元或笔触的位置、姿态、尺寸、透明度、深度层级与色彩基调。
/// </summary>
public sealed class BrushInstruction
{
    public BrushType Type { get; set; }

    /// <summary>世界逻辑空间坐标。</summary>
    public PolyVec2 Position { get; set; }

    /// <summary>旋转弧度（顺时针为正）。</summary>
    public float Rotation { get; set; } = 0.0f;

    /// <summary>主缩放系数（默认 1.0）。</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>纵向形变拉伸系数（默认 1.0）。</summary>
    public float ScaleY { get; set; } = 1.0f;

    /// <summary>不透明度（0.0 ~ 1.0）。</summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>Y 轴景深遮挡排序基准值（通常取根部/山脚接触面的 Y 坐标）。</summary>
    public float YOrder { get; set; } = 0.0f;

    /// <summary>色彩调色调（乘法着色）。</summary>
    public CartographyColor Tint { get; set; } = CartographyColor.White;

    /// <summary>图元资产特定变体标识（例如 "peak_main_01", "mist_soft" 等）。</summary>
    public string? VariantKey { get; set; }

    /// <summary>所属区域 ID（若无则为 -1）。</summary>
    public int RegionId { get; set; } = -1;

    /// <summary>线状笔刷专用折线轨迹点集合（仅 RiverStroke, SeaWave 有效）。</summary>
    public PolyVec2[]? Points { get; set; }

    /// <summary>笔触墨线宽度（像素或逻辑单位）。</summary>
    public float StrokeWidth { get; set; } = 1.0f;

    /// <summary>辅助元数据标志位或扩展说明。</summary>
    public string? Tag { get; set; }

    public override string ToString() => $"Brush[{Type}]: Pos={Position}, Scale={Scale:F2}, YOrder={YOrder:F1}, Opacity={Opacity:F2}";
}
