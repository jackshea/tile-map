# mountain_landscape 图集分类与使用

`terrain.tscn` 保留原有 254 块瓦片的切片坐标。只有 78 块地面归属地形集 0，用于 Match Corners 自动连接；其余 176 块草丛、山体与装饰物没有地形归属，以 23 个完整 Pattern 放置。

| 用途 | 内容 | 瓦片数 | 放置方式 |
| --- | --- | ---: | --- |
| 地形集 0 | 草、浅地、深地、石路、石砖路 | 78 | 地形笔刷自动连接 |
| 草丛 Pattern | 高草、矮草、长叶、蕨叶、花丛、小草簇 | 18 | 完整图案盖章 |
| 山体 Pattern | 圆顶山地、横向山地、陡坡、崖壁、山洞 | 112 | 完整图案盖章 |
| 装饰 Pattern | 篝火、阔叶树、大小针叶树、六种岩石、石堆、石制圆盘 | 46 | 完整图案盖章 |

## 放置草丛、山地、树木等

1. 打开 `terrain.tscn` 并选择 `TileMapLayer`。
2. 在下方 **TileMap → 图案 / Patterns** 中选择完整物体缩略图。
3. 在地图上点击放置。23 个图案保存在当前 TileSet 中。

图案包括高草、矮草、长叶、蕨叶、花丛、小草簇、圆顶山地、横向山地、陡坡、崖壁、山洞、篝火、阔叶树、小针叶树、大针叶树、立石、碎石、尖石、深色岩石、宽石、扁石、石堆、石制圆盘。

这些物体的素材不能支持任意形状自动拼接，地形连接笔刷可能选择错误碎片。若将来需要可自动连接的山体，须补绘可重复的边缘、转角、斜向和不同高度衔接素材，再单独设计地形规则。

## 维护与验证

`terrain_atlas_catalog.json` 列出各物体的名称、原图坐标和完整切片范围。分类信息由目录与 Pattern 名称维护，不占用 Godot 地形集。

```text
python tools/classify_mountain_atlas.py
godot --headless --path . --script tools/configure_mountain_atlas.gd
godot --headless --path . --script tools/verify_mountain_atlas.gd
```

配置脚本会清除物体切片的地形归属、移除地形集 1～3，并创建或更新相应 Pattern；地形集 0 的地面配置保持不变。重新配置前请备份手工调整过的 Pattern。

验证覆盖：254 块瓦片、78 块地面归属、176 块物体无地形归属，以及 23 个 Pattern 的原始切片坐标与盖章结果。山体任意形状自动拼接不在验收范围内。

## 山间遗迹示例地图

`mountain_valley.tscn` 是 64×52 格的完整地图，并已设置为项目入口。`Ground` 图层使用现有的四角地形过渡铺设草地、土路、岩地和石铺地；`Objects` 图层使用完整图案放置山体、洞口、营火、树木与装饰物。洞口在北，营地居中，林地与遗迹分列东西，南侧道路作为入口。

地图使用图集中全部 254 块切片，每一块至少出现一次。源 `terrain.tscn` 和原始图集不参与地图生成时的修改；地图场景内包含独立的 TileSet 副本，便于直接在编辑器中继续调整。

```text
python tools/build_mountain_valley.py
python tools/verify_mountain_valley.py
```

此命令会重新生成 `mountain_valley.tscn` 和 `mountain_valley_preview.png`，覆盖地图场景中的手工布局改动；已手工修改地图时不要直接运行。瓦片图集无法证明设计师原始关卡的精确布局，这张地图是按现有素材语义完成的地图设计。
