extends Node

## 全局设置存储：负责通用、显示、音频、画质与键位设置的校验、应用和持久化。
## 以 autoload 方式注册（SettingsStore），启动时加载 user://settings.cfg 并应用到引擎。

## 存档里的设置值发生变化、需要重新应用到引擎时发出（键位由 InputBindings 监听）。
signal input_bindings_changed

enum DisplayMode { WINDOWED, FULLSCREEN, BORDERLESS }

## 画质档位（低/中/高）；预设是一键套用抗锯齿的快捷档位。
enum QualityPreset { LOW, MEDIUM, HIGH }

const SETTINGS_PATH := "user://settings.cfg"
const SAVE_DEBOUNCE_SECONDS := 0.5

## 基础档位（16:9 阶梯）；实际可选列表由 get_resolutions() 按当前屏幕动态生成，
## 上限是屏幕原生分辨率——窗口尺寸不应超过屏幕。
const BASE_RESOLUTIONS: Array[Vector2i] = [
	Vector2i(1280, 720),
	Vector2i(1600, 900),
	Vector2i(1920, 1080),
	Vector2i(2560, 1440),
]
const DEFAULT_RESOLUTION := Vector2i(1280, 720)
const DEFAULT_DISPLAY_MODE := DisplayMode.WINDOWED
const DEFAULT_VSYNC_ENABLED := true
const DEFAULT_VOLUMES := {"Master": 100.0, "Music": 100.0, "SFX": 100.0}
## 抗锯齿档位（下拉顺序即 Viewport.MSAA 枚举值）；2D 矢量地图的边缘全靠它收敛锯齿。
const MSAA_LEVELS: Array[int] = [
	Viewport.MSAA_DISABLED, Viewport.MSAA_2X, Viewport.MSAA_4X, Viewport.MSAA_8X,
]
## 画质预设 -> 抗锯齿档位。
const QUALITY_MSAA := {
	QualityPreset.LOW: Viewport.MSAA_DISABLED,
	QualityPreset.MEDIUM: Viewport.MSAA_2X,
	QualityPreset.HIGH: Viewport.MSAA_4X,
}
const DEFAULT_QUALITY_PRESET := QualityPreset.MEDIUM
const DEFAULT_MSAA_2D := Viewport.MSAA_2X
## 语言存档值：空串表示跟随操作系统，其余为 I18n 发现的区域代码。
const DEFAULT_LANGUAGE := ""
## 世界地图样式预设 id（合法值集合由 C# MapStylePresets 决定，
## 未知 id 在消费方回退默认预设，这里只做格式校验）。
const DEFAULT_MAP_STYLE_PRESET := "ghibli"
## 世界地图图层可见性存档格式：逗号分隔的可见图层键。首启默认全可见，
## 键集合与渲染器 TOGGLEABLE_LAYERS 一致，未知键由消费方忽略。
const DEFAULT_MAP_LAYER_VISIBILITY := "ShallowOcean,LandFill,Lake,River,Coastline,Relief,Symbol"

var _display_mode: int = DEFAULT_DISPLAY_MODE
var _resolution: Vector2i = DEFAULT_RESOLUTION
var _vsync_enabled: bool = DEFAULT_VSYNC_ENABLED
var _language: String = DEFAULT_LANGUAGE
var _volumes: Dictionary = DEFAULT_VOLUMES.duplicate()
var _volume_save_scheduled := false
var _map_style_preset: String = DEFAULT_MAP_STYLE_PRESET
var _map_layer_visibility: String = DEFAULT_MAP_LAYER_VISIBILITY
var _quality_preset: int = DEFAULT_QUALITY_PRESET
var _msaa_2d: int = DEFAULT_MSAA_2D
## 动作名 -> 绑定令牌列表。只存玩家改过的动作，缺项表示沿用 project.godot 的默认键位；
## 键位的语义解释与 InputMap 写入由 InputBindings 负责，这里只管格式校验与落盘。
var _input_bindings: Dictionary = {}


## 在对象实例化时立即从磁盘加载配置并设置音频总线，防止主场景节点就绪前音频驱动已输出未静音的缓冲
func _init() -> void:
	_preset_id_regex = RegEx.new()
	_preset_id_regex.compile("^[a-z0-9_]{1,48}$")
	_layer_key_regex = RegEx.new()
	_layer_key_regex.compile("^[A-Za-z]{1,48}$")
	_action_regex = RegEx.new()
	_action_regex.compile("^[a-z0-9_]{1,48}$")
	_load_from_disk()
	apply_audio()


## 进入场景树时再次确保音频配置已正确应用到 AudioServer
func _enter_tree() -> void:
	apply_audio()


func _ready() -> void:
	# 启动就绪时应用语言、显示模式与画质
	_apply_language()
	apply_display()
	apply_audio()
	apply_quality()


func get_language() -> String:
	return _language


func set_language(language: String) -> void:
	_language = _validated_language(language)
	_apply_language()
	_save_to_disk()


## 空串表示跟随系统：显式恢复引擎默认的操作系统区域。
func _apply_language() -> void:
	TranslationServer.set_locale(_language if not _language.is_empty() else OS.get_locale())


func get_display_mode() -> int:
	return _display_mode


func set_display_mode(mode: int) -> void:
	_display_mode = clampi(mode, 0, DisplayMode.size() - 1)
	apply_display()
	_save_to_disk()


## 可选分辨率：基础档位中不超过当前屏幕的部分，再加上屏幕原生分辨率（去重后按像素升序）。
## 屏幕原生分辨率即列表上限，保证窗口模式不会选到超出屏幕的尺寸。
func get_resolutions() -> Array[Vector2i]:
	var resolutions: Array[Vector2i] = []
	var screen_size := DisplayServer.screen_get_size()
	for resolution: Vector2i in BASE_RESOLUTIONS:
		if resolution.x <= screen_size.x and resolution.y <= screen_size.y:
			resolutions.append(resolution)
	if screen_size.x > 0 and screen_size.y > 0:
		if not resolutions.has(screen_size):
			resolutions.append(screen_size)
	elif resolutions.is_empty():
		resolutions.append(DEFAULT_RESOLUTION)  # 屏幕信息不可用（如无头模式）时的兜底
	resolutions.sort_custom(func(a: Vector2i, b: Vector2i) -> bool: return a.x * a.y < b.x * b.y)
	return resolutions


func get_resolution_index() -> int:
	var resolutions := get_resolutions()
	var index := resolutions.find(_resolution)
	if index >= 0:
		return index
	# 屏幕热切换等导致当前值不在列表中时，回退到最接近的档位。
	return _closest_resolution_index(resolutions, _resolution)


func set_resolution_index(index: int) -> void:
	var resolutions := get_resolutions()
	_resolution = resolutions[clampi(index, 0, resolutions.size() - 1)]
	apply_display()
	_save_to_disk()


func get_vsync_enabled() -> bool:
	return _vsync_enabled


func set_vsync_enabled(enabled: bool) -> void:
	_vsync_enabled = enabled
	apply_display()
	_save_to_disk()


func get_volume(bus_name: String) -> float:
	return _volumes[bus_name]


func get_quality_preset() -> int:
	return _quality_preset


## 套用画质档位：预设是一键套用抗锯齿档位，之后仍可单独调抗锯齿。
func set_quality_preset(preset: int) -> void:
	_quality_preset = clampi(preset, 0, QualityPreset.size() - 1)
	_msaa_2d = QUALITY_MSAA[_quality_preset]
	apply_quality()
	_save_to_disk()


func get_msaa_2d() -> int:
	return _msaa_2d


## 直接指定抗锯齿档位：画质预设回填到与当前档位最接近的档位，两者始终自洽。
func set_msaa_2d(level: int) -> void:
	_msaa_2d = level if MSAA_LEVELS.has(level) else DEFAULT_MSAA_2D
	_quality_preset = _preset_for_msaa(_msaa_2d)
	apply_quality()
	_save_to_disk()


## 画质设置落到引擎：2D 抗锯齿写在根视口上，立即生效。
func apply_quality() -> void:
	var viewport := get_viewport()
	if viewport == null:
		return
	viewport.msaa_2d = _msaa_2d as Viewport.MSAA


## 与给定抗锯齿档位最接近（不高于它）的画质预设。
func _preset_for_msaa(level: int) -> int:
	var best := QualityPreset.LOW
	for preset: int in QUALITY_MSAA:
		if level >= int(QUALITY_MSAA[preset]):
			best = preset
	return best


## 动作当前的绑定令牌；空数组表示沿用 project.godot 的默认键位。
func get_input_binding(action: StringName) -> PackedStringArray:
	var tokens: PackedStringArray = _input_bindings.get(String(action), PackedStringArray())
	return tokens


## 覆盖单个动作的绑定；令牌全部非法时忽略本次写入，避免把玩家锁在无效键位上。
func set_input_binding(action: StringName, tokens: PackedStringArray) -> void:
	var validated := _validated_key_tokens(tokens)
	if validated.is_empty():
		push_warning("键位绑定：动作 %s 的令牌全部非法，忽略本次写入" % action)
		return
	_input_bindings[String(action)] = validated
	_save_to_disk()
	input_bindings_changed.emit()


## 清除单个动作的覆盖，恢复为 project.godot 的默认键位。
func clear_input_binding(action: StringName) -> void:
	if not _input_bindings.has(String(action)):
		return
	_input_bindings.erase(String(action))
	_save_to_disk()
	input_bindings_changed.emit()


## 清除全部键位覆盖。
func clear_input_bindings() -> void:
	if _input_bindings.is_empty():
		return
	_input_bindings.clear()
	_save_to_disk()
	input_bindings_changed.emit()


func get_map_style_preset() -> String:
	return _map_style_preset


func set_map_style_preset(id: String) -> void:
	_map_style_preset = _validated_preset_id(id)
	_save_to_disk()


func get_map_layer_visibility() -> String:
	return _map_layer_visibility


func set_map_layer_visibility(csv: String) -> void:
	_map_layer_visibility = _validated_layer_csv(csv)
	_save_to_disk()


func set_volume(bus_name: String, value: float) -> void:
	if not _volumes.has(bus_name):
		push_warning("未知的音频总线：%s" % bus_name)
		return
	_volumes[bus_name] = clampf(value, 0.0, 100.0)
	_apply_volume(bus_name)
	# 拖动滑条会高频触发，写盘按防抖延迟合并。
	_queue_save()


func reset_defaults() -> void:
	_display_mode = DEFAULT_DISPLAY_MODE
	_resolution = _validated_resolution(DEFAULT_RESOLUTION)
	_vsync_enabled = DEFAULT_VSYNC_ENABLED
	_language = DEFAULT_LANGUAGE
	_volumes = DEFAULT_VOLUMES.duplicate()
	_map_style_preset = DEFAULT_MAP_STYLE_PRESET
	_map_layer_visibility = DEFAULT_MAP_LAYER_VISIBILITY
	_quality_preset = DEFAULT_QUALITY_PRESET
	_msaa_2d = DEFAULT_MSAA_2D
	_input_bindings.clear()
	_apply_language()
	apply_display()
	apply_audio()
	apply_quality()
	_save_to_disk()
	# 键位覆盖已清空：通知 InputBindings 把默认键位重新写回 InputMap。
	input_bindings_changed.emit()


func apply_display() -> void:
	match _display_mode:
		DisplayMode.FULLSCREEN:
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, false)
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
		DisplayMode.BORDERLESS:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
			DisplayServer.window_set_size(_resolution)
			_center_window(_resolution)
		_:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, false)
			DisplayServer.window_set_size(_resolution)
			_center_window(_resolution)
	DisplayServer.window_set_vsync_mode(
		DisplayServer.VSYNC_ENABLED if _vsync_enabled else DisplayServer.VSYNC_DISABLED
	)


## 窗口/无边框模式在尺寸变化后于窗口所在屏幕居中；全屏由系统接管，无需定位。
func _center_window(window_size: Vector2i) -> void:
	var screen_index := DisplayServer.window_get_current_screen()
	var screen_position := DisplayServer.screen_get_position(screen_index)
	var screen_size := DisplayServer.screen_get_size(screen_index)
	DisplayServer.window_set_position(screen_position + (screen_size - window_size) / 2)


func apply_audio() -> void:
	for bus_name: String in _volumes:
		_apply_volume(bus_name)


func _apply_volume(bus_name: String) -> void:
	var bus_index := AudioServer.get_bus_index(bus_name)
	if bus_index < 0:
		push_warning("音频总线不存在：%s" % bus_name)
		return
	var linear: float = _volumes[bus_name] / 100.0
	var is_muted := linear <= 0.0
	# 设置总线静音状态
	AudioServer.set_bus_mute(bus_index, is_muted)
	# 若静音则将音量分贝彻底置为 -80.0 dB（完全消除采样），未静音时按线性换算分贝
	var volume_db := -80.0 if is_muted else linear_to_db(maxf(linear, 0.001))
	AudioServer.set_bus_volume_db(bus_index, volume_db)


func _queue_save() -> void:
	if _volume_save_scheduled:
		return
	_volume_save_scheduled = true
	get_tree().create_timer(SAVE_DEBOUNCE_SECONDS).timeout.connect(_on_save_debounce_timeout)


func _on_save_debounce_timeout() -> void:
	_volume_save_scheduled = false
	_save_to_disk()


func _load_from_disk() -> void:
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) != OK:
		return  # 首次运行或文件不可读：保持默认值
	_display_mode = _validated_int(
		config.get_value("display", "mode", DEFAULT_DISPLAY_MODE),
		DEFAULT_DISPLAY_MODE, 0, DisplayMode.size() - 1)
	_resolution = _validated_resolution(config.get_value("display", "resolution", DEFAULT_RESOLUTION))
	_vsync_enabled = _validated_bool(
		config.get_value("display", "vsync", DEFAULT_VSYNC_ENABLED),
		DEFAULT_VSYNC_ENABLED)
	_language = _validated_language(config.get_value("general", "language", DEFAULT_LANGUAGE))
	for bus_name: String in DEFAULT_VOLUMES:
		_volumes[bus_name] = _validated_float(
			config.get_value("audio", bus_name.to_lower(), DEFAULT_VOLUMES[bus_name]),
			DEFAULT_VOLUMES[bus_name], 0.0, 100.0)
	_map_style_preset = _validated_preset_id(
		config.get_value("map", "style_preset", DEFAULT_MAP_STYLE_PRESET))
	_map_layer_visibility = _validated_layer_csv(
		config.get_value("map", "layer_visibility", DEFAULT_MAP_LAYER_VISIBILITY))
	_quality_preset = _validated_int(
		config.get_value("quality", "preset", DEFAULT_QUALITY_PRESET),
		DEFAULT_QUALITY_PRESET, 0, QualityPreset.size() - 1)
	_msaa_2d = _validated_int(
		config.get_value("quality", "msaa_2d", DEFAULT_MSAA_2D),
		DEFAULT_MSAA_2D, Viewport.MSAA_DISABLED, Viewport.MSAA_8X)
	if not MSAA_LEVELS.has(_msaa_2d):
		_msaa_2d = DEFAULT_MSAA_2D
	_input_bindings = _validated_input_bindings(
		config.get_value("input", "bindings", {}))


func _save_to_disk() -> void:
	var config := ConfigFile.new()
	config.set_value("display", "mode", _display_mode)
	config.set_value("display", "resolution", _resolution)
	config.set_value("display", "vsync", _vsync_enabled)
	config.set_value("general", "language", _language)
	for bus_name: String in _volumes:
		config.set_value("audio", bus_name.to_lower(), _volumes[bus_name])
	config.set_value("map", "style_preset", _map_style_preset)
	config.set_value("map", "layer_visibility", _map_layer_visibility)
	config.set_value("quality", "preset", _quality_preset)
	config.set_value("quality", "msaa_2d", _msaa_2d)
	config.set_value("input", "bindings", _input_bindings)
	if config.save(SETTINGS_PATH) != OK:
		push_warning("设置保存失败：%s" % SETTINGS_PATH)


func _validated_int(value: Variant, fallback: int, min_value: int, max_value: int) -> int:
	if value is int and value >= min_value and value <= max_value:
		return value
	return fallback


## 分辨率按值持久化；兼容旧版的档位索引格式，并把结果收敛到当前屏幕的可选列表
## （例如换过显示器后存档分辨率超出屏幕，取最接近的可用档位）。
func _validated_resolution(value: Variant) -> Vector2i:
	var requested := DEFAULT_RESOLUTION
	if value is Vector2i:
		requested = value
	elif value is int and value >= 0 and value < BASE_RESOLUTIONS.size():
		requested = BASE_RESOLUTIONS[value]
	var resolutions := get_resolutions()
	if resolutions.has(requested):
		return requested
	return resolutions[_closest_resolution_index(resolutions, requested)]


func _closest_resolution_index(resolutions: Array[Vector2i], requested: Vector2i) -> int:
	var closest_index := 0
	var closest_diff := INT32_MAX
	for i in resolutions.size():
		var resolution := resolutions[i]
		var diff := absi(resolution.x * resolution.y - requested.x * requested.y)
		if diff < closest_diff:
			closest_diff = diff
			closest_index = i
	return closest_index


func _validated_bool(value: Variant, fallback: bool) -> bool:
	if value is bool:
		return value
	return fallback


func _validated_float(value: Variant, fallback: float, min_value: float, max_value: float) -> float:
	if value is float or value is int:
		var as_float := float(value)
		if as_float >= min_value and as_float <= max_value:
			return as_float
	return fallback


func _validated_language(value: Variant) -> String:
	if value is String and (value.is_empty() or I18n.has_locale(value)):
		return value
	return DEFAULT_LANGUAGE


## 预设 id 与图层键的格式约束：合法值集合由 C# 注册表/渲染器决定，且消费方对
## 未知 id 本来就回退默认，这里只用正则收紧格式防脏数据入库。
var _preset_id_regex: RegEx
var _layer_key_regex: RegEx
var _action_regex: RegEx


## 键位绑定令牌的合法性：按键令牌必须是引擎认识的键名（"Escape"/"E"），
## 鼠标令牌必须是 "Mouse<1..9>"。用引擎自身的解析结果判定，不维护第二份键名表。
func _is_valid_key_token(token: String) -> bool:
	if token.begins_with("Mouse"):
		var index := token.substr(5)
		return index.is_valid_int() and int(index) >= MOUSE_BUTTON_LEFT \
			and int(index) <= MOUSE_BUTTON_XBUTTON2
	return OS.find_keycode_from_string(token) != KEY_NONE


func _validated_key_tokens(value: Variant) -> PackedStringArray:
	var tokens := PackedStringArray()
	if not (value is PackedStringArray or value is Array):
		return tokens
	for raw: Variant in value:
		var token := str(raw).strip_edges()
		if _is_valid_key_token(token):
			tokens.append(token)
	return tokens


## 键位存档：动作名 -> 令牌列表。动作名只校验格式（合法动作集合由 InputBindings
## 注册表决定，未知动作在应用阶段被忽略），令牌逐个校验后重组。
func _validated_input_bindings(value: Variant) -> Dictionary:
	var result := {}
	if not (value is Dictionary):
		return result
	for action: Variant in value:
		var action_name := str(action).strip_edges()
		if _action_regex.search(action_name) == null:
			continue
		var tokens := _validated_key_tokens(value[action])
		if not tokens.is_empty():
			result[action_name] = tokens
	return result


## 预设 id 只约束格式（小写字母/数字/下划线）。
func _validated_preset_id(value: Variant) -> String:
	if value is String and _preset_id_regex.search(value) != null:
		return value
	return DEFAULT_MAP_STYLE_PRESET


## 图层可见性 CSV：逐 token 校验后重组，压缩空段与脏字符。
func _validated_layer_csv(value: Variant) -> String:
	if not (value is String):
		return DEFAULT_MAP_LAYER_VISIBILITY
	var tokens: Array[String] = []
	for token in (value as String).split(",", false):
		var clean := str(token).strip_edges()
		if _layer_key_regex.search(clean) != null:
			tokens.append(clean)
	return ",".join(tokens)
