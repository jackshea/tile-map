#if TOOLS
using System.Collections.Generic;
using Godot;

/// <summary>
/// 在检查器里为 <see cref="NoiseTerrainMap"/> 的导出参数补一行中文说明。
///
/// 为什么不直接用参数悬停提示：Godot 4.7.2 的 <c>CSharpScript::get_documentation()</c>
/// 仍是 TODO（见 modules/mono/csharp_script.h），C# 脚本无法向检查器提供脚本文档，
/// 悬停提示的正文只能回落成“暂无可用描述”。因此这里在原生参数行下面追加一个只读 Label：
/// 该控件不是 EditorProperty，且以 <c>add_to_end</c> 方式加入，所以不会取代原生编辑器，
/// 参数本身照常编辑、撤销与 Revert。
/// </summary>
[Tool]
public partial class ParamDocsInspector : EditorInspectorPlugin
{
    private const string TargetScriptPath = "res://tile_map/NoiseTerrainMap.cs";

    private static readonly Dictionary<string, string> Descriptions = new()
    {
        // 基础
        ["InitialSeed"] = "地图随机种子：相同种子与参数会生成完全相同的地图，换一个种子就是换一张地图。",
        ["NoiseFrequency"] = "噪声频率：调大后地形块更小、重复更多，调小则地形更大更平滑。",
        ["WaterThreshold"] = "水位阈值：调大后水域面积增大、陆地减少，调小则陆地更多。",

        // 大陆与海岸
        ["ContinentScale"] = "大陆尺度：大陆噪声的频率倍数，调大后大陆轮廓更密集、地块更小。",
        ["ContinentWeight"] = "大陆权重：调大后大块陆地更占主导、细碎海岸减少；其余权重自动分配给中尺度海岸噪声。",
        ["CoastWidth"] = "黄沙海岸宽度（格）：黄沙始终沿水陆边界生成，调大后海岸带更宽，最多 32 格。",
        ["EdgeFalloff"] = "边缘衰减：调大后地图边缘更容易成为海水，形成岛屿布局；0 表示不衰减。",

        // 地形细节
        ["DetailScale"] = "细节尺度：细节噪声的频率倍数，调大后小尺度细节更密集。",
        ["DetailWeight"] = "细节权重：调大后海岸细节更明显，过大会让海岸变得细碎。",
        ["WarpScale"] = "扭曲尺度：海岸扭曲噪声的频率倍数，调大后海岸弯曲的间距更短。",
        ["WarpStrength"] = "扭曲强度：调大后海岸线弯曲更明显；0 表示海岸线不做扭曲。",

        // 迭代
        ["IterationStrength"] = "迭代轮数：点击“迭代”时对每格执行的邻域平滑轮数，调大后地形更平滑。",

        // 视角
        ["DetailZoom"] = "细节缩放：视角倍率低于该值只显示整图预览，高于该值才绘制原始瓦片。",
        ["WheelZoomStep"] = "滚轮步长：每滚一格视角放大的倍数。",
        ["MaxZoom"] = "最大缩放：视角放大倍率的上限。",
    };

    public override bool _CanHandle(GodotObject @object) => IsTarget(@object);

    // 编辑器里非 [Tool] 脚本只会有占位脚本实例，不能用 `is NoiseTerrainMap` 判断，必须看脚本路径。
    private static bool IsTarget(GodotObject @object)
    {
        if (@object is NoiseTerrainMap)
        {
            return true;
        }

        Resource? script = @object.GetScript().As<Resource>();
        return script is not null && script.ResourcePath == TargetScriptPath;
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (!Descriptions.TryGetValue(name, out string? description))
        {
            return false;
        }

        // 追加到末尾：说明行排在原生参数行下面。
        AddPropertyEditor(name, CreateHint(description), true);
        return false; // 不独占：内置插件继续创建原生编辑器。
    }

    private static Control CreateHint(string text)
    {
        Label hint = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        // 进入场景树后才能取到编辑器主题：沿用检查器的字体，并用禁用色弱化这行提示。
        hint.TreeEntered += () => ApplyEditorTheme(hint);
        return hint;
    }

    private static void ApplyEditorTheme(Label hint)
    {
        // 编辑器主题项取不到时保持 Label 默认样式，避免覆盖成不可读的颜色。
        if (hint.HasThemeFont("font", "Tree"))
        {
            hint.AddThemeFontOverride("font", hint.GetThemeFont("font", "Tree"));
        }

        if (hint.HasThemeFontSize("font_size", "Tree"))
        {
            hint.AddThemeFontSizeOverride("font_size", hint.GetThemeFontSize("font_size", "Tree"));
        }

        if (hint.HasThemeColor("font_disabled_color", "Editor"))
        {
            hint.AddThemeColorOverride("font_color", hint.GetThemeColor("font_disabled_color", "Editor"));
        }
    }
}
#endif
