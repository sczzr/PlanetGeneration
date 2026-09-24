class_name PauseMenu
extends Control

## 游戏内暂停菜单（模态浮层）：在游戏界面按暂停键呼出，压暗地图并暂停整棵场景树。
## 分区：音量（主/音乐/音效）、画质（预设 + 抗锯齿 + 垂直同步）、键位绑定；
## 底部为「继续游戏」与「返回主菜单」。
##
## 暂停语义由本节点自身承担：open() 置 get_tree().paused，_finish_close() 复位，
## 因此任何关闭路径（再次按暂停键/Esc、点击菜单外、继续游戏、返回主菜单）都不会
## 漏掉恢复。节点 process_mode 在场景里固定为 ALWAYS，暂停期间仍能收输入、
## 播补间与界面音效。
##
## 只发信号不直接驱动流程：继续游戏与返回主菜单的意图在关闭动画结束后才发出，
## 保证流程层拿到信号时游戏进程已经恢复。

## 菜单已收起并恢复游戏进程（宿主据此归还键盘焦点）。
signal closed
## 玩家选择返回主菜单。
signal main_menu_requested

## 设置分区；顺序与标签行一致。
enum Section { AUDIO, QUALITY, KEYBIND }

const PANEL_POP_DURATION := 0.22
const PANEL_CLOSE_DURATION := 0.16
const PAGE_FADE_DURATION := 0.12
## 卡片最小宽度；三个分区共用同一最小高度，切换标签时卡片不跳动。
const CARD_MIN_WIDTH := 720.0
const PAGE_MIN_HEIGHT := 216.0
## 行标签与键位徽章的固定宽度，保证各行的滑条/按钮纵向对齐。
const ROW_LABEL_WIDTH := 110.0
const KEYBIND_NAME_WIDTH := 168.0
const KEYBIND_BADGE_WIDTH := 132.0

# ---- 素材（与向导页/设置页同一套水彩语言） ----
const FONT_REGULAR := preload("res://assets/fonts/ui_handwritten.tres")
const FONT_BOLD := preload("res://assets/fonts/ui_handwritten_bold.tres")
const BTN_REGULAR_TEX := preload("res://assets/textures/ui/worldgen_button_regular_9.png")
const BTN_PRESSED_TEX := preload("res://assets/textures/ui/worldgen_button_pressed_9.png")
const CARD_FRAME_TEX := preload("res://assets/textures/ui/worldgen_card_frame_9.png")
const ARROW_TEX := preload("res://assets/textures/ui/settings_arrow_down.svg")
const SLIDER_TRACK_TEX := preload("res://assets/textures/ui/worldgen_slider_track.png")
const SLIDER_FILL_TEX := preload("res://assets/textures/ui/worldgen_slider_fill.png")
const SLIDER_GRABBER_TEX := preload("res://assets/textures/ui/worldgen_slider_grabber.png")
const SLIDER_GRABBER_HL_TEX := preload("res://assets/textures/ui/worldgen_slider_grabber_hl.png")
const TAB_ICONS: Array[Texture2D] = [
	preload("res://assets/textures/ui/settings_icon_audio.png"),
	preload("res://assets/textures/ui/settings_icon_quality.png"),
	preload("res://assets/textures/ui/settings_icon_keybind.png"),
]
const RESUME_ICON: Texture2D = preload("res://assets/textures/ui/menu_icon_start.png")
const MENU_ICON: Texture2D = preload("res://assets/textures/ui/menu_icon_quit.png")

const BTN_MARGIN_X := 12.0
const BTN_MARGIN_Y := 9.0
const CARD_MARGIN := 22.0

const COLOR_INK := Color(0.13, 0.18, 0.15, 1.0)
const COLOR_INK_MUTED := Color(0.35, 0.44, 0.40, 1.0)
const COLOR_ACCENT := Color(0.16, 0.26, 0.20, 1.0)
const COLOR_PARCHMENT := Color(0.97, 0.95, 0.90, 0.88)
const COLOR_HINT_ALERT := Color(0.60, 0.30, 0.16, 1.0)
const BTN_HOVER_TINT := Color(1.07, 1.07, 1.05, 1.0)
const BTN_ACTIVE_FONT := Color(0.98, 0.97, 0.92, 1.0)
const BTN_DISABLED_TINT := Color(1.0, 1.0, 1.0, 0.5)

## 分区标签、画质预设与抗锯齿档位的文案 key（顺序与枚举/枚举值一致）。
const SECTION_KEYS: Array[String] = [
	"PAUSE_SECTION_AUDIO", "PAUSE_SECTION_QUALITY", "PAUSE_SECTION_KEYBIND",
]
const QUALITY_KEYS: Array[String] = [
	"PAUSE_QUALITY_LOW", "PAUSE_QUALITY_MEDIUM", "PAUSE_QUALITY_HIGH",
]
const MSAA_KEYS: Array[String] = [
	"PAUSE_MSAA_OFF", "PAUSE_MSAA_2X", "PAUSE_MSAA_4X", "PAUSE_MSAA_8X",
]

@onready var _sfx: AudioStreamPlayer = %Sfx

## 压暗层与卡片（代码构建：内容随存档/注册表动态生成）。
var _dim: ColorRect
var _card: NineSlicePanel
var _tabs: Array[Button] = []
var _pages: Array[Control] = []
## 总线名 -> 滑条 / 数值徽章。
var _volume_sliders: Dictionary = {}
var _volume_values: Dictionary = {}
var _quality_buttons: Array[Button] = []
var _msaa_option: OptionButton
## [开启, 关闭] 两个分段按钮。
var _vsync_buttons: Array[Button] = []
## 动作 -> {badge, rebind, reset}。
var _key_rows: Dictionary = {}
var _keybind_hint: Label
var _resume_button: Button
var _menu_button: Button
## 木牌按钮 -> 九宫格背景（皮肤刷新依据）。
var _button_bg: Dictionary = {}
var _grabber_stub: ImageTexture
## i18n 登记表：代码创建/赋值的控件文案在语言切换时统一刷新。
var _localized: Array[Dictionary] = []

var _show_tween: Tween
var _close_tween: Tween
var _closing := false
var _current_section := Section.AUDIO
## 正在等待新按键的动作；空表示未进入重绑监听。
var _listening_action := StringName()
## 关闭后的去向：true 回主菜单，false 继续游戏。
var _exit_to_menu := false


func _ready() -> void:
	visible = false
	_build_ui()
	_refresh_texts()
	_sync_from_store()
	InputBindings.bindings_changed.connect(_refresh_keybinds)


# ------------------------------------------------------------------ 开合

## 打开菜单并暂停游戏进程。已打开时为空操作。
func open() -> void:
	if visible:
		return
	_closing = false
	if _close_tween != null and _close_tween.is_valid():
		_close_tween.kill()
	_sync_from_store()
	visible = true
	# 先暂停再播动画：暂停后本节点仍是 ALWAYS，补间与音效照常推进。
	get_tree().paused = true
	_sfx.play_open()
	_play_show()
	_resume_button.grab_focus()


## 收起菜单并恢复游戏进程。关闭动画期间重复调用为空操作。
func close() -> void:
	if _closing or not visible:
		return
	_closing = true
	_cancel_listen()
	_sfx.play_close()
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _card.size.x > 0.0:
		_card.pivot_offset = _card.size * 0.5
	_close_tween = create_tween().set_parallel(true) \
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	_close_tween.tween_property(_card, "scale", Vector2(0.96, 0.96), PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_card, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_dim, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.chain().tween_callback(_finish_close)


func is_open() -> bool:
	return visible


func _finish_close() -> void:
	_closing = false
	visible = false
	_card.scale = Vector2.ONE
	_card.modulate.a = 1.0
	_dim.modulate.a = 1.0
	# 恢复游戏进程：与 open() 成对，任何关闭路径都收敛到这里。
	get_tree().paused = false
	closed.emit()
	if _exit_to_menu:
		_exit_to_menu = false
		main_menu_requested.emit()


## 打开动画：压暗层渐入，木框卡片从 94% 弹开，当前分区内容随后淡入。
func _play_show() -> void:
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _card.size.x > 0.0:
		_card.pivot_offset = _card.size * 0.5
	_card.scale = Vector2(0.94, 0.94)
	_card.modulate.a = 0.0
	_dim.modulate.a = 0.0
	var page := _pages[_current_section] as Control
	page.modulate.a = 0.0
	_show_tween = create_tween().set_parallel(true) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_show_tween.tween_property(_card, "scale", Vector2.ONE, PANEL_POP_DURATION)
	_show_tween.tween_property(_card, "modulate:a", 1.0, PANEL_POP_DURATION)
	_show_tween.tween_property(_dim, "modulate:a", 1.0, PANEL_POP_DURATION * 0.8)
	_show_tween.tween_property(page, "modulate:a", 1.0, PANEL_POP_DURATION)


# ------------------------------------------------------------------ 输入

## 重绑监听：抢占式处理（先于 GUI），把下一个按键/鼠标按下当作新绑定。
func _input(event: InputEvent) -> void:
	if _listening_action.is_empty() or not visible:
		return
	if event is InputEventKey:
		var key := event as InputEventKey
		if not key.pressed or key.echo:
			return
		get_viewport().set_input_as_handled()
		# Esc 始终是「取消监听」，否则玩家无法从监听态退出（要绑 Esc 用「重置」）。
		if key.is_action_pressed(&"ui_cancel"):
			_cancel_listen()
		else:
			_try_rebind(_listening_action, key)
	elif event is InputEventMouseButton:
		var mouse := event as InputEventMouseButton
		if not mouse.pressed:
			return
		get_viewport().set_input_as_handled()
		_try_rebind(_listening_action, mouse)


func _unhandled_input(event: InputEvent) -> void:
	if not is_visible_in_tree() or _closing:
		return
	# 再次按下暂停键或 Esc 收起菜单。事件已由监听态消费时不会走到这里。
	if event.is_action_pressed(&"game_pause") or event.is_action_pressed(&"ui_cancel"):
		get_viewport().set_input_as_handled()
		close()


## 点击卡片之外的压暗区域收起菜单（卡片自身拦截鼠标，不会走到这里）。
func _on_dim_input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton and event.pressed):
		return
	var mouse := event as InputEventMouseButton
	if mouse.button_index != MOUSE_BUTTON_LEFT:
		return
	if not _card.get_global_rect().has_point(mouse.global_position):
		close()


# ------------------------------------------------------------------ UI 构建

func _build_ui() -> void:
	_dim = ColorRect.new()
	_dim.name = "Dim"
	_dim.color = Color(0.05, 0.09, 0.07, 0.55)
	_dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_dim.mouse_filter = Control.MOUSE_FILTER_STOP
	_dim.gui_input.connect(_on_dim_input)
	add_child(_dim)

	_card = NineSlicePanel.new(CARD_FRAME_TEX, CARD_MARGIN, CARD_MARGIN, 26.0, 20.0)
	_card.name = "Card"
	_card.bg.tint = Color(1.0, 1.0, 1.0, 0.96)
	_card.set_anchors_preset(Control.PRESET_CENTER)
	# 锚在屏幕中心后必须双向生长，否则卡片从中心点向右下展开、底部被截断。
	_card.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_card.grow_vertical = Control.GROW_DIRECTION_BOTH
	_card.custom_minimum_size = Vector2(CARD_MIN_WIDTH, 0.0)
	_card.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_card)

	var column := VBoxContainer.new()
	column.name = "Column"
	column.add_theme_constant_override("separation", 14)
	_card.content.add_child(column)

	column.add_child(_make_title())
	column.add_child(_make_tab_row())
	for section in SECTION_KEYS.size():
		var page := _make_page(section)
		_pages.append(page)
		column.add_child(page)
	column.add_child(_make_footer())
	_show_section(Section.AUDIO, false)


func _make_title() -> Label:
	var label := Label.new()
	label.name = "Title"
	label.text = tr("PAUSE_TITLE")
	label.add_theme_font_override("font", FONT_BOLD)
	label.add_theme_font_size_override("font_size", 28)
	label.add_theme_color_override("font_color", COLOR_ACCENT)
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_register_localized(label, "PAUSE_TITLE")
	return label


## 分区标签行：互斥 toggle 按钮，选中态走按下贴图。
func _make_tab_row() -> Control:
	var row := HBoxContainer.new()
	row.name = "TabRow"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 10)
	var group := ButtonGroup.new()
	for i in SECTION_KEYS.size():
		var button := _make_button()
		_skin_button(button, true)
		button.toggle_mode = true
		button.button_group = group
		button.custom_minimum_size = Vector2(176.0, 0.0)
		if i < TAB_ICONS.size() and TAB_ICONS[i] != null:
			button.icon = TAB_ICONS[i]
			button.expand_icon = true
			button.add_theme_constant_override("icon_max_width", 22)
			button.add_theme_constant_override("h_separation", 6)
		button.pressed.connect(_show_section.bind(i))
		row.add_child(button)
		_tabs.append(button)
		_register_localized(button, SECTION_KEYS[i])
	return row


func _make_page(section: int) -> Control:
	var page := VBoxContainer.new()
	page.name = "Page%d" % section
	page.add_theme_constant_override("separation", 8)
	page.custom_minimum_size = Vector2(0.0, PAGE_MIN_HEIGHT)
	match section:
		Section.AUDIO:
			_build_audio_page(page)
		Section.QUALITY:
			_build_quality_page(page)
		Section.KEYBIND:
			_build_keybind_page(page)
	return page


## 音量分区：主音量/音乐/音效三条滑条，拖动即时生效并按防抖写盘。
func _build_audio_page(page: Control) -> void:
	_add_volume_row(page, "SETTINGS_MASTER_VOLUME", "Master")
	_add_volume_row(page, "SETTINGS_MUSIC_VOLUME", "Music")
	_add_volume_row(page, "SETTINGS_SFX_VOLUME", "SFX")


## 画质分区：画质预设（一键套用抗锯齿）、抗锯齿档位、垂直同步。
func _build_quality_page(page: Control) -> void:
	var preset_row := _make_row()
	var preset_label := _make_row_label("PAUSE_QUALITY_PRESET", ROW_LABEL_WIDTH)
	preset_row.add_child(preset_label)
	var group := ButtonGroup.new()
	for i in QUALITY_KEYS.size():
		var button := _make_button()
		_skin_button(button, true)
		button.toggle_mode = true
		button.button_group = group
		button.custom_minimum_size = Vector2(96.0, 0.0)
		button.pressed.connect(_on_quality_preset_pressed.bind(i))
		preset_row.add_child(button)
		_quality_buttons.append(button)
		_register_localized(button, QUALITY_KEYS[i])
	preset_row.add_child(_make_spacer())
	page.add_child(preset_row)

	var msaa_row := _make_row()
	msaa_row.add_child(_make_row_label("PAUSE_ANTIALIASING", ROW_LABEL_WIDTH))
	_msaa_option = OptionButton.new()
	_skin_option_button(_msaa_option)
	_msaa_option.custom_minimum_size = Vector2(180.0, 0.0)
	for i in MSAA_KEYS.size():
		_msaa_option.add_item(tr(MSAA_KEYS[i]), i)
	_msaa_option.item_selected.connect(_on_msaa_selected)
	msaa_row.add_child(_msaa_option)
	msaa_row.add_child(_make_spacer())
	page.add_child(msaa_row)

	var vsync_row := _make_row()
	vsync_row.add_child(_make_row_label("SETTINGS_VSYNC", ROW_LABEL_WIDTH))
	for i in 2:
		var button := _make_button()
		_skin_button(button, true)
		button.toggle_mode = true
		button.custom_minimum_size = Vector2(96.0, 0.0)
		button.pressed.connect(_on_vsync_segment_pressed.bind(i == 0))
		vsync_row.add_child(button)
		_vsync_buttons.append(button)
		_register_localized(button, "SETTINGS_ENABLED" if i == 0 else "SETTINGS_DISABLED")
	vsync_row.add_child(_make_spacer())
	page.add_child(vsync_row)


## 键位分区：每个可重绑动作一行（动作名 / 当前按键 / 重绑 / 重置），
## 底部一条提示条反馈监听、冲突与绑定结果。
func _build_keybind_page(page: Control) -> void:
	for entry: Dictionary in InputBindings.get_actions():
		var action: StringName = entry["action"]
		var label_key := str(entry["label_key"])
		var row := _make_row()
		row.add_child(_make_row_label(label_key, KEYBIND_NAME_WIDTH))

		var badge := Label.new()
		badge.custom_minimum_size = Vector2(KEYBIND_BADGE_WIDTH, 0.0)
		badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_skin_badge(badge)
		row.add_child(badge)

		var rebind := _make_button()
		_skin_button(rebind, true)
		rebind.toggle_mode = true
		rebind.custom_minimum_size = Vector2(126.0, 0.0)
		rebind.pressed.connect(_begin_listen.bind(action))
		row.add_child(rebind)

		var reset := _make_button()
		_skin_button(reset, false)
		reset.custom_minimum_size = Vector2(96.0, 0.0)
		reset.pressed.connect(_on_reset_action.bind(action))
		row.add_child(reset)

		row.add_child(_make_spacer())
		page.add_child(row)
		_key_rows[action] = {"badge": badge, "rebind": rebind, "reset": reset}
		_register_localized(rebind, "KEYBIND_REBIND")
		_register_localized(reset, "KEYBIND_RESET")

	_keybind_hint = Label.new()
	_keybind_hint.add_theme_font_override("font", FONT_REGULAR)
	_keybind_hint.add_theme_font_size_override("font_size", 14)
	_keybind_hint.add_theme_color_override("font_color", COLOR_INK_MUTED)
	_keybind_hint.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	_keybind_hint.add_theme_constant_override("shadow_offset_x", 0)
	_keybind_hint.add_theme_constant_override("shadow_offset_y", 0)
	_keybind_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_keybind_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	page.add_child(_keybind_hint)


func _make_footer() -> Control:
	var column := VBoxContainer.new()
	column.name = "Footer"
	column.add_theme_constant_override("separation", 8)

	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 18)
	_resume_button = _make_button()
	_skin_button(_resume_button, false)
	_resume_button.custom_minimum_size = Vector2(190.0, 0.0)
	_resume_button.icon = RESUME_ICON
	_resume_button.expand_icon = true
	_resume_button.add_theme_constant_override("icon_max_width", 22)
	_resume_button.add_theme_constant_override("h_separation", 8)
	_resume_button.pressed.connect(_on_resume_pressed)
	row.add_child(_resume_button)
	_menu_button = _make_button()
	_skin_button(_menu_button, false)
	_menu_button.custom_minimum_size = Vector2(190.0, 0.0)
	_menu_button.icon = MENU_ICON
	_menu_button.expand_icon = true
	_menu_button.add_theme_constant_override("icon_max_width", 22)
	_menu_button.add_theme_constant_override("h_separation", 8)
	_menu_button.pressed.connect(_on_menu_pressed)
	row.add_child(_menu_button)
	column.add_child(row)
	_register_localized(_resume_button, "PAUSE_RESUME")
	_register_localized(_menu_button, "PAUSE_BACK_TO_MENU")

	var hint := Label.new()
	hint.text = tr("PAUSE_HINT")
	hint.add_theme_font_override("font", FONT_REGULAR)
	hint.add_theme_font_size_override("font_size", 13)
	hint.add_theme_color_override("font_color", COLOR_INK_MUTED)
	hint.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	hint.add_theme_constant_override("shadow_offset_x", 0)
	hint.add_theme_constant_override("shadow_offset_y", 0)
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(hint)
	_register_localized(hint, "PAUSE_HINT")
	return column


## 音量行：标签 + 水彩滑条 + 数值徽章；滑条与徽章按总线名登记。
func _add_volume_row(page: Control, label_key: String, bus_name: String) -> void:
	var row := _make_row()
	row.add_child(_make_row_label(label_key, ROW_LABEL_WIDTH))

	var slider := HSlider.new()
	slider.min_value = 0.0
	slider.max_value = 100.0
	slider.step = 1.0
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.custom_minimum_size = Vector2(0.0, 24.0)
	_skin_slider(slider)
	slider.value_changed.connect(_on_volume_changed.bind(bus_name))
	row.add_child(slider)
	_volume_sliders[bus_name] = slider

	var value := RollingNumberLabel.new()
	value.custom_minimum_size = Vector2(66.0, 0.0)
	value.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_skin_badge(value)
	row.add_child(value)
	_volume_values[bus_name] = value

	page.add_child(row)


func _make_row() -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	return row


func _make_spacer() -> Control:
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return spacer


func _make_row_label(key: String, min_width: float) -> Label:
	var label := Label.new()
	label.text = tr(key)
	label.custom_minimum_size = Vector2(min_width, 0.0)
	label.add_theme_font_override("font", FONT_REGULAR)
	label.add_theme_font_size_override("font_size", 16)
	label.add_theme_color_override("font_color", COLOR_INK)
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_register_localized(label, key)
	return label


## 空文案按钮：文案统一由 i18n 登记表刷新，避免两处写死。
func _make_button() -> Button:
	var button := Button.new()
	button.add_theme_font_override("font", FONT_BOLD)
	button.add_theme_font_size_override("font_size", 16)
	return button


## 羊皮纸数值徽章（滑条读数、键位徽章共用）。
func _skin_badge(label: Label) -> void:
	label.add_theme_font_override("font", FONT_REGULAR)
	label.add_theme_font_size_override("font_size", 15)
	label.add_theme_color_override("font_color", COLOR_INK)
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	var badge := StyleBoxFlat.new()
	badge.bg_color = COLOR_PARCHMENT
	badge.border_color = Color(0.72, 0.54, 0.20, 0.4)
	badge.set_border_width_all(1)
	badge.set_corner_radius_all(8)
	badge.content_margin_left = 10
	badge.content_margin_right = 10
	badge.content_margin_top = 3
	badge.content_margin_bottom = 3
	label.add_theme_stylebox_override("normal", badge)


## 木牌按钮皮肤：常态亮木面、选中/按压深木面，悬浮与焦点提亮（与向导页/设置页同源）。
## toggle 为真时随 toggle 状态刷新背景，供标签行与分段按钮使用。
func _skin_button(button: Button, toggle: bool) -> void:
	for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 16
		empty.content_margin_right = 16
		empty.content_margin_top = 5
		empty.content_margin_bottom = 5
		button.add_theme_stylebox_override(state_name, empty)
	button.add_theme_color_override("font_disabled_color", Color(COLOR_INK, 0.4))
	button.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	button.add_theme_constant_override("shadow_offset_x", 0)
	button.add_theme_constant_override("shadow_offset_y", 0)
	var bg := NineSliceBg.new(BTN_REGULAR_TEX, BTN_MARGIN_X, BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	button.add_child(bg)
	_button_bg[button] = bg
	button.mouse_entered.connect(_refresh_button_bg.bind(button))
	button.mouse_exited.connect(_refresh_button_bg.bind(button))
	button.focus_entered.connect(_refresh_button_bg.bind(button))
	button.focus_exited.connect(_refresh_button_bg.bind(button))
	if toggle:
		button.toggled.connect(func(_on: bool) -> void: _refresh_button_bg(button))
	_refresh_button_bg(button)


func _refresh_button_bg(button: Button) -> void:
	var bg: NineSliceBg = _button_bg.get(button)
	if bg == null:
		return
	if button.button_pressed:
		bg.texture = BTN_PRESSED_TEX
		bg.tint = Color.WHITE
		_set_button_font_color(button, BTN_ACTIVE_FONT)
	elif button.disabled:
		bg.texture = BTN_REGULAR_TEX
		bg.tint = BTN_DISABLED_TINT
		_set_button_font_color(button, COLOR_INK)
	else:
		bg.texture = BTN_REGULAR_TEX
		bg.tint = BTN_HOVER_TINT if button.is_hovered() or button.has_focus() else Color.WHITE
		_set_button_font_color(button, COLOR_INK)


func _set_button_font_color(button: Button, color: Color) -> void:
	for state_name in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(state_name, color)


## 下拉框水彩皮肤：木面九宫格画在按钮之下，箭头用苔绿雪佛龙，弹出菜单为羊皮纸底。
func _skin_option_button(option: OptionButton) -> void:
	for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 14
		empty.content_margin_right = 12
		empty.content_margin_top = 6
		empty.content_margin_bottom = 6
		option.add_theme_stylebox_override(state_name, empty)
	option.add_theme_font_override("font", FONT_REGULAR)
	option.add_theme_font_size_override("font_size", 16)
	_set_button_font_color(option, COLOR_INK)
	option.add_theme_icon_override("arrow", ARROW_TEX)
	var bg := NineSliceBg.new(BTN_REGULAR_TEX, BTN_MARGIN_X, BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	option.add_child(bg)
	_button_bg[option] = bg
	option.mouse_entered.connect(_refresh_button_bg.bind(option))
	option.mouse_exited.connect(_refresh_button_bg.bind(option))
	option.focus_entered.connect(_refresh_button_bg.bind(option))
	option.focus_exited.connect(_refresh_button_bg.bind(option))
	_refresh_button_bg(option)

	var panel := StyleBoxFlat.new()
	panel.bg_color = Color(0.98, 0.96, 0.91, 0.98)
	panel.border_color = Color(0.30, 0.48, 0.38, 0.35)
	panel.set_border_width_all(1)
	panel.set_corner_radius_all(8)
	panel.content_margin_left = 12
	panel.content_margin_right = 12
	panel.content_margin_top = 6
	panel.content_margin_bottom = 6
	var hover := StyleBoxFlat.new()
	hover.bg_color = Color(0.30, 0.48, 0.38, 0.16)
	hover.set_corner_radius_all(6)
	var popup := option.get_popup()
	popup.add_theme_stylebox_override("panel", panel)
	popup.add_theme_stylebox_override("hover", hover)
	popup.add_theme_font_override("font", FONT_REGULAR)
	popup.add_theme_font_size_override("font_size", 16)
	popup.add_theme_color_override("font_color", COLOR_INK)
	popup.add_theme_color_override("font_hover_color", COLOR_INK)


## 音量滑条水彩皮肤：轨道/填充/把手全部超采样自绘，滑条自身的样式盒与把手置空。
func _skin_slider(slider: HSlider) -> void:
	var empty := StyleBoxEmpty.new()
	slider.add_theme_stylebox_override("slider", empty)
	slider.add_theme_stylebox_override("grabber_area", empty)
	slider.add_theme_stylebox_override("grabber_area_highlight", empty)
	var stub := _grabber_stub_texture()
	slider.add_theme_icon_override("grabber", stub)
	slider.add_theme_icon_override("grabber_highlight", stub)
	slider.add_theme_icon_override("grabber_disabled", stub)
	var skin := ThemedSliderSkin.new()
	skin.slider = slider
	skin.track_tex = SLIDER_TRACK_TEX
	skin.fill_tex = SLIDER_FILL_TEX
	skin.grabber_tex = SLIDER_GRABBER_TEX
	skin.grabber_hl_tex = SLIDER_GRABBER_HL_TEX
	skin.particle_theme = ""
	slider.add_child(skin)


## 透明占位把手图标：屏蔽主题默认把手（真实把手由 ThemedSliderSkin 自绘）。
func _grabber_stub_texture() -> ImageTexture:
	if _grabber_stub == null:
		var image := Image.create(24, 24, false, Image.FORMAT_RGBA8)
		image.fill(Color(0, 0, 0, 0))
		_grabber_stub = ImageTexture.create_from_image(image)
	return _grabber_stub


# ------------------------------------------------------------------ 分区切换

func _show_section(section: int, animate := true) -> void:
	_current_section = section
	for i in _pages.size():
		(_pages[i] as Control).visible = i == section
	for i in _tabs.size():
		_tabs[i].set_pressed_no_signal(i == section)
		_refresh_button_bg(_tabs[i])
	if not animate or not is_visible_in_tree():
		return
	var page := _pages[section] as Control
	page.modulate.a = 0.0
	create_tween().tween_property(page, "modulate:a", 1.0, PAGE_FADE_DURATION)


# ------------------------------------------------------------------ 设置读写

func _sync_from_store() -> void:
	for bus_name: String in SettingsStore.DEFAULT_VOLUMES:
		var value: float = SettingsStore.get_volume(bus_name)
		(_volume_sliders[bus_name] as HSlider).set_value_no_signal(value)
		(_volume_values[bus_name] as RollingNumberLabel).value = value
	_refresh_quality_segments()
	_msaa_option.select(SettingsStore.MSAA_LEVELS.find(SettingsStore.get_msaa_2d()))
	_refresh_vsync_segments()
	_refresh_keybinds()


func _on_volume_changed(value: float, bus_name: String) -> void:
	SettingsStore.set_volume(bus_name, value)
	(_volume_values[bus_name] as RollingNumberLabel).value = value


func _on_quality_preset_pressed(preset: int) -> void:
	SettingsStore.set_quality_preset(preset)
	_refresh_quality_segments()
	# 预设会一并改动抗锯齿档位，下拉框要跟着回填。
	_msaa_option.select(SettingsStore.MSAA_LEVELS.find(SettingsStore.get_msaa_2d()))


func _refresh_quality_segments() -> void:
	var preset := SettingsStore.get_quality_preset()
	for i in _quality_buttons.size():
		_quality_buttons[i].set_pressed_no_signal(i == preset)
		_refresh_button_bg(_quality_buttons[i])


func _on_msaa_selected(index: int) -> void:
	SettingsStore.set_msaa_2d(SettingsStore.MSAA_LEVELS[index])
	# 直接调抗锯齿会回填画质预设，两处分段要同步。
	_refresh_quality_segments()


func _on_vsync_segment_pressed(enabled: bool) -> void:
	SettingsStore.set_vsync_enabled(enabled)
	_refresh_vsync_segments()


func _refresh_vsync_segments() -> void:
	var enabled := SettingsStore.get_vsync_enabled()
	for i in _vsync_buttons.size():
		_vsync_buttons[i].set_pressed_no_signal(enabled == (i == 0))
		_refresh_button_bg(_vsync_buttons[i])


# ------------------------------------------------------------------ 键位绑定

func _begin_listen(action: StringName) -> void:
	_listening_action = action
	_sfx.play_click()
	_set_keybind_hint("KEYBIND_HINT_LISTENING", COLOR_INK_MUTED)
	_refresh_keybinds()


func _cancel_listen() -> void:
	if _listening_action.is_empty():
		return
	_listening_action = StringName()
	_set_keybind_hint("KEYBIND_HINT_IDLE", COLOR_INK_MUTED)
	_refresh_keybinds()


## 试绑新按键：冲突则拒绝并指名占用者，成功则即时生效并写盘。
func _try_rebind(action: StringName, event: InputEvent) -> void:
	var conflict := InputBindings.find_conflict(action, event)
	if not conflict.is_empty():
		_listening_action = StringName()
		_set_keybind_hint("KEYBIND_HINT_CONFLICT", COLOR_HINT_ALERT, [_action_label(conflict)])
		_refresh_keybinds()
		return
	if not InputBindings.rebind(action, event):
		_listening_action = StringName()
		_set_keybind_hint("KEYBIND_HINT_FAILED", COLOR_HINT_ALERT)
		_refresh_keybinds()
		return
	_listening_action = StringName()
	_sfx.play_click()
	_set_keybind_hint("KEYBIND_HINT_BOUND",
		COLOR_INK_MUTED, [InputBindings.get_binding_text(action)])
	_refresh_keybinds()


func _on_reset_action(action: StringName) -> void:
	_sfx.play_click()
	InputBindings.reset_action(action)
	_set_keybind_hint("KEYBIND_HINT_RESET", COLOR_INK_MUTED, [_action_label(action)])


## 刷新键位行：当前绑定徽章、监听态按钮文案与「重置」可用性。
func _refresh_keybinds() -> void:
	for action: StringName in _key_rows:
		var row: Dictionary = _key_rows[action]
		(row["badge"] as Label).text = InputBindings.get_binding_text(action)
		var listening := action == _listening_action
		var rebind: Button = row["rebind"]
		rebind.text = tr("KEYBIND_LISTENING") if listening else tr("KEYBIND_REBIND")
		rebind.set_pressed_no_signal(listening)
		_refresh_button_bg(rebind)
		var reset: Button = row["reset"]
		reset.disabled = InputBindings.is_default(action)
		_refresh_button_bg(reset)


func _action_label(action: StringName) -> String:
	for entry: Dictionary in InputBindings.get_actions():
		if entry["action"] == action:
			return tr(str(entry["label_key"]))
	return str(action)


func _set_keybind_hint(key: String, color: Color, args: Array = []) -> void:
	if _keybind_hint == null:
		return
	var text := tr(key)
	_keybind_hint.text = text % args if not args.is_empty() else text
	_keybind_hint.add_theme_color_override("font_color", color)


# ------------------------------------------------------------------ 按钮回调

func _on_resume_pressed() -> void:
	_sfx.play_click()
	_exit_to_menu = false
	close()


func _on_menu_pressed() -> void:
	_sfx.play_click()
	_exit_to_menu = true
	close()


# ------------------------------------------------------------------ i18n

func _register_localized(node: Control, key: String) -> void:
	_localized.append({"node": node, "key": key})


func _refresh_texts() -> void:
	for entry: Dictionary in _localized:
		var node: Control = entry["node"]
		if is_instance_valid(node):
			node.text = tr(entry["key"])
	# 下拉项是代码添加的，不随语言切换自动重译，需要整体重建。
	var selected := _msaa_option.selected
	_msaa_option.clear()
	for i in MSAA_KEYS.size():
		_msaa_option.add_item(tr(MSAA_KEYS[i]), i)
	_msaa_option.select(selected)
	_set_keybind_hint("KEYBIND_HINT_IDLE", COLOR_INK_MUTED)
