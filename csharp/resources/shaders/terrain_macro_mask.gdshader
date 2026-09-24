shader_type canvas_item;
render_mode blend_mix;

// 区域羽化遮罩纹理（R通道存储平滑深度或 Alpha 权重）
uniform sampler2D region_mask : hint_default_white, filter_linear, repeat_disable;

// 基础调色
uniform vec4 tint_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// 边缘羽化阈值（在 mask 距离场上的平滑过渡跨度）
uniform float edge_threshold : hint_range(0.0, 0.5) = 0.04;
uniform float edge_softness : hint_range(0.001, 0.5) = 0.12;

// 宏观材质 UV 变换
uniform vec2 uv_scale = vec2(1.0, 1.0);
uniform vec2 uv_offset = vec2(0.0, 0.0);

// 整体透明度调节
uniform float overall_alpha : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    // 采样主纹理（宏观森林/沙丘/地貌图元）
    vec2 macro_uv = UV * uv_scale + uv_offset;
    vec4 tex = texture(TEXTURE, macro_uv);

    // 采样局部 Region Mask 距离场（R 通道：0.0 边界外/边缘，1.0 核心深处）
    float mask_val = texture(region_mask, UV).r;

    // 平滑羽化计算
    float mask_alpha = smoothstep(edge_threshold, edge_threshold + edge_softness, mask_val);

    // 最终颜色合成
    COLOR = tex * tint_color;
    COLOR.a *= mask_alpha * overall_alpha;
}
