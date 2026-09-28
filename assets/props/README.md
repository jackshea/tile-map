# 阔叶树 A

- `prop_tree_broadleaf_a.png`：64×96 RGBA 独立 Sprite，透明背景，原生像素尺寸；颜色均取自 `assets/tile_map/mountain_landscape.png` 的阔叶树区域。
- `prop_tree_broadleaf_a.tscn`：Godot 4.7 场景，以树干脚底为根节点原点，Sprite 使用最近邻过滤。可实例化到地图物件层；父节点启用 Y 排序时按脚底排序。
- `source/prop_tree_broadleaf_a_source.png`：保留的原始出图，仅供离线重建；`source/.gdignore` 防止 Godot 导入原始大图。
- 逻辑占地：1×1 个 32×32 地图格。画布覆盖 2×3 图像格，不等于占地。
- 脚底锚点：从 PNG 左上角起算 `(32, 88)`；可见像素范围约为 `x=1..62, y=7..87`。
- 未配置碰撞、导航或交互；如需树干阻挡，应在落点附近添加小范围碰撞，不要把整张树冠作为碰撞框。

生成提示词和透明度修正说明见 `generation_prompt.txt`。本次已完成 PNG 静态规格检查；本机未找到 Godot 可执行文件，场景导入和运行效果尚未验证。

## 重建与检查

在项目根目录安装 Pillow 后运行：

```text
python tools/build_tree_sprite.py
python tools/verify_tree_sprite.py
```

构建脚本会覆盖 `prop_tree_broadleaf_a.png`，只在需要重建时运行。验证脚本不修改资源，会重新计算像素并与交付 PNG 比较。
