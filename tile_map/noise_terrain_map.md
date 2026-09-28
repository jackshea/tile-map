# 柏林噪声地图

在 Godot 中打开 `res://tile_map/noise_terrain_map.tscn` 并运行场景，或直接运行当前项目。

地图逻辑和场景脚本均使用 C#，需要 Godot .NET 4.7.2 与 .NET 8 SDK。命令行可运行 `dotnet build TileMap.csproj` 编译；验证场景是 `res://tools/verify_noise_terrain_map.tscn`。

地图为 1024×1024 格，每格 32×32 像素。多个尺度的柏林噪声组合出陆地、海岸与细节；黄沙始终沿水陆边界生成。在场景根节点的检查器中调整参数：

| 参数 | 默认值 | 调大后的效果 |
| --- | ---: | --- |
| `InitialSeed` | `12345` | 换一张可复现的地图 |
| `NoiseFrequency` | `0.003` | 地形块更小、重复更多 |
| `WaterThreshold` | `0` | 水域面积增大 |
| `ContinentScale` | `0.45` | 大陆轮廓更密集 |
| `ContinentWeight` | `0.62` | 大块地形更占主导，细碎海岸减少 |
| `CoastWidth` | `2` | 黄沙海岸带更宽，最多 32 格 |
| `EdgeFalloff` | `0` | 边缘更容易成为海水，形成岛屿布局 |
| `DetailScale` | `2.5` | 小尺度细节更密集 |
| `DetailWeight` | `0.09` | 海岸细节更明显 |
| `WarpScale` | `0.7` | 海岸弯曲的间距更短 |
| `WarpStrength` | `0.18` | 海岸线弯曲更明显 |
| `IterationStrength` | `1` | 每次“迭代”做更多轮邻域平滑 |

`ContinentWeight` 与 `DetailWeight` 之外的权重自动分配给中尺度海岸噪声。想要较平滑的大块陆地，可降低 `NoiseFrequency`、`DetailWeight` 和 `WarpStrength`；想要岛屿，可提高 `EdgeFalloff`。修改检查器参数后，运行场景或点击 **应用参数** 查看结果。

“视角”分组还开放 `DetailZoom`（切换为原始瓦片的缩放倍率）、`WheelZoomStep`（每格滚轮的缩放倍数）和 `MaxZoom`（最大缩放倍率）。运行前可在本地检查器修改；运行中可在远程检查器修改，生成参数需点击 **应用参数** 才会重建地图。

打开场景会自动将整张地图放进视口。滚轮以鼠标位置为中心缩放，右键或中键拖动可平移视角，**全图**按钮可恢复整图视角。整图视角显示轻量预览；放大后显示当前视野内的原始瓦片。

运行时按钮：

- **重新生成**：使用新种子生成整张地图，覆盖当前未保存的地图。
- **应用参数**：保留检查器中的种子，按当前参数重新生成，便于比较；会覆盖尚未保存的迭代结果。
- **迭代**：以当前水陆格局为输入，对每格的相邻地形做一轮或多轮平滑，再重建海岸带。
- **保存／加载**：使用单个 `user://noise_terrain_map.json` 存档。保存包含生成参数和完整水陆逻辑格，黄沙由海岸规则准确重建；加载前会校验数据，失败时不会替换当前地图。仍可读取此前保存的 1024×1024 旧格式地图。

脚本可调用场景根节点的 `GetTerrainAt(new Vector2I(x, y))`。返回值为 `NoiseTerrainMapData.Terrain.Water`（0）、`Sand`（1）或 `Grass`（2）；越界返回 `-1`。逻辑数据在根节点的 `MapData` 属性中。
