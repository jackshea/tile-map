#if TOOLS
using Godot;

/// <summary>
/// 编辑器插件入口，注册 <see cref="ParamDocsInspector"/>。
/// 整个文件只在编辑器（Debug 构建，定义 TOOLS 且引用 GodotSharpEditor）中编译，
/// 导出/Release 构建不包含编辑器代码，运行时行为不受影响。
/// </summary>
[Tool]
public partial class ParamDocsPlugin : EditorPlugin
{
    private ParamDocsInspector? _inspector;

    public override void _EnterTree()
    {
        _inspector = new ParamDocsInspector();
        AddInspectorPlugin(_inspector);
    }

    public override void _ExitTree()
    {
        if (_inspector is null)
        {
            return;
        }

        RemoveInspectorPlugin(_inspector);
        _inspector = null;
    }
}
#endif
