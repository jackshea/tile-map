"""验证树木资源规格，并确认已交付 PNG 可由原始出图重建。"""

from PIL import Image

from build_tree_sprite import ATLAS, CATALOG, OUTPUT, ROOT, SIZE, SOURCE, build_sprite, tree_palette


SCENE = ROOT / "assets/props/prop_tree_broadleaf_a.tscn"


def main() -> None:
    with Image.open(OUTPUT) as file:
        if file.size != SIZE or file.mode != "RGBA":
            raise AssertionError(f"PNG 规格不符：{file.size} {file.mode}")
        delivered = file.copy()
    pixels = list(delivered.get_flattened_data())
    alphas = {pixel[3] for pixel in pixels}
    if alphas != {0, 255}:
        raise AssertionError(f"透明通道应为二值：{sorted(alphas)}")
    if any(pixel[:3] != (0, 0, 0) for pixel in pixels if pixel[3] == 0):
        raise AssertionError("透明区有残留 RGB 光晕")
    bbox = delivered.getchannel("A").getbbox()
    if bbox is None or not (0 < bbox[0] < bbox[2] < 64 and 0 < bbox[1] < bbox[3] <= 88):
        raise AssertionError(f"树木轮廓越界或没有预留脚底：{bbox}")
    greens, browns = tree_palette(ATLAS, CATALOG)
    source_colors = set(greens) | set(browns)
    if any(pixel[:3] not in source_colors for pixel in pixels if pixel[3]):
        raise AssertionError("存在原图集树木区域之外的颜色")
    rebuilt = build_sprite(SOURCE, ATLAS, CATALOG)
    if rebuilt.tobytes() != delivered.tobytes():
        raise AssertionError("交付 PNG 与构建脚本的结果不一致")
    scene = SCENE.read_text(encoding="utf-8")
    for required in (
        'path="res://assets/props/prop_tree_broadleaf_a.png"',
        "metadata/footprint_tiles = Vector2i(1, 1)",
        "metadata/foot_anchor_px = Vector2i(32, 88)",
        "position = Vector2(-32, -88)",
        "texture_filter = 1",
        "centered = false",
    ):
        if required not in scene:
            raise AssertionError(f"场景缺少必要配置：{required}")
    print(f"通过：{SIZE[0]}×{SIZE[1]}、透明背景、原图集色值、1×1 占地、脚底锚点和确定性重建；轮廓 {bbox}")


if __name__ == "__main__":
    main()
