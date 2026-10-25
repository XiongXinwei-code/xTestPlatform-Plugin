# 全插件生命周期复核与三轮勘误交付

开始：2026-10-25；完成：2026-10-26。任务等级：L3。范围为本仓库十个插件及 Shared；用户已明确“暂无，先完成软件验证”，因此没有连接实际设备、运行真实设备序列或替换平台已部署目录。

本轮已发现并有明确证据的问题均已修复，完成非作者复审、最终完整回归和打包。该结论针对下面列明的软件验证范围，不代表未知缺陷已经归零，也不代表实际平台窗口、厂商驱动和设备时序验收通过。

## 1. 源码、手册及分支

- 2026-10-26 再次 fetch 的 `origin/master` 为 `5351a0a9d66594455b292fe65308303488a07537`。本地和该远端开发手册 blob 同为 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`，本轮没有按过期示例开发。
- 工作分支为 `codex/full-plugin-lifecycle-reaudit-20261025`；产品修复提交 `74c8354f50a5127d774ff779148ab05a8333d820`，交付追溯脚本修正提交 `7ee1096a50c922282cfa61470668bc7b1a7ed2eb`。最终文档另行本地提交，没有推送远端。
- 原备份分支 `develop-backup-20261024` 仍指向 `54ef5f5f38d64813fb409b1d000b966a8a1a2949`，没有覆盖原快照。
- 本轮从干净的 `8fc8a5337a42da9a68d282d6e2ae87d9d8464f6d` 继续。与 master 的整体差异还包括此前的缺陷修复、NIDAQ AO 两步骤和插件树分层，不能全部算成本轮新增功能。
- 云端文件差异见同目录 `2026-10-26-followup-vs-origin-master-files.csv`；每行标识“本轮增量/前轮已有”。完整复核过程和阶段依赖见 [计划](../plans/2026-10-25-plugin-lifecycle-followup.md)。

差异清单以 `7ee1096a` 为产品代码快照，列出当时相对 master 已跟踪的源码、测试、构建和历史文档；本次新报告/清单文件自身不计入。清单共 1117 项，其中 239 项为本轮增量，878 项前轮已有，避免把持续任务此前成果误说成本轮新写。

## 2. 修改了什么，以及为什么

| 范围 | 已修行为 | 原因与兼容边界 |
| --- | --- | --- |
| CAN 打开、通用周期 | 最终进入驱动前复查原执行身份、原任务停止状态和配置期限，启动步骤的短令牌不误终止持久任务 | 防止排队中的旧 Open/Write 在中断后迟到提交；保留公开 Open 签名 |
| CAN 自动回收 | 观察真实关闭任务，不再用默认 5 秒软等待截断已配置 8 秒的受控停机 | 等待结束不是关闭完成；未退 I/O、未确认发送和 PartialCycle 仍保留隔离 |
| NI/ZLG 确认 | 配置的单调总预算贯穿排队、多帧、监测和所有者停机；查询/回显成功后再次检查原取消及期限 | 原内层 5 秒会提前拒绝更长配置。假驱动实际等待 5.5 秒的回显已验证；公开无预算接口仍保留原默认保护，票据不会因停止等待而丢弃 |
| CAN Flash 执行 | Exists、打开和只读文件读取统一在后台；文件任务晚退仍被观察，回到前台先检查原身份 | 映射盘/网络路径的同步文件入口可能阻塞调用线程；取消前台等待不冒称文件操作已退出 |
| CAN 下载容量 | ECU `0x34` 响应最大块长为 0/1/2 时拒绝后续 `0x36/0x37` | 容量连 SID/BSC 头都不能容纳时，不能退回用户配置 512 字节继续发送；合法容量和用户限额保持 |
| CAN 实际编辑页面 | SDO 路径未失焦也先提交再保存；Flash 分析卸载撤销结果归属、保留真实 worker；DTC 整数越界显示错误 | 防止刷新/切步骤丢输入、旧结果写入新页面、int 转 byte 静默截断；序列 byte 设置与合法值不变 |
| Ethernet/SOME-IP/DoIP | 临时 socket 进入执行级所有者，SDK 回收能结束前台等待，实际 I/O 和取消回调退出后才释放端口 | 旧局部 using 无法被资源表回收，可能让首重跑端口占用。取消桥不在宿主线程执行原生回调；已退出回调抛错保留诊断，原生释放失败仍隔离 |
| 网络输出/UDP 端口 | 七类输出在首发或消耗响应前验证声明和 string 类型；端口 0 在编辑、描述和运行中一致支持 | 不让错误变量配置先发诊断、消费数据再失败；保留空输出、发现收集窗口、ReuseAddress 和合法报文字节 |
| HTTP 客户端与发布 | Close 的 Contains/TryGet 失效不再裸 Remove 新客户端；每项输出、最终结果和 SOAP Fault 解析后复查原 caller/epoch/owner | 旧续体不能移除或继续发布新运行的连接；查询失败先保留原取消与作用域原因 |
| HTTP 输出与提取 | REST/SOAP 输出发送前检查；JSON/XML 在首次写变量前验证全部目标与路径 | 本地配置错误不发请求、不留部分提取结果；真正的发布失败/取消不承诺回滚已写值，日志继续隐藏敏感正文 |
| Modbus 收尾 | 取消回调、native interrupt 和诊断进入受跟踪后台；真实 I/O 与取消任务都退出后释放；失败 interrupt 可在释放开始前重试 | 旧 Dispose 会同步卡住调用线程，回调异常还会冒出；不修改原建立/通信阶段的期限合同 |
| Modbus 请求 | 单次及批量读写在首发前验证全部可确定配置、地址范围、32 位成对数量和输出声明/形状 | 第二项本地配置错不能让第一项先写设备；真实通信第二项失败时，原先成功项仍保留，不回滚 |
| SerialPort | 打开/关闭全步骤沿用用户原总期限，覆盖工厂和最终日志；Read/Query 在 I/O 前校验可选输出 | 不以内部另起计时延长原期限；已真实关闭的事实不因日志超时丢失，也不关闭新 owner |
| LIN/Serial/Modbus 查询 | HasVariable/GetVariable 返回后，先检查原取消和资源作用域，再报告未声明/类型错误 | 避免内部回收丢失原因、原调用方取消误记 Error；公开两参 helper 保留，运行内部增加 guard |
| NIDAQ/OPC UA/VISA | 查询前后及最终发布补原取消/资源代次检查；旧 OPC Connect/采集启动不关闭新 owner | 不再把已结束执行误报为普通缺资源。OPC 恢复仍由用户序列控制；本轮不新增自动重连 |
| CiA402 | 保持本轮轴步骤控制语义，同步私有 CAN 到 1.0.38 | 不改 PDO 布局、停机控制字、绑定行为或物理停稳判据；独立编译并验证依赖一致 |
| 追溯工具 | build-manifest 从真实 Git 状态读取未提交标志，读取失败拒绝生成 | 原脚本固定写 true；本次实际打包标志为 false，与打包时干净工作区一致 |

Modbus 数量上限依据官方 [Application Protocol V1.1b3](https://www.modbus.org/file/secure/modbusprotocolspecification.pdf) §§6.1–6.4、6.11–6.12：读位 2000、读寄存器 125、写位 1968、写寄存器 123；插件没有以推测限制 TCP 的 Unit ID。

所有新增预检保留原字段、枚举和可选留空语义。完整变量路径按手册 `Scope.Name` 检查形状，再以 SDK `HasVariable` 为声明权威，没有硬编码五个作用域白名单，也没有认定 SDK 实际支持任意自定义别名。历史序列使用未声明/错误类型输出时会更早报错，需由用户显式声明，不自动创建业务变量。

## 3. 三轮勘误与能力执行

1. 第一轮逐文件阅读当前产品源码和调用链：CAN 251、CiA402 148、Ethernet 120、HTTP 53、LIN 80、Modbus 64、NIDAQ 106、OPC UA 87、SerialPort 49、VISA 59、Shared 4，共 **1021 个 C#/XAML/csproj**，未读 0。各阅读台账保存路径、阅读者及当时指纹，后续差异另行复审；没有用重新计算哈希冒充全文阅读。
2. 第二轮对已确认缺陷保留真实 RED，再实施 GREEN，并安排非作者只读复审。检查原 caller 与内部期限、SDK CleanupExecution/CleanupAll、同表/新调试资源表、阻塞取消回调、原始 I/O 晚退、旧 owner 与新 owner、实际编辑控件保存。Root 复审提出的网络取消桥、回调异常释放及 CAN 确认结果后栅栏均有失败证据并闭环。
3. 第三轮冻结产品源码后运行九套完整回归、十模块核心/UI 重建、120 步骤身份/依赖/文档核对、私有 CAN 一致性、UTF-8 与源码指纹校验。最终结果如下，不以早期 1369 基线或局部通过代替本轮结果。

采用 ECC planner/architect 制定阶段及所有权设计，按 TDD/C# testing 保留失败证据，按 C# reviewer、silent-failure-hunter 检查异常与取消传播，按 security-review 检查路径、输出和敏感日志，并执行 verification-loop。高风险部分由原生子 Agent 非作者只读复审；限定的并行实现采用互不重叠文件及独占测试构建窗口。Windows/WPF 使用现有组件测试，没有把组件结果说成真实平台 E2E。

勘误中撤回或修正的证据也保留：

- Modbus 的建立期限不等同串口明确的全步骤期限，早期类比已撤回，没有据此乱改合同。
- Ethernet 输出最初 31 个失败中，一个依赖五根白名单的探针随手册复核失效；有效缺陷 RED 为 30，另 1 个无效探针和 40 个原正例保留原 TRX，最终按声明权威重测。
- 部分旧合法夹具补显式变量声明、OS 已绑定屏障和真实 I/O 退出 TCS；未放宽步骤配置期限、重发测试包或删除零发送/零发布断言。
- 一次 CAN 测试夹具回调注册 Dispose 自等导致测试 host 中止；修正 finally 释放顺序后重测，没有修改产品绕过。构建曾因遗漏 OutputPath 被沙箱拒绝写平台目录，修正为仓库 artifacts，未将这些工具/夹具失败计作有效缺陷 RED。

非作者记录及逐文件台账位于 `artifacts/lifecycle-followup-20261025/`，包括 `http-final-independent-review.md`、`modbus-shutdown-independent-review.md`、`modbus-preflight-independent-review.md`、`output-preflight-final-independent-review.md` 和 `network-owner/ni-opc-visa-independent-review.md`。作者自测没有冒称为独立复审。

## 4. 最终实际验证

| 测试项目 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| CAN | 552 | 0 | 0 |
| CiA402 | 237 | 0 | 0 |
| LIN | 114 | 0 | 0 |
| Modbus | 153 | 0 | 0 |
| NIDAQ | 116 | 0 | 0 |
| OPC UA | 185 | 0 | 0 |
| SerialPort | 80 | 0 | 0 |
| VISA | 85 | 0 | 0 |
| Ethernet/HTTP 集成回归 | 424 | 0 | 0 |
| **合计** | **1946** | **0** | **0** |

已实际执行的命令（工作目录为仓库根目录）：

```powershell
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-25-lifecycle-followup
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Package -OutputRoot artifacts/Release/2026-10-25-lifecycle-followup
pwsh -NoProfile -File tools/Verify-PluginDelivery.ps1 -OutputRoot artifacts/Release/2026-10-25-lifecycle-followup -BaselineRoot artifacts/Release/2026-10-25-lifecycle-reaudit
pwsh -NoProfile -File tools/Verify-PluginBinarySources.ps1 -OutputRoot artifacts/Release/2026-10-25-lifecycle-followup
pwsh -NoProfile -File tools/Verify-PluginSources.ps1
```

- 十组重建覆盖十核心与十 UI，均成功；逐组构建日志 **0 警告、0 错误**。
- 120 个步骤 ID/显示路径与上一已验收包一致；本轮没有增删步骤。核心/UI 版本一致，依赖严格加载通过。
- 私有 CAN 两份 SHA-256 相同；交付目录无测试程序集或 testhost。
- **30 个产品/测试程序集**的 PE/PDB 配对及 **1405 次非生成 C# 文档引用**与最终源码匹配。XAML/csproj 用最终 1021 项产品输入指纹补充，不拿 PDB 检查替代 XAML 行为验证。
- UTF-8、C# 无 BOM、120 独立编辑器与执行状态检查通过；最终文档加入后再次静态检查。没有测量覆盖率，不声称达到覆盖率百分比。

证据：交付根目录 `verification/third-round-tests.log`、九份 `*.Tests.trx`、十份 `*-build.log`、`delivery-contract-receipt.json`、`delivery-source-receipt.json`、`source-snapshot.json`、`build-warning-summary.json`，以及父目录 `build-manifest.json`。这些是本轮新运行结果，阶段 RED/GREEN 位于另一个证据目录，没有覆盖旧交付。

## 5. 云端版本与可用目录

| 插件 | master 版本 | 本地核心/UI版本 |
| --- | --- | --- |
| CAN | 1.0.30 | 1.0.38 |
| CiA402 | 1.0.0 | 1.0.10 |
| Ethernet | 1.0.19 | 1.0.24 |
| HTTP | 1.0.19 | 1.0.24 |
| LIN | 1.0.27 | 1.0.32 |
| Modbus | 1.0.19 | 1.0.23 |
| NIDAQ | 1.0.19 | 1.0.24 |
| OPC UA | 1.0.19 | 1.0.24 |
| SerialPort | 1.0.27 | 1.0.32 |
| VISA | 1.0.19 | 1.0.24 |

可直接使用的十个完整插件文件夹：

`D:/project/xTestPlatform_Plugin/artifacts/Release/2026-10-25-lifecycle-followup/Plugins`

共 543 个文件，432630428 字节（含配套依赖和文档）；构建目录保留任务开始日期，完成日期见本记录和清单。使用时结束旧平台进程，成套替换所需插件目录后重启；不要只挑一个 DLL 混用。新包没有自动写入实际平台目录。

本包 `Plugins/CAN/CAN.StepPlugin.dll` 的 AssemblyVersion/FileVersion 均为 `1.0.38.0`，MVID 为 `34021085-8176-4a1c-9873-cabd3df5f038`，SHA-256 为 `06923772926b0fa24700d722f47b1086dbe091ea44b86ea401f8c4df5c061416`。这是本地交付 DLL 的标识；没有读取真实平台进程所加载 DLL，不能据此证明现场已经更新。

## 6. 尚未验证及使用边界

- **未执行**真实平台“中断后立即重跑、退出调试后重新进入”的窗口端到端验证：用户暂无安全测试工程。已用真实 SDK 资源表回收、假驱动、localhost、WPF 实际控件覆盖其软件生命周期分支。
- **未执行**真实 NI-XNET/DAQ、ZLG 等驱动/硬件的回显时序、时钟质量、队列/BusOff、Stop/Clear、故障恢复及运动安全验证。假驱动 5.5 秒回显是实际墙钟的软件探针，不是实卡证明。
- 厂商原生调用若始终不退或驱动拒绝释放，插件会在用户配置期限内返回未确认状态，并保持所有者/隔离；不会强清占用制造可重跑假象。成功首重跑的条件是旧资源已实际释放，不能承诺驱动永不故障。
- TOSUN Receive 非零状态究竟属于空队列还是故障，仍需对应 SDK 的 RX 状态契约核实；未凭通用错误码猜白名单。现有单库所有权限制和已记录的驱动/设备资格边界保留。
- CAN/CiA402 的命令 ACK、CycleStop、点表结束和软件任务退出不代表电机物理停稳。没有放宽周期时序判据、伪造时间戳、关闭发送确认或绕过共享 CAN 隔离来取得通过。

回退须保留旧插件包，停止使用新包后成套还原核心/UI/依赖。软件回退不会撤销已发送的设备命令或自动复位输出。
