using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 连接插件，建立与 OPC UA 服务器的会话</summary>
public sealed class OpcUaConnectPlugin : StepPluginBase<OpcUaConnectSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.Connect";
    public override string DisplayName => "OpcUa_Connect";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        建立 OPC UA 连接，支持匿名和用户名密码认证以及多种安全策略，连接成功后通过 ConnectionName 标识。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | OpcUa1 | 连接标识名，序列内唯一 |
        | EndpointUrl | string([ExpressionField]) | 是 | — | 服务器端点，如 opc.tcp://192.168.1.1:4840 |
        | SecurityPolicy | 枚举 | 否 | None | 可选值：None, Basic256Sha256, Aes128Sha256RsaOaep, Aes256Sha256RsaPss |
        | AuthMode | 枚举 | 否 | Anonymous | 可选值：Anonymous, UserPassword |
        | UserName | string | 否 | 空 | AuthMode=UserPassword 时使用 |
        | Password | string | 否 | 空 | AuthMode=UserPassword 时使用 |
        | TimeoutMs | int | 否 | 5000 | 连接超时毫秒数 |
        | AutoAcceptCertificate | bool | 否 | true | 是否自动接受服务器证书 |

        ## 行为

        - 连接失败、认证失败或同名连接已存在时步骤报错

        ## 相关插件

        - `OpcUa_Read` / `OpcUa_Write` / `OpcUa_BatchRead` / `OpcUa_BatchWrite` / `OpcUa_Subscribe`：在此连接上操作
        - `OpcUa_DataAcq_Start` / `OpcUa_DataAcq_Stop`：后台数据采集
        - `OpcUa_Disconnect`：断开本插件建立的连接
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Connect {s.ConnectionName} ({s.EndpointUrl})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaConnectSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_001", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("OPCUA_001E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.EndpointUrl))
            errors.Add(StepSettingError.Error("OPCUA_002", "端点 URL 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.EndpointUrl, context.ExecutionContext, out var urlErr))
            errors.Add(StepSettingError.Error("OPCUA_002E", $"EndpointUrl 表达式无效: {urlErr}"));
        if (s.AuthMode == OpcUaAuthMode.UserPassword && string.IsNullOrWhiteSpace(s.UserName))
            errors.Add(StepSettingError.Error("OPCUA_003", "用户名密码模式下用户名不能为空"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_004", "超时必须大于 0"));
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
