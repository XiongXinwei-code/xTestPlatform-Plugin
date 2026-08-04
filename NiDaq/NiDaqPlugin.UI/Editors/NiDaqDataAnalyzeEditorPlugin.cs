using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqDataAnalyzeEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DataAnalyze";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDataAnalyzeEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDataAnalyzePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDataAnalyzeSetting)new NiDaqDataAnalyzePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.FilePath)) errors.Add(StepSettingError.Error("DAQ_050", "文件路径不能为空"));
        if (string.IsNullOrWhiteSpace(s.ChannelName)) errors.Add(StepSettingError.Error("DAQ_051", "通道名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_052", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_056", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not double)
                errors.Add(StepSettingError.Error("DAQ_057", $"变量 {s.ResultVariable} 类型不匹配，期望 double，实际类型 {val.GetType().Name}"));
        }
        if (s.Mode == AnalyzeMode.PeakWithRef && string.IsNullOrWhiteSpace(s.ReferenceChannel))
            errors.Add(StepSettingError.Error("DAQ_053", "PeakWithRef 模式下参考通道不能为空"));
        if (s.Mode == AnalyzeMode.PeakWithRef && string.IsNullOrWhiteSpace(s.RefAtPeakVariable))
            errors.Add(StepSettingError.Error("DAQ_054", "PeakWithRef 模式下峰值参考变量不能为空"));
        if ((s.Mode == AnalyzeMode.RangeStats || s.Mode == AnalyzeMode.Slope) && string.IsNullOrWhiteSpace(s.ReferenceChannel))
            errors.Add(StepSettingError.Error("DAQ_055", "当前分析模式需要指定参考通道"));
        return errors;
    }
}
