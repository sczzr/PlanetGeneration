class_name SmoothTabSwitch
extends Control

## 丝滑方案切换组件（参考 B 站「原子软糖At」《教你做一个超丝滑的方案切换组件》
## BV1g3t96kEEF）：静态纸签轨道之上、标签文字之下，一枚滑块在选项间滑动，
## 标签墨色同步交叉渐变，全程统一三次缓出曲线（急出缓停、无回弹）。
## 视频原版缓动 cubic-bezier(0.34, 0.96, 0.6, 0.99) 与 Godot 原生
## TRANS_CUBIC + EASE_OUT 逐点几乎重合，故直接用原生过渡。
##
## 本控件为普通 Control（非容器），原始子节点（标签按钮与装饰件）由本控件
## 手动横向排布；绘制顺序经 move_child 排定为：纸签轨道 → 装饰件 → 滑块 →
## 标签按钮，从而复刻"滑块从标签下方扫过"的层次。用法：场景中子节点按
## 顺序放好按钮与装饰件，宿主在 _ready 中调用 setup() 配置贴图与配色，
## 切换时调用 set_active()。

## 滑块滑动与文字变色时长（视频主切换实测约 0.5~0.65s，取 0.5s）。
const SLIDE_DURATION := 0.5

## 标签文字需要覆盖的四个字体状态：避免主题默认的悬浮/聚焦色
## 落在纸签上不可见（与旧版页签逻辑一致）。
const FONT_STATES: Array[StringName] = [
	&"font_color", &"font_hover_color", &"font_focus_color", &"font_pressed_color",
]

## 横向排布间距（普通子节点之间）。
@export var separation := 8.0

## 当前激活选项索引。
var active_index := 0

## 参与横向排布的原始子节点（按钮与装饰件，按场景树顺序）。
var _slots: Array[Control] = []
## 其中的标签按钮（顺序与 _slots 一致）。
var _buttons: Array[Button] = []
## 每个按钮的静态纸签背景（未激活态贴图，铺在轨道层）。
var _papers: Array[NineSliceBg] = []
## 唯一滑块（激活态贴图，在纸签之上、文字之下滑动）。
var _pill: NineSliceBg

var _active_tex: Texture2D
var _inactive_tex: Texture2D
var _margin := Vector2(11.0, 6.0)
var _active_color := Color.BLACK
var _muted_color := Color.GRAY
var _hover_tint := Color.WHITE
## 纸签相对文字的留白（空样式盒 content margin，撑大按钮最小尺寸）。
var _content_pad := Vector2(18.0, 8.0)

var _slide_tween: Tween
var _color_tweens: Array[Tween] = []
var _relayout_queued := false


func _ready() -> void:
	# 收集原始子节点：按钮进入 _buttons，其余 Control 视作装饰件参与排布。
	for child in get_children():
		if child is Button:
			_slots.append(child)
			_buttons.append(child)
		elif child is Control:
			_slots.append(child)

	# 纸签与滑块为本控件自建节点；move_child 排定绘制顺序：
	# 纸签（最底）→ 装饰件 → 滑块 → 按钮文字（最顶）。
	var insert_at := 0
	for i in _buttons.size():
		var paper := NineSliceBg.new()
		add_child(paper)
		move_child(paper, insert_at)
		_papers.append(paper)
		insert_at += 1
	for slot in _slots:
		if not (slot is Button):
			move_child(slot, insert_at)
			insert_at += 1
	_pill = NineSliceBg.new()
	add_child(_pill)
	move_child(_pill, insert_at)

	# 文案或字体变化（最小尺寸增减）后重排；宿主亦可在语言切换后主动调
	# request_relayout()。不挂 resized：本组件自身设尺寸会引发无谓回环。
	for btn in _buttons:
		btn.minimum_size_changed.connect(_queue_relayout)
		btn.mouse_entered.connect(_refresh_hover.bind(btn))
		btn.mouse_exited.connect(_refresh_hover.bind(btn))
	resized.connect(_queue_relayout)
	_queue_relayout()


## 配置贴图与配色并应用到初始状态（宿主 _ready 中调用，须在本节点就绪后）。
func setup(active_tex: Texture2D, inactive_tex: Texture2D, margin_x: float, margin_y: float,
		active_color: Color, muted_color: Color, hover_tint: Color,
		content_pad := Vector2(18.0, 8.0)) -> void:
	_active_tex = active_tex
	_inactive_tex = inactive_tex
	_margin = Vector2(margin_x, margin_y)
	_active_color = active_color
	_muted_color = muted_color
	_hover_tint = hover_tint
	_content_pad = content_pad

	for i in _buttons.size():
		_skin_button(_buttons[i])
		_papers[i].texture = _inactive_tex
		_papers[i].margin_x = margin_x
		_papers[i].margin_y = margin_y
	_pill.texture = _active_tex
	_pill.margin_x = margin_x
	_pill.margin_y = margin_y

	_apply_label_colors(false)
	_queue_relayout()


## 切换到指定选项：滑块滑动 + 标签墨色交叉渐变；animate=false 或目标未变时直接落位。
func set_active(index: int, animate := true) -> void:
	index = clampi(index, 0, _buttons.size() - 1)
	var changed := index != active_index
	active_index = index

	_apply_label_colors(animate and changed)
	if not changed or not animate:
		if _slide_tween != null and _slide_tween.is_valid():
			_slide_tween.kill()
		_snap_pill()
		return

	# 从滑块当前矩形接续滑动到目标纸签矩形（中断旧补间，可被快速连点打断重入）。
	if _slide_tween != null and _slide_tween.is_valid():
		_slide_tween.kill()
	var target := _paper_rect(active_index)
	_slide_tween = create_tween()
	_slide_tween.set_parallel(true).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	_slide_tween.tween_property(_pill, "position", target.position, SLIDE_DURATION)
	_slide_tween.tween_property(_pill, "size", target.size, SLIDE_DURATION)


## 请求重排（延迟合帧执行，避免布局期递归）。
func request_relayout() -> void:
	_queue_relayout()


func _queue_relayout() -> void:
	if _relayout_queued:
		return
	_relayout_queued = true
	_relayout.call_deferred()


## 横向排布全部原始子节点（垂直居中），并让纸签与滑块跟随各自按钮矩形。
func _relayout() -> void:
	_relayout_queued = false
	if _slots.is_empty():
		return

	var total_w := 0.0
	var track_h := 0.0
	for slot in _slots:
		var ms := slot.get_combined_minimum_size()
		total_w += ms.x
		track_h = maxf(track_h, ms.y)
	total_w += separation * (_slots.size() - 1)
	track_h = maxf(track_h, size.y)

	# 控件实际宽度即最小宽度（父容器按需分配），内容整体水平居中兜底。
	var x := maxf((size.x - total_w) * 0.5, 0.0)
	var btn_index := 0
	for slot in _slots:
		var ms := slot.get_combined_minimum_size()
		slot.size = ms
		slot.position = Vector2(x, (track_h - ms.y) * 0.5)
		x += ms.x + separation
		if slot is Button:
			var paper := _papers[btn_index]
			paper.position = slot.position
			paper.size = slot.size
			btn_index += 1

	# 布局变化时滑块直接落位（多为窗口尺寸/语言切换，不打断即中断当前滑动）。
	if _slide_tween != null and _slide_tween.is_valid():
		_slide_tween.kill()
	_snap_pill()


## 上报最小尺寸给父容器：全部子节点最小尺寸的横向总和。
func _get_minimum_size() -> Vector2:
	var total := Vector2.ZERO
	for slot in _slots:
		var ms := slot.get_combined_minimum_size()
		total.x += ms.x
		total.y = maxf(total.y, ms.y)
	total.x += separation * (_slots.size() - 1)
	return total


## 滑块落位到当前激活按钮的纸签矩形。
func _snap_pill() -> void:
	if _pill == null or _papers.is_empty():
		return
	var r := _paper_rect(active_index)
	_pill.position = r.position
	_pill.size = r.size


func _paper_rect(index: int) -> Rect2:
	var paper := _papers[clampi(index, 0, _papers.size() - 1)]
	return Rect2(paper.position, paper.size)


## 按钮只负责文字：空样式盒（带留白）+ 关闭文字投影，皮肤全由纸签/滑块承担。
func _skin_button(btn: Button) -> void:
	for state_name in ["normal", "hover", "pressed", "focus"]:
		var empty := StyleBoxEmpty.new()
		empty.content_margin_left = _content_pad.x
		empty.content_margin_right = _content_pad.x
		empty.content_margin_top = _content_pad.y
		empty.content_margin_bottom = _content_pad.y
		btn.add_theme_stylebox_override(state_name, empty)
	btn.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0))
	btn.add_theme_constant_override("shadow_offset_x", 0)
	btn.add_theme_constant_override("shadow_offset_y", 0)


## 标签墨色：激活=深墨，未激活=灰墨；四个字体状态同步过渡，保证悬浮/聚焦不掉色。
func _apply_label_colors(animate: bool) -> void:
	for tw in _color_tweens:
		if tw != null and tw.is_valid():
			tw.kill()
	_color_tweens.clear()

	for i in _buttons.size():
		var btn := _buttons[i]
		var target := _active_color if i == active_index else _muted_color
		if not animate:
			for state in FONT_STATES:
				btn.add_theme_color_override(state, target)
			continue
		var tw := btn.create_tween()
		tw.set_parallel(true).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		for state in FONT_STATES:
			tw.tween_property(btn, "theme_override_colors/%s" % state, target, SLIDE_DURATION)
		_color_tweens.append(tw)


## 悬浮提亮：作用于该按钮当前生效的背景（激活=滑块，未激活=纸签）。
func _refresh_hover(btn: Button) -> void:
	var index := _buttons.find(btn)
	if index < 0:
		return
	var bg: NineSliceBg = _pill if index == active_index else _papers[index]
	bg.tint = _hover_tint if btn.is_hovered() else Color.WHITE
