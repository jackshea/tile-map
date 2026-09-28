extends SceneTree
## 编辑器外运行：Godot --headless --path . --script tools/build_grass_dirt_blob.gd
## 纹理由 imagegen 制作；此脚本按精确拓扑组装 32px Blob 图集及资源。
const OUT = "res://assets/tile_map/grass_dirt_blob/"
const BITS = [TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
    TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
    TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
    TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]

func valid_mask(mask: int) -> bool:
    for corner in [1, 3, 5, 7]:
        if mask & (1 << corner):
            if not (mask & (1 << ((corner + 7) % 8))) or not (mask & (1 << ((corner + 1) % 8))):
                return false
    return true

func material(source: Image, right: bool) -> Image:
    var half = source.get_width() / 2
    var sample = source.get_region(Rect2i(half if right else 0, 0, half, source.get_height()))
    sample.resize(64, 64, Image.INTERPOLATE_LANCZOS)
    var result = Image.create(32, 32, false, Image.FORMAT_RGBA8)
    # 镜像周期让四条图块边缘的纹理像素精确相同。
    for y in 32:
        for x in 32:
            result.set_pixel(x, y, sample.get_pixel(24 + mini(x, 31-x), 24 + mini(y, 31-y)))
    return result

func boundary_distance(mask: int, x: int, y: int) -> float:
    var left = x < 16
    var top = y < 16
    var u = float(x if left else 31-x) + 0.5
    var v = float(y if top else 31-y) + 0.5
    var horizontal = (mask & (64 if left else 4)) != 0
    var vertical = (mask & (1 if top else 16)) != 0
    var diagonal_bit = (128 if left else 2) if top else (32 if left else 8)
    if horizontal and vertical:
        return 32.0 if mask & diagonal_bit else Vector2(u, v).length() - 6.0
    if horizontal:
        return v - 6.0
    if vertical:
        return u - 6.0
    if u < 10.0 and v < 10.0:
        return 4.0 - Vector2(u-10.0, v-10.0).length()
    return minf(u-6.0, v-6.0)

func _initialize():
    var source = Image.load_from_file(OUT + "material_source.png")
    if source == null or source.is_empty():
        push_error("Missing material source")
        quit(1)
        return
    var grass = material(source, false)
    var dirt = material(source, true)
    var atlas = Image.create(256, 192, false, Image.FORMAT_RGBA8)
    var masks: Array[int] = []
    var catalog = []
    for mask in 256:
        if valid_mask(mask): masks.append(mask)
    assert(masks.size() == 47)
    for index in 47:
        var mask = masks[index]
        var origin = Vector2i(index % 8, index / 8) * 32
        for y in 32:
            for x in 32:
                var distance = boundary_distance(mask, x, y)
                var color = dirt.get_pixel(x, y) if distance >= 0 else grass.get_pixel(x, y)
                if distance >= 0 and distance < 1.2:
                    color = color.darkened(0.18)
                elif distance < 0 and distance > -1.2:
                    color = color.lightened(0.08)
                atlas.set_pixelv(origin + Vector2i(x,y), color)
        catalog.append({"index": index, "atlas": [index % 8, index / 8], "mask": mask})
    atlas.blit_rect(grass, Rect2i(0,0,32,32), Vector2i(224,160))
    assert(atlas.save_png(OUT + "grass_dirt_blob_47.png") == OK)
    var text = '[gd_resource type="TileSet" load_steps=3 format=3]\n\n'
    text += '[ext_resource type="Texture2D" path="' + OUT + 'grass_dirt_blob_47.png" id="1"]\n\n'
    text += '[sub_resource type="TileSetAtlasSource" id="Atlas"]\ntexture = ExtResource("1")\ntexture_region_size = Vector2i(32, 32)\n'
    var names = ["top_side", "top_right_corner", "right_side", "bottom_right_corner", "bottom_side", "bottom_left_corner", "left_side", "top_left_corner"]
    for index in 48:
        var key = "%d:%d/0" % [index % 8, index / 8]
        text += key + ' = 0\n' + key + '/terrain_set = 0\n' + key + '/terrain = %d\n' % (1 if index < 47 else 0)
        for bit in 8:
            var terrain = 1 if index < 47 and masks[index] & (1 << bit) else 0
            text += key + '/terrains_peering_bit/' + names[bit] + ' = %d\n' % terrain
    text += '\n[resource]\ntile_size = Vector2i(32, 32)\nterrain_set_0/mode = 0\n'
    text += 'terrain_set_0/terrain_0/name = "草地"\nterrain_set_0/terrain_0/color = Color(0.28, 0.56, 0.16, 1)\n'
    text += 'terrain_set_0/terrain_1/name = "干土"\nterrain_set_0/terrain_1/color = Color(0.69, 0.43, 0.2, 1)\nsources/0 = SubResource("Atlas")\n'
    var file = FileAccess.open(OUT + "grass_dirt_blob.tres", FileAccess.WRITE)
    file.store_string(text)
    file.close()
    file = FileAccess.open(OUT + "mask_catalog.json", FileAccess.WRITE)
    file.store_string(JSON.stringify(catalog, "  "))
    file.close()
    print("Built 47 Blob tiles + grass; 256x192 atlas, 32x32 cells.")
    quit()
