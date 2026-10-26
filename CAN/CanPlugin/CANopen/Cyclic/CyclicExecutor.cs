using System.Text.Json;
using CAN.Helpers;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.CANopen.Cyclic;

public sealed class CyclicExecutor(CyclicPluginBase plugin) : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var s = (CyclicSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var connectionName = await CanopenExecutorHelper.EvalTextAsync(s.ConnectionName, context);
            var name = await CanopenExecutorHelper.EvalTextAsync(s.ServiceName, context);
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(connectionName), out var connection))
            {
                guard.Check();
                throw new InvalidOperationException($"CAN 连接 {connectionName} 不存在或不是新版连接，请先执行 CAN_Open");
            }
            if (!string.IsNullOrWhiteSpace(s.ResultVariable) &&
                (!context.HasVariable(s.ResultVariable) || context.GetVariable(s.ResultVariable) is not (null or string)))
                throw new ArgumentException("结果必须是已声明的 string 变量");
            guard.Watch(CanHelper.GetAdapterKey(connectionName), connection);
            cancellationToken.ThrowIfCancellationRequested();
            if (plugin.Operation == CyclicOperation.ServiceStart)
            {
                var routes = new List<FeedbackRoute>();
                foreach (var route in s.FeedbackRoutes)
                    routes.Add(new(route.Name, await CanopenExecutorHelper.EvalNumberAsync(route.CobId, context, "COB-ID"), route.Dlc, route.MaxAgeMs));
                cancellationToken.ThrowIfCancellationRequested();
                var monitorSync = s.EnableMonitoringSync ? new OrderedFrame(
                    await CanopenExecutorHelper.EvalNumberAsync(s.SyncCobId, context, "SYNC ID"),
                    CanopenExecutorHelper.ParseHex(await CanopenExecutorHelper.EvalTextAsync(s.SyncData, context))) : null;
                cancellationToken.ThrowIfCancellationRequested();
                guard.Check();
                var created = connection.StartService(name, routes.ToArray());
                if (monitorSync != null)
                {
                    try { await Task.Run(() => { guard.Check(); created.StartMonitoring(monitorSync, new(s.MonitoringPeriodMs, s.CycleTimeoutMs, s.MaxLatenessMs), cancellationToken); }, CancellationToken.None); }
                    catch { await connection.StopServiceAsync(name, created, s.StopTimeoutMs, CancellationToken.None); throw; }
                }
                try { guard.Check(); }
                catch { await connection.StopServiceAsync(name, created, s.StopTimeoutMs, CancellationToken.None); throw; }
            }
            else
            {
                var service = connection.GetService(name);
                switch (plugin.Operation)
                {
                    case CyclicOperation.CycleStart:
                        var nodes = (await CanopenExecutorHelper.EvalTextAsync(s.NodeIds, context))
                            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(byte.Parse).ToArray();
                        var targets = s.UseFiniteTrajectory ? [] : await EvaluateTargets(s, context);
                        var sync = new OrderedFrame(await CanopenExecutorHelper.EvalNumberAsync(s.SyncCobId, context, "SYNC ID"),
                            CanopenExecutorHelper.ParseHex(await CanopenExecutorHelper.EvalTextAsync(s.SyncData, context)));
                        cancellationToken.ThrowIfCancellationRequested();
                        var stopConfig = CyclicInputParser.Stop(await CanopenExecutorHelper.EvalTextAsync(s.StopCommandsJson, context), s.StopTimeoutMs);
                        var table = s.UseFiniteTrajectory ? CyclicInputParser.Trajectory(
                            await CanopenExecutorHelper.EvalTextAsync(s.TrajectoryJson, context), s.TrajectoryDurationMs) : null;
                        var options = new CycleOptions(s.PeriodMs, s.CycleTimeoutMs, s.MaxLatenessMs,
                            s.RequireOnBusConfirmation, s.InputMaxAgeMs, s.SuperviseFeedback, s.FeedbackStartupGraceMs);
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Run(() =>
                        {
                            guard.Check();
                            if (table == null) service.StartConfiguredTargets(nodes, targets, sync, options, stopConfig, cancellationToken);
                            else service.StartConfiguredTrajectory(nodes, table, sync, options, stopConfig, cancellationToken);
                        }, CancellationToken.None);
                        try { guard.Check(); }
                        catch { await service.StopTargetsAsync(s.StopTimeoutMs, CancellationToken.None); throw; }
                        break;
                    case CyclicOperation.CycleInputsUpdate:
                        var updated = await EvaluateTargets(s, context);
                        cancellationToken.ThrowIfCancellationRequested();
                        guard.Check();
                        service.PublishTargets(updated);
                        break;
                    case CyclicOperation.CycleStop:
                        await service.StopTargetsAsync(s.StopTimeoutMs, cancellationToken);
                        var stoppedCycle = service.Snapshot();
                        if (stoppedCycle.Cycle.Error != null || stoppedCycle.Receive.BackgroundError != null)
                            return CanopenExecutorHelper.Result(TestStatus.Error, JsonSerializer.Serialize(stoppedCycle), "发送已退出，但后台通信曾失败");
                        break;
                    case CyclicOperation.ServiceStop:
                        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(s.StopTimeoutMs);
                        try { await connection.StopServiceAsync(name, service, s.StopTimeoutMs, cancellationToken); }
                        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                            ReferenceEquals(ex, service.CloseHistory) && service.Snapshot().ServiceShutdownConfirmed)
                        {
                            // Only a fully closed service can classify this as history.
                            // PartialCycle, pending TX, callbacks and native work still fail.
                        }
                        var stopped = service.Snapshot();
                        if (!stopped.ServiceShutdownConfirmed)
                            throw new InvalidOperationException("服务关闭未确认；不能把宿主退出等同于订阅、发送与控制占用均已释放");
                        guard.Check();
                        var stopJson = JsonSerializer.Serialize(stopped);
                        if (!string.IsNullOrWhiteSpace(s.ResultVariable)) guard.SetVariable(s.ResultVariable, stopJson);
                        if (stopped.BackgroundError != null)
                            context.Log(LogLevel.Warn, "CANopen 服务已完成关闭，历史故障仍保留：" + stopped.ErrorSummary);
                        guard.Check();
                        return CanopenExecutorHelper.Result(TestStatus.Completed, stopJson);
                    case CyclicOperation.SnapshotRead:
                        var snapshot = service.Snapshot();
                        var json = JsonSerializer.Serialize(snapshot);
                        guard.Check();
                        if (!string.IsNullOrWhiteSpace(s.ResultVariable)) guard.SetVariable(s.ResultVariable, json);
                        if (snapshot.Receive.BackgroundError != null || snapshot.Cycle.Error != null || (s.RequireAllValid && !snapshot.Receive.AllValid))
                            return CanopenExecutorHelper.Result(TestStatus.Error, json,
                                "CANopen 快照无效或后台任务故障；" + (snapshot.ErrorSummary ?? "部分反馈缺失、过期或时间不可信；详情见完整快照"));
                        return CanopenExecutorHelper.Result(TestStatus.Completed, json);
                }
            }
            guard.Check();
            return CanopenExecutorHelper.Result(TestStatus.Completed, $"{plugin.Operation}: {connectionName}/{name}");
        }
        catch (Exception ex) { return CanopenExecutorHelper.FromException(ex, cancellationToken, plugin.DisplayName); }
    }

    private static async Task<OrderedFrame[]> EvaluateTargets(CyclicSetting setting, IExecutionContext context)
    {
        var frames = new List<OrderedFrame>();
        foreach (var target in setting.Targets)
        {
            var id = await CanopenExecutorHelper.EvalNumberAsync(target.CobId, context, "RPDO ID");
            var data = CanopenExecutorHelper.ParseHex(await CanopenExecutorHelper.EvalTextAsync(target.Data, context));
            if (target.Dlc is < 0 or > 8 || data.Length != target.Dlc) throw new ArgumentException($"RPDO 0x{id:X} 数据长度 {data.Length} 与 DLC {target.Dlc} 不符");
            frames.Add(new(id, data));
        }
        return frames.ToArray();
    }
}
