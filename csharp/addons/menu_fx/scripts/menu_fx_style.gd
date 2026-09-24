extends Resource
## 菜单强调效果参数集：一份资源统管全部效果的数值与颜色。
## MenuList 把它套给整列按钮做统一调整；MenuFxButton 也可单独引用一份做覆盖。
## 所有默认值与主菜单现行外观一致，不配置 style 时按钮使用自身同名参数。

@export_group("焦点放大")
## 放大幅度：焦点时缩放目标 = 1.0 + 强度 × 宽度衰减比。
@export var pop_strength := 0.08
## 缩放补间时长（秒）。
@export var pop_duration := 0.3
## 失焦复位时长（秒）。
@export var settle_duration := 0.15
## 强度按此参考宽度起算：更宽的按钮按比例衰减，下限保留一半强度，防止两端位移过大。
@export var reference_width := 220.0

@export_group("下划线")
## 线条颜色：与主菜单主题的焦点强调色保持一致。
@export var underline_color := Color(0.7, 0.85, 1.0, 0.9)
## 下划线粗细（像素）；偶数数值配合整像素对齐可获得锐利边缘。
@export var underline_thickness := 4.0
## 获得焦点时从中心向两端展开的时长（秒）。
@export var underline_grow_duration := 0.2
## 失焦收回的时长（秒）。
@export var underline_shrink_duration := 0.15

@export_group("文字高亮")
## 焦点时文字过渡到的强调色；普通色取主题 font_color。
## 默认为主题焦点色（白）：开启后等于把主题的瞬间变色改为平滑过渡。
@export var highlight_color := Color(1.0, 1.0, 1.0, 1.0)
## 文字颜色过渡时长（秒）。
@export var highlight_duration := 0.15

@export_group("指示箭头")
@export var marker_color := Color(0.7, 0.85, 1.0, 0.9)
## 三角形边长（像素）。
@export var marker_size := 8.0
## 三角形尖端与按钮左缘的间距（像素）；三角形整体位于按钮外侧，指向按钮。
@export var marker_margin := 10.0
## 滑入/滑出时长（秒）。
@export var marker_duration := 0.15

@export_group("发光光晕")
## 光晕颜色：绘制为文字外描边，透明度随进度淡入。
@export var glow_color := Color(0.7, 0.85, 1.0, 0.55)
## 光晕最大描边宽度（像素）。
@export var glow_size := 6.0
## 光晕淡入/淡出时长（秒）。
@export var glow_duration := 0.2

@export_group("声音动效")
## 焦点进入时播放的音效：悬停与键盘导航共用。留空不播。
@export var focus_sound: AudioStream
## 按下时播放的音效。留空不播。
@export var press_sound: AudioStream
## 音效音量偏移（dB），0 为素材原始音量；总线固定走 SFX。
@export var sound_volume_db := 0.0
