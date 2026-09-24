class_name ThemedSliderSkin
extends Control

## 主题水彩滑条皮肤：以超采样密度自绘轨道/填充/把手，并绘制沉浸光感。
## 背景：StyleBoxTexture 的九宫格 margin 在纹理/设计两侧同值，无法利用高分贴图；
## Slider 的 grabber 主题图标按原始尺寸绘制（96px 贴图会画成 96px 大）。
## 因此轨道/填充/把手全部经 draw_texture_rect_region（源/目标矩形独立）绘制，
## 高分贴图可以任意缩放而保持锐利。
## 沉浸光感（参考华为旗舰机的光效语言）：光从把手端柔和渗出、沿凹槽渐隐，
## 凹槽周围一圈微弱光晕极缓慢地呼吸——弥散、安静、无流动追逐。

## 宿主滑条（add_child 之前必须赋值；皮肤画在滑条之下）。
var slider: HSlider

## 轨道 / 填充 / 把手两态贴图（把手为 96x96 超采样图标，按 24 设计像素绘制）。
var track_tex: Texture2D
var fill_tex: Texture2D
var grabber_tex: Texture2D
var grabber_hl_tex: Texture2D

## 光效主题："" 无光感；"water"；"mountain"；"rock"。
var particle_theme := ""

## 设计空间尺寸（屏幕像素）：轨道高度、把手边长、端帽宽。
const BAR_H := 12.0
const GRABBER_S := 24.0
const CAP_DST := 6.0
## 源图（512x96）上的端帽宽与中心切片位置。
const CAP_SRC := 12.0
const SLICE_SRC_X := 250.0
const SLICE_SRC_W := 12.0
## 凹槽（轨道贴图中央的暗色通道）在设计空间中的位置与高度。
## 填充条与光感都收进这条凹槽里，不盖整条轨道。
const GROOVE_Y := 4.3
const GROOVE_H := 2.0
## 填充源图的纵向实心中段（避开上下软 alpha 边，缩放后不外溢出凹槽）。
const FILL_SRC_Y := 24.0
const FILL_SRC_H := 48.0
## 光晕呼吸周期（秒）：极缓慢，营造沉浸而非动感。
const BREATHE_PERIOD := 5.2

## 光感动画累计时间（秒）。
var _t := 0.0
## 悬停状态（Control 无 is_hovered，用信号自记）。
var _hover := false

## 光感主题参数：亮部色（近把手端）、氛围色（元素主题色）、强度。
var _head_color := Color.WHITE
var _tail_color := Color.WHITE
var _glow_strength := 0.5


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	show_behind_parent = true
	if slider == null:
		return
	# 直接跟随宿主尺寸（锚点在父级后期增高时不会自动跟随，实测高度卡在 0）
	size = slider.size
	slider.resized.connect(func() -> void:
		size = slider.size
		_sync())
	# 值变化/悬停/焦点驱动重绘
	slider.value_changed.connect(func(_v: float) -> void: _sync())
	slider.mouse_entered.connect(func() -> void: _hover = true; queue_redraw())
	slider.mouse_exited.connect(func() -> void: _hover = false; queue_redraw())
	slider.focus_entered.connect(queue_redraw)
	slider.focus_exited.connect(queue_redraw)
	_setup_glow()


func _setup_glow() -> void:
	match particle_theme:
		"water":
			# 水系：冰白亮部 + 青蓝氛围光
			_head_color = Color(0.88, 0.98, 1.0)
			_tail_color = Color(0.30, 0.68, 0.95)
			_glow_strength = 0.55
			set_process(true)
		"mountain":
			# 山系：炽金亮部 + 琥珀氛围光
			_head_color = Color(1.0, 0.95, 0.78)
			_tail_color = Color(0.92, 0.62, 0.22)
			_glow_strength = 0.5
			set_process(true)
		"rock":
			# 岩系：亮橙白亮部 + 铜褐氛围光
			_head_color = Color(1.0, 0.92, 0.80)
			_tail_color = Color(0.80, 0.48, 0.24)
			_glow_strength = 0.5
			set_process(true)
		_:
			set_process(false)


func _process(delta: float) -> void:
	# 光效动画：累计时间驱动重绘（常驻动画，不依赖交互）
	_t += delta
	queue_redraw()


## 把手中心的 x 坐标（与 HSlider 内部定位一致：两端各留半个把手宽）。
func _grabber_x() -> float:
	var ratio := 0.5
	if slider != null:
		var span := slider.max_value - slider.min_value
		ratio = (slider.value - slider.min_value) / maxf(span, 0.0001)
	return lerpf(GRABBER_S * 0.5, size.x - GRABBER_S * 0.5, ratio)


## 值变化后请求重绘。
func _sync() -> void:
	queue_redraw()


func _draw() -> void:
	if track_tex == null or slider == null:
		return
	var w := size.x
	var cy := size.y * 0.5
	var y0 := cy - BAR_H * 0.5
	var src_h := float(track_tex.get_height())
	var src_w := float(track_tex.get_width())

	# 轨道：左端帽 + 中心切片横向延展 + 右端帽。
	# 中心取源图固定窄切片拉伸，保证任意滑条宽度下纹理完全一致（无拉伸差异）。
	draw_texture_rect_region(track_tex, Rect2(0, y0, CAP_DST, BAR_H),
		Rect2(0, 0, CAP_SRC, src_h))
	draw_texture_rect_region(track_tex, Rect2(CAP_DST, y0, w - CAP_DST * 2.0, BAR_H),
		Rect2(SLICE_SRC_X, 0, SLICE_SRC_W, src_h))
	draw_texture_rect_region(track_tex, Rect2(w - CAP_DST, y0, CAP_DST, BAR_H),
		Rect2(src_w - CAP_SRC, 0, CAP_SRC, src_h))

	# 填充：只落在轨道中央的凹槽线里（细能量线，不盖满整条木槽）。
	# 源图取纵向实心中段——上下软 alpha 边在缩放后会外溢出凹槽。
	# 横向从端帽之后起笔（CAP_DST）：端帽是实心圆头、没有刻槽，
	# 填充画进端帽会超出凹槽（用户指出的越界）。
	# 其上叠加沉浸光感：光从把手端渗出、沿凹槽渐隐。
	var gx := _grabber_x()
	if fill_tex != null and gx > CAP_DST + 0.5:
		var fill_w := gx - CAP_DST
		draw_texture_rect_region(fill_tex, Rect2(CAP_DST, y0 + GROOVE_Y, fill_w, GROOVE_H),
			Rect2(24, FILL_SRC_Y, fill_tex.get_width() - 48, FILL_SRC_H))
		if particle_theme != "":
			_draw_immersive_glow(fill_w, y0)

	# 把手：悬浮/聚焦时用提亮态
	var hl: bool = _hover or slider.has_focus()
	var tex := grabber_hl_tex if hl and grabber_hl_tex != null else grabber_tex
	if tex != null:
		draw_texture_rect_region(tex,
			Rect2(gx - GRABBER_S * 0.5, cy - GRABBER_S * 0.5, GRABBER_S, GRABBER_S),
			Rect2(0, 0, tex.get_width(), tex.get_height()))


## 沉浸光感（参考华为旗舰机的光效语言）：光从把手端柔和渗出、
## 沿凹槽向左渐隐（顶点色渐变），凹槽周围一圈微弱氛围光极缓慢呼吸。
## 没有流动、追逐、扫描——光是"泡"在元素里的。
## fill_w 为凹槽段（端帽之后 → 把手）的宽度，内部坐标加 CAP_DST 偏移。
func _draw_immersive_glow(fill_w: float, y0: float) -> void:
	var gy := y0 + GROOVE_Y
	var x0 := CAP_DST
	var breathe := 0.5 + 0.5 * sin(_t * TAU / BREATHE_PERIOD)

	# 1) 填充线内的渗出光：把手端最亮（近白）→ 左端渐隐到氛围色
	var c_head := _head_color.lerp(Color.WHITE, 0.25)
	c_head.a = _glow_strength * 0.42
	var c_tail := _tail_color
	c_tail.a = 0.0
	var pts := PackedVector2Array([
		Vector2(x0, gy), Vector2(x0 + fill_w, gy),
		Vector2(x0 + fill_w, gy + GROOVE_H), Vector2(x0, gy + GROOVE_H)])
	var cols := PackedColorArray([c_tail, c_head, c_head, c_tail])
	draw_polygon(pts, cols)

	# 2) 凹槽周围的氛围光晕：光渗进上下木面，随呼吸微微起伏
	var halo := _tail_color
	halo.a = _glow_strength * (0.05 + 0.04 * breathe)
	draw_rect(Rect2(x0, gy - 2.6, fill_w, GROOVE_H + 5.2), halo)

	# 3) 把手内芯的一团柔光：光的源头，微微呼吸
	var gx := _grabber_x()
	var core := _head_color
	core.a = _glow_strength * (0.14 + 0.10 * breathe)
	draw_circle(Vector2(gx, gy + GROOVE_H * 0.5), 7.0, core)

