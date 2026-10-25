using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Http.Models;

namespace Http.Helpers;

/// <summary>
/// 已创建的 HTTP 客户端资源，持有 HttpClient 及其底层 Handler，由资源注册表统一释放
/// </summary>
public sealed class HttpClientResource : IDisposable
{
    private readonly object _sync = new();
    private readonly HashSet<Task> _active = new();
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _closeTask;
    private bool _closing;
    private bool _closed;
    public bool IsClosed { get { lock (_sync) return _closed; } }
    public required HttpClient Client { get; init; }
    internal Action? CheckExecutionScope { get; init; }

    /// <summary>创建该客户端时使用的基地址，便于日志与诊断</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>创建该客户端时使用的认证方式</summary>
    public AuthMode AuthMode { get; init; }

    public sealed record Response(string Content, int StatusCode, bool IsSuccess, long ElapsedMs);

    // The worker owns the request until actual completion, not until a caller stops waiting.
    // HTTP requests remain concurrent; only admission/close/publication use the owner lock.
    public Task<Response> SendAsync(HttpRequestMessage request, CancellationToken caller)
    {
        Task<Response> actual;
        RequestCancellation cancellation;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var timeout = Client.Timeout;
        lock (_sync)
        {
            caller.ThrowIfCancellationRequested();
            CheckAvailable();
            cancellation = new RequestCancellation(caller, _shutdown.Token);
            actual = Task.Run(async () =>
            {
                Exception? primary = null;
                try
                {
                    using (request)
                    {
                        lock (_sync)
                        {
                            // Proxy cancellation may lag behind the original stop/close state.
                            caller.ThrowIfCancellationRequested();
                            CheckAvailable();
                            cancellation.Token.ThrowIfCancellationRequested();
                            if (timeout != Timeout.InfiniteTimeSpan && System.Diagnostics.Stopwatch.GetElapsedTime(started) >= timeout)
                                throw new TimeoutException($"HTTP 请求提交前超过 {timeout.TotalMilliseconds} ms，本次未发送");
                        }
                        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellation.Token).ConfigureAwait(false);
                        var content = await response.Content.ReadAsStringAsync(cancellation.Token).ConfigureAwait(false);
                        return new Response(content, (int)response.StatusCode, response.IsSuccessStatusCode,
                            (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    }
                }
                catch (Exception ex) { primary = ex; throw; }
                finally
                {
                    try { await cancellation.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception cleanup) when (primary != null)
                    { throw new AggregateException("HTTP 请求与取消回调收尾均失败", primary, cleanup); }
                }
            });
            _active.Add(actual);
            _ = actual.ContinueWith(task =>
            {
                if (task.Exception is not null) System.Diagnostics.Trace.TraceError($"HTTP 后台请求失败: {task.Exception}");
                lock (_sync) _active.Remove(task);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        return WaitForResponseAsync(actual, timeout, started, cancellation, caller);
    }

    private static async Task<Response> WaitForResponseAsync(Task<Response> actual, TimeSpan timeout, long started,
        RequestCancellation cancellation, CancellationToken caller)
    {
        try
        {
            var result = await actual.WaitAsync(timeout, caller).ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            if (timeout != Timeout.InfiniteTimeSpan && System.Diagnostics.Stopwatch.GetElapsedTime(started) >= timeout)
                throw new TimeoutException("HTTP 请求结果已超过单调总期限");
            return result;
        }
        catch (TimeoutException ex)
        {
            cancellation.Request();
            throw new TimeoutException($"HTTP 请求超过 {timeout.TotalMilliseconds} ms；停止等待不表示远端已取消，实际请求仍受资源管理", ex);
        }
        catch (OperationCanceledException) when (caller.IsCancellationRequested)
        { cancellation.Request(); throw; }
    }

    public void CheckPublication(xTestPlatform.Core.Engine.IResourceRegistry resources, string key, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        lock (_sync)
        {
            CheckAvailable();
            bool found = resources.TryGet<HttpClientResource>(key, out var current);
            caller.ThrowIfCancellationRequested();
            CheckAvailable();
            caller.ThrowIfCancellationRequested();
            if (!found || !ReferenceEquals(current, this))
                throw new InvalidOperationException("HTTP 客户端所属代次已被替换，拒绝发布旧请求结果");
        }
        caller.ThrowIfCancellationRequested();
    }

    private void CheckAvailable()
    {
        CheckExecutionScope?.Invoke();
        if (_closing) throw new InvalidOperationException("HTTP 客户端正在关闭或已关闭，拒绝新请求及迟到结果");
    }

    public async Task CloseAsync(int timeoutMs, CancellationToken caller = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        caller.ThrowIfCancellationRequested();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        await BeginClose().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), caller).ConfigureAwait(false);
        caller.ThrowIfCancellationRequested();
        if (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
            throw new TimeoutException($"HTTP 关闭返回时已超过总期限 {timeoutMs} ms；真实释放完成={IsClosed}");
    }

    private Task BeginClose()
    {
        lock (_sync)
        {
            _closing = true;
            if (_closeTask is null || _closeTask.IsFaulted)
            {
                _closeTask = Task.Run(async () =>
                {
                    await _shutdown.CancelAsync().ConfigureAwait(false);
                    Task[] active;
                    lock (_sync) active = _active.ToArray();
                    try { await Task.WhenAll(active).ConfigureAwait(false); }
                    catch { /* Individual request errors are observed above; all actual tasks have exited. */ }
                    Client.Dispose();
                    lock (_sync) _closed = true;
                });
                _ = _closeTask.ContinueWith(task =>
                {
                    if (task.Exception is not null) System.Diagnostics.Trace.TraceError($"HTTP 资源关闭未完成: {task.Exception}");
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            return _closeTask;
        }
    }

    public void Dispose() => _ = BeginClose();

    private sealed class RequestCancellation : IAsyncDisposable
    {
        private readonly object _state = new();
        private readonly CancellationTokenSource _source = new();
        private readonly CancellationTokenRegistration _callerRegistration, _closeRegistration;
        private Task? _callbackTask;
        private bool _disposed;
        internal CancellationToken Token => _source.Token;
        internal RequestCancellation(CancellationToken caller, CancellationToken closing)
        {
            // Original stop callbacks only schedule cancellation; handler callbacks never run
            // on the platform's cancelling thread. Actual completion includes their real exit.
            _callerRegistration = caller.UnsafeRegister(static state => ((RequestCancellation)state!).Request(), this);
            _closeRegistration = closing.UnsafeRegister(static state => ((RequestCancellation)state!).Request(), this);
        }
        internal void Request()
        {
            lock (_state)
                if (!_disposed) _callbackTask ??= _source.CancelAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await _callerRegistration.DisposeAsync().ConfigureAwait(false);
            await _closeRegistration.DisposeAsync().ConfigureAwait(false);
            Task callbacks;
            lock (_state) { _disposed = true; callbacks = _callbackTask ?? Task.CompletedTask; }
            try { await callbacks.ConfigureAwait(false); }
            finally { _source.Dispose(); }
        }
    }
}

internal static class HttpHelper
{
    /// <summary>设置请求头；名称及值非法时在发送/登记前失败，不泄露头值。</summary>
    internal static void SetRequestHeader(HttpRequestHeaders headers, string name, string value, bool replace = true)
    {
        if (value.IndexOfAny(['\r', '\n']) >= 0)
            throw new InvalidOperationException($"HTTP 请求头 {name} 的值不能包含换行字符");
        if (replace) headers.Remove(name);
        if (!headers.TryAddWithoutValidation(name, value))
            throw new InvalidOperationException($"HTTP 请求头 {name} 不能用于请求；内容类型请使用步骤的 ContentType 设置");
    }

    /// <summary>拼接客户端在资源注册表中的键</summary>
    public static string GetClientKey(string clientName) => $"Http.Client.{clientName}";

    /// <summary>错误诊断仅记录目标主机，不记录认证信息、路径、查询参数或请求负载。</summary>
    public static string DescribeRequest(string clientName, HttpClient client, HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is { IsAbsoluteUri: false } && client.BaseAddress != null)
            uri = new Uri(client.BaseAddress, uri);
        var target = uri is { IsAbsoluteUri: true }
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "未配置绝对目标地址";
        var timeout = client.Timeout == Timeout.InfiniteTimeSpan
            ? "无限制"
            : $"{client.Timeout.TotalMilliseconds} ms";
        return $"客户端={clientName}，方法={request.Method}，目标={target}，超时={timeout}";
    }

    /// <summary>
    /// 按认证方式与 TLS 选项构建 HttpClient。
    /// 四种认证方式与 TLS 选项相互正交，未使用的字段全部忽略。
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<xTestPlatform.Core.Engine.IResourceRegistry, SemaphoreSlim> RegistryGates = new();
    public static SemaphoreSlim GetRegistryGate(xTestPlatform.Core.Engine.IResourceRegistry resources) =>
        RegistryGates.GetValue(resources, _ => new SemaphoreSlim(1, 1));

    public static string DescribeEndpoint(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "未配置有效 HTTP(S) 目标";

    // Certificate file/crypto initialization may block. A timed-out builder keeps ownership
    // of its exact late result and must not dispose a newer named client's handler.
    internal static async Task<HttpClient> BuildClientAsync(Func<HttpClient> build, int timeoutMs, CancellationToken caller)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        caller.ThrowIfCancellationRequested();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var actual = Task.Run(() =>
        {
            caller.ThrowIfCancellationRequested();
            if (timeoutMs > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"HTTP 客户端建立提交前超过 {timeoutMs} ms");
            return build();
        });
        try
        {
            var client = await actual.WaitAsync(timeoutMs == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(timeoutMs), caller).ConfigureAwait(false);
            caller.ThrowIfCancellationRequested();
            if (timeoutMs > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"HTTP 客户端建立返回时已超过总期限 {timeoutMs} ms");
            return client;
        }
        catch
        {
            _ = actual.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    try { task.Result.Dispose(); }
                    catch (Exception ex) { System.Diagnostics.Trace.TraceError($"HTTP 迟到客户端释放失败: {ex}"); }
                }
                else if (task.Exception is not null) System.Diagnostics.Trace.TraceError($"HTTP 后台客户端建立失败: {task.Exception}");
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    public static void ValidateRequestDestination(HttpClientResource resource, HttpRequestMessage request)
    {
        var target = request.RequestUri;
        if (target is { IsAbsoluteUri: false } && resource.Client.BaseAddress != null)
            target = new Uri(resource.Client.BaseAddress, target);
        if (target == null || !target.IsAbsoluteUri || target.Scheme is not ("http" or "https") || target.UserInfo.Length != 0)
            throw new InvalidOperationException("HTTP 目标必须为不含用户凭据的 HTTP(S) 地址");
        bool authenticated = resource.AuthMode != AuthMode.None || resource.Client.DefaultRequestHeaders.Contains("Authorization");
        if (authenticated)
        {
            var origin = resource.Client.BaseAddress;
            if (origin == null || !string.Equals(origin.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(origin.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase) || origin.Port != target.Port)
                throw new InvalidOperationException("拒绝将客户端默认认证发送到其他源；请为该目标创建独立 HTTP 客户端");
        }
    }

    public static HttpClient BuildClient(
        string baseUrl, int timeoutMs, AuthMode authMode, string userName, string password,
        string token, string certPath, string certPassword, bool ignoreServerCertificateErrors,
        bool forceConnectionClose = true, bool hasDefaultAuthorization = false)
    {
        Uri? address = null;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
                parsed.Scheme is not ("http" or "https") || parsed.UserInfo.Length != 0)
                throw new InvalidOperationException("HTTP 基地址必须为不含用户凭据的 HTTP(S) 绝对地址");
            var builder = new UriBuilder(parsed);
            if (!builder.Path.EndsWith('/')) builder.Path += "/";
            address = builder.Uri;
        }
        if (timeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "HTTP 超时不能为负数");
        if (!Enum.IsDefined(authMode)) throw new ArgumentException("HTTP 认证方式无效", nameof(authMode));
        if (authMode == AuthMode.BearerToken && string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("认证方式为 Bearer Token，但未配置 Token");
        if (authMode == AuthMode.BearerToken && token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Bearer Token 不能包含空白或换行字符");

        var handler = new HttpClientHandler
        {
            UseProxy = false, Proxy = null,
            // Redirects must not silently change the credential/certificate destination.
            AllowAutoRedirect = authMode == AuthMode.None && !hasDefaultAuthorization
        };
        HttpClient? client = null;
        X509Certificate2? certificate = null;
        try
        {
            if (authMode == AuthMode.ClientCertificate)
            {
                if (string.IsNullOrWhiteSpace(certPath) || !File.Exists(certPath))
                    throw new InvalidOperationException("客户端证书文件不存在或未配置");
                certificate = new X509Certificate2(certPath, certPassword,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ClientCertificates.Add(certificate);
            }
            handler.ServerCertificateCustomValidationCallback = ignoreServerCertificateErrors
                ? static (_, _, _, _) => true
                : static (_, _, _, errors) => errors == SslPolicyErrors.None;
            HttpMessageHandler pipeline = certificate == null ? handler : new CertificateOwnerHandler(handler, certificate);
            client = new HttpClient(pipeline, disposeHandler: true)
            {
                BaseAddress = address,
                Timeout = timeoutMs > 0 ? TimeSpan.FromMilliseconds(timeoutMs) : Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.ConnectionClose = forceConnectionClose;
            if (authMode == AuthMode.Basic)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}")));
            else if (authMode == AuthMode.BearerToken)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        catch
        {
            if (client != null) client.Dispose(); else handler.Dispose();
            certificate?.Dispose();
            throw;
        }
    }

    private sealed class CertificateOwnerHandler(HttpMessageHandler inner, X509Certificate2 certificate) : DelegatingHandler(inner)
    {
        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) certificate.Dispose(); }
        }
    }

    /// <summary>将枚举方法名映射为 HttpMethod</summary>
    public static HttpMethod ToHttpMethod(HttpMethodType method) => method switch
    {
        HttpMethodType.Get => HttpMethod.Get,
        HttpMethodType.Post => HttpMethod.Post,
        HttpMethodType.Put => HttpMethod.Put,
        HttpMethodType.Patch => HttpMethod.Patch,
        HttpMethodType.Delete => HttpMethod.Delete,
        HttpMethodType.Head => HttpMethod.Head,
        HttpMethodType.Options => HttpMethod.Options,
        _ => throw new ArgumentOutOfRangeException(nameof(method), "HTTP 方法无效")
    };

    /// <summary>将内容类型枚举映射为 MIME 字符串，None 返回 null</summary>
    public static string? ToMediaType(BodyContentType contentType) => contentType switch
    {
        BodyContentType.Json => "application/json",
        BodyContentType.Xml => "application/xml",
        BodyContentType.Text => "text/plain",
        BodyContentType.FormUrlEncoded => "application/x-www-form-urlencoded",
        BodyContentType.None => null,
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), "HTTP 请求体类型无效")
    };
}
