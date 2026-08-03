using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.UI;

public sealed class NiDaqDiReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DiRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDiReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDiReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDiReadSetting)new NiDaqDiReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("E001", "物理通道不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable)) errors.Add(StepSettingError.Error("E002", "结果变量不能为空"));
        return errors;
    }
}

public sealed class NiDaqDoWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DoWrite";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDoWriteEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDoWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDoWriteSetting)new NiDaqDoWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("E001", "物理通道不能为空"));
        if (string.IsNullOrWhiteSpace(s.Value)) errors.Add(StepSettingError.Error("E002", "输出值不能为空"));
        return errors;
    }
}

public sealed class NiDaqAiAcquireEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiAcquire";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiAcquireEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiAcquirePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiAcquireSetting)new NiDaqAiAcquirePlugin().CreateSerializer().Deserialize(setting, 1);
        if (s.Channels.Count == 0) errors.Add(StepSettingError.Error("E001", "AI 通道列表为空"));
        return errors;
    }
}

public sealed class NiDaqAiStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiStart";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiStartEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiStartSetting)new NiDaqAiStartPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (s.Channels.Count == 0) errors.Add(StepSettingError.Error("E002", "AI 通道列表为空"));
        return errors;
    }
}

public sealed class NiDaqAiStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiStop";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiStopEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiStopSetting)new NiDaqAiStopPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

public sealed class NiDaqEncoderEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.EncoderRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderEditorView();
        view.ViewModel.AttachSerializer(new NiDaqEncoderReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderSetting)new NiDaqEncoderReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.CounterChannel)) errors.Add(StepSettingError.Error("E001", "Counter 通道不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable)) errors.Add(StepSettingError.Error("E002", "结果变量不能为空"));
        return errors;
    }
}

public sealed class NiDaqSyncStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncStart";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncStartEditorView();
        view.ViewModel.AttachSerializer(new NiDaqSyncStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncStartSetting)new NiDaqSyncStartPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (s.AiChannels.Count == 0) errors.Add(StepSettingError.Error("E002", "AI 通道列表为空"));
        if (s.EncoderChannels.Count == 0) errors.Add(StepSettingError.Error("E003", "编码器通道列表为空"));
        return errors;
    }
}

public sealed class NiDaqSyncStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncStop";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncStopEditorView();
        view.ViewModel.AttachSerializer(new NiDaqSyncStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncStopSetting)new NiDaqSyncStopPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

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

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDataAnalyzeSetting)new NiDaqDataAnalyzePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.FilePath)) errors.Add(StepSettingError.Error("E001", "文件路径不能为空"));
        if (string.IsNullOrWhiteSpace(s.ChannelName)) errors.Add(StepSettingError.Error("E002", "通道名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable)) errors.Add(StepSettingError.Error("E003", "结果变量不能为空"));
        return errors;
    }
}
