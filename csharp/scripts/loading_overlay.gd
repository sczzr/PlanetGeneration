class_name LoadingOverlay
extends Control

## 全屏加载遮罩：数学曲线加载动画 + 标题 + 按真实管线阶段轮换的提示文案。
## 用于世界生成等暂无进度上报的耗时流程：创建世界向导关闭后弹出（转场遮盖），
## 流程层回报结果后关闭。open()/close() 也供未来场景切换复用
## （change_scene 前先 open 遮盖、新场景就绪后 close）。

signal closed

## 标题文案的翻译 key（空串则隐藏标题与提示区，仅剩动画——纯转场模式）。
@export var title_key := "NOTICE_WORLD_GENERATING"
## 轮换提示的翻译 key 列表，对应世界生成的各个真实阶段。
@export var hint_keys: PackedStringArray = [
	"LOADING_HINT_TERRAIN",
	"LOADING_HINT_HYDROLOGY",
	"LOADING_HINT_BIOMES",
	"LOADING_HINT_CITIES",
]
## 提示轮换间隔（秒）。
@export_range(0.5, 10.0, 0.1) var hint_interval := 2.6

## 遮罩淡入/淡出时长（秒）。
const FADE_DURATION := 0.28
## 提示切换交叉淡化的时长（秒）。
const HINT_FADE_DURATION := 0.35

@onready var _loader: MathCurveLoader = %Loader
@onready var _title: Label = %Title
@onready var _hint: Label = %Hint

## 遮罩是否处于"已打开或正在打开"状态；close() 对未打开的遮罩是安全空操作。
var _open := false
## 开关场淡入淡出补间（重复触发时先杀旧补间）。
var _fade_tween: Tween
## 提示轮换计时与当前索引。
var _hint_time := 0.0
var _hint_index := 0
## 提示交叉淡化补间。
var _hint_tween: Tween


func _ready() -> void:
	visible = false
	# 遮罩自身不参与焦点导航，靠 STOP 挡住点击即可；键盘由宿主禁用菜单输入。
	mouse_filter = Control.MOUSE_FILTER_STOP


func _process(delta: float) -> void:
	# 未打开时不推进提示轮换（关闭淡出期间冻结在最后一条）。
	if not _open or hint_keys.is_empty():
		return
	_hint_time += delta
	if _hint_time >= hint_interval:
		_hint_time = 0.0
		_advance_hint()


## 打开遮罩：淡入并开始轮换提示。已打开时仅重置提示计时（幂等）。
func open() -> void:
	_open = true
	visible = true
	_hint_time = 0.0
	_hint_index = -1
	# 进度先归位到无进度动画模式：无进度上报的复用场景（如二级地图转场）
	# 保持原循环动画，有进度上报的流程会在打开后立刻被 set_progress 接管。
	_loader.progress = -1.0
	_refresh_title()
	_advance_hint()
	_play_fade(1.0)


## 上报加载进度（0~1）：驱动加载动画按五瓣花环逐瓣填充；≤1 由动画内部平滑
## 逼近，≥1 立即整环填满。未调用时保持无进度的纯循环动画。
func set_progress(progress: float) -> void:
	_loader.progress = progress


## 关闭遮罩：淡出后隐藏并发 closed 信号。未打开时为空操作。
func close() -> void:
	if not _open:
		return
	_open = false
	_play_fade(0.0)


func is_open() -> bool:
	return _open


func _play_fade(target_alpha: float) -> void:
	if _fade_tween != null and _fade_tween.is_valid():
		_fade_tween.kill()
	_fade_tween = create_tween()
	_fade_tween.tween_property(self, "modulate:a", target_alpha, FADE_DURATION)
	if target_alpha < 1.0:
		_fade_tween.tween_callback(func() -> void:
			visible = false
			closed.emit())


## 标题与提示区可见性：无标题时整体收起（纯转场模式，只显示动画）。
func _refresh_title() -> void:
	var has_title := not title_key.is_empty()
	_title.visible = has_title
	_hint.visible = has_title and not hint_keys.is_empty()
	if has_title:
		_title.text = tr(title_key)


## 切到下一条提示：先淡出、换文案、再淡入，避免文字瞬跳。
func _advance_hint() -> void:
	if hint_keys.is_empty() or not _hint.visible:
		return
	_hint_index = (_hint_index + 1) % hint_keys.size()
	if _hint_tween != null and _hint_tween.is_valid():
		_hint_tween.kill()
	_hint_tween = create_tween()
	_hint_tween.tween_property(_hint, "modulate:a", 0.0, HINT_FADE_DURATION * 0.5)
	_hint_tween.tween_callback(func() -> void:
		_hint.text = tr(hint_keys[_hint_index]))
	_hint_tween.tween_property(_hint, "modulate:a", 1.0, HINT_FADE_DURATION)
