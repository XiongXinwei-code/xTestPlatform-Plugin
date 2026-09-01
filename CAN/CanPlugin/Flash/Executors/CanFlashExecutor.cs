using CAN.Flash.HexParser;
using CAN.Flash.Models;
using CAN.Helpers;
using CAN.UDS;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Flash.Executors;

public sealed class CanFlashExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanFlashPlugin().CreateSerializer();
        var setting = (CanFlashSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            // ── 执行器内部校验 ───────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(setting.FilePath))
                return Error("固件文件路径未配置");
            if (setting.MaxBlockSize <= 0)
                return Error("单块最大字节数必须大于 0");
            if (setting.BlockRetryCount < 0)
                return Error("重试次数不能为负数");

            var filePath = await Evaluator.EvalStringAsync(setting.FilePath, context);
            if (!File.Exists(filePath))
                return Error($"固件文件不存在: {filePath}");

            // ── 解析固件 ─────────────────────────────────────────────────
            uint baseAddress = UdsExecutorHelper.ParseId(
                await Evaluator.EvalStringAsync(setting.BaseAddress, context));

            var content = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var parser = FirmwareParserFactory.Create(setting.Format, filePath);
            var segments = parser.Parse(content, baseAddress);

            int totalBytes = segments.Sum(s => s.Length);
            if (totalBytes == 0)
                return Error("固件文件解析结果为空，没有可烧录的数据");

            if (setting.EnableLog)
            {
                context.LogAction?.Invoke($"UDS Flash: 固件 {filePath}");
                context.LogAction?.Invoke($"UDS Flash: 共 {segments.Count} 个数据段，合计 {totalBytes} 字节");
                foreach (var seg in segments)
                    context.LogAction?.Invoke($"UDS Flash: 数据段 {seg}");
            }

            // ── 创建 UDS 客户端 ──────────────────────────────────────────
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return Error(error!);

            // 擦除耗时远长于普通请求，使用独立的长超时客户端
            int normalTimeout = setting.ResponseTimeoutMs;
            setting.ResponseTimeoutMs = setting.EraseTimeoutMs > 0 ? setting.EraseTimeoutMs : normalTimeout;
            var (eraseClient, eraseError) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            setting.ResponseTimeoutMs = normalTimeout;
            if (eraseClient == null)
                return Error(eraseError!);

            // ── 解析格式标识 ─────────────────────────────────────────────
            byte alfid = (byte)UdsExecutorHelper.ParseId(
                await Evaluator.EvalStringAsync(setting.AddressAndLengthFormatId, context));
            byte dataFormatId = (byte)UdsExecutorHelper.ParseId(
                await Evaluator.EvalStringAsync(setting.DataFormatId, context));

            int addressBytes = alfid & 0x0F;
            int lengthBytes = (alfid >> 4) & 0x0F;
            if (addressBytes is < 1 or > 4 || lengthBytes is < 1 or > 4)
                return Error($"地址与长度格式标识 0x{alfid:X2} 非法，地址与长度字节数均须在 1~4 之间");

            int writtenBytes = 0;

            // ── 逐段烧录 ─────────────────────────────────────────────────
            foreach (var segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 擦除
                if (setting.EraseBeforeDownload)
                {
                    uint eraseRoutineId = UdsExecutorHelper.ParseId(
                        await Evaluator.EvalStringAsync(setting.EraseRoutineId, context));

                    var eraseRequest = new List<byte>
                    {
                        0x31, 0x01,
                        (byte)(eraseRoutineId >> 8), (byte)eraseRoutineId
                    };
                    // 擦除例程的 option record 与 RequestDownload 一样以 ALFID 开头：
                    // [addressAndLengthFormatId][memoryAddress][memorySize]。
                    // 缺少 ALFID 会使常见的 FF00 擦除例程以 NRC 0x13 拒绝请求。
                    eraseRequest.Add(alfid);
                    eraseRequest.AddRange(EncodeValue(segment.StartAddress, addressBytes));
                    eraseRequest.AddRange(EncodeValue((uint)segment.Length, lengthBytes));

                    if (setting.EnableLog)
                        context.LogAction?.Invoke($"UDS Flash: 擦除 0x{segment.StartAddress:X8}，长度 {segment.Length} 字节");

                    var eraseResponse = await eraseClient.RequestAsync(eraseRequest.ToArray(), cancellationToken);
                    if (!eraseResponse.IsPositive)
                        return Failed($"擦除失败: {eraseResponse.GetNrcDescription()}",
                            $"NRC=0x{eraseResponse.NegativeResponseCode:X2}");
                }

                // 请求下载 (0x34)
                var downloadRequest = new List<byte> { 0x34, dataFormatId, alfid };
                downloadRequest.AddRange(EncodeValue(segment.StartAddress, addressBytes));
                downloadRequest.AddRange(EncodeValue((uint)segment.Length, lengthBytes));

                var downloadResponse = await client.RequestAsync(downloadRequest.ToArray(), cancellationToken);
                if (!downloadResponse.IsPositive)
                    return Failed($"请求下载失败: {downloadResponse.GetNrcDescription()}",
                        $"NRC=0x{downloadResponse.NegativeResponseCode:X2}");

                int blockSize = ResolveBlockSize(downloadResponse.Data, setting.MaxBlockSize);
                if (blockSize <= 0)
                    return Error("ECU 返回的最大块长度无效，无法确定分块大小");

                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 开始传输 0x{segment.StartAddress:X8}，分块大小 {blockSize} 字节");

                // 分块传输 (0x36)
                byte blockCounter = 1;
                int offset = 0;
                while (offset < segment.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int chunkLength = Math.Min(blockSize, segment.Length - offset);
                    var transferRequest = new byte[2 + chunkLength];
                    transferRequest[0] = 0x36;
                    transferRequest[1] = blockCounter;
                    Buffer.BlockCopy(segment.Data, offset, transferRequest, 2, chunkLength);

                    UdsResponse? transferResponse = null;
                    for (int attempt = 0; attempt <= setting.BlockRetryCount; attempt++)
                    {
                        transferResponse = await client.RequestAsync(transferRequest, cancellationToken);
                        if (transferResponse.IsPositive)
                            break;

                        if (attempt < setting.BlockRetryCount && setting.EnableLog)
                            context.LogAction?.Invoke(
                                $"UDS Flash: 块 {blockCounter} 传输失败({transferResponse.GetNrcDescription()})，第 {attempt + 1} 次重试");
                    }

                    if (transferResponse is null || !transferResponse.IsPositive)
                        return Failed(
                            $"数据传输失败于地址 0x{segment.StartAddress + (uint)offset:X8}: {transferResponse?.GetNrcDescription()}",
                            $"NRC=0x{transferResponse?.NegativeResponseCode:X2}");

                    offset += chunkLength;
                    writtenBytes += chunkLength;
                    blockCounter = (byte)(blockCounter + 1); // 到 0xFF 后自动回绕到 0x00

                    ReportProgress(context, setting, writtenBytes, totalBytes);

                    if (setting.InterBlockDelayMs > 0)
                        await Task.Delay(setting.InterBlockDelayMs, cancellationToken);
                }

                // 结束传输 (0x37)
                var exitResponse = await client.RequestAsync([0x37], cancellationToken);
                if (!exitResponse.IsPositive)
                    return Failed($"结束传输失败: {exitResponse.GetNrcDescription()}",
                        $"NRC=0x{exitResponse.NegativeResponseCode:X2}");

                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 数据段 0x{segment.StartAddress:X8} 传输完成");
            }

            // ── 校验 (0x31) ──────────────────────────────────────────────
            if (setting.CheckMode != FlashCheckMode.None)
            {
                uint checkRoutineId = UdsExecutorHelper.ParseId(
                    await Evaluator.EvalStringAsync(setting.CheckRoutineId, context));

                var payloads = segments.Select(s => s.Data);
                uint checkValue = setting.CheckMode == FlashCheckMode.Crc32
                    ? FlashCrc.Crc32(payloads)
                    : FlashCrc.Checksum(payloads);

                var checkRequest = new List<byte>
                {
                    0x31, 0x01,
                    (byte)(checkRoutineId >> 8), (byte)checkRoutineId
                };
                checkRequest.AddRange(EncodeValue(checkValue, 4));

                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 校验 {setting.CheckMode}=0x{checkValue:X8}");

                var checkResponse = await eraseClient.RequestAsync(checkRequest.ToArray(), cancellationToken);
                if (!checkResponse.IsPositive)
                    return Failed($"固件校验失败: {checkResponse.GetNrcDescription()}",
                        $"NRC=0x{checkResponse.NegativeResponseCode:X2}");
            }

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, writtenBytes);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"UDS Flash: 烧录完成，共写入 {writtenBytes} 字节");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"{writtenBytes} 字节" }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) }
            };
        }
    }

    /// <summary>按大端序将数值编码为指定字节数</summary>
    private static byte[] EncodeValue(uint value, int byteCount)
    {
        var result = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
            result[byteCount - 1 - i] = (byte)(value >> (i * 8));
        return result;
    }

    /// <summary>
    /// 解析 0x34 正响应中的 maxNumberOfBlockLength，并与用户配置取较小值。
    /// 响应格式：74 [lengthFormatIdentifier] [maxNumberOfBlockLength...]
    /// maxNumberOfBlockLength 含 SID 与块序号，实际数据长度需再减 2。
    /// </summary>
    private static int ResolveBlockSize(byte[] responseData, int configuredMax)
    {
        if (responseData.Length < 2)
            return configuredMax;

        int lengthFieldSize = (responseData[0] >> 4) & 0x0F;
        if (lengthFieldSize <= 0 || lengthFieldSize > responseData.Length - 1)
            return configuredMax;

        long maxBlockLength = 0;
        for (int i = 0; i < lengthFieldSize; i++)
            maxBlockLength = (maxBlockLength << 8) | responseData[1 + i];

        long usable = maxBlockLength - 2; // 扣除 SID 与块序号
        if (usable <= 0)
            return configuredMax;

        return (int)Math.Min(usable, configuredMax);
    }

    private static void ReportProgress(IExecutionContext context, CanFlashSetting setting, int written, int total)
    {
        if (string.IsNullOrWhiteSpace(setting.ProgressVariable) || total <= 0)
            return;

        int percent = (int)((long)written * 100 / total);
        context.SetVariable(setting.ProgressVariable, percent);
    }

    private static ExecutionResult Error(string message) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = new ErrorInfo { Message = message }
        }
    };

    private static ExecutionResult Failed(string message, string value) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Failed,
            Error = new ErrorInfo { Message = message },
            Value = value
        }
    };
}
