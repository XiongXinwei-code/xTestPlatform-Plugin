using System.Text.Json.Nodes;
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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Http");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new HttpJsonExtractPlugin().CreateSerializer();
            var setting = (HttpJsonExtractSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            if (setting.Items.Count == 0)
                return Error("提取映射列表为空");

            var json = await Evaluator.EvalStringAsync(setting.SourceJson, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(json))
                return Error("待解析的 JSON 文本为空");

            var missing = new List<string>();
            var root = JsonNode.Parse(json);
            var resolved = new List<(string Target, string Path, string Value)>();

            foreach (var item in setting.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckExecution();

                if (string.IsNullOrWhiteSpace(item.TargetVariable))
                    return Error("提取映射中存在未配置目标变量的行");

                string? value;
                try
                {
                    value = JsonPathHelper.EvaluateNode(root, item.Path);
                }
                catch (Exception ex)
                {
                    return Error($"JSON 解析失败: {ex.Message}", ex);
                }

                if (value == null)
                {
                    missing.Add(item.Path);
                    value = item.DefaultValue;
                }

                resolved.Add((item.TargetVariable, item.Path, value));
            }

            if (missing.Count > 0 && setting.FailOnMissingPath)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Value = $"{setting.Items.Count - missing.Count}/{setting.Items.Count}",
                        Error = new ErrorInfo { Message = $"以下 JSON 路径未命中: {string.Join(", ", missing)}" }
                    }
                };
            }

            foreach (var value in resolved)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckExecution();
                context.SetVariable(value.Target, value.Value);
                context.Log($"JSON 提取: {value.Path} => {value.Target} = {value.Value}");
            }
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
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
            return Error($"JSON 提取失败: {ex.Message}", ex);
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null)
        => ex is null ? ExecutionResults.Error(message) : ExecutionResults.FromException(ex, message);
}
