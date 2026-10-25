# 全插件再次核查与三轮勘误

## 范围与基线

- L3；本轮从干净 `8fc8a5337a42da9a68d282d6e2ae87d9d8464f6d` 开始，保留之前提交、备份和交付。工作分支不变，不覆盖master或已部署插件。
- 重新fetch后master仍为 `5351a0a9d66594455b292fe65308303488a07537`；本地/远端开发手册blob均为 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`。按该权威契约检查全部10插件/120步骤与Shared；不以旧报告取代代码。
- 验收：原运行中断或SDK资源回收后，旧执行不再提交/发布；实际已退出资源允许首重跑，同表与新调试资源表均验证；尚未退出实际I/O仍保留所有权；宿主同步回收不等待厂商阻塞调用；编辑刷新/卸载前将实际控件输入保存到原步骤。
- 用户已明确“暂无，先完成软件验证”。只使用真实SDK资源表、fake驱动、localhost与可用WPF组件；不跑实际设备序列、不绕过许可、不猜停机字。命令ACK/软件任务退出不代表电机停稳。

## 架构与能力选择

- planner/architect检查epoch→逻辑owner→实际I/O→关闭任务→物理退休占用的顺序及UI控件→VM→捕获Step保存链。复用已有内部契约，不新增跨组件API或自动重连；如确有必要，先记录提供/消费端兼容后原子修改。
- tdd-workflow/csharp-testing：确定缺陷先留实际RED/屏障复现，再最小修复和GREEN；测试原调用方取消、内部超时、排队入口、晚返回、重新创建和刷新卸载。使用.NET/xUnit，不套JS命令或未测量覆盖率门槛。
- C# reviewer/silent-failure-hunter核异常传播、吞错、软超时与真实退出、持锁同步等待、任务引用及旧/新代次；security-review核输入和敏感诊断。设备/共享并发属于L3，安排非作者只读审查。
- 原生子Agent只读分工：CAN/CiA402/LIN；NiDaq/OpcUa/VISA；Ethernet/HTTP/Modbus/SerialPort。主线程负责Shared、跨模块集成与复现修复；若需要并行实现，另记录文件边界、接口与独占构建窗口。审查者不写源码、不构建、不操作设备；作者自测不算独立复审。
- verification-loop用于最终构建、C#类型检查、定向/完整回归、静态UTF-8/契约与差异检查、产物版本/依赖/PE-PDB/源码指纹。WPF实际宿主不可用及第三方许可限制如实标未执行；组件测试不冒称全宿主E2E。

## 三轮与交付

1. 第一轮：逐模块重新阅读全产品文件与调用链，确认前次交付源码是否变化；定位确定缺陷、优先编写失败测试。旧1369项只作基线，未运行的新断言不写通过。
2. 第二轮：修复后非作者复审及重测，覆盖真实ResourceRegistry CleanupExecution/CleanupAll、同表/新表、取消/超时/释放交错、控件未失焦保存及卸载重入。新问题先定位再修，不改合同或放宽期限迎合错误探针。
3. 第三轮：冻结产品源码，对最终状态执行必要回归、核心/UI构建与严格交付核对。与最终源码哈希一致的有效旧证据可作补充；明确本轮亲自运行、独立读取、未执行和不适用。
4. 保存本轮缺陷/修复/实际证据与限制；有产品变化才按既有1.0补丁递增受影响核心/UI及文档。生成独立可用目录 `artifacts/Release/2026-10-25-lifecycle-followup/Plugins`，不混用旧DLL；证据放 `artifacts/lifecycle-followup-20261025/`。
5. 提交使用中文Conventional标题和原因/改动/验证正文，协议/硬件变化说明兼容与回退；不推送/部署，不强清未退出的占用。若无新缺陷，不凭空改行为或递增版本。

## 当前状态

- 已确认：干净起点、远端手册一致和用户的软件验收边界。
- SDO实际页输入：两页×卸载/同页刷新/切步骤，统一STA组RED显示6种丢失路径；Root保留LostFocus策略，刷新/卸载先UpdateSource再Flush，卸载finally取消EDS归属。实际页面及EDS旧行为17/17 GREEN。
- VISA缺资源边界：5执行器×原caller取消/SDK CleanupExecution/CleanupAll/普通缺资源，RED15失败5正例→20 GREEN，全VISA85 GREEN。新代次未建立不假造会话或发送。
- Serial已实际RED：Open factory/最终日志原总期限、Read/Query输出预检、lookup取消分类、空输出合同及UI校验；21项15失败6正例，正在修复。
- CAN通用周期发现实际发送入口的长生命周期/停止令牌遗漏；独立调查并复现。OPC Connect发现lookup跨越回收会误关新owner，以及NI/OPC缺资源后分类遗漏，先按真实SDK/fake owner复现再修。

### 经证据授权的有限并行实施

根线程只改SDO实际页及其专属测试/Shared风险分析和最终版本/文档；followup_can只改通用周期Executor、CanConnectionResource内部发送边界和专属测试，独占CAN.Tests构建（Root SDO绿色后交接）；followup_network_serial只改SerialPort模块与专属测试，独占其测试构建；followup_ni_opc_visa只改VISA/NI/OPC限定边界和专属测试，按模块串行构建。各方不改Shared/Git/版本/文档，不写其它模块，不部署或操作硬件。核心接口仍同构建内原子修改；最终由Root集成、非作者交叉复审。无共同obj并行构建；需扩大到新文件/合同先说明原因。

- 正在进行：剩余全文阅读、授权缺陷修复及第一轮证据汇总。
- 待完成：第二轮非作者复审、第三轮最终冻结、验证与交付回执。

### 第一轮扩展的所有权取舍

- Modbus已有建立/通信阶段期限不等同Serial明确的整个步骤期限，撤回“最终日志超时必属缺陷”的无依据类比，不更改其建立/读写合同。已实际复现的Dispose/Close同步取消、transport interrupt和诊断回调阻塞/冒异常另行修复：立即冻结owner，后台跟踪CancelAsync与native abort，真实I/O及停止任务均退出后才释放；失败abort允许重试，但释放开始后不再碰同一句柄。用户Close软等待由原配置控制，后台任务不因等待结束被遗弃。诊断在独立观察者后台，不能占宿主资源清理线程。
- 临时网络socket发现未登记生命周期导致SDK回收后固定端口仍被旧Receive占用。Ethernet组复用真实NamedConnectionPool的登记/Retiring/后台关闭（其当前没有lease stop API，不把设计当既有功能）；最小owned handle追踪独立stop和actual I/O、finally按捕获身份关闭。包括UDP与SOME/IP临时TCP；固定端口重跑等旧释放，port0不串行化，SD ReuseAddress与原集合窗口保留。
- CAN补读扩展UDS/XCP同构lookup检查；自动Dispose不再把默认5秒外部软等待变成取消用户8000ms受控停机。全部先RED再GREEN，不强清占用。
- CAN UI继续核Flash文件分析同步预检/卸载归属及DTC int→byte截断；保持设置wire与有效值行为，先UI线程/非法输入失败探针，构建窗口在两实施者间显式交接。

### 第二轮已核证的补漏与软件证据

- Modbus 收尾：21项初始探针17失败/4正例，最小修复21通过；再用failed-abort重试探针取得1个新RED并修复。原模块95项通过。数据配置预检再取得35项中的23失败/12正例；最终新增40项覆盖所有本地配置在首发前校验、协议上限和原通信失败不回滚，全模块135/135，非作者只读复审无阻断。
- CAN UI：原SDO保存17项通过；Flash文件分析与DTC越界11项9失败/2正例，补原worker未退/取消回调阻塞/结果重入等33项通过，全CAN当时515/515（尚未纳入随后的核心期限改动）。全部239个CAN/CiA402/LIN UI产品文件本轮全文阅读有逐文件账本。
- 核心CAN追加真实5500ms回显探针，NI/ZLG各自循环/监测/所有者停机共6项被旧5秒默认截断，2项公共默认保护正例；内部预算能力保持公开确认接口默认行为，由原配置单调预算贯穿排队/多帧。Open注册后marker移除最终提交屏障取得1RED，另4种原caller/SDK cleanup/正常正例；不将单独marker移除冒充完整SDK回收。
- 网络临时socket已取得真实SDK/localhost4项固定端口首重跑RED、4项DNS/TCP cleanup RED，以及独立原caller取消回调阻塞RED。新增最小内部owner及retirement key（不改设置wire），所有停止信号/真实I/O/取消回调分别追踪；同一取消回调异常已经退出后不能永远卡占用，原生Dispose失败仍隔离。Root非作者审查提出后两项，先失败测试再修改。
- OPC AcqStart最后lookup补漏8项6失败/2正例，完整OPC185/185；同类HTTP Close Contains/typed lookup与裸Remove的代次风险继续由Root专属复现，不把源码假设提前计为修复。
- 实际日期跨至2026-10-26，本计划与证据目录保留启动日期2026-10-25；最终交付记录使用真实完成日期。编译命令均重定向到本仓库artifacts，不写已部署平台目录；一次遗漏OutputPath的构建因沙箱禁止部署目录而失败，纠正参数后继续，未计作有效RED或通过。
### 第二轮完成与第三轮冻结

所有作者已冻结产品源码及测试；240核心、239相关UI、252 NI/OPC/VISA、117 HTTP/Modbus、169 Ethernet/Serial、Root Shared4的逐文件记录合计覆盖1021个C#/XAML/csproj产品文件。新增helper及逻辑差异由非作者复核；Root机械递增20个核心/UI版本、修订包内文档，最终阅读覆盖和SHA保存在final-product-read-inventory.json，不把重算哈希说成重新全文阅读。

最后专项闭环：HTTP CheckPublication查询后6RED/2正例与SOAP解析后4RED已修，连同七类Ethernet输入及真实页面保存的PluginRegression阶段424/424；CAN确认结果后3RED→26相关GREEN；Flash预检/ECU容量6RED/9正例→166相关GREEN。变量预检原取消/资源回收优先级：Serial12RED→12GREEN/full80，LIN18RED/6正例→56相关GREEN，Modbus12RED/4正例→58相关GREEN。三模块作用域白名单各2RED→2GREEN；按手册Scope.Name形状+SDK HasVariable作为声明权威，不声称真实SDK存在自定义别名。

勘误口径：Ethernet输出首次31fail中一个只因非五根拒绝已声明路径的探针随手册复核失效，实际有效30RED、1无效白名单探针、40正例；原TRX保留。Modbus建立期限不等于Serial全步骤期限的早期类比已撤回；没有据错误探针更改该合同。部分旧合法测试夹具补显式声明或OS已bind屏障、实际I/O退出TCS，所有用户期限、发送/发布断言保留，不让新配置预检遮蔽原安全验证。

开始第三轮：冻结后的9套完整回归、10核心/10 UI重建、120步骤身份与依赖、UTF-8、PE/PDB/源码、私有CAN一致性及最终diff。结果在实际命令完成后填回，不以阶段局部通过或旧1369基线代替。2026-10-26再次fetch master仍5351a0a；手册blob仍9ca9f70e，与本地一致。完整宿主/真实设备验证按用户“先完成软件验证”保留未执行。
### 第三轮最终结果（完成于2026-10-26）

- 已完成：9套完整回归1946/1946，失败0、跳过0；十组核心/UI重建均0警告0错误。
- 已完成：严格交付契约10模块/120步骤、两份私有CAN SHA一致、30个程序集PE/PDB与1405次C#源码引用匹配；1021最终产品输入指纹没有冻结后漂移。
- 已完成：本地独立交付目录artifacts/Release/2026-10-25-lifecycle-followup/Plugins，543文件/432630428字节，未写入平台已部署目录。版本核心/UI成对递增，CAN1.0.38、CiA4021.0.10，其余见最终报告。
- 第三轮另修构建追溯脚本固定true标志，实读Git提交/状态失败则拒绝清单；实际Package运行通过，sourceCommit=7ee1096a，sourceIncludesUncommittedChanges=false，符合打包时干净工作区。此工具改动不影响测试覆盖的产品逻辑，无需为仅工具元信息重复无变化的全回归。
- 产品提交74c8354f、工具提交7ee1096a均在本地工作分支，未推送；原develop-backup-20261024快照54ef5f5f保留。最终中文修改说明、源代码相对远端对照和软件/硬件边界保存在docs/reviews/2026-10-25-plugin-lifecycle-followup.md及相邻CSV；最终文档单独提交，不改已验收产品源文件。
- 明确未执行：真实平台调试窗口退出/重入、现场设备、厂商原生驱动时序与运动安全；没有以软件仿真替代硬件验收。TOSUN RX非零返回含义仍需对应SDK合同，不凭猜测修改。