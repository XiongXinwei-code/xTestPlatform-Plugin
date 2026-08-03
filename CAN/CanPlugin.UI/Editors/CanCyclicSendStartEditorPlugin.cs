using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UI;

public sealed class CanCyclicSendStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCyclicSendStart";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCyclicSendStartEditorView();
        view.ViewModel.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_030", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("CAN_031", "任务标识名不能为空"));
        if (s.Messages.Count == 0)
            errors.Add(StepSettingError.Warning("CAN_W30", "报文列表为空"));
        else if (!s.Messages.Any(m => m.Enabled))
            errors.Add(StepSettingError.Warning("CAN_W31", "没有启用的报文"));
        return errors;
    }
}
