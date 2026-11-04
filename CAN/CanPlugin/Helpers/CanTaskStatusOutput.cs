using CAN.Models;
using xTestPlatform.Core.Engine;

namespace CAN.Helpers;

/// <summary>One public output contract shared by the editor and executor; never returns plugin-private DTOs.</summary>
public static class CanTaskStatusOutput
{
    public static void ValidateTarget(CanTaskStatusSetting setting, IExecutionContext context)
    {
        string path = setting.ResultVariable;
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.IndexOf('.') <= 0 || !context.HasVariable(path))
            throw new ArgumentException("任务状态输出变量必须已声明并填写完整路径，例如 Locals.CanTasks");
        int dot = path.IndexOf('.');
        var scope = path[..dot].ToLowerInvariant() switch
        {
            "locals" => context.Locals, "fileglobals" => context.FileGlobals,
            "projectglobals" => context.ProjectGlobals, "parameters" => context.Parameters,
            "runstate" => context.RunState, _ => null
        };
        string? declared = scope?.GetVariableDefinition(path[(dot + 1)..])?.DataType.ToString();
        object? current = context.GetVariable(path);
        // A JSON string has one declared destination type; never replace list/object variables implicitly.
        if (declared != "String" || current is not (null or string))
            throw new ArgumentException("JSON 输出需要已声明的 String 字符串变量，不能写入 List、ListDynamic 或其他列表变量；请更换输出变量路径。");
    }
}
