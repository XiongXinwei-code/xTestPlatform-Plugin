using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Validation;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqAoWriteSingleEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AoWriteSingle";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ao_write_single.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAoWriteSingleEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new NiDaqAoWriteSinglePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var s = (NiDaqAoWriteSingleSetting)new NiDaqAoWriteSinglePlugin().CreateSerializer().Deserialize(context.Setting, 1);
            foreach (var (name, value) in new[] { ("TaskName", s.TaskName), ("Values", s.Values) })
            {
                if (string.IsNullOrWhiteSpace(value)) errors.Add(StepSettingError.Error("DAQ_AO_" + name, name + " 表达式不能为空"));
                else if (!context.Evaluator.ValidateExpression(value, context.ExecutionContext, out var error)) errors.Add(StepSettingError.Error("DAQ_AO_" + name, name + " 表达式无效：" + error));
            }
            if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("DAQ_AO_TIMEOUT", "单次总期限必须大于 0 ms"));
            NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors, "NiDaq.AoConfig");
            NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("DAQ_AO_SETTING", "AO 单点输出设置无效：" + ex.Message)); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
