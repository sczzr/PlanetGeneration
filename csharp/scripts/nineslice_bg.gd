class_name NineSliceBg
extends Control

## 超采样九宫格背景：纹理按 3x3 等分网格排布，绘制时把每个网格单元映射到
## 设计空间的对应区域（源/目标矩形独立指定），因此纹理密度可以倍于设计尺寸，
## 在 canvas_items 高倍拉伸（4K 屏）下仍保持原生分辨率不模糊。
## StyleBoxTexture 的切缝是纹理像素与设计像素 1:1 硬映射，无法利用高分辨率
## 纹理；凡九宫格贴图需要高分屏锐度时用本控件自绘替代样式盒。
## 作为宿主 Control 的子节点使用；按钮场景配合 show_behind_parent 画在宿主之下。

## 九宫格纹理（3x3 等分网格；单元边长应为设计切缝的整数倍以获得超采样）。
var texture: Texture2D:
	set(value):
		texture = value
		queue_redraw()

## 设计空间切缝（屏幕像素）：角部与边条的渲染宽度。
var margin_x := 12.0:
	set(value):
		margin_x = value
		queue_redraw()

var margin_y := 9.0:
	set(value):
		margin_y = value
		queue_redraw()

## 整体着色（悬浮提亮等状态差异复用同一张纹理时使用）。
var tint := Color.WHITE:
	set(value):
		tint = value
		queue_redraw()


func _init(tex: Texture2D = null, mx: float = 12.0, my: float = 9.0) -> void:
	# 不拦截鼠标，避免遮住宿主控件的悬浮/点击判定
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	texture = tex
	margin_x = mx
	margin_y = my


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	if texture == null or size.x < 1.0 or size.y < 1.0:
		return
	var cell := texture.get_size() / 3.0
	# 切缝超出控件尺寸时收缩，保证中心区域非负
	var mx := minf(margin_x, size.x * 0.5)
	var my := minf(margin_y, size.y * 0.5)
	var cw := size.x - 2.0 * mx
	var ch := size.y - 2.0 * my
	for row in 3:
		# 目标行 y：0 / my / my+ch；行高：my / ch / my
		var dy := my * row if row < 2 else my + ch
		var dh := my if row != 1 else ch
		for col in 3:
			var dx := mx * col if col < 2 else mx + cw
			var dw := mx if col != 1 else cw
			var src := Rect2(col * cell.x, row * cell.y, cell.x, cell.y)
			draw_texture_rect_region(texture, Rect2(dx, dy, dw, dh), src, tint)
