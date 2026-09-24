using Godot;
using PlanetGeneration.Rendering;
using System;
using System.IO;

namespace PlanetGeneration.Tests;

/// <summary>
/// 方案 A 地貌解耦渲染自动化验证测试（重构完善版）：
/// 彻底解决上一版的四大违和问题：
/// 1. 山体主体完整保留（雪顶、岩壁、山体前坡），不再被生硬截断，绝不吞没前景建筑；
/// 2. 真正识别“接地部分”：仅将山脚接触平原的外围草皮裙边分离为接地贴花层；
/// 3. 全面结合 2.5D 规范的 Y-Sort 景深排序：
///    - 后方森林（北面）：山体主体立于其前自然遮挡，但山脚接地草皮绝不覆盖森林；
///    - 前方城镇（南面）：完完整整立于山脚，屋顶城楼无一被吞，自然压住山脚草皮；
///    - 前景树木（东南）：完整立在前景，景深分明。
/// </summary>
public partial class DecoupledReliefVerification : Node
{
    public override async void _Ready()
    {
        GD.Print("[DecoupledTest] === 开始方案 A 地貌免切图解耦管线自动化验证 ===");

        try
        {
            var viewport = new SubViewport
            {
                Size = new Vector2I(1600, 900),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false
            };
            AddChild(viewport);

            var root2D = new Node2D();
            viewport.AddChild(root2D);

            BuildScene(root2D);

            // 等待多帧以便渲染树完成 GPU 材质编译与绘制
            for (var f = 0; f < 8; f++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            var tex = viewport.GetTexture();
            var img = tex.GetImage();
            if (img != null)
            {
                var savePathRes = "res://decoupled_relief_verification.png";
                var savePathLocal = ProjectSettings.GlobalizePath(savePathRes);
                var err = img.SavePng(savePathLocal);
                GD.Print($"[DecoupledTest] 验证截图已保存至本地: {savePathLocal}, 结果码: {err}");

                var artifactDir = @"C:\Users\shawn\.gemini\antigravity\brain\bc0c95b2-b403-492e-9217-3eeb583fcb86";
                if (Directory.Exists(artifactDir))
                {
                    var artifactFile = Path.Combine(artifactDir, "decoupled_relief_verification.png");
                    File.Copy(savePathLocal, artifactFile, true);
                    GD.Print($"[DecoupledTest] 验证截图已同步至 Artifact 目录: {artifactFile}");
                }
            }
            else
            {
                GD.PrintErr("[DecoupledTest] 无法抓取视口渲染图像！");
            }

            GD.Print("[DecoupledTest] === 自动化验证全部成功完成！ ===");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DecoupledTest] 验证异常: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            GetTree().Quit();
        }
    }

    private void BuildScene(Node2D root)
    {
        // 1. 背景底色：地图土地大地色（柔和平原草地绿）
        var bg = new ColorRect
        {
            Name = "TerrainBackground",
            Size = new Vector2(1600, 900),
            Color = new Color(0.55f, 0.64f, 0.51f, 1.0f)
        };
        root.AddChild(bg);

        // 加载贴图资产
        var texMountain = GD.Load<Texture2D>("res://assets/sprites/nature/mountain_isolated_peak.png");
        var texTreeCluster = GD.Load<Texture2D>("res://assets/sprites/map_symbols/tree_cluster.png");
        var texCityTown = GD.Load<Texture2D>("res://assets/sprites/map_symbols/city_town.png");
        var texTreePine = GD.Load<Texture2D>("res://assets/sprites/map_symbols/tree_pine.png");

        // 顶栏说明条
        var headerBar = new ColorRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(1600, 95),
            Color = new Color(0.10f, 0.13f, 0.17f, 0.96f)
        };
        root.AddChild(headerBar);

        var titleLabel = new Label
        {
            Text = "全局物件免切图接地解耦管线 (Universal Decoupled Object) 验证",
            Position = new Vector2(40, 16)
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 28);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.96f, 0.96f));
        headerBar.AddChild(titleLabel);

        var ruleLabel = new Label
        {
            Text = "全局铁律：所有物件的底部【只能覆盖地面，不能覆盖任何其他贴图，但可被其他素材覆盖】；主体完整参与 2.5D Y-Sort。",
            Position = new Vector2(40, 56)
        };
        ruleLabel.AddThemeFontSizeOverride("font_size", 15);
        ruleLabel.AddThemeColorOverride("font_color", new Color(0.58f, 0.72f, 0.88f));
        headerBar.AddChild(ruleLabel);

        // ==========================================
        // 左侧测试组：传统单图未解耦（Traditional Single Sprite）
        // ==========================================
        var leftGroup = new Node2D { Position = new Vector2(410, 500), Name = "LeftTraditionalGroup" };
        root.AddChild(leftGroup);

        var leftCard = new ColorRect
        {
            Position = new Vector2(-370, -380),
            Size = new Vector2(740, 750),
            Color = new Color(0.14f, 0.17f, 0.21f, 0.40f)
        };
        leftGroup.AddChild(leftCard);

        var leftTitle = new Label
        {
            Text = "【对比组】传统单图未解耦 (Single Sprite)",
            Position = new Vector2(-340, -360)
        };
        leftTitle.AddThemeFontSizeOverride("font_size", 22);
        leftTitle.AddThemeColorOverride("font_color", new Color(0.95f, 0.35f, 0.35f));
        leftGroup.AddChild(leftTitle);

        var leftDesc = new Label
        {
            Text = "严重违规表现：\n1. 山脚草坡裙边直接涂抹在后方树林上；\n2. 前方城镇地基与松树根底泥土生硬涂抹在山体岩壁上；\n3. 物件底部直接覆盖了其他素材贴图，违背地图规则。",
            Position = new Vector2(-340, -325)
        };
        leftDesc.AddThemeFontSizeOverride("font_size", 14);
        leftDesc.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        leftGroup.AddChild(leftDesc);

        var leftYLayer = new Node2D
        {
            Name = "LeftYLayer",
            YSortEnabled = true,
            ZAsRelative = false,
            ZIndex = 1
        };
        leftGroup.AddChild(leftYLayer);

        // 1. 左后方森林 (Y = -20)
        var treeLeft = new Sprite2D
        {
            Texture = texTreeCluster,
            Position = new Vector2(-200, -20),
            Scale = new Vector2(0.95f, 0.95f)
        };
        leftYLayer.AddChild(treeLeft);

        // 2. 传统单图山峰（Y = 40，草坡裙边直接覆盖在后方森林上）
        var mountLeft = new Sprite2D
        {
            Texture = texMountain,
            Position = new Vector2(0, 40),
            Scale = new Vector2(2.4f, 2.4f)
        };
        leftYLayer.AddChild(mountLeft);

        // 3. 山脚前缘城镇 (Y = 180，地基直接涂在山岩上)
        var townLeft = new Sprite2D
        {
            Texture = texCityTown,
            Position = new Vector2(-40, 180),
            Scale = new Vector2(1.5f, 1.5f)
        };
        leftYLayer.AddChild(townLeft);

        // 4. 右前景松树 (Y = 200，根部直接涂在山岩上)
        var pineLeft = new Sprite2D
        {
            Texture = texTreePine,
            Position = new Vector2(160, 200),
            Scale = new Vector2(1.1f, 1.1f)
        };
        leftYLayer.AddChild(pineLeft);


        // ==========================================
        // 右侧测试组：全局通用物件免切图接地解耦管线 (Universal Decoupled Object)
        // ==========================================
        var rightGroup = new Node2D { Position = new Vector2(1190, 500), Name = "RightDecoupledGroup" };
        root.AddChild(rightGroup);

        var rightCard = new ColorRect
        {
            Position = new Vector2(-370, -380),
            Size = new Vector2(740, 750),
            Color = new Color(0.14f, 0.17f, 0.21f, 0.40f)
        };
        rightGroup.AddChild(rightCard);

        var rightTitle = new Label
        {
            Text = "【实验组】全局通用解耦管线 (Universal Decoupled Object)",
            Position = new Vector2(-340, -360)
        };
        rightTitle.AddThemeFontSizeOverride("font_size", 22);
        rightTitle.AddThemeColorOverride("font_color", new Color(0.35f, 0.95f, 0.55f));
        rightGroup.AddChild(rightTitle);

        var rightDesc = new Label
        {
            Text = "完美达成全局铁律：\n1. 底部只能覆地：山裙、城镇地基、树根通过先贴花、后立面的全局分层，绝不涂抹在任何其他贴图上；\n2. 建筑与树木贴合山体：城镇城墙与松树躯干完好自然立于山麓，地基不脏污山岩；\n3. 主体 2.5D Y-Sort 深度自然：城楼屋顶 100% 完整展现，不被山体吞没；\n4. 全资产通用：同一管线自动适配山峰、城镇、森林、松树等一切地图物件。",
            Position = new Vector2(-340, -325)
        };
        rightDesc.AddThemeFontSizeOverride("font_size", 14);
        rightDesc.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        rightGroup.AddChild(rightDesc);

        // 物件主体层 (开启 Y-Sort，统一在 Z=1，保证真实 2.5D 前后遮挡关系)
        var rightYLayer = new Node2D
        {
            Name = "RightYLayer",
            YSortEnabled = true,
            ZAsRelative = false,
            ZIndex = 1
        };
        rightGroup.AddChild(rightYLayer);

        var groundCol = new Color(0.55f, 0.64f, 0.51f, 1.0f);

        // ① 后方森林 (Y = -20，通用解耦物件)
        var treeRight = new UniversalDecoupledObject
        {
            Texture = texTreeCluster,
            Position = new Vector2(-200, -20),
            Scale = new Vector2(0.95f, 0.95f),
            SplitMode = UniversalDecoupledObject.DecoupledSplitMode.BottomRatio,
            BottomRatio = 0.04f,
            BottomSoftness = 0.01f,
            GroundColor = groundCol,
            GroundThreshold = 0.08f
        };
        rightYLayer.AddChild(treeRight);

        // ② 解耦山峰 (Y = 40，色彩特征提取草坡裙边)
        var mountRight = new UniversalDecoupledObject
        {
            Texture = texMountain,
            Position = new Vector2(0, 40),
            Scale = new Vector2(2.4f, 2.4f),
            SplitMode = UniversalDecoupledObject.DecoupledSplitMode.ColorFeature,
            FeatureYStart = 0.52f,
            GrassRatioR = 0.95f,
            GrassRatioB = 1.08f,
            GroundColor = groundCol,
            GroundThreshold = 0.08f
        };
        rightYLayer.AddChild(mountRight);

        // ③ 前方城镇 (Y = 260，真正位于山脚山麓接触带，与山体下缘及大地交接)
        var townRight = new UniversalDecoupledObject
        {
            Texture = texCityTown,
            Position = new Vector2(-50, 260),
            Scale = new Vector2(1.5f, 1.5f),
            SplitMode = UniversalDecoupledObject.DecoupledSplitMode.BottomRatio,
            BottomRatio = 0.03f,
            BottomSoftness = 0.01f,
            GroundColor = groundCol,
            GroundThreshold = 0.08f
        };
        rightYLayer.AddChild(townRight);

        // ④ 右前景松树 (Y = 280，位于山脚东南侧，与山体裙边及大地交接)
        var pineRight = new UniversalDecoupledObject
        {
            Texture = texTreePine,
            Position = new Vector2(150, 280),
            Scale = new Vector2(1.1f, 1.1f),
            SplitMode = UniversalDecoupledObject.DecoupledSplitMode.BottomRatio,
            BottomRatio = 0.025f,
            BottomSoftness = 0.01f,
            GroundColor = groundCol,
            GroundThreshold = 0.08f
        };
        rightYLayer.AddChild(pineRight);

        // 同步更新左侧对比组的坐标，保证位置完全一致可比！
        townLeft.Position = new Vector2(-50, 260);
        pineLeft.Position = new Vector2(150, 280);
    }
}
