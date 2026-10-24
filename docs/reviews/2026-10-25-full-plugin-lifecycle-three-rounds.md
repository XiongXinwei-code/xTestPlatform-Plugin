# 全插件手册与运行生命周期三轮勘误验收

## 当前状态与范围

本轮确认的缺陷已修复并完成三轮软件勘误、跨作者复审及本地可用目录编译；未发现新的确定阻断。真实设备和完整平台调试窗口流程仍未验证，不能据此认定全部插件绝无潜在问题。

- L3。10套插件/120步骤，含设置、执行器、资源/后台任务、Description、VM、实际页面和编辑校验。保留原工作区改动；未重置仓库、未提交/推送、未覆盖已部署平台、未操作真实硬件。
- [开发手册](https://github.com/XiongXinwei-code/xTestPlatform-Plugin/blob/master/xTestPlatform_StepPlugin_Development_Guide.md)完整读取后按最新远端复核，blob仍为`9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`，与本地一致（3.4.0 / SDK1.2.8补充）。本轮不自动拉取并覆盖大量未提交工作。
- [分阶段计划](../plans/2026-10-24-full-plugin-debug-lifecycle-three-rounds.md)；原始审查、实际RED/GREEN及独立报告保存在`artifacts/full-plugin-audit-20261024/`。前一轮`remediation-ao`仅为历史120步骤身份基线，不复用其旧测试结果作为当前通过。

## 本轮主要修复

| 范围 | 已实现的生命周期/手册修复 |
| --- | --- |
| Shared / 全10模块 | 私有ExecutionEpoch在首await前捕获SDK执行资源代次；原caller、精确owner与实际任务退出共同管理准入/发布。旧步骤在求值、登记等待、日志或native晚完成后不得消费新运行同名连接。必要关闭/厂商释放/AO final不被普通过期围栏阻挡。 |
| CAN / CANopen / UDS / XCP | 真实旧物理owner释放后首重开；raw和整事务单调期限返回边界；帧、null、位宽与枚举拒绝；服务/周期/收尾继续共享接收路由及严格确认，PartialCycle和未知发送不清表；同星初始化/handle归属、Kvaser数据段显式采样点及页面/校验/卸载保存修复。 |
| CiA402 | 轴步骤首次设置访问、lookup、操作及输出的原执行代次/精确轴保护；Bind末尾取消释放本次新轴；通信/步骤单调期限；临时动作预算不绑定持久axis，专用清理不关闭父CAN。被动接收恢复仍要求真正退出。 |
| LIN | WriteRead共享事务；NI原生建立/Wakeup多阶段提交预算和取消；旧Close/周期Stop不操作新代次；null/枚举、表达式、选中行与说明同步。 |
| NiDaq | 配置等待退休owner、Start/Stop捕获后再查代次、DI/DO临时物理任务登记SDK执行表；旧native退出前下一同端口任务不能配置。AO等writer真正退出后才final/Stop/Dispose，未知终值禁盲重发；UI当前编辑/选中行/Sync字段修复。 |
| OpcUa | 精确Session和采集owner；旧Connect失败/Disconnect/AcqStop不删停新运行；结果/日志/CSV晚完成不发布。Delete/SessionClose专用清理退休后仍可执行，保持序列控制重连；批写真实类型、ColumnName可选及保存修复。 |
| Modbus / Serial / VISA | 实际提交与外层读写/查询/波形/批量结果围栏、同表首重开、迟到Open只清自身；null、枚举、SlaveAddress位宽、文化无关数字；真实DataGrid编辑Flush/变量类型/表达式校验。 |
| Ethernet / Http | TCP/DoIP旧Close不误关新pool；DNS/提交/响应与结果原代次；UDP数据报精确长度、SOME/IP枚举。HTTP等待实际请求和异步取消回调退出再释放，迟到构建清自身；XML整组解析后发布、标准表达式Header与Clear订阅修复。 |
| Flash编辑辅助 | 真实Roslyn路径字面量、仅解码literal的编辑分析、CPU parser离开Dispatcher、旧分析不跨步骤/输入发布；末地址不回绕；损坏预设不被空表覆盖、IO错误可见、同目录替换和UTF8。 |

核心文件包括`Shared/Runtime/ExecutionEpoch.cs`、`PendingIoGate.cs`、`RetiringResources.cs`；各插件的Guard/owner和executor；CAN的ProtocolTransaction/原生适配器、CiA的ExecutorBase/CoordinatedAxisTransport、DAQ的DaqTaskResource/Factory、OPC的ConnectionResource/采集Stop、HTTP的HttpClientResource、Ethernet的NamedConnectionPool，以及对应UI/校验。完整最终源指纹以`final-source.json`为准，不把原有用户变更全部冒认为本轮独立新增。

## 三轮勘误与独立审查

1. 第一轮已闭环：按手册逐步骤读实际调用链及页面，跨作者审查静态/配置/序列化/变量/错误边界，不以字段正则替代语义。已读范围和最终问题关闭分别见`round1-root-review.md`、`round1-nidaq-opc-review.md`、`round1-can-cia-lin-review.md`、`round1-flash-xcp-review.md`等；CAN分工55步与主审补5步覆盖齐全，没有用早期阶段报告冒充最终阅读。
2. 第二轮已闭环：真实SDK1.2.8 ResourceRegistry的CleanupExecution/CleanupAll、同表首重跑/新调试资源表、已进入的native不协作、旧lookup拿新owner、迟到求值/日志、AO final和独立关闭。使用受控屏障/localhost/WPF组件；不是自制资源表或真实平台调试窗口。实际RED保留；Root旧Close误关新pool、NI临时数字owner/四路lookup和CAN/CiA/LIN追加期限/发布等由非作者复审关闭。
3. 第三轮已闭环：冻结源码九套全回归、20core/UI重建、严格装载120身份、私有CAN/Windows资产/文档/版本/UTF8与PE/PDB源码核对。重编译时的既存CS8601又实证CANopen缺值结果为null：三状态实际RED后最小归一为空字符串，补文化/错误上下文正向用例；4/4 GREEN并独立复审。保留此前1225全套回执/日志，仅重跑受影响CAN360和CiA234，未改代码的七套不重复。最终构建无该警告。

独立映射：can_review实现CAN/CiA/LIN、审Shared/网络/HTTP与Modbus/Serial/VISA；audit_io实现NI/OPC、审CAN/CiA/LIN；audit_opc_visa实现Modbus/Serial/VISA、审NI/OPC及Root第二轮；主线程补读Flash/XCP，后写Flash UI由audit_opc_visa另行独立审。没有将作者自测称为独立审查。

## 最终验证与交付

可用输出：`artifacts/Release/2026-10-24-full-lifecycle/Plugins`，10独立模块、543文件，共432510107字节。仅写本仓库artifacts，没有部署/启动平台；整个模块文件夹含匹配核心/UI/依赖、README和CHANGELOG。

| 测试项目（最终verification中的TRX） | 通过/总数 | 失败/跳过 |
| --- | --- | --- |
| CanPlugin.Tests | 360/360 | 0/0 |
| Cia402Plugin.Tests | 234/234 | 0/0 |
| LinPlugin.Tests | 72/72 | 0/0 |
| ModbusPlugin.Tests | 73/73 | 0/0 |
| NiDaqPlugin.Tests | 89/89 | 0/0 |
| OpcUaPlugin.Tests | 130/130 | 0/0 |
| PluginRegression.Tests（Shared/Ethernet/Http） | 166/166 | 0/0 |
| SerialPortPlugin.Tests | 42/42 | 0/0 |
| VisaPlugin.Tests | 63/63 | 0/0 |
| 合计 | 1229/1229 | 0/0 |

版本：CAN1.0.36、CiA4021.0.8、LIN/SerialPort1.0.30，Ethernet/Http/Modbus/NiDaq/OpcUa/VISA1.0.22；全部core/UI一致，沿既有1.0补丁序列递增。保留全部120个StepTypeId和显示路径，没有本轮新改树/新增步骤。

实际命令及回执（相对仓库根）：

```text
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-24-full-lifecycle
dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj -c Release --no-restore -m:1 -p:OutputPath=<交付verification/test-bin/CanPlugin.Tests> --logger trx;LogFileName=CanPlugin.Tests.trx --results-directory <verification> --blame-hang-timeout 60s
dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-restore -m:1 -p:OutputPath=<交付verification/test-bin/Cia402Plugin.Tests> --logger trx;LogFileName=Cia402Plugin.Tests.trx --results-directory <verification> --blame-hang-timeout 60s
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Package -OutputRoot artifacts/Release/2026-10-24-full-lifecycle
pwsh -NoProfile -File tools/Verify-PluginBinarySources.ps1 -OutputRoot artifacts/Release/2026-10-24-full-lifecycle
pwsh -NoProfile -File tools/Verify-PluginDelivery.ps1 -OutputRoot artifacts/Release/2026-10-24-full-lifecycle -BaselineRoot artifacts/Release/2026-10-24-remediation-ao
pwsh -NoProfile -File tools/Verify-PluginSources.ps1
git diff --check
pwsh -NoProfile -File artifacts/full-plugin-audit-20261024/Capture-SourceSnapshot.ps1 -Name final
```

- 九套TRX均实际执行；十模块的20核心/UI Release/win-x64 Rebuild成功，0警告/0错误；严格加载步骤与Editor描述、图标和依赖成功；包内无测试运行器。
- `delivery-contract-receipt.json`：120唯一ID/显示路径与旧120元数据逐项一致；所有包文件清单哈希与当前README/CHANGELOG一致，两份CAN私有副本SHA一致。模块版本在build-manifest记录，逐程序集版本在source receipt；System.IO.Ports的Windows资产由Verify-Plugins Package阶段独立校验。
- `delivery-source-receipt.json`：30个PE/PDB配对、1350次非生成C#源文档SHA与当前文件一致，含九套测试源码；不涵盖真实宿主加载或XAML运行资格。
- `Verify-PluginSources`最终1316文本/120独立Editor，UTF8/no C# BOM/no替换字符/执行状态检查通过；`git diff --check`通过。不是把正则检测当完整语义证明。
- `final-source.json`：1216个C#/XAML/csproj指纹，SHA256 `29419A040A9C1FBEBC634CD84EDFD9D671710E5026ABB57084AB8B2801A3E4C5`。初始1121快照并非干净Git基线，已有未提交修改仍保留。
- CAN DLL：Assembly/FileVersion均`1.0.36.0`，MVID `d7682cb2-ad62-48a4-99a2-6a50f0f82b9e`，SHA256 `72cc4da8d23d9258aaf37938212abb43721c3200c5b7aeb456a529e2dd1ce655`。其余逐DLL信息在source receipt中，可用于比对现场实际装载，不凭源码行号猜版本。

完整日志为交付根目录的`third-round-*.log`。中间失败与无效探针、并行夹具饥饿、CiA双包装/并发Close契约修正均在阶段报告和原TRX保留；未放宽产品期限或静默忽略Error。第三轮补修前回执另存`artifacts/full-plugin-audit-20261024/third-round-before-result-fix/`，没有用新结果抹掉旧证据。

第三轮非作者只读复核已关闭：`artifacts/full-plugin-audit-20261024/round3-delivery-independent-review.md`。审查者另行执行纯文件校验脚本，重算543包文件、1216源码/快照、9TRX的逐条1229条Passed、120旧身份、20项目版本、私有CAN/Windows资产及原1225证据备份，exit0且failures=[]；一处交付说明字段归属已修正文案并回读。审查者没有重建/重跑测试/操作设备，PE/PDB元数据依据已读工具和主线程实际回执，独立文件复算与主线程执行证据分别记录。

## 能力检查与剩余边界

- ECC planner/architect：执行层与长生命周期owner分离、物理退休、逐代准入、真实退出、兼容和回退已在计划记录；内部同构建类型原子演进，不增接口框架。
- tdd-workflow/csharp-testing：核心状态与异常路径先实际RED；初次缺using、无效屏障、旧计数假设或测试并行饥饿另行记录，不把编译失败算功能RED，不以增大产品期限换绿。
- C# reviewer/silent-failure-hunter：跨作者读源码/调用链/实际TRX，异常保全、不吞失败、不同原生阶段与取消/超时分类；最终闭环以各报告末节和最终源码为准。
- security-review：检查用户输入位宽/枚举/路径/变量、HTTP凭据跨源、编辑分析不执行任意代码、辅助文件错误保留；不宣称全依赖漏洞审计。固件全量读入无新文件大小配额，预设锁不是跨进程事务，替换不承诺断电持久化。
- windows-desktop-e2e方法：已跑实际WPF组件/Dispatcher/页面绑定、编辑保存与布局；没有安全可控的完整宿主调试环境，完整调试窗口退出→重新进入→真实运行仍未执行，不能以构建或替身替代。
- verification-loop：上列最终命令、九TRX、20构建、严格装载、PE/PDB及清单/版本/源码均通过，证据对应最后产品文件。self-evaluation五轴实际评分见`artifacts/full-plugin-audit-20261024/self-evaluation.md`：3.8/5，完整性3/5的具体缺口是完整宿主/硬件未验，不能在现有条件下安全快速补齐；不以软件数量或平均分宣布现场通过。覆盖率未测量。

本机NI-XNET/DAQmx已安装，但没有执行真实CAN/LIN、NI采集/输出、OPC服务器、PLC/仪器或电机动作。真实总线顺序/周期、BusOff、时间戳误差、驱动取消/Stop/Clear、电气终值与物理停稳需单独现场资格。测试中的ACK或发送任务停止不等于电机停稳。

中断后正常重跑的保证以旧调用真实释放、配置预算和SDK生命周期契约成立为前提：原生永久不退、未知发送或未知AO终值必须明确报错并保留隔离，不承诺强行立即重开。HTTP取消不撤回服务器已经执行的事务；任意第三方表达式/回调无法被插件强杀。旧结果失效不代表原工作物理撤销。

回退应停止平台并备份后整目录成套替换匹配版本的核心/UI/依赖（CiA私有CAN须与CAN一致），不能混用DLL或清静态占用表绕过安全保护；软件回退不撤销已发硬件动作或输出。
