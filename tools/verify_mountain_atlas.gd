extends SceneTree
var failures = 0

func check(ok: bool, message: String):
    if not ok:
        failures += 1
        push_error(message)

func _initialize():
    call_deferred("run")

func run():
    var packed: PackedScene = load("res://tile_map/terrain.tscn")
    if packed == null:
        push_error("Cannot load terrain scene")
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
    var source: TileSetAtlasSource = tiles.get_source(0)
    var catalog = JSON.parse_string(FileAccess.get_file_as_string("res://tile_map/terrain_atlas_catalog.json"))
    if source == null or not (catalog is Dictionary) or not catalog.has("groups"):
        push_error("Missing atlas or invalid catalog")
        scene.free()
        quit(1)
        return
    check(tiles.get_terrain_sets_count()==1,"Only the ground terrain set should remain")
    check(source.get_tiles_count()==catalog.tile_count,"Atlas tiles were added or removed")
    check(tiles.get_patterns_count()==catalog.groups.size(),"Unexpected pattern count")
    var object_cells = {}
    for group in catalog.groups:
        for pair in group.cells:
            var cell = Vector2i(int(pair[0]),int(pair[1]))
            check(not object_cells.has(cell),"Duplicate object tile: %s" % cell)
            object_cells[cell] = true
    var ground_count = 0
    for i in source.get_tiles_count():
        var cell = source.get_tile_id(i)
        var td = source.get_tile_data(cell,0)
        if object_cells.has(cell):
            check(td.terrain_set==-1,"Object tile has a terrain set: %s" % cell)
        else:
            ground_count += 1
            check(td.terrain_set==0,"Ground terrain set changed at %s" % cell)
            if td.terrain_set==0:
                check(td.terrain>=0 and td.terrain<tiles.get_terrains_count(0),"Invalid ground terrain at %s" % cell)
    check(ground_count==catalog.ground_count,"Ground tile count changed")
    var stamped_cells = 0
    for group in catalog.groups:
        var members = {}
        for pair in group.cells: members[Vector2i(int(pair[0]),int(pair[1]))] = true
        var pattern: TileMapPattern
        for i in tiles.get_patterns_count():
            if tiles.get_pattern(i).get_meta("mountain_atlas_group","")==group.name: pattern = tiles.get_pattern(i)
        if pattern == null:
            check(false,"Missing pattern: "+group.name)
            continue
        check(pattern.get_used_cells().size()==members.size(),"Pattern size mismatch: "+group.name)
        var stamp_layer = TileMapLayer.new()
        stamp_layer.tile_set = tiles
        root.add_child(stamp_layer)
        stamp_layer.set_pattern(Vector2i(3,5),pattern)
        check(stamp_layer.get_used_cells().size()==members.size(),"Pattern placement failed: "+group.name)
        var origin = Vector2i(int(group.origin[0]),int(group.origin[1]))
        for cell in members:
            check(stamp_layer.get_cell_atlas_coords(cell-origin+Vector2i(3,5))==cell,"Pattern rearranged sprite: "+group.name)
            stamped_cells += 1
        stamp_layer.free()
    print("Tiles=",source.get_tiles_count()," ground=",ground_count,"; stamped=",stamped_cells," patterns=",catalog.groups.size()," failures=",failures)
    scene.free()
    quit(1 if failures else 0)
