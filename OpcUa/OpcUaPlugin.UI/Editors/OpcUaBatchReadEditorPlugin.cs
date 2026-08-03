using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.UI;

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
