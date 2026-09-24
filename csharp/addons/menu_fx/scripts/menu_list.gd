extends VBoxContainer
## Menu FX 菜单列组件：按 items 配置在运行时生成一列强调按钮，整列复用。
## 每个菜单项可在 Inspector 里单独勾选强调效果；数值参数由一份 MenuFxStyle
## 统一提供（不配置时用按钮默认值）。组件自包含，单独一个节点即可工作。
##
## 内建交错入场动画，参数与项目 ContainerAppear 组件对齐，默认值即主菜单
## 按钮列现行外观；入场只针对本组件生成的按钮。
##
## 焦点约定：就绪后自动聚焦第一个可用项；覆盖层（如设置面板）打开期间
## 由宿主调用 set_input_enabled(false) 禁用整列，关闭后 focus_item() 归还焦点。

## 菜单项被点击（按下）时发出，参数为菜单项配置的 id。
signal item_activated(id: StringName)

const MenuFxButton := preload("res://addons/menu_fx/scripts/menu_fx_button.gd")
const MenuListItem := preload("res://addons/menu_fx/scripts/menu_list_item.gd")
const MenuFxStyle := preload("res://addons/menu_fx/scripts/menu_fx_style.gd")

## 入场动画类型：与 ContainerAppear.AnimType 对齐，便于两种组件间迁移。
enum AnimType { SCALE, SLIDE_IN_LEFT, SLIDE_IN_RIGHT }

@export_group("菜单项")
## 菜单项列表：每项生成一个按钮，id 唯一，按数组顺序纵向排列。
@export var items: Array[MenuListItem] = []
## 整列按钮共用的强调效果参数资源；留空则使用下方各参数组直出的数值（默认值=主菜单现行外观）。
@export var style: MenuFxStyle

@export_group("焦点放大")
## 放大幅度：聚焦时按钮缩放目标 = 1.0 + 强度 × 宽度衰减比（仅勾选"焦点放大"效果的项生效）。
@export var pop_strength := 0.08
## 放大补间时长（秒）。
@export var pop_duration := 0.3
## 失焦复位时长（秒）。
@export var settle_duration := 0.15
## 强度按此参考宽度起算：更宽的按钮按比例衰减，下限保留一半强度，防止长按钮放大过度。
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
## 焦点时文字过渡到的强调色；普通色取主题 font_color（默认白=主题焦点色）。
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
## 焦点进入时播放的音效：悬停与键盘导航共用（悬停即接管焦点），整列按钮共用一份。留空不播。
@export var focus_sound: AudioStream
## 按下时播放的音效，整列按钮共用一份。留空不播。
@export var press_sound: AudioStream
## 音效音量偏移（dB），0 为素材原始音量；总线固定走 SFX，受设置页音效音量控制。
@export var sound_volume_db := 0.0

@export_group("按钮外观")
## 每个按钮的最小高度（像素）。
@export var button_min_height := 46.0
## 按钮列的纵向间距（像素）。
@export var button_separation := 10
## 按钮在列内水平居中（SHRINK_CENTER）；关闭则横向填满整列。
@export var center_buttons := true
## 图标最大宽度（像素），0 表示使用纹理原始尺寸。
@export var icon_max_width := 28
## 图标与文字之间的水平间距（像素）。
@export var icon_separation := 12

@export_group("交错入场")
## 入场动画类型。注意 SCALE 会与"焦点放大"同时写 scale 属性，菜单列建议用滑入类。
@export_enum("缩放", "左滑入", "右滑入") var anim_type: int = AnimType.SLIDE_IN_LEFT
## 单个元素的入场时长（秒）。
@export var duration := 0.3
## 首个元素的入场延迟（秒），留给背景与标题先动。
@export var delay_appear := 0.2
## 相邻元素之间的重叠延迟（秒）。
@export var delay_between_elements := 0.05
## 滑入类动画的起始偏移量（像素）。
@export var slide_offset := 48.0

var _buttons: Array[MenuFxButton] = []
var _buttons_by_id := {}
var _appear_tween: Tween


func _ready() -> void:
	add_theme_constant_override("separation", button_separation)
	_build_buttons()
	# 入场前先置全透明：按钮不能以原始状态闪现。
	_set_buttons_alpha(0.0)
	_appear_deferred()


## 按 items 生成按钮。id 为空或重复的项跳过并告警，不让坏配置静默失效。
func _build_buttons() -> void:
	var seen := {}
	for i in items.size():
		var item := items[i]
		if item == null:
			push_warning("MenuList：items[%d] 为空，已跳过" % i)
			continue
		if item.id == &"":
			push_warning("MenuList：items[%d]（%s）缺少 id，已跳过" % [i, item.text_key])
			continue
		if seen.has(item.id):
			push_warning("MenuList：菜单项 id 重复（%s），后者已跳过" % item.id)
			continue
		seen[item.id] = true
		var button: MenuFxButton = MenuFxButton.new()
		# 节点名沿用菜单项 id，便于在远端检视器里对照查找。
		button.name = String(item.id)
		button.text = item.text_key
		if item.icon != null:
			button.icon = item.icon
			button.expand_icon = true
			if icon_max_width > 0:
				button.add_theme_constant_override("icon_max_width", icon_max_width)
			button.add_theme_constant_override("h_separation", icon_separation)
		button.emphasis = item.emphasis
		button.disabled = item.disabled
		# style 资源优先；未挂时把面板直出参数打包成临时 style，复用同一条应用路径。
		button.style = style if style != null else _build_style_from_params()
		button.custom_minimum_size = Vector2(0, button_min_height)
		button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER if center_buttons else Control.SIZE_FILL
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		button.pressed.connect(_on_button_pressed.bind(item.id))
		add_child(button)
		_buttons.append(button)
		_buttons_by_id[item.id] = button
	_focus_first_enabled()


## 就绪后把焦点交给第一个可用项（对应主菜单初始聚焦"开始游戏"的行为）。
func _focus_first_enabled() -> void:
	for button in _buttons:
		if not button.disabled:
			button.grab_focus()
			return


func _on_button_pressed(id: StringName) -> void:
	item_activated.emit(id)


## 把面板直出的强调参数打包成一份 style：生成按钮路径与挂 style 资源时完全相同，
## 参数应用只有 style→按钮一条逻辑；数值来自本节点的同名导出参数。
func _build_style_from_params() -> MenuFxStyle:
	var packed := MenuFxStyle.new()
	packed.pop_strength = pop_strength
	packed.pop_duration = pop_duration
	packed.settle_duration = settle_duration
	packed.reference_width = reference_width
	packed.underline_color = underline_color
	packed.underline_thickness = underline_thickness
	packed.underline_grow_duration = underline_grow_duration
	packed.underline_shrink_duration = underline_shrink_duration
	packed.highlight_color = highlight_color
	packed.highlight_duration = highlight_duration
	packed.marker_color = marker_color
	packed.marker_size = marker_size
	packed.marker_margin = marker_margin
	packed.marker_duration = marker_duration
	packed.glow_color = glow_color
	packed.glow_size = glow_size
	packed.glow_duration = glow_duration
	packed.focus_sound = focus_sound
	packed.press_sound = press_sound
	packed.sound_volume_db = sound_volume_db
	return packed


## 按 id 取生成的按钮；找不到返回 null。
func get_button(id: StringName) -> MenuFxButton:
	return _buttons_by_id.get(id)


## 把焦点移到指定菜单项；id 不存在或已禁用时告警并保持现状。
func focus_item(id: StringName) -> void:
	var button := get_button(id)
	if button == null:
		push_warning("MenuList：找不到菜单项 %s，无法聚焦" % id)
		return
	if button.disabled:
		push_warning("MenuList：菜单项 %s 已禁用，无法聚焦" % id)
		return
	button.grab_focus()


## 覆盖层打开期间禁用整列按钮，避免键盘焦点逃出覆盖层。
func set_input_enabled(enabled: bool) -> void:
	for button in _buttons:
		button.disabled = not enabled


## 延迟一帧再入场：刚就绪时容器会重新排版子节点，等布局稳定后才能取到正确位置与尺寸。
func _appear_deferred() -> void:
	await get_tree().process_frame
	_appear()


## 交错入场。滑入只动画 position.x，不碰 y：容器排版是异步的（宿主隐藏期间不排版），
## 播动画时按钮可能还停在原点；若把整个 position 当目标写回，排版结果会被补间逐帧
## 覆盖，整列按钮会叠在原点不动。滑入的静止横向位置取按钮当前的排版结果，不能写死
## 成 0：见循环内的说明。
func _appear() -> void:
	if _appear_tween != null and _appear_tween.is_valid():
		_appear_tween.kill()
	if _buttons.is_empty():
		return
	_appear_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_parallel(true)
	for i in _buttons.size():
		var button := _buttons[i]
		var delay := delay_appear + delay_between_elements * i
		# 静止横向位置取自容器排版结果：按钮按横向 shrink-center 对齐时 x 是居中偏移，
		# 不是 0；写死 0 会把整列按钮贴到容器左缘，菜单看起来整体偏左。
		var rest_x: float = button.position.x
		button.modulate.a = 0.0
		match anim_type:
			AnimType.SCALE:
				button.pivot_offset = button.size / 2.0
				_appear_tween.tween_property(button, "scale", Vector2.ONE, duration).from(Vector2.ZERO).set_delay(delay)
			AnimType.SLIDE_IN_LEFT:
				_appear_tween.tween_property(button, "position:x", rest_x, duration) \
					.from(rest_x - slide_offset).set_delay(delay)
			AnimType.SLIDE_IN_RIGHT:
				_appear_tween.tween_property(button, "position:x", rest_x, duration) \
					.from(rest_x + slide_offset).set_delay(delay)
		# 位移/缩放的同时快速淡入，透明度先行到位让入场更干脆。
		_appear_tween.tween_property(button, "modulate:a", 1.0, duration * 0.5).set_delay(delay)
	# 补间只借 position.x 做动画，静止位始终由容器说了算：动画结束后交回排版，
	# 避免补间写入的值成为最终位置（宿主隐藏期间播放时容器可能还没排过版）。
	_appear_tween.finished.connect(queue_sort)


func _set_buttons_alpha(alpha: float) -> void:
	for button in _buttons:
		button.modulate.a = alpha
