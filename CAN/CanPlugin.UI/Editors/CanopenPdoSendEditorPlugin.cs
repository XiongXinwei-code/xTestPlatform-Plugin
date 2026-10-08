using System.Windows;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

/// <summary>CANopen_PdoSend 编辑器插件：创建编辑器并在编辑期校验设置</summary>
public sealed class CanopenPdoSendEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "CANopen.PdoSend";
    public string IconPath => string.Empty;

    /// <summary>创建编辑器视图并加载步骤设置</summary>
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanopenPdoSendEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    /// <summary>编辑期校验：必填/表达式语法、字面量取值范围、变量存在性，以及前序是否存在 CAN_Open</summary>
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanopenPdoSendSetting)new CanopenPdoSendPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        CanopenValidation.Expr(context, s.ConnectionName, "ConnectionName", "CO_001", errors);
        CanopenValidation.Expr(context, s.CobId, "CobId", "CO_008", errors);
        CanopenValidation.Range(s.CobId, "CobId", 0, 0x7FF, "CO_008R", errors);
        CanopenValidation.Expr(context, s.Data, "Data", "CO_009", errors);

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
