using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var step = context.CurrentStep!.Step;
            var serializer = new CanWritePlugin().CreateSerializer();
            var setting = (CanWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = CanHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ICanAdapter>(key, out var adapter))
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"CAN 连接未找到: {connName}" }
                    }
                };
            }

            // 求值 CAN ID
            guard.Watch(key, adapter);
            var canIdStr = await Evaluator.EvalStringAsync(setting.CanId, context);
            uint canId = CanHelper.ParseCanId(canIdStr);

            // 求值数据
            var dataStr = await Evaluator.EvalStringAsync(setting.Data, context);
            byte[] data = CanHelper.ParseHexData(dataStr);

            var message = new CanMessage
            {
                Id = canId,
                FrameType = setting.FrameType,
                Data = data,
                IsFd = setting.UseFdFrame
            };

            if (adapter is not CAN.Runtime.CanConnectionResource connection)
                throw new InvalidOperationException("此旧 CAN 连接不提供可确认退出的发送生命周期，请先关闭并重新执行 CAN_Open");
            await guard.RunAsync(() => connection.WriteAsync(message, setting.TimeoutMs, cancellationToken));

            guard.Log($"CAN 驱动提交完成（非上总线确认）: ID=0x{canId:X}, Data=[{BitConverter.ToString(data).Replace("-", " ")}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"0x{canId:X}: {BitConverter.ToString(data).Replace("-", " ")}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Aborted }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"CAN 发送失败: {ex.Message}")
                }
            };
        }
    }
}
