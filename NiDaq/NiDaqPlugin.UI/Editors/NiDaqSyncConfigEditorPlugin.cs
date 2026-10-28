using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqSyncConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_sync_config.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncConfigEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqSyncConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncConfigSetting)new NiDaqSyncConfigPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_020", "任务名称不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("DAQ_020E", $"TaskName 表达式无效: {taskNameErr}"));
        if (s.AiChannels.Count == 0) errors.Add(StepSettingError.Error("DAQ_021", "AI 通道列表为空"));
        if (s.EncoderChannels.Count == 0) errors.Add(StepSettingError.Error("DAQ_022", "编码器通道列表为空"));
        if (s.SampleRate <= 0) errors.Add(StepSettingError.Error("DAQ_023", "采样率必须大于 0"));
        if (!Enum.IsDefined(s.SampleMode)) errors.Add(StepSettingError.Error("DAQ_SAMPLE_MODE", "SampleMode 无效"));
        if (s.UseTrigger && string.IsNullOrWhiteSpace(s.TriggerSource)) errors.Add(StepSettingError.Error("DAQ_TRIGGER_SOURCE", "启用触发时 TriggerSource 不能为空"));
        if (s.UseTrigger && !Enum.IsDefined(s.TriggerEdge)) errors.Add(StepSettingError.Error("DAQ_TRIGGER_EDGE", "TriggerEdge 无效"));
        if (s.SamplesPerChannel <= 0) errors.Add(StepSettingError.Error("DAQ_024", "每通道采样数必须大于 0"));
        for (int i = 0; i < s.AiChannels.Count; i++)
        {
            var ch = s.AiChannels[i];
            if (string.IsNullOrWhiteSpace(ch.PhysicalChannel))
                errors.Add(StepSettingError.Error("DAQ_025", $"AI 第 {i + 1} 行：物理通道不能为空"));
            if (string.IsNullOrWhiteSpace(ch.ColumnName))
                errors.Add(StepSettingError.Error("DAQ_026", $"AI 第 {i + 1} 行：列名不能为空"));
            if (!double.IsFinite(ch.MinValue) || !double.IsFinite(ch.MaxValue) || ch.MinValue >= ch.MaxValue)
                errors.Add(StepSettingError.Error("DAQ_027", $"AI 第 {i + 1} 行：量程下限 ({ch.MinValue}) 必须小于上限 ({ch.MaxValue})"));
            if (!Enum.IsDefined(ch.Terminal)) errors.Add(StepSettingError.Error("DAQ_TERMINAL", $"AI 第 {i + 1} 行：Terminal 无效"));
        }
        for (int i = 0; i < s.EncoderChannels.Count; i++)
        {
            if (s.EncoderChannels[i].PulsesPerRevolution <= 0) errors.Add(StepSettingError.Error("DAQ_SYNC_PPR", $"编码器第 {i + 1} 行：PPR 必须大于 0"));
            if (!Enum.IsDefined(s.EncoderChannels[i].DecodingType)) errors.Add(StepSettingError.Error("DAQ_SYNC_DECODING", $"编码器第 {i + 1} 行：DecodingType 无效"));
        }
        try { NiDaq.Helpers.DaqSamplePlan.Create(s); }
        catch (Exception ex) { errors.Add(StepSettingError.Error("DAQ_SYNC_ROUTE", ex.Message)); }
        if (!double.IsFinite(s.SampleRate)) errors.Add(StepSettingError.Error("DAQ_RATE_FINITE", "采样率必须为有限正数"));
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("DAQ_099", "总期限必须大于 0（包含旧资源收尾等待）"));
        return errors;
    }
}
