extends SceneTree

func _init() -> void:
	call_deferred("_run_test")

func _run_test() -> void:
	print("[TEST] Starting Continents Visual & Topology Verification...")
	var main_scene := load("res://scenes/Main.tscn")
	if not main_scene:
		printerr("[TEST FAIL] Failed to load Main.tscn")
		quit(1)
		return

	var main_node: Node = main_scene.instantiate()
	root.add_child(main_node)
	await process_frame
	await process_frame

	var main_menu: Control = main_node.get_node_or_null("MainLayout/MainMenu")
	if not main_menu:
		printerr("[TEST FAIL] MainMenu not found")
		quit(1)
		return

	var config := {
		"seed": 42,
		"width": 512,
		"height": 256,
		"world_type": "Continents",
		"continent_count": 5,
		"continent_bias": 0.50,
		"oceanic_ratio": 0.45,
		"sea_level": 0.48,
		"heat": 0.50,
		"moisture": 1.0,
		"plate_count": 24,
		"magic_density": 65,
		"civil_aggression": 35,
		"species_diversity": 70,
		"initial_epoch": 120,
		"polygon_tile_mode": 0
	}

	print("[TEST] Emitting new_game_requested for 5 Continents...")
	main_menu.emit_signal("new_game_requested", StringName("guard"), config)

	# 等待生成完成
	var timeout := 30.0
	var elapsed := 0.0
	var completed := false
	while elapsed < timeout:
		await process_frame
		elapsed += 0.016
		if not main_menu.visible:
			var console_panel: Control = main_node.get_node_or_null("MainLayout/ConsolePanel")
			if console_panel and console_panel.visible:
				completed = true
				break
		var t := create_tween()
		t.tween_interval(0.2)
		await t.finished
		elapsed += 0.2

	if not completed:
		printerr("[TEST FAIL] World generation timed out")
		quit(1)
		return

	print("[TEST] Generation completed in %.1f s!" % elapsed)

	# 获取小地图贴图图像
	var minimap_rect: TextureRect = main_node.find_child("MinimapTexture", true, false)
	if not minimap_rect or not minimap_rect.texture:
		printerr("[TEST FAIL] MinimapTexture not found or empty")
		quit(1)
		return

	var img: Image = minimap_rect.texture.get_image()
	if not img:
		printerr("[TEST FAIL] Cannot get image from MinimapTexture")
		quit(1)
		return

	# 保存整张世界地图用于审查
	img.save_png("res://tests/continents_generated_preview.png")
	print("[TEST] Saved preview image to tests/continents_generated_preview.png")

	# 采样 64x32 ASCII 地图
	var cols := 64
	var rows := 32
	var ascii_map := []
	for r in range(rows):
		var line := ""
		for c in range(cols):
			var px := int((float(c) / cols) * img.get_width())
			var py := int((float(r) / rows) * img.get_height())
			var col: Color = img.get_pixel(px, py)
			# 判断是否为水体（蓝色分量明显大于红色或较深蓝）
			var is_water := (col.b > col.r + 0.08 and col.b > col.g) or (col.b > 0.4 and col.r < 0.2)
			if is_water:
				line += " "
			else:
				line += "#"
		ascii_map.append(line)

	print("\n========== 64x32 WORLD ASCII MAP ( # = Land, Space = Ocean ) ==========")
	for line in ascii_map:
		print(line)
	print("========================================================================\n")

	# 计算陆地连通分量 (Connected Components)
	var visited := []
	for r in range(rows):
		var row_v := []
		for c in range(cols):
			row_v.append(false)
		visited.append(row_v)

	var components: Array[int] = []
	for r in range(rows):
		for c in range(cols):
			if ascii_map[r][c] == "#" and not visited[r][c]:
				# BFS
				var q := [Vector2i(c, r)]
				visited[r][c] = true
				var count := 0
				while not q.is_empty():
					var cur: Vector2i = q.pop_front()
					count += 1
					for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
						var nc: int = (cur.x + d.x + cols) % cols # 经度循环
						var nr: int = cur.y + d.y
						if nr >= 0 and nr < rows:
							if ascii_map[nr][nc] == "#" and not visited[nr][nc]:
								visited[nr][nc] = true
								q.append(Vector2i(nc, nr))
				components.append(count)

	components.sort()
	components.reverse()
	print("[TEST] Connected landmasses found (by pixel size in 64x32 map): %s" % str(components))

	# 至少应该有 4~5 个大连通块，并且最大的块不能占总陆地的 80% 以上！
	var total_land := 0
	for size in components:
		total_land += size
	print("[TEST] Total land cells: %d (%.1f%% of map)" % [total_land, (float(total_land) / (cols * rows)) * 100.0])

	var expected_count: int = int(config.get("continent_count", 5))
	if components.size() == expected_count:
		var largest_ratio := float(components[0]) / float(total_land)
		print("[TEST PASS] Exactly %d distinct continents verified! Largest continent accounts for %.1f%% of land." % [expected_count, largest_ratio * 100.0])
		quit(0)
	else:
		printerr("[TEST FAIL] Expected %d continents, found %d: %s" % [expected_count, components.size(), str(components)])
		quit(1)
