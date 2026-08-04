using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqStart";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqStartEditorView();
        view.ViewModel.AttachSerializer(new OpcUaDataAcqStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqStartSetting)new OpcUaDataAcqStartPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_070", "采集任务名不能为空"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_071", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_072", "采集节点列表为空"));
        if (s.SamplingIntervalMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_073", "采样间隔必须大于 0"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
