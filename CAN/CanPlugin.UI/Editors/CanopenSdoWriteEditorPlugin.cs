using System.Windows;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

/// <summary>CANopen_SdoWrite 编辑器插件：创建编辑器并在编辑期校验设置</summary>
public sealed class CanopenSdoWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "CANopen.SdoWrite";
    public string IconPath => string.Empty;

    /// <summary>创建编辑器视图并加载步骤设置</summary>
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanopenSdoWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    /// <summary>编辑期校验：必填/表达式语法、字面量取值范围、变量存在性，以及前序是否存在 CAN_Open</summary>
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanopenSdoWriteSetting)new CanopenSdoWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);

        CanopenValidation.Expr(context, s.ConnectionName, "ConnectionName", "CO_001", errors);
        CanopenValidation.Expr(context, s.NodeId, "NodeId", "CO_002", errors);
        CanopenValidation.Range(s.NodeId, "NodeId", 1, 127, "CO_002R", errors);
        CanopenValidation.Expr(context, s.Index, "Index", "CO_003", errors);
        CanopenValidation.Range(s.Index, "Index", 0, 0xFFFF, "CO_003R", errors);
        CanopenValidation.Expr(context, s.SubIndex, "SubIndex", "CO_004", errors);
        CanopenValidation.Range(s.SubIndex, "SubIndex", 0, 255, "CO_004R", errors);
        CanopenValidation.Positive(s.ResponseTimeoutMs, "响应超时", "CO_005", errors);
        CanopenValidation.EdsFile(s.EdsFilePath, errors);
        CanopenValidation.Expr(context, s.Value, "Value", "CO_007", errors);

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
