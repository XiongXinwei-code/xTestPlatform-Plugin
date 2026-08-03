using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.UI;

public sealed class OpcUaConnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Connect";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaConnectEditorView();
        view.ViewModel.AttachSerializer(new OpcUaConnectPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaConnectSetting)new OpcUaConnectPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_001", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.EndpointUrl))
            errors.Add(StepSettingError.Error("OPCUA_002", "端点 URL 不能为空"));
        if (s.AuthMode == OpcUaAuthMode.UserPassword && string.IsNullOrWhiteSpace(s.UserName))
            errors.Add(StepSettingError.Error("OPCUA_003", "用户名密码模式下用户名不能为空"));
        return errors;
    }
}

public sealed class OpcUaDisconnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Disconnect";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDisconnectEditorView();
        view.ViewModel.AttachSerializer(new OpcUaDisconnectPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaDisconnectSetting)new OpcUaDisconnectPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_010", "连接标识名不能为空"));
        return errors;
    }
}

public sealed class OpcUaReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Read";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaReadEditorView();
        view.ViewModel.AttachSerializer(new OpcUaReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaReadSetting)new OpcUaReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_021", "节点 ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_022", "结果变量名不能为空"));
        return errors;
    }
}

public sealed class OpcUaWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Write";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaWriteEditorView();
        view.ViewModel.AttachSerializer(new OpcUaWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaWriteSetting)new OpcUaWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_030", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_031", "节点 ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.WriteValue))
            errors.Add(StepSettingError.Error("OPCUA_032", "写入值不能为空"));
        return errors;
    }
}

public sealed class OpcUaBatchReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.BatchRead";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaBatchReadEditorView();
        view.ViewModel.AttachSerializer(new OpcUaBatchReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaBatchReadSetting)new OpcUaBatchReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_040", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_041", "节点列表为空"));
        return errors;
    }
}

public sealed class OpcUaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.BatchWrite";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaBatchWriteEditorView();
        view.ViewModel.AttachSerializer(new OpcUaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaBatchWriteSetting)new OpcUaBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_050", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_051", "节点列表为空"));
        return errors;
    }
}

public sealed class OpcUaSubscribeEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Subscribe";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaSubscribeEditorView();
        view.ViewModel.AttachSerializer(new OpcUaSubscribePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaSubscribeSetting)new OpcUaSubscribePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_060", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_061", "节点 ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.ExpectedValue))
            errors.Add(StepSettingError.Error("OPCUA_062", "期望值不能为空"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_063", "超时时间必须大于 0"));
        return errors;
    }
}
