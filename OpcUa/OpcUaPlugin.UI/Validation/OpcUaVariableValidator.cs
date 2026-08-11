using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Validation;

internal static class OpcUaVariableValidator
{
    /// <summary>
    /// 校验结果变量的声明类型是否为波形类型（Waveform）。
    /// 优先通过变量定义检查声明类型；若无法获取定义，则回退检查运行时值类型。
    /// </summary>
    public static void CheckWaveformVariable(
        IExecutionContext context, string variablePath, string errorCode, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(variablePath)) return;

        var def = GetVariableDefinition(context, variablePath);
        if (def is not null)
        {
            if (def.DataType != VariableDataType.Waveform)
                errors.Add(StepSettingError.Error(errorCode,
                    $"变量 {variablePath} 类型不匹配，期望波形类型（Waveform），实际类型 {def.DataType}"));
            return;
        }

        var val = context.GetVariable(variablePath);
        if (val is not null && val is not WaveformData)
            errors.Add(StepSettingError.Error(errorCode,
                $"变量 {variablePath} 类型不匹配，期望波形类型（WaveformData），实际类型 {val.GetType().Name}"));
    }

    private static Variables? GetVariableDefinition(IExecutionContext context, string variablePath)
    {
        var dot = variablePath.IndexOf('.');
        if (dot <= 0 || dot >= variablePath.Length - 1) return null;

        var scope = variablePath[..dot] switch
        {
            "Locals" => context.Locals,
            "FileGlobals" => context.FileGlobals,
            "ProjectGlobals" => context.ProjectGlobals,
            "Parameters" => context.Parameters,
            "RunState" => context.RunState,
            _ => null
        };
        return scope?.GetVariableDefinition(variablePath[(dot + 1)..]);
    }
}
