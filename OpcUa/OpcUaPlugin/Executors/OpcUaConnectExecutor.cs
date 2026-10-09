using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
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

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaConnectPlugin().CreateSerializer();
        var setting = (OpcUaConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var endpointUrl = await Evaluator.EvalStringAsync(setting.EndpointUrl, context);
            var key = OpcUaHelper.GetSessionKey(connName);

            // 创建应用程序配置
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

            if (setting.AutoAcceptCertificate)
            {
                appConfig.CertificateValidator.CertificateValidation += (_, e) => { e.Accept = true; };
            }

            // 选择端点：按 SecurityPolicy 精确匹配
            var selectedEndpoint = SelectEndpoint(appConfig, endpointUrl, setting.SecurityPolicy);

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
            var session = await Session.Create(
                appConfig,
                endpoint,
                false,
                "xTestPlatform_OpcUa_" + connName,
                (uint)setting.TimeoutMs,
                userIdentity,
                null,
                cancellationToken);

            // Set 会自动销毁同名旧会话（如上次运行异常终止未断开）
            context.Resources.Set(key, session);

            context.Log($"OPC UA 连接已建立: {connName} ({endpointUrl})");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已连接: {connName}" }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
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
            _ => SecurityPolicies.None
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
