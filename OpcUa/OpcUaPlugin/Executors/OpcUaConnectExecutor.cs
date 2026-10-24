using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Diagnostics;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 连接执行器</summary>
public sealed class OpcUaConnectExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<CancellationToken, Task<Session>>? _createSession;
    public OpcUaConnectExecutor() { }
    internal OpcUaConnectExecutor(Func<CancellationToken, Task<Session>> createSession) => _createSession = createSession;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        OpcUaConnectionResource? created = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var epoch = guard.Epoch;
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaConnectPlugin().CreateSerializer();
            var setting = (OpcUaConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.SecurityPolicy)) throw new ArgumentOutOfRangeException(nameof(setting.SecurityPolicy), "未知 SecurityPolicy，不能降级为无安全策略");
            if (!Enum.IsDefined(setting.AuthMode)) throw new ArgumentOutOfRangeException(nameof(setting.AuthMode), "未知 AuthMode，不能回退匿名认证");

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var endpointUrl = await Evaluator.EvalStringAsync(setting.EndpointUrl, context);
            epoch.ThrowIfExpired();
            guard.Check();
            var key = OpcUaHelper.GetSessionKey(connName);
            if (string.IsNullOrWhiteSpace(connName)) throw new InvalidOperationException("OPC UA 连接名称不能为空");
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            var startedAt = Stopwatch.GetTimestamp();
            long generation = OpcUaHelper.GetCloseGeneration(context.Resources, key);
            var registration = OpcUaHelper.GetRegistrationLock(context.Resources, key);
            if (!await registration.WaitAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken))
                throw new TimeoutException("OPC UA 等待同名建立/关闭登记门超过连接总期限");
            try
            {
                epoch.ThrowIfExpired();
                OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                if (context.Resources.TryGet<OpcUaConnectionResource>(key, out var previous))
                {
                    await previous.CloseAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                    guard.Check();
                    if (context.Resources.TryGet<OpcUaConnectionResource>(key, out var previousCurrent) && ReferenceEquals(previousCurrent, previous))
                        context.Resources.Remove(key, dispose: false);
                }
                OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                epoch.ThrowIfExpired();
                var owner = new OpcUaConnectionResource(setting.TimeoutMs,
                    ex => context.Log(LogLevel.Error, $"OPC UA {connName} 收尾未确认", ex.ToString()),
                    OpcUaHelper.GetRetirementKey(endpointUrl, connName));
                owner.ExecutionCheck = epoch.ThrowIfExpired;
                created = owner;
                guard.Check();
                if (!context.Resources.TryAdd(key, owner))
                {
                    owner.Dispose();
                    throw new InvalidOperationException($"OPC UA 连接 {connName} 已存在，不能覆盖未关闭的资源");
                }
                try
                {
                    epoch.ThrowIfExpired();
                    OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                    await owner.InitializeAsync(async token =>
                    {
                        epoch.ThrowIfExpired();
                        if (_createSession != null) return await _createSession(token).ConfigureAwait(false);
                        var appConfig = new ApplicationConfiguration
                        {
                ApplicationName = "xTestPlatform_OpcUaPlugin",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = setting.AutoAcceptCertificate
                },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = setting.TimeoutMs },
                TransportQuotas = new TransportQuotas { OperationTimeout = setting.TimeoutMs }
                        };

            await appConfig.Validate(ApplicationType.Client);
            owner.ThrowIfSubmissionStopped();

            if (setting.AutoAcceptCertificate)
            {
                appConfig.CertificateValidator.CertificateValidation += (_, e) => { e.Accept = true; };
            }

            // 选择端点：按 SecurityPolicy 精确匹配
            var selectedEndpoint = SelectEndpoint(appConfig, endpointUrl, setting.SecurityPolicy);
            owner.ThrowIfSubmissionStopped();

            var endpointConfig = EndpointConfiguration.Create(appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            // 创建用户身份
            IUserIdentity userIdentity;
            if (setting.AuthMode == OpcUaAuthMode.UserPassword)
            {
                userIdentity = new UserIdentity(setting.UserName, setting.Password);
            }
            else
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }

            // 创建会话
            owner.ThrowIfSubmissionStopped();
            return await Session.Create(
                appConfig,
                endpoint,
                false,
                "xTestPlatform_OpcUa_" + connName,
                (uint)setting.TimeoutMs,
                userIdentity,
                null,
                token);
                    }, OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                    epoch.ThrowIfExpired();
                    OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
                {
                    // 用户只取消等待。真实 Create/SDK 回调仍由已登记 owner 持有，不再用 None 额外等待收尾并把 Aborted 改成 Error。
                    owner.Dispose();
                    throw;
                }
                catch (Exception primary)
                {
                    try
                    {
                        owner.Dispose();
                        if (!owner.IsClosed) await owner.CloseAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs));
                        if (context.Resources.TryGet<OpcUaConnectionResource>(key, out var failedCurrent) && ReferenceEquals(failedCurrent, owner))
                            context.Resources.Remove(key, dispose: false);
                    }
                    catch (Exception cleanup) { throw new AggregateException("OPC UA 连接失败且真实收尾未确认，保留连接资源，请重试断开", primary, cleanup); }
                    throw;
                }
            }
            finally { registration.Release(); }

            context.Log($"OPC UA 连接已建立: {connName} ({endpointUrl})");
            guard.EnsureCurrent(key, created!);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已连接: {connName}" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            created?.Dispose();
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            created?.Dispose();
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"OPC UA 连接失败: {ex.Message}")
                }
            };
        }
    }

    private static EndpointDescription SelectEndpoint(ApplicationConfiguration appConfig, string endpointUrl, OpcUaSecurityPolicy policy)
    {
        var policyUri = policy switch
        {
            OpcUaSecurityPolicy.Basic256Sha256 => SecurityPolicies.Basic256Sha256,
            OpcUaSecurityPolicy.Aes128Sha256RsaOaep => SecurityPolicies.Aes128_Sha256_RsaOaep,
            OpcUaSecurityPolicy.Aes256Sha256RsaPss => SecurityPolicies.Aes256_Sha256_RsaPss,
            OpcUaSecurityPolicy.None => SecurityPolicies.None,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), "未知 OPC UA 安全策略，禁止降级")
        };

        var uri = new Uri(endpointUrl);
        using var client = DiscoveryClient.Create(appConfig, uri);
        var endpoints = client.GetEndpoints(null);

        var selected = endpoints
            .Where(e => e.SecurityPolicyUri == policyUri && e.EndpointUrl.StartsWith(uri.Scheme, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.SecurityMode == MessageSecurityMode.SignAndEncrypt)
            .ThenByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (selected == null)
        {
            throw new InvalidOperationException($"服务器 {endpointUrl} 未提供安全策略为 {policy} 的端点");
        }

        return selected;
    }
}
