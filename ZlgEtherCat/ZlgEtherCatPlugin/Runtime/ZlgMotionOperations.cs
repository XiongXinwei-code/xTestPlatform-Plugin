using System.Globalization;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Motion;

namespace ZlgEtherCat.Runtime;

/// <summary>固定运动步骤分发；调用方负责同连接字典及绑定/解绑生命周期的串行化。</summary>
public static class ZlgMotionOperations
{
    public static bool Handles(string operation) => operation is "BindAxis" or "UnbindAxis" or "ReadAxisState"
        or "ReadAxisMode" or "ReadAxisFeedback" or "ReadAxisDiagnostics" or "SetAxisMode"
        or "ResetAxisFault" or "EnableAxis" or "DisableAxis" or "HomeAxis" or "MovePosition"
        or "RunVelocity" or "RunTorque" or "StopMotion" or "WriteCspTarget" or "WriteCsvTarget" or "WriteCstTarget";

    public static async Task<object?> ExecuteAsync(string operation, ZlgRequest request, IZlgMotionIo io,
        IDictionary<string, ZlgMotionAxis> axes, CancellationToken ct)
    {
        if (!Handles(operation)) throw new ArgumentException($"未知运动操作：{operation}。", nameof(operation));
        ct.ThrowIfCancellationRequested();
        var name = request.Get<string>("AxisName");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("轴名称不能为空。");
        var timeout = request.TimeoutMs;
        if (operation == "BindAxis")
        {
            if (axes.ContainsKey(name)) throw new InvalidOperationException($"轴 {name} 已绑定，禁止覆盖现有会话。");
            var configuration = ParseConfiguration(request, name);
            var created = await ZlgMotionAxis.BindAsync(io, configuration, timeout, ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                axes.Add(name, created);
            }
            catch
            {
                // 绑定本身不写设备，此处解绑只释放预留；父连接及原生调用仍由运行时管理。
                await created.UnbindAsync(timeout, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            return new MotionCommandResult(name, operation, "轴绑定已建立；未使能、未切换模式、未提交运动。");
        }
        if (!axes.TryGetValue(name, out var axis)) throw new InvalidOperationException($"轴 {name} 未绑定。");
        switch (operation)
        {
            case "ReadAxisState": return await axis.ReadStateAsync(timeout, ct).ConfigureAwait(false);
            case "ReadAxisMode": return await axis.ReadModeAsync(timeout, ct).ConfigureAwait(false);
            case "ReadAxisFeedback": return await axis.ReadFeedbackAsync(timeout, ct).ConfigureAwait(false);
            case "ReadAxisDiagnostics": return await axis.ReadDiagnosticsAsync(timeout, ct).ConfigureAwait(false);
            case "UnbindAxis":
                await axis.UnbindAsync(timeout, ct).ConfigureAwait(false);
                axes.Remove(name);
                break;
            case "SetAxisMode": await axis.SetModeAsync(request.Get<MotionMode>("Mode"), timeout, ct).ConfigureAwait(false); break;
            case "ResetAxisFault": await axis.ResetFaultAsync(timeout, ct).ConfigureAwait(false); break;
            case "EnableAxis": await axis.EnableAsync(timeout, ct).ConfigureAwait(false); break;
            case "DisableAxis": await axis.DisableAsync(timeout, ct).ConfigureAwait(false); break;
            case "HomeAxis":
                await axis.HomeAsync(request.Get<sbyte>("Method"), request.Get<uint>("SearchVelocity"), request.Get<uint>("ZeroVelocity"), request.Get<uint>("Acceleration"), timeout, ct).ConfigureAwait(false);
                break;
            case "MovePosition":
                await axis.MovePositionAsync(request.Get<int>("Position"), request.Get<bool>("Relative"), request.Get<uint>("ProfileVelocity"), request.Get<uint>("Acceleration"), request.Get<uint>("Deceleration"), timeout, ct).ConfigureAwait(false);
                break;
            case "RunVelocity":
                await axis.RunVelocityAsync(request.Get<int>("Velocity"), request.Get<uint>("Acceleration"), request.Get<uint>("Deceleration"), timeout, ct).ConfigureAwait(false);
                break;
            case "RunTorque": await axis.RunTorqueAsync(request.Get<short>("Torque"), timeout, ct).ConfigureAwait(false); break;
            case "StopMotion": await axis.StopMotionAsync(request.Get<bool>("QuickStop"), request.Get<int>("VelocityTolerance"), timeout, ct).ConfigureAwait(false); break;
            case "WriteCspTarget": await axis.WriteCyclicTargetAsync(MotionMode.CSP, request.Get<int>("Position"), timeout, ct).ConfigureAwait(false); break;
            case "WriteCsvTarget": await axis.WriteCyclicTargetAsync(MotionMode.CSV, request.Get<int>("Velocity"), timeout, ct).ConfigureAwait(false); break;
            case "WriteCstTarget": await axis.WriteCyclicTargetAsync(MotionMode.CST, request.Get<short>("Torque"), timeout, ct).ConfigureAwait(false); break;
        }
        var detail = operation switch
        {
            "WriteCspTarget" or "WriteCsvTarget" or "WriteCstTarget" => "目标已提交通道输出缓冲；不证明已按期发送、驱动应用或运动完成。",
            "RunVelocity" or "RunTorque" => "目标已提交；持续运动须显式停止。",
            "StopMotion" => "已确认工艺状态及实际速度反馈；不构成功能安全停轴证明。",
            "MovePosition" => "已确认新目标握手及驱动目标到达状态。",
            "HomeAxis" => "已确认本次回零启动后的完成状态。",
            _ => "操作已完成，设备状态以反馈为准。"
        };
        return new MotionCommandResult(name, operation, detail);
    }

    private static MotionAxisConfiguration ParseConfiguration(ZlgRequest request, string name)
    {
        var configuredModes = request.Get<string>("AllowedModes");
        var items = configuredModes.Split(',', StringSplitOptions.TrimEntries);
        var modes = new List<MotionMode>();
        foreach (var item in items)
        {
            if (!sbyte.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
                || !Enum.IsDefined((MotionMode)numeric))
                throw new ArgumentException("允许模式须为 1、3、4、6、8、9、10 的逗号分隔整数，不接受空项。");
            modes.Add((MotionMode)numeric);
        }
        return new MotionAxisConfiguration
        {
            AxisName = name,
            SlaveIndex = request.Get<ushort>("SlaveIndex"),
            AllowedModes = modes.ToArray(),
            MaxFeedbackAgeMs = request.GetOrDefault("MaxFeedbackAgeMs", 100),
            PollIntervalMs = request.GetOrDefault("PollIntervalMs", 10),
            InputByteOffset = request.GetOrDefault("InputByteOffset", 0),
            InputByteLength = request.GetOrDefault("InputByteLength", 0),
            OutputByteOffset = request.GetOrDefault("OutputByteOffset", 0),
            OutputByteLength = request.GetOrDefault("OutputByteLength", 0),
            StatusWordOffset = request.GetOrDefault("StatusWordOffset", -1),
            ActualPositionOffset = request.GetOrDefault("ActualPositionOffset", -1),
            ActualVelocityOffset = request.GetOrDefault("ActualVelocityOffset", -1),
            ActualTorqueOffset = request.GetOrDefault("ActualTorqueOffset", -1),
            ModeDisplayOffset = request.GetOrDefault("ModeDisplayOffset", -1),
            ControlWordOffset = request.GetOrDefault("ControlWordOffset", -1),
            TargetPositionOffset = request.GetOrDefault("TargetPositionOffset", -1),
            TargetVelocityOffset = request.GetOrDefault("TargetVelocityOffset", -1),
            TargetTorqueOffset = request.GetOrDefault("TargetTorqueOffset", -1),
            ModeCommandOffset = request.GetOrDefault("ModeCommandOffset", -1)
        };
    }
}

public sealed record MotionCommandResult(string AxisName, string Operation, string Detail);
