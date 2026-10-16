using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsSecurityAccessExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsSecurityAccessPlugin().CreateSerializer();
        var setting = (UdsSecurityAccessSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            // Step 1: Request Seed (奇数子功能)
            byte seedSubFunc = (byte)(setting.SecurityLevel * 2 - 1);
            var seedRequest = new byte[] { 0x27, seedSubFunc };
            var seedResponse = await client.RequestAsync(seedRequest, cancellationToken);

            if (!seedResponse.IsPositive)
            {
                return UdsExecutorHelper.Failure($"UDS 请求种子，访问级别 {setting.SecurityLevel}",
                    seedResponse, seedResponse.GetFailureValue());
            }

            // 提取 Seed (响应: 67 [subFunc] [seed bytes...])
            byte[] seed = seedResponse.Data.Length > 1 ? seedResponse.Data[1..] : [];
            context.Log($"UDS 已接收种子，共 {seed.Length} 字节（内容不记录）");

            // 将 Seed 存入用户配置的变量名供表达式使用
            if (!string.IsNullOrWhiteSpace(setting.SeedVariable))
                context.SetVariable(setting.SeedVariable, seed);

            // Step 2: 通过表达式计算 Key
            byte[] key;
            try
            {
                var keyResult = await Evaluator.EvaluateAsync<object>(setting.KeyExpression, context);
                key = keyResult switch
                {
                    byte[] bytes => bytes,
                    _ => throw new InvalidOperationException($"Key 表达式返回类型不正确，期望 byte[]，实际为 {keyResult?.GetType().Name ?? "null"}")
                };
            }
            catch (Exception ex)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = ErrorInfo.FromException(ex, $"Key 计算失败: {ex.Message}")
                    }
                };
            }

            context.Log($"UDS 密钥计算完成，共 {key.Length} 字节（内容不记录）");

            // Step 3: Send Key (偶数子功能)
            byte keySubFunc = (byte)(setting.SecurityLevel * 2);
            var keyRequest = new byte[2 + key.Length];
            keyRequest[0] = 0x27;
            keyRequest[1] = keySubFunc;
            Buffer.BlockCopy(key, 0, keyRequest, 2, key.Length);

            var keyResponse = await client.RequestAsync(keyRequest, cancellationToken);

            bool unlocked = keyResponse.IsPositive;

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, unlocked);

            if (unlocked)
            {
                context.Log($"UDS SecurityAccess: 解锁成功 (Level {setting.SecurityLevel})");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = "Unlocked" } };
            }
            else
            {
                return UdsExecutorHelper.Failure($"UDS 提交密钥，访问级别 {setting.SecurityLevel}",
                    keyResponse, keyResponse.GetFailureValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } };
        }
    }
}
