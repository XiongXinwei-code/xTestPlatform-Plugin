using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCyclicSendStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCyclicSendStart";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_cyclic_start.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCyclicSendStartEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer());
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
            var s = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            if (s.ControlRefreshMs <= 0)
                errors.Add(StepSettingError.Error("CAN_CONTROL_REFRESH", "发送条件 / 周期刷新间隔必须大于 0 ms"));
            if (s.ExpressionTimeoutMs <= 0)
                errors.Add(StepSettingError.Error("CAN_EXPRESSION_TIMEOUT", "表达式求值超时必须大于 0 ms"));
            if (string.IsNullOrWhiteSpace(s.ConnectionName))
                errors.Add(StepSettingError.Error("CAN_030", "连接标识名不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
                errors.Add(StepSettingError.Error("CAN_030E", $"ConnectionName 表达式无效: {connErr}"));
            if (string.IsNullOrWhiteSpace(s.TaskName))
                errors.Add(StepSettingError.Error("CAN_031", "任务标识名不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskErr))
                errors.Add(StepSettingError.Error("CAN_031E", $"TaskName 表达式无效: {taskErr}"));
            if (s.Messages == null)
            {
                errors.Add(StepSettingError.Error("CAN_MESSAGES", "报文列表不能为空引用"));
                return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
            }
            if (s.Messages.Count == 0)
                errors.Add(StepSettingError.Warning("CAN_W30", "报文列表为空"));
            else if (s.Messages.Count > 128)
                errors.Add(StepSettingError.Error("CAN_MESSAGES", "周期报文最多 128 行"));
            else if (!s.Messages.Any(m => m != null && (m.Enabled || !string.IsNullOrWhiteSpace(m.EnabledExpression))))
                errors.Add(StepSettingError.Warning("CAN_W31", "没有启用的报文"));
            for (int i = 0; i < s.Messages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var m = s.Messages[i];
                if (m == null) { errors.Add(StepSettingError.Error("CAN_MESSAGES", $"第 {i + 1} 行不能为空")); continue; }
                PayloadProtectionValidator.Check(m.Protection, m.UseFdFrame, m.Data, context, errors, $"第 {i + 1} 行：");
                if (!Enum.IsDefined(m.FrameType))
                    errors.Add(StepSettingError.Error("CAN_035", $"第 {i + 1} 行：帧类型必须是 Standard 或 Extended"));
                if (string.IsNullOrWhiteSpace(m.CanId))
                    errors.Add(StepSettingError.Error("CAN_032", $"第 {i + 1} 行：CAN ID 不能为空"));
                else if (!context.Evaluator.ValidateExpression(m.CanId, context.ExecutionContext, out var canIdErr))
                    errors.Add(StepSettingError.Error("CAN_032E", $"第 {i + 1} 行：CAN ID 表达式无效: {canIdErr}"));
                if (string.IsNullOrWhiteSpace(m.Data))
                    errors.Add(StepSettingError.Error("CAN_033", $"第 {i + 1} 行：数据不能为空"));
                else if (!context.Evaluator.ValidateExpression(m.Data, context.ExecutionContext, out var dataErr))
                    errors.Add(StepSettingError.Error("CAN_033E", $"第 {i + 1} 行：Data 表达式无效: {dataErr}"));
                if (string.IsNullOrWhiteSpace(m.CycleTimeMsExpression))
                {
                    if (m.CycleTimeMs <= 0) errors.Add(StepSettingError.Error("CAN_034", $"第 {i + 1} 行：发送周期必须大于 0"));
                }
                else
                {
                    if (!context.Evaluator.ValidateExpression(m.CycleTimeMsExpression, context.ExecutionContext, out var periodError))
                        errors.Add(StepSettingError.Error("CAN_PERIOD_EXPRESSION", $"第 {i + 1} 行：周期表达式无效：{periodError}"));
                    if (int.TryParse(m.CycleTimeMsExpression, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int period) && period <= 0)
                        errors.Add(StepSettingError.Error("CAN_034", $"第 {i + 1} 行：周期表达式的常量值必须大于 0 ms"));
                }
                if (!string.IsNullOrWhiteSpace(m.EnabledExpression) && !context.Evaluator.ValidateExpression(m.EnabledExpression, context.ExecutionContext, out var enabledError))
                    errors.Add(StepSettingError.Error("CAN_ENABLED_EXPRESSION", $"第 {i + 1} 行：发送条件表达式无效：{enabledError}"));
            }
            CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("CAN_CYCLIC_SETTINGS", $"CAN 周期发送设置无效：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
