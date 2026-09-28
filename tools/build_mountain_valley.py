"""从现有 TileSet 构建可编辑的山间遗迹地图及 PNG 预览。"""

from __future__ import annotations

import base64
import json
import math
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tile_map/terrain.tscn"
CATALOG = ROOT / "tile_map/terrain_atlas_catalog.json"
ATLAS = ROOT / "assets/tile_map/mountain_landscape.png"
SCENE = ROOT / "tile_map/mountain_valley.tscn"
PREVIEW = ROOT / "tile_map/mountain_valley_preview.png"
WIDTH, HEIGHT, SIZE = 64, 52, 32
CORNERS = ("top_left_corner", "top_right_corner", "bottom_left_corner", "bottom_right_corner")


def tile_metadata(scene: str):
    metadata = defaultdict(dict)
    for x, y, key, value in re.findall(
        r"^(\d+):(\d+)/0/(terrain|terrains_peering_bit/[^ =]+) = (-?\d+)$", scene, re.M
    ):
        metadata[int(x), int(y)][key] = int(value)
    variants = defaultdict(list)
    for atlas_cell, values in metadata.items():
        key = tuple(values.get(f"terrains_peering_bit/{corner}", -1) for corner in CORNERS)
        variants[key].append(atlas_cell)
    return metadata, variants


def distance_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def paint_path(vertices, points, radius, terrain):
    for y in range(HEIGHT + 1):
        for x in range(WIDTH + 1):
            if min(distance_to_segment(x, y, *a, *b) for a, b in zip(points, points[1:])) <= radius:
                vertices[y][x] = terrain


def paint_ellipse(vertices, cx, cy, rx, ry, terrain, wobble=0.0):
    for y in range(HEIGHT + 1):
        for x in range(WIDTH + 1):
            variance = wobble * math.sin(x * 1.7 + y * 0.9) * math.cos(x * 0.6 - y * 1.3)
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 + variance:
                vertices[y][x] = terrain


def paint_rect(vertices, left, top, right, bottom, terrain):
    for y in range(top, bottom + 1):
        for x in range(left, right + 1):
            vertices[y][x] = terrain


def make_ground(variants):
    # 顶点地形决定四角图块，确保相邻格共享同一条边的颜色。
    vertices = [[0 for _ in range(WIDTH + 1)] for _ in range(HEIGHT + 1)]
    paint_path(vertices, [(32, 52), (32, 45), (29, 39), (31, 33), (31, 27), (33, 21), (33, 15), (30, 4)], 3.1, 1)
    paint_path(vertices, [(31, 30), (24, 31), (19, 30), (13, 27)], 2.8, 1)
    paint_path(vertices, [(32, 26), (39, 26), (45, 29), (51, 28)], 2.7, 1)
    paint_ellipse(vertices, 31, 30, 8.3, 7.1, 1, 0.09)
    paint_ellipse(vertices, 50, 28, 8.0, 7.0, 1, 0.07)
    paint_ellipse(vertices, 14, 27, 6.5, 6.0, 1, 0.06)
    paint_ellipse(vertices, 31, 11, 7.0, 6.5, 1, 0.06)
    # 岩地嵌在北面土坡；东侧另有直接从草地露出的岩块。
    paint_ellipse(vertices, 31, 11, 4.0, 3.5, 2, 0.11)
    paint_ellipse(vertices, 57, 13, 3.2, 3.0, 2, 0.05)
    # 两种石铺地都被土路包围，避免出现没有过渡瓦片的三材质交点。
    paint_rect(vertices, 28, 28, 34, 32, 3)
    paint_rect(vertices, 48, 26, 53, 30, 4)
    # 营地南侧的小径由斜向踏点构成，补足草土交界的两种对角连接。
    for x, y in [(35, 42), (36, 42), (37, 43), (38, 44), (39, 43), (40, 42)]:
        vertices[y][x] = 1

    ground = {}
    used = Counter()
    unsupported = Counter()
    for y in range(HEIGHT):
        for x in range(WIDTH):
            key = (vertices[y][x], vertices[y][x + 1], vertices[y + 1][x], vertices[y + 1][x + 1])
            choices = variants.get(key)
            if not choices:
                unsupported[key] += 1
                continue
            if key == (0, 0, 0, 0):
                atlas_cell = (14 + x % 2, 4 + y % 2)
            else:
                atlas_cell = choices[(x * 13 + y * 7) % len(choices)]
            ground[x, y] = atlas_cell
            used[atlas_cell] += 1
    if unsupported:
        raise ValueError(f"缺少角落过渡瓦片: {unsupported}")
    # 同一连接组合下的纹理变体，至少各使用一次。
    unfillable = []
    for key, choices in variants.items():
        missing = [cell for cell in choices if used[cell] == 0]
        if not missing:
            continue
        matching = [position for position, cell in ground.items() if cell in choices]
        if len(matching) < len(choices):
            unfillable.append((key, choices, len(matching)))
            continue
        for position, cell in zip(matching, choices):
            used[ground[position]] -= 1
            ground[position] = cell
            used[cell] += 1
    if unfillable:
        raise ValueError(f"没有足够位置使用全部纹理变体: {unfillable}")
    return ground


def make_objects(catalog):
    groups = {group["name"]: group for group in catalog["groups"]}
    objects = {}
    placements = []

    def stamp(name, x, y):
        group = groups[name]
        origin_x, origin_y = group["origin"]
        for ax, ay in group["cells"]:
            cell = (x + ax - origin_x, y + ay - origin_y)
            if cell in objects:
                raise ValueError(f"物体重叠: {name} at {cell}")
            if not (0 <= cell[0] < WIDTH and 0 <= cell[1] < HEIGHT):
                raise ValueError(f"物体超出地图: {name} at {cell}")
            objects[cell] = (ax, ay)
        placements.append((name, x, y))

    # 北缘断续山脊、洞口与东侧露岩。
    for name, x, y in [
        ("山地-圆顶", 3, 2), ("山地-横向", 11, 0), ("山地-陡坡", 17, 0),
        ("山地-崖壁", 23, 0), ("山洞", 29, 0), ("山地-圆顶", 35, 2),
        ("山地-横向", 44, 0), ("山地-崖壁", 51, 0), ("山地-陡坡", 58, 0),
        ("山地-圆顶", 0, 17), ("山地-圆顶", 1, 24),
    ]:
        stamp(name, x, y)

    # 西侧林地、主路旁的营地和东侧石制遗迹。
    for name, x, y in [
        ("篝火", 30, 29), ("石制圆盘", 49, 27),
        ("阔叶树", 8, 36), ("阔叶树", 15, 39), ("阔叶树", 3, 43),
        ("阔叶树", 54, 38), ("阔叶树", 58, 45),
        ("针叶树-大", 11, 12), ("针叶树-大", 20, 16),
        ("针叶树-大", 43, 13), ("针叶树-大", 58, 17),
        ("针叶树-大", 6, 46), ("针叶树-大", 47, 43),
        ("针叶树-小", 8, 12), ("针叶树-小", 16, 15),
        ("针叶树-小", 47, 12), ("针叶树-小", 55, 17),
        ("针叶树-小", 11, 43), ("针叶树-小", 53, 45),
        ("草丛-高草", 4, 33), ("草丛-高草", 19, 42),
        ("草丛-高草", 39, 39), ("草丛-高草", 58, 34),
        ("草丛-矮草", 12, 34), ("草丛-矮草", 23, 45),
        ("草丛-矮草", 43, 43), ("草丛-矮草", 60, 41),
        ("草丛-长叶", 19, 22), ("草丛-长叶", 40, 20),
        ("蕨叶", 13, 21), ("蕨叶", 37, 35),
        ("花丛", 16, 25), ("花丛", 50, 35),
        ("草丛-小簇", 22, 37), ("草丛-小簇", 45, 36),
        ("岩石-立石", 26, 10), ("岩石-尖石", 40, 12),
        ("岩石-深色", 53, 13), ("岩石-宽石", 56, 20),
        ("岩石-碎石", 21, 11), ("岩石-扁石", 59, 23),
        ("石堆", 45, 19),
    ]:
        stamp(name, x, y)

    # 周缘林冠与草簇疏密变化，使营地和遗迹成为林中空地。
    for name, x, y in [
        ("阔叶树", 0, 37), ("阔叶树", 19, 35),
        ("阔叶树", 54, 32), ("阔叶树", 42, 46),
        ("针叶树-大", 3, 39), ("针叶树-大", 17, 35), ("针叶树-大", 60, 29),
        ("针叶树-小", 24, 39), ("针叶树-小", 46, 38), ("针叶树-小", 59, 38),
        ("草丛-高草", 12, 46), ("草丛-高草", 48, 47), ("草丛-高草", 23, 49),
        ("草丛-矮草", 36, 47), ("花丛", 8, 30), ("花丛", 18, 46),
        ("石堆", 41, 42),
    ]:
        stamp(name, x, y)

    assert {name for name, _, _ in placements} == set(groups)
    return objects, placements


def pack_layer(cells):
    # Godot TileMapLayer 序列化格式：source_id、格坐标、alternative、图集坐标，各为 int16。
    records = bytearray()
    for (x, y), (ax, ay) in sorted(cells.items(), key=lambda item: (item[0][1], item[0][0])):
        records.extend(struct.pack("<hhhhhh", 0, x, y, 0, ax, ay))
    records.extend(b"\0\0")
    return base64.b64encode(records).decode("ascii")


def build_scene(source, ground, objects):
    marker = '[node name="Node2D"'
    if marker not in source:
        raise ValueError("找不到源场景节点")
    resource_text = source[: source.index(marker)]
    resource_text = re.sub(r"^\[gd_scene[^\n]*\]", "[gd_scene format=4]", resource_text, count=1)
    scene_text = resource_text + (
        '[node name="MountainValley" type="Node2D"]\n\n'
        '[node name="Ground" type="TileMapLayer" parent="."]\n'
        f'tile_map_data = PackedByteArray("{pack_layer(ground)}")\n'
        'tile_set = SubResource("TileSet_wvhbo")\n\n'
        '[node name="Objects" type="TileMapLayer" parent="."]\n'
        'z_index = 1\n'
        f'tile_map_data = PackedByteArray("{pack_layer(objects)}")\n'
        'tile_set = SubResource("TileSet_wvhbo")\n\n'
        '[node name="Camera2D" type="Camera2D" parent="."]\n'
        f'position = Vector2({WIDTH * SIZE // 2}, {HEIGHT * SIZE // 2})\n'
        'zoom = Vector2(0.38, 0.38)\n'
    )
    SCENE.write_text(scene_text, encoding="utf-8")


def render_preview(ground, objects):
    atlas = Image.open(ATLAS).convert("RGBA")
    if atlas.size != (512, 512):
        raise ValueError(f"图集尺寸已改变: {atlas.size}")
    canvas = Image.new("RGBA", (WIDTH * SIZE, HEIGHT * SIZE))
    for layer in (ground, objects):
        for (x, y), (ax, ay) in layer.items():
            sprite = atlas.crop((ax * SIZE, ay * SIZE, (ax + 1) * SIZE, (ay + 1) * SIZE))
            canvas.alpha_composite(sprite, (x * SIZE, y * SIZE))
    canvas.save(PREVIEW)


def main():
    source = SOURCE.read_text(encoding="utf-8")
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    metadata, variants = tile_metadata(source)
    ground = make_ground(variants)
    objects, placements = make_objects(catalog)
    expected = set(metadata) | {tuple(cell) for group in catalog["groups"] for cell in group["cells"]}
    used = set(ground.values()) | set(objects.values())
    if used != expected or len(used) != catalog["tile_count"]:
        raise ValueError(f"瓦片未完整使用: {sorted(expected - used)}")
    build_scene(source, ground, objects)
    render_preview(ground, objects)
    print(f"地图 {WIDTH}x{HEIGHT}：地面 {len(ground)} 格，物体 {len(objects)} 格，")
    print(f"使用 {len(used)}/{catalog['tile_count']} 块瓦片，放置 {len(placements)} 个完整物体")
    print(SCENE)
    print(PREVIEW)


if __name__ == "__main__":
    main()
