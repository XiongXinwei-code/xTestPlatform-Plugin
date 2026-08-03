using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.UI;

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
