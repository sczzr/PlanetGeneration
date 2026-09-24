extends Node

## 键位绑定注册表与运行时输入映射（autoload: InputBindings）。
##
## 默认键位的唯一来源是 project.godot 的 [input] 段（在编辑器的输入映射里可直接
## 查看与调整）；本模块启动时先把引擎里的默认事件快照下来作为「恢复默认」的目标，
## 再把 SettingsStore 存的玩家覆盖写回 InputMap。注册表只登记「哪些动作允许玩家
## 改」与对应的文案 key，不另存一份默认值，避免两处默认值各自漂移。
##
## 存储格式：每个动作一条字符串令牌列表。按键用引擎键名（"Escape"/"E"），
## 鼠标键用 "Mouse<N>"（N 为 MouseButton 枚举值）；令牌的合法性由 SettingsStore 校验。

## 运行时输入映射已按存档重建；键位面板据此刷新。
signal bindings_changed

## 允许玩家重绑的动作；数组顺序即键位面板自上而下的排列顺序。
const ACTIONS: Array[Dictionary] = [
	{"action": &"game_pause", "label_key": "KEYBIND_ACTION_GAME_PAUSE"},
	{"action": &"barrier_span_increase", "label_key": "KEYBIND_ACTION_BARRIER_INCREASE"},
	{"action": &"barrier_span_decrease", "label_key": "KEYBIND_ACTION_BARRIER_DECREASE"},
	{"action": &"placement_cancel", "label_key": "KEYBIND_ACTION_PLACEMENT_CANCEL"},
]

## 鼠标键令牌前缀。
const MOUSE_TOKEN_PREFIX := "Mouse"
## 常用鼠标键 -> 文案 key（表外的鼠标键回落到带序号的通用文案）。
const MOUSE_LABEL_KEYS := {
	MOUSE_BUTTON_LEFT: "KEYBIND_KEY_MOUSE_LEFT",
	MOUSE_BUTTON_RIGHT: "KEYBIND_KEY_MOUSE_RIGHT",
	MOUSE_BUTTON_MIDDLE: "KEYBIND_KEY_MOUSE_MIDDLE",
}
## 引擎键名 -> 文案 key。字母/数字/符号键本身即键面文字，无需翻译，
## 因此只登记按键面文字看不出是什么键的特殊键；表外的键直接显示引擎键名。
const KEY_LABEL_KEYS := {
	"Escape": "KEYBIND_KEY_ESCAPE",
	"Space": "KEYBIND_KEY_SPACE",
	"Enter": "KEYBIND_KEY_ENTER",
	"Tab": "KEYBIND_KEY_TAB",
	"Backspace": "KEYBIND_KEY_BACKSPACE",
	"Delete": "KEYBIND_KEY_DELETE",
	"Insert": "KEYBIND_KEY_INSERT",
	"Shift": "KEYBIND_KEY_SHIFT",
	"Ctrl": "KEYBIND_KEY_CTRL",
	"Alt": "KEYBIND_KEY_ALT",
	"Home": "KEYBIND_KEY_HOME",
	"End": "KEYBIND_KEY_END",
	"Up": "KEYBIND_KEY_UP",
	"Down": "KEYBIND_KEY_DOWN",
	"Left": "KEYBIND_KEY_LEFT",
	"Right": "KEYBIND_KEY_RIGHT",
	"BracketLeft": "KEYBIND_KEY_BRACKET_LEFT",
	"BracketRight": "KEYBIND_KEY_BRACKET_RIGHT",
}

## 动作 -> 引擎默认事件（启动时快照；InputEvent 是资源，应用前一律深拷贝）。
var _defaults: Dictionary = {}


func _ready() -> void:
	_snapshot_defaults()
	# 存档变更（含设置页的「恢复默认设置」）统一经信号回流，保证 InputMap 与存档一致。
	SettingsStore.input_bindings_changed.connect(_reload_from_store)
	_reload_from_store()


## 允许玩家重绑的动作注册表（每项含 action 与 label_key）。
func get_actions() -> Array[Dictionary]:
	return ACTIONS


func is_rebindable(action: StringName) -> bool:
	for entry: Dictionary in ACTIONS:
		if entry["action"] == action:
			return true
	return false


## 当前生效的绑定令牌（按 InputMap 里的实际顺序）。
func get_binding_tokens(action: StringName) -> PackedStringArray:
	var tokens := PackedStringArray()
	if not InputMap.has_action(action):
		return tokens
	for event: InputEvent in InputMap.action_get_events(action):
		var token := token_from_event(event)
		if not token.is_empty():
			tokens.append(token)
	return tokens


## 当前绑定的人类可读文案（多个按键以 " / " 连接）。
func get_binding_text(action: StringName) -> String:
	var labels: Array[String] = []
	for token in get_binding_tokens(action):
		labels.append(describe_token(token))
	return " / ".join(labels)


## 该动作是否仍是引擎默认绑定。
func is_default(action: StringName) -> bool:
	return get_binding_tokens(action) == _default_tokens(action)


## 重绑：把动作的绑定整体替换为该事件（单个按键）。返回是否成功。
## 失败原因（不可重绑、事件不支持、与其它动作冲突）由 find_conflict 与调用方共同呈现。
func rebind(action: StringName, event: InputEvent) -> bool:
	if not is_rebindable(action):
		return false
	var token := token_from_event(event)
	if token.is_empty():
		return false
	if not find_conflict(action, event).is_empty():
		return false
	SettingsStore.set_input_binding(action, PackedStringArray([token]))
	return true


## 恢复单个动作的默认绑定。
func reset_action(action: StringName) -> void:
	if not is_rebindable(action):
		return
	SettingsStore.clear_input_binding(action)


## 恢复全部动作的默认绑定。
func reset_all() -> void:
	SettingsStore.clear_input_bindings()


## 查找与给定事件冲突的动作：先看其它可重绑动作，再看引擎内置的 ui_* 动作。
## 返回空 StringName 表示无冲突。Esc 是暂停键的默认值，与 ui_cancel 的重合是
## 设计内行为（暂停菜单对两者都响应），因此 game_pause 不参与 ui_* 冲突判定。
func find_conflict(action: StringName, event: InputEvent) -> StringName:
	var token := token_from_event(event)
	if token.is_empty():
		return &""
	for entry: Dictionary in ACTIONS:
		var other: StringName = entry["action"]
		if other != action and get_binding_tokens(other).has(token):
			return other
	if action != &"game_pause":
		return _builtin_conflict(event)
	return &""


## 事件 -> 存储令牌；不支持的事件返回空串。
func token_from_event(event: InputEvent) -> String:
	if event is InputEventKey:
		var key := event as InputEventKey
		var code: Key = key.physical_keycode if key.physical_keycode != KEY_NONE else key.keycode
		return "" if code == KEY_NONE else OS.get_keycode_string(code)
	if event is InputEventMouseButton:
		return "%s%d" % [MOUSE_TOKEN_PREFIX, (event as InputEventMouseButton).button_index]
	return ""


## 存储令牌 -> 事件；令牌非法时返回 null。
func event_from_token(token: String) -> InputEvent:
	if token.begins_with(MOUSE_TOKEN_PREFIX):
		var index := token.substr(MOUSE_TOKEN_PREFIX.length())
		if not index.is_valid_int():
			return null
		var button := InputEventMouseButton.new()
		button.button_index = int(index) as MouseButton
		return button
	var code := OS.find_keycode_from_string(token)
	if code == KEY_NONE:
		return null
	var key := InputEventKey.new()
	key.physical_keycode = code
	return key


## 令牌 -> 展示文案（鼠标键与特殊键走本地化，其余直接用键面文字）。
func describe_token(token: String) -> String:
	if token.begins_with(MOUSE_TOKEN_PREFIX):
		var index := token.substr(MOUSE_TOKEN_PREFIX.length())
		if not index.is_valid_int():
			return token
		var label_key: String = MOUSE_LABEL_KEYS.get(int(index), "")
		if not label_key.is_empty():
			return tr(label_key)
		return tr("KEYBIND_KEY_MOUSE_OTHER") % int(index)
	var key_label: String = KEY_LABEL_KEYS.get(token, "")
	return tr(key_label) if not key_label.is_empty() else token


# ------------------------------------------------------------------ 内部

## 快照引擎默认事件：必须在任何覆盖写入之前执行（_ready 首行）。
func _snapshot_defaults() -> void:
	for entry: Dictionary in ACTIONS:
		var action: StringName = entry["action"]
		if not InputMap.has_action(action):
			push_error("InputBindings：project.godot 的 [input] 段缺少动作 %s" % action)
			continue
		_defaults[action] = InputMap.action_get_events(action)


func _default_events(action: StringName) -> Array[InputEvent]:
	var events: Array[InputEvent] = []
	for event: InputEvent in _defaults.get(action, []):
		events.append(event.duplicate(true))
	return events


func _default_tokens(action: StringName) -> PackedStringArray:
	var tokens := PackedStringArray()
	for event: InputEvent in _default_events(action):
		var token := token_from_event(event)
		if not token.is_empty():
			tokens.append(token)
	return tokens


func _reload_from_store() -> void:
	for entry: Dictionary in ACTIONS:
		var action: StringName = entry["action"]
		if not InputMap.has_action(action):
			continue
		_apply_action(action)
	bindings_changed.emit()


## 把单个动作的生效绑定写回 InputMap：有存档用存档，没有则用默认。
## 存档令牌全部非法时保持默认，避免玩家把自己锁在无法操作的键位上。
func _apply_action(action: StringName) -> void:
	var stored := SettingsStore.get_input_binding(action)
	var events: Array[InputEvent] = []
	if stored.is_empty():
		events = _default_events(action)
	else:
		for token in stored:
			var event := event_from_token(token)
			if event != null:
				events.append(event)
	if events.is_empty():
		events = _default_events(action)
	InputMap.action_erase_events(action)
	for event: InputEvent in events:
		InputMap.action_add_event(action, event)


## 内置 ui_* 动作占用了同一事件时返回该动作名（菜单导航键不应被游戏动作抢占）。
func _builtin_conflict(event: InputEvent) -> StringName:
	for action: StringName in InputMap.get_actions():
		if not str(action).begins_with("ui_"):
			continue
		if InputMap.action_has_event(action, event):
			return action
	return &""
