using System.Windows;
using CAN.Flash;
using CAN.Flash.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanFlashEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.Flash";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanFlashEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanFlashSetting)new CanFlashPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        // ── CAN 连接 ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("UDS_001E", $"ConnectionName 表达式无效: {connErr}"));

        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("UDS_002", "TX ID 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TxId, context.ExecutionContext, out var txErr))
            errors.Add(StepSettingError.Error("UDS_002E", $"TxId 表达式无效: {txErr}"));

        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("UDS_003", "RX ID 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.RxId, context.ExecutionContext, out var rxErr))
            errors.Add(StepSettingError.Error("UDS_003E", $"RxId 表达式无效: {rxErr}"));

        if (s.ResponseTimeoutMs == 0 || s.ResponseTimeoutMs < -1)
            errors.Add(StepSettingError.Error("UDS_005", "响应超时必须大于 0，或为 -1 表示永不超时"));

        // ── 固件文件 ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(s.FilePath))
            errors.Add(StepSettingError.Error("UDS_F001", "固件文件路径不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.FilePath, context.ExecutionContext, out var pathErr))
            errors.Add(StepSettingError.Error("UDS_F001E", $"固件文件路径表达式无效: {pathErr}"));

        if (s.Format == FirmwareFormat.Binary)
        {
            if (string.IsNullOrWhiteSpace(s.BaseAddress))
                errors.Add(StepSettingError.Error("UDS_F002", "二进制格式必须指定基地址"));
            else if (!context.Evaluator.ValidateExpression(s.BaseAddress, context.ExecutionContext, out var baseErr))
                errors.Add(StepSettingError.Error("UDS_F002E", $"基地址表达式无效: {baseErr}"));
        }

        // ── 下载参数 ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(s.AddressAndLengthFormatId))
            errors.Add(StepSettingError.Error("UDS_F003", "地址与长度格式标识不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.AddressAndLengthFormatId, context.ExecutionContext, out var alfidErr))
            errors.Add(StepSettingError.Error("UDS_F003E", $"地址与长度格式标识表达式无效: {alfidErr}"));

        if (string.IsNullOrWhiteSpace(s.DataFormatId))
            errors.Add(StepSettingError.Error("UDS_F004", "数据格式标识不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.DataFormatId, context.ExecutionContext, out var dfidErr))
            errors.Add(StepSettingError.Error("UDS_F004E", $"数据格式标识表达式无效: {dfidErr}"));

        if (s.MaxBlockSize < 0)
            errors.Add(StepSettingError.Error("UDS_F005", "单块最大字节数不能为负数；0 表示采用 ECU 返回的最大块长度"));
        else if (s.MaxBlockSize > 4095)
            errors.Add(StepSettingError.Warning("UDS_F006", "单块最大字节数超过 4095，多数 ISO-TP 实现无法承载，建议减小"));

        if (s.BlockRetryCount < 0)
            errors.Add(StepSettingError.Error("UDS_F007", "块重试次数不能为负数"));

        if (s.InterBlockDelayMs < 0)
            errors.Add(StepSettingError.Error("UDS_F008", "块间延时不能为负数"));

        if (s.PreDownloadDelayMs < 0)
            errors.Add(StepSettingError.Error("UDS_F008A", "下载前延时不能为负数"));

        // ── 映射与填充 ──────────────────────────────────────────────
        if (s.UseMappedRange)
        {
            ValidateExpression(s.MappedStartAddress, "UDS_F014", "启用映射范围时必须指定映射起始地址", "映射起始地址表达式无效", context, errors);
            ValidateExpression(s.MappedEndAddress, "UDS_F015", "启用映射范围时必须指定映射结束地址", "映射结束地址表达式无效", context, errors);
            ValidateExpression(s.GapFillByte, "UDS_F016", "映射填充字节不能为空", "映射填充字节表达式无效", context, errors);
        }

        // ── 擦除 ────────────────────────────────────────────────────
        if (s.EraseBeforeDownload)
        {
            if (string.IsNullOrWhiteSpace(s.EraseRoutineId))
                errors.Add(StepSettingError.Error("UDS_F009", "启用擦除时必须指定擦除例程 ID"));
            else if (!context.Evaluator.ValidateExpression(s.EraseRoutineId, context.ExecutionContext, out var eraseErr))
                errors.Add(StepSettingError.Error("UDS_F009E", $"擦除例程 ID 表达式无效: {eraseErr}"));

            if (s.EraseTimeoutMs <= 0)
                errors.Add(StepSettingError.Error("UDS_F010", "擦除超时必须大于 0"));
        }

        // ── 校验 ────────────────────────────────────────────────────
        if (s.CheckMode != FlashCheckMode.None)
        {
            if (string.IsNullOrWhiteSpace(s.CheckRoutineId))
                errors.Add(StepSettingError.Error("UDS_F011", "启用校验时必须指定校验例程 ID"));
            else if (!context.Evaluator.ValidateExpression(s.CheckRoutineId, context.ExecutionContext, out var checkErr))
                errors.Add(StepSettingError.Error("UDS_F011E", $"校验例程 ID 表达式无效: {checkErr}"));
        }

        // ── 输出变量 ────────────────────────────────────────────────
        ValidateIntVariable(context, s.ProgressVariable, "UDS_F012", errors);
        ValidateIntVariable(context, s.ResultVariable, "UDS_F013", errors);

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }

    private static void ValidateExpression(
        string expression,
        string errorCode,
        string emptyMessage,
        string invalidMessage,
        StepEditorValidationContext context,
        List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(expression))
            errors.Add(StepSettingError.Error(errorCode, emptyMessage));
        else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var error))
            errors.Add(StepSettingError.Error($"{errorCode}E", $"{invalidMessage}: {error}"));
    }

    private static void ValidateIntVariable(
        StepEditorValidationContext context, string variableName, string code, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return;

        if (!context.ExecutionContext.HasVariable(variableName))
        {
            errors.Add(StepSettingError.Error(code, $"变量 {variableName} 不存在，请先创建该变量"));
            return;
        }

        var val = context.ExecutionContext.GetVariable(variableName);
        if (val is not null and not int and not long)
            errors.Add(StepSettingError.Warning($"{code}W", $"变量 {variableName} 类型不匹配，期望整数，实际类型 {val.GetType().Name}"));
    }
}
