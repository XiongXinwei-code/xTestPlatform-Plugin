using System.Windows;
using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanWrite";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        try
        {
        ct.ThrowIfCancellationRequested();
        var s = (CanWriteSetting)new CanWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("CAN_WRITE_TIMEOUT", "发送等待超时必须大于 0 ms"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_020", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("CAN_020E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.UseMessageList)
        {
            if (s.Messages == null || s.Messages.Count is < 1 or > 128)
                errors.Add(StepSettingError.Error("CAN_WRITE_MESSAGES", "多报文列表必须包含 1～128 行"));
            if (s.Messages != null)
            {
                bool anyEnabled = false;
                for (int i = 0; i < s.Messages.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = s.Messages[i];
                    if (row == null) { errors.Add(StepSettingError.Error("CAN_WRITE_MESSAGES", $"第 {i + 1} 行不能为空")); continue; }
                    if (!row.Enabled) continue;
                    anyEnabled = true;
                    CheckFrame(row.CanId, row.Data, row.FrameType, row.UseFdFrame, row.Protection, context, errors, $"第 {i + 1} 行：");
                }
                if (!anyEnabled) errors.Add(StepSettingError.Error("CAN_WRITE_MESSAGES", "多报文列表至少启用一行"));
            }
        }
        else CheckFrame(s.CanId, s.Data, s.FrameType, s.UseFdFrame, s.Protection, context, errors, "");
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("CAN_WRITE_SETTINGS", $"CAN 写入设置无效：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }

    private static void CheckFrame(string canId, string data, CanFrameType frameType, bool useFd, CanPayloadProtectionSetting? protection,
        StepEditorValidationContext context, List<StepSettingError> errors, string prefix)
    {
        if (!Enum.IsDefined(frameType)) errors.Add(StepSettingError.Error("CAN_WRITE_FRAME_TYPE", prefix + "帧类型必须是 Standard 或 Extended"));
        PayloadProtectionValidator.Check(protection, useFd, data, context, errors, prefix);
        if (string.IsNullOrWhiteSpace(canId)) errors.Add(StepSettingError.Error("CAN_021", prefix + "CAN ID 不能为空"));
        else if (!context.Evaluator.ValidateExpression(canId, context.ExecutionContext, out var idError))
            errors.Add(StepSettingError.Error("CAN_021E", $"{prefix}CanId 表达式无效：{idError}"));
        if (string.IsNullOrWhiteSpace(data)) errors.Add(StepSettingError.Error("CAN_W20", prefix + "Data 需要字符串表达式；空载荷可填写 \"\""));
        else if (!context.Evaluator.ValidateExpression(data, context.ExecutionContext, out var dataError))
            errors.Add(StepSettingError.Error("CAN_W20E", $"{prefix}Data 表达式无效：{dataError}"));
        try
        {
            string? literalId = Literal(canId), literalData = Literal(data);
            if (literalId != null)
            {
                uint id = CanHelper.ParseCanId(literalId);
                if (id > (frameType == CanFrameType.Extended ? 0x1FFFFFFFu : 0x7FFu)) throw new ArgumentException("CAN ID 超出所选帧类型范围");
            }
            if (literalData != null)
            {
                byte[] payload = CanHelper.ParseHexData(literalData);
                int maximumLength = useFd ? 64 : 8;
                if (payload.Length > maximumLength) throw new ArgumentException($"{(useFd ? "CAN FD" : "CAN")} 数据不能超过 {maximumLength} 字节");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        { errors.Add(StepSettingError.Error("CAN_WRITE_LITERAL", prefix + ex.Message)); }
    }

    private static string? Literal(string? expression)
    {
        if (expression is not { Length: >= 2 } || !expression.StartsWith('"') || !expression.EndsWith('"')) return null;
        try { return JsonSerializer.Deserialize<string>(expression); }
        catch (JsonException) { return null; } // A composed expression is checked by the evaluator/runtime.
    }
}
