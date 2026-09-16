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
        var accent = _currentTheme == ThemeType.Chinese
            ? new Color("64c9bb") : new Color("dfa778");
        var text = new Color("e0e8ed");
        var surface = new Color("17232f");
        var field = new Color("1d2b38");
        var border = new Color("344858");
        if (node is ColorRect rect && rect.Name == "Background") rect.Color = new Color("0b1017");
        if (node is Control control)
        {
            // Duplicate the resolved style instead of replacing it with an empty box:
            // margins, radii, track thickness and layout metrics must survive theme changes.
            foreach (var key in new[] { "panel", "normal", "hover", "pressed", "disabled", "focus",
                         "background", "fill", "slider", "grabber_area", "grabber_area_highlight",
                         "tab_selected", "tab_unselected", "tab_hovered", "tab_focus" })
            {
                if (!control.HasThemeStylebox(key)) continue;
                if (control.GetThemeStylebox(key) is not StyleBoxFlat original) continue;
                // CheckButton paints its own switch icon; a box behind it is just noise.
                if (control is CheckButton && key is "normal" or "hover" or "pressed" or "disabled") continue;
                var style = (StyleBoxFlat)original.Duplicate();
                var active = key is "pressed" or "tab_selected" or "fill" or "grabber_area" or "grabber_area_highlight";
                style.BgColor = active ? new Color("284b50")
                    : key is "hover" or "tab_hovered" ? new Color("293d4d")
                    : key == "normal" ? field
                    : surface;
                if (key == "slider" || key == "background") style.BgColor = new Color("2a3a49");
                if (key == "fill" || key == "grabber_area" || key == "grabber_area_highlight") style.BgColor = accent;
                style.BorderColor = active || key == "focus" || key == "tab_focus" ? accent : border;
                style.ShadowColor = Colors.Transparent;
                if (control is Button button && button is not OptionButton && button is not CheckButton
                    && button is not CheckBox && key is "normal" or "pressed")
                {
                    // Plain action buttons lean toward the accent so they read as clickable.
                    style.BgColor = surface.Lerp(accent, key == "pressed" ? 0.3f : 0.16f);
                    style.BorderColor = new Color(accent.R, accent.G, accent.B, key == "pressed" ? 1f : 0.7f);
                }
                if (key == "focus")
                {
                    // A hairline ring; the default opaque plate would hide the focused control.
                    style.DrawCenter = false;
                    style.SetBorderWidthAll(1);
                    style.BorderColor = new Color(accent.R, accent.G, accent.B, 0.55f);
                }
                control.AddThemeStyleboxOverride(key, style);
            }
            foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_selected_color", "font_unselected_color", "default_color" })
                control.AddThemeColorOverride(key, text);
            control.AddThemeColorOverride("font_disabled_color", new Color("82929f"));
            if (control is Label)
            {
                var name = control.Name.ToString();
                if (name.Contains("Title") || name.EndsWith("Value"))
                    control.AddThemeColorOverride("font_color", accent);
                else if (name.StartsWith("Section"))
                    control.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.8f));
            }
        }
        foreach (var child in node.GetChildren()) ApplyColors(child);
    }
}
