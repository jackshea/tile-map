"""检查已生成场景的序列化格子、地形接缝和全图集覆盖。"""

import base64
import json
import re
import struct
from pathlib import Path

from build_mountain_valley import CATALOG, HEIGHT, SCENE, SOURCE, WIDTH, tile_metadata


def decode_layer(scene, name):
    block = re.search(
        rf'^\[node name="{name}" type="TileMapLayer" parent="\."[^\]]*\]\n(.*?)(?=\n\[node |\Z)',
        scene,
        re.M | re.S,
    )
    assert block, f"缺少图层 {name}"
    encoded = re.search(r'^tile_map_data = PackedByteArray\("([^"]+)"\)$', block[1], re.M)
    assert encoded, f"缺少图层数据 {name}"
    data = base64.b64decode(encoded[1], validate=True)
    assert len(data) >= 2 and data[-2:] == b"\0\0" and (len(data) - 2) % 12 == 0
    cells = {}
    for offset in range(0, len(data) - 2, 12):
        source_id, x, y, alternative, ax, ay = struct.unpack_from("<hhhhhh", data, offset)
        assert source_id == 0 and alternative == 0, (name, x, y)
        assert 0 <= x < WIDTH and 0 <= y < HEIGHT, (name, x, y)
        assert (x, y) not in cells, (name, x, y)
        cells[x, y] = ax, ay
    return cells


def main():
    scene = SCENE.read_text(encoding="utf-8")
    source = SOURCE.read_text(encoding="utf-8")
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    metadata, _ = tile_metadata(source)
    ground = decode_layer(scene, "Ground")
    objects = decode_layer(scene, "Objects")
    ground_atlas = set(metadata)
    object_atlas = {tuple(cell) for group in catalog["groups"] for cell in group["cells"]}
    assert len(ground) == WIDTH * HEIGHT
    assert set(ground.values()) == ground_atlas, sorted(ground_atlas - set(ground.values()))
    assert set(objects.values()) == object_atlas, sorted(object_atlas - set(objects.values()))
    assert len(ground_atlas | object_atlas) == catalog["tile_count"] == 254
    assert ground_atlas.isdisjoint(object_atlas)
    names = [group["name"] for group in catalog["groups"]]
    assert len(names) == len(set(names)) == 23
    for y in range(HEIGHT):
        for x in range(WIDTH):
            bits = metadata[ground[x, y]]
            if x + 1 < WIDTH:
                right = metadata[ground[x + 1, y]]
                for left_bit, right_bit in (("top_right_corner", "top_left_corner"),
                                            ("bottom_right_corner", "bottom_left_corner")):
                    assert bits[f"terrains_peering_bit/{left_bit}"] == right[f"terrains_peering_bit/{right_bit}"], (x, y, "right")
            if y + 1 < HEIGHT:
                below = metadata[ground[x, y + 1]]
                for top_bit, bottom_bit in (("bottom_left_corner", "top_left_corner"),
                                            ("bottom_right_corner", "top_right_corner")):
                    assert bits[f"terrains_peering_bit/{top_bit}"] == below[f"terrains_peering_bit/{bottom_bit}"], (x, y, "below")
    project = (SCENE.parents[1] / "project.godot").read_text(encoding="utf-8")
    assert 'run/main_scene="res://tile_map/mountain_valley.tscn"' in project
    print(f"通过：地面 {len(ground)} 格，物体 {len(objects)} 格，254/254 块瓦片，四角接缝一致")


if __name__ == "__main__":
    main()
