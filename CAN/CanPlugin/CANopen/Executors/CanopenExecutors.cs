using CAN.CANopen.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.CANopen.Executors;

/// <summary>CANopen_SdoRead 执行器：通过 SDO 上传读取对象字典条目并写入结果变量</summary>
public sealed class CanopenSdoReadExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var setting = (CanopenSdoReadSetting)new CanopenSdoReadPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.ResponseTimeoutMs <= 0)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: "响应超时必须大于 0");

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            byte node = await CanopenExecutorHelper.EvalNodeIdAsync(setting.NodeId, context, allowBroadcast: false);
            uint index = await CanopenExecutorHelper.EvalNumberAsync(setting.Index, context, "Index");
            uint sub = await CanopenExecutorHelper.EvalNumberAsync(setting.SubIndex, context, "SubIndex");
            if (index > 0xFFFF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"Index 超出范围 0x0000~0xFFFF: 0x{index:X}");
            if (sub > 0xFF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"SubIndex 超出范围 0~255: {sub}");

            var client = new CanopenSdoClient(bus, node, setting.ResponseTimeoutMs);
            bool block = setting.TransferMode == CanopenSdoTransferMode.Block;
            var raw = await client.UploadAsync((ushort)index, (byte)sub, block, cancellationToken);
            var value = CanopenDataCodec.Decode(setting.DataType, raw);

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, value);

            context.Log($"SDO 读取 节点{node} 0x{index:X4}sub{sub}：{value}（原始数据 [{CanopenExecutorHelper.ToHex(raw)}]）");
            return CanopenExecutorHelper.Result(TestStatus.Passed, value);
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_SdoWrite 执行器：按数据类型编码写入值并通过 SDO 下载</summary>
public sealed class CanopenSdoWriteExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var setting = (CanopenSdoWriteSetting)new CanopenSdoWritePlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.ResponseTimeoutMs <= 0)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: "响应超时必须大于 0");

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            byte node = await CanopenExecutorHelper.EvalNodeIdAsync(setting.NodeId, context, allowBroadcast: false);
            uint index = await CanopenExecutorHelper.EvalNumberAsync(setting.Index, context, "Index");
            uint sub = await CanopenExecutorHelper.EvalNumberAsync(setting.SubIndex, context, "SubIndex");
            if (index > 0xFFFF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"Index 超出范围 0x0000~0xFFFF: 0x{index:X}");
            if (sub > 0xFF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"SubIndex 超出范围 0~255: {sub}");

            var text = await CanopenExecutorHelper.EvalTextAsync(setting.Value, context);
            byte[] data;
            try { data = CanopenDataCodec.Encode(setting.DataType, text); }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                return CanopenExecutorHelper.Result(TestStatus.Error, error: $"写入值 \"{text}\" 无法转换为 {setting.DataType}: {ex.Message}");
            }

            var client = new CanopenSdoClient(bus, node, setting.ResponseTimeoutMs);
            bool block = setting.TransferMode == CanopenSdoTransferMode.Block;
            await client.DownloadAsync((ushort)index, (byte)sub, data, block, cancellationToken);

            context.Log($"SDO 写入 节点{node} 0x{index:X4}sub{sub}：{text}（数据 [{CanopenExecutorHelper.ToHex(data)}]）");
            return CanopenExecutorHelper.Result(TestStatus.Passed, text);
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_NmtControl 执行器：在 COB-ID 0x000 上发送 NMT 命令（无应答）</summary>
public sealed class CanopenNmtControlExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = context.CurrentStep!.Step;
            var setting = (CanopenNmtControlSetting)new CanopenNmtControlPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            byte node = await CanopenExecutorHelper.EvalNodeIdAsync(setting.NodeId, context, allowBroadcast: true);
            // NMT 命令字（CiA 301）
            byte cs = setting.Command switch
            {
                CanopenNmtCommand.Start => 0x01,
                CanopenNmtCommand.Stop => 0x02,
                CanopenNmtCommand.EnterPreOperational => 0x80,
                CanopenNmtCommand.ResetNode => 0x81,
                CanopenNmtCommand.ResetCommunication => 0x82,
                _ => throw new NotSupportedException($"不支持的 NMT 命令: {setting.Command}")
            };

            // NMT 报文：[命令字, 节点 ID]，节点 ID 0 表示广播
            bus.Send(0x000, [cs, node]);
            context.Log($"NMT {setting.Command} → {(node == 0 ? "所有节点" : $"节点{node}")}");
            return CanopenExecutorHelper.Result(TestStatus.Passed, setting.Command.ToString());
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_WaitHeartbeat 执行器：监听 0x700 + NodeId 心跳，直到出现期望状态或超时</summary>
public sealed class CanopenWaitHeartbeatExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var setting = (CanopenWaitHeartbeatSetting)new CanopenWaitHeartbeatPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.TimeoutMs <= 0)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: "等待超时必须大于 0");

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            byte node = await CanopenExecutorHelper.EvalNodeIdAsync(setting.NodeId, context, allowBroadcast: false);
            uint cobId = 0x700u + node;
            var deadline = DateTime.UtcNow.AddMilliseconds(setting.TimeoutMs);
            CanopenNmtState? lastState = null;

            // 在总超时内循环接收；状态不符时继续等待，并记录最后一次状态用于超时提示
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;

                var msg = await bus.ReceiveAsync(cobId, remaining, cancellationToken);
                if (msg == null) break;
                if (msg.Data.Length < 1) continue;

                // bit7 为节点守护翻转位，心跳状态只取低 7 位
                var state = ToState(msg.Data[0] & 0x7F);
                if (state == null) continue;
                lastState = state;

                if (setting.ExpectedState == CanopenNmtState.Any || setting.ExpectedState == state)
                {
                    var name = state.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(setting.StateVariable))
                        context.SetVariable(setting.StateVariable, name);
                    context.Log($"收到节点{node}心跳，状态 {name}");
                    return CanopenExecutorHelper.Result(TestStatus.Passed, name);
                }
            }

            var detail = lastState == null ? "未收到心跳" : $"最后收到的状态为 {lastState}";
            return CanopenExecutorHelper.Result(TestStatus.Error,
                error: $"等待节点{node}进入 {setting.ExpectedState} 状态超时（{setting.TimeoutMs} ms），{detail}");
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }

    /// <summary>心跳状态字节转 NMT 状态，未知值返回 null</summary>
    private static CanopenNmtState? ToState(int code) => code switch
    {
        0x00 => CanopenNmtState.BootUp,
        0x04 => CanopenNmtState.Stopped,
        0x05 => CanopenNmtState.Operational,
        0x7F => CanopenNmtState.PreOperational,
        _ => null
    };
}

/// <summary>CANopen_PdoSend 执行器：按 COB-ID 发送一帧 0~8 字节 PDO</summary>
public sealed class CanopenPdoSendExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = context.CurrentStep!.Step;
            var setting = (CanopenPdoSendSetting)new CanopenPdoSendPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            uint cobId = await CanopenExecutorHelper.EvalNumberAsync(setting.CobId, context, "CobId");
            if (cobId > 0x7FF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"COB-ID 超出 11 位范围: 0x{cobId:X}");

            var text = await CanopenExecutorHelper.EvalTextAsync(setting.Data, context);
            var data = CanopenExecutorHelper.ParseHex(text);
            if (data.Length > 8) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"PDO 数据不能超过 8 字节，当前 {data.Length} 字节");

            bus.Send(cobId, data);
            var hex = CanopenExecutorHelper.ToHex(data);
            context.Log($"PDO 发送 0x{cobId:X3}: [{hex}]");
            return CanopenExecutorHelper.Result(TestStatus.Passed, hex);
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_PdoReceive 执行器：等待指定 COB-ID 的 PDO 并以十六进制写入结果变量</summary>
public sealed class CanopenPdoReceiveExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var setting = (CanopenPdoReceiveSetting)new CanopenPdoReceivePlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.TimeoutMs <= 0)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: "等待超时必须大于 0");

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            uint cobId = await CanopenExecutorHelper.EvalNumberAsync(setting.CobId, context, "CobId");
            if (cobId > 0x7FF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"COB-ID 超出 11 位范围: 0x{cobId:X}");

            var msg = await bus.ReceiveAsync(cobId, setting.TimeoutMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (msg == null)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: $"在 {setting.TimeoutMs} ms 内未收到 COB-ID 0x{cobId:X3} 的 PDO");

            var hex = CanopenExecutorHelper.ToHex(msg.Data);
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, hex);

            context.Log($"PDO 接收 0x{cobId:X3}: [{hex}]");
            return CanopenExecutorHelper.Result(TestStatus.Passed, hex);
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_SyncSend 执行器：发送一帧 SYNC（可选 1 字节计数器）</summary>
public sealed class CanopenSyncSendExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = context.CurrentStep!.Step;
            var setting = (CanopenSyncSendSetting)new CanopenSyncSendPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            uint cobId = await CanopenExecutorHelper.EvalNumberAsync(setting.CobId, context, "CobId");
            if (cobId > 0x7FF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"COB-ID 超出 11 位范围: 0x{cobId:X}");

            uint counter = string.IsNullOrWhiteSpace(setting.Counter)
                ? 0
                : await CanopenExecutorHelper.EvalNumberAsync(setting.Counter, context, "Counter");
            if (counter > 240) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"SYNC 计数器必须在 0~240 之间，当前值 {counter}");

            // CiA 301：计数器为 0 时 SYNC 不带数据，否则携带 1 字节计数器
            byte[] data = counter == 0 ? [] : [(byte)counter];
            bus.Send(cobId, data);

            context.Log(counter == 0
                ? $"SYNC 发送 0x{cobId:X3}"
                : $"SYNC 发送 0x{cobId:X3}，计数器 {counter}");
            return CanopenExecutorHelper.Result(TestStatus.Passed, counter);
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}

/// <summary>CANopen_EmcyReceive 执行器：监听 0x80 + NodeId 的 EMCY 报文并解析</summary>
public sealed class CanopenEmcyReceiveExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var setting = (CanopenEmcyReceiveSetting)new CanopenEmcyReceivePlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.TimeoutMs <= 0)
                return CanopenExecutorHelper.Result(TestStatus.Error, error: "等待超时必须大于 0");

            var (bus, error) = await CanopenExecutorHelper.GetBusAsync(setting, context);
            if (bus == null) return CanopenExecutorHelper.Result(TestStatus.Error, error: error);

            byte node = await CanopenExecutorHelper.EvalNodeIdAsync(setting.NodeId, context, allowBroadcast: false);

            ushort? expected = null;
            if (!string.IsNullOrWhiteSpace(setting.ExpectedErrorCode))
            {
                uint code = await CanopenExecutorHelper.EvalNumberAsync(setting.ExpectedErrorCode, context, "ExpectedErrorCode");
                if (code > 0xFFFF) return CanopenExecutorHelper.Result(TestStatus.Error, error: $"期望错误码超出范围 0x0000~0xFFFF: 0x{code:X}");
                expected = (ushort)code;
            }

            uint cobId = 0x80u + node;
            var deadline = DateTime.UtcNow.AddMilliseconds(setting.TimeoutMs);
            ushort? lastCode = null;

            // 在总超时内循环接收；错误码不符时继续等待，并记录最后一次错误码用于超时提示
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;

                var msg = await bus.ReceiveAsync(cobId, remaining, cancellationToken);
                if (msg == null) break;
                if (msg.Data.Length < 3) continue;

                // CiA 301 EMCY：字节 0~1 错误码（小端），字节 2 错误寄存器，字节 3~7 厂商数据
                ushort errorCode = (ushort)(msg.Data[0] | (msg.Data[1] << 8));
                byte errorRegister = msg.Data[2];
                byte[] manufacturer = msg.Data.Length > 3 ? msg.Data[3..] : [];
                var codeText = $"0x{errorCode:X4}";
                var regText = $"0x{errorRegister:X2}";
                var mfrText = CanopenExecutorHelper.ToHex(manufacturer);
                lastCode = errorCode;

                if (expected != null && expected != errorCode)
                {
                    context.Log($"收到节点{node} EMCY 错误码 {codeText}（错误寄存器 {regText}），与期望 0x{expected:X4} 不符，继续等待");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(setting.ErrorCodeVariable))
                    context.SetVariable(setting.ErrorCodeVariable, codeText);
                if (!string.IsNullOrWhiteSpace(setting.ErrorRegisterVariable))
                    context.SetVariable(setting.ErrorRegisterVariable, regText);
                if (!string.IsNullOrWhiteSpace(setting.ManufacturerDataVariable))
                    context.SetVariable(setting.ManufacturerDataVariable, mfrText);

                var resetHint = errorCode == 0 ? "（错误复位）" : string.Empty;
                context.Log($"收到节点{node} EMCY 错误码 {codeText}{resetHint}，错误寄存器 {regText}，厂商数据 [{mfrText}]");
                return CanopenExecutorHelper.Result(TestStatus.Passed, codeText);
            }

            var target = expected == null ? "EMCY" : $"错误码 0x{expected:X4} 的 EMCY";
            var detail = lastCode == null ? "未收到 EMCY" : $"最后收到的错误码为 0x{lastCode:X4}";
            return CanopenExecutorHelper.Result(TestStatus.Error,
                error: $"等待节点{node} {target}超时（{setting.TimeoutMs} ms），{detail}");
        }
        catch (Exception ex)
        {
            return CanopenExecutorHelper.FromException(ex, cancellationToken);
        }
    }
}
