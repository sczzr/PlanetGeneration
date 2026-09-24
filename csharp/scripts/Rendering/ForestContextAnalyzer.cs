using Godot;
using PlanetGeneration.Core.Domain;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 基于 Voronoi 邻接图与地理物理场的森林群落形态与语境分析器。
/// </summary>
public static class ForestContextAnalyzer
{
    private const float MaxAllowedRotationRad = 0.60f; // 约 ±34 度，保护手绘树木右下光影朝向

    /// <summary>
    /// 分析给定森林地块的拓扑语境，推导最适配的群落形态、旋转角度与缩放系数。
    /// </summary>
    public static ForestClusterShape AnalyzeCell(
        WorldSnapshot snapshot,
        int cellId,
        bool[] isForest,
        float seaLevel,
        out float rotation,
        out float scale)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;

        rotation = 0f;
        scale = 1.0f;

        var cx = (float)geom.CentroidX[cellId];
        var cy = (float)geom.CentroidY[cellId];
        var cellArea = (float)geom.Area[cellId];

        var start = geom.CellNeighborStart[cellId];
        var end = geom.CellNeighborStart[cellId + 1];
        var neighborCount = end - start;

        if (neighborCount == 0)
        {
            return ForestClusterShape.CircleOval;
        }

        var forestNeighbors = new List<int>(neighborCount);
        var hasNearWater = false;
        var waterNeighbors = 0;
        var waterDir = Vector2.Zero;
        var hasNearMountain = false;

        for (var k = start; k < end; k++)
        {
            var nb = geom.CellNeighbors[k];
            if (isForest[nb])
            {
                forestNeighbors.Add(nb);
            }

            var nh = fields.Height[nb];
            if (nh <= seaLevel)
            {
                waterNeighbors++;
                hasNearWater = true;
                var nPos = new Vector2((float)geom.CentroidX[nb], (float)geom.CentroidY[nb]);
                waterDir += (nPos - new Vector2(cx, cy)).Normalized();
            }
            else if (fields.River[nb] > 0.04f)
            {
                hasNearWater = true;
                var nPos = new Vector2((float)geom.CentroidX[nb], (float)geom.CentroidY[nb]);
                waterDir += (nPos - new Vector2(cx, cy)).Normalized();
            }

            if (fields.Landform[nb] == (byte)LandformType.Mountain || nh > seaLevel + 0.35f)
            {
                hasNearMountain = true;
            }
        }

        var fnCount = forestNeighbors.Count;

        // ── 规则 0：狭窄半岛/沿海尖岬（临海面多）──
        // 陆地狭窄脆弱，杜绝放置大密林，仅点缀紧凑疏林微丛或散木
        if (waterNeighbors >= 2)
        {
            return (cellId % 2 == 0) ? ForestClusterShape.SparseEdge : ForestClusterShape.SingleScatter;
        }

        // ── 规则 1：水岸环抱 / 滨水微凹弧（C形环抱）──
        // 仅在朝向匹配（开口朝右即水体在东侧）且偶发概率下选用 CurvedC，其余使用平缓条带或自然簇
        if (hasNearWater && fnCount >= 2 && fnCount <= 4)
        {
            if (waterDir.X > 0.35f && (cellId % 4 == 0))
            {
                return ForestClusterShape.CurvedC;
            }
            if (fnCount == 2)
            {
                return ForestClusterShape.LinearStrip;
            }
            return (cellId % 2 == 0) ? ForestClusterShape.CircleOval : ForestClusterShape.DenseCore;
        }

        // ── 规则 2：孤立或极边缘林地 ──
        if (fnCount <= 1)
        {
            return (cellId % 3 == 0) ? ForestClusterShape.SparseEdge : ForestClusterShape.CircleOval;
        }

        // ── 规则 3：带状走廊 / 山谷隘口（度数 = 2）──
        if (fnCount == 2)
        {
            var nb1 = forestNeighbors[0];
            var nb2 = forestNeighbors[1];
            var p1 = new Vector2((float)geom.CentroidX[nb1], (float)geom.CentroidY[nb1]);
            var p2 = new Vector2((float)geom.CentroidX[nb2], (float)geom.CentroidY[nb2]);

            var v1 = (p1 - new Vector2(cx, cy)).Normalized();
            var v2 = (p2 - new Vector2(cx, cy)).Normalized();
            var dot = v1.Dot(v2);

            // 拐角明显（约 90~120 度）且偶发
            if (dot > -0.45f && (cellId % 3 == 0))
            {
                return ForestClusterShape.CornerL;
            }

            // 位于山地挤压过渡
            if (hasNearMountain && (cellId % 2 == 0))
            {
                return ForestClusterShape.Dumbbell;
            }

            // 平滑带状走向与自然簇
            return (cellId % 3) switch
            {
                0 => ForestClusterShape.LinearStrip,
                1 => ForestClusterShape.CurvedS,
                _ => ForestClusterShape.CircleOval
            };
        }

        // ── 规则 4：三叉汇流 / 分叉走廊（度数 = 3）──
        if (fnCount == 3)
        {
            return (cellId % 3 == 0) ? ForestClusterShape.ForkY : ForestClusterShape.DenseCore;
        }

        // ── 规则 5：双核哑铃过渡（度数 = 4 且有山脊挤压）──
        if (fnCount == 4 && hasNearMountain && (cellId % 2 == 0))
        {
            return ForestClusterShape.Dumbbell;
        }

        // ── 规则 6：深处核心密林（度数 >= 4）──
        if (fnCount >= 4)
        {
            return (cellId % 4 == 0) ? ForestClusterShape.CircleOval : ForestClusterShape.DenseCore;
        }

        // ── 规则 7：边缘稀疏过渡 ──
        if (fnCount < neighborCount)
        {
            return (cellId % 2 == 0) ? ForestClusterShape.SparseEdge : ForestClusterShape.CircleOval;
        }

        return ForestClusterShape.CircleOval;
    }

    /// <summary>
    /// 将任意走向角规整并钳制在安全区间内，确保右下侧统一手绘阴影方向不颠倒。
    /// </summary>
    private static float ClampRotation(float angle)
    {
        // 归一化到 [-PI, PI]
        while (angle > Mathf.Pi) angle -= Mathf.Tau;
        while (angle < -Mathf.Pi) angle += Mathf.Tau;

        // 对称性：由于条形、走廊沿反向也是相同走向，投影到半平面
        if (angle > Mathf.Pi * 0.5f) angle -= Mathf.Pi;
        else if (angle < -Mathf.Pi * 0.5f) angle += Mathf.Pi;

        return Math.Clamp(angle, -MaxAllowedRotationRad, MaxAllowedRotationRad);
    }
}
