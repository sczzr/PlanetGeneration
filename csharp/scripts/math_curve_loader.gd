class_name MathCurveLoader
extends Control

## 数学曲线加载动画：一组粒子沿参数曲线循环运动，以半径与透明度衰减形成拖尾。
## 参照 A Gallery of Mathematical Loading Animations（paidax01/math-curve-loaders）
## 的实现思路：progress ∈ [0,1) 映射为曲线参数 t；第 i 个粒子在进度上回退
## tail_offset * trail_span 形成尾迹，fade = (1 - tail_offset)^0.56 决定半径与透明度。
## 配合"呼吸"脉冲（幅度缓慢起伏）与可选的慢速整体旋转构成常驻动画。
## 所有曲线都是纯函数点集，绘制走 _draw() 自绘，无节点与粒子系统开销；
## 隐藏时自动停 _process（NOTIFICATION_VISIBILITY_CHANGED）。

## 可选曲线族。曲线输出统一归一化到单位圆内，由控件短边半径缩放绘制。
enum CurveType {
	ROSE,          ## 玫瑰线 r = a·cos(kθ)：五瓣花形，幅度随呼吸脉动
	LISSAJOUS,     ## 利萨茹 3:4：两轴异频正弦叠加的缠绕闭合图形
	LEMNISCATE,    ## 伯努利双纽线：分母 1+sin²t 向中心收束的 ∞ 形
	HYPOTROCHOID,  ## 内次摆线（万花尺）：小圆在大圆内滚动描出的花环
	SPIRAL,        ## 搜索螺旋：半径随 (1-cos t) 展开收拢的旋转扫描轨迹
	BUTTERFLY,     ## 蝴蝶曲线：极径含指数与四倍频项，单圈 6 周描绘完整蝶形
}

@export var curve_type: CurveType = CurveType.ROSE

@export_group("视觉")
## 拖尾主色（尾端颜色）；粒子头部向 head_color 过渡。
@export var trail_color := Color(0.28, 0.50, 0.36, 1.0)
## 粒子头部颜色（近白高光，同 themed_slider_skin 流光的波头做法）。
@export var head_color := Color(1.0, 0.96, 0.86, 1.0)
## 是否绘制半透明轨迹底图（帮助读出整条曲线形状）。
@export var show_path := true

@export_group("进度")
## 加载进度（0~1）。≥0 时进入进度模式：花环按进度逐瓣填充（每瓣占 1/5，瓣内
## 平滑逼近），≥1 时整环填满；<0 为无进度的纯循环动画。完成值直接贴满，
## 避免平滑逼近错过「填满」再淡出的时刻。
@export_range(-1.0, 1.0, 0.01) var progress := -1.0:
	set(value):
		progress = clampf(value, -1.0, 1.0)
		if progress >= 1.0:
			_display_progress = 1.0
## 显示进度逼近目标的速度（每秒进度）：把上游里程碑式的跳变抹成连续填充。
const PROGRESS_SMOOTH_SPEED := 0.45

@export_group("运动")
## 粒子数量，即拖尾分辨率。
@export_range(8, 140, 1) var particle_count := 72
## 尾迹跨度，占整圈进度的比例。
@export_range(0.05, 0.9, 0.01) var trail_span := 0.32
## 沿曲线运动一圈的时长（秒）。
@export_range(0.5, 30.0, 0.1) var loop_duration := 5.6
## 呼吸脉冲周期（秒）；驱动曲线幅度缓慢起伏。
@export_range(0.5, 30.0, 0.1) var pulse_duration := 5.2
## 整体慢速旋转周期（秒）；0 表示不旋转（顺时针，同参考项目取负角）。
@export_range(0.0, 120.0, 0.1) var rotation_duration := 0.0

## 动画累计时间（秒）。
var _t := 0.0
## 随机初始相位，多实例并排时互不同步。
var _phase := randf()
## 显示用进度：向目标 progress 恒速逼近；-1 与无进度模式同义。
var _display_progress := -1.0


func _notification(what: int) -> void:
	# 隐藏时停止动画推进，避免不可见时持续 queue_redraw。
	if what == NOTIFICATION_VISIBILITY_CHANGED:
		set_process(is_visible_in_tree())


func _process(delta: float) -> void:
	_t += delta
	# 显示进度跟随：目标是缓动映射后的观感进度（非真实线性值）。
	# 无进度时重置回动画模式；目标回退（重新开始）时直接贴位。
	if progress < 0.0:
		_display_progress = -1.0
	else:
		var target := _ease_display(progress)
		if _display_progress < 0.0 or _display_progress > target:
			_display_progress = target
		elif _display_progress < target:
			_display_progress = move_toward(_display_progress, target, delta * PROGRESS_SMOOTH_SPEED)
	queue_redraw()


## 真实进度 -> 显示进度的观感映射（视觉欺骗，分段线性，端点连续）：
## 前 1/4 快速掠过（斜率 1.0，开局即有动静）、中间 1/4 慢速爬升
## （斜率 0.3，这段真实重活期间的等待观感都压在这里）、最后 1/2 冲刺收尾
## （斜率 1.35，全场最快，临近完成的爽感）。E(0)=0、E(1)=1，全程单调。
func _ease_display(p: float) -> float:
	const SLOW_START := 0.25
	const SLOW_END := 0.5
	const SLOW_RATE := 0.3
	if p <= SLOW_START:
		return p
	if p <= SLOW_END:
		return SLOW_START + (p - SLOW_START) * SLOW_RATE
	var slow_display := SLOW_START + (SLOW_END - SLOW_START) * SLOW_RATE
	return slow_display + (p - SLOW_END) * (1.0 - slow_display) / (1.0 - SLOW_END)


## 呼吸系数，取值约 [0.52, 1.0]（同参考项目的脉动计算式）。
func _pulse_value() -> float:
	var angle := fposmod(_t / pulse_duration, 1.0) * TAU
	return 0.52 + ((sin(angle + 0.55) + 1.0) / 2.0) * 0.48


## 曲线取点：progress ∈ [0,1) 一圈，pulse 为呼吸系数；返回单位圆内坐标。
func _point(progress: float, pulse: float) -> Vector2:
	var t := progress * TAU
	match curve_type:
		CurveType.ROSE:
			var r := (0.76 + 0.24 * pulse) * cos(5.0 * t)
			return Vector2(cos(t), sin(t)) * r
		CurveType.LISSAJOUS:
			return Vector2(sin(3.0 * t + PI * 0.5), sin(4.0 * t)) * 0.92
		CurveType.LEMNISCATE:
			var d := 1.0 + pow(sin(t), 2.0)
			return Vector2(cos(t) / d, sin(t) * cos(t) / d) * 0.92
		CurveType.HYPOTROCHOID:
			# R=5、r=1 的滚动圆，画笔距离 d 随呼吸微调（瓣形饱满度起伏）。
			var d := 2.4 * (0.8 + 0.35 * pulse)
			var v := Vector2(
				4.0 * cos(t) + d * cos(4.0 * t),
				4.0 * sin(t) - d * sin(4.0 * t))
			return v / 6.9
		CurveType.SPIRAL:
			# 整数圈（3 圈）保证 progress 回绕时首尾闭合。
			var r := 0.14 + (1.0 - cos(t)) * (0.30 + 0.09 * pulse)
			return Vector2(cos(3.0 * t), sin(3.0 * t)) * r
		CurveType.BUTTERFLY:
			# 经典蝴蝶曲线（Temple Fay 型），极径最大约 4，除以 4.05 归一化。
			var bt := progress * TAU * 6.0
			var s := exp(cos(bt)) - 2.0 * cos(4.0 * bt) - pow(sin(bt / 12.0), 5.0)
			return Vector2(sin(bt), -cos(bt)) * (s / 4.05)
	return Vector2.ZERO


func _draw() -> void:
	if particle_count < 2:
		return
	var center := size * 0.5
	# 0.92 的安全系数保证呼吸峰值时曲线不出控件边界。
	var radius := minf(size.x, size.y) * 0.5 * 0.92
	var pulse := _pulse_value()
	var rot := 0.0
	if rotation_duration > 0.0:
		rot = -(_t / rotation_duration) * TAU
	# 以中心为原点的局部坐标系绘制，整体旋转只改 draw 变换。
	draw_set_transform(center, rot, Vector2.ONE)

	if show_path:
		_draw_path(radius, pulse)

	# 进度模式：按显示进度逐瓣填充叶瓣（画在轨迹底图之上、粒子之下）。
	if _display_progress >= 0.0:
		_draw_progress_fill(radius, pulse)

	# 头部粒子最大半径约占控件短边 3.4%（参考项目 3.6/100 视野比例）。
	var max_r := minf(size.x, size.y) * 0.034
	var anim_progress := fposmod(_t / loop_duration + _phase, 1.0)
	for i in particle_count:
		var tail := float(i) / float(particle_count - 1)
		var p := _point(fposmod(anim_progress - tail * trail_span, 1.0), pulse) * radius
		var fade := pow(1.0 - tail, 0.56)
		var col := trail_color.lerp(head_color, fade)
		col.a = 0.04 + 0.96 * fade
		draw_circle(p, max_r * (0.25 + 0.75 * fade), col)

	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


## 半透明轨迹底图：采样 200 段折线（曲线均为整数圈闭合，首尾相接）。
func _draw_path(radius: float, pulse: float) -> void:
	const SEGMENTS := 200
	var pts := PackedVector2Array()
	pts.resize(SEGMENTS + 1)
	for i in SEGMENTS + 1:
		pts[i] = _point(float(i) / float(SEGMENTS), pulse) * radius
	var col := trail_color
	col.a = 0.1
	draw_polyline(pts, col, 1.0)


## 进度填充：花环五瓣各占进度 1/5，整瓣用完整瓣弧 + 中心点闭合成花瓣扇形，
## 当前瓣按剩余比例只扫过部分弧。内次摆线（R=5、r=1）的瓣尖恰好落在
## t = 2πk/5（|P|² 在 cos5t=1 处最大），因此 1/5 参数区间端点贴合瓣尖，
## 扇形能撑满整个瓣形；填充色随呼吸脉动与整体动画保持一致。
func _draw_progress_fill(radius: float, pulse: float) -> void:
	const LEAVES := 5
	const SEGMENTS := 26
	var filled := _display_progress * LEAVES
	var whole := int(filled)
	var frac := filled - float(whole)
	var fill := trail_color
	fill.a = 0.32
	var edge := trail_color
	edge.a = 0.6
	for k in LEAVES:
		# 整瓣区间 [k/5, (k+1)/5]；当前瓣只画 frac 比例的弧段，之后的瓣未开始。
		var span := 1.0
		if k >= whole:
			if k > whole or frac <= 0.0:
				break
			span = frac
		var arc := PackedVector2Array()
		arc.resize(SEGMENTS + 1)
		for i in SEGMENTS + 1:
			arc[i] = _point((float(k) + span * float(i) / float(SEGMENTS)) / LEAVES, pulse) * radius
		# 三角形扇填充：中心 + 相邻弧点两两成三角。瓣弧是带尖点的凹多边形，
		# 直接 draw_colored_polygon 的三角化会在尖点/细扇处退化失败，
		# 单三角形无需三角化，对任意弧形都稳定。
		for i in SEGMENTS:
			draw_colored_polygon(
				PackedVector2Array([Vector2.ZERO, arc[i], arc[i + 1]]), fill)
		# 瓣缘描边（只描弧段，不含闭合半径线），让已填充瓣的轮廓清晰可读。
		draw_polyline(arc, edge, 1.2)
