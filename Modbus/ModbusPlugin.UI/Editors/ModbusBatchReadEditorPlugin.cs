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
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusBatchReadEditorView();
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
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("MB_041", "批量读取列表为空"));
        ModbusLifecycleValidator.CheckPrecedingConnect(context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
