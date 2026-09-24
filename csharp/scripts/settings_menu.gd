extends Control
class_name SettingsMenu

## 设置覆盖层：吉卜力水彩风格整页（与创建世界向导共用同一视觉体系）。
## 布局为"左侧导航 + 右侧分区内容"：左侧纵向导航列表（细分隔线 +
## 选中高亮），右侧为当前分区的设置行；背景为磨砂水彩插画，
## 前景为木框卡片。木牌按钮/卡片框/滑条贴图复用向导页产物，
## 背景画与导航图标由 tools/gemini_media/generate_settings_ui.py 产出。
## 由宿主场景（主菜单、以后的暂停菜单）以隐藏子节点方式持有，
## 通过 open()/close() 控制显隐。
##
## 分区只负责界面与交互，取值一律经 SettingsStore 读写（键位另经 InputBindings
## 落到 InputMap），因此本面板与暂停菜单等其它入口始终共享同一份配置与存档。

signal closed

## 设置分区：顺序与左侧导航自上而下的排列一致。
enum Section { GENERAL, DISPLAY, AUDIO, QUALITY, KEYBIND }

## 显示模式下拉项的翻译 key，顺序与 SettingsStore.DisplayMode 枚举一致。
const DISPLAY_MODE_KEYS: Array[String] = [
	"DISPLAY_MODE_WINDOWED", "DISPLAY_MODE_FULLSCREEN", "DISPLAY_MODE_BORDERLESS",
]

## 画质预设分段按钮的文案 key，顺序与 SettingsStore.QualityPreset 枚举一致。
const QUALITY_PRESET_KEYS: Array[String] = [
	"SETTINGS_QUALITY_LOW", "SETTINGS_QUALITY_MEDIUM", "SETTINGS_QUALITY_HIGH",
]
## 抗锯齿下拉项的文案 key，顺序与 SettingsStore.MSAA_LEVELS 档位表一致；
## 「关闭」一项复用通用的 SETTINGS_DISABLED，避免同一文案两处维护。
const MSAA_KEYS: Array[String] = [
	"SETTINGS_DISABLED", "SETTINGS_MSAA_2X", "SETTINGS_MSAA_4X", "SETTINGS_MSAA_8X",
]

# ---- 字体（与向导页一致的手写体体系） ----
const HEADER_FONT: FontVariation = preload("res://assets/fonts/ui_handwritten_bold.tres")
const BODY_FONT: FontVariation = preload("res://assets/fonts/ui_handwritten.tres")

# ---- 复用向导页的通用 UI 素材 ----
const CARD_FRAME_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_card_frame_9.png")
const CARD_FRAME_MARGIN := 22.0
# 设置页专属关闭与重置图标（由 generate_settings_ui.py 产出）
const CLOSE_ICON_TEX: Texture2D = preload("res://assets/textures/ui/settings_icon_close.png")
const RESET_ICON_TEX: Texture2D = preload("res://assets/textures/ui/settings_icon_reset.png")
const ARROW_TEX: Texture2D = preload("res://assets/textures/ui/settings_arrow_down.svg")
const GUARD_BTN_REGULAR_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_button_regular_9.png")
const GUARD_BTN_PRESSED_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_button_pressed_9.png")
const GUARD_BTN_MARGIN_X := 12.0
const GUARD_BTN_MARGIN_Y := 9.0
const SLIDER_TRACK_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_track.png")
const SLIDER_FILL_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_fill.png")
const SLIDER_GRABBER_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_grabber.png")
const SLIDER_GRABBER_HL_TEX: Texture2D = preload("res://assets/textures/ui/worldgen_slider_grabber_hl.png")

# ---- 本页专属素材（generate_settings_ui.py / generate_menu_icons.py 产出） ----
## 导航分类贴纸图标。
const SECTION_ICONS := {
	Section.GENERAL: preload("res://assets/textures/ui/settings_icon_general.png"),
	Section.DISPLAY: preload("res://assets/textures/ui/settings_icon_display.png"),
	Section.AUDIO: preload("res://assets/textures/ui/settings_icon_audio.png"),
	Section.QUALITY: preload("res://assets/textures/ui/settings_icon_quality.png"),
	Section.KEYBIND: preload("res://assets/textures/ui/settings_icon_keybind.png"),
}

# ---- 诗意自然色彩规范（与向导页共用） ----
const COLOR_INK_PRIMARY := Color(0.13, 0.18, 0.15, 1.0)
const COLOR_INK_MUTED := Color(0.35, 0.44, 0.40, 1.0)
const COLOR_ACCENT_FOREST := Color(0.16, 0.26, 0.20, 1.0)
const COLOR_ACCENT_MOSS := Color(0.28, 0.50, 0.36, 1.0)
const COLOR_CARD_PARCHMENT := Color(0.97, 0.95, 0.90, 0.88)
const COLOR_FIELD_HOVER := Color(1.0, 1.0, 1.0, 0.92)
const GUARD_BTN_HOVER_TINT := Color(1.07, 1.07, 1.05, 1.0)
const GUARD_BTN_DISABLED_TINT := Color(1.0, 1.0, 1.0, 0.5)
const GUARD_BTN_ACTIVE_FONT := Color(0.98, 0.97, 0.92, 1.0)
## 键位冲突与绑定失败的提示色：暖砖红，与水墨正文区分但不刺眼。
const COLOR_HINT_ALERT := Color(0.60, 0.30, 0.16, 1.0)

const PANEL_POP_DURATION := 0.24
const PANEL_CLOSE_DURATION := 0.18

## 键位行的固定列宽：动作名与当前按键徽章，保证各行控件纵向对齐。
const KEYBIND_NAME_WIDTH := 168.0
const KEYBIND_BADGE_WIDTH := 132.0

@onready var _backdrop: TextureRect = %Backdrop
@onready var _frame: Control = %Frame
@onready var _title: Label = %Title
@onready var _close_button: Button = %CloseButton
@onready var _nav_buttons: Array[Button] = [
	%NavGeneral, %NavDisplay, %NavAudio, %NavQuality, %NavKeybind,
]
@onready var _nav_seps: Array[ColorRect] = [
	%NavSepGeneral, %NavSepDisplay, %NavSepAudio, %NavSepQuality, %NavSepKeybind,
]
@onready var _pages: Dictionary = {
	Section.GENERAL: %GeneralPage as ContainerAppear,
	Section.DISPLAY: %DisplayPage as ContainerAppear,
	Section.AUDIO: %AudioPage as ContainerAppear,
	Section.QUALITY: %QualityPage as ContainerAppear,
	Section.KEYBIND: %KeybindPage as ContainerAppear,
}
@onready var _language_option: OptionButton = %LanguageOption
@onready var _mode_option: OptionButton = %ModeOption
@onready var _resolution_option: OptionButton = %ResolutionOption
@onready var _vsync_on: Button = %VSyncOn
@onready var _vsync_off: Button = %VSyncOff
@onready var _quality_buttons: Array[Button] = [%QualityLow, %QualityMedium, %QualityHigh]
@onready var _msaa_option: OptionButton = %MsaaOption
@onready var _master_slider: HSlider = %MasterSlider
@onready var _music_slider: HSlider = %MusicSlider
@onready var _sfx_slider: HSlider = %SfxSlider
@onready var _master_value: RollingNumberLabel = %MasterValue
@onready var _music_value: RollingNumberLabel = %MusicValue
@onready var _sfx_value: RollingNumberLabel = %SfxValue
@onready var _keybind_rows: VBoxContainer = %KeybindRows
@onready var _keybind_hint: Label = %KeybindHint
@onready var _reset_button: Button = %ResetButton
## 界面音效播放器：面板开合音与重置按钮确认音（Menu FX 插件的 MenuFxSound）。
@onready var _sfx: AudioStreamPlayer = %Sfx

var _show_tween: Tween
var _close_tween: Tween
var _closing := false
## 当前所在分区。
var _current_section := Section.GENERAL
## OptionButton / 分段按钮 -> 九宫格背景控件（画在按钮之下）。
var _guard_bg: Dictionary = {}
## 分段按钮 -> 是否选中（VSync 开/关、画质预设、键位重绑与下拉/重置等按钮的皮肤刷新依据）。
var _guard_selected: Dictionary = {}
## 动作 -> {label, label_key, badge, rebind, reset}；行内容由 InputBindings 注册表驱动生成。
var _key_rows: Dictionary = {}
## 正在等待新按键的动作；空表示未进入重绑监听。
var _listening_action := StringName()
## 透明占位把手图标：屏蔽主题默认把手，真实把手由 ThemedSliderSkin 自绘。
var _grabber_stub: ImageTexture


func _ready() -> void:
	_backdrop.modulate.a = 0.0
	_setup_frame_bg()
	_setup_close_button()
	_setup_title()
	_setup_nav()
	_setup_option_button(_language_option)
	_setup_option_button(_mode_option)
	_setup_option_button(_resolution_option)
	_setup_option_button(_msaa_option)
	_setup_guard_button(_vsync_on, true)
	_setup_guard_button(_vsync_off, true)
	_setup_guard_button(_reset_button, false)
	# 为恢复默认设置按钮绑定专属水彩倒转沙漏图标
	_reset_button.icon = RESET_ICON_TEX
	_reset_button.expand_icon = true
	_reset_button.add_theme_constant_override(&"icon_max_width", 22)
	_reset_button.add_theme_constant_override(&"h_separation", 8)
	_setup_slider(_master_slider)
	_setup_slider(_music_slider)
	_setup_slider(_sfx_slider)
	for i in _quality_buttons.size():
		_setup_guard_button(_quality_buttons[i], true)
		_quality_buttons[i].pressed.connect(_on_quality_preset_pressed.bind(i))
	# 键位行由注册表生成，须先于内容标签排版——徽章样式按节点名分派。
	_build_keybind_rows()
	_style_content_labels()
	for resolution: Vector2i in SettingsStore.get_resolutions():
		_resolution_option.add_item("%d × %d" % [resolution.x, resolution.y])
	_mode_option.item_selected.connect(_on_mode_selected)
	_resolution_option.item_selected.connect(_on_resolution_selected)
	_language_option.item_selected.connect(_on_language_selected)
	_msaa_option.item_selected.connect(_on_msaa_selected)
	_vsync_on.pressed.connect(_on_vsync_segment_pressed.bind(true))
	_vsync_off.pressed.connect(_on_vsync_segment_pressed.bind(false))
	_master_slider.value_changed.connect(_on_master_volume_changed)
	_music_slider.value_changed.connect(_on_music_volume_changed)
	_sfx_slider.value_changed.connect(_on_sfx_volume_changed)
	_reset_button.pressed.connect(_on_reset_pressed)
	_close_button.pressed.connect(close)
	_backdrop.gui_input.connect(_on_backdrop_input)
	# 键位可能在别处被改动（暂停菜单、恢复默认设置），统一经信号回流刷新徽章。
	InputBindings.bindings_changed.connect(_refresh_keybind_rows)
	_sync_from_store()
	_show_section(Section.GENERAL, false)


func open() -> void:
	_sync_from_store()
	_closing = false
	if _close_tween != null and _close_tween.is_valid():
		_close_tween.kill()
	visible = true
	# 面板打开音：与卡片弹出动画同步。
	_sfx.play_open()
	# 进入设置默认无焦点：释放宿主菜单遗留的焦点（顺带清掉其强调态）。
	get_viewport().gui_release_focus()
	_play_show()


func close() -> void:
	if _closing or not visible:
		return
	_closing = true
	# 收起面板时退出重绑监听：避免下次打开时仍停在「等待按键」状态。
	_cancel_listen()
	# 面板关闭音：关闭按钮、Esc、点背景统一由此承担收起反馈。
	_sfx.play_close()
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _frame.size.x > 0.0:
		_frame.pivot_offset = _frame.size * 0.5
	_close_tween = create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	_close_tween.tween_property(_frame, "scale", Vector2(0.96, 0.96), PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_frame, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_backdrop, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.chain().tween_callback(_finish_close)


func _finish_close() -> void:
	_closing = false
	visible = false
	_frame.scale = Vector2.ONE
	_frame.modulate.a = 1.0
	_backdrop.modulate.a = 0.0
	closed.emit()


## 打开动画：磨砂背景渐入，木框卡片从中心轻微放大弹出；当前分区行再滑入。
func _play_show() -> void:
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _frame.size.x > 0.0:
		_frame.pivot_offset = _frame.size * 0.5
	_frame.scale = Vector2(0.96, 0.96)
	_frame.modulate.a = 0.0
	_show_tween = create_tween().set_parallel(true).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_show_tween.tween_property(_frame, "scale", Vector2.ONE, PANEL_POP_DURATION)
	_show_tween.tween_property(_frame, "modulate:a", 1.0, PANEL_POP_DURATION)
	_show_tween.tween_property(_backdrop, "modulate:a", 1.0, PANEL_POP_DURATION * 0.8)
	(_pages[_current_section] as ContainerAppear).appear_deferred()


## 重绑监听：抢占式处理（先于 GUI 派发），把下一个按键/鼠标按下当作新绑定。
## Esc 固定为「取消监听」，否则玩家无法退出监听态（要绑 Esc 请用「重置」）。
func _input(event: InputEvent) -> void:
	if _listening_action.is_empty() or _closing or not is_visible_in_tree():
		return
	if event is InputEventKey:
		var key := event as InputEventKey
		if not key.pressed or key.echo:
			return
		get_viewport().set_input_as_handled()
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
	if event.is_action_pressed(&"ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
		return
	# Tab 循环导航：焦点在首/尾控件上继续 Tab 时回绕到另一端。
	if event.is_action_pressed(&"ui_focus_next"):
		var focus := get_viewport().gui_get_focus_owner()
		if focus != null and _is_edge_focusable(focus, false):
			get_viewport().set_input_as_handled()
			_grab_edge_focus(false)
			return
	if event.is_action_pressed(&"ui_focus_prev"):
		var focus := get_viewport().gui_get_focus_owner()
		if focus != null and _is_edge_focusable(focus, true):
			get_viewport().set_input_as_handled()
			_grab_edge_focus(true)


func _grab_edge_focus(from_end: bool) -> void:
	var list: Array[Control] = []
	_collect_focusables(self, list)
	if list.is_empty():
		return
	if from_end:
		list.back().grab_focus()
	else:
		list.front().grab_focus()


func _collect_focusables(node: Node, out: Array[Control]) -> void:
	if node is Control:
		var control := node as Control
		if control.is_visible_in_tree() and control.focus_mode != Control.FOCUS_NONE \
				and not (node is Panel or node is PanelContainer or node is Label):
			out.append(control)
	for child in node.get_children():
		_collect_focusables(child, out)


func _is_edge_focusable(control: Control, from_end: bool) -> bool:
	var list: Array[Control] = []
	_collect_focusables(self, list)
	if list.is_empty():
		return false
	var edge: Control = list.back() if from_end else list.front()
	return edge == control


# ------------------------------------------------------------- 皮肤搭建

## 木框卡片背景：超采样九宫格画在内容之下（StyleBoxTexture 无法超采样）。
func _setup_frame_bg() -> void:
	var bg := NineSliceBg.new(CARD_FRAME_TEX, CARD_FRAME_MARGIN, CARD_FRAME_MARGIN)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	_frame.add_child(bg)
	_frame.move_child(bg, 0)


func _setup_close_button() -> void:
	for state_name in ["normal", "hover", "pressed", "focus"]:
		_close_button.add_theme_stylebox_override(state_name, StyleBoxEmpty.new())
	_close_button.flat = true
	_close_button.text = ""
	_close_button.icon = CLOSE_ICON_TEX
	_close_button.expand_icon = true


func _setup_title() -> void:
	_clean_label(_title)
	_title.add_theme_font_override("font", HEADER_FONT)
	_title.add_theme_font_size_override("font_size", 30)
	_title.add_theme_color_override("font_color", COLOR_INK_PRIMARY)


## 左侧导航：扁平文字按钮 + 分类贴纸图标，每项下方一条细分隔线；
## 选中项文字墨绿、分隔线亮起（参考布局：无框纯文字导航 + 分隔线指示）。
func _setup_nav() -> void:
	for i in _nav_buttons.size():
		var btn := _nav_buttons[i]
		for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
			var empty := StyleBoxEmpty.new()
			empty.content_margin_left = 10
			empty.content_margin_right = 8
			empty.content_margin_top = 6
			empty.content_margin_bottom = 6
			btn.add_theme_stylebox_override(state_name, empty)
		btn.add_theme_font_override("font", HEADER_FONT)
		btn.add_theme_font_size_override("font_size", 19)
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.icon = SECTION_ICONS[i]
		btn.add_theme_constant_override("icon_max_width", 30)
		btn.pressed.connect(_show_section.bind(i, true))
		btn.mouse_entered.connect(_refresh_nav.bind(btn, i))
		btn.mouse_exited.connect(_refresh_nav.bind(btn, i))
		btn.focus_entered.connect(_refresh_nav.bind(btn, i))
		btn.focus_exited.connect(_refresh_nav.bind(btn, i))
		_refresh_nav(btn, i)


## 刷新单个导航项的选中/悬浮态：选中=墨绿文字 + 亮苔绿分隔线；
## 未选中=弱化水墨文字 + 极细暗分隔线；悬浮整体提亮。
func _refresh_nav(btn: Button, section: int) -> void:
	var active := section == _current_section
	var hovered := btn.is_hovered() or btn.has_focus()
	btn.add_theme_color_override("font_color",
		COLOR_ACCENT_FOREST if active else COLOR_INK_MUTED)
	btn.add_theme_color_override("font_hover_color", COLOR_ACCENT_FOREST)
	btn.add_theme_color_override("font_focus_color", COLOR_ACCENT_FOREST)
	btn.add_theme_color_override("font_pressed_color", COLOR_ACCENT_FOREST)
	# 图标与文字一起提亮/压暗：未选中项整体降不透明度，悬浮恢复。
	btn.modulate = Color(1, 1, 1, 0.66) if not active and not hovered else Color.WHITE
	var sep := _nav_seps[section]
	sep.color = COLOR_ACCENT_MOSS if active else Color(0.18, 0.25, 0.22, 0.16)


## OptionButton 水彩木牌皮肤：木面九宫格画在按钮之下，箭头用苔绿雪佛龙，
## 弹出菜单为羊皮纸底 + 苔绿悬停条。
func _setup_option_button(option: OptionButton) -> void:
	for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 14
		empty.content_margin_right = 12
		empty.content_margin_top = 6
		empty.content_margin_bottom = 6
		option.add_theme_stylebox_override(state_name, empty)
	option.add_theme_font_override("font", BODY_FONT)
	option.add_theme_font_size_override("font_size", 16)
	option.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	option.add_theme_color_override("font_hover_color", COLOR_INK_PRIMARY)
	option.add_theme_color_override("font_focus_color", COLOR_INK_PRIMARY)
	option.add_theme_color_override("font_pressed_color", COLOR_INK_PRIMARY)
	option.add_theme_icon_override("arrow", ARROW_TEX)
	var bg := NineSliceBg.new(GUARD_BTN_REGULAR_TEX, GUARD_BTN_MARGIN_X, GUARD_BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	option.add_child(bg)
	_guard_bg[option] = bg
	option.mouse_entered.connect(_refresh_guard.bind(option))
	option.mouse_exited.connect(_refresh_guard.bind(option))
	option.focus_entered.connect(_refresh_guard.bind(option))
	option.focus_exited.connect(_refresh_guard.bind(option))
	_refresh_guard(option)
	_setup_option_popup(option)


## 下拉弹出菜单：羊皮纸面板 + 苔绿悬停条 + 水墨文字。
func _setup_option_popup(option: OptionButton) -> void:
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
	popup.add_theme_font_override("font", BODY_FONT)
	popup.add_theme_font_size_override("font_size", 16)
	popup.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	popup.add_theme_color_override("font_hover_color", COLOR_INK_PRIMARY)


## 守卫风木牌按钮（VSync 分段/画质预设/键位重绑/重置等）：常规态亮木面、
## 选中/按压态深木面。segment 为真时支持选中态（互斥分段），为假时只有悬浮提亮。
func _setup_guard_button(btn: Button, segment: bool) -> void:
	for state_name in ["normal", "hover", "pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = 16
		empty.content_margin_right = 16
		empty.content_margin_top = 5
		empty.content_margin_bottom = 5
		btn.add_theme_stylebox_override(state_name, empty)
	btn.add_theme_font_override("font", HEADER_FONT)
	btn.add_theme_font_size_override("font_size", 16)
	btn.add_theme_color_override("font_disabled_color", Color(COLOR_INK_PRIMARY, 0.4))
	var bg := NineSliceBg.new(GUARD_BTN_REGULAR_TEX, GUARD_BTN_MARGIN_X, GUARD_BTN_MARGIN_Y)
	bg.show_behind_parent = true
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	btn.add_child(bg)
	_guard_bg[btn] = bg
	_guard_selected[btn] = false
	btn.mouse_entered.connect(_refresh_guard.bind(btn))
	btn.mouse_exited.connect(_refresh_guard.bind(btn))
	btn.focus_entered.connect(_refresh_guard.bind(btn))
	btn.focus_exited.connect(_refresh_guard.bind(btn))
	_refresh_guard(btn)


## 刷新木牌按钮背景：选中/按压用深木面 + 浅色文字，悬浮/焦点提亮木面，
## 禁用态整体压暗（键位「重置」在未改动时不可用）。
func _refresh_guard(btn: Button) -> void:
	var bg: NineSliceBg = _guard_bg.get(btn)
	if bg == null:
		return
	var selected: bool = _guard_selected.get(btn, false)
	if selected:
		bg.texture = GUARD_BTN_PRESSED_TEX
		bg.tint = Color.WHITE
		btn.add_theme_color_override("font_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_hover_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_pressed_color", GUARD_BTN_ACTIVE_FONT)
		btn.add_theme_color_override("font_focus_color", GUARD_BTN_ACTIVE_FONT)
	elif btn.disabled:
		bg.texture = GUARD_BTN_REGULAR_TEX
		bg.tint = GUARD_BTN_DISABLED_TINT
		btn.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
	else:
		bg.texture = GUARD_BTN_REGULAR_TEX
		bg.tint = GUARD_BTN_HOVER_TINT if btn.is_hovered() or btn.has_focus() else Color.WHITE
		btn.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
		btn.add_theme_color_override("font_hover_color", COLOR_INK_PRIMARY)
		btn.add_theme_color_override("font_pressed_color", COLOR_INK_PRIMARY)
		btn.add_theme_color_override("font_focus_color", COLOR_INK_PRIMARY)


## 音量滑条水彩皮肤：轨道/填充/橡果把手全部超采样自绘（ThemedSliderSkin），
## 滑条自身的样式盒与把手图标置空屏蔽主题默认绘制。
func _setup_slider(slider: HSlider) -> void:
	var empty := StyleBoxEmpty.new()
	slider.add_theme_stylebox_override("slider", empty)
	slider.add_theme_stylebox_override("grabber_area", empty)
	slider.add_theme_stylebox_override("grabber_area_highlight", empty)
	var stub := _grabber_stub_tex()
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


func _grabber_stub_tex() -> ImageTexture:
	if _grabber_stub == null:
		var img := Image.create(24, 24, false, Image.FORMAT_RGBA8)
		img.fill(Color(0, 0, 0, 0))
		_grabber_stub = ImageTexture.create_from_image(img)
	return _grabber_stub


## 内容区标签排版：按节点名分派样式（标题/描述/行标签/数值徽章/提示条）。
func _style_content_labels() -> void:
	var content := %ContentColumn
	for node in content.find_children("*", "Label", true, false):
		var label := node as Label
		if label == null:
			continue
		_clean_label(label)
		match label.name:
			"PageTitle":
				label.add_theme_font_override("font", HEADER_FONT)
				label.add_theme_font_size_override("font_size", 24)
				label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
			"PageDesc":
				label.add_theme_font_override("font", BODY_FONT)
				label.add_theme_font_size_override("font_size", 13)
				label.add_theme_color_override("font_color", COLOR_INK_MUTED)
			_:
				if label.name.ends_with("Value"):
					# 数值徽章：羊皮纸底圆角小牌。
					label.add_theme_font_override("font", BODY_FONT)
					label.add_theme_font_size_override("font_size", 14)
					label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)
					var badge := StyleBoxFlat.new()
					badge.bg_color = COLOR_CARD_PARCHMENT
					badge.border_color = Color(0.72, 0.54, 0.20, 0.4)
					badge.set_border_width_all(1)
					badge.set_corner_radius_all(8)
					badge.content_margin_left = 8
					badge.content_margin_right = 8
					badge.content_margin_top = 2
					badge.content_margin_bottom = 2
					label.add_theme_stylebox_override("normal", badge)
				elif label.name.ends_with("Hint"):
					# 提示条：弱化小字，颜色由 _set_keybind_hint 在运行时按状态改写。
					label.add_theme_font_override("font", BODY_FONT)
					label.add_theme_font_size_override("font_size", 13)
					label.add_theme_color_override("font_color", COLOR_INK_MUTED)
				else:
					label.add_theme_font_override("font", BODY_FONT)
					label.add_theme_font_size_override("font_size", 16)
					label.add_theme_color_override("font_color", COLOR_INK_PRIMARY)


## 清除从根主题泄漏进来的文字阴影（与向导页同款处理）。
func _clean_label(label: Control) -> void:
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	label.add_theme_constant_override("shadow_outline_size", 0)


# ------------------------------------------------------------- 分区切换

## 切换分区：右页内容整页替换并重播滑入动画，左页导航高亮同步。
func _show_section(section: int, animate := true) -> void:
	_current_section = section
	for s: int in _pages:
		(_pages[s] as Control).visible = s == section
	for i in _nav_buttons.size():
		_refresh_nav(_nav_buttons[i], i)
	if animate and is_visible_in_tree():
		(_pages[section] as ContainerAppear).appear_deferred()


# ------------------------------------------------------------- 设置项读写

func _sync_from_store() -> void:
	# 下拉项由代码填充、不随语言自动重译，这里整体重建并按存档值回填选中项。
	_refresh_option_items()
	_resolution_option.select(SettingsStore.get_resolution_index())
	_master_slider.set_value_no_signal(SettingsStore.get_volume("Master"))
	_music_slider.set_value_no_signal(SettingsStore.get_volume("Music"))
	_sfx_slider.set_value_no_signal(SettingsStore.get_volume("SFX"))
	_update_volume_labels()
	_update_resolution_availability()
	_refresh_vsync_segments()
	_refresh_quality_segments()
	# 面板每次打开都回到未监听态，提示条复位。
	_listening_action = StringName()
	_set_keybind_hint("KEYBIND_HINT_IDLE", COLOR_INK_MUTED)
	_refresh_keybind_rows()


func _on_mode_selected(index: int) -> void:
	SettingsStore.set_display_mode(index)
	_update_resolution_availability()


func _on_language_selected(index: int) -> void:
	SettingsStore.set_language("" if index == 0 else I18n.get_locales()[index - 1])
	# 语言即时生效后重建下拉项与键位行，让代码填充的文案切换到新语言
	# （场景里写死 key 的静态标签由引擎自动重译）。
	_refresh_option_items()
	_refresh_keybind_rows()


func _on_resolution_selected(index: int) -> void:
	SettingsStore.set_resolution_index(index)


## VSync 分段按钮（开启/关闭）：互斥选中态，参考布局的"使用/未使用"双按钮。
func _on_vsync_segment_pressed(enabled: bool) -> void:
	SettingsStore.set_vsync_enabled(enabled)
	_refresh_vsync_segments()


func _refresh_vsync_segments() -> void:
	var enabled := SettingsStore.get_vsync_enabled()
	_guard_selected[_vsync_on] = enabled
	_guard_selected[_vsync_off] = not enabled
	_refresh_guard(_vsync_on)
	_refresh_guard(_vsync_off)


# ------------------------------------------------------------- 画质

## 画质预设（低/中/高）：一键套用对应的抗锯齿档位，之后仍可单独调抗锯齿。
func _on_quality_preset_pressed(preset: int) -> void:
	SettingsStore.set_quality_preset(preset)
	# 预设会一并改动抗锯齿档位，下拉框要跟着回填，两处始终自洽。
	_msaa_option.select(SettingsStore.MSAA_LEVELS.find(SettingsStore.get_msaa_2d()))
	_refresh_quality_segments()


func _on_msaa_selected(index: int) -> void:
	SettingsStore.set_msaa_2d(SettingsStore.MSAA_LEVELS[index])
	# 直接调抗锯齿会回填画质预设，两处分段要同步。
	_refresh_quality_segments()


func _refresh_quality_segments() -> void:
	var preset := SettingsStore.get_quality_preset()
	for i in _quality_buttons.size():
		_guard_selected[_quality_buttons[i]] = i == preset
		_refresh_guard(_quality_buttons[i])


# ------------------------------------------------------------- 键位绑定

## 键位行：每个可重绑动作一行（动作名 / 当前按键徽章 / 重绑 / 重置）。
## 行内容由 InputBindings 注册表驱动生成，注册表增删动作时界面自动跟随。
func _build_keybind_rows() -> void:
	for entry: Dictionary in InputBindings.get_actions():
		var action: StringName = entry["action"]
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)

		var label := Label.new()
		label.name = "KeybindLabel"
		label.custom_minimum_size = Vector2(KEYBIND_NAME_WIDTH, 0.0)
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		row.add_child(label)

		var badge := Label.new()
		# 以 Value 结尾：交给 _style_content_labels 统一套上羊皮纸数值徽章。
		badge.name = "KeybindValue"
		badge.custom_minimum_size = Vector2(KEYBIND_BADGE_WIDTH, 0.0)
		badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		badge.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		row.add_child(badge)

		var rebind := Button.new()
		rebind.custom_minimum_size = Vector2(126, 38)
		rebind.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		rebind.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		_setup_guard_button(rebind, true)
		rebind.pressed.connect(_begin_listen.bind(action))
		row.add_child(rebind)

		var reset := Button.new()
		reset.custom_minimum_size = Vector2(96, 38)
		reset.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		reset.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		_setup_guard_button(reset, false)
		reset.pressed.connect(_on_reset_action.bind(action))
		row.add_child(reset)

		row.add_child(_make_spacer())
		_keybind_rows.add_child(row)
		_key_rows[action] = {
			"label": label,
			"label_key": str(entry["label_key"]),
			"badge": badge,
			"rebind": rebind,
			"reset": reset,
		}


## 行尾弹性占位：让行内控件左对齐，不随容器宽度拉伸。
func _make_spacer() -> Control:
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return spacer


## 进入重绑监听：下一次按键/鼠标按下即作为该动作的新绑定。
func _begin_listen(action: StringName) -> void:
	if not InputBindings.is_rebindable(action):
		return
	_listening_action = action
	_sfx.play_click()
	_refresh_keybind_rows()
	_set_keybind_hint("KEYBIND_HINT_LISTENING", COLOR_INK_MUTED)


## 退出重绑监听并复位提示条；未在监听时为空操作。
func _cancel_listen() -> void:
	if _listening_action.is_empty():
		return
	_listening_action = StringName()
	_refresh_keybind_rows()
	_set_keybind_hint("KEYBIND_HINT_IDLE", COLOR_INK_MUTED)


## 试绑新按键：与其它动作或内置 ui_* 动作冲突时拒绝并指名占用者；
## 成功则由 InputBindings 即时写回 InputMap 并经 SettingsStore 落盘。
func _try_rebind(action: StringName, event: InputEvent) -> void:
	var conflict := InputBindings.find_conflict(action, event)
	if not conflict.is_empty():
		_listening_action = StringName()
		_refresh_keybind_rows()
		_set_keybind_hint("KEYBIND_HINT_CONFLICT", COLOR_HINT_ALERT, [_action_label(conflict)])
		return
	if not InputBindings.rebind(action, event):
		_listening_action = StringName()
		_refresh_keybind_rows()
		_set_keybind_hint("KEYBIND_HINT_FAILED", COLOR_HINT_ALERT)
		return
	_listening_action = StringName()
	_sfx.play_click()
	_refresh_keybind_rows()
	_set_keybind_hint("KEYBIND_HINT_BOUND", COLOR_INK_MUTED,
		[InputBindings.get_binding_text(action)])


## 恢复单个动作的默认键位。
func _on_reset_action(action: StringName) -> void:
	_sfx.play_click()
	InputBindings.reset_action(action)
	_refresh_keybind_rows()
	_set_keybind_hint("KEYBIND_HINT_RESET", COLOR_INK_MUTED, [_action_label(action)])


## 刷新键位行：动作名与按钮文案按当前语言重取，徽章显示当前绑定，
## 「重置」仅在键位被改过时可用。
func _refresh_keybind_rows() -> void:
	for action: StringName in _key_rows:
		var row: Dictionary = _key_rows[action]
		(row["label"] as Label).text = tr(str(row["label_key"]))
		(row["badge"] as Label).text = InputBindings.get_binding_text(action)
		var listening := action == _listening_action
		var rebind: Button = row["rebind"]
		rebind.text = tr("KEYBIND_LISTENING") if listening else tr("KEYBIND_REBIND")
		_guard_selected[rebind] = listening
		_refresh_guard(rebind)
		var reset: Button = row["reset"]
		reset.text = tr("KEYBIND_RESET")
		reset.disabled = InputBindings.is_default(action)
		_refresh_guard(reset)


## 动作的本地化名称（提示条指名冲突占用者时使用）。
func _action_label(action: StringName) -> String:
	for entry: Dictionary in InputBindings.get_actions():
		if entry["action"] == action:
			return tr(str(entry["label_key"]))
	return str(action)


## 键位提示条：按 key 取词并可选地带参格式化，颜色区分常态与警示。
func _set_keybind_hint(key: String, color: Color, args: Array = []) -> void:
	if _keybind_hint == null:
		return
	var text := tr(key)
	_keybind_hint.text = text % args if not args.is_empty() else text
	_keybind_hint.add_theme_color_override("font_color", color)


# ------------------------------------------------------------- 音量与重置

func _on_master_volume_changed(value: float) -> void:
	_set_volume("Master", value, _master_value)


func _on_music_volume_changed(value: float) -> void:
	_set_volume("Music", value, _music_value)


func _on_sfx_volume_changed(value: float) -> void:
	_set_volume("SFX", value, _sfx_value)


func _on_reset_pressed() -> void:
	# 重置按钮的确认音由宿主经 MenuFxSound 触发。
	_sfx.play_click()
	SettingsStore.reset_defaults()
	_sync_from_store()


func _on_backdrop_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		# 仅点击卡片之外的区域才关闭；卡片留白处的点击不当误触处理。
		var mouse := event as InputEventMouseButton
		if not _frame.get_global_rect().has_point(mouse.global_position):
			close()


func _set_volume(bus_name: String, value: float, value_label: RollingNumberLabel) -> void:
	SettingsStore.set_volume(bus_name, value)
	value_label.value = value


func _update_volume_labels() -> void:
	_master_value.value = SettingsStore.get_volume("Master")
	_music_value.value = SettingsStore.get_volume("Music")
	_sfx_value.value = SettingsStore.get_volume("SFX")


## 全屏由系统接管分辨率，此时禁用分辨率选择。
func _update_resolution_availability() -> void:
	_resolution_option.disabled = (
		SettingsStore.get_display_mode() == SettingsStore.DisplayMode.FULLSCREEN
	)


## 代码填充的下拉项不随语言切换自动重译，语言可能已变化时需要整体重建；
## 重建会清空当前选中项，因此按存档值一并回填，避免切语言后下拉框显示回第一项。
func _refresh_option_items() -> void:
	_repopulate_option(_mode_option, _display_mode_labels())
	_repopulate_option(_language_option, _language_labels())
	_repopulate_option(_msaa_option, _msaa_labels())
	_mode_option.select(SettingsStore.get_display_mode())
	_language_option.select(_language_index(SettingsStore.get_language()))
	_msaa_option.select(SettingsStore.MSAA_LEVELS.find(SettingsStore.get_msaa_2d()))


func _display_mode_labels() -> Array[String]:
	var labels: Array[String] = []
	for key: String in DISPLAY_MODE_KEYS:
		labels.append(tr(key))
	return labels


## 抗锯齿下拉项的文案（顺序与 SettingsStore.MSAA_LEVELS 一致）。
func _msaa_labels() -> Array[String]:
	var labels: Array[String] = []
	for key: String in MSAA_KEYS:
		labels.append(tr(key))
	return labels


## 语言下拉：首项为跟随系统，其后列出 I18n 发现的可用语言（原生名显示）。
func _language_labels() -> Array[String]:
	var labels: Array[String] = [tr("SETTINGS_LANGUAGE_AUTO")]
	for locale: String in I18n.get_locales():
		labels.append(I18n.get_native_language_name(locale))
	return labels


func _repopulate_option(option: OptionButton, labels: Array[String]) -> void:
	option.clear()
	for label: String in labels:
		option.add_item(label)


## 语言存档值转下拉索引：0 为跟随系统，其后与 I18n.get_locales() 顺序对应。
func _language_index(code: String) -> int:
	if code.is_empty():
		return 0
	var index := I18n.get_locales().find(code)
	return index + 1 if index >= 0 else 0
