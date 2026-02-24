using Godot;

namespace PlanetGeneration;

public partial class ThemeManager : Node
{
    public static ThemeManager? Instance { get; private set; }
    
    public enum ThemeType
    {
        Chinese,
        Steampunk
    }
    
    private ThemeType _currentTheme = ThemeType.Chinese;
    public ThemeType CurrentTheme => _currentTheme;
    
    private Theme? _currentThemeResource;
    
    [Export]
    public Theme? ChineseTheme { get; set; }
    
    [Export]
    public Theme? SteampunkTheme { get; set; }
    
    public override void _Ready()
    {
        Instance = this;
    }
    
    public void ToggleTheme()
    {
        _currentTheme = _currentTheme switch
        {
            ThemeType.Chinese => ThemeType.Steampunk,
            ThemeType.Steampunk => ThemeType.Chinese,
            _ => ThemeType.Chinese
        };
        ApplyTheme();
    }
    
    public void SetTheme(ThemeType theme)
    {
        if (_currentTheme != theme)
        {
            _currentTheme = theme;
            ApplyTheme();
        }
    }
    
    private void ApplyTheme()
    {
        var root = GetTree().Root;
        if (root == null) return;
        
        var mainControl = root.GetNodeOrNull<Control>("Main");
        if (mainControl == null) return;
        
        _currentThemeResource = _currentTheme switch
        {
            ThemeType.Chinese => ChineseTheme,
            ThemeType.Steampunk => SteampunkTheme,
            _ => ChineseTheme
        };
        
        if (_currentThemeResource != null)
        {
            ApplyThemeResource(mainControl, _currentThemeResource);
        }
        
        ApplyColorOverlays(mainControl, _currentTheme);
    }
    
    private void ApplyThemeResource(Control root, Theme theme)
    {
        root.Theme = theme;
        
        foreach (var child in root.GetChildren())
        {
            if (child is Control control)
            {
                control.Theme = theme;
                ApplyThemeToChildren(control, theme);
            }
        }
    }
    
    private void ApplyThemeToChildren(Control control, Theme theme)
    {
        foreach (var child in control.GetChildren())
        {
            if (child is Control childControl)
            {
                childControl.Theme = theme;
                ApplyThemeToChildren(childControl, theme);
            }
        }
    }
    
    private void ApplyColorOverlays(Control root, ThemeType theme)
    {
        ApplyNodeColors(root, theme);
    }
    
    private void ApplyNodeColors(Node node, ThemeType theme)
    {
        if (node is ColorRect colorRect)
        {
            var name = colorRect.Name.ToString();
            if (name == "Background")
            {
                colorRect.Color = GetBackgroundColor(theme);
            }
            else if (name == "OverlayTint")
            {
                colorRect.Color = GetOverlayColor(theme);
            }
        }
        
        if (node is PanelContainer panel)
        {
            var bgColor = GetPanelBgColor(theme);
            var borderColor = GetPanelBorderColor(theme);
            
            var newStyle = new StyleBoxFlat();
            newStyle.BgColor = bgColor;
            newStyle.BorderColor = borderColor;
            newStyle.BorderWidthLeft = theme == ThemeType.Steampunk ? 2 : 1;
            newStyle.BorderWidthTop = theme == ThemeType.Steampunk ? 2 : 1;
            newStyle.BorderWidthRight = theme == ThemeType.Steampunk ? 2 : 1;
            newStyle.BorderWidthBottom = theme == ThemeType.Steampunk ? 2 : 1;
            newStyle.CornerRadiusTopLeft = 6;
            newStyle.CornerRadiusTopRight = 6;
            newStyle.CornerRadiusBottomRight = 6;
            newStyle.CornerRadiusBottomLeft = 6;
            
            panel.AddThemeStyleboxOverride("panel", newStyle);
        }
        
        if (node is Label label)
        {
            label.AddThemeColorOverride("font_color", GetLabelColor(theme, label.Name.ToString()));
        }
        
        if (node is Button button)
        {
            var name = button.Name.ToString();
            if (name == "GenerateButton")
            {
                button.AddThemeColorOverride("font_color", GetButtonAccentColor(theme));
            }
            else
            {
                button.AddThemeColorOverride("font_color", GetTextColor(theme));
            }
        }
        
        if (node is HSlider slider)
        {
            slider.AddThemeColorOverride("font_color", GetSliderColor(theme));
        }
        
        if (node is OptionButton optionButton)
        {
            optionButton.AddThemeColorOverride("font_color", GetTextColor(theme));
        }
        
        if (node is CheckBox checkBox)
        {
            checkBox.AddThemeColorOverride("font_color", GetTextColor(theme));
        }
        
        if (node is SpinBox spinBox)
        {
            spinBox.AddThemeColorOverride("font_color", GetTextColor(theme));
        }
        
        if (node is RichTextLabel richText)
        {
            richText.AddThemeColorOverride("default_color", GetTextColor(theme));
        }
        
        if (node is ProgressBar progressBar)
        {
            progressBar.AddThemeColorOverride("font_color", GetTextColor(theme));
        }
        
        foreach (var child in node.GetChildren())
        {
            ApplyNodeColors(child, theme);
        }
    }
    
    private Color GetBackgroundColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.95f, 0.92f, 0.86f, 1f),
            ThemeType.Steampunk => new Color(0.17f, 0.11f, 0.09f, 1f),
            _ => new Color(0.95f, 0.92f, 0.86f, 1f)
        };
    }
    
    private Color GetPanelBgColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.91f, 0.89f, 0.82f, 1f),
            ThemeType.Steampunk => new Color(0.23f, 0.14f, 0.11f, 1f),
            _ => new Color(0.91f, 0.89f, 0.82f, 1f)
        };
    }
    
    private Color GetPanelBorderColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.37f, 0.39f, 0.37f, 0.5f),
            ThemeType.Steampunk => new Color(0.83f, 0.62f, 0.21f, 0.6f),
            _ => new Color(0.37f, 0.39f, 0.37f, 0.5f)
        };
    }
    
    private Color GetOverlayColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.1f, 0.1f, 0.1f, 0.3f),
            ThemeType.Steampunk => new Color(0.05f, 0.03f, 0.02f, 0.5f),
            _ => new Color(0.1f, 0.1f, 0.1f, 0.3f)
        };
    }
    
    private Color GetLabelColor(ThemeType theme, string name)
    {
        if (name.Contains("Title") || name == "Title" || name == "Subtitle" || 
            name.EndsWith("Value") || name.Contains("Value"))
        {
            return GetTitleColor(theme);
        }
        return GetTextColor(theme);
    }
    
    private Color GetTitleColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.18f, 0.36f, 0.31f, 1f),
            ThemeType.Steampunk => new Color(0.83f, 0.69f, 0.22f, 1f),
            _ => new Color(0.18f, 0.36f, 0.31f, 1f)
        };
    }
    
    private Color GetTextColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.17f, 0.17f, 0.17f, 1f),
            ThemeType.Steampunk => new Color(0.92f, 0.85f, 0.70f, 1f),
            _ => new Color(0.17f, 0.17f, 0.17f, 1f)
        };
    }
    
    private Color GetSliderColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.69f, 0.71f, 0.17f, 1f),
            ThemeType.Steampunk => new Color(1.00f, 0.44f, 0.26f, 1f),
            _ => new Color(0.69f, 0.71f, 0.17f, 1f)
        };
    }
    
    private Color GetButtonAccentColor(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Chinese => new Color(0.75f, 0.28f, 0.32f, 1f),
            ThemeType.Steampunk => new Color(0.90f, 0.70f, 0.35f, 1f),
            _ => new Color(0.75f, 0.28f, 0.32f, 1f)
        };
    }
}
