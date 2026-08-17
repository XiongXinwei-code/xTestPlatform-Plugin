using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 按 XPath 从 XML 文本提取字段并写入变量
/// </summary>
public sealed class HttpXmlExtractExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpXmlExtractPlugin().CreateSerializer();
        var setting = (HttpXmlExtractSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (setting.Items.Count == 0)
                return Error("提取映射列表为空");

            var xml = await Evaluator.EvalStringAsync(setting.SourceXml, context);
            if (string.IsNullOrWhiteSpace(xml))
                return Error("待解析的 XML 文本为空");

            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
                if (setting.IgnoreNamespaces)
                    StripNamespaces(document.Root!);
            }
            catch (Exception ex)
            {
                return Error($"XML 解析失败: {ex.Message}");
            }

            var navigator = document.CreateNavigator();
            var missing = new List<string>();

            foreach (var item in setting.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(item.TargetVariable))
                    return Error("提取映射中存在未配置目标变量的行");
                if (string.IsNullOrWhiteSpace(item.Path))
                    return Error($"变量 {item.TargetVariable} 的 XPath 未配置");

                string? value;
                try
                {
                    value = navigator.SelectSingleNode(item.Path)?.Value;
                }
                catch (XPathException ex)
                {
                    return Error($"XPath 语法错误 [{item.Path}]: {ex.Message}");
                }

                if (value == null)
                {
                    missing.Add(item.Path);
                    value = item.DefaultValue;
                }

                context.SetVariable(item.TargetVariable, value);
                context.LogAction?.Invoke($"XML 提取: {item.Path} => {item.TargetVariable} = {value}");
            }

            if (missing.Count > 0 && setting.FailOnMissingPath)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Value = $"{setting.Items.Count - missing.Count}/{setting.Items.Count}",
                        Error = new ErrorInfo { Message = $"以下 XPath 未命中: {string.Join(", ", missing)}" }
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
            return Error($"XML 提取失败: {ex.Message}");
        }
    }

    /// <summary>递归剥离元素与属性上的命名空间，使 XPath 可以直接书写元素名</summary>
    private static void StripNamespaces(XElement element)
    {
        foreach (var descendant in element.DescendantsAndSelf())
        {
            descendant.Name = XName.Get(descendant.Name.LocalName);

            var attributes = descendant.Attributes()
                .Where(a => !a.IsNamespaceDeclaration)
                .Select(a => new XAttribute(XName.Get(a.Name.LocalName), a.Value))
                .ToList();

            descendant.ReplaceAttributes(attributes);
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
