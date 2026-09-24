namespace PlanetGeneration.UI.State;

/// <summary>
/// 控制地图属性使用栅格还是多边形单元格渲染的运行模式。
///
/// 这是 UI/运行时显示策略，不属于多边形几何算法命名空间。
/// 数值保持兼容，避免已有设置文件和场景配置失效。
/// </summary>
public enum PolygonTileMode
{
    /// <summary>全部走栅格，作为回归基线和故障回退模式。</summary>
    Raster = 0,

    /// <summary>离散分类使用多边形，连续场保持栅格。</summary>
    Hybrid = 1,

    /// <summary>按多边形单元格平铺可用的地图属性。</summary>
    Cells = 2,

    /// <summary>在单元格模式基础上绘制地块边界。</summary>
    Outlined = 3,
}
