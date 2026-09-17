using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 二维向量：多边形核心专用的最小实现。
///
/// 为什么不用 Godot 的 <c>Vector2</c>：
/// 整个 <c>WorldGen.Polygon</c> 命名空间刻意不引用 Godot，几何部分因此可以脱离编辑器
/// 独立编译与自检（抖动取点、裁剪、面积、邻接都可以用纯控制台断言验证）。
/// 需要交给 Godot 绘制时，在渲染层做一次转换即可。
///
/// 为什么内部用 <c>double</c> 而不是 <c>float</c>：
/// 顶点去重（相邻地块共享角点）依赖两次独立裁剪得到同一个坐标，
/// float32 在 4096 量级坐标上只有约 0.04px 的分辨力，去重容差会非常紧张；
/// double 可以把误差压到 1e-9 以下，去重容差就能放到安全的 1e-3px。
/// </summary>
public readonly struct PolyVec2 : IEquatable<PolyVec2>
{
    /// <summary>横坐标（源栅格像素空间，可能超出 [0, 地图宽度) 表示跨经度缝）。</summary>
    public readonly double X;

    /// <summary>纵坐标（源栅格像素空间，纬度方向不环绕）。</summary>
    public readonly double Y;

    public PolyVec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>零向量。</summary>
    public static PolyVec2 Zero => new(0d, 0d);

    public static PolyVec2 operator +(PolyVec2 a, PolyVec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static PolyVec2 operator -(PolyVec2 a, PolyVec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static PolyVec2 operator *(PolyVec2 a, double scale) => new(a.X * scale, a.Y * scale);

    /// <summary>点积。</summary>
    public double Dot(PolyVec2 other) => (X * other.X) + (Y * other.Y);

    /// <summary>二维叉积的 z 分量；符号用于判断点在直线的哪一侧。</summary>
    public double Cross(PolyVec2 other) => (X * other.Y) - (Y * other.X);

    /// <summary>模长的平方（比 <see cref="Length"/> 少一次开方，比较距离时优先用这个）。</summary>
    public double LengthSquared => (X * X) + (Y * Y);

    /// <summary>模长。</summary>
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>两点距离的平方。</summary>
    public double DistanceSquaredTo(PolyVec2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>单位化；零向量返回零向量而不抛异常，避免退化情形污染调用方。</summary>
    public PolyVec2 Normalized()
    {
        var length = Length;
        return length <= 1e-12 ? Zero : new PolyVec2(X / length, Y / length);
    }

    /// <summary>左法线（逆时针旋转 90°）。</summary>
    public PolyVec2 LeftNormal() => new(-Y, X);

    /// <summary>线性插值，<paramref name="t"/> = 0 返回自身，= 1 返回 <paramref name="other"/>。</summary>
    public PolyVec2 Lerp(PolyVec2 other, double t) => new(X + ((other.X - X) * t), Y + ((other.Y - Y) * t));

    /// <summary>四舍五入到指定小数位；用于把浮点坐标规整成可比较的键。</summary>
    public PolyVec2 Snapped(double step)
    {
        if (step <= 0d)
        {
            return this;
        }

        return new PolyVec2(Math.Round(X / step) * step, Math.Round(Y / step) * step);
    }

    public bool Equals(PolyVec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override bool Equals(object? obj) => obj is PolyVec2 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X:0.####}, {Y:0.####})";
}
