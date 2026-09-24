@tool
extends EditorPlugin
## Menu FX 编辑器插件入口：把强调按钮、菜单列与界面音效播放器注册为自定义节点类型，
## 让"创建节点"对话框能直接搜索添加，并显示插件专属图标。
## 场景文件保存的是脚本路径引用，因此插件被停用时已有场景照常运行。

const MenuFxButtonScript := preload("res://addons/menu_fx/scripts/menu_fx_button.gd")
const MenuListScript := preload("res://addons/menu_fx/scripts/menu_list.gd")
const MenuFxSoundScript := preload("res://addons/menu_fx/scripts/menu_fx_sound.gd")


func _enter_tree() -> void:
	add_custom_type(
		"MenuFxButton", "Button", MenuFxButtonScript,
		load("res://addons/menu_fx/icons/menu_fx_button.svg")
	)
	add_custom_type(
		"MenuList", "VBoxContainer", MenuListScript,
		load("res://addons/menu_fx/icons/menu_list.svg")
	)
	add_custom_type(
		"MenuFxSound", "AudioStreamPlayer", MenuFxSoundScript,
		load("res://addons/menu_fx/icons/menu_fx_sound.svg")
	)


func _exit_tree() -> void:
	# 与注册顺序无关，逐个移除即可；插件未启用时不会进入这里。
	remove_custom_type("MenuList")
	remove_custom_type("MenuFxButton")
	remove_custom_type("MenuFxSound")
