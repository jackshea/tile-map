# 设计图与生产资源提示词

只读取并展开本次所需模板。先替换所有花括号变量；记录资源 ID、参考图、规格版本、实际提示词与输出路径，方便复用和返修。

## 共用风格块

```text
2D pixel art for a Godot game, {view_and_projection}, {theme_and_mood}.
Base map grid: 32x32 logical pixels per tile. {pixel_density_and_scale}.
Palette: {hex_colors_and_roles}. Light comes from {light_direction}.
{outline_and_material_rules}. Match the supplied style reference {reference}.
Clear silhouettes and readable interactive objects at native game scale.
{asset_specific_constraints}
```

没有参考图时删除参考图句子，不声称已提供。全套请求共享这段设定；单条交付给独立出图工具时，把风格块展开，使该提示词可以单独执行。中文交付说明列出尺寸、用途与仍需验证的项目。

## 设计图：允许说明性排版

```text
Create a concept art board for {scene_or_asset_family}, using the style rules above.
Show {requested_views_and_elements}, coherent proportions relative to a 32x32 tile.
Include {palette_swatch_or_scale_reference_if_needed}.
Communicate {gameplay_readability_and_modular_relationships}.
This is a visual design reference, not a production sprite atlas.
```

按请求选择场景总览、建筑组合、角色设定、物件展示或 UI 草图，不默认生成整套七张。说明文字和标尺可以放在设计板上，不能混入生产 PNG。

## 单体道具 / 建筑模块

```text
Create a single production-candidate pixel-art asset: {asset_id_and_description}.
Target logical canvas: {width}x{height} px. {view_and_orientation}.
Foot contact anchor: ({anchor_x}, {anchor_y}), measured from the top-left.
Gameplay footprint: {footprint}; visual overhang: {overhang}.
Show only {state_and_required_parts}. {module_edge_constraints}.
{transparent_background_or_required_opaque_fill}.
{shadow_policy}. No surrounding scenery, labels, grid lines, or cropped silhouette.
```

大物件分别指定画布与占地；需要分层时逐层说明共享画布、锚点及合成顺序。不要在透明背景上额外画地面底座，除非规格本来需要。

## 地形与 Tile Sheet

```text
Create {terrain_material_or_candidate_atlas} in the shared pixel-art style.
Target tile region: 32x32 logical pixels.
Terrain foreground: {foreground}; background: {background_or_transparency}.
{seam_and_transition_requirements}.
For an atlas, use {columns} columns x {rows} rows, {margins_and_spacing},
target canvas {sheet_width}x{sheet_height} px, ordered by {layout_mapping}.
Keep compatible boundary pixels consistent across the specified variants.
No text, guide grid, decorative frame, cast shadow outside the specified tile,
or objects spanning cells unless explicitly defined as a multi-tile module.
```

精确拓扑优先复用已验证图集与布局。AI 可以提供材质/方向候选，不假设一次生成就能保证 47 个掩码全对。复杂图集先拿少量中心/边角小样校验，再批量扩展；精确切片、掩码与打包使用当前工具允许的流程。需要像素后处理时遵守所用图像工具的编辑边界，不擅自切换到脚本重绘。

## 角色 Sprite Sheet

```text
Create a production-candidate sprite sheet of {character}, action {action}.
Target frame canvas: {frame_width}x{frame_height} px; fixed foot anchor {anchor}.
Rows: {direction_order}; columns: {frames_per_direction} time-ordered frames.
Target sheet canvas: {sheet_width}x{sheet_height} px, {margins_and_spacing}.
Keep body proportions, clothing, equipment, lighting, and scale consistent.
{pose_progression_and_loop_constraints}. True transparent background.
No labels, grid lines, extra poses, cropped equipment, or drift between frames.
```

先用单角色/单动作建立一致性，避免把多个角色和动作混成一张不可验证的大图。镜像方案要核对光源、左右手装备与非对称服饰。播放速度写入规格，图片提示词本身不会生成 Godot 动画时序。

## UI / VFX

UI 写明目标显示尺寸、交互状态、图标安全边距；可拉伸面板需要定义边角固定区。VFX 写明帧尺寸、时序、循环、透明与混合需求。两者都不继承“必须 32×32”的限制；共用色板和视觉层级即可。

## 出图后的交付边界

图片工具可能返回比目标更大的画布或不精确的格线布局；实测后才能宣称满足规格。不要把直接缩小的插画当作合格原生像素图，也不要把棋盘纹理当 alpha。

实际生成时展示或链接结果，标明“设计图”或“待验收生产候选”；按[资源验收](asset-spec.md)检查后更新状态。若仅要求提示词，则交付提示词即可，不编造生成或验证结果。
