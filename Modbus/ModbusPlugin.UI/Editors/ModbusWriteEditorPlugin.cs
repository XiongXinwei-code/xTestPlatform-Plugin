using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using Modbus.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusWrite";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (ModbusWriteSetting)new ModbusWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("MB_030", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("MB_030E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.StartAddress))
            errors.Add(StepSettingError.Error("MB_032", "起始地址不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.StartAddress, context.ExecutionContext, out var addrErr))
            errors.Add(StepSettingError.Error("MB_032E", $"StartAddress 表达式无效: {addrErr}"));
        if (string.IsNullOrWhiteSpace(s.Values))
            errors.Add(StepSettingError.Error("MB_031", "写入值不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Values, context.ExecutionContext, out var valErr))
            errors.Add(StepSettingError.Error("MB_031E", $"Values 表达式无效: {valErr}"));
        ModbusLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
