using System.Windows;
using LIN.Models;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinOpen";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinOpenSetting)new LinOpenPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.Channel))
            errors.Add(StepSettingError.Error("LIN_001", "通道名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_002", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("LIN_003", $"ConnectionName 表达式无效: {connErr}"));
        if (s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("LIN_004", "波特率必须大于 0"));
        if (s.RxQueueSize <= 0)
            errors.Add(StepSettingError.Error("LIN_005", "接收缓冲区大小必须大于 0"));
        else if (s.RxQueueSize < 512)
            errors.Add(StepSettingError.Warning("LIN_W01", "接收缓冲区小于默认值 512 帧，高负载总线下可能丢帧"));

        return errors;
    }
}
