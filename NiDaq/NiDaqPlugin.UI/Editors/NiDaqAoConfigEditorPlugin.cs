using System.Windows;
using NiDaq.Helpers;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqAoConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AoConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ao_config.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAoConfigEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new NiDaqAoConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var s = (NiDaqAoConfigSetting)new NiDaqAoConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            try { AoOutputPlan.ValidateConfiguration(s); }
            catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DAQ_AO_CONFIG", ex.Message)); }
            CheckExpression(s.TaskName, "TaskName");
            CheckExpression(s.InitialValues, "InitialValues");
            if (s.StopPolicy == AoStopPolicy.WriteFinalValues) CheckExpression(s.FinalValues, "FinalValues");
            void CheckExpression(string value, string name)
            {
                if (string.IsNullOrWhiteSpace(value)) errors.Add(StepSettingError.Error("DAQ_AO_" + name, name + " 表达式不能为空"));
                else if (!context.Evaluator.ValidateExpression(value, context.ExecutionContext, out var error)) errors.Add(StepSettingError.Error("DAQ_AO_" + name, name + " 表达式无效：" + error));
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("DAQ_AO_SETTING", "AO 配置数据无效：" + ex.Message)); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
