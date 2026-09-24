extends AudioStreamPlayer
## Menu FX 界面音效播放器：覆盖层（设置面板、模式选择等）专用的界面音效组件。
## 作为覆盖层场景的子节点存在，宿主在 open()/close() 里调 play_open()/play_close()，
## 不适合挂强调组件的普通按钮（如下拉框旁的重置按钮）也可由宿主触发 play_click()。
## 总线固定 SFX，受设置页"音效音量"控制；播放即打断上一次，快速连开连关不叠音。
## 创建节点对话框搜索 MenuFxSound 即可添加。

## 面板打开时播放的音效；留空不播。
@export var open_sound: AudioStream
## 面板关闭时播放的音效；留空不播。
@export var close_sound: AudioStream
## 确认点击音：供宿主脚本在普通按钮 pressed 回调里触发；留空不播。
@export var click_sound: AudioStream


func _ready() -> void:
	# UI 界面音效统一走 SFX 总线，与 default_bus_layout 的总线名一致；
	# 写在脚本里让单独添加的节点也无需手动选总线。
	bus = &"SFX"


func play_open() -> void:
	_play(open_sound)


func play_close() -> void:
	_play(close_sound)


func play_click() -> void:
	_play(click_sound)


func _play(stream: AudioStream) -> void:
	if stream == null:
		return
	self.stream = stream
	play()
