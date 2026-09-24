shader_type canvas_item;

// 创建世界菜单背景处理：空灵诗意晨雾磨砂画风（Artistic Morning Mist Frosted Glass）
// 启发自《ARIA》概念艺术画风：明朗天光、晨曦微雾、青翠草甸、柔和胶片画意。

uniform float blur_level : hint_range(0.0, 6.0, 0.1) = 3.6;
uniform float brightness : hint_range(0.0, 1.5, 0.01) = 0.98;
uniform float saturation : hint_range(0.0, 1.5, 0.01) = 1.05;
uniform vec4 tint : source_color = vec4(0.92, 0.96, 0.95, 1.0);
uniform float tint_amount : hint_range(0.0, 1.0, 0.01) = 0.10;
uniform float vignette_start : hint_range(0.0, 1.0, 0.01) = 0.45;
uniform float vignette_strength : hint_range(0.0, 1.0, 0.01) = 0.15;
uniform float grain_strength : hint_range(0.0, 0.1, 0.002) = 0.008;

// 高效哈希随机噪声，模拟美术画纸与微磨砂漫反射质感
float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
	// 单次 mip LOD 采样实现高性能大半径柔焦画风
	vec3 col = texture(TEXTURE, UV, blur_level).rgb;

	// 饱和度与晨曦天光微调
	float luma = dot(col, vec3(0.2126, 0.7152, 0.0722));
	col = mix(vec3(luma), col, saturation);
	col = mix(col, col * tint.rgb, tint_amount);
	col *= brightness;

	// 自然微弱边缘柔焦（避免黑角遮挡自然画意）
	vec2 suv = SCREEN_UV;
	float edge = distance(suv, vec2(0.5)) * 1.35;
	col *= mix(1.0, 1.0 - vignette_strength, smoothstep(vignette_start, 1.0, edge));

	// 细腻画纸磨砂微粒
	col += (hash12(suv * vec2(1920.0, 1080.0) + vec2(17.3, 43.7)) - 0.5) * grain_strength;

	COLOR = vec4(max(col, vec3(0.0)), 1.0);
}
