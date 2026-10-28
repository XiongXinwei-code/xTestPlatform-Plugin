using System.Windows;
using Modbus.Models;
using Modbus.Helpers;
using Modbus.UI.Views;
using Modbus.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusRead";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus_read.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (ModbusReadSetting)new ModbusReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (!Enum.IsDefined(s.RegisterType)) errors.Add(StepSettingError.Error("MB_ENUM_TYPE", "不支持的寄存器类型"));
        if (!Enum.IsDefined(s.DataFormat)) errors.Add(StepSettingError.Error("MB_ENUM_FORMAT", "不支持的数据格式"));
        ushort? knownQuantity = ModbusRequestValidator.ReadConstants(s, errors);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("MB_020", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("MB_020E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.StartAddress))
            errors.Add(StepSettingError.Error("MB_024", "起始地址不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.StartAddress, context.ExecutionContext, out var addrErr))
            errors.Add(StepSettingError.Error("MB_024E", $"StartAddress 表达式无效: {addrErr}"));
        if (string.IsNullOrWhiteSpace(s.Quantity))
            errors.Add(StepSettingError.Error("MB_025", "读取数量不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Quantity, context.ExecutionContext, out var qtyErr))
            errors.Add(StepSettingError.Error("MB_025E", $"Quantity 表达式无效: {qtyErr}"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("MB_021", "结果变量名不能为空"));
        else if (!ModbusRequestPreflight.IsScopedPath(s.ResultVariable))
            errors.Add(StepSettingError.Error("MB_022", "结果必须使用带作用域的完整变量路径，如 Locals.result"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("MB_022", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            ModbusVariableValidator.CheckKnownOutput(context.ExecutionContext, s.ResultVariable,
                s.RegisterType, s.DataFormat, errors, "MB_023", "MB_023_SHAPE", knownQuantity);
        }
        ModbusLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
