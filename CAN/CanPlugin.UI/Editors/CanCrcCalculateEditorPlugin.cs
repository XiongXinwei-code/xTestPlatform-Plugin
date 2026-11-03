using System.Text.Json;
using System.Windows;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCrcCalculateEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCrcCalculate";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCrcCalculateEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        try
        {
            ct.ThrowIfCancellationRequested();
            var s = (CanCrcCalculateSetting)new CanCrcCalculatePlugin().CreateSerializer().Deserialize(context.Setting, 1);
            if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("CAN_CRC_TIMEOUT", "CRC 计算超时必须大于 0"));
            if (string.IsNullOrWhiteSpace(s.Data)) errors.Add(StepSettingError.Error("CAN_CRC_DATA", "报文数据表达式不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.Data, context.ExecutionContext, out var detail))
                errors.Add(StepSettingError.Error("CAN_CRC_DATA", $"报文数据表达式无效：{detail}"));
            if (s.Protection is not { CrcEnabled: true })
                errors.Add(StepSettingError.Error("CAN_CRC_DISABLED", "离线 CRC 计算步骤必须启用 CRC"));
            else
            {
                PayloadProtectionValidator.CheckTableExpression(s.Protection, context, errors);
                // The zero table validates layout only; execution resolves the configured expression and checks all 16 values.
                var layout = CanPayloadProtectionExpressions.ForLayoutValidation(s.Protection);
                DbcPayload.ValidateReadProtection(layout, 64);
                if (s.Data is { Length: >= 2 } && s.Data.StartsWith('"') && s.Data.EndsWith('"'))
                {
                    string? literal = null;
                    try { literal = JsonSerializer.Deserialize<string>(s.Data); }
                    catch (JsonException) { /* Composed expressions are checked by the evaluator and validated with their actual runtime data. */ }
                    if (literal != null) DbcPayload.ValidateReadProtection(layout, CanHelper.ParseHexData(literal).Length);
                }
            }
            if (!string.IsNullOrWhiteSpace(s.ResultVariable))
            {
                if (s.ResultVariable.IndexOf('.') <= 0 || !context.ExecutionContext.HasVariable(s.ResultVariable))
                    errors.Add(StepSettingError.Error("CAN_CRC_RESULT", "CRC 结果变量必须为已声明的完整变量路径"));
                else if (context.ExecutionContext.GetVariable(s.ResultVariable) is { } result && result is not string)
                    errors.Add(StepSettingError.Error("CAN_CRC_RESULT", "CRC 结果变量必须是字符串"));
            }
            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            errors.Add(StepSettingError.Error("CAN_CRC_SETTING", $"CRC 计算配置无效：{ex.Message}"));
        }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
