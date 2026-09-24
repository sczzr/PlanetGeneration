extends SceneTree

func _init() -> void:
	print("[TEST] Starting Menu Flow & World Gen Verification...")
	call_deferred("_run_test")

func _run_test() -> void:
	var main_scene := load("res://scenes/Main.tscn")
	if not main_scene:
		printerr("[TEST FAIL] Could not load Main.tscn")
		quit(1)
		return

	var main_node: Node = main_scene.instantiate()
	root.add_child(main_node)

	# 等待两帧让 _Ready 完成
	await process_frame
	await process_frame

	print("[TEST] Scene instantiated.")

	# 1. 验证 MainMenu 与 ConsolePanel 初始状态
	var main_menu: Control = main_node.get_node_or_null("MainLayout/MainMenu")
	if not main_menu or not main_menu.visible:
		printerr("[TEST FAIL] MainMenu is not visible on start")
		quit(1)
		return
	print("[TEST PASS] MainMenu is visible on start.")

	if main_menu.z_index != 20:
		printerr("[TEST FAIL] MainMenu z_index should be 20, got %d" % main_menu.z_index)
		quit(1)
		return
	print("[TEST PASS] MainMenu z_index is 20.")

	var minimap_panel: Control = main_node.find_child("MinimapPanel", true, false)
	var biome_legend: Control = main_node.find_child("BiomeLegendPanel", true, false)
	if minimap_panel and minimap_panel.visible:
		printerr("[TEST FAIL] MinimapPanel should be hidden on start")
		quit(1)
		return
	print("[TEST PASS] MinimapPanel is hidden on start.")

	if biome_legend and biome_legend.visible:
		printerr("[TEST FAIL] BiomeLegendPanel should be hidden on start")
		quit(1)
		return
	print("[TEST PASS] BiomeLegendPanel is hidden on start.")

	var console_panel: Control = main_node.get_node_or_null("MainLayout/ConsolePanel")
	if console_panel and console_panel.visible:
		printerr("[TEST FAIL] ConsolePanel should be hidden on start")
		quit(1)
		return
	print("[TEST PASS] ConsolePanel is hidden on start.")

	# 2. 验证 PauseMenu 存在
	var pause_menu: Control = main_node.get_node_or_null("OverlayLayer/PauseMenu")
	if not pause_menu:
		printerr("[TEST FAIL] PauseMenu not found in OverlayLayer")
		quit(1)
		return
	print("[TEST PASS] PauseMenu exists in OverlayLayer.")

	# 3. 验证 WorldConfigMenu 存在并可用
	var world_config: Control = main_menu.get_node_or_null("%WorldConfigMenu")
	if not world_config:
		printerr("[TEST FAIL] WorldConfigMenu not found in MainMenu")
		quit(1)
		return
	print("[TEST PASS] WorldConfigMenu found in MainMenu.")

	# 4. 测试 open_world_config
	main_menu.call("open_world_config")
	if not world_config.visible:
		printerr("[TEST FAIL] WorldConfigMenu did not become visible after open_world_config()")
		quit(1)
		return
	print("[TEST PASS] WorldConfigMenu opened successfully.")

	# 5. 测试触发生成世界
	var config := {
		"seed": 99999,
		"width": 512,
		"height": 256,
		"world_type": "Supercontinent",
		"sea_level": 0.48,
		"heat": 0.52,
		"moisture": 1.1,
		"plate_count": 16,
		"oceanic_ratio": 0.44,
		"continent_bias": 0.25,
		"landform_relief": 1.05,
		"rock_debris_frequency": 6,
		"magic_density": 65,
		"civil_aggression": 35,
		"species_diversity": 70,
		"initial_epoch": 120,
		"polygon_tile_mode": 0
	}

	print("[TEST] Emitting new_game_requested with test config...")
	main_menu.emit_signal("new_game_requested", StringName("guard"), config)

	# 等待生成完成（轮询主菜单隐藏且控制台展开）
	var timeout := 30.0
	var elapsed := 0.0
	var completed := false
	while elapsed < timeout:
		await create_timer(0.5).timeout
		elapsed += 0.5
		if not main_menu.visible and console_panel.visible:
			print("[TEST PASS] World generated and console appeared in %0.1f seconds!" % elapsed)
			completed = true
			break

	if not completed:
		printerr("[TEST FAIL] World generation timed out or UI transition failed!")
		quit(1)
		return

	# 6. 验证生成完成后主菜单隐藏、控制台展开
	if main_menu.visible:
		printerr("[TEST FAIL] MainMenu should be hidden after generation")
		quit(1)
		return
	print("[TEST PASS] MainMenu hidden after generation.")

	if not console_panel.visible:
		printerr("[TEST FAIL] ConsolePanel should be visible after generation")
		quit(1)
		return
	print("[TEST PASS] ConsolePanel is visible after generation.")

	# 7. 测试暂停菜单打开
	pause_menu.call("open")
	if not pause_menu.visible:
		printerr("[TEST FAIL] PauseMenu did not open")
		quit(1)
		return
	print("[TEST PASS] PauseMenu opened successfully.")

	# 8. 测试从暂停菜单回到主菜单
	pause_menu.emit_signal("main_menu_requested")
	await process_frame
	await process_frame

	if not main_menu.visible:
		printerr("[TEST FAIL] MainMenu should be restored after returning from pause menu")
		quit(1)
		return
	print("[TEST PASS] MainMenu successfully restored from pause menu.")

	if minimap_panel and minimap_panel.visible:
		printerr("[TEST FAIL] MinimapPanel MUST be hidden after returning to MainMenu")
		quit(1)
		return
	print("[TEST PASS] MinimapPanel is properly hidden after returning to MainMenu.")

	if biome_legend and biome_legend.visible:
		printerr("[TEST FAIL] BiomeLegendPanel MUST be hidden after returning to MainMenu")
		quit(1)
		return
	print("[TEST PASS] BiomeLegendPanel is properly hidden after returning to MainMenu.")

	# 9. 测试通过新建世界请求打开向导时 HUD 依然保持隐藏
	main_node.call("OnNewWorldRequested")
	await process_frame
	await process_frame
	if minimap_panel and minimap_panel.visible:
		printerr("[TEST FAIL] MinimapPanel MUST be hidden during OnNewWorldRequested")
		quit(1)
		return
	if biome_legend and biome_legend.visible:
		printerr("[TEST FAIL] BiomeLegendPanel MUST be hidden during OnNewWorldRequested")
		quit(1)
		return
	print("[TEST PASS] HUD remains hidden during OnNewWorldRequested.")

	print("\n=======================================================")
	print("[ALL TESTS PASSED] Menu System & WorldConfig Integrated Successfully!")
	print("=======================================================\n")
	quit(0)
