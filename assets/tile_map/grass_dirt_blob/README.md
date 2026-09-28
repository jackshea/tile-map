# 草地 / 干土 Blob 地形

独立资源，不引用原来的 mountain_landscape.png 或 terrain.tscn。

- `grass_dirt_blob_47.png`：256×192，8 列×6 行，无间距，每格 32×32。
- `grass_dirt_blob.tres`：已配置的 Godot TileSet，Match Corners and Sides。
- 前 47 格为草地背景上的干土地形，最后一格 `(7,5)` 为纯草地。
- 地形集 0：地形 0 = 草地，地形 1 = 干土。
- `mask_catalog.json`：47 块的坐标及掩码；按 N、NE、E、SE、S、SW、W、NW 对应 bit 0–7。
- 对角连接仅在相邻两条边均连接时有效，256 种二进制组合归并为 47 种合法 Blob 形状。

## 使用

打开 `res://tile_map/grass_dirt_blob_demo.tscn`，选择 Terrain 节点，在 TileMap 地形面板选择干土并使用连接工具绘制。需要清除干土时重新绘制草地。

新建地图时，把 `.tres` 赋给 TileMapLayer 的 Tile Set，先用纯草地 `(7,5)` 填满底图，再绘制干土。外部背景是草地，不是透明空白；本套资源没有草地到透明的边界。TileMapLayer 的 Texture Filter 使用 Nearest。

也可以将整个目录复制到另一 Godot 项目的相同资源路径后使用；改变目录时应由编辑器更新 `.tres` 中的图片路径。

## 构建与验证

图片材质由内置 imagegen 生成，最终提示词见 `generation_prompt.txt`；GDScript 按精确的几何连接掩码组装图集。生成工具只用于离线构建，演示场景及 TileSet 不依赖运行时脚本。

在项目根目录运行（godot 替换成本机引擎路径）：

```text
godot --headless --path . --script tools/build_grass_dirt_blob.gd
godot --headless --editor --path . --import
godot --headless --path . --script tools/verify_grass_dirt_blob.gd
```

构建脚本会重建此目录中的图集、TileSet 和掩码表。验证脚本还会重建演示场景及 preview.png。手动修改这些生成文件前请另存副本。

验证覆盖：全部 47 种形状的 Godot 自动选块、47 次重新绘制草地，以及 1036 对兼容边缘的逐像素接缝检查。配置只包含绘制地形，不包含碰撞或导航。
