using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using Modbus.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusRead";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

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
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("MB_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.StartAddress))
            errors.Add(StepSettingError.Error("MB_024", "起始地址不能为空"));
        if (string.IsNullOrWhiteSpace(s.Quantity))
            errors.Add(StepSettingError.Error("MB_025", "读取数量不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("MB_021", "结果变量名不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("MB_022", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var expectedElem = (s.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
                ? typeof(bool)
                : s.DataFormat switch
                {
                    ModbusDataFormat.UInt16                                        => typeof(ushort),
                    ModbusDataFormat.Int16                                         => typeof(short),
                    ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.UInt32_CD_AB => typeof(uint),
                    ModbusDataFormat.Int32_AB_CD  or ModbusDataFormat.Int32_CD_AB  => typeof(int),
                    ModbusDataFormat.Float_AB_CD  or ModbusDataFormat.Float_CD_AB  => typeof(float),
                    _                                                              => typeof(ushort)
                };
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null)
            {
                var valType = val.GetType();
                var elemType = valType.IsArray ? valType.GetElementType()! : valType;
                if (elemType != expectedElem)
                    errors.Add(StepSettingError.Error("MB_023", $"变量 {s.ResultVariable} 类型不匹配，期望 {expectedElem.Name}，实际类型 {valType.Name}"));
            }
        }
        ModbusLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
