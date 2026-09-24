using Godot;

namespace PlanetGeneration;

public partial class ThemeManager : Node
{
    public static ThemeManager? Instance { get; private set; }
    public enum ThemeType { Chinese, Steampunk }
    private ThemeType _currentTheme = ThemeType.Steampunk;
    public ThemeType CurrentTheme => _currentTheme;
    [Export] public Theme? ChineseTheme { get; set; }
    [Export] public Theme? SteampunkTheme { get; set; }

    public override void _Ready() => Instance = this;
    public override void _ExitTree() { if (Instance == this) Instance = null; }
    public void ToggleTheme() => SetTheme(_currentTheme == ThemeType.Chinese ? ThemeType.Steampunk : ThemeType.Chinese);
    public void SetTheme(ThemeType theme) { _currentTheme = theme; RefreshTheme(); }

    public void RefreshTheme()
    {
        var main = GetParent() as Control;
        if (main == null) return;
        var source = _currentTheme == ThemeType.Chinese ? ChineseTheme : SteampunkTheme;
        if (source != null) main.Theme = (Theme)source.Duplicate(true);
        ApplyColors(main);
    }

    private void ApplyColors(Node node)
    {
        if (node is UI.WorldSetupController
            || node.Name == "WorldSetupMenu"
            || node.Name == "MainMenu"
            || node.Name == "PauseMenu"
            || node.Name == "OverlayLayer"
            || node.Name == "WorldConfigMenu"
            || node.Name == "ModeSelectMenu"
            || node.Name == "SettingsMenu") return;
        var isChinese = _currentTheme == ThemeType.Chinese;
        var accent = isChinese ? new Color("5ed1c1") : new Color("e8a256");
        var text = isChinese ? new Color("ecf5f3") : new Color("f4ebe1");
        var surface = isChinese ? new Color("10171e") : new Color("1c1511");
        var field = isChinese ? new Color("162029") : new Color("251b15");
        var border = isChinese ? new Color("274352") : new Color("5a3d28");
        var activeColor = isChinese ? new Color("1a3d3c") : new Color("422a18");
        var hoverColor = isChinese ? new Color("1f2f3c") : new Color("32231b");

        if (node is ColorRect rect && rect.Name == "Background")
        {
            rect.Color = isChinese ? new Color("090e13") : new Color("0d0908");
        }

        if (node is Control control)
        {
            foreach (var key in new[] { "panel", "normal", "hover", "pressed", "disabled", "focus",
                         "background", "fill", "slider", "grabber_area", "grabber_area_highlight",
                         "tab_selected", "tab_unselected", "tab_hovered", "tab_focus" })
            {
                if (!control.HasThemeStylebox(key)) continue;
                if (control.GetThemeStylebox(key) is not StyleBoxFlat original) continue;
                if (control is CheckButton && key is "normal" or "hover" or "pressed" or "disabled") continue;

                var style = (StyleBoxFlat)original.Duplicate();
                var active = key is "pressed" or "tab_selected" or "fill" or "grabber_area" or "grabber_area_highlight";

                if (key is "tab_selected")
                {
                    style.BgColor = surface.Lightened(0.12f);
                    style.BorderColor = accent;
                    style.BorderWidthTop = 3;
                }
                else if (key is "tab_hovered")
                {
                    style.BgColor = hoverColor;
                    style.BorderColor = accent.Lerp(border, 0.4f);
                }
                else if (key is "tab_unselected")
                {
                    style.BgColor = surface.Darkened(0.18f);
                    style.BorderColor = border.Darkened(0.2f);
                    style.BorderWidthTop = 0;
                }
                else
                {
                    style.BgColor = active ? activeColor
                        : key == "hover" ? hoverColor
                        : key == "normal" ? field
                        : surface;
                }

                if (key == "slider" || key == "background")
                {
                    style.BgColor = surface.Darkened(0.3f);
                    style.BorderColor = border.Darkened(0.1f);
                }
                if (key == "fill" || key == "grabber_area" || key == "grabber_area_highlight")
                {
                    style.BgColor = accent;
                }

                style.BorderColor = (active || key == "focus" || key == "tab_focus") ? accent : border;

                // ConsolePanel gets floating drop shadow for game depth
                var nodeName = control.Name.ToString();
                if (key == "panel" && (nodeName.Contains("ConsolePanel") || nodeName.Contains("PreviewPanel") || nodeName.Contains("WorldSetupPanel")))
                {
                    style.ShadowColor = new Color(0f, 0f, 0f, 0.65f);
                    style.ShadowSize = 20;
                    style.ShadowOffset = new Vector2(0, 6);
                }
                else if (key == "panel" && (nodeName.EndsWith("Card") || nodeName.Contains("Group") || nodeName.Contains("Body")))
                {
                    style.BgColor = field.Darkened(0.1f);
                    style.BorderColor = border.Lerp(accent, 0.12f);
                    style.ShadowColor = new Color(0f, 0f, 0f, 0.25f);
                    style.ShadowSize = 4;
                    style.ShadowOffset = new Vector2(1, 2);
                }
                else
                {
                    style.ShadowColor = Colors.Transparent;
                }

                if (control is Button button && button is not OptionButton && button is not CheckButton && button is not CheckBox)
                {
                    if (button.Name == "GenerateButton" || button.Name == "ConfirmGenerateButton")
                    {
                        // Hero Primary Action CTA
                        style.BgColor = key == "pressed" ? accent.Darkened(0.15f) : key == "hover" ? accent.Lightened(0.12f) : accent;
                        style.BorderColor = key == "pressed" ? new Color("fff2b8") : new Color("ffe38a");
                        style.SetBorderWidthAll(2);
                        button.AddThemeColorOverride("font_color", isChinese ? new Color("061a19") : new Color("1a1005"));
                        button.AddThemeColorOverride("font_hover_color", Colors.Black);
                        button.AddThemeColorOverride("font_pressed_color", Colors.Black);
                    }
                    else if (button.Name == "ConsoleCollapseTab" || button.Name == "CloseCornerButton")
                    {
                        // Close / Collapse window button with subtle wine-red highlight on hover/pressed
                        if (key == "hover")
                        {
                            style.BgColor = new Color(0.65f, 0.20f, 0.20f, 0.95f);
                            style.BorderColor = new Color(1.0f, 0.50f, 0.50f, 1f);
                            button.AddThemeColorOverride("font_hover_color", Colors.White);
                        }
                        else if (key == "pressed")
                        {
                            style.BgColor = new Color(0.45f, 0.12f, 0.12f, 1f);
                            style.BorderColor = new Color(0.9f, 0.35f, 0.35f, 1f);
                            button.AddThemeColorOverride("font_pressed_color", Colors.White);
                        }
                        else
                        {
                            style.BgColor = surface.Lerp(border, 0.18f);
                            style.BorderColor = border.Lerp(accent, 0.2f);
                            button.AddThemeColorOverride("font_color", new Color(0.92f, 0.82f, 0.78f, 1f));
                        }
                    }
                    else if (key is "normal" or "pressed" or "hover")
                    {
                        style.BgColor = surface.Lerp(accent, key == "pressed" ? 0.35f : key == "hover" ? 0.25f : 0.14f);
                        style.BorderColor = new Color(accent.R, accent.G, accent.B, key == "pressed" ? 1f : key == "hover" ? 0.85f : 0.6f);
                    }
                }

                if (key == "focus")
                {
                    style.DrawCenter = false;
                    style.SetBorderWidthAll(1);
                    style.BorderColor = new Color(accent.R, accent.G, accent.B, 0.6f);
                }

                control.AddThemeStyleboxOverride(key, style);
            }

            foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_selected_color", "font_unselected_color", "default_color" })
            {
                control.AddThemeColorOverride(key, text);
            }
            control.AddThemeColorOverride("font_disabled_color", new Color("728391"));

            if (control is TabContainer tabContainer)
            {
                tabContainer.AddThemeColorOverride("font_selected_color", accent);
                tabContainer.AddThemeColorOverride("font_hovered_color", text);
                tabContainer.AddThemeColorOverride("font_unselected_color", text.Darkened(0.3f));
            }

            if (control is Label)
            {
                var name = control.Name.ToString();
                if (name.Contains("Title") || name.EndsWith("Value"))
                {
                    control.AddThemeColorOverride("font_color", accent);
                }
                else if (name.StartsWith("Section"))
                {
                    control.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.95f));
                }
                else if (name.EndsWith("Label") && (name.Contains("Status") || name.Contains("Info")))
                {
                    control.AddThemeColorOverride("font_color", text.Darkened(0.2f));
                }
            }
        }

        foreach (var child in node.GetChildren()) ApplyColors(child);
    }
}
