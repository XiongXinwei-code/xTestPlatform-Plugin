using System.Windows;
using CAN.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCaptureEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCapture";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCaptureEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step); PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanCaptureSetting)new CanCapturePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName)) errors.Add(StepSettingError.Error("CAN_CAPTURE_01", "连接名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connError))
            errors.Add(StepSettingError.Error("CAN_CAPTURE_02", $"连接名称表达式无效：{connError}"));
        if (string.IsNullOrWhiteSpace(s.FilterIds) || !context.Evaluator.ValidateExpression(s.FilterIds, context.ExecutionContext, out _))
            errors.Add(StepSettingError.Error("CAN_CAPTURE_03", "ID 过滤表达式无效；采集全部报文请填写空字符串表达式"));
        if (s.DurationMs <= 0 || s.SetupTimeoutMs <= 0) errors.Add(StepSettingError.Error("CAN_CAPTURE_04", "采集时间和准备超时必须大于 0"));
        if (s.MaxFrames is < 1 or > 100_000) errors.Add(StepSettingError.Error("CAN_CAPTURE_05", "最大帧数须为 1~100000"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable) || s.ResultVariable.IndexOf('.') <= 0 || !context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("CAN_CAPTURE_06", "请指定已存在的字符串结果变量"));
        else if (context.ExecutionContext.GetVariable(s.ResultVariable) is { } result && result is not string)
            errors.Add(StepSettingError.Error("CAN_CAPTURE_07", "结果变量必须是字符串"));
        if (s.ExportEnabled)
        {
            if (string.IsNullOrWhiteSpace(s.ExportFilePath) || s.ExportFilePath.Trim() == "\"\"" ||
                !context.Evaluator.ValidateExpression(s.ExportFilePath, context.ExecutionContext, out _))
                errors.Add(StepSettingError.Error("CAN_CAPTURE_EXPORT_PATH", "启用导出时请填写有效的文件路径字符串表达式；运行时须得到绝对文件路径，父目录须已存在"));
            if (s.ExportTimeoutMs <= 0)
                errors.Add(StepSettingError.Error("CAN_CAPTURE_EXPORT_TIMEOUT", "导出超时必须大于 0"));
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
