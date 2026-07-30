using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Executors;

public sealed class CanCloseExecutor : IStepExecutor
{
    public Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanClosePlugin().CreateSerializer();
        var setting = (CanCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var key = CanHelper.GetAdapterKey(setting.ConnectionName);

            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is ICanAdapter adapter)
            {
                adapter.Close();
                adapter.Dispose();
                context.CurrentStep.RuntimeData.Remove(key);
                context.LogAction?.Invoke($"CAN 通道已关闭: {setting.ConnectionName}");
            }
            else
            {
                context.LogAction?.Invoke($"CAN 通道未找到: {setting.ConnectionName}");
            }

            return Task.FromResult(new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"关闭 CAN 通道失败: {ex.Message}" }
                }
            });
        }
    }
}
