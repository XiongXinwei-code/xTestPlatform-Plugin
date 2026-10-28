using MessagePack;
using System.Collections.ObjectModel;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

[MessagePackObject(true)]
public sealed class BindAxisSetting : AxisSetting
{
	public TransportKind Transport { get; set; } = TransportKind.CANopen;
	[ExpressionField]
	public string ConnectionName { get; set; } = "\"CAN1\"";
	[ExpressionField]
	public string NodeId { get; set; } = "1";
	[ExpressionField]
	public string SlaveAddress { get; set; } = "0";
	public int ObjectIndexOffset { get; set; }
	public int CommunicationTimeoutMs { get; set; } = 1000;
	public int ShutdownTimeoutMs { get; set; } = 2000;
	public int FeedbackMaxAgeMs { get; set; } = 100;
	public int CspPdoNumber { get; set; }
	public string CspPdoLayout { get; set; } = "0x607A:0:32";
	public int CsvPdoNumber { get; set; }
	public string CsvPdoLayout { get; set; } = "0x60FF:0:32";
	public int CstPdoNumber { get; set; }
	public string CstPdoLayout { get; set; } = "0x6071:0:16";
	public int FeedbackPdoNumber { get; set; }
	public string FeedbackPdoLayout { get; set; } = "0x6041:0:16,0x6064:0:32,0x6077:0:16";
	public bool UseMultipleFeedbackPdos { get; set; }
	public ObservableCollection<FeedbackPdoSetting> FeedbackPdos { get; set; } = [];
	public int DiagnosticIndex { get; set; }
	public int DiagnosticSubIndex { get; set; }
	public int DiagnosticBytes { get; set; } = 2;
	public long DiagnosticMask { get; set; }
	public long DiagnosticActiveValue { get; set; }
}
