using System;

namespace PlanetGeneration.Core.Geometry;

/// <summary>
/// 二维向量：多边形核心专用的纯 C# 实现（零 Godot 依赖）。
///
/// 内部使用 double，保证在 4096+ 量级坐标上的裁剪与顶点去重误差在 1e-9 以内，
/// 支持横向环绕与测地运算。
/// </summary>
public readonly struct PolyVec2 : IEquatable<PolyVec2>
{
    /// <summary>横坐标（逻辑世界空间，可能超出 [0, Width) 表示跨经度缝）。</summary>
    public readonly double X;

    /// <summary>纵坐标（逻辑世界空间，纬度方向不环绕）。</summary>
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
    public static PolyVec2 operator -(PolyVec2 a) => new(-a.X, -a.Y);
    public static PolyVec2 operator *(PolyVec2 a, double scale) => new(a.X * scale, a.Y * scale);
    public static PolyVec2 operator /(PolyVec2 a, double scale) => new(a.X / scale, a.Y / scale);

    /// <summary>点积。</summary>
    public double Dot(PolyVec2 other) => (X * other.X) + (Y * other.Y);

    /// <summary>二维叉积的 z 分量；符号用于判断点在直线的哪一侧。</summary>
    public double Cross(PolyVec2 other) => (X * other.Y) - (Y * other.X);

    /// <summary>模长的平方。</summary>
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

    /// <summary>两点欧几里得距离。</summary>
    public double DistanceTo(PolyVec2 other) => Math.Sqrt(DistanceSquaredTo(other));

    /// <summary>单位化；零向量返回零向量。</summary>
    public PolyVec2 Normalized()
    {
        var length = Length;
        return length <= 1e-12 ? Zero : new PolyVec2(X / length, Y / length);
    }

    /// <summary>左法线（逆时针旋转 90°）。</summary>
    public PolyVec2 LeftNormal() => new(-Y, X);

    /// <summary>线性插值。</summary>
    public PolyVec2 Lerp(PolyVec2 other, double t) => new(X + ((other.X - X) * t), Y + ((other.Y - Y) * t));

    /// <summary>规整到指定步长。</summary>
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
