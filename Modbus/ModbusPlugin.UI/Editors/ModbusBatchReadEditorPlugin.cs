using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using Modbus.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusBatchReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusBatchRead";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus_batch_read.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusBatchReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (ModbusBatchReadSetting)new ModbusBatchReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("MB_040", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("MB_040E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.Items.Count == 0) errors.Add(StepSettingError.Warning("MB_041", "批量读取列表为空"));
        if (s.IntervalMs < 0) errors.Add(StepSettingError.Error("MB_045", "读取间隔时间不能为负数"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (!Enum.IsDefined(item.RegisterType)) errors.Add(StepSettingError.Error("MB_ENUM_TYPE", $"第 {i + 1} 行：不支持的寄存器类型"));
            if (!Enum.IsDefined(item.DataFormat)) errors.Add(StepSettingError.Error("MB_ENUM_FORMAT", $"第 {i + 1} 行：不支持的数据格式"));
            if (!string.IsNullOrWhiteSpace(item.ResultVariable) && !context.ExecutionContext.HasVariable(item.ResultVariable))
                errors.Add(StepSettingError.Error("MB_044", $"第 {i + 1} 行：变量 {item.ResultVariable} 不存在，请先创建该变量"));
            else if (!string.IsNullOrWhiteSpace(item.ResultVariable))
                ModbusVariableValidator.CheckKnownOutput(context.ExecutionContext, item.ResultVariable,
                    item.RegisterType, item.DataFormat, errors, "MB_046", "MB_047", item.Quantity);
            if (item.Quantity == 0)
                errors.Add(StepSettingError.Error("MB_043", $"第 {i + 1} 行：读取数量必须大于 0"));
        }
        ModbusLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
