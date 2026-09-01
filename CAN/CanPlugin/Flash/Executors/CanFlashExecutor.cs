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
            if (setting.MaxBlockSize < 0)
                return Error("单块最大字节数不能为负数；设为 0 表示采用 ECU 返回的最大块长度");
            if (setting.BlockRetryCount < 0)
                return Error("重试次数不能为负数");
            if (setting.PreDownloadDelayMs < 0)
                return Error("下载前延时不能为负数");

            var filePath = await Evaluator.EvalStringAsync(setting.FilePath, context);
            if (!File.Exists(filePath))
                return Error($"固件文件不存在: {filePath}");

            // ── 解析固件 ─────────────────────────────────────────────────
            uint baseAddress = UdsExecutorHelper.ParseId(
                await Evaluator.EvalStringAsync(setting.BaseAddress, context));

            var content = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var parser = FirmwareParserFactory.Create(setting.Format, filePath);
            var parsedSegments = parser.Parse(content, baseAddress);
            var segments = await ApplyMappedRangeAsync(parsedSegments, setting, context);

            int totalBytes = segments.Sum(s => s.Length);
            if (totalBytes == 0)
                return Error("固件文件解析结果为空，没有可烧录的数据");

            if (setting.EnableLog)
            {
                context.LogAction?.Invoke($"UDS Flash: 固件 {filePath}");
                context.LogAction?.Invoke($"UDS Flash: 共 {segments.Count} 个数据段，合计 {totalBytes} 字节");
                context.LogAction?.Invoke(setting.UseFdFrame
                    ? "UDS Flash: ISO-TP 使用 CAN FD/BRS，分段帧最大 64 字节"
                    : "UDS Flash: ISO-TP 使用 Classic CAN，分段帧最大 8 字节");
                foreach (var seg in segments)
                    context.LogAction?.Invoke($"UDS Flash: 数据段 {seg}");
            }

            if (setting.PreDownloadDelayMs > 0)
            {
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 下载前等待 {setting.PreDownloadDelayMs} ms");
                await Task.Delay(setting.PreDownloadDelayMs, cancellationToken);
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
            int lastLoggedProgress = -1;

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
                    // 映射范围必须把同一范围的地址和长度传给擦除例程；历史 UI 状态即使保存为
                    // false，也不能让映射模式退化为无参数擦除。
                    if (setting.UseMappedRange || setting.EraseWithAddressAndLength != false)
                    {
                        // 带参数擦除的 option record：
                        // [addressAndLengthFormatId][memoryAddress][memorySize]。
                        eraseRequest.Add(alfid);
                        eraseRequest.AddRange(EncodeValue(segment.StartAddress, addressBytes));
                        eraseRequest.AddRange(EncodeValue((uint)segment.Length, lengthBytes));
                    }

                    if (setting.EnableLog)
                        context.LogAction?.Invoke(
                            $"UDS Flash: 擦除 0x{segment.StartAddress:X8}，长度 {segment.Length} 字节，" +
                            $"TX=[{UdsExecutorHelper.ToHex(eraseRequest.ToArray())}]");

                    var eraseResponse = await eraseClient.RequestAsync(eraseRequest.ToArray(), cancellationToken);
                    if (setting.EnableLog)
                        context.LogAction?.Invoke($"UDS Flash: 擦除 RX=[{UdsExecutorHelper.ToHex(eraseResponse.Data)}]");
                    if (!eraseResponse.IsPositive)
                        return Failed($"擦除失败: {eraseResponse.GetNrcDescription()}",
                            $"NRC=0x{eraseResponse.NegativeResponseCode:X2}; " +
                            $"TX=[{UdsExecutorHelper.ToHex(eraseRequest.ToArray())}]; " +
                            $"RX=[{UdsExecutorHelper.ToHex(eraseResponse.Data)}]");
                }

                // 请求下载 (0x34)
                var downloadRequest = new List<byte> { 0x34, dataFormatId, alfid };
                downloadRequest.AddRange(EncodeValue(segment.StartAddress, addressBytes));
                downloadRequest.AddRange(EncodeValue((uint)segment.Length, lengthBytes));

                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 请求下载 TX=[{UdsExecutorHelper.ToHex(downloadRequest.ToArray())}]");

                var downloadResponse = await client.RequestAsync(downloadRequest.ToArray(), cancellationToken);
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 请求下载 RX=[{UdsExecutorHelper.ToHex(downloadResponse.Data)}]");
                if (!downloadResponse.IsPositive)
                    return Failed($"请求下载失败: {downloadResponse.GetNrcDescription()}",
                        $"NRC=0x{downloadResponse.NegativeResponseCode:X2}");

                int blockSize = ResolveBlockSize(downloadResponse.Data, setting.MaxBlockSize);
                if (blockSize <= 0)
                    return Error("ECU 返回的最大块长度无效，无法确定分块大小");

                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 开始传输 0x{segment.StartAddress:X8}，分块大小 {blockSize} 字节");
                ReportProgress(context, setting, writtenBytes, totalBytes, ref lastLoggedProgress);

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

                    ReportProgress(context, setting, writtenBytes, totalBytes, ref lastLoggedProgress);

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
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS Flash: 校验 RX=[{UdsExecutorHelper.ToHex(checkResponse.Data)}]");
                if (!checkResponse.IsPositive)
                    return Failed($"固件校验失败: {checkResponse.GetNrcDescription()}",
                        $"NRC=0x{checkResponse.NegativeResponseCode:X2}");

                // 本项目 ECU 的 0x0202 校验例程在正响应最后附带厂商结果码：
                // 71 01 02 02 00 表示校验成功，非 00 表示例程执行未成功。仅判断 0x71
                // 会把 71 01 02 02 01 误判为成功，并导致下一步擦除返回 NRC 0x22。
                if (checkResponse.Data.Length >= 4 && checkResponse.Data[^1] != 0x00)
                {
                    byte routineResult = checkResponse.Data[^1];
                    return Failed(
                        $"固件校验例程返回失败状态 0x{routineResult:X2}",
                        $"RoutineResult=0x{routineResult:X2}; " +
                        $"TX=[{UdsExecutorHelper.ToHex(checkRequest.ToArray())}]; " +
                        $"RX=[{UdsExecutorHelper.ToHex(checkResponse.RawBytes)}]");
                }

                if (setting.EnableLog)
                    context.LogAction?.Invoke(
                        $"UDS Flash: {setting.CheckMode} 校验通过，校验值=0x{checkValue:X8}");
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

        int ecuMax = (int)Math.Min(usable, int.MaxValue);
        return configuredMax == 0 ? ecuMax : Math.Min(ecuMax, configuredMax);
    }

    /// <summary>
    /// 将离散固件段投影到用户指定的连续地址范围。该模式用于与部分刷写工具的
    /// “映射地址/映射结束地址/填充字节”配置对齐，保证擦除、0x34 和 CRC 使用同一范围。
    /// </summary>
    private static async Task<IReadOnlyList<FlashSegment>> ApplyMappedRangeAsync(
        IReadOnlyList<FlashSegment> sourceSegments,
        CanFlashSetting setting,
        IExecutionContext context)
    {
        if (!setting.UseMappedRange)
            return sourceSegments;

        if (string.IsNullOrWhiteSpace(setting.MappedStartAddress) ||
            string.IsNullOrWhiteSpace(setting.MappedEndAddress))
            throw new InvalidDataException("启用固件映射范围时必须配置映射起始地址和结束地址");

        uint startAddress = UdsExecutorHelper.ParseId(
            await Evaluator.EvalStringAsync(setting.MappedStartAddress, context));
        uint endAddress = UdsExecutorHelper.ParseId(
            await Evaluator.EvalStringAsync(setting.MappedEndAddress, context));
        int fillValue = (int)UdsExecutorHelper.ParseId(
            await Evaluator.EvalStringAsync(setting.GapFillByte, context));

        if (endAddress < startAddress)
            throw new InvalidDataException("映射结束地址不能小于映射起始地址");
        if (fillValue is < byte.MinValue or > byte.MaxValue)
            throw new InvalidDataException("映射填充字节必须在 0x00~0xFF 之间");

        ulong mappedLength = (ulong)endAddress - startAddress + 1;
        if (mappedLength > int.MaxValue)
            throw new InvalidDataException("映射范围超过当前插件可处理的最大大小（2 GB）");

        var mappedData = new byte[(int)mappedLength];
        Array.Fill(mappedData, (byte)fillValue);

        ulong mappedStart = startAddress;
        ulong mappedEndExclusive = (ulong)endAddress + 1;
        foreach (var segment in sourceSegments)
        {
            ulong segmentStart = segment.StartAddress;
            ulong segmentEndExclusive = segmentStart + (uint)segment.Length;
            if (segmentStart < mappedStart || segmentEndExclusive > mappedEndExclusive)
                throw new InvalidDataException(
                    $"固件数据段 0x{segment.StartAddress:X8}-0x{segment.EndAddress:X8} 超出映射范围 " +
                    $"0x{startAddress:X8}-0x{endAddress:X8}");

            Buffer.BlockCopy(segment.Data, 0, mappedData, (int)(segmentStart - mappedStart), segment.Length);
        }

        return
        [
            new FlashSegment
            {
                StartAddress = startAddress,
                Data = mappedData
            }
        ];
    }

    private static void ReportProgress(
        IExecutionContext context,
        CanFlashSetting setting,
        int written,
        int total,
        ref int lastLoggedPercent)
    {
        if (total <= 0)
            return;

        int percent = (int)((long)written * 100 / total);
        if (!string.IsNullOrWhiteSpace(setting.ProgressVariable))
            context.SetVariable(setting.ProgressVariable, percent);

        // 大文件可能包含数千个 0x36 块；只在整数百分比变化时记录，既能看到
        // 实时进度，也避免每个数据块都写日志造成明显额外开销。
        if (setting.EnableLog && percent != lastLoggedPercent)
        {
            context.LogAction?.Invoke(
                $"UDS Flash: 下载进度 {percent}% ({written:N0}/{total:N0} 字节)");
            lastLoggedPercent = percent;
        }
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
