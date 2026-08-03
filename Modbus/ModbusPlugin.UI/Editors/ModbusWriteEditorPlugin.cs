using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
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
        if (string.IsNullOrWhiteSpace(s.Values))
            errors.Add(StepSettingError.Error("MB_031", "写入值不能为空"));
        return errors;
    }
}
