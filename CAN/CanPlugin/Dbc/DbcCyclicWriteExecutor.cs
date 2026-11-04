using CAN.Helpers;
using CAN.Runtime;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

internal sealed class DbcCyclicWriteExecutor : DbcExecutorBase<DbcCyclicWriteSetting>
{
    protected override IStepPlugin Plugin => new DbcCyclicWritePlugin();
    protected override async Task<ExecutionResult> RunAsync(DbcCyclicWriteSetting setting, DbcExecution run)
    {
        using var preparation = CanCyclicPreparationGate.Enter(run.Context.Resources,
            "dbc-cycle/" + run.Context.CurrentStep!.Step.StepSetting.StepAddress, setting.TimeoutMs, run.Token);
        using var owned = PluginCommon.ExecutionScopedExecutor.OwnOperation(preparation.Dispose);
        void CheckPreparing() { preparation.Check(); run.Check(); }
        var (database, _) = await run.LoadAsync(setting).ConfigureAwait(false);
        var selected = await run.SelectMessagesAsync(database, setting).ConfigureAwait(false);
        var owner = await run.ConnectionAsync(setting).ConfigureAwait(false);
        string taskName = await run.TextAsync(setting.TaskName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(taskName)) throw new ArgumentException("DBC 周期发送任务名称不能为空");
        owner.ValidateCyclicProtocol(selected.Any(item => item.Identity.Fd));
        var definitions = new List<CanCyclicDefinition>();
        var guard = run.Guard; var context = run.Context;
        foreach (var item in selected)
        {
            var bytes = await DbcExecution.EncodeAsync(item.Message, item.Inputs, item.Protection, context, run.Token, CheckPreparing, preparation.Evaluations.Track).ConfigureAwait(false);
            var frame = DbcExecution.Frame(item.Message, item.Format, bytes);
            CanHelper.ValidateTransmitFrame(frame); CanPayloadProtection.Validate(item.Protection, bytes.Length);
            var evaluations = new CanCyclicEvaluationScope(setting.TimeoutMs);
            definitions.Add(new(frame.Id, frame.FrameType, frame.IsFd, new(true, item.CycleTimeMs), 0, setting.TimeoutMs,
                item.Protection, async (ct, check) => DbcExecution.Frame(item.Message, item.Format,
                    await DbcExecution.EncodeAsync(item.Message, item.Inputs, item.Protection, context, ct, check, evaluations.Track).ConfigureAwait(false)),
                (_, _) => Task.FromResult(new CanCyclicControl(true, item.CycleTimeMs)), guard.CheckLifetime, evaluations.DrainAsync));
        }
        owner.UpdateCyclicPool(taskName, "DBC", definitions.ToArray(), run.Check);
        run.Check();
        return CanExecutionResults.Passed($"DBC 周期发送池已更新：{taskName}，{selected.Length} 个 ID；" +
            string.Join("，", selected.Select(item => $"0x{item.Message.Id:X}={item.CycleTimeMs} ms")) +
            "；重复 ID 下一节拍替换，保留节拍与 RC；使用 CAN_Cyclic_SendStop 停止当前归属 ID");
    }
}
