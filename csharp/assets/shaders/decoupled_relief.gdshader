shader_type canvas_item;
render_mode blend_mix;

// 0: SkirtOnly (仅绘制接地草皮裙边：置于地表贴花层 Z=0，只覆盖土地，绝不覆盖其他素材)
// 1: BodyOnly  (仅绘制立体主体：置于物件层 Z=1，参与正常 Y-Sort 深度排序)
uniform int pass_mode : hint_enum("SkirtOnly:0,BodyOnly:1") = 0;

// 地貌素材下半部接地草皮判定的起始高度（归一化 UV.y，山顶雪峰与岩壁不参与判定）
uniform float skirt_y_start : hint_range(0.0, 1.0, 0.01) = 0.52;

// 草地色系判定阈值（偏橄榄绿/黄绿色草皮）
uniform float grass_ratio_r : hint_range(0.8, 1.3, 0.01) = 0.95;
uniform float grass_ratio_b : hint_range(0.9, 1.5, 0.01) = 1.08;

bool is_ground_skirt(vec4 col, vec2 uv) {
	if (uv.y < skirt_y_start) {
		return false;
	}
	// 判定为外围接地草地过渡带
	return (col.g > col.r * grass_ratio_r) && (col.g > col.b * grass_ratio_b);
}

void fragment() {
	vec4 col = texture(TEXTURE, UV);
	if (col.a < 0.01) {
		discard;
	}
	
	bool is_skirt = is_ground_skirt(col, UV);
	
	if (pass_mode == 0) {
		// --- 接地裙边模式 (SkirtOnly) ---
		// 仅绘制外围接地草皮，山体主体完全丢弃
		if (!is_skirt) {
			discard;
		}
	} else {
		// --- 立体主体模式 (BodyOnly) ---
		// 绘制完整巍峨山体（雪顶、岩壁、前坡山脊全部保留），仅丢弃外围接地草皮
		if (is_skirt) {
			discard;
		}
	}
	
	COLOR = col;
}
