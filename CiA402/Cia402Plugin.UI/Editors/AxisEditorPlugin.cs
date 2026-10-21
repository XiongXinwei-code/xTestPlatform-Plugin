using System.Globalization;
using System.Windows;
using CiA402.Core;
using CiA402.Models;
using CiA402.UI.Validation;
using CiA402.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Editors;

public abstract class AxisEditorPlugin<TPlugin, TView> : IStepEditorPlugin
    where TPlugin : ICia402Plugin, new() where TView : AxisEditorView, new()
{
    private readonly TPlugin _plugin = new();
    public string StepTypeId => _plugin.StepTypeId;
    public string? IconPath => _plugin.IconPath;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new TView { SequenceFile = sequenceFile };
        view.ViewModel.Attach(_plugin, step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        try
        {
            ct.ThrowIfCancellationRequested();
            var serializer = _plugin.CreateSerializer();
            var setting = context.Setting is { Length: > 0 } data
                ? (AxisSetting)serializer.Deserialize(data, context.CurrentStep.StepSetting.SettingVersion) : (AxisSetting)serializer.CreateDefault();
            errors.AddRange(SettingValidation.Validate(setting).Select(message => StepSettingError.Error("C402_001", message)));
            foreach (var field in SettingFields.Get(setting.GetType()).Where(f => f.IsActive(setting)))
            {
                ct.ThrowIfCancellationRequested();
                var text = field.Property.GetValue(setting) as string ?? "";
                if (field.IsExpression)
                {
                    if (!context.Evaluator.ValidateExpression(text, context.ExecutionContext, out var error))
                        errors.Add(StepSettingError.Error("C402_002", $"{field.Metadata.Label}表达式无效：{error}"));
                    if (field.Metadata.ResultType != typeof(string))
                    {
                        try
                        {
                            long number;
                            var literal = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
                            if (!literal && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) literal = long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number);
                            if (literal) SettingValidation.Integer(number, field.Metadata.Label, field.Metadata.Min, field.Metadata.Max);
                        }
                        catch (Exception ex) { errors.Add(StepSettingError.Error("C402_003", ex.Message)); }
                    }
                }
                if (field.IsOutput)
                {
                    try { SettingValidation.ValidateOutput(context.ExecutionContext, text, field.Metadata.OutputType!); }
                    catch (Exception ex) { errors.Add(StepSettingError.Error("C402_004", ex.Message)); }
                }
            }
            if (setting is ReadDiagnosticsSetting diagnostics && string.IsNullOrWhiteSpace(diagnostics.ErrorCodeVariable) &&
                string.IsNullOrWhiteSpace(diagnostics.HardwareValueVariable) && string.IsNullOrWhiteSpace(diagnostics.HardwareActiveVariable))
                errors.Add(StepSettingError.Error("C402_005", "至少配置一项诊断输出变量"));
            AxisLifecycleValidator.Validate(context, setting, errors);
            if (setting is BindAxisSetting { Transport: TransportKind.EtherCAT })
                errors.Add(StepSettingError.Warning("C402_006", "EtherCAT 需要运行环境注册主站适配器，本插件不包含主站 SDK"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("C402_007", $"设置校验失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
