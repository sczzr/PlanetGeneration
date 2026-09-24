class_name NineSlicePanel
extends Control

## 超采样九宫格面板：背景 NineSliceBg 铺满整面板且不参与内容布局，
## 内容统一加到 content（MarginContainer）里以内边距排布；
## 最小尺寸随内容收缩，可像 PanelContainer 一样放进盒子容器。
## 用于替代"StyleBoxTexture 九宫格 + PanelContainer"方案以获得高分屏锐度。

var bg: NineSliceBg
var content: MarginContainer


func _init(tex: Texture2D, mx: float, my: float, pad_h: float = 18.0, pad_v: float = 14.0) -> void:
	bg = NineSliceBg.new(tex, mx, my)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)

	content = MarginContainer.new()
	content.set_anchors_preset(Control.PRESET_FULL_RECT)
	content.add_theme_constant_override("margin_left", int(pad_h))
	content.add_theme_constant_override("margin_right", int(pad_h))
	content.add_theme_constant_override("margin_top", int(pad_v))
	content.add_theme_constant_override("margin_bottom", int(pad_v))
	add_child(content)


func _get_minimum_size() -> Vector2:
	return content.get_combined_minimum_size()
