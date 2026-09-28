"""Generate an explicit ownership manifest for the original 32px atlas.

Only writes the manifest, never edits pixels or scene data.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
scene = (ROOT / 'tile_map/terrain.tscn').read_text(encoding='utf-8')
tiles = {tuple(map(int, pair)) for pair in re.findall(r'^(\d+):(\d+)/0 =', scene, re.M)}
ground = {tuple(map(int, pair)) for pair in re.findall(r'^(\d+):(\d+)/0/terrain_set = 0$', scene, re.M)}
groups = []
assigned = set(ground)


def group(name, rect):
    x, y, w, h = rect
    cells = sorted((a, b) for a in range(x, x+w) for b in range(y, y+h) if (a, b) in tiles)
    assert not (set(cells) & assigned), (name, set(cells) & assigned)
    assigned.update(cells)
    groups.append(dict(name=name, origin=[x, y], size=[w, h], cells=cells))


group('草丛-高草', (4, 9, 3, 3))
group('草丛-矮草', (6, 7, 2, 2))
group('草丛-长叶', (10, 5, 1, 2))
group('蕨叶', (7, 9, 1, 1))
group('花丛', (7, 10, 1, 1))
group('草丛-小簇', (8, 10, 1, 1))
group('山地-圆顶', (0, 0, 6, 7))
group('山地-横向', (6, 0, 4, 7))
group('山地-陡坡', (0, 7, 4, 5))
group('山地-崖壁', (0, 12, 4, 4))
group('山洞', (14, 10, 2, 4))
group('篝火', (4, 7, 2, 2))
group('阔叶树', (4, 12, 3, 4))
group('针叶树-小', (7, 13, 2, 3))
group('针叶树-大', (9, 12, 2, 4))
group('岩石-立石', (11, 12, 1, 2))
group('岩石-碎石', (12, 12, 1, 1))
group('岩石-尖石', (13, 12, 1, 2))
group('岩石-深色', (12, 13, 1, 2))
group('岩石-宽石', (11, 14, 1, 2))
group('岩石-扁石', (12, 15, 1, 1))
group('石堆', (13, 14, 1, 2))
group('石制圆盘', (14, 14, 2, 2))

assert assigned == tiles, ('unassigned', tiles-assigned, 'extra', assigned-tiles)
out = ROOT / 'tile_map/terrain_atlas_catalog.json'
out.write_text(json.dumps(dict(tile_count=len(tiles), ground_count=len(ground), groups=groups),
                          ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(f'{len(ground)} ground tiles + {sum(len(g["cells"]) for g in groups)} classified tiles = {len(tiles)}; {len(groups)} patterns')
