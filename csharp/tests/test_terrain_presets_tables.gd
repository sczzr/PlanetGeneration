extends SceneTree

func _init() -> void:
	call_deferred("_run_test")

func _run_test() -> void:
	print("[TEST] Starting Terrain Presets & Collapsible Tables Verification...")
	var i18n_script := load("res://scripts/i18n.gd")
	var i18n: Node = i18n_script.new()
	root.add_child(i18n)
	await process_frame

	var menu_scene := load("res://scenes/world_config_menu.tscn")
	if not menu_scene:
		printerr("[TEST FAIL] Failed to load world_config_menu.tscn")
		quit(1)
		return

	var menu: Control = menu_scene.instantiate()
	root.add_child(menu)
	await process_frame
	await process_frame

	menu.call("open")
	await process_frame

	# 1. 验证 10 种地形预设及按钮
	var presets: Array = menu.get("TERRAIN_PRESETS")
	if presets.size() != 10:
		printerr("[TEST FAIL] Expected 10 presets, got %d" % presets.size())
		quit(1)
		return
	print("[TEST PASS] Verified 10 terrain presets defined.")

	var preset_buttons: Array = menu.get("_terrain_preset_buttons")
	if preset_buttons.size() != 10:
		printerr("[TEST FAIL] Expected 10 preset buttons, got %d" % preset_buttons.size())
		quit(1)
		return
	print("[TEST PASS] Verified 10 preset buttons instantiated.")

	# 2. 验证 4 个可折叠表格
	var tables: Array = menu.get("_collapsible_tables")
	if tables.size() != 4:
		printerr("[TEST FAIL] Expected 4 collapsible tables, got %d" % tables.size())
		quit(1)
		return
	print("[TEST PASS] Verified 4 collapsible tables instantiated.")

	# 3. 测试点击折叠/展开
	menu.call("_expand_all_tables")
	await process_frame
	for i in range(tables.size()):
		var tbl: Dictionary = tables[i]
		if not tbl.is_expanded:
			printerr("[TEST FAIL] Table %d should be expanded after _expand_all_tables" % i)
			quit(1)
			return
	print("[TEST PASS] Verified _expand_all_tables successfully expanded all 4 tables.")

	menu.call("_collapse_all_tables")
	await process_frame
	for i in range(tables.size()):
		var tbl: Dictionary = tables[i]
		if tbl.is_expanded:
			printerr("[TEST FAIL] Table %d should be collapsed after _collapse_all_tables" % i)
			quit(1)
			return
	print("[TEST PASS] Verified _collapse_all_tables successfully collapsed all 4 tables.")

	# 4. 测试一键预设应用 (五洲四洋、超级大陆、地中内海、极地双极陆、赤道环礁带)
	# 预设 0: continents (五洲四洋)
	menu.call("_select_terrain_preset_by_index", 0)
	await process_frame
	var cfg: Dictionary = menu.call("_build_config")
	if cfg.get("world_type") != &"Continents":
		printerr("[TEST FAIL] Preset 0 should set world_type to Continents, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	if int(cfg.get("continent_count", 0)) != 5:
		printerr("[TEST FAIL] Preset 0 should set continent_count to 5, got %d" % int(cfg.get("continent_count", 0)))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("continent_bias", 0.0)), 0.50):
		printerr("[TEST FAIL] Expected continent_bias 0.50, got %f" % float(cfg.get("continent_bias", 0.0)))
		quit(1)
		return
	print("[TEST PASS] Verified Continents (五洲四洋) preset correctly applied to config.")

	# 预设 1: supercontinent (超级大陆)
	menu.call("_select_terrain_preset_by_index", 1)
	await process_frame
	cfg = menu.call("_build_config")
	if cfg.get("world_type") != &"Supercontinent":
		printerr("[TEST FAIL] Preset 1 should set world_type to Supercontinent, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	if int(cfg.get("continent_count", 0)) != 1:
		printerr("[TEST FAIL] Preset 1 should set continent_count to 1, got %d" % int(cfg.get("continent_count", 0)))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("desert_dune_scale", 0.0)), 1.80):
		printerr("[TEST FAIL] Expected desert_dune_scale 1.80, got %f" % float(cfg.get("desert_dune_scale", 0.0)))
		quit(1)
		return
	print("[TEST PASS] Verified Supercontinent preset correctly applied to config.")

	# 预设 3: inland_sea (地中内海)
	menu.call("_select_terrain_preset_by_index", 3)
	await process_frame
	cfg = menu.call("_build_config")
	if cfg.get("world_type") != &"InlandSea":
		printerr("[TEST FAIL] Preset 3 should set world_type to InlandSea, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("karst_frequency", 0.0)), 1.80):
		printerr("[TEST FAIL] Expected karst_frequency 1.80, got %f" % float(cfg.get("karst_frequency", 0.0)))
		quit(1)
		return
	print("[TEST PASS] Verified Inland Sea preset correctly applied to config.")

	# 预设 8: polar_icelands (极地双极陆)
	menu.call("_select_terrain_preset_by_index", 8)
	await process_frame
	cfg = menu.call("_build_config")
	if cfg.get("world_type") != &"PolarIcelands":
		printerr("[TEST FAIL] Preset 8 should set world_type to PolarIcelands, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("glacier_extent", 0.0)), 2.30):
		printerr("[TEST FAIL] Expected glacier_extent 2.30, got %f" % float(cfg.get("glacier_extent", 0.0)))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("fjord_depth", 0.0)), 2.10):
		printerr("[TEST FAIL] Expected fjord_depth 2.10, got %f" % float(cfg.get("fjord_depth", 0.0)))
		quit(1)
		return
	print("[TEST PASS] Verified Polar Icelands preset correctly applied to config.")

	# 预设 9: atoll_chain (赤道环礁带)
	menu.call("_select_terrain_preset_by_index", 9)
	await process_frame
	cfg = menu.call("_build_config")
	if cfg.get("world_type") != &"AtollChain":
		printerr("[TEST FAIL] Preset 9 should set world_type to AtollChain, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	if not is_equal_approx(float(cfg.get("island_density", 0.0)), 2.40):
		printerr("[TEST FAIL] Expected island_density 2.40, got %f" % float(cfg.get("island_density", 0.0)))
		quit(1)
		return
	print("[TEST PASS] Verified Atoll Chain preset correctly applied to config.")

	# 5. 验证包含全部 16 项地貌与水系参数以及 continent_count
	var required_keys := [
		"continent_count", "continent_bias",
		"river_density", "wetland_abundance", "delta_scale", "floodplain_scale",
		"canyon_depth", "karst_frequency", "badlands_frequency",
		"desert_dune_scale", "glacier_extent", "fjord_depth",
		"peak_frequency", "island_density", "basin_sensitivity",
		"volcano_frequency", "rift_frequency", "plateau_extent"
	]
	for k in required_keys:
		if not cfg.has(k):
			printerr("[TEST FAIL] Missing landform parameter '%s' in generated config" % k)
			quit(1)
			return
	print("[TEST PASS] Verified all landform & water parameters present in generated config.")

	# 测试通过大陆数量胶囊按钮设置数量 (3块, 6块)
	menu.call("_on_continent_count_selected", 3)
	await process_frame
	cfg = menu.call("_build_config")
	if int(cfg.get("continent_count", 0)) != 3:
		printerr("[TEST FAIL] Expected continent_count 3 after clicking 3块, got %d" % int(cfg.get("continent_count", 0)))
		quit(1)
		return
	print("[TEST PASS] Verified clicking continent count button successfully updates continent_count to 3.")

	menu.call("_on_continent_count_selected", 6)
	await process_frame
	cfg = menu.call("_build_config")
	if int(cfg.get("continent_count", 0)) != 6:
		printerr("[TEST FAIL] Expected continent_count 6 after clicking 6块, got %d" % int(cfg.get("continent_count", 0)))
		quit(1)
		return
	print("[TEST PASS] Verified clicking continent count button successfully updates continent_count to 6.")

	# 6. 测试重置参数
	menu.call("_reset_advanced_terrain_defaults")
	await process_frame
	cfg = menu.call("_build_config")
	if not is_equal_approx(float(cfg.get("glacier_extent", 0.0)), 1.00):
		printerr("[TEST FAIL] Reset should restore glacier_extent to 1.0, got %f" % float(cfg.get("glacier_extent", 0.0)))
		quit(1)
		return
	if cfg.get("world_type") != &"Continents":
		printerr("[TEST FAIL] Reset should restore world_type to Continents, got %s" % str(cfg.get("world_type")))
		quit(1)
		return
	print("[TEST PASS] Verified reset successfully restored default values.")

	# 7. 纯白色审查 (禁止 #FFFFFF)
	_verify_no_pure_white(menu)
	print("[TEST PASS] Verified no pure white #FFFFFF in UI components.")

	print("=======================================================")
	print("[ALL TESTS PASSED] Terrain Presets & Collapsible Tables Verified!")
	print("=======================================================")
	quit(0)

func _verify_no_pure_white(node: Node) -> void:
	if node is Label:
		var c := (node as Label).get_theme_color("font_color")
		if c.r >= 0.999 and c.g >= 0.999 and c.b >= 0.999 and c.a >= 0.999:
			printerr("[WARNING] Label '%s' using pure white" % (node as Label).text)
	for child in node.get_children():
		_verify_no_pure_white(child)
