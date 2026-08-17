using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 按路径从 JSON 文本提取字段并写入变量
/// </summary>
public sealed class HttpJsonExtractExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpJsonExtractPlugin().CreateSerializer();
        var setting = (HttpJsonExtractSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (setting.Items.Count == 0)
                return Error("提取映射列表为空");

            var json = await Evaluator.EvalStringAsync(setting.SourceJson, context);
            if (string.IsNullOrWhiteSpace(json))
                return Error("待解析的 JSON 文本为空");

            var missing = new List<string>();

            foreach (var item in setting.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(item.TargetVariable))
                    return Error("提取映射中存在未配置目标变量的行");

                string? value;
                try
                {
                    value = JsonPathHelper.Evaluate(json, item.Path);
                }
                catch (Exception ex)
                {
                    return Error($"JSON 解析失败: {ex.Message}");
                }

                if (value == null)
                {
                    missing.Add(item.Path);
                    value = item.DefaultValue;
                }

                context.SetVariable(item.TargetVariable, value);
                context.LogAction?.Invoke($"JSON 提取: {item.Path} => {item.TargetVariable} = {value}");
            }

            if (missing.Count > 0 && setting.FailOnMissingPath)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Value = $"{setting.Items.Count - missing.Count}/{setting.Items.Count}",
                        Error = new ErrorInfo { Message = $"以下 JSON 路径未命中: {string.Join(", ", missing)}" }
                    }
                };
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{setting.Items.Count - missing.Count}/{setting.Items.Count}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return Error($"JSON 提取失败: {ex.Message}");
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
}
