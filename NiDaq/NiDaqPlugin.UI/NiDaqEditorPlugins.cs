using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.UI;

// ─── DI / DO (保留不变) ───

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

// ─── AI Config ───

public sealed class NiDaqAiConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiConfigEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiConfigSetting)new NiDaqAiConfigPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (s.Channels.Count == 0) errors.Add(StepSettingError.Error("E002", "AI 通道列表为空"));
        return errors;
    }
}

// ─── Encoder Config ───

public sealed class NiDaqEncoderConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.EncoderConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderConfigEditorView();
        view.ViewModel.AttachSerializer(new NiDaqEncoderConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderConfigSetting)new NiDaqEncoderConfigPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.CounterChannel)) errors.Add(StepSettingError.Error("E002", "Counter 通道不能为空"));
        return errors;
    }
}

// ─── Sync Config ───

public sealed class NiDaqSyncConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncConfigEditorView();
        view.ViewModel.AttachSerializer(new NiDaqSyncConfigPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncConfigSetting)new NiDaqSyncConfigPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        if (s.AiChannels.Count == 0) errors.Add(StepSettingError.Error("E002", "AI 通道列表为空"));
        if (s.EncoderChannels.Count == 0) errors.Add(StepSettingError.Error("E003", "编码器通道列表为空"));
        return errors;
    }
}

// ─── Task Start (通用) ───

public sealed class NiDaqTaskStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.TaskStart";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqTaskStartEditorView();
        view.ViewModel.AttachSerializer(new NiDaqTaskStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqTaskStartSetting)new NiDaqTaskStartPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

// ─── AI Read ───

public sealed class NiDaqAiReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqAiReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiReadSetting)new NiDaqAiReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

// ─── Sync Read ───

public sealed class NiDaqSyncReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqSyncReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncReadSetting)new NiDaqSyncReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

// ─── Encoder Read (保留，纯读取) ───

public sealed class NiDaqEncoderReadEditorPlugin : IStepEditorPlugin
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

// ─── Task Stop (通用) ───

public sealed class NiDaqTaskStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.TaskStop";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqTaskStopEditorView();
        view.ViewModel.AttachSerializer(new NiDaqTaskStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqTaskStopSetting)new NiDaqTaskStopPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("E001", "任务名称不能为空"));
        return errors;
    }
}

// ─── Data Analyze ───

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
        return errors;
    }
}
