using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.UI.Validation;

internal static class EthernetOutputValidator
{
    internal static void Check(StepEditorValidationContext context, string path, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!context.ExecutionContext.HasVariable(path))
        {
            errors.Add(StepSettingError.Error("ETH_VAR01", $"结果变量 {path} 不存在，请先声明字符串变量或留空不写入"));
            return;
        }
        var value = context.ExecutionContext.GetVariable(path);
        if (value is not null && value is not string)
            errors.Add(StepSettingError.Error("ETH_VAR02", $"结果变量 {path} 类型不匹配，期望string，实际{value.GetType().Name}"));
    }
}
