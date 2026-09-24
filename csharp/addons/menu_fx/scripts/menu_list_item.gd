extends Resource
## 菜单项配置：MenuList 据此在运行时生成一个强调按钮。
## 在 Inspector 的 items 数组里逐项展开即可配置，每个菜单项可选择不同的强调效果。

const MenuFxButton := preload("res://addons/menu_fx/scripts/menu_fx_button.gd")

## 项标识：用于 item_activated 信号分发与 focus_item 定位，同一 MenuList 内必须唯一。
@export var id: StringName = &""
## 按钮文本的本地化 key，由引擎自动翻译。
@export var text_key: String = ""
## 菜单项图标（可选）：显示在文本左侧。
@export var icon: Texture2D
## 本项启用的强调效果组合（可多选叠加），覆盖整列 style 之外的效果开关维度。
@export_flags("焦点放大", "下划线", "文字高亮", "指示箭头", "发光")
var emphasis: int = MenuFxButton.Effect.POP | MenuFxButton.Effect.UNDERLINE
## 初始即禁用：不参与焦点链，悬停也不接管焦点。
@export var disabled := false
