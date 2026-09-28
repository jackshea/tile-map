extends SceneTree
## 离线配置工具：从分类表创建完整物体 Pattern，不给装饰切片配置自动连接。
const SCENE = "res://tile_map/terrain.tscn"
const CATALOG = "res://tile_map/terrain_atlas_catalog.json"

func _initialize():
    var packed: PackedScene = load(SCENE)
    if packed == null:
        push_error("Cannot load scene: " + SCENE)
        quit(1)
        return
    var scene = packed.instantiate()
    var layer: TileMapLayer = scene.get_node_or_null("TileMapLayer")
    if layer == null or layer.tile_set == null:
        push_error("Missing TileMapLayer or TileSet")
        scene.free()
        quit(1)
        return
    var tiles: TileSet = layer.tile_set
    var atlas: TileSetAtlasSource = tiles.get_source(0)
    var catalog = JSON.parse_string(FileAccess.get_file_as_string(CATALOG))
    if atlas == null or not (catalog is Dictionary) or not catalog.has("groups"):
        push_error("Missing atlas or invalid catalog")
        scene.free()
        quit(1)
        return
    for group in catalog.groups:
        var members = {}
        for pair in group.cells: members[Vector2i(int(pair[0]),int(pair[1]))] = true
        for cell in members:
            var td = atlas.get_tile_data(cell,0)
            if td == null:
                push_error("Missing atlas tile: %s" % cell)
                scene.free()
                quit(1)
                return
            td.terrain_set = -1
        # Pattern 保留原始坐标和完整切片，避免连接笔刷随机替换同拓扑图块。
        var pattern = TileMapPattern.new()
        pattern.resource_name = group.name
        pattern.set_meta("mountain_atlas_group",group.name)
        var origin = Vector2i(int(group.origin[0]),int(group.origin[1]))
        for cell in members: pattern.set_cell(cell-origin,0,cell,0)
        var existing = -1
        for i in tiles.get_patterns_count():
            if tiles.get_pattern(i).get_meta("mountain_atlas_group","") == group.name: existing = i
        if existing>=0:
            tiles.remove_pattern(existing)
            tiles.add_pattern(pattern,existing)
        else: tiles.add_pattern(pattern)
    # 仅保留原有地面地形集 0。
    while tiles.get_terrain_sets_count()>1:
        tiles.remove_terrain_set(tiles.get_terrain_sets_count()-1)
    var pattern_count = tiles.get_patterns_count()
    var error = ResourceSaver.save(packed,SCENE)
    scene.free()
    if error != OK:
        push_error("Save failed: %s" % error)
        quit(1)
        return
    print("Configured ",catalog.tile_count," tiles; patterns=",pattern_count)
    quit()
