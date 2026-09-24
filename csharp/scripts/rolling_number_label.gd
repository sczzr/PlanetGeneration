extends Label
class_name RollingNumberLabel

## 数值滚动标签（教程 _set 技巧的 4.x 落地）：对 value 赋值时文本不跳变，
## 而是短时间内滚动到新值，落实"数值变化必须有动画"的验收项。
## 连续赋值（如拖动滑条）时先 kill 旧补间、从当前显示值继续滚，快速触发不叠加。

const ROLL_DURATION := 0.15

## 标签显示的数值：赋值即触发 0.15s 滚动动画，连续赋值时从当前显示值接续。
@export var value: float = 100.0:
	set(next_value):
		value = next_value
		_roll_to(next_value)
## 文本格式，占位符接收滚动中的数值（默认整数百分比）。
@export var number_format := "%d%%"

var _displayed := 0.0
var _roll_tween: Tween


func _ready() -> void:
	_set_displayed(value)


## 场景未就绪（如属性初始化阶段）时不启动补间，只同步显示值；_ready 会写出初始文本。
func _roll_to(target: float) -> void:
	if not is_node_ready():
		_displayed = target
		return
	if is_equal_approx(_displayed, target):
		return
	if _roll_tween != null and _roll_tween.is_valid():
		_roll_tween.kill()
	_roll_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_roll_tween.tween_method(_set_displayed, _displayed, target, ROLL_DURATION)


func _set_displayed(displayed: float) -> void:
	_displayed = displayed
	text = number_format % displayed
