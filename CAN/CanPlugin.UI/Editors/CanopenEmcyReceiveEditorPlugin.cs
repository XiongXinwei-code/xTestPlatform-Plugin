using System.Windows;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

/// <summary>CANopen_EmcyReceive 编辑器插件：创建编辑器并在编辑期校验设置</summary>
public sealed class CanopenEmcyReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "CANopen.EmcyReceive";
    public string IconPath => string.Empty;

    /// <summary>创建编辑器视图并加载步骤设置</summary>
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanopenEmcyReceiveEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    /// <summary>编辑期校验：必填/表达式语法、字面量取值范围、变量存在性，以及前序是否存在 CAN_Open</summary>
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanopenEmcyReceiveSetting)new CanopenEmcyReceivePlugin().CreateSerializer().Deserialize(context.Setting, 1);

        CanopenValidation.Expr(context, s.ConnectionName, "ConnectionName", "CO_001", errors);
        CanopenValidation.Expr(context, s.NodeId, "NodeId", "CO_002", errors);
        CanopenValidation.Range(s.NodeId, "NodeId", 1, 127, "CO_002R", errors);
        // 期望错误码可选，留空表示任意 EMCY
        if (!string.IsNullOrWhiteSpace(s.ExpectedErrorCode))
        {
            CanopenValidation.Expr(context, s.ExpectedErrorCode, "ExpectedErrorCode", "CO_011", errors);
            CanopenValidation.Range(s.ExpectedErrorCode, "ExpectedErrorCode", 0, 0xFFFF, "CO_011R", errors);
        }
        CanopenValidation.Positive(s.TimeoutMs, "等待超时", "CO_005", errors);
        CanopenValidation.Variable(context, s.ErrorCodeVariable, v => v is string, "string", "CO_006", errors);
        CanopenValidation.Variable(context, s.ErrorRegisterVariable, v => v is string, "string", "CO_006", errors);
        CanopenValidation.Variable(context, s.ManufacturerDataVariable, v => v is string, "string", "CO_006", errors);

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
