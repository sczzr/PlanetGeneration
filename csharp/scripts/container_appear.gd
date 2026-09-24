extends Container
class_name ContainerAppear
## 容器子元素交错入场动画：挂到任意 Container 上，入场时逐个以
## TRANS_BACK + EASE_OUT 把子元素缩放或滑入到位，元素之间保留少量
## 重叠延迟，避免所有元素同时移动而显得呆板。
## anim_when 为 READY 时场景就绪后自动播放；MANUAL 供宿主脚本调用 appear()。
##
## 滑入只动画子节点的 position.x，不碰 y：容器（含隐藏期间）的排版是异步的，
## 播放动画时子节点可能还停在原点；若把整个 position 当目标写回，排版结果会被
## 补间逐帧覆盖，子节点将叠在原点不动（见 tests/Gameplay/settings_menu_smoke.gd）。

enum AnimWhen { READY, MANUAL }
enum AnimType { SCALE, SLIDE_IN_LEFT, SLIDE_IN_RIGHT }

## 子元素在容器排版后的静止横向位置。挂载本组件的节点都是 VBoxContainer，
## 它把子节点的 x 一律置 0、只按顺序分配 y，因此滑入动画只需动画 x 分量。
const REST_POSITION_X := 0.0

## 播放时机：场景就绪后自动播放，或由宿主脚本调用 appear()。
@export_enum("就绪自动播放", "手动调用 appear()") var anim_when: int = AnimWhen.READY
## 入场方式：以中心缩放进入、从左侧滑入、从右侧滑入。
@export_enum("缩放", "左滑入", "右滑入") var anim_type: int = AnimType.SLIDE_IN_LEFT
## 单个元素的入场时长（秒）。
@export var duration := 0.3
## 整列入场的起始延迟（秒），留给上层元素先动。
@export var delay_appear := 0.05
## 相邻元素之间的重叠延迟（秒），逐个递增形成交错。
@export var delay_between_elements := 0.06
## 滑入类动画的起始偏移量（像素）。
@export var slide_offset := 48.0
## 需要从底部开始入场时开启：内部反转子节点数组，无需手动调整节点顺序。
@export var reverse_order := false

var _tween: Tween


func _ready() -> void:
	# 先把子元素置为全透明：入场前它们不能以原始状态闪现。
	_set_children_alpha(0.0)
	if anim_when == AnimWhen.READY:
		appear_deferred()


## 延迟一帧再入场：刚显示时容器会重新排版子节点，等布局稳定后才能取到正确位置与尺寸。
func appear_deferred() -> void:
	await get_tree().process_frame
	appear()


func appear() -> void:
	if _tween != null and _tween.is_valid():
		_tween.kill()
	var children := _animatable_children()
	if children.is_empty():
		return
	_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_parallel(true)
	for i in children.size():
		var child := children[i] as Control
		var delay := delay_appear + delay_between_elements * i
		child.modulate.a = 0.0
		match anim_type:
			AnimType.SCALE:
				child.pivot_offset = child.size / 2.0
				_tween.tween_property(child, "scale", Vector2.ONE, duration).from(Vector2.ZERO).set_delay(delay)
			AnimType.SLIDE_IN_LEFT:
				_tween.tween_property(child, "position:x", REST_POSITION_X, duration) \
					.from(REST_POSITION_X - slide_offset).set_delay(delay)
			AnimType.SLIDE_IN_RIGHT:
				_tween.tween_property(child, "position:x", REST_POSITION_X, duration) \
					.from(REST_POSITION_X + slide_offset).set_delay(delay)
		# 位移/缩放的同时快速淡入，透明度先行到位让入场更干脆。
		_tween.tween_property(child, "modulate:a", 1.0, duration * 0.5).set_delay(delay)


func _set_children_alpha(alpha: float) -> void:
	for child in _animatable_children():
		(child as Control).modulate.a = alpha


func _animatable_children() -> Array:
	var children := get_children().filter(func(child: Node) -> bool: return child is Control)
	if reverse_order:
		children.reverse()
	return children
