using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 创建命名 HTTP 客户端并注册到运行期资源表
/// </summary>
public sealed class HttpClientCreateExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<Func<System.Net.Http.HttpClient>, int, CancellationToken, PluginCommon.OwnerStopSignal?, Task<System.Net.Http.HttpClient>> _build;
    public HttpClientCreateExecutor() : this(HttpHelper.BuildClientAsync) { }
    internal HttpClientCreateExecutor(Func<Func<System.Net.Http.HttpClient>, int, CancellationToken, PluginCommon.OwnerStopSignal?, Task<System.Net.Http.HttpClient>> build) => _build = build;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

        SemaphoreSlim? registrationGate = null;
        bool ownsGate = false;
        System.Net.Http.HttpClient? stagedClient = null;
        HttpClientResource? stagedResource = null;
        var budget = System.Diagnostics.Stopwatch.StartNew();
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
            var serializer = new HttpClientCreatePlugin().CreateSerializer();
            var setting = (HttpClientCreateSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "HTTP 超时不能为负数；0 表示显式无限等待");
            int Remaining()
            {
                CheckExecution();
                if (setting.TimeoutMs == 0) return Timeout.Infinite;
                int remaining = setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
                if (remaining <= 0) throw new TimeoutException($"HTTP 客户端建立总超时 {setting.TimeoutMs} ms 已到");
                return remaining;
            }
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            var baseUrl = await Evaluator.EvalStringAsync(setting.BaseUrl, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(baseUrl))
                return Error("服务基地址未配置");

            if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute, out _))
                return Error("服务基地址不是合法的 HTTP(S) 绝对 URL");

            var key = HttpHelper.GetClientKey(clientName);
            registrationGate = HttpHelper.GetRegistryGate(execution, clientName);
            if (!await execution.StopSignal.EnterAsync(registrationGate, Remaining(), cancellationToken, "HTTP 客户端建立排队"))
                throw new TimeoutException($"等待 HTTP 客户端建立生命周期操作超过 {setting.TimeoutMs} ms");
            ownsGate = true;
            _ = Remaining();
            if (context.Resources.Contains(key))
            {
                if (!setting.ReplaceIfExists)
                    return Error($"HTTP 客户端已存在: {clientName}");

            }

            var userName = setting.AuthMode == AuthMode.Basic ? await Evaluator.EvalStringAsync(setting.UserName, context) : "";
            CheckExecution();
            var password = setting.AuthMode == AuthMode.Basic ? await Evaluator.EvalStringAsync(setting.Password, context) : "";
            CheckExecution();
            var token = setting.AuthMode == AuthMode.BearerToken ? await Evaluator.EvalStringAsync(setting.Token, context) : "";
            CheckExecution();
            var certPath = setting.AuthMode == AuthMode.ClientCertificate ? await Evaluator.EvalStringAsync(setting.ClientCertPath, context) : "";
            CheckExecution();
            var certPassword = setting.AuthMode == AuthMode.ClientCertificate ? await Evaluator.EvalStringAsync(setting.ClientCertPassword, context) : "";
            CheckExecution();

            var headers = new List<(string Name, string Value)>();
            foreach (var header in setting.DefaultHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Name)) continue;
                var value = await Evaluator.EvalStringAsync(header.Value, context);
                CheckExecution();
                headers.Add((header.Name, value));
            }
            int creationRemaining = setting.TimeoutMs == 0 ? 0 : setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
            if (setting.TimeoutMs != 0 && creationRemaining <= 0) throw new TimeoutException($"HTTP 客户端建立总超时 {setting.TimeoutMs} ms 已到");
            var client = stagedClient = await _build(() =>
            {
                CheckExecution();
                var built = HttpHelper.BuildClient(baseUrl, setting.TimeoutMs, setting.AuthMode, userName, password,
                    token, certPath, certPassword, setting.IgnoreServerCertificateErrors, setting.ForceConnectionClose,
                    headers.Any(h => string.Equals(h.Name, "Authorization", StringComparison.OrdinalIgnoreCase)));
                try
                {
                    foreach (var header in headers)
                        HttpHelper.SetRequestHeader(built.DefaultRequestHeaders, header.Name, header.Value, replace: false);
                    return built;
                }
                catch { built.Dispose(); throw; }
            }, creationRemaining, cancellationToken, execution.StopSignal);

            var resource = stagedResource = new HttpClientResource
            {
                Client = client,
                BaseUrl = baseUrl,
                AuthMode = setting.AuthMode,
                CheckExecutionScope = execution.ThrowIfExpired
            };
            stagedClient = null; // The staged resource now owns asynchronous cleanup.
            PluginCommon.ExecutionScopedExecutor.Own(resource.Dispose);

            cancellationToken.ThrowIfCancellationRequested(); // Never publish a client after sequence cancellation.
            CheckExecution();
            HttpClientResource? previous = null;
            if (setting.ReplaceIfExists && context.Resources.TryGet<HttpClientResource>(key, out var foundPrevious))
            {
                previous = foundPrevious;
                int remaining = Remaining(); // lookup可能跨越SDK回收，触碰返回owner前复查原执行代次。
                if (remaining == Timeout.Infinite) remaining = int.MaxValue;
                await execution.StopSignal.WaitAsync(foundPrevious.CloseAsync(remaining, cancellationToken),
                    TimeSpan.FromMilliseconds(remaining), cancellationToken, "替换 HTTP 客户端前等待旧资源关闭");
                cancellationToken.ThrowIfCancellationRequested();
                CheckExecution();
            }
            var publicationGate = HttpHelper.GetPublicationGate(context.Resources, clientName);
            if (!await execution.StopSignal.EnterAsync(publicationGate, Remaining(), cancellationToken, "HTTP 客户端发布登记排队"))
                throw new TimeoutException($"等待 HTTP 客户端发布登记超过总期限 {setting.TimeoutMs} ms");
            try
            {
                _ = Remaining();
                if (setting.ReplaceIfExists)
                {
                    bool found = context.Resources.TryGet<HttpClientResource>(key, out var current);
                    _ = Remaining();
                    if ((found && !ReferenceEquals(current, previous)) || (!found && context.Resources.Contains(key)))
                        throw new InvalidOperationException("HTTP 同名客户端已被其它所有者替换，拒绝覆盖未收尾资源");
                }
                try
                {
                    _ = Remaining();
                    if (setting.ReplaceIfExists) context.Resources.Set(key, resource);
                    else if (!context.Resources.TryAdd(key, resource)) throw new InvalidOperationException("同名 HTTP 客户端已存在");
                    CheckExecution();
                }
                catch
                {
                    // Cleanup may invalidate the epoch during a registry method.
                    // The cross-epoch fence keeps a fresh publisher outside while
                    // this exact staged owner is compensated; never remove another.
                    if (context.Resources.TryGet<HttpClientResource>(key, out var current) && ReferenceEquals(current, resource))
                        context.Resources.Remove(key, false);
                    throw;
                }
            }
            finally { publicationGate.Release(); }

            var endpoint = HttpHelper.DescribeEndpoint(baseUrl);
            if (setting.IgnoreServerCertificateErrors)
                context.Log(LogLevel.Warn, "当前 HTTP 客户端关闭了服务端证书验证，仅可用于经过确认的测试环境。");
            context.Log($"HTTP 客户端已创建: {clientName} -> {endpoint}（认证方式: {setting.AuthMode}）");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            resource.CheckPublication(context.Resources, key, cancellationToken);
            stagedResource = null;

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = endpoint }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return Error("创建 HTTP 客户端失败", ex);
        }
        finally
        {
            stagedResource?.Dispose();
            stagedClient?.Dispose();
            if (ownsGate) registrationGate!.Release();
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null)
        => ex is null ? ExecutionResults.Error(message) : ExecutionResults.FromException(ex, message);
}
