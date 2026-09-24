extends Button
## Menu FX 强调按钮：可配置强调效果的基础菜单按钮，可脱离菜单列单独挂在任意按钮上。
## emphasis 决定启用哪些强调效果，可多选叠加：
##   焦点放大——获得焦点时以中心为轴平滑放大到目标尺寸并停住保持；
##   失焦后从最大尺寸缩回正常状态，形成"聚焦放大"效果；
##   下划线——文本下方从中心向两端展开的线条，失焦收回；
##   文字高亮——文字颜色在普通色与强调色之间平滑过渡；
##   指示箭头——左缘滑入一个小三角，失焦滑出；
##   发光光晕——文字外描边光晕淡入，柔化焦点态。
## 悬停默认通过接管焦点触发同样的效果，因此鼠标移开不复位，焦点真正离开才复位；
## 独立页面（无菜单列管理焦点）可开启 hover_release_focus，改为鼠标离开即释放焦点复原。
## 所有强调效果在焦点保持期间持续存在，只有失去焦点才复位回普通样式。
## 各效果参数默认值与主菜单现行外观一致；可选 style 资源整组覆盖本节点参数。

## 强调效果位标志：与 @export_flags 的勾选位一一对应（从低位到高位）。
enum Effect { POP = 1, UNDERLINE = 2, HIGHLIGHT = 4, MARKER = 8, GLOW = 16 }

const MenuFxStyle := preload("res://addons/menu_fx/scripts/menu_fx_style.gd")

## 启用的强调效果组合（可多选）。默认 = 放大 + 下划线，即主菜单现行效果。
@export_flags("焦点放大", "下划线", "文字高亮", "指示箭头", "发光")
var emphasis: int = Effect.POP | Effect.UNDERLINE

## 可选参数覆盖资源：就绪时把数值一次性拷入下方同名参数，效果代码只读本节点参数。
@export var style: MenuFxStyle

## 鼠标离开时若仍持有焦点则立即释放，让"悬浮放大、移开复原"成立。
## 供没有菜单列等焦点管理宿主的独立页面（如创建世界向导）使用；
## 默认关闭，保持主菜单"焦点选中项持续保持，失焦才复位"的既有语义。
@export var hover_release_focus := false

@export_group("焦点放大")
## 放大幅度：焦点时缩放目标 = 1.0 + 强度 × 宽度衰减比。
@export var pop_strength := 0.08
## 缩放补间时长（秒）。
@export var pop_duration := 0.3
## 失焦复位时长（秒）。
@export var settle_duration := 0.15
## 强度按此参考宽度起算：更宽的按钮按比例衰减，下限保留一半强度，防止两端位移过大。
@export var reference_width := 220.0

@export_group("下划线")
## 线条颜色：与主菜单主题的焦点强调色保持一致。
@export var underline_color := Color(0.7, 0.85, 1.0, 0.9)
## 下划线粗细（像素）；偶数数值配合整像素对齐可获得锐利边缘。
@export var underline_thickness := 4.0
## 获得焦点时从中心向两端展开的时长（秒）。
@export var underline_grow_duration := 0.2
## 失焦收回的时长（秒）。
@export var underline_shrink_duration := 0.15

@export_group("文字高亮")
## 默认为主题焦点色（白）：开启后等于把主题的瞬间变色改为平滑过渡。
@export var highlight_color := Color(1.0, 1.0, 1.0, 1.0)
## 文字颜色过渡时长（秒）。
@export var highlight_duration := 0.15

@export_group("指示箭头")
## 左缘小三角的颜色。
@export var marker_color := Color(0.7, 0.85, 1.0, 0.9)
## 三角形边长（像素）。
@export var marker_size := 8.0
## 三角形尖端与按钮左缘的间距（像素）；三角形整体位于按钮外侧，指向按钮。
@export var marker_margin := 10.0
## 滑入/滑出时长（秒）。
@export var marker_duration := 0.15

@export_group("发光光晕")
## 光晕颜色：绘制为文字外描边，透明度随进度淡入。
@export var glow_color := Color(0.7, 0.85, 1.0, 0.55)
## 光晕最大描边宽度（像素）。
@export var glow_size := 6.0
## 光晕淡入/淡出时长（秒）。
@export var glow_duration := 0.2

@export_group("声音动效")
## 焦点进入时播放的音效：悬停与键盘导航共用（悬停即接管焦点）。留空不播。
@export var focus_sound: AudioStream
## 按下时播放的音效。留空不播。
@export var press_sound: AudioStream
## 音效音量偏移（dB），0 为素材原始音量；总线固定走 SFX，受设置页音效音量控制。
@export var sound_volume_db := 0.0

var _fx_tween: Tween
var _underline_tween: Tween
var _highlight_tween: Tween
var _marker_tween: Tween
var _glow_tween: Tween
## 懒创建的音效播放子节点：焦点音与按下音共用（重触发即打断上一声，不叠音）。
var _sound_player: AudioStreamPlayer
## 焦点音武装标记：节点可见满一帧后才允许播焦点音。场景加载时 MenuList 的
## 初始 grab_focus、覆盖层打开瞬间的聚焦都发生在武装前，保持安静；
## 之后用户真实的悬停/键盘导航才发声。
var _focus_sound_armed := false
## 下划线展开进度 0~1，1 为完整宽度；绘制时按进度从按钮中心向两侧取半宽。
var _underline_progress := 0.0
## 指示箭头滑入进度 0~1；绘制时按进度从按钮左缘外滑入并淡入。
var _marker_progress := 0.0
## 发光光晕进度 0~1；驱动文字描边颜色与宽度两组主题覆盖。
var _glow_progress := 0.0
## 文字颜色过渡的当前值：快速来回切换焦点时从当前颜色接续，避免跳变。
## 就绪时初始化为主题普通色，保证首次聚焦从正常文字色出发。
var _highlight_current := Color.TRANSPARENT


func _ready() -> void:
	_apply_style()
	_highlight_current = get_theme_color("font_color")
	_center_pivot()
	resized.connect(_center_pivot)
	# 悬停即接管焦点：所有强调效果统一跟随焦点状态，鼠标与键盘共用同一"选中项"。
	# 默认不监听 mouse_exited：焦点态需要持续保持，鼠标移开不复位，失焦才复位；
	# 独立页面开启 hover_release_focus 后额外监听，鼠标离开即释放悬停接管的焦点。
	mouse_entered.connect(_focus_on_hover)
	if hover_release_focus:
		mouse_exited.connect(_release_focus_on_mouse_exit)
	focus_entered.connect(_on_focus_entered)
	focus_exited.connect(_on_focus_exited)
	pressed.connect(_on_pressed)
	# 武装焦点音：排程到帧末执行，本帧内的程序化聚焦（初始聚焦、面板打开聚焦）不发声。
	_arm_focus_sound.call_deferred()
	queue_redraw()


## 画布可见性变化时重新排程武装：覆盖层里的按钮在 open() 设 visible 后同帧 grab_focus，
## 该次聚焦仍处于未武装状态不发声；此后再聚焦（真实导航/焦点归还）才播焦点音。
func _notification(what: int) -> void:
	if what == NOTIFICATION_VISIBILITY_CHANGED and is_visible_in_tree():
		_arm_focus_sound.call_deferred()


## 武装焦点音。仅当此刻在树内可见才置位：覆盖层里的按钮实例化当帧虽排程武装，
## 但回调执行时仍不可见，保持未武装，open() 同帧的聚焦因此不发声。重复排程无害。
func _arm_focus_sound() -> void:
	if is_visible_in_tree():
		_focus_sound_armed = true


## 把 style 资源的数值一次性拷入本节点参数。拷贝而非每帧读取：
## 效果代码保持直接读参数的简单写法，且共享资源不会被多个按钮意外写入。
func _apply_style() -> void:
	if style == null:
		return
	pop_strength = style.pop_strength
	pop_duration = style.pop_duration
	settle_duration = style.settle_duration
	reference_width = style.reference_width
	underline_color = style.underline_color
	underline_thickness = style.underline_thickness
	underline_grow_duration = style.underline_grow_duration
	underline_shrink_duration = style.underline_shrink_duration
	highlight_color = style.highlight_color
	highlight_duration = style.highlight_duration
	marker_color = style.marker_color
	marker_size = style.marker_size
	marker_margin = style.marker_margin
	marker_duration = style.marker_duration
	glow_color = style.glow_color
	glow_size = style.glow_size
	glow_duration = style.glow_duration
	focus_sound = style.focus_sound
	press_sound = style.press_sound
	sound_volume_db = style.sound_volume_db


func _has(effect: Effect) -> bool:
	return (emphasis & effect) != 0


func _center_pivot() -> void:
	pivot_offset = size / 2.0


## 鼠标悬停时把焦点移到本项；禁用期间（如设置面板打开时）不接管，避免强调效果停在被禁用的选项上。
func _focus_on_hover() -> void:
	if not disabled:
		grab_focus()


## 鼠标离开时释放悬停期间接管的焦点：独立页面上往往没有其他可聚焦控件接管焦点，
## 不主动释放会让强调效果（放大等）一直停在按钮上。仅 hover_release_focus 开启时连接。
func _release_focus_on_mouse_exit() -> void:
	if has_focus():
		release_focus()


func _on_focus_entered() -> void:
	if disabled:
		return
	# 焦点音只对武装后的聚焦发声：场景加载的初始聚焦与覆盖层打开瞬间的聚焦不响。
	if _focus_sound_armed:
		_play_sound(focus_sound)
	if _has(Effect.POP):
		_play_pop()
	if _has(Effect.UNDERLINE):
		_grow_underline()
	if _has(Effect.HIGHLIGHT):
		_grow_highlight()
	if _has(Effect.MARKER):
		_slide_in_marker()
	if _has(Effect.GLOW):
		_grow_glow()


## 复位到普通样式。pop 之外的复位不短路 disabled：按钮可能在聚焦放大期间被禁用
## （如打开设置）后被设置面板夺走焦点，此时也必须复位，否则会卡在强调状态。
func _on_focus_exited() -> void:
	if _has(Effect.POP):
		_play_settle()
	if _has(Effect.UNDERLINE):
		_shrink_underline()
	if _has(Effect.HIGHLIGHT):
		_shrink_highlight()
	if _has(Effect.MARKER):
		_slide_out_marker()
	if _has(Effect.GLOW):
		_shrink_glow()


## 按钮越宽，强度越低（0.5~1.0），与参考宽度的比值决定衰减程度。
func _strength_ratio() -> float:
	return clampf(reference_width / size.x, 0.5, 1.0)


## 放大到焦点尺寸并停住：单条 scale 补间平滑放大到目标，无旋转与挤压，
## 到达目标后保持；快速反复切换焦点时从当前值接续，不跳变。
func _play_pop() -> void:
	_kill_fx_tween()
	var scale_target := 1.0 + pop_strength * _strength_ratio()
	_fx_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_fx_tween.tween_property(self, "scale", Vector2.ONE * scale_target, pop_duration)


## 失焦后从最大尺寸缩回正常状态（1.0）。刻意不短路 disabled：按钮可能在聚焦
## 放大期间被禁用，复位路径必须照常执行才能回到普通样式。
func _play_settle() -> void:
	_kill_fx_tween()
	_fx_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_fx_tween.tween_property(self, "scale", Vector2.ONE, settle_duration)


func _kill_fx_tween() -> void:
	if _fx_tween != null and _fx_tween.is_valid():
		_fx_tween.kill()


# ---------------------------------------------------------------- 声音动效

## 按下时播放按下音。disabled 按钮不会发出 pressed，无需再判。
func _on_pressed() -> void:
	_play_sound(press_sound)


## 播放一段音效：播放器首次用到时才创建，挂在按钮下走 SFX 总线；
## 同一播放器重触发即打断上一声，快速来回导航不会叠成噪音。
func _play_sound(stream: AudioStream) -> void:
	if stream == null:
		return
	if _sound_player == null:
		_sound_player = AudioStreamPlayer.new()
		# UI 音效统一走 SFX 总线，受设置页"音效音量"控制；总线名与 default_bus_layout 一致。
		_sound_player.bus = &"SFX"
		add_child(_sound_player)
	_sound_player.volume_db = sound_volume_db
	_sound_player.stream = stream
	_sound_player.play()


# ---------------------------------------------------------------- 下划线

## 焦点下划线：画在按钮内容区底边（贴齐下缘），从中心向两端按进度展开。
## y 取整数值且粗细为偶数，线条上下边缘精确落在像素边界上，只有生长端需要抗锯齿。
func _draw_underline() -> void:
	var half_width := size.x * 0.5 * _underline_progress
	var center_x := size.x * 0.5
	var y := size.y - underline_thickness * 0.5
	draw_line(
		Vector2(center_x - half_width, y),
		Vector2(center_x + half_width, y),
		underline_color,
		underline_thickness,
		true
	)


func _grow_underline() -> void:
	_tween_underline(1.0, underline_grow_duration)


func _shrink_underline() -> void:
	_tween_underline(0.0, underline_shrink_duration)


## 焦点快速来回切换时从当前进度接续，避免跳变。
func _tween_underline(target: float, duration: float) -> void:
	if _underline_tween != null and _underline_tween.is_valid():
		_underline_tween.kill()
	_underline_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_underline_tween.tween_method(_set_underline_progress, _underline_progress, target, duration)


func _set_underline_progress(value: float) -> void:
	_underline_progress = value
	queue_redraw()


# ---------------------------------------------------------------- 指示箭头

## 左缘小三角（指向右侧），整体位于按钮左缘之外、尖端距按钮 marker_margin，
## 随进度从更左侧滑入并淡入。放在按钮外是因为按钮常随文字收缩宽度（SHRINK_CENTER），
## 左缘内侧就是文字起点，画在内部会与首字重叠。
func _draw_marker() -> void:
	var half := marker_size * 0.5
	var center_y := size.y * 0.5
	# 进度 0 时三角再往左退一个边长（完全藏起），1 时滑到目标位。
	var slide := marker_size * (1.0 - _marker_progress)
	var base_x := -marker_margin - marker_size - slide
	var color := marker_color
	color.a *= _marker_progress
	draw_colored_polygon(
		PackedVector2Array([
			Vector2(base_x, center_y - half),
			Vector2(base_x, center_y + half),
			Vector2(base_x + marker_size, center_y),
		]),
		color
	)


func _slide_in_marker() -> void:
	_tween_marker(1.0, marker_duration)


func _slide_out_marker() -> void:
	_tween_marker(0.0, marker_duration)


func _tween_marker(target: float, duration: float) -> void:
	if _marker_tween != null and _marker_tween.is_valid():
		_marker_tween.kill()
	_marker_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_marker_tween.tween_method(_set_marker_progress, _marker_progress, target, duration)


func _set_marker_progress(value: float) -> void:
	_marker_progress = value
	queue_redraw()


# ---------------------------------------------------------------- 文字高亮

## 焦点时把主题焦点色（font_focus_color）从普通文字色过渡到强调色；
## 每帧写主题覆盖，失焦移除覆盖、改写普通色覆盖做回落，交还主题控制。
func _grow_highlight() -> void:
	if _highlight_tween != null and _highlight_tween.is_valid():
		_highlight_tween.kill()
	# 进入焦点后文字由 font_focus_color 驱动，清掉上次失焦回落留下的普通色覆盖。
	remove_theme_color_override("font_color")
	var base := get_theme_color("font_color")
	_highlight_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_highlight_tween.tween_method(_set_highlight_color, _highlight_current, highlight_color, highlight_duration)


## 失焦后文字立刻改由普通色（font_color）驱动，焦点色覆盖不再可见，
## 因此回落动画改写普通色覆盖：从当前颜色平滑回到主题普通色，结束移除覆盖。
func _shrink_highlight() -> void:
	if _highlight_tween != null and _highlight_tween.is_valid():
		_highlight_tween.kill()
	remove_theme_color_override("font_focus_color")
	var base := get_theme_color("font_color")
	_highlight_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_highlight_tween.tween_method(_set_highlight_color, _highlight_current, base, highlight_duration)
	_highlight_tween.finished.connect(remove_theme_color_override.bind("font_color"))


func _set_highlight_color(value: Color) -> void:
	_highlight_current = value
	# 焦点态写焦点色覆盖；失焦回落期间写普通色覆盖，由当前是否有焦点决定写哪一项。
	if has_focus():
		add_theme_color_override("font_focus_color", value)
	else:
		add_theme_color_override("font_color", value)


# ---------------------------------------------------------------- 发光光晕

## 光晕实现为文字外描边：颜色透明度与描边宽度随进度同步增长，
## 由引擎绘制在文字填充之下，形成柔和光环，无需着色器。
func _grow_glow() -> void:
	_tween_glow(1.0, glow_duration)


func _shrink_glow() -> void:
	_tween_glow(0.0, glow_duration)


func _tween_glow(target: float, duration: float) -> void:
	if _glow_tween != null and _glow_tween.is_valid():
		_glow_tween.kill()
	_glow_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_glow_tween.tween_method(_set_glow_progress, _glow_progress, target, duration)
	if target <= 0.0:
		# 完全收回后移除主题覆盖，交还主题控制，避免残留 0 值覆盖。
		_glow_tween.finished.connect(_clear_glow_overrides)


func _set_glow_progress(value: float) -> void:
	_glow_progress = value
	var color := glow_color
	color.a *= value
	add_theme_color_override("font_outline_color", color)
	add_theme_constant_override("outline_size", roundi(glow_size * value))


func _clear_glow_overrides() -> void:
	remove_theme_color_override("font_outline_color")
	remove_theme_constant_override("outline_size")


func _draw() -> void:
	if _underline_progress > 0.0:
		_draw_underline()
	if _marker_progress > 0.0:
		_draw_marker()
