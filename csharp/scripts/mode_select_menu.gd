extends Control
class_name ModeSelectMenu

## 模式选择覆盖层：点击主菜单"开始游戏"后弹出，供玩家在冒险模式与守卫模式之间选择。
## 由宿主场景（主菜单、以后的暂停菜单）以隐藏子节点方式持有，通过 open()/close() 控制显隐；
## 选中模式后发出 mode_selected 并自行关闭，宿主监听信号驱动后续开局流程。

signal closed
signal mode_selected(mode: StringName)

## 模式标识：随 mode_selected 发出，供流程层区分模式；界面文案走翻译 key。
const MODE_ADVENTURE := &"Adventure"
const MODE_GUARD := &"Guard"

const PANEL_POP_DURATION := 0.2
const PANEL_CLOSE_DURATION := 0.15

@onready var _dim: ColorRect = %Dim
@onready var _panel: ContainerAppear = %Panel
## 界面音效播放器：面板打开/关闭音（Menu FX 插件的 MenuFxSound）。
@onready var _sfx: AudioStreamPlayer = %Sfx
@onready var _adventure_button: Button = %AdventureButton
@onready var _guard_button: Button = %GuardButton
@onready var _back_button: Button = %BackButton

var _show_tween: Tween
var _close_tween: Tween
var _closing := false


func _ready() -> void:
	# 关闭态下把压暗层预置为全透明，打开时从当前值渐入即可。
	_dim.modulate.a = 0.0
	_adventure_button.pressed.connect(_on_mode_pressed.bind(MODE_ADVENTURE))
	_guard_button.pressed.connect(_on_mode_pressed.bind(MODE_GUARD))
	_back_button.pressed.connect(close)
	_dim.gui_input.connect(_on_dim_input)
	_setup_focus_trap()


## 显式指定焦点邻接与 Tab 顺序：两个界面都居中排布，自动焦点寻路会穿过压暗层
## 找到主菜单按钮（如右键从"冒险模式"跳到"加载游戏"），把键盘导航困在覆盖层内。
func _setup_focus_trap() -> void:
	_adventure_button.focus_neighbor_left = _adventure_button.get_path_to(_guard_button)
	_adventure_button.focus_neighbor_right = _adventure_button.get_path_to(_guard_button)
	_adventure_button.focus_neighbor_top = _adventure_button.get_path_to(_back_button)
	_adventure_button.focus_neighbor_bottom = _adventure_button.get_path_to(_back_button)
	_adventure_button.focus_next = _adventure_button.get_path_to(_guard_button)
	_adventure_button.focus_previous = _adventure_button.get_path_to(_back_button)
	_guard_button.focus_neighbor_left = _guard_button.get_path_to(_adventure_button)
	_guard_button.focus_neighbor_right = _guard_button.get_path_to(_adventure_button)
	_guard_button.focus_neighbor_top = _guard_button.get_path_to(_back_button)
	_guard_button.focus_neighbor_bottom = _guard_button.get_path_to(_back_button)
	_guard_button.focus_next = _guard_button.get_path_to(_back_button)
	_guard_button.focus_previous = _guard_button.get_path_to(_adventure_button)
	_back_button.focus_neighbor_left = _back_button.get_path_to(_adventure_button)
	_back_button.focus_neighbor_right = _back_button.get_path_to(_guard_button)
	_back_button.focus_neighbor_top = _back_button.get_path_to(_adventure_button)
	_back_button.focus_neighbor_bottom = _back_button.get_path_to(_guard_button)
	_back_button.focus_next = _back_button.get_path_to(_adventure_button)
	_back_button.focus_previous = _back_button.get_path_to(_guard_button)


func open() -> void:
	_closing = false
	if _close_tween != null and _close_tween.is_valid():
		_close_tween.kill()
	visible = true
	# 面板打开音：与弹出动画同步；打开瞬间的按钮聚焦在焦点音武装机制下保持安静。
	_sfx.play_open()
	# 焦点直接交给第一个模式按钮：键盘立即可导航，焦点强调效果同步点亮。
	_adventure_button.grab_focus()
	# 面板尺寸要等容器排版后才可靠，延后到本帧布局完成后再播弹出动画。
	_play_show.call_deferred()


func close() -> void:
	if _closing or not visible:
		return
	_closing = true
	# 面板关闭音：返回键、Esc、点压暗层、模式卡确认后统一由此承担收起反馈，
	# 因此"返回"按钮不配按下音，避免两声叠加。
	_sfx.play_close()
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	_panel.pivot_offset = _panel.size / 2.0
	_close_tween = create_tween().set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_CUBIC).set_parallel(true)
	_close_tween.tween_property(_dim, "modulate:a", 0.0, PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_panel, "scale", Vector2(0.9, 0.9), PANEL_CLOSE_DURATION)
	_close_tween.tween_property(_panel, "modulate:a", 0.0, PANEL_CLOSE_DURATION * 0.6)
	_close_tween.chain().tween_callback(_finish_close)


func _finish_close() -> void:
	_closing = false
	visible = false
	_panel.scale = Vector2.ONE
	_panel.modulate.a = 1.0
	closed.emit()


## 选中模式：发出信号后自行关闭；宿主在 closed 后恢复菜单输入、归还焦点并显示提示。
## 关闭动画期间忽略重复点击，避免 mode_selected 发出多次。
func _on_mode_pressed(mode: StringName) -> void:
	if _closing:
		return
	mode_selected.emit(mode)
	close()


## 打开动画：压暗层渐入，内容组从中心轻微放大弹出并交错入场。
func _play_show() -> void:
	if _show_tween != null and _show_tween.is_valid():
		_show_tween.kill()
	if _panel.size.x > 0.0:
		_panel.pivot_offset = _panel.size / 2.0
	_panel.modulate.a = 1.0
	_panel.scale = Vector2.ONE
	_show_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_parallel(true)
	_show_tween.tween_property(_dim, "modulate:a", 1.0, PANEL_POP_DURATION)
	_show_tween.tween_property(_panel, "scale", Vector2.ONE, PANEL_POP_DURATION).from(Vector2(0.85, 0.85))
	_panel.appear_deferred()


func _unhandled_input(event: InputEvent) -> void:
	if not is_visible_in_tree():
		return
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
		return
	# 无焦点时 Tab/Shift+Tab 是键盘操作的唯一入口：引擎的焦点导航没有起点不会生效，
	# 这里手动聚焦面板内末/首个可交互控件（与设置面板同一套处理）。
	if get_viewport().gui_get_focus_owner() != null:
		return
	if event.is_action_pressed("ui_focus_prev"):
		get_viewport().set_input_as_handled()
		_grab_edge_focus(true)
	elif event.is_action_pressed("ui_focus_next"):
		get_viewport().set_input_as_handled()
		_grab_edge_focus(false)


## 聚焦面板内最后一个（from_end）或第一个可交互控件，按场景树顺序，跳过禁用项。
func _grab_edge_focus(from_end: bool) -> void:
	var candidates: Array[Control] = []
	for node: Node in _panel.find_children("*", "Control", true, false):
		var control := node as Control
		if control != null and control.focus_mode == Control.FOCUS_ALL \
				and control.is_visible_in_tree() \
				and not (control is BaseButton and control.disabled):
			candidates.append(control)
	if candidates.is_empty():
		return
	var target: Control = candidates[-1] if from_end else candidates[0]
	target.grab_focus()


func _on_dim_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		close()
