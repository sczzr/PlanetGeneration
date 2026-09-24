extends Control

## 主菜单界面。只负责展示与输入反馈，实际流程由外部监听信号后驱动。
## 按钮列由 Menu FX 插件的 MenuList 组件按 items 配置生成，本脚本按 id 分发点击。

## mode：ModeSelectMenu 的模式标识；config：守卫模式"创建世界"的配置汇总（冒险模式为空）。
signal new_game_requested(mode: StringName, config: Dictionary)
signal load_game_requested
signal settings_requested
signal mods_requested
signal back_to_game_requested

const MenuList := preload("res://addons/menu_fx/scripts/menu_list.gd")

## 菜单项 id：与 main_menu.tscn 中 MenuList 的 items 配置一一对应。
const ID_START := &"StartButton"
const ID_LOAD := &"LoadButton"
const ID_SETTINGS := &"SettingsButton"
const ID_MODS := &"ModsButton"
const ID_QUIT := &"QuitButton"

@onready var _notice: Label = %Notice
@onready var _notice_fx: AutoTween = %NoticeFx
@onready var _menu_list: MenuList = %Buttons
@onready var _settings_menu: SettingsMenu = %SettingsMenu
@onready var _mode_select_menu: ModeSelectMenu = %ModeSelectMenu
@onready var _world_config_menu: WorldConfigMenu = %WorldConfigMenu
@onready var _loading_overlay: LoadingOverlay = %LoadingOverlay
@onready var _column: VBoxContainer = %Column

## 主菜单列淡出/淡入时长。
const COLUMN_FADE_DURATION := 0.15

## 当前提示的翻译 key 与格式化参数；设置面板关闭后可能切换了语言，需要按新语言重取文案。
var _notice_key := ""
var _notice_args: Array = []

## 选择模式后暂存的提示 key；等覆盖层关闭动画结束再显示，避免提示被压暗层挡住。
var _pending_notice_key := ""

## 确认"生成世界"后暂存：等向导关闭动画结束再弹出加载遮罩（生成已由流程层启动）。
var _pending_loading := false

## 选了守卫模式：等模式选择关闭动画结束后转开"创建世界"向导（守卫模式第一级页面）。
var _pending_world_config := false
var _from_in_game := false

## 创建世界向导是否以"生成世界"确认关闭：true 时回主菜单，false 时回模式选择。
var _world_config_confirmed := false

## 主菜单列淡出/淡入补间，打开期间重复触发时先杀旧补间。
var _column_tween: Tween


func _ready() -> void:
	_menu_list.item_activated.connect(_on_item_activated)
	_settings_menu.closed.connect(_on_settings_closed)
	_mode_select_menu.mode_selected.connect(_on_mode_selected)
	_mode_select_menu.closed.connect(_on_mode_select_closed)
	_world_config_menu.confirmed.connect(_on_world_config_confirmed)
	_world_config_menu.closed.connect(_on_world_config_closed)
	_loading_overlay.closed.connect(_on_loading_overlay_closed)
	_notice.text = ""
	# 初始焦点由 MenuList 自动交给第一个可用项（开始游戏）。
	# 启动主菜单背景音乐：SettingsStore 已在 _init() 与 _enter_tree() 应用静音设置，
	# 此时受控播放不会产生任何初始漏音或爆音。
	if has_node("Music"):
		$Music.play()


## 菜单项点击分发。id 来自 MenuList 的 items 配置，与上方 ID_* 常量对应。
func _on_item_activated(id: StringName) -> void:
	if id == ID_START:
		# 打开模式选择覆盖层；期间禁用菜单按钮，避免键盘焦点逃出覆盖层。
		# 覆盖层没有大面积面板背景，主菜单列同步淡出让位，关闭时淡入恢复。
		_menu_list.set_input_enabled(false)
		_set_column_shown(false)
		_mode_select_menu.open()
	elif id == ID_LOAD:
		load_game_requested.emit()
		_show_notice("NOTICE_LOAD_NOT_IMPLEMENTED")
	elif id == ID_SETTINGS:
		settings_requested.emit()
		# 覆盖层打开期间禁用菜单按钮，避免键盘焦点逃出设置面板。
		_menu_list.set_input_enabled(false)
		_settings_menu.open()
	elif id == ID_MODS:
		mods_requested.emit()
		_show_notice("NOTICE_MODS_NOT_IMPLEMENTED")
	elif id == ID_QUIT:
		# 稍候再退出：让按下的确认音完整播出，进程立即结束会截断声音产生爆音。
		await get_tree().create_timer(0.2).timeout
		get_tree().quit()
	else:
		push_warning("主菜单：未处理的菜单项 id %s" % id)


func _on_settings_closed() -> void:
	_menu_list.set_input_enabled(true)
	if not _notice_key.is_empty():
		_notice.text = _format_notice(_notice_key, _notice_args)
	# 焦点归还到"设置"项：焦点态强调效果会随之重新展开。
	_menu_list.focus_item(ID_SETTINGS)


## 玩家选定模式：冒险模式直接转发开局请求并暂存提示；守卫模式在模式选择关闭后
## 转开"创建世界"向导，等配置完成（生成世界）才真正发出开局请求。
func _on_mode_selected(mode: StringName) -> void:
	if mode == ModeSelectMenu.MODE_GUARD:
		_pending_world_config = true
		return
	new_game_requested.emit(mode, {})
	_pending_notice_key = "NOTICE_ADVENTURE_NOT_IMPLEMENTED"


func _on_mode_select_closed() -> void:
	if _pending_world_config:
		# 守卫模式：菜单列保持隐藏、输入保持禁用，无缝进入创建世界向导。
		_pending_world_config = false
		_world_config_menu.open()
		return
	_restore_menu_column()
	if not _pending_notice_key.is_empty():
		_show_notice(_pending_notice_key)
		_pending_notice_key = ""


## 玩家在向导里点了"生成世界"：携带配置转发开局请求（流程层启动世界生成），
## 暂存加载遮罩等向导关闭后弹出；若流程层拒绝（忙碌）会在信号同步回调里取消。
func _on_world_config_confirmed(config: Dictionary) -> void:
	_pending_loading = true
	new_game_requested.emit(ModeSelectMenu.MODE_GUARD, config)
	_world_config_confirmed = true


## 向导关闭：确认过生成则回主菜单并弹出全屏加载遮罩（结果由流程层随后回报）；
## 中途放弃则重新打开模式选择，让玩家换一个模式（或从游戏内打开时直接返回游戏）。
func _on_world_config_closed() -> void:
	if not _world_config_confirmed:
		if _from_in_game:
			_from_in_game = false
			visible = false
			back_to_game_requested.emit()
			return
		_mode_select_menu.open()
		return
	_world_config_confirmed = false
	_from_in_game = false
	_restore_menu_column()
	if _pending_loading:
		# 生成已启动：遮罩期间禁用菜单输入，防止键盘焦点逃到遮罩后的按钮。
		_pending_loading = false
		_menu_list.set_input_enabled(false)
		_loading_overlay.open()


## 外部（如游戏内控制台或快捷键）直接拉起创建世界向导
func open_world_config(from_in_game: bool = false) -> void:
	_from_in_game = from_in_game
	_world_config_confirmed = false
	_pending_loading = false
	visible = true
	_menu_list.set_input_enabled(false)
	_set_column_shown(false)
	_world_config_menu.open()


## 覆盖层流程结束：恢复菜单列输入与显示，焦点归还"开始游戏"。
## （焦点态强调效果会随之重新展开。）
func _restore_menu_column() -> void:
	_menu_list.set_input_enabled(true)
	_set_column_shown(true)
	_menu_list.focus_item(ID_START)


## 主菜单列淡出/淡入：模式选择覆盖层没有大面积面板背景，打开时整列让位，
## 关闭时恢复。只动透明度不改布局，提示条随列一起出现。
func _set_column_shown(shown: bool) -> void:
	if _column_tween != null and _column_tween.is_valid():
		_column_tween.kill()
	_column_tween = create_tween()
	if shown:
		_column_tween.set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	else:
		_column_tween.set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_CUBIC)
	_column_tween.tween_property(_column, "modulate:a", 1.0 if shown else 0.0, COLUMN_FADE_DURATION)


## 在菜单下方显示一条会自动淡出的提示，用于反馈尚未实现的入口与世界生成状态。
## 弹出→停留→淡出的三段时序由 NoticeFx（AutoTween）驱动，这里只负责内容与触发。
## 参数是翻译 key 与可选格式化参数，实际文案按当前语言取词。
func _show_notice(message_key: String, args: Array = []) -> void:
	_notice_key = message_key
	_notice_args = args
	_notice.text = _format_notice(message_key, args)
	_notice_fx.popup()


## 流程层（main.gd）反馈世界生成状态用的公开入口，与内部提示同一套展示逻辑。
func show_notice(message_key: String, args: Array = []) -> void:
	_show_notice(message_key, args)


## 流程层反馈：开局请求被拒（生成器忙碌）时取消加载遮罩。
## 在 new_game_requested 信号的同步回调链里调用，因此能赶在向导关闭前清除暂存。
func cancel_loading() -> void:
	_pending_loading = false
	_loading_overlay.close()


## 流程层反馈：世界生成结束（无论成败）后关闭加载遮罩；未开遮罩时安全空操作。
func end_loading() -> void:
	_pending_loading = false
	_loading_overlay.close()


## 流程层反馈：转发世界生成进度（0~1）给加载遮罩，驱动加载动画逐瓣填充叶子。
func set_loading_progress(progress: float) -> void:
	if _loading_overlay.is_open():
		_loading_overlay.set_progress(progress)


## 加载遮罩关闭后归还菜单输入与焦点（生成流程结束，回主菜单待命）。
func _on_loading_overlay_closed() -> void:
	_menu_list.set_input_enabled(true)
	_menu_list.focus_item(ID_START)


## 按当前语言取词并可选地带参格式化。
func _format_notice(message_key: String, args: Array) -> String:
	var text := tr(message_key)
	return text % args if not args.is_empty() else text
