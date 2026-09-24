using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 绘制指令构建器（PainterCommandBuilder）。
/// 负责将离散的 BrushInstruction 与 Landmark 按照图层层级（Paper -> River -> Forest -> Mountain -> Fog -> Landmark -> Label -> Texture）
/// 进行规范化分组与景深深度排序。
/// </summary>
public static class PainterCommandBuilder
{
    public const string LayerPaper = "painter_paper";
    public const string LayerRiver = "painter_river";
    public const string LayerForest = "painter_forest";
    public const string LayerMountain = "painter_mountain";
    public const string LayerFog = "painter_fog";
    public const string LayerLandmark = "painter_landmark";
    public const string LayerLabel = "painter_label";
    public const string LayerTexture = "painter_texture";

    public static List<PainterCommand> BuildCommands(
        IEnumerable<BrushInstruction> brushes,
        IEnumerable<LandmarkStyle> landmarks)
    {
        var result = new List<PainterCommand>();

        // 1. 宣纸底温地子
        result.Add(new PainterCommand
        {
            LayerId = LayerPaper,
            LayerPriority = 0,
            LayerOpacity = 1.0f
        });

        // 2. 江河水系与沧海微澜 (包含蛇曲牛轭湖与智能海岸波纹)
        var riverBrushes = brushes
            .Where(b => b.Type is BrushType.RiverStroke or BrushType.SeaWave or BrushType.OxbowLake or BrushType.CoastlineWave)
            .ToList();

        result.Add(new PainterCommand
        {
            LayerId = LayerRiver,
            LayerPriority = 10,
            Brushes = riverBrushes
        });

        // 3. 巨型宏观林海基底 (含沼泽死水潭)
        var macroForestBrushes = brushes
            .Where(b => b.Type is BrushType.ForestCluster or BrushType.SwampPool)
            .OrderBy(b => b.YOrder)
            .ToList();

        result.Add(new PainterCommand
        {
            LayerId = LayerForest,
            LayerPriority = 20,
            Brushes = macroForestBrushes
        });

        // 4. 群峰叠峦、托尔金排线、高原断崖、深渊裂谷、点缀单木、微地貌（农田/孤丘/镜湖/沙垄/草纹/三角洲沙洲/海拱/垂苔）
        var mountainAndObjectBrushes = brushes
            .Where(b => b.Type is BrushType.MountainFar or BrushType.MountainMain or BrushType.MountainSecondary or BrushType.MountainRidge or BrushType.Hill or BrushType.SnowCap
                        or BrushType.MountainHachure or BrushType.TreeGroup or BrushType.PlateauCliff or BrushType.WetlandReeds
                        or BrushType.DesertDune or BrushType.GrassTussock or BrushType.LakePond or BrushType.FieldTerraced
                        or BrushType.DeltaIsland or BrushType.ChasmAbyss or BrushType.ChasmCliff or BrushType.SeaArch or BrushType.SpanishMoss)
            .OrderBy(b => b.YOrder)
            .ToList();

        result.Add(new PainterCommand
        {
            LayerId = LayerMountain,
            LayerPriority = 30,
            Brushes = mountainAndObjectBrushes
        });

        // 5. 山麓流岚与低空烟霭
        var fogBrushes = brushes
            .Where(b => b.Type == BrushType.Fog)
            .OrderBy(b => b.YOrder)
            .ToList();

        result.Add(new PainterCommand
        {
            LayerId = LayerFog,
            LayerPriority = 40,
            Brushes = fogBrushes
        });

        // 6. 城池聚落与建筑地标符号
        var cityBrushes = brushes
            .Where(b => b.Type == BrushType.CityIcon)
            .OrderBy(b => b.YOrder)
            .ToList();

        result.Add(new PainterCommand
        {
            LayerId = LayerLandmark,
            LayerPriority = 50,
            Brushes = cityBrushes,
            Landmarks = landmarks.ToList()
        });

        // 7. 书法题名与题注标注
        result.Add(new PainterCommand
        {
            LayerId = LayerLabel,
            LayerPriority = 60,
            Landmarks = landmarks.ToList()
        });

        // 8. 宣纸卷轴边框与纸本肌理
        result.Add(new PainterCommand
        {
            LayerId = LayerTexture,
            LayerPriority = 70,
            LayerOpacity = 1.0f
        });

        return result;
    }
}
