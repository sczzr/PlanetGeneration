shader_type canvas_item;
render_mode blend_mul;

// 古董羊皮纸质感覆盖层：微纤细纹、茶渍水痕与古典暗角。
// 采用 blend_mul（正片叠底），亮色无影响，纸质肌理与泛黄自然融入底层的矢量水彩图层。

uniform float fiber_strength : hint_range(0.0, 0.15, 0.005) = 0.040;
uniform float mottle_strength : hint_range(0.0, 0.20, 0.005) = 0.055;
uniform float vignette_strength : hint_range(0.0, 0.30, 0.01) = 0.080;
uniform vec4 paper_tint : source_color = vec4(0.98, 0.96, 0.91, 1.0);

// 伪随机哈希
float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// 连续平滑值噪声 (Value Noise)
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);

	float a = hash(i + vec2(0.0, 0.0));
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));

	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// 多阶分形噪声，模拟古纸水痕浸润与岁月风化
float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	mat2 rot = mat2(vec2(0.8, -0.6), vec2(0.6, 0.8));
	for (int i = 0; i < 3; i++) {
		v += a * vnoise(p);
		p = rot * p * 2.02;
		a *= 0.5;
	}
	return v;
}

void fragment() {
	vec2 p = UV * vec2(48.0, 24.0);

	// 1. 高频古纸微纤维（横竖交织质感）
	float fiber_h = hash(UV * vec2(1024.0, 256.0) + vec2(13.1, 71.7));
	float fiber_v = hash(UV * vec2(256.0, 1024.0) + vec2(37.9, 19.3));
	float fiber = (fiber_h * 0.6 + fiber_v * 0.4) - 0.5;

	// 2. 低频羊皮纸水痕斑驳
	float mottle = fbm(p * 0.85) - 0.5;

	// 3. 地图外缘微弱自然收敛（四角微暗）
	vec2 d = abs(UV - vec2(0.5)) * 2.0;
	float edge = max(d.x, d.y);
	float vig = smoothstep(0.72, 1.0, edge) * vignette_strength;

	// 组合正片叠底颜色
	vec3 col = paper_tint.rgb;
	col -= vec3(fiber * fiber_strength);
	col -= vec3(mottle * mottle_strength);
	col *= (1.0 - vig);

	COLOR = vec4(clamp(col, 0.0, 1.0), 1.0);
}
