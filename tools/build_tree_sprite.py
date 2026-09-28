"""把原始出图确定性地制作成 64×96 的透明树木 Sprite。"""

from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/props/source/prop_tree_broadleaf_a_source.png"
ATLAS = ROOT / "assets/tile_map/mountain_landscape.png"
CATALOG = ROOT / "tile_map/terrain_atlas_catalog.json"
OUTPUT = ROOT / "assets/props/prop_tree_broadleaf_a.png"
SIZE = (64, 96)
TILE_SIZE = 32
FOLIAGE_SWATCHES = (
    (0, 3, 1), (2, 17, 5), (8, 27, 9), (8, 41, 13),
    (21, 50, 19), (23, 60, 23), (28, 67, 25), (35, 76, 29),
    (47, 83, 35), (51, 93, 39), (60, 101, 43), (69, 109, 47),
    (78, 116, 53), (86, 122, 56), (96, 128, 61), (116, 145, 73),
    (137, 163, 86), (167, 188, 107), (205, 220, 137),
)
BARK_SWATCHES = (
    (40, 32, 16), (48, 40, 24), (56, 48, 32),
    (72, 64, 32), (80, 72, 40), (96, 88, 72),
)


def nearest(rgb: tuple[int, int, int], palette: tuple[tuple[int, int, int], ...]) -> tuple[int, int, int]:
    return min(palette, key=lambda color: sum((a - b) ** 2 for a, b in zip(rgb, color)))


def tree_palette(atlas_path: Path, catalog_path: Path) -> tuple[tuple[tuple[int, int, int], ...], tuple[tuple[int, int, int], ...]]:
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    group = next((item for item in catalog["groups"] if item["name"] == "阔叶树"), None)
    if group is None:
        raise ValueError("图集目录缺少阔叶树区域")
    x, y = group["origin"]
    width, height = group["size"]
    box = (x * TILE_SIZE, y * TILE_SIZE, (x + width) * TILE_SIZE, (y + height) * TILE_SIZE)
    with Image.open(atlas_path) as atlas:
        colors = {
            rgba[:3] for rgba in atlas.convert("RGBA").crop(box).get_flattened_data()
            if rgba[3] >= 240 and max(rgba[:3]) < 245
        }
    greens = tuple(sorted(color for color in colors if color[1] >= color[0] * 1.04 and color[1] >= color[2] * 1.2))
    browns = tuple(sorted(color for color in colors if color[0] > color[1] * 1.05 and color[1] > color[2] * 1.05))
    if not greens or not browns:
        raise ValueError("原图集树木区域缺少可用的绿/棕色值")
    return greens, browns


def remove_glow(image: Image.Image) -> Image.Image:
    """用明亮枝叶/树干提取主体，并清掉出图工具留下的不透明光晕。"""
    width, height = image.size
    if width % SIZE[0] or height % SIZE[1] or width // SIZE[0] != height // SIZE[1]:
        raise ValueError(f"原始画布须为 64×96 的整数倍，实际为 {width}×{height}")
    sprite = image.convert("RGBA").resize(SIZE, Image.Resampling.NEAREST)
    pixels = sprite.load()
    core = Image.new("L", SIZE)
    mask_pixels = core.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = pixels[x, y]
            leaf = green >= 58 and green >= red * 1.05 and green >= blue * 1.3
            bark = y >= 58 and red >= 57 and red >= green * 1.10 and green >= blue * 1.15
            mask_pixels[x, y] = 255 if alpha > 200 and (leaf or bark) else 0

    # 扩出一像素暗边；只填主体包围的洞，不填开放的树枝间隙。
    mask = core.filter(ImageFilter.MaxFilter(3))
    mask_pixels = mask.load()
    outside: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()
    for x in range(SIZE[0]):
        for y in (0, SIZE[1] - 1):
            if mask_pixels[x, y] == 0:
                queue.append((x, y))
    for y in range(SIZE[1]):
        for x in (0, SIZE[0] - 1):
            if mask_pixels[x, y] == 0:
                queue.append((x, y))
    while queue:
        x, y = queue.popleft()
        if (x, y) in outside or mask_pixels[x, y]:
            continue
        outside.add((x, y))
        for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            nx, ny = neighbor
            if 0 <= nx < SIZE[0] and 0 <= ny < SIZE[1] and neighbor not in outside and not mask_pixels[nx, ny]:
                queue.append(neighbor)

    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            if mask_pixels[x, y] or (x, y) not in outside:
                red, green, blue, _ = pixels[x, y]
                pixels[x, y] = (red, green, blue, 255)
            else:
                pixels[x, y] = (0, 0, 0, 0)
    return sprite


def build_sprite(source_path: Path = SOURCE, atlas_path: Path = ATLAS, catalog_path: Path = CATALOG) -> Image.Image:
    with Image.open(source_path) as original:
        sprite = remove_glow(original)
    greens, browns = tree_palette(atlas_path, catalog_path)
    pixels = sprite.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = pixels[x, y]
            if not alpha:
                continue
            is_bark = red > green * 1.10 and green > blue * 1.15
            swatch = nearest((red, green, blue), BARK_SWATCHES if is_bark else FOLIAGE_SWATCHES)
            source_palette = browns if swatch[0] > swatch[1] * 1.05 and swatch[1] > swatch[2] * 1.05 else greens
            pixels[x, y] = (*nearest(swatch, source_palette), 255)
    return sprite


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE, help="保留的原始出图 PNG")
    parser.add_argument("--atlas", type=Path, default=ATLAS, help="项目原图集")
    parser.add_argument("--catalog", type=Path, default=CATALOG, help="图集区域目录")
    parser.add_argument("--output", type=Path, default=OUTPUT, help="输出的 64×96 PNG")
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve():
        parser.error("输出路径不能覆盖原始出图")
    sprite = build_sprite(args.source, args.atlas, args.catalog)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sprite.save(args.output)
    print(f"已生成 {args.output}：{sprite.width}×{sprite.height}，透明背景")


if __name__ == "__main__":
    main()
