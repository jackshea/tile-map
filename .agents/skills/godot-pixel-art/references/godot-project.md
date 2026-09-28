# Godot 接入与本仓库约定

接入现有资源、修改 TileSet、验收或规划沿用当前美术时读取。以下是创建 Skill 时的项目观察，使用前以当前文件为准，不把统计值当永久断言。

## 最少读取路径

- [project.godot](../../../../project.godot)：当前声明 Godot 4.7，使用 `canvas_items` 拉伸；声明版本不代表已验证本机引擎版本。
- [terrain_atlas_guide.md](../../../../tile_map/terrain_atlas_guide.md)：原图集分类、Pattern、示例地图与重建行为。
- [grass_dirt_blob/README.md](../../../../assets/tile_map/grass_dirt_blob/README.md)：独立草/土地形、掩码定义、构建和验证行为。

只扩展其中一套资源时继续读取那套文档和实际场景/图集即可；不要默认扫描所有 PNG 或生成缓存。

## 两套资源的不同契约

| 资源 | 当前组织 | 扩展边界 |
| --- | --- | --- |
| `tile_map/terrain.tscn` | 32×32，地形集 0 使用 Match Corners；文档列出 78 个地面切片，其他 176 块通过 23 个完整 Pattern 组合 | 树木、山体等不具备任意形状地形连接所需的全套边角；保留已有 atlas 坐标和地形归属 |
| `assets/tile_map/grass_dirt_blob/grass_dirt_blob.tres` | 32×32，8×6 无间距图集；47 块干土 Blob + 1 格纯草；Match Corners and Sides | 背景为草地；没有草地到透明的边界，未配置碰撞或导航 |

原山谷示例 `tile_map/mountain_valley.tscn` 有自己的 TileSet 副本；修改源 `terrain.tscn` 不应假设会自动同步示例。原图集用 `Ground/Objects` 分层；Blob 演示层已显式设置 Nearest。其他资源是否采用最近邻应逐项核实。

## 接入决定

- 本项目沿用 `TileMapLayer` 与 `TileSet`。迁移到别的 Godot 项目时按目标版本核对 API，不为美术规划擅自升级工程。
- 地形选块依赖 terrain set 的匹配模式及邻接配置；新增自动连接范围需要同时补素材和规则。正式规则参见 [TileSet](https://docs.godotengine.org/en/stable/classes/class_tileset.html) 与 [TileMapLayer 地形连接](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html#class-tilemaplayer-method-set-cells-terrain-connect)。
- 像素风需要核对 CanvasItem/项目继承后的实际纹理过滤。最近邻由 [CanvasItem.texture_filter](https://docs.godotengine.org/en/stable/classes/class_canvasitem.html#class-canvasitem-property-texture-filter) 控制；不能仅写成“PNG 导入关闭过滤”并认定已生效。mipmap、缩放和相机策略按显示需求检查。
- 采用整数倍显示检查像素边缘；`canvas_items` 本身不证明所有窗口尺寸下整数缩放。未请求调整全局显示策略时只报告差异。
- 地面、按脚底排序的物件、固定前景按行为分层。树木或房屋若需要与角色互相遮挡，应检验排序原点与共同排序关系，不能把所有树冠永远放在角色前面。
- 只有需要交互/独立生命周期的物件才采用场景实例。绘制地形不自动获得碰撞、导航或交互配置。

## 执行前检查写入范围

先运行 `git status --short` 并读取相关脚本。此仓库部分名为 verify 的脚本也会写文件；不要因名字包含“验证”就视为只读。

| 命令/入口（在项目根目录执行） | 当前已知行为 |
| --- | --- |
| `python tools/classify_mountain_atlas.py` | 重新生成分类相关产物；先检查脚本输出路径 |
| `godot --headless --path . --script tools/configure_mountain_atlas.gd` | 修改地形归属并创建/更新 Pattern；手工调整可能被覆盖 |
| `godot --headless --path . --script tools/verify_mountain_atlas.gd` | 检查原图集分类与 Pattern；运行前确认当前实现 |
| `python tools/build_mountain_valley.py` | 覆盖山谷场景与预览，可能抹掉手工地图布局 |
| `python tools/verify_mountain_valley.py` | 检查地图产物；运行前确认当前实现 |
| `godot --headless --path . --script tools/build_grass_dirt_blob.gd` | 重建 Blob PNG、TileSet、掩码表 |
| `godot --headless --path . --script tools/verify_grass_dirt_blob.gd` | 除验证外还写入演示场景与预览 |

已有手工改动时选择隔离副本、非覆盖输出或直接检查产物；无法保留且必须覆盖时才向用户说明具体冲突。只编写美术规划或 Skill 不需要执行这些构建命令。

`godot` 是占位命令名，执行时先确认实际可用的引擎路径和版本。接入任务按需使用 `godot --headless --editor --path . --import` 检查导入并读取日志；没有引擎时报告未验证，不自行安装。

验收时分别记录：静态规格、自动选块/边缘检查、导入结果、运行画面与交互。已有验证工具覆盖什么就报告什么；尤其不能用图集接缝通过推断运行时遮挡、碰撞或动画也通过。
