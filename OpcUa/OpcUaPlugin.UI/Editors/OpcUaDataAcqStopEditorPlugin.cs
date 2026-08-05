using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqStop";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqStopEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqStopSetting)new OpcUaDataAcqStopPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_080", "采集任务名不能为空"));
        if ((s.ExportFormat == DataAcqExportFormat.Csv || s.ExportFormat == DataAcqExportFormat.Both)
            && string.IsNullOrWhiteSpace(s.CsvFilePath))
            errors.Add(StepSettingError.Error("OPCUA_081", "CSV 导出路径不能为空"));
        return errors;
    }
}
