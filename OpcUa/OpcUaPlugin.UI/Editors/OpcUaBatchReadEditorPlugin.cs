using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaBatchReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.BatchRead";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaBatchReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaBatchReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaBatchReadSetting)new OpcUaBatchReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_040", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_041", "节点列表为空"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (string.IsNullOrWhiteSpace(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_042", $"第 {i + 1} 行：节点标识不能为空"));
            if (string.IsNullOrWhiteSpace(item.ResultVariable))
                errors.Add(StepSettingError.Error("OPCUA_043", $"第 {i + 1} 行：结果变量不能为空"));
        }
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
