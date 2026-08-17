using System.Collections.ObjectModel;
using Http.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http.UI.Validation;

/// <summary>
/// HTTP 编辑器共用的校验辅助方法
/// </summary>
internal static class HttpEditorValidationHelper
{
    /// <summary>校验可选的输出变量：留空跳过，否则要求变量存在且类型匹配</summary>
    public static void CheckVariable(
        StepEditorValidationContext context, string variableName, Type expected, string code, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(variableName)) return;

        if (!context.ExecutionContext.HasVariable(variableName))
        {
            errors.Add(StepSettingError.Error(code, $"变量 {variableName} 不存在，请先创建该变量"));
            return;
        }

        var val = context.ExecutionContext.GetVariable(variableName);
        if (val is not null && val.GetType() != expected)
            errors.Add(StepSettingError.Error($"{code}T",
                $"变量 {variableName} 类型不匹配，期望 {expected.Name}，实际类型 {val.GetType().Name}"));
    }

    /// <summary>校验请求头集合：名称不可为空，值表达式必须合法</summary>
    public static void CheckHeaders(
        StepEditorValidationContext context, ObservableCollection<HttpHeaderItem> headers, string code, List<StepSettingError> errors)
    {
        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Name))
            {
                errors.Add(StepSettingError.Error(code, "请求头中存在名称为空的行"));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(header.Value) &&
                !context.Evaluator.ValidateExpression(header.Value, context.ExecutionContext, out var err))
                errors.Add(StepSettingError.Error($"{code}E", $"请求头 {header.Name} 的值表达式无效: {err}"));
        }
    }

    /// <summary>校验提取映射集合：路径与目标变量不可为空，且不能重复写入同一变量</summary>
    public static void CheckExtractItems(
        ObservableCollection<HttpExtractItem> items, string pathLabel, string code, List<StepSettingError> errors)
    {
        if (items.Count == 0)
        {
            errors.Add(StepSettingError.Error(code, "提取映射列表不能为空"));
            return;
        }

        var seen = new HashSet<string>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.TargetVariable))
            {
                errors.Add(StepSettingError.Error($"{code}V", "提取映射中存在目标变量为空的行"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Path))
                errors.Add(StepSettingError.Error($"{code}P", $"变量 {item.TargetVariable} 的{pathLabel}不能为空"));
            if (!seen.Add(item.TargetVariable))
                errors.Add(StepSettingError.Warning($"{code}D", $"目标变量 {item.TargetVariable} 被重复写入，后一行会覆盖前一行"));
        }
    }
}
