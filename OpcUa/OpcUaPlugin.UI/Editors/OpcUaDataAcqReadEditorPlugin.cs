using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqRead";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqReadSetting)new OpcUaDataAcqReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_090", "采集任务名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("OPCUA_090E", $"TaskName 表达式无效: {taskNameErr}"));
        if (s.SamplesToRead == 0 || s.SamplesToRead < -1)
            errors.Add(StepSettingError.Error("OPCUA_091", "读取条数必须大于 0，或为 -1 表示读取全部可用数据"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_092", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_093", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
            OpcUaVariableValidator.CheckWaveformVariable(context.ExecutionContext, s.ResultVariable, "OPCUA_094", errors);
        if (s.SaveToFile && string.IsNullOrWhiteSpace(s.CsvFilePath))
            errors.Add(StepSettingError.Error("OPCUA_095", "启用存盘时 CSV 文件路径不能为空"));
        else if (s.SaveToFile && !context.Evaluator.ValidateExpression(s.CsvFilePath, context.ExecutionContext, out var pathErr))
            errors.Add(StepSettingError.Error("OPCUA_095E", $"CsvFilePath 表达式无效: {pathErr}"));
        OpcUaLifecycleValidator.CheckPrecedingDataAcqStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return errors;
    }
}
