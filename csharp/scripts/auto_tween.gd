extends Node
class_name AutoTween

## 显隐自动补间（教程 AutoTween 思路落地）：作为宿主 Control 的子节点独立驱动动画，
## 宿主代码零改动——VISIBLE 模式监听宿主显隐信号自动播放，MANUAL 模式由宿主调 popup()。
## 可选"弹出后停留 N 秒自动淡出"，适合提示通知、游戏内弹出的暂停菜单和奖励面板。

enum Trigger { READY, VISIBLE, MANUAL }

## 触发时机：就绪后自动播放 / 宿主变为可见时播放 / 由宿主脚本调用 popup()。
## 注意：弹出幅度由"起始缩放"(from_scale) 控制；本组件没有"放大强度"参数，那是按钮焦点放大效果的配置。
@export_enum("就绪自动播放", "可见时自动播放", "手动调用 popup()") var trigger: int = Trigger.VISIBLE
## 弹出起始延迟；VISIBLE/READY 模式下先等一帧让布局稳定，再叠加此延迟。
@export var start_delay := 0.0
## 弹出后停留秒数，随后自动淡出；0 表示一直停留。
@export var auto_hide_after := 0.0
## 弹出缩放补间时长（秒）。
@export var pop_duration := 0.2
## 面板类显隐按缓动基准表改用 TRANS_CUBIC + EASE_IN_OUT；提示弹出保持 BACK+EASE_OUT 主力。
@export var pop_trans: Tween.TransitionType = Tween.TRANS_BACK
## 弹出缩放的缓动方式（先快后慢等）。
@export var pop_ease: Tween.EaseType = Tween.EASE_OUT
## 淡出时长（秒）。
@export var hide_fade_duration := 0.15
## 淡出结束后是否把宿主设为不可见；纯提示类保持可见、仅透明度归零，避免布局塌陷。
@export var hide_node := false
## Force From：无论是否正在播放，都从起始缩放重播；关闭则从当前值接续。
@export var force_from := true
## 弹出起始缩放（越小弹出越明显，1.0 表示不缩放）：Force From 时先回到此值再弹到 1.0。
@export var from_scale := Vector2(0.85, 0.85)

var _host: Control
var _tween: Tween


func _ready() -> void:
	_host = get_parent() as Control
	if _host == null:
		push_warning("AutoTween 的父节点必须是 Control：%s" % get_parent().name)
		return
	_center_pivot()
	_host.resized.connect(_center_pivot)
	# 入场前预隐藏，避免以原始状态闪现。
	_host.modulate.a = 0.0
	# 引擎只提供 hidden 与 visibility_changed 信号，"变为可见"需自行比对状态。
	_host.visibility_changed.connect(_on_host_visibility_changed)
	if trigger == Trigger.READY or (trigger == Trigger.VISIBLE and _host.visible):
		popup.call_deferred()


## 弹出：Force From 时先回到起始缩放，再回弹到 1.0；
## auto_hide_after > 0 时弹出后停留、淡出，最后视 hide_node 决定是否隐藏宿主。
func popup() -> void:
	if _host == null:
		return
	# 等一帧：刚改变可见性或文本时容器会重新排版，立即取 pivot 会拿到旧值。
	await get_tree().process_frame
	if _host == null:
		return
	if _tween != null and _tween.is_valid():
		_tween.kill()
	if force_from:
		_center_pivot()
		_host.scale = from_scale
	_host.modulate.a = 1.0
	_tween = create_tween().set_trans(pop_trans).set_ease(pop_ease)
	_tween.tween_property(_host, "scale", Vector2.ONE, pop_duration).set_delay(start_delay)
	if auto_hide_after > 0.0:
		_tween.tween_interval(auto_hide_after)
		_tween.tween_property(_host, "modulate:a", 0.0, hide_fade_duration)
		if hide_node:
			_tween.tween_callback(_hide_host)


func _on_host_visibility_changed() -> void:
	if _host.visible:
		if trigger == Trigger.VISIBLE:
			popup()
	else:
		# 宿主被外部隐藏时停掉补间并复位缩放，避免下次显示带着旧状态。
		if _tween != null and _tween.is_valid():
			_tween.kill()
		_host.scale = Vector2.ONE


func _hide_host() -> void:
	_host.visible = false
	# 透明度复位，下次显示不依赖外部初始化。
	_host.modulate.a = 1.0


func _center_pivot() -> void:
	_host.pivot_offset = _host.size / 2.0
