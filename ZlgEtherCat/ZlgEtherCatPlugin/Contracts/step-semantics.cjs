// Human-maintained meanings shared by descriptions, summaries and explicit WPF sources.
// This file is used only by the offline generator and never by the deployed plugin.
module.exports = specs => {
 const displayGroups={Master:'主站管理',Configuration:'总线配置',Diagnostics:'诊断与抓包',PDO:'过程数据',
  'Background Reading':'常驻读取','Periodic Reading':'周期读取',SDO:'参数读写',IO:'从站IO',Watchdog:'看门狗',Advanced:'高级功能',Maintenance:'设备维护','Board IO':'板载IO',
  Motion_Session:'运动控制_轴绑定',Motion_State:'运动控制_轴状态',Motion_Feedback:'运动控制_反馈诊断',Motion_Motion:'运动控制_运动指令',Motion_Cyclic:'运动控制_周期目标'};
 const choices = {
  PdoDataType: [
   ['Bytes','原始 byte[]，长度按配置，不转换'],['Boolean','bool，1 字节；任一位非零为 true，不是单个位'],
   ['UInt8','byte，无符号 8 位，1 字节'],['Int8','sbyte，有符号 8 位，1 字节'],
   ['UInt16','ushort，无符号 16 位，2 字节'],['Int16','short，有符号 16 位，2 字节'],
   ['UInt32','uint，无符号 32 位，4 字节'],['Int32','int，有符号 32 位，4 字节'],
   ['UInt64','ulong，无符号 64 位，8 字节'],['Int64','long，有符号 64 位，8 字节'],
   ['Float32','float，IEEE 754，4 字节'],['Float64','double，IEEE 754，8 字节']],
  ExchangeMode: [['Queue','SDK 输入/输出队列；运动 PDO 必须选此项'],['Direct','直接过程映像访问，无新队列帧证明'],['Cycle','SDK 周期异步映像，无新队列帧证明'],['Manual','显式更新异步映像，无新队列帧证明']],
  AcquisitionPriority: [['Normal','普通，默认值 0'],['AboveNormal','高于普通，值 1'],['Highest','最高，值 2；仍由 Windows 调度，不保证硬实时']],
  MasterState: [['Init','INIT 初始化'],['PreOperational','PREOP 预运行／邮箱配置'],['Bootstrap','BOOT 引导／设备支持的维护'],['SafeOperational','SAFEOP 安全运行协议状态'],['Operational','OP 运行状态']],
  PiReinitMode: [['Auto','SDK 自动策略'],['ClearAll','清空全部过程映像'],['PreserveAsynchronous','保留异步映像'],['PreserveSynchronous','保留同步映像'],['PreserveAll','保留全部映像']]
 };
 const scalar = '随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端';
 const timestamps = 'TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间';
 const rxDiagnostics = 'Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false';
 const acquisitionDiagnostics = 'Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（'+rxDiagnostics+'）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null';
 const nullResult = '无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志';
 const results = {
  EnumerateBoards:'板卡数组：每项 Index（驱动枚举索引）与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）；同型号用序列号区分',
  Open:'已选板卡：Index 与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）',
  Close:'对象：Closed、PhysicalStopConfirmed、Detail；Closed 只表示通信句柄关闭',
  BeginExit:'对象：ExitRequested、PhysicalStopConfirmed；不表示驱动已停止',
  StartBus:'ENI 信息对象：MainCycle、MailboxCycle（μs）、OutputBytes、InputBytes（全主站字节数）',
  ReadEniInfo:'ENI 信息对象：MainCycle、MailboxCycle（μs）、OutputBytes、InputBytes（全主站字节数）',
  RequestState:'byte：已确认的 EtherCAT 状态码（INIT=1、PREOP=2、BOOT=3、SAFEOP=4、OP=8）',
  ReadMasterStatus:'对象：Board、BusStatus、Running、State、RequireDc、ActiveBoardOutputs、BoardLatchOwned、BoardLatchStopUnconfirmed；State 是 EtherCAT 状态码，板载输出状态不证明电机物理停稳',
  ConfigureExchange:'对象：Mode（实际配置的交换模式）、OutputBytes（完整主站输出字节数）',
  ReadPiRange:'对象：SlaveIndex、Input、Output、HasMetadata、InputMetadataAvailable、OutputMetadataAvailable、InputFields、OutputFields、ObjectIdentityVerified=false。HasMetadata 仅表示该从站至少有一个 ENI 字段，两个方向标志仅表示该方向有字段，均不保证完整覆盖。Input/Output 的 Offset/Length 是全局字节起点/长度；字段含 Name、EniDataType、AbsoluteBitOffset、RelativeBitOffset、BitLength、ByteAligned、ScalarType、ByteOrder。字段名不证明对象索引或物理身份',
  EnumerateSlaves:'从站数组：Index/SlaveIndex（本 ENI 的零基 SlaveIdx）、Active、Ready、FixedAddress、Info（可为 null）、Range、InfoSource、FixedAddressSource。FixedAddress 来源是当前 ENI；SDK 身份信息未独立验证在线物理身份；离线项仍保留其索引',
  ReadSlaveInfo:'对象：SlaveIndex、Active、Ready、FixedAddress、InfoSource、FixedAddressSource，以及可空的 VendorId、ProductCode、SerialNumber、Revision、Alias、InputBits、OutputBits（uint?）；离线时身份字段可为 null，型号不能唯一定位模块',
  ReadSlaveStatus:'对象：SlaveIndex、Active、Ready、FixedAddress、FixedAddressSource、State（byte?）、AlStatus（ushort?）、DlStatus（ushort?）；离线时状态字段可为 null，不用 0 假装在线状态',
  ReadDiagnostics:'对象：Raw（原始主从站诊断）、Updated、DcInternalSyncEstablished、DcExternalSyncEstablished、DcPropagationDelayInitialized',
  ReadLinkDiagnostics:'对象：FirstLinkDown、Faults（链路故障及拓扑）；拓扑 PhysicalPosition 不是 SlaveIndex',
  ReadLastError:'string：SDK 错误文本；不是自动重试或恢复操作',
  ConfigureAutoClear:'bool：读取回来的自动清错使能状态',
  ConfigureReadyLost:'uint：读取回来的就绪丢失超时（ms）',
  ReadAcquisitionStatus:'对象：Running、Sequence、AgeMs、QueueBacklog、LocallyValid、Error、WorkerStarts、'+acquisitionDiagnostics+'、HardwareSampleAgeMs=null；Sequence 是 long 软件序号，AgeMs 为主机读取完成后的年龄，不含此前 RX 排队时间',
  ReadPdo:scalar+'；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 '+timestamps,
  WaitNextPdo:scalar+'；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 '+timestamps+'；后续可引用 Sequence',
  ReadPdoBits:'ulong：按 LSB0 从低位起打包的无符号位域；BitLength=1 仍是 ulong（0/1）',
  ReadDigitalInput:'bool：指定从站输入字节内选定位的值',
  ReadAnalogInput:'double：工程值 = 原始标量 × Scale + Bias；不自动识别传感器量程',
  ReadExchangeStatus:'对象：Mode、PiEnabled、OutputRunning、InputQueue、OutputQueue、Isolated',
  StopCyclicOutput:'对象：Stopped、QueuedFramesWithdrawn；停止续发不撤回 SDK 队列中已有帧',
  ReadHistory:'对象：Dropped、DroppedMeaning="LocalHistoryEvictions"、HardwareDroppedFrames=null、OldestSequence、Samples；Dropped 仅为本地历史缓冲淘汰数。每个样本有 Sequence、ReceivedTimestamp、Data（完整主站输入 byte[]）、QueueBacklog、LocallyValid、HasQueueEvent，以及 '+timestamps,
  ReadSdo:scalar+'；Bytes 时返回实际收到的完整字节块',
  ReadWatchdog:'对象：Type、TimeoutMs、Mask、Data（byte[]）',
  ReadForceOutput:'对象：CommandType、Offset、Data；Data 为 SDK 原始字节，不能当作单一标量',
  ReadSlaveMemory:'byte[]：指定 ESC 地址范围的原始字节',
  ReadSlaveEeprom:'byte[]：指定 EEPROM 地址范围的原始数据；地址单位依 SDK/设备定义',
  ReadCapture:'byte[]：SDK 原始抓包数据块，不是解码后的 EtherCAT 帧列表或 PCAP 文件',
  ReadBoardDi:'uint：板载 DI 位图，位对应板载端口而非 EtherCAT 从站模块',
  ReadBoardDo:'uint：板载 DO 位图，位对应板载端口而非 EtherCAT 从站模块',
  ReadBoardPwm:'对象：Frequency（Hz）、DutyPercent（%）',
  ReadBoardPulse:'对象：Mode、TriggerMode、Direction、Frequency（Hz）、DutyPercent（%）',
  ReadBoardLatch:'ulong：指定锁存通道/样本的原始计数值',
  ReadAxisState:'MotionDriveState：Unknown、NotReady、SwitchOnDisabled、ReadyToSwitchOn、SwitchedOn、OperationEnabled、QuickStopActive、FaultReactionActive、Fault',
  ReadAxisMode:'sbyte：驱动实际模式（1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST）',
  ReadAxisFeedback:'对象：StatusWord（ushort）、Mode（sbyte?）、Position（int?）、Velocity（int?）、Torque（short?）、Sequence（long）、ReceivedTimestamp（单调时钟刻度）、TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；未映射反馈可为 null，SDO 多次读取后的主机时刻不表示原子采样',
  ReadAxisDiagnostics:'对象：StatusWord（ushort）、State（MotionDriveState）、Mode（sbyte）、ErrorCode（ushort，0x603F）、ErrorRegister（byte，0x1001）'
 };
 const returnsTrue = new Set(['StopBus','CheckEni','ConfigureReinit','EnablePi','DisablePi','StartAcquisition','StopAcquisition','WritePdo','WritePdoMasked','WriteDigitalOutput','WriteAnalogOutput','StartCyclicOutput','ResetChannel']);
 const prerequisites = {
  EnumerateBoards:[], Open:[], Close:['Open'], BeginExit:['Open'], ConfigureReinit:['Open'], StartBus:['ConfigureReinit'],
  ConfigureExchange:['StartBus'], RequestState:['StartBus'], EnablePi:['ConfigureExchange','RequestState'], DisablePi:['EnablePi'],
  StartAcquisition:['EnablePi'], StopAcquisition:['StartAcquisition'], ReadAcquisitionStatus:['Open'],
  ReadPdo:['StartAcquisition'], WaitNextPdo:['StartAcquisition'], ReadPdoBits:['StartAcquisition'], ReadDigitalInput:['StartAcquisition'], ReadAnalogInput:['StartAcquisition'], ReadHistory:['StartAcquisition'],
  WritePdo:['EnablePi'], WritePdoMasked:['EnablePi'], WriteDigitalOutput:['EnablePi'], WriteAnalogOutput:['EnablePi'],
  StartCyclicOutput:['EnablePi'], StopCyclicOutput:['StartCyclicOutput'], StopBus:['StartBus'],
  ReadEniInfo:['StartBus'],CheckEni:['StartBus'],ReadPiRange:['StartBus'],EnumerateSlaves:['StartBus'],ReadSlaveInfo:['StartBus'],ReadSlaveStatus:['StartBus'],ReadSdo:['StartBus'],WriteSdo:['StartBus'],
  ReadCapture:['StartCapture'],StopCapture:['StartCapture'],ReadBoardPwm:['Open'],StopBoardPwm:['StartBoardPwm'],
  OutputBoardPulse:['ConfigureBoardPulse'],StopBoardPulse:['ConfigureBoardPulse'],StartBoardLatch:['ConfigureBoardLatch'],ReadBoardLatch:['StartBoardLatch'],StopBoardLatch:['StartBoardLatch'],
  EnableAxis:['BindAxis','SetAxisMode'],ReadAxisDiagnostics:['BindAxis']
 };
 const mappings = {
  StatusWordOffset:['状态字偏移','0x6041:00','UInt16','2','状态位图'],
  ActualPositionOffset:['实际位置偏移','0x6064:00','Int32','4','驱动位置原始单位'],
  ActualVelocityOffset:['实际速度偏移','0x606C:00','Int32','4','驱动速度原始单位'],
  ActualTorqueOffset:['实际转矩偏移','0x6077:00','Int16','2','驱动转矩原始单位，按驱动手册换算'],
  ModeDisplayOffset:['实际模式偏移','0x6061:00','Int8','1','CiA402 模式代码'],
  ControlWordOffset:['控制字偏移','0x6040:00','UInt16','2','控制位图'],
  TargetPositionOffset:['目标位置偏移','0x607A:00','Int32','4','驱动位置原始单位'],
  TargetVelocityOffset:['目标速度偏移','0x60FF:00','Int32','4','驱动速度原始单位'],
  TargetTorqueOffset:['目标转矩偏移','0x6071:00','Int16','2','驱动转矩原始单位，按驱动手册换算'],
  ModeCommandOffset:['设定模式偏移','0x6060:00','Int8','1','CiA402 模式代码']
 };
 for(const s of specs){
  s.displayGroup=displayGroups[s.group]??s.group;
  s.prerequisites=prerequisites[s.op]??[s.group.startsWith('Motion')&&s.op!=='BindAxis'?'BindAxis':'Open'];
  if(s.op.includes('Pool'))s.prerequisites=[s.op.endsWith('Create')?'StartAcquisition':s.op.startsWith('Background')?'BackgroundPoolCreate':'PeriodicPoolCreate'];
  const motion=s.group.startsWith('Motion');
  s.result=results[s.op]??(motion?'MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为':s.op.endsWith('PoolStatus')?'对象：WorkerStarts、InputAgeMs、AcquisitionRunning、Isolated、'+acquisitionDiagnostics+'、Items；每项含 Name、Enabled、Publications、LastSequence、Error、SkippedSamples；InputAgeMs 不含此前 RX 排队时间':returnsTrue.has(s.op)||s.op.includes('Pool')?'bool：true 表示本次配置或软件操作完成，不证明物理动作完成':nullResult);
  s.summaryFields=s.fields.filter(f=>['ConnectionName','BoardSelector','Channel','AxisName','SlaveIndex','PoolName','ItemName','Index','SubIndex','Offset','Port','DataType','State','Value','Position','Velocity','Torque','AfterSequence','ResultVariable','DataVariable'].includes(f.name)).map(f=>f.name);
  for(const f of s.fields){
   if(choices[f.type]){f.choices=choices[f.type].map(([value,label])=>({value,label:value+' — '+label}));f.info='可选值：'+f.choices.map(x=>x.label).join('；')+(f.type==='PdoDataType'?'。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。':'');if(f.type==='PdoDataType'&&s.group!=='SDO')f.info+=' ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。';}
   if(f.name==='ConnectionName')f.info=s.op==='Open'?'本插件连接会话名称表达式；用于后续步骤选择同一板卡通道，不是板卡序列号或从站编号':'选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名';
   if(f.name==='AcquisitionThreadPriority')f.info+=' 作用于本通道整个共享 SDK 线程，包括输入采集、SDO 和输出，不新增 RX 线程。StartAcquisition 应用并读回确认；StopAcquisition 恢复原优先级。更改须先停止再启动；优先级不能保证 Windows 实时调度。';
   if(f.name==='RxHighWatermarkPercent')f.info='Queue 模式且 RX 容量已知时，按容量百分比计算高水位；容量未知时不进行高水位/满判定。只描述 SDK 最后观测的深度，队列满仅提示溢出疑似，不报告硬件丢帧数。';
   if(f.name==='RxBacklogWarningMs')f.info='Queue 模式下 RX 深度持续非零的积压告警时间阈值，以主机观测为依据；容量未知时仍监测持续积压。SDK 调用阻塞时观测及告警会延迟，不是独立实时监控线程。';
   if(f.name==='RxAlarmRepeatMs')f.info='本通道相邻 RX 通知（含恢复）的最小间隔；使用已有后台工作循环通知，不为告警或读取项新增线程。';
   if(f.name==='LatestOnly')f.info='主动清理旧队列数据以优先获得较新输入；这是用户选择的软件丢弃策略，不等于硬件丢帧。监控在清旧前记录最后观测，不能证明所有总线帧均被读取。';
   if(f.name==='MaxAgeMs'||f.name==='MaxFeedbackAgeMs')f.info='从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。';
   if(f.name==='SlaveIndex'&&!motion){f.label='从站 ENI 索引（常量）';f.info='本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。';}
   if(f.name==='SlaveIndex'&&motion){f.label='从站 ENI 索引表达式';f.info='求值为 int，本连接 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址或别名。范围 0～65534，先用 EnumerateSlaves 核对。';}
   if(f.name==='SlaveIndexExpression')f.label='从站 ENI 索引表达式（可选）';
   if(f.name==='AxisName')f.info='本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号';
   if(f.name==='Offset'){const direction=s.op.startsWith('Write')||s.op.includes('Force')?'输出':'输入';f.label='从站'+direction+'区相对字节偏移';f.info='相对指定从站'+direction+'映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中'+direction+' Offset+此值。';}
   if(f.name==='Length'&&s.fields.some(x=>x.name==='DataType'))f.info='Bytes 时为原始字节数；标量必须为所选类型的固定宽度（1/2/4/8 字节），范围不得超出当前从站输入区';
   if(f.name==='DataVariable')f.info='必填的已声明变量完整路径，如 Locals.Module2Input；写入按 DataType 解码后的值（'+scalar+'），不是样本包。只写该变量，不创建派生路径。';
   if(f.name==='ResultVariable')f.info='可选已声明变量完整路径；'+s.result+'。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。';
   if(f.name==='ReturnSample')f.info='false 保持旧行为仅返回解码值；true 返回 Value 与 Sequence 等样本元数据，结果变量应能接收对象，可将其 Sequence 用于下一次 AfterSequenceExpression';
   if(f.name==='AfterSequence')f.info='long 软件样本序号的兼容常量输入（此字段保留 int 范围）；仅返回序号大于它的样本，AfterSequenceExpression 非空时优先；不使用设备周期号';
   if(f.name==='BitOffset')f.info='LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定';
   if(f.name==='BitLength')f.info='可跨字节；从 BitOffset 起读取 1～64 位并返回 ulong。符号扩展和工程单位换算由后续逻辑显式处理';
   if(f.name==='Index'){f.label='CoE 对象索引（常量）';f.info='当前从站对象字典索引，范围 0～65535；支持 0x6040 或十进制 24640。不能填 PDO 字节偏移或从站编号。';}
   if(f.name==='SubIndex')f.info='同一对象内的子索引，0～255；不是从站索引。CompleteAccess=true 时仅允许 0 或 1，具体起点由设备对象字典确定';
   if(f.name==='CompleteAccess')f.info='false 只访问一个 Index:SubIndex；true 是同一对象内多个子索引的整体访问，不是读取所有从站，当前仅支持 DataType=Bytes；需设备支持';
   if(f.name==='Capacity')f.info=s.op==='ReadSdo'?'最大接收字节容量，可以大于标量宽度；标量模式容量不得小于类型宽度，并要求实际返回长度与类型一致；CompleteAccess 应选 Bytes 并提供完整块容量':'SDK 缓冲/接收容量，不是已捕获的帧数；读取结果保留原始抓包格式';
   if(f.name==='Address')f.info=s.op.includes('Eeprom')?'原样传给 SDK u16StartAdr 的 EEPROM 地址；当前 SDK 头未注明地址计数单位，须按厂商接口说明确认，不能套用 PDO 字节偏移':'ESC 寄存器起始字节地址，支持十进制/0x；不是 PDO 映像偏移或 CoE 对象索引';
   if(f.name==='Length'&&(s.op.includes('Memory')||s.op.includes('Eeprom')))f.info='本次读取的字节数，SDK DataLen 以字节计；不要把它当作地址单位或寄存器个数';
   if(f.name==='Scale')f.info='有限且非零。读取：工程值=原始值×Scale+Bias；写入：原始值=(工程值-Bias)/Scale。单位由设备量程和用户约定，不自动识别';
   if(f.name==='Bias')f.info='工程单位零点偏移；读取在缩放后相加，写入在反缩放前相减；不是 PDO 字节偏移';
   if(f.name==='Value'&&s.fields.some(x=>x.name==='ValueExpression'))f.info='兼容常量；ValueExpression 非空时使用表达式结果。'+(s.op==='WriteAnalogOutput'?'工程值按 Scale/Bias 反换算成指定数值类型；请核对整数舍入和量程。':s.op==='WriteDigitalOutput'?'false 清除选定位，true 置位；不覆盖同字节其他位。':'板载 DO 整体位图，非 EtherCAT 从站输出；会更新整个位图。');
   if(f.name==='QueuePrefill')f.info='仅 ExchangeMode=Queue 生效，填入完整初始输出映像的队列帧数；受真实 SDK 队列容量限制，不是毫秒';
   if(f.name==='InitialOutput')f.info='整个主站输出映像的 byte[] 初值，长度必须等于 ReadEniInfo.OutputBytes；不是单个从站输出，不能假定全零安全';
   if(f.name==='InputByteOffset'||f.name==='OutputByteOffset'){const input=f.name.startsWith('Input');f.label=input?'轴输入区全局字节起点':'轴输出区全局字节起点';f.info='整个主站'+(input?'输入':'输出')+'映像内的绝对字节起点，按 ReadPiRange 返回值配置；后续对象偏移同为绝对值';}
   if(f.name==='InputByteLength'||f.name==='OutputByteLength'){f.label=f.name.startsWith('Input')?'轴输入区字节长度':'轴输出区字节长度';f.info='0 表示不配置该方向 PDO，所有对应对象偏移应为 -1；非 0 必须在指定从站映像范围内。CSP/CSV/CST 需要实际 PDO 映射';}
   if(mappings[f.name]){const [label,index,type,width,unit]=mappings[f.name];f.label=label+'（'+type+' / '+width+' 字节）';f.info=index+' 固定 '+type+'，'+width+' 字节，小端；'+unit+'。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 '+index+'，仍需用户按驱动字典核对。';}
   if(f.name==='Mode'){f.choices=[[1,'PP 轮廓位置'],[3,'PV 轮廓速度'],[4,'PT 轮廓转矩'],[6,'HM 回零'],[8,'CSP 周期同步位置'],[9,'CSV 周期同步速度'],[10,'CST 周期同步转矩']].map(([value,label])=>({value,label:value+' — '+label}));f.info='必须包含在该轴 AllowedModes 白名单中；先设置再读取确认。类型代码不是 PDO 数据类型。';}
   if(f.name==='PulseMode'){f.choices=[[0,'AB 相'],[1,'正/逆脉冲序列'],[2,'脉冲+符号，servo_sign=!Direction'],[3,'脉冲+符号，servo_sign=Direction']].map(([value,label])=>({value,label:value+' — '+label}));f.info='按厂商端口接线选择；方向的符号关系随模式变化，不自动推断电机正方向';}
   if(f.name==='LatchMode'){f.choices=[
    [1,'单次 Z上升→A'],[2,'单次 Z下降→A'],[3,'单次 R0上升→A'],[4,'单次 R0下降→A'],
    [6,'单次 R0上升→A + Z上升→B'],[7,'单次 R0上升→A + Z下降→B'],[8,'单次 R0下降→A + Z上升→B'],[9,'单次 R0下降→A + Z下降→B'],
    [10,'单次 R0上升→A + R1上升→B'],[11,'单次 R0上升→A + R1下降→B'],[12,'单次 R0下降→A + R1上升→B'],[13,'单次 R0下降→A + R1下降→B'],
    [14,'单次 R1上升→B'],[15,'单次 R1下降→B'],[16,'单次 Z上升→B'],[17,'单次 Z下降→B'],[18,'单次 R2上升→C'],[19,'单次 R2下降→C'],[20,'单次 R3上升→D'],[21,'单次 R3下降→D'],
    [101,'连续 Z上升→A'],[102,'连续 Z下降→A'],[103,'连续 R0上升→A'],[104,'连续 R0下降→A'],[114,'连续 R1上升→B'],[115,'连续 R1下降→B'],[116,'连续 Z上升→B'],[117,'连续 Z下降→B'],
    [118,'连续 R2上升→C'],[119,'连续 R2下降→C'],[120,'连续 R3上升→D'],[121,'连续 R3下降→D'],[123,'连续 R0上升→B'],[124,'连续 R0下降→B'],[133,'连续 R0先上后下→A'],[134,'连续 R0先下后上→A'],[135,'连续 R1先上后下→B'],[136,'连续 R1先下后上→B']
   ].map(([value,label])=>({value,label:value+' — '+label}));f.info='必须与 Continuous 一致；列表中的单次/连续是不同 SDK 模式，配置不匹配会被拒绝';}
   if(f.name==='AllowedModes')f.info='逗号分隔且不重复：1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST。仅勾选/填写驱动确实支持的模式；不配置不会自动推断';
   if(['ProfileVelocity','Acceleration','Deceleration','SearchVelocity','ZeroVelocity'].includes(f.name))f.info='求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm';
   if(f.name==='Method')f.info='求值为 sbyte，必须为非 0 的驱动回零方法代码；不是速度/位置单位，按驱动支持的方法选择，默认 0 不可执行';
   if(f.name==='VelocityTolerance')f.info='求值为 int，必须大于等于 0；停止确认时允许的实际速度绝对值，单位与驱动实际速度相同';
   if(f.name==='LocalPath')f.info='本地文件路径表达式；读取设备文件时为保存目标，写入设备文件时为已有源文件，不是设备远端路径';
   if(f.name==='RemotePath')f.info='设备侧文件名/路径表达式，格式与权限由厂商协议/固件定义；不是本地文件路径';
   if(motion&&f.name==='SlaveIndex')Object.assign(f,{dynamicRange:true,min:0,max:65534});
   if(motion&&['ProfileVelocity','Acceleration','Deceleration','SearchVelocity','ZeroVelocity'].includes(f.name))Object.assign(f,{dynamicRange:true,min:1});
   if(motion&&f.name==='Method')Object.assign(f,{dynamicRange:true,nonZero:true});
   if(motion&&f.name==='VelocityTolerance')Object.assign(f,{dynamicRange:true,min:0});
   if(s.op==='ReadBoardLatch'&&f.name==='SampleIndex')f.info='索引必须小于本连接启动锁存时配置的记录容量。连续模式读取指定硬件缓冲槽；触发后可读取，但不保证该槽本次已更新，不证明新样本';
   if(s.op==='StartBoardLatch'&&f.name==='Count')f.info='仅连续锁存使用的记录容量（字节数无关），0 表示触发但不记录可读样本；单次锁存仅有一个可读槽，读取 SampleIndex 应为 0';
  }
  s.scope=motion?'目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。':s.fields.some(f=>f.name==='SlaveIndex')?'目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。':s.group==='Board IO'?'目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。':s.op.includes('Pool')?'目标为指定连接、读取池类型及 PoolName；ItemName 只在该命名池内唯一。':'目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。';
  if(s.op==='EnumerateBoards')s.scope='驱动级只读枚举，不需要 ConnectionName，不打开通道。';
  if(s.group==='Board IO')s.scope+=' 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。';
  if(s.group==='Watchdog')s.scope+=' HPCIe-2E 官方能力表与通用 SDK 接口存在支持差异；本版本对 HPCIe-2E 系列拒绝全部五项看门狗操作，不能据此宣称已有硬件保护。';
  s.help=s.scope+' 返回：'+s.result+'。';
  if(s.op==='MovePosition')s.behavior+=' 本步骤等待本次目标握手及驱动目标到达；轮廓速度、加速度和减速度必须大于 0。';
  if(s.op==='RunVelocity'||s.op==='RunTorque')s.behavior+=' 目标提交后返回，持续运动必须显式执行 StopMotion；不等待速度/转矩归零。';
  if(s.op==='HomeAxis')s.behavior+=' 回零方法非 0，搜索速度、零位速度和加速度均大于 0；等待本次回零完成。';
  if(s.op.startsWith('WriteCs'))s.behavior+=' 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。';
  if(s.op==='StopAcquisition')s.behavior+=' 必须先关闭该连接两类所有命名读取池。';
  if(['FoeRead','FoeWrite','ReadSlaveMemory','WriteSlaveMemory','ReadSlaveEeprom','WriteSlaveEeprom'].includes(s.op))s.behavior+=' 要求总线已经启动且处于 PREOP 或 BOOT、过程映像交换禁用；不等于 StopBus 后使用。';
  if(s.op==='WaitNextPdo')s.behavior+=' 仅 Queue 模式具有新输入帧证据；循环使用应启用 ReturnSample 并把返回 Sequence 传入下一次 AfterSequenceExpression。';
  if(s.op==='ReadBoardLatch')s.behavior='仅在锁存已触发且 Continuous 与本连接配置匹配时读取。SampleIndex 必须小于启动时配置的容量；SDK 不提供逐样本已写入计数，返回不证明此槽在本次触发中已更新。';
  if(s.op==='StartBoardLatch')s.behavior+=' 启动前清除旧触发状态，不清编码器计数值；需要复位计数使用独立 ResetBoardLatchCounter。';
  if(['ReadSdo','WriteSdo','FoeRead','FoeWrite','ReadSlaveMemory','ReadSlaveEeprom','ReadCapture'].includes(s.op))s.behavior+=' Queue 周期输出运行期间拒绝本操作，避免长邮箱/维护调用阻塞唯一 SDK 线程；需先完成停轴、去使能、解绑并停止周期输出。';
  if(['WriteSdo','ConfigureAutoClear','ConfigureReadyLost','ConfigureWatchdog','ConfigureForceOutput','DisableForceOutput','WriteBoardDo','ConfigureBoardPulse','ConfigureBoardLatch','FoeWrite','WriteSlaveMemory','WriteSlaveEeprom','ResetBoard','BoardUpdate','BoardFileWrite'].includes(s.op))s.behavior+=' 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。';
  if(['StartBus','StopBus','ResetChannel','ConfigureReinit','ConfigureExchange','FoeWrite','WriteSlaveMemory','WriteSlaveEeprom','ResetBoard','BoardUpdate','BoardFileWrite'].includes(s.op))s.behavior+=' 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。';
  if(['StopCyclicOutput','DisablePi'].includes(s.op))s.behavior+=' 本操作要求所有轴已停止、去使能并解绑；只执行 StopMotion 而保留绑定仍不能执行本步骤。';
  if(s.op==='DisablePi')s.behavior+=' 还需先停止采集及周期输出。';
  if(s.op==='RequestState')s.behavior+=' 请求非 OP 状态前必须停止、去使能并解绑所有轴；需要保持周期读取时不要在采集中切换离开 OP。';
  if(['ResetChannel','ConfigureReinit','ResetBoard','BoardFileRead','BoardFileWrite','BoardUpdate'].includes(s.op))s.behavior+=' 主站总线必须已停止。';
  if(['ResetBoard','BoardFileRead','BoardFileWrite','BoardUpdate'].includes(s.op))s.behavior+=' 整块板卡需由本通道独占维护；其他打开通道会阻止本操作。';
  if(['StartBoardPwm','ConfigureBoardPulse','OutputBoardPulse','StopBoardPwm','StopBoardPulse','ConfigureBoardLatch','StartBoardLatch','ReadBoardLatch','StopBoardLatch','ResetBoardLatchCounter'].includes(s.op))s.behavior+=' 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。';
  if(['ConfigureBoardLatch','StartBoardLatch','ResetBoardLatchCounter'].includes(s.op))s.behavior+=' 锁存运行中或停止结果未确认时，必须先成功执行 StopBoardLatch。';
  if(s.group==='Background Reading')s.behavior+=' 最新帧覆盖缓存可能合并中间帧；SkippedSamples 是读取项跳过的软件序号数，不是硬件丢帧统计。';
  if(s.op==='Close')s.behavior+=' 正常关闭前必须停止、去使能并解绑全部轴，并显式停止本连接启动的板载 PWM/脉冲输出；通道隔离后的清理仍不证明物理停止。';
  if(s.op==='StartAcquisition')s.behavior+=' 要求 ENI 包含非空输入映像；当前主站必须保持 OP，RequireDc=true 时 DC 同步必须有效。优先级应用于本通道共享 SDK 线程，包含 SDO/输出访问，应用后读取实际值确认；不新增 RX 线程。采集配置改变须先停止再启动，成功启动才重置 RX 诊断。';
  if(s.op==='StopAcquisition')s.behavior+=' 停止后恢复共享 SDK 线程原优先级；保留 RX 诊断证据并标 Monitoring=false，不把停止采集当作队列异常已经恢复。';
  if(s.op==='ReadAcquisitionStatus'||s.op.endsWith('PoolStatus'))s.behavior+=' RxQueue 为最后一次观测值，须同时查看 ObservationAgeMs 与 MaxPollGapMs；SDK 阻塞时监控也会延迟。LastObservationTimestamp/LastRecoveryTimestamp 为主机队列观测/恢复时刻的单调时钟刻度；HighWatermarkEvents/BacklogWarningEvents 计告警事件段，FullQueueObservations 计满深度观测次数。高水位/持续积压状态可随观测变化，OverflowSuspected 为疑似事件锁存，HardwareDroppedFrames=null。Queue 容量未知时仍记录深度/持续积压，但禁用高水位和满判定；非 Queue 模式禁用队列告警。告警由既有后台工作循环发送，不在 SDK 工作线程中调用宿主日志；重复消息受 RxAlarmRepeatMs 节流并可合并，NotificationError 显示通知失败。';
  if(s.op==='ReadAcquisitionStatus')s.behavior+=' 本操作只读缓存，不调用 SDK；SDK 阻塞或通道隔离时仍可查看最后观测和错误，直到连接关闭。';
  if(s.op==='ReadHistory')s.behavior+=' Dropped 仅统计本地历史缓冲淘汰，SkippedSamples 仅统计读取项跳过的软件序号；二者都不是硬件丢帧计数。';
  if(s.op==='ReadAxisFeedback')s.behavior+=' PDO 时间来自对应快照的主机读取完成时刻；SDO 时间在多次对象读取完成后记录，不保证各字段来自同一采样时刻。';
  if(['EnablePi','StartCyclicOutput','WritePdo','WritePdoMasked','WriteDigitalOutput','WriteAnalogOutput'].includes(s.op))s.behavior+=' 执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。';
  if(s.op==='StartCyclicOutput')s.behavior+=' ENI 输出映像须非空，CommandLeaseMs 必须大于 IntervalMs；目标续期是 Windows 毫秒级服务，不是 ENI 的微秒总线周期。';
  if(['WritePdo','WritePdoMasked','WriteDigitalOutput','WriteAnalogOutput'].includes(s.op))s.behavior+=' 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。';
  if(motion){
   s.behavior+=' PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。';
   if(s.op==='BindAxis')s.behavior+=' 同一连接内 AxisName 与 SlaveIndex 均须未被其他轴占用；当前实现一个从站只绑定一个轴。';
   if(s.op==='SetAxisMode')s.behavior+=' 模式必须在 AllowedModes 中；切换前须停轴、去使能，故障状态不可切换。';
   if(s.op==='EnableAxis')s.behavior+=' 实际模式必须在 AllowedModes 中；QuickStopActive 状态须先明确去使能，再重新使能。';
   if(s.op==='UnbindAxis')s.behavior+=' 本轴执行过控制且尚未去使能时拒绝解绑；须先完成停止和去使能。';
   const requiredMode={MovePosition:'1 / PP',RunVelocity:'3 / PV',RunTorque:'4 / PT',HomeAxis:'6 / HM',WriteCspTarget:'8 / CSP',WriteCsvTarget:'9 / CSV',WriteCstTarget:'10 / CST'}[s.op];
   if(requiredMode)s.behavior+=' 要求轴已运行使能，且实际模式为 '+requiredMode+'；该模式必须包含在 AllowedModes 白名单中。';
   if(s.op==='ReadAxisDiagnostics')s.behavior+=' 报警对象 0x603F/0x1001 需要 SDO，须在 Queue 周期输出停止后读取。';
  }
  const allowedDuringExit=s.op==='BeginExit'||s.op==='Close'||s.op.startsWith('Read')||s.op.startsWith('Stop')||s.op.startsWith('Disable')||['UnbindAxis','WaitNextPdo','EnumerateSlaves'].includes(s.op)||/Pool(StopItem|Close|Status)$/.test(s.op);
  if(s.needsConnection&&s.op!=='Open'&&!allowedDuringExit)s.behavior+=s.op==='RequestState'?' BeginExit 后只允许请求非 OP 状态；不能重新请求 OP。':' BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。';
  s.help=s.scope+'\n前置步骤：'+(s.prerequisites.length?s.prerequisites.map(p=>'ZlgEtherCat.'+p).join('、'):'无')+'。\n'+s.behavior+'\n返回：'+s.result+'。';
 }
};
