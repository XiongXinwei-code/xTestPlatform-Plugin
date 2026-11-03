using PluginCommon;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

internal sealed class DbcWriteExecutor : DbcExecutorBase<DbcWriteSetting>
{
    protected override IStepPlugin Plugin => new DbcWritePlugin();
    protected override async Task<ExecutionResult> RunAsync(DbcWriteSetting setting, DbcExecution run)
    {
        var (database, _) = await run.LoadAsync(setting).ConfigureAwait(false);
        var selected = await run.SelectMessagesAsync(database, setting).ConfigureAwait(false);
        var owner = await run.ConnectionAsync(setting).ConfigureAwait(false);
        var frames = new List<ProtectedCanFrame>();
        var address = run.Context.CurrentStep!.Step.StepSetting.StepAddress;
        foreach (var item in selected)
        {
            var bytes = await DbcExecution.EncodeAsync(item.Message, item.Inputs, item.Protection,
                run.Context, run.Token, run.Check).ConfigureAwait(false);
            frames.Add(new(DbcExecution.Frame(item.Message, item.Format, bytes), item.Protection,
                item.Row == 0 ? address : $"{address}/dbc/{item.Row}", Math.Max(1, item.Row)));
        }
        var submitted = setting.UseMessageList
            ? await run.Guard.RunAsync(() => owner.WriteProtectedBatchAsync(frames.ToArray(), run.Remaining(), run.Token)).ConfigureAwait(false)
            : new[] { await run.Guard.RunAsync(() => owner.WriteProtectedAsync(frames[0].Frame,
                frames[0].Protection, address, run.Remaining(), run.Token)).ConfigureAwait(false) };
        run.Check();
        string summary = string.Join("; ", submitted.Select(f => $"0x{f.Id:X}: {Convert.ToHexString(f.Data)}"));
        run.Guard.Log($"DBC 驱动提交完成（非设备确认），{submitted.Length} 帧：{summary}");
        return CanExecutionResults.Passed(summary);
    }
}
