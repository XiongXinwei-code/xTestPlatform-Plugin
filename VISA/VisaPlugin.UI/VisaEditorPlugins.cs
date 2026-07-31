using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.UI;

/// <summary>
/// VISA Open 编辑器插件
/// </summary>
public sealed class VisaOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaOpen";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaOpenEditorView();
        view.ViewModel.AttachSerializer(new VisaOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaOpenSetting)new VisaOpenPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_001", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResourceString))
            errors.Add(StepSettingError.Error("VISA_002", "VISA 资源字符串不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA Close 编辑器插件
/// </summary>
public sealed class VisaCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaClose";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaCloseEditorView();
        view.ViewModel.AttachSerializer(new VisaClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaCloseSetting)new VisaClosePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_010", "连接标识名不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA Write 编辑器插件
/// </summary>
public sealed class VisaWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaWriteEditorView();
        view.ViewModel.AttachSerializer(new VisaWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaWriteSetting)new VisaWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.Command))
            errors.Add(StepSettingError.Error("VISA_021", "SCPI 命令不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA Query 编辑器插件
/// </summary>
public sealed class VisaQueryEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaQuery";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaQueryEditorView();
        view.ViewModel.AttachSerializer(new VisaQueryPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaQuerySetting)new VisaQueryPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_030", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.Command))
            errors.Add(StepSettingError.Error("VISA_031", "SCPI 命令不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("VISA_032", "结果变量名不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA Read 编辑器插件
/// </summary>
public sealed class VisaReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaRead";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaReadEditorView();
        view.ViewModel.AttachSerializer(new VisaReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaReadSetting)new VisaReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_040", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("VISA_041", "结果变量名不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA WaitOPC 编辑器插件
/// </summary>
public sealed class VisaWaitOpcEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaWaitOpc";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaWaitOpcEditorView();
        view.ViewModel.AttachSerializer(new VisaWaitOpcPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaWaitOpcSetting)new VisaWaitOpcPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_050", "连接标识名不能为空"));
        return errors;
    }
}

/// <summary>
/// VISA BatchWrite 编辑器插件
/// </summary>
public sealed class VisaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaBatchWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaBatchWriteEditorView();
        view.ViewModel.AttachSerializer(new VisaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaBatchWriteSetting)new VisaBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_060", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Error("VISA_061", "至少需要一条 SCPI 命令"));
        return errors;
    }
}
