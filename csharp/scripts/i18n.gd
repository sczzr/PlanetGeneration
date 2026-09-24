extends Node

## 国际化：启动时加载 assets/locale 下的 JSON 翻译文件并注册到 TranslationServer。
## 文件名（不含扩展名）即区域代码，如 zh_CN.json 对应 zh_CN；
## 翻译 key 为大写下划线英文，场景文本与代码统一通过 tr() 取词。

const LOCALE_DIR := "res://assets/locale"

## 语言的原生显示名（设置界面语言下拉用）；按国际惯例始终以该语言自身书写，不参与翻译。
const NATIVE_LANGUAGE_NAMES := {
	"zh_CN": "中文",
	"en_US": "English",
}

var _locales: PackedStringArray = []


func _ready() -> void:
	_load_translations()


## 发现的可用语言列表（按区域代码排序）。
func get_locales() -> PackedStringArray:
	return _locales


func has_locale(locale: String) -> bool:
	return _locales.has(locale)


func get_native_language_name(locale: String) -> String:
	return NATIVE_LANGUAGE_NAMES.get(locale, locale)


func _load_translations() -> void:
	var dir := DirAccess.open(LOCALE_DIR)
	if dir == null:
		push_error("翻译目录不存在：%s" % LOCALE_DIR)
		return
	for file_name: String in dir.get_files():
		if file_name.ends_with(".json"):
			_load_locale_file(file_name)
	_locales.sort()


func _load_locale_file(file_name: String) -> void:
	var path := "%s/%s" % [LOCALE_DIR, file_name]
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("翻译文件无法读取：%s" % path)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not (parsed is Dictionary):
		push_error("翻译文件不是有效的 JSON 对象：%s" % path)
		return
	var translation := Translation.new()
	translation.locale = file_name.get_basename()
	for key: String in parsed:
		var message: Variant = parsed[key]
		if message is String:
			translation.add_message(key, message)
		else:
			push_error("翻译值必须是字符串：%s 的 %s" % [path, key])
	TranslationServer.add_translation(translation)
	_locales.append(translation.locale)
