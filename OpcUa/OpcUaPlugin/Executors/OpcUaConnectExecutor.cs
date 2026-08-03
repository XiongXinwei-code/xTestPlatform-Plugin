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
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var endpointUrl = await Evaluator.EvaluateAsync<string>(setting.EndpointUrl, context) ?? setting.EndpointUrl;
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

            // 选择端点
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: setting.SecurityPolicy != OpcUaSecurityPolicy.None);

            var endpointConfig = EndpointConfiguration.Create(appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            // 创建用户身份
            IUserIdentity userIdentity;
            if (setting.AuthMode == OpcUaAuthMode.UserPassword)
            {
                var userName = await Evaluator.EvaluateAsync<string>(setting.UserName, context) ?? setting.UserName;
                var password = await Evaluator.EvaluateAsync<string>(setting.Password, context) ?? setting.Password;
                userIdentity = new UserIdentity(userName, password);
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

            context.CurrentStep.RuntimeData[key] = session;

            context.LogAction?.Invoke($"OPC UA 连接已建立: {connName} ({endpointUrl})");

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
                    Error = new ErrorInfo { Message = $"OPC UA 连接失败: {ex.Message}" }
                }
            };
        }
    }
}
