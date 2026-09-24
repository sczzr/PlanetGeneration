extends SceneTree

func _init():
	call_deferred("_run")

func _run():
	var i18n_script = load("res://scripts/i18n.gd")
	var i18n = i18n_script.new()
	root.add_child(i18n)
	await process_frame
	print("--- TEST WITH I18N ---")
	print("tr('WORLD_CONFIG_SEA_LEVEL'): ", tr("WORLD_CONFIG_SEA_LEVEL"))
	print("tr('WORLD_CONFIG_SEED'): ", tr("WORLD_CONFIG_SEED"))

	var main_scene = load("res://scenes/Main.tscn")
	var main = main_scene.instantiate()
	root.add_child(main)
	await process_frame

	var main_menu = main.get_node("MainLayout/MainMenu")
	main_menu.call("open_world_config")
	await process_frame

	var world_config = main_menu.get_node("%WorldConfigMenu")
	_dump_labels(world_config)
	quit(0)

func _dump_labels(node: Node, indent: String = ""):
	if node is Label:
		var col = node.get_theme_color("font_color")
		var font = node.get_theme_font("font")
		print("%sLabel: text='%s' visible=%s color=%s font=%s" % [
			indent, node.text, node.visible, col,
			font.resource_path if font else "null"
		])
	for child in node.get_children():
		_dump_labels(child, indent + "  ")
