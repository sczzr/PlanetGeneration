shader_type canvas_item;

// 主菜单背景处理：mip LOD 模糊 + 压暗降饱和 + 缓慢移动的光带 + 暗角。
// 目标是压掉背景细节，让中间的标题和按钮保持足够对比度。
//
// 模糊靠贴图的 mipmap 完成（导入已开 mipmap）：一次采样让纹理单元按 lod 做盒式滤波，
// 替代早期 24 次黄金角螺旋采样，每帧 GPU 开销从毫秒级降到零头，效果更平滑。
// blur_level 每加 1，等效模糊半径翻倍；4.9 ≈ 旧版 0.0075 屏宽等效半径（含 2D 放大时基准 LOD≈-1.6 的偏移）。

uniform float blur_level : hint_range(0.0, 6.0, 0.1) = 4.9;
uniform float brightness : hint_range(0.0, 1.5, 0.01) = 0.38;
uniform float saturation : hint_range(0.0, 1.5, 0.01) = 0.7;
uniform vec4 tint : source_color = vec4(0.58, 0.68, 0.92, 1.0);
uniform float tint_amount : hint_range(0.0, 1.0, 0.01) = 0.35;
uniform float vignette_start : hint_range(0.0, 1.0, 0.01) = 0.2;
uniform float vignette_strength : hint_range(0.0, 1.0, 0.01) = 0.7;
uniform float light_speed : hint_range(0.0, 1.0, 0.01) = 0.06;
uniform float light_strength : hint_range(0.0, 0.6, 0.01) = 0.13;
uniform float grain_strength : hint_range(0.0, 0.2, 0.005) = 0.02;

// 无三角函数的哈希噪声，用于极轻微的颗粒感。
float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
	// 带 LOD 偏移的单次采样完成全部模糊。
	vec3 col = texture(TEXTURE, UV, blur_level).rgb;

	float luma = dot(col, vec3(0.2126, 0.7152, 0.0722));
	col = mix(vec3(luma), col, saturation);
	col = mix(col, col * tint.rgb, tint_amount);
	col *= brightness;

	vec2 suv = SCREEN_UV;
	float band = sin((suv.x * 1.3 + suv.y * 0.7) * PI - TIME * light_speed * TAU);
	col += vec3(0.85, 0.9, 1.0) * smoothstep(0.05, 1.0, band) * light_strength * (0.35 + luma);

	float edge = distance(suv, vec2(0.5)) * 1.35;
	col *= mix(1.0, 1.0 - vignette_strength, smoothstep(vignette_start, 1.0, edge));

	col += (hash12(suv * vec2(1920.0, 1080.0) + vec2(TIME)) - 0.5) * grain_strength;

	// 沿用传入 alpha（顶点色里已折叠 modulate 链）而不是写死 1.0：
	// 加载遮罩靠根节点 modulate:a 淡入淡出，写死会让背景硬切、与前景文字脱节。
	COLOR = vec4(max(col, vec3(0.0)), COLOR.a);
}
