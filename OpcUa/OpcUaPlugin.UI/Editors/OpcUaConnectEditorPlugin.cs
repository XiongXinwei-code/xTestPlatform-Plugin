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
