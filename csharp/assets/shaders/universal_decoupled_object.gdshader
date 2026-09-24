shader_type canvas_item;
render_mode blend_mix;

// Ground decals are composed BEFORE ALL upright bodies. Never infer receiver
// identity from screen RGB: terrain varies, and walls can have the same color.
uniform int pass_mode : hint_range(0, 1) = 0;
uniform int split_mode : hint_range(0, 2) = 0;
uniform float bottom_ratio : hint_range(0.0, 0.4) = 0.10;
uniform float feature_y_start : hint_range(0.0, 1.0) = 0.52;
uniform float grass_ratio_r : hint_range(0.5, 1.5) = 0.95;
uniform float grass_ratio_b : hint_range(0.5, 1.5) = 1.08;
uniform sampler2D decal_mask : filter_nearest, repeat_disable;
uniform bool has_decal_mask = false;
varying vec4 item_tint;

void vertex() {
    item_tint = COLOR;
}

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);
    bool is_decal = false;
    if (split_mode == 0) {
        is_decal = bottom_ratio > 0.0 && UV.y >= 1.0 - bottom_ratio;
    } else if (split_mode == 1) {
        is_decal = UV.y >= feature_y_start &&
            tex_color.g > tex_color.r * grass_ratio_r &&
            tex_color.g > tex_color.b * grass_ratio_b;
    } else if (has_decal_mask) {
        vec4 mask_value = texture(decal_mask, UV);
        is_decal = mask_value.r * mask_value.a >= 0.5;
    }
    // Disjoint membership: no skirt remnants in the body pass, no double alpha.
    // A missing explicit mask fails closed (entire source stays in the body).
    if ((pass_mode == 0) != is_decal) {
        discard;
    }
    COLOR = tex_color * item_tint;
}
