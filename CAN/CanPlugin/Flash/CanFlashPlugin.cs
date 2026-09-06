using CAN.Flash.Executors;
using CAN.Flash.Models;
using MessagePack;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN.Flash;

public sealed class CanFlashPlugin : StepPluginBase<CanFlashSetting>, IStepPlugin
{
    /// <summary>
    /// 版本 2 新增映射范围、填充、擦除参数模式、自动块大小与下载前延时；
    /// 迁移旧设置时依赖 MessagePack 对缺失字段使用模型默认值。
    /// </summary>
    protected override int CurrentSettingVersion => 2;

    public override string StepTypeId => "UDS.Flash";
    public override string DisplayName => "UDS_Flash";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 UDS 服务将固件文件烧录到 ECU，依次执行擦除例程（0x31）、请求下载（0x34）、
        分块传输数据（0x36）、结束传输（0x37）与校验例程（0x31）。支持 Intel HEX、
        Motorola S-Record 与原始二进制三种固件格式。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | FilePath | string([ExpressionField]) | 是 | — | 固件文件路径 |
        | Format | 枚举 | 否 | Auto | 可选值：Auto, IntelHex, SRecord, Binary；Auto 按扩展名识别 |
        | BaseAddress | string([ExpressionField]) | 否 | "0x08000000" | 基地址，仅 Binary 格式使用 |
        | UseMappedRange | bool | 否 | false | 是否将固件映射为一个连续地址范围；启用后对地址空洞填充并只执行一次擦除/下载 |
        | MappedStartAddress | string([ExpressionField]) | 条件必填 | 空 | 映射范围起始地址，仅 UseMappedRange=true 时使用 |
        | MappedEndAddress | string([ExpressionField]) | 条件必填 | 空 | 映射范围结束地址（含），仅 UseMappedRange=true 时使用 |
        | GapFillByte | string([ExpressionField]) | 否 | 0 | 映射范围内 HEX/S-Record 地址空洞的填充字节（0x00~0xFF） |
        | AddressAndLengthFormatId | string([ExpressionField]) | 否 | "0x44" | 地址与长度格式标识，高半字节为长度字节数、低半字节为地址字节数；用于擦除例程与 0x34 请求下载 |
        | DataFormatId | string([ExpressionField]) | 否 | "0x00" | 数据格式标识，0x00 表示不压缩不加密 |
        | EraseBeforeDownload | bool | 否 | true | 是否在下载前执行擦除例程 |
        | EraseRoutineId | string([ExpressionField]) | 否 | "0xFF00" | 擦除例程 ID |
        | EraseWithAddressAndLength | bool | 否 | true | true 时擦除请求附带 [ALFID][地址][长度]；false 时执行无参数擦除；UseMappedRange=true 时强制带参数 |
        | EraseTimeoutMs | int | 否 | 30000 | 擦除超时毫秒数 |
        | MaxBlockSize | int | 否 | 512 | 单块最大字节数，实际不超过 ECU 在 0x34 响应中允许的长度；0 表示完全采用 ECU 返回值 |
        | PreDownloadDelayMs | int | 否 | 0 | 擦除与下载前等待时间，用于等待 FlashDriver 激活 |
        | CheckMode | 枚举 | 否 | Crc32 | 可选值：None, Crc32, Checksum |
        | CheckRoutineId | string([ExpressionField]) | 否 | "0x0202" | 校验例程 ID |
        | BlockRetryCount | int | 否 | 2 | 单块传输失败后的重试次数 |
        | InterBlockDelayMs | int | 否 | 0 | 每块之间的间隔毫秒数 |
        | ProgressVariable | string(变量路径) | 否 | 空 | 进度变量名，写入类型为 int（0~100） |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 int（已烧录总字节数） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | FrameType | 枚举 | 否 | Standard | CAN ID 类型：Standard（11-bit）或 Extended（29-bit） |
        | UseFdFrame | bool | 否 | false | 是否按 CAN FD/BRS 发送 ISO-TP；启用后分段帧最大使用 64 字节数据区 |
        | ResponseTimeoutMs | int | 否 | 5000 | 普通请求的响应超时毫秒数 |

        ## 行为

        - 本插件不切换诊断会话、不执行安全访问，需由前置步骤先完成 `UDS_DiagSession`（进入编程会话）与安全解锁
        - 烧录期间不要再次发送 0x10 服务，否则会清除已有的安全解锁状态
        - 默认将固件解析为多个地址连续的数据段，逐段执行 0x34 → 0x36 循环 → 0x37；启用映射范围后，所有数据段会按指定范围合并，地址空洞使用填充字节补齐
        - 实际分块大小取 MaxBlockSize 与 ECU 在 0x34 响应中允许长度的较小值；MaxBlockSize 为 0 时不再额外限制
        - CAN_Open 选择 CAN FD 并不自动改变 UDS 帧格式；ECU 刷写流程使用 CAN FD/BRS 时，还必须在本步骤启用 UseFdFrame
        - 擦除例程可按 ECU 规范选择携带或不携带 [ALFID][地址][长度] 参数；映射范围模式始终携带完整范围参数
        - 块序号从 1 开始循环递增，到 0xFF 后回绕到 0x00
        - 单块传输失败时按 BlockRetryCount 重试，重试耗尽则步骤报错
        - 启用日志时按整数百分比输出下载进度，并在校验成功后明确输出校验方式与校验值
        - ECU 返回否定响应、请求超时或固件文件解析失败时步骤报错
        - 用户主动取消时立即停止传输并返回中止状态

        ## 示例

        ```json
        {
          "ConnectionName": "\"CAN1\"",
          "TxId": "\"0x7E0\"",
          "RxId": "\"0x7E8\"",
          "UseFdFrame": true,
          "FilePath": "\"D:\\\\firmware\\\\app.hex\"",
          "Format": "IntelHex",
          "UseMappedRange": true,
          "MappedStartAddress": "\"0x02000000\"",
          "MappedEndAddress": "\"0x0237FFFF\"",
          "GapFillByte": "\"0x00\"",
          "AddressAndLengthFormatId": "\"0x44\"",
          "EraseBeforeDownload": true,
          "EraseRoutineId": "\"0xFF00\"",
          "EraseWithAddressAndLength": true,
          "MaxBlockSize": 0,
          "PreDownloadDelayMs": 500,
          "CheckMode": "Crc32",
          "CheckRoutineId": "\"0x0202\"",
          "ProgressVariable": "Locals.flashProgress",
          "ResultVariable": "Locals.flashedBytes"
        }
        ```

        ## 相关插件

        - `CAN_Open`：打开本插件使用的 CAN 连接
        - `UDS_DiagSession`：进入编程会话（必须在本步骤之前执行）
        - `UDS_SecurityAccess`：安全解锁（必须在本步骤之前执行）
        - `UDS_RawRequest`：烧录完成后发送 ECU 复位（0x11 0x01）
        """;

    public override IStepExecutor CreateExecutor() => new CanFlashExecutor();

    protected override CanFlashSetting MigrateSetting(byte[] data, int fromVersion)
    {
        if (fromVersion == 1)
        {
            var migrated = MessagePackSerializer.Deserialize<CanFlashSetting>(data, SerializerOptions);
            migrated.EraseWithAddressAndLength ??= true;
            return migrated;
        }

        return base.MigrateSetting(data, fromVersion);
    }

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var blockSize = s.MaxBlockSize == 0 ? "自动" : $"{s.MaxBlockSize} 字节";
        return $"Flash {s.Format} 文件 {s.FilePath} → 块大小 {blockSize}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (CanFlashSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

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
        StepSettingValidationContext context,
        List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(expression))
            errors.Add(StepSettingError.Error(errorCode, emptyMessage));
        else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var error))
            errors.Add(StepSettingError.Error($"{errorCode}E", $"{invalidMessage}: {error}"));
    }

    private static void ValidateIntVariable(
        StepSettingValidationContext context, string variableName, string code, List<StepSettingError> errors)
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
