extends SceneTree
const OUT = "res://assets/tile_map/grass_dirt_blob/"
const DIRECTIONS = [Vector2i.UP, Vector2i(1,-1), Vector2i.RIGHT, Vector2i(1,1), Vector2i.DOWN, Vector2i(-1,1), Vector2i.LEFT, Vector2i(-1,-1)]
var errors = 0

func check(condition: bool, message: String):
    if not condition:
        errors += 1
        push_error(message)

func _initialize():
    call_deferred("run")

func run():
    var tiles: TileSet = load(OUT + "grass_dirt_blob.tres")
    check(tiles != null and tiles.tile_size == Vector2i(32,32), "Invalid TileSet")
    check(tiles.get_terrain_set_mode(0) == TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES, "Invalid mode")
    var catalog = JSON.parse_string(FileAccess.get_file_as_string(OUT + "mask_catalog.json"))
    var source: TileSetAtlasSource = tiles.get_source(0)
    var atlas = source.texture.get_image()
    var checks = 0
    # 对任意可连接的图块组合，验证贴图边缘逐像素相等，而不只检查数字掩码。
    for a in 48:
        for b in 48:
            var ca = Vector2i(a%8,a/8)
            var cb = Vector2i(b%8,b/8)
            var ta = source.get_tile_data(ca,0)
            var tb = source.get_tile_data(cb,0)
            for axis in 2:
                var pairs = [[0,8],[15,11],[3,7]] if axis == 0 else [[4,12],[7,11],[3,15]]
                var matches = true
                for pair in pairs:
                    if ta.get_terrain_peering_bit(pair[0]) != tb.get_terrain_peering_bit(pair[1]): matches = false
                if not matches: continue
                checks += 1
                for p in 32:
                    var pa = ca*32 + (Vector2i(31,p) if axis==0 else Vector2i(p,31))
                    var pb = cb*32 + (Vector2i(0,p) if axis==0 else Vector2i(p,0))
                    check(atlas.get_pixelv(pa)==atlas.get_pixelv(pb), "Texture seam: %d -> %d axis %d pixel %d" % [a,b,axis,p])
    # 使用引擎的正式自动铺地接口，逐个验证全部 47 种邻接拓扑。
    for item in catalog:
        var layer = TileMapLayer.new()
        layer.tile_set = tiles
        root.add_child(layer)
        for y in 9:
            for x in 9: layer.set_cell(Vector2i(x,y),0,Vector2i(7,5))
        var cells: Array[Vector2i] = [Vector2i(4,4)]
        var mask = int(item.mask)
        for i in 8:
            if mask & (1<<i): cells.append(Vector2i(4,4)+DIRECTIONS[i])
        layer.set_cells_terrain_connect(cells,0,1,false)
        var expected = Vector2i(int(item.atlas[0]),int(item.atlas[1]))
        check(layer.get_cell_atlas_coords(Vector2i(4,4)) == expected, "Autotile mask failed: %d" % mask)
        # 再把中心擦回草地，确认邻居会重新计算。
        layer.set_cells_terrain_connect([Vector2i(4,4)],0,0,false)
        check(layer.get_cell_tile_data(Vector2i(4,4)).terrain == 0, "Grass repaint failed")
        layer.free()
    print("47 masks + 47 grass repaints; ", checks, " compatible edges checked pixel by pixel; errors=",errors)
    if errors:
        quit(1)
        return
    var demo = Node2D.new()
    demo.name = "GrassDirtBlobDemo"
    var layer = TileMapLayer.new()
    layer.name = "Terrain"
    layer.tile_set = tiles
    layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    demo.add_child(layer)
    layer.owner = demo
    root.add_child(demo)
    for y in 26:
        for x in 40: layer.set_cell(Vector2i(x,y),0,Vector2i(7,5))
    var paint: Array[Vector2i] = []
    for y in range(2,12):
        for x in range(2,17):
            if not (x>=6 and x<=11 and y>=5 and y<=8): paint.append(Vector2i(x,y))
    for x in range(20,37): paint.append(Vector2i(x,5))
    for y in range(2,12): paint.append(Vector2i(28,y))
    for y in range(15,24):
        for x in range(3,17):
            if x<7 or y>20: paint.append(Vector2i(x,y))
    for x in [21,24,27,30,33,36]: paint.append(Vector2i(x,16))
    for x in range(21,37):
        paint.append(Vector2i(x,21))
        if x%3 == 0: paint.append(Vector2i(x,22))
    layer.set_cells_terrain_connect(paint,0,1,false)
    var packed = PackedScene.new()
    check(packed.pack(demo)==OK, "Scene packing failed")
    check(ResourceSaver.save(packed,"res://tile_map/grass_dirt_blob_demo.tscn")==OK, "Scene save failed")
    var preview = Image.create(1280,832,false,Image.FORMAT_RGBA8)
    for pos in layer.get_used_cells():
        preview.blit_rect(atlas,Rect2i(layer.get_cell_atlas_coords(pos)*32,Vector2i(32,32)),pos*32)
    check(preview.save_png(OUT + "preview.png")==OK,"Preview save failed")
    demo.free()
    quit(1 if errors else 0)
