using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 世界逻辑范围与环绕规则。
///
/// 坐标系约定：
///   - 默认逻辑尺寸为 2048×1024（2:1 比例）；
///   - 横向（经度）严格环绕：[0, Width)，跨缝自动计算最近测地距离与镜像；
///   - 纵向（纬度）南北极截断：[0, Height]，不环绕。
/// </summary>
public sealed record WorldExtent
{
    public double Width { get; init; }
    public double Height { get; init; }

    public double AspectRatio => Height > 0d ? Width / Height : 2.0;

    public WorldExtent(double width = 2048d, double height = 1024d)
    {
        if (width <= 0d || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "世界逻辑尺寸必须为正数。");
        }

        Width = width;
        Height = height;
    }

    /// <summary>默认 2K 逻辑范围（2048×1024）。</summary>
    public static readonly WorldExtent Default = new(2048d, 1024d);

    /// <summary>经度横向周期取模，规范到 [0, Width)。</summary>
    public double WrapX(double x)
    {
        var wrapped = x % Width;
        if (wrapped < 0d)
        {
            wrapped += Width;
        }

        // 避免极值取模后恰好等于 Width
        if (wrapped >= Width)
        {
            wrapped = 0d;
        }

        return wrapped;
    }

    /// <summary>纬度纵向钳制，限制在 [0, Height] 内。</summary>
    public double ClampY(double y)
    {
        if (y < 0d) return 0d;
        if (y > Height) return Height;
        return y;
    }

    /// <summary>点坐标规范化：横向环绕，纵向钳制。</summary>
    public PolyVec2 Normalize(PolyVec2 point) => new(WrapX(point.X), ClampY(point.Y));

    /// <summary>从 fromX 到 toX 的最短环绕位移。</summary>
    public double DeltaWrappedX(double fromX, double toX)
    {
        var dx = toX - fromX;
        var half = Width * 0.5;
        while (dx > half) dx -= Width;
        while (dx < -half) dx += Width;
        return dx;
    }

    /// <summary>考虑经度环绕的两点最短测地距离的平方。</summary>
    public double DistanceSquaredWrapped(PolyVec2 a, PolyVec2 b)
    {
        var dx = Math.Abs(a.X - b.X);
        if (dx > Width * 0.5)
        {
            dx = Width - dx;
        }

        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>考虑经度环绕的两点最短测地距离。</summary>
    public double DistanceWrapped(PolyVec2 a, PolyVec2 b) => Math.Sqrt(DistanceSquaredWrapped(a, b));

    /// <summary>
    /// 将逻辑世界点坐标 (wx, wy) 映射到目标分辨率像素坐标 (px, py)。
    /// </summary>
    public (double PixelX, double PixelY) WorldToPixel(double wx, double wy, int pixelWidth, int pixelHeight)
    {
        var nx = WrapX(wx) / Width;
        var ny = Math.Clamp(wy / Height, 0d, 0.9999999d);
        return (nx * pixelWidth, ny * pixelHeight);
    }

    /// <summary>
    /// 将目标分辨率像素坐标 (px, py) 映射回逻辑世界点坐标 (wx, wy)。
    /// </summary>
    public PolyVec2 PixelToWorld(double px, double py, int pixelWidth, int pixelHeight)
    {
        var nx = pixelWidth > 0 ? px / pixelWidth : 0d;
        var ny = pixelHeight > 0 ? py / pixelHeight : 0d;
        return new PolyVec2(WrapX(nx * Width), ClampY(ny * Height));
    }
}
