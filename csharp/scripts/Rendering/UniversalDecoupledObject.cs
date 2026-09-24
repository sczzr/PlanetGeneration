using Godot;
using System;

namespace PlanetGeneration.Rendering;

/// <summary>
/// Splits an object into a ground-only pass and a Y-sorted upright pass in one canvas.
/// Ground must be below Z=0; all upright receivers must be at Z=1. The owner position
/// is the foot anchor (use Offset to align the artwork). ExplicitMask is the exact
/// asset contract; ratio/color modes are only migration heuristics.
/// </summary>
[Tool]
public partial class UniversalDecoupledObject : Node2D
{
    public enum DecoupledSplitMode
    {
        BottomRatio = 0,
        ColorFeature = 1,
        ExplicitMask = 2
    }

    private static readonly Shader _shader = GD.Load<Shader>("res://assets/shaders/universal_decoupled_object.gdshader");

    private Sprite2D _bottomSprite = null!;
    private Sprite2D _bodySprite = null!;
    private ShaderMaterial _bottomMaterial = null!;
    private ShaderMaterial _bodyMaterial = null!;

    private Texture2D? _texture;
    private Texture2D? _decalMask;
    private DecoupledSplitMode _splitMode = DecoupledSplitMode.BottomRatio;
    private float _bottomRatio = 0.10f;
    private float _bottomSoftness = 0.02f;
    private float _featureYStart = 0.52f;
    private float _grassRatioR = 0.95f;
    private float _grassRatioB = 1.08f;
    private bool _checkGround = true;
    private Color _groundColor = new Color(0.55f, 0.64f, 0.51f, 1.0f);
    private float _groundThreshold = 0.14f;
    private int _bottomZIndex = 0;
    private bool _centered = true;
    private Vector2 _offset = Vector2.Zero;

    /// <summary>White/red >= 0.5 = decal; black/transparent = body. Same UV layout as Texture.</summary>
    [Export]
    public Texture2D? DecalMask
    {
        get => _decalMask;
        set { _decalMask = value; UpdateShaderUniforms(); }
    }

    [Export]
    public Texture2D? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            UpdateTextures();
        }
    }

    [Export]
    public DecoupledSplitMode SplitMode
    {
        get => _splitMode;
        set
        {
            _splitMode = value;
            UpdateShaderUniforms();
        }
    }

    [Export(PropertyHint.Range, "0.0,0.4,0.01")]
    public float BottomRatio
    {
        get => _bottomRatio;
        set
        {
            _bottomRatio = Mathf.Clamp(value, 0f, 0.4f);
            UpdateShaderUniforms();
        }
    }

    // Legacy setting; split ownership is now disjoint, softness comes from asset alpha.
    [Export(PropertyHint.Range, "0.001,0.08,0.005")]
    public float BottomSoftness
    {
        get => _bottomSoftness;
        set
        {
            _bottomSoftness = value;
            UpdateShaderUniforms();
        }
    }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float FeatureYStart
    {
        get => _featureYStart;
        set
        {
            _featureYStart = value;
            UpdateShaderUniforms();
        }
    }

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float GrassRatioR
    {
        get => _grassRatioR;
        set
        {
            _grassRatioR = value;
            UpdateShaderUniforms();
        }
    }

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float GrassRatioB
    {
        get => _grassRatioB;
        set
        {
            _grassRatioB = value;
            UpdateShaderUniforms();
        }
    }

    // Legacy serialized settings retained for scene compatibility; never used to guess receivers.
    [Export]
    public bool CheckGround
    {
        get => _checkGround;
        set
        {
            _checkGround = value;
            UpdateShaderUniforms();
        }
    }

    [Export]
    public Color GroundColor
    {
        get => _groundColor;
        set
        {
            _groundColor = value;
            UpdateShaderUniforms();
        }
    }

    [Export(PropertyHint.Range, "0.01,0.5,0.01")]
    public float GroundThreshold
    {
        get => _groundThreshold;
        set
        {
            _groundThreshold = value;
            UpdateShaderUniforms();
        }
    }

    [Export]
    public int BottomZIndex
    {
        get => _bottomZIndex;
        set
        {
            _bottomZIndex = GroundDecalLayers.Decal; // Fixed: callers cannot lift a decal over a body.
            if (_bottomSprite != null)
            {
                _bottomSprite.ZIndex = _bottomZIndex;
            }
        }
    }

    [Export]
    public bool Centered
    {
        get => _centered;
        set
        {
            _centered = value;
            if (_bottomSprite != null) _bottomSprite.Centered = value;
            if (_bodySprite != null) _bodySprite.Centered = value;
        }
    }

    [Export]
    public Vector2 Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            if (_bottomSprite != null) _bottomSprite.Offset = value;
            if (_bodySprite != null) _bodySprite.Offset = value;
        }
    }

    public override void _EnterTree()
    {
        // Absolute owner depth makes nested/negative-Z parents safe. Keep this object
        // atomic in its parent's Y sort; children share the same foot transform.
        ZAsRelative = false;
        ZIndex = GroundDecalLayers.Body;
        YSortEnabled = false;
        EnsureChildren();
        UpdateTextures();
        UpdateShaderUniforms();
    }

    private void EnsureChildren()
    {
        if (_bottomSprite != null && _bodySprite != null)
        {
            return;
        }

        // 1. 接地贴花层 (BottomSprite): 强制渲染在地表层 (Z=0)，绝不相对父级递增
        _bottomSprite = GetNodeOrNull<Sprite2D>("BottomSprite") ?? new Sprite2D { Name = "BottomSprite" };
        _bottomSprite.ZAsRelative = false;
        _bottomSprite.ZIndex = _bottomZIndex;
        _bottomSprite.Centered = _centered;
        _bottomSprite.Offset = _offset;

        _bottomMaterial = new ShaderMaterial { Shader = _shader };
        _bottomMaterial.SetShaderParameter("pass_mode", 0); // BottomOnly
        _bottomSprite.Material = _bottomMaterial;

        if (_bottomSprite.GetParent() == null)
        {
            AddChild(_bottomSprite);
        }

        // 2. 主体景深层 (BodySprite): 随父节点处于 Z=1 且随父级 Y-Sort 深度排序
        _bodySprite = GetNodeOrNull<Sprite2D>("BodySprite") ?? new Sprite2D { Name = "BodySprite" };
        _bodySprite.ZAsRelative = true;
        _bodySprite.ZIndex = 0;
        _bodySprite.Centered = _centered;
        _bodySprite.Offset = _offset;

        _bodyMaterial = new ShaderMaterial { Shader = _shader };
        _bodyMaterial.SetShaderParameter("pass_mode", 1); // BodyOnly
        _bodySprite.Material = _bodyMaterial;

        if (_bodySprite.GetParent() == null)
        {
            AddChild(_bodySprite);
        }
    }

    private void UpdateTextures()
    {
        if (_bottomSprite != null) _bottomSprite.Texture = _texture;
        if (_bodySprite != null) _bodySprite.Texture = _texture;
    }

    private void UpdateShaderUniforms()
    {
        if (_bottomMaterial == null || _bodyMaterial == null)
        {
            return;
        }

        foreach (var material in new[] { _bottomMaterial, _bodyMaterial })
        {
            material.SetShaderParameter("split_mode", (int)_splitMode);
            material.SetShaderParameter("bottom_ratio", _bottomRatio);
            material.SetShaderParameter("feature_y_start", _featureYStart);
            material.SetShaderParameter("grass_ratio_r", _grassRatioR);
            material.SetShaderParameter("grass_ratio_b", _grassRatioB);
            material.SetShaderParameter("has_decal_mask", _decalMask != null);
            material.SetShaderParameter("decal_mask", _decalMask);
        }
    }
}
