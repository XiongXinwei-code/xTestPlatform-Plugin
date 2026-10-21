using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class BindAxisSetting : AxisSetting
{
	[Field("通信后端", "EtherCAT 必须注册主站适配工厂")]
	public TransportKind Transport { get; set; } = TransportKind.CANopen;
	[ExpressionField, Field("连接名", "求值结果为 string；CAN_Open 连接或 EtherCAT 主站连接", Required = true, ResultType = typeof(string))]
	public string ConnectionName { get; set; } = "\"CAN1\"";
	[ExpressionField, Field("CANopen 节点", "求值结果为整数；不允许广播", Required = true, Min = 1, Max = 127, Backend = "CANopen")]
	public string NodeId { get; set; } = "1";
	[ExpressionField, Field("EtherCAT 从站地址", "求值结果为整数；含义由主站适配器定义", Required = true, Min = 0, Max = 65535, Backend = "EtherCAT")]
	public string SlaveAddress { get; set; } = "0";
	[Field("轴对象索引偏移", "标准 0x6000~0x67FF 对象的显式偏移；按设备手册配置", Min = 0, Max = 38912)]
	public int ObjectIndexOffset { get; set; }
	[Field("单次通信超时（ms）", "阻塞通信软超时；不得无限等待", Min = 1, Max = 60000)]
	public int CommunicationTimeoutMs { get; set; } = 1000;
	[Field("反馈有效期（ms）", "共享接收队列中反馈的最大驻留时间；不保证硬件采样年龄", Min = 1, Max = 60000)]
	public int FeedbackMaxAgeMs { get; set; } = 100;
	[Field("CSP RPDO 编号", "0 表示未配置；1 对应 0x1400/0x1600，绑定时核对设备映射", Min = 0, Max = 512, Backend = "CANopen")]
	public int CspPdoNumber { get; set; }
	[Field("CSP RPDO 布局", "显式索引:子索引:位数，逗号分隔；仅允许目标位置及可选控制字", Backend = "CANopen")]
	public string CspPdoLayout { get; set; } = "0x607A:0:32";
	[Field("CSV RPDO 编号", "0 表示未配置；不自动重配驱动", Min = 0, Max = 512, Backend = "CANopen")]
	public int CsvPdoNumber { get; set; }
	[Field("CSV RPDO 布局", "显式索引:子索引:位数；仅允许目标速度及可选控制字", Backend = "CANopen")]
	public string CsvPdoLayout { get; set; } = "0x60FF:0:32";
	[Field("CST RPDO 编号", "0 表示未配置；不自动重配驱动", Min = 0, Max = 512, Backend = "CANopen")]
	public int CstPdoNumber { get; set; }
	[Field("CST RPDO 布局", "显式索引:子索引:位数；仅允许目标转矩及可选控制字", Backend = "CANopen")]
	public string CstPdoLayout { get; set; } = "0x6071:0:16";
	[Field("反馈 TPDO 编号", "0 使用 SDO 顺序读取；非零使用指定 TPDO，须包含状态字", Min = 0, Max = 512, Backend = "CANopen")]
	public int FeedbackPdoNumber { get; set; }
	[Field("反馈 TPDO 布局", "允许 0x6041、0x6064、0x606C、0x6077；索引需包含轴偏移", Backend = "CANopen")]
	public string FeedbackPdoLayout { get; set; } = "0x6041:0:16,0x6064:0:32,0x6077:0:16";
	[Field("硬件诊断对象索引", "0 表示未配置；按设备手册填写实际索引，不自动加轴偏移", Min = 0, Max = 65535)]
	public int DiagnosticIndex { get; set; }
	[Field("硬件诊断子索引", "设备定义的诊断对象子索引", Min = 0, Max = 255)]
	public int DiagnosticSubIndex { get; set; }
	[Field("硬件诊断字节数", "支持 1、2、4 字节无符号值", Min = 1, Max = 4)]
	public int DiagnosticBytes { get; set; } = 2;
	[Field("硬件异常位掩码", "0 只读取；非零时匹配异常值会阻止运动操作", Min = 0, Max = 4294967295)]
	public long DiagnosticMask { get; set; }
	[Field("硬件异常匹配值", "(诊断值 & 掩码) 等于此值时视为硬件条件不满足", Min = 0, Max = 4294967295)]
	public long DiagnosticActiveValue { get; set; }
}
