using NiGrpc.Executors;
using NiGrpc.Runtime;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiGrpc;

public interface INiGrpcPlugin
{
    string StepTypeId { get; }
    string IconPath { get; }
    IStepSettingSerializer CreateSerializer();
    void ValidateRequestJson(string json);
}

public abstract class NiGrpcPluginBase<T> : NiGrpc.PluginInfrastructure.StrictStepPluginBase<T>, INiGrpcPlugin where T : NiGrpcSetting, new()
{
    private readonly Operation _operation;
    private readonly NiDriver? _driver;
    private readonly string? _method;
    private readonly string _description;
    protected NiGrpcPluginBase(Operation operation, NiDriver? driver, string? method, string description)
    { _operation = operation; _driver = driver; _method = method; _description = description; }
    public override string Category => "Communication";
    public void ValidateRequestJson(string json)
    {
        if (_driver == NiDriver.Daqmx && DaqmxExtendedRequests.Supports(_method))
            DaqmxExtendedRequests.ValidateJson(_method!, json);
    }
    public override string IconPath => "pack://application:,,,/NiGrpc.StepPlugin.UI;component/Resources/Icons/" + StepTypeId.Replace('.', '_').ToLowerInvariant() + ".png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiGrpc", () => new NiGrpcExecutor<T>(_operation, CreateSerializer(), _driver, _method));
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"{DisplayName}: {s.ConnectionName} {(s as NiGrpcSessionSetting)?.SessionName}";
    }
    public override string Description
    {
        get
        {
            var rows = "| ConnectionName | 是 | string([ExpressionField]) | \"NIGrpc1\" | 求值结果为 string，连接名称 |\n";
            if (typeof(NiGrpcSessionSetting).IsAssignableFrom(typeof(T))) rows += "| SessionName | 是 | string([ExpressionField]) | \"Task1\" | 求值结果为 string，会话别名 |\n";
            if (typeof(T) == typeof(NiGrpcConnectSetting)) rows += "| Endpoint | 是 | string([ExpressionField]) | \"http://localhost:31763\" | 求值结果为 string，gRPC 服务地址 |\n";
            if (typeof(T) == typeof(NiGrpcXnetOpenSetting)) rows += "| ConfigurationJson | 是 | string([ExpressionField]) | 空 JSON 对象字符串 | 求值结果为 string；protobuf JSON 含 interfaceName 字符串、mode 或 modeRaw 数值，mode 取 CREATE_SESSION_MODE_FRAME_IN_STREAM、CREATE_SESSION_MODE_FRAME_IN_QUEUED、CREATE_SESSION_MODE_FRAME_OUT_STREAM、CREATE_SESSION_MODE_FRAME_OUT_QUEUED 等；可含 databaseName、clusterName、list 字符串，不能包含 sessionName 或 initializationBehavior |\n";
            if (typeof(NiGrpcRequestSetting).IsAssignableFrom(typeof(T)))
            {
                string requestDefault = DaqmxExtendedRequests.Supports(_method) ? "预填请求表达式（按实际通道、容量及路由修改）" : "空 JSON 对象字符串";
                rows += $"| RequestJson | 是 | string([ExpressionField]) | {requestDefault} | 求值结果为 string，本操作的 protobuf JSON；{RequestDetails(_method)} |\n";
                rows += "| ResultVariable | 否 | string([VariablePathField]) | 空 | 完整字符串变量路径，用于保存响应 protobuf JSON；默认零值或空数组字段可按 protobuf 规则省略 |\n";
            }
            rows += "| TimeoutMs | 是 | int | 5000 | 本步骤总等待期限，单位毫秒，须大于 0 |\n";
            string sharedWarning = _driver == NiDriver.Xnet && _method is "Start" or "Stop" or "SetProperty"
                ? "接口级 scope、接口波特率或终端电阻配置可能影响同一物理接口的其他会话及通信。" : "";
            if (DaqmxExtendedRequests.Supports(_method))
                sharedWarning += "新增 DAQmx 操作沿用已有任务与独立关闭步骤，不能在请求中自填 task。先创建任务、配置通道/定时/触发，再启动和读写；停止任务不删除会话，关闭会话执行 Clear。数字输出和脉冲输出可能驱动外部设备；默认 autoStart=false，按需显式设置。每个任务的通道顺序遵循 DAQmx 配置顺序，按 fillMode/dataLayout 解释数组，读取使用实际 sampsPerChanRead，不把数组容量当采样数。同步须有硬件共享时钟/触发及可用路由，先启动等待触发的从任务，最后启动主任务；多个顺序 RPC 不能证明硬件同步。具体设备可能拒绝不支持的通道、模式或路由，保留服务端错误。";
            return $"## 功能\n\n{_description}；本步骤对应 {_method ?? _operation.ToString()}。\n\n## 参数\n\n| 参数 | 必填 | 类型 | 默认值 | 说明 |\n|------|------|------|--------|------|\n{rows}\n## 行为\n\n同一插件实例中的不同执行代次采用保守协调域；另一代仍持有连接、活动任务或收尾未退出时按本步骤期限等待，远端状态未知时保持隔离。RPC 超时或取消不代表远端操作已结束，未知状态不会自动重发。连接健康检查不代表驱动可用。{sharedWarning}\n\n## 相关插件\n\nNI gRPC 连接、DAQmx / XNET 创建会话、关闭会话和断开连接。";
        }
    }
    private static string RequestDetails(string? method) => method switch
    {
        "CreateAIVoltageChan" => "physicalChannel 字符串；terminalConfig 或 terminalConfigRaw 必选：INPUT_TERM_CFG_WITH_DEFAULT_CFG_DEFAULT 对应 raw=-1、INPUT_TERM_CFG_WITH_DEFAULT_RSE 对应 10083、INPUT_TERM_CFG_WITH_DEFAULT_NRSE 对应 10078、INPUT_TERM_CFG_WITH_DEFAULT_DIFF 对应 10106、INPUT_TERM_CFG_WITH_DEFAULT_PSEUDO_DIFF 对应 12529；minVal/maxVal 数值；units 或 unitsRaw 必选：VOLTAGE_UNITS2_VOLTS 对应 raw=10348、VOLTAGE_UNITS2_FROM_CUSTOM_SCALE 对应 raw=10065；可选 nameToAssignToChannel、customScaleName 字符串",
        "CreateAOVoltageChan" => "physicalChannel 字符串、minVal/maxVal 数值；units 或 unitsRaw 必选：VOLTAGE_UNITS2_VOLTS 对应 raw=10348、VOLTAGE_UNITS2_FROM_CUSTOM_SCALE 对应 raw=10065；可选 nameToAssignToChannel、customScaleName 字符串",
        "CfgSampClkTiming" => "source 字符串、rate 数值；activeEdge 或 activeEdgeRaw 必选：EDGE1_RISING 对应 raw=10280、EDGE1_FALLING 对应 raw=10171；sampleMode 或 sampleModeRaw 必选：ACQUISITION_TYPE_FINITE_SAMPS 对应 raw=10178、ACQUISITION_TYPE_CONT_SAMPS 对应 raw=10123、ACQUISITION_TYPE_HW_TIMED_SINGLE_POINT 对应 raw=12522；sampsPerChan 为 uint64（JSON 字符串）",
        "ReadAnalogF64" => "numSampsPerChan 整数（-1 或正数）、timeout 数值（秒）、fillMode 或 fillModeRaw 必选（GROUP_BY_GROUP_BY_CHANNEL 对应 raw=0、GROUP_BY_GROUP_BY_SCAN_NUMBER 对应 raw=1）、arraySizeInSamps 整数 1 至 1000000",
        "WriteAnalogF64" => "numSampsPerChan 正整数、autoStart 布尔值、timeout 数值（秒）、dataLayout 或 dataLayoutRaw 必选（GROUP_BY_GROUP_BY_CHANNEL 对应 raw=0、GROUP_BY_GROUP_BY_SCAN_NUMBER 对应 raw=1）、writeArray 数值数组；数组长度必须等于实际通道数乘每通道采样数且最多 1000000 项",
        "Start" or "Stop" => "scope 或 scopeRaw 必选：START_STOP_SCOPE_NORMAL 对应 raw=0、START_STOP_SCOPE_SESSION_ONLY 对应 raw=1、START_STOP_SCOPE_INTERFACE_ONLY 对应 raw=2、START_STOP_SCOPE_SESSION_ONLY_BLOCKING 对应 raw=3",
        "ReadFrame" => "numberOfFrames 整数 1 至 4096，maxPayloadPerFrame 整数 1 至 64；protocol=PROTOCOL_CAN 或 protocolRaw=1；timeout 或 timeoutRaw 必选，timeoutRaw 数值单位秒",
        "WriteFrame" => "timeout 或 timeoutRaw 必选（后者数值单位秒），buffer 数组含 1 至 4096 帧；每项 can 包含 identifier 整数、type 或 typeRaw（FRAME_TYPE_CAN_DATA 对应 raw=0、FRAME_TYPE_CAN_REMOTE 对应 raw=1、FRAME_TYPE_CAN20_DATA 对应 raw=8、FRAME_TYPE_CANFD_DATA 对应 raw=16、FRAME_TYPE_CANFDBRS_DATA 对应 raw=24）、payload Base64 字符串，普通帧最多 8 字节、FD 最多 64 字节",
        "ReadState" => "stateId 或 stateIdRaw 必选，仅支持 READ_STATE_TIME_CURRENT 对应 raw=118685697、READ_STATE_TIME_COMMUNICATING 对应 118685698、READ_STATE_TIME_START 对应 118685699、READ_STATE_SESSION_INFO 对应 1245188、READ_STATE_TIME_CURRENT_2 对应 202571781、READ_STATE_TIME_COMMUNICATING_2 对应 202571782、READ_STATE_TIME_START_2 对应 202571783、READ_STATE_CAN_COMM 对应 1245200",
        "GetProperty" => "propertyId 或 propertyIdRaw 必选，仅支持 PROPERTY_SESSION_INTF_BAUD_RATE 对应 raw=1048598、PROPERTY_SESSION_INTF_CAN_FD_BAUD_RATE 对应 1048615、PROPERTY_SESSION_INTF_CAN_TERM 对应 1048613（均 uint32），PROPERTY_SESSION_INTF_BAUD_RATE_64 对应 152043542、PROPERTY_SESSION_INTF_CAN_FD_BAUD_RATE_64 对应 152043559（均 uint64）；禁止自填 device/interface/session",
        "SetProperty" => "propertyId 或 propertyIdRaw 必选；PROPERTY_SESSION_INTF_BAUD_RATE、PROPERTY_SESSION_INTF_CAN_FD_BAUD_RATE、PROPERTY_SESSION_INTF_CAN_TERM 对应 raw=1048598、1048615、1048613，使用 u32Scalar 数值；PROPERTY_SESSION_INTF_BAUD_RATE_64、PROPERTY_SESSION_INTF_CAN_FD_BAUD_RATE_64 对应 raw=152043542、152043559，使用 u64Scalar（JSON 字符串）；其他属性拒绝提交",
        _ when DaqmxExtendedRequests.Supports(method) => DaqmxExtendedRequests.Describe(method!),
        _ => "不提供 task 或 session，这些字段由插件注入"
    };
}
