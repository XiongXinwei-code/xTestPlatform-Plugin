# 全插件生命周期再次审查与三轮勘误回执

任务等级：L3。日期：2026-10-25。当前报告对应 `codex/full-plugin-lifecycle-reaudit-20261025` 的新增审查；与此前备份提交的三轮记录分别保存。

## 范围、基线和方法

- 起点 `54ef5f5f38d64813fb409b1d000b966a8a1a2949`；原 `develop-backup-20261024` 及旧插件包保留。最新 fetch 的 `origin/master=5351a0a9d66594455b292fe65308303488a07537`；远端与本地开发手册同为 blob `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`，按该契约检查。
- 10插件、120步骤；全文阅读产品 C#/XAML/csproj 共1016：CAN251、CiA402148、Ethernet118、HTTP52、LIN80、Modbus62、NI DAQ106、OPC UA87、Serial49、VISA59、Shared4。包括 Setting/默认值/Description、执行器和资源所有权、后台任务、C# interop、校验器、VM、独立步骤编辑器与实际页面。排除生成文件、测试、图标、第三方原生二进制内部；没有声称逐字阅读所有测试和厂商SDK内部实现。
- 当前逐文件库存和哈希：`artifacts/lifecycle-reaudit-20261025/full-product-inventory.json`；CAN/CiA/LIN补读台账 `can-fullread-inventory.json`。库存脚本证明文件范围和内容，不替代人工阅读和运行。
- ECC planner/architect 已用于执行epoch→逻辑owner→实际I/O→退休物理占用→SDK清理的数据流审查；tdd-workflow/csharp-testing 用于实际RED/GREEN；C# reviewer/silent-failure-hunter 用于取消、超时、异常和后台退出；security-review 检查输入与凭据/正文诊断边界；windows-desktop-e2e 明确组件与宿主运行差别；最终使用 verification-loop。未启用额外框架、自动Hooks、自动重连或设备动作。
- 独立审查由非作者读取源码和调用链，不写产品、不操作设备。主线程负责复核、集成与最终运行；读取作者TRX不写成审查者亲自运行。

## 第一轮：缺陷、修复和兼容性

| 模块 | 实际问题与改动 | 验证主线 |
| --- | --- | --- |
| CAN 打开/提交 | 排队后与原生入口缺执行代次/取消/单调预算复查，Open日志后可能发布过期成功。增加最终入口检查及原Open预算复核；后台泵/服务不绑定短步骤token，所有者停机使用自己的期限。 | 真实SDK CleanupExecution/CleanupAll、原生入口屏障、Open完成/日志超限、首次重跑。 |
| CAN 服务关闭 | 按名称再次查找可能关闭同名新服务；连接关闭使用无参StopTargets默认值丢失已配置停止期限。改为捕获精确服务代次及受控停止配置。 | 旧Stop不关闭replacement；假SDO ACK 5500ms、用户停止预算8000ms、父关闭10000ms可完成；不放宽产品默认期限。 |
| CAN 协议输入 | SDO块下载不可能ACK/后续块大小未拒绝；UDS文本表达式null回退到源码；基础读写缺资源时漏原调用方取消。严格校验并保留事务隔离、null与明确空字符串的区别、Aborted分类。 | 畸形ACK后不发End、不复用隔离事务；null无发送；lookup取消。 |
| CAN PEAK/原生库 | USB9–16原线性句柄错误；ZLG/Vector竞争同程序集唯一resolver。按官方句柄表修正；一个resolver按精确库名分派，绝对路径/安全依赖搜索，不再修改进程级DLL目录。 | 官方PEAK表与Microsoft LoadLibraryEx；两个初始化顺序、独立ALC空DLL、搜索目录保持和失败诊断。厂商内部动态加载仍需实机。 |
| CAN 固件/编辑器 | 固件合并先排序丢失文件写入顺序；Channel漏语法验证；EDS加载在Dispatcher同步读文件。合并按原记录顺序覆盖，不改地址空洞；补实际语法验证；EDS后台按行解析、合并pending、捕获设置/路径/代次、卸载只取消归属，真实旧读取退出后才读最新请求，文件存在性校验也移后台。 | 重叠、等地址、32位尾地址和真实HEX解析链；Channel实际校验；EDS线程、失败提示、路径/步骤换代、卸载/重入、集合通知。 |
| CiA402 | TPDO去重把不同ClockId/时间域时间戳直接比较。按时钟身份重置去重下界和部分反馈组，仍以可信采集时间判断新鲜度。 | 时钟重置与混合反馈组；不以出队刷新帧龄，不改变运动控制或父CAN所有权。 |
| LIN | 原生入口缺步骤代次；Open最终日志后缺原期限检查；输出变量不存在/类型错误在通信后才发现。增加入口与预算复查、通信前结果/ID变量预检及同步页面/Description/校验。 | SDK清理与排队入口、Open日志预算、9个负输出用例和正例；周期任务用长生命周期。 |
| HTTP | null回退到源码；默认头CRLF与添加返回值未检查；登记排队重置期限；日志/提取日志可能包含正文和敏感值；已有设置页面漏选项。修复求值与头校验，保留重复默认头追加；用原配置预算；日志仅摘要；实际REST/SOAP页补既有选项。 | 实际handler计数、头解析、gate预算、日志虚构秘密、实际控件卸载保存；null与显式字符串null区别。 |
| VISA | 旧Open lookup跨越SDK回收后可能Close新运行会话。触碰lookup返回实例前再查epoch与Open代次。 | 真实ResourceRegistry两种cleanup交错，0新Open、不关闭新owner。 |
| OPC UA | 批读Grid未失焦编辑在卸载/刷新丢失；密码普通TextBox显示；采集超时把剩余预算当配置期限。先Commit Cell/Row再Flush；PasswordBox双向同步；外层错误保留用户配置期限和内层信息。 | 6个真实Grid、5个密码生命周期用例、采集预算原失败复现。密码存储仍是旧普通字符串格式，没有宣称静态加密。 |
| NI DAQ/Serial | 实际业务Tab漏图标/尺寸；NI TaskStop缺不存在名称说明。补标准已有图标和Description。 | 实际页面图标/尺寸、说明契约；没有新增默认AO输出或隐式复位。 |
| Ethernet | SOME/IP漏ClientId关联；SD用有符号截断长度解析；DoIP在头未验证前发布VIN。完整请求ID匹配，无符号完整范围验证，公告反码/声明长度先校验。 | 本地UDP/TCP、真实解析函数、DoIP fake公告30项：24 RED/6原正例→30 GREEN；相关56项GREEN。保留InterfaceVersion、发现窗口和原I/O期限。 |
| Modbus | 本轮完整读取未确认新增产品缺陷，保留已验证的生命周期实现。 | 最终仍重新执行完整测试和核心/UI编译，版本不为凑数递增。 |

## 第二轮：非作者复核与纠错

- 非作者复核 Root HTTP/VISA/Serial、Ethernet三项、CAN/CiA/LIN第一批；主线程独立核 NI/OPC、Ethernet和CAN第二批产品及测试；另一次非作者专项核 SOAP、原生resolver/固件和EDS。
- 发现遗漏：SOAP1.2非法Action经 .NET 格式异常进入Error.Detail。4个真实失败、1个合法正例后改用TryParse及不含输入的错误，并将Content先挂到request保证异常释放；5项GREEN包含在本轮HTTP/网络62项GREEN中。
- 发现新实现遗漏：EDS清空列表/选中项通知可重入并覆盖最新pending。2个真实RED后，在每次初始化通知之后复核原设置/路径/代次；最终EDS11项与实际页调度组1项12/12 GREEN。后续冻结全套再次运行。
- 最终专项复核又补两个相邻发布交错：两个集合订阅者中的一个在Add通知刷新，会因同步Clear触发ObservableCollection重入异常（1个真实RED）；同文件选中项通知改选/清空，旧外层仍继续写字段（2个真实RED）。初始化清空/通知改到独立Dispatcher轮次，单一worker等待本请求初始化后再核身份；选择回填同时核对当前对象身份。另加缓存旧选择和跨步骤选择保护，最终全部纳入405项完整CAN回归；没有移除订阅者、吞掉异常或用空列表伪装加载成功。
- 最后非作者回执已收到：最终EDS16项及实际页事件组保持，捕获身份、独立初始化队列、同代次选择ref保护、实际旧I/O退出均复核无新增确定缺陷。审查时VM SHA-256=`fe50396c492f6e0f531f40714c33e76b3004d185771b5998d0f44886c3d733cf`；对应测试=`4e5e845acf023f6f5fe922e64de5d260190f7a5f7880a4b148e38078361d0f9f`，后续构建不再修改这些源码。非作者未运行最终405项，不把其读证据写成亲自执行。
- 撤回错误探针：最初TCP/UDP把事后日志耗时误当通信期限，核对契约后保留原产品语义并改成3个成功保护测试；不列为产品缺陷、不以修改期限迎合错误断言。
- 测试夹具修正：合法FD32的1000ms配置受并行启动负载影响，将该组纳入既有禁止并行collection；Flash实际页面与现有统一STA分裂产生Freezable跨线程错误，原全部断言归到同一STA。没有放宽产品期限或删除页面断言。
- EDS新实际页Dispatcher泵探针一次中止；dump明确停在第三方Syncfusion LicenseMessage.ShowDialog。保留 `round2-eds-green.trx` 中止回执和 `eds-hang-stacks.txt`，不计通过。实际页改为Loaded/Unloaded调度与保存检查，不再在未授权许可环境泵第三方对话框；纯VM WpfFact仍运行真实Dispatcher，验证文件线程、换代和回填。不能据此称完整宿主UI文件加载已通过。

## 第三轮：最终冻结与交付

版本与文档已同步。九套最终完整测试已执行，0失败、0跳过：

| 项目 | 通过/总数 |
| --- | --- |
| CAN | 405/405 |
| CiA402 | 237/237 |
| LIN | 85/85 |
| Modbus | 73/73 |
| NI DAQ | 92/92 |
| OPC UA | 141/141 |
| Ethernet/HTTP/Shared | 228/228 |
| SerialPort | 43/43 |
| VISA | 65/65 |
| 合计 | 1369/1369 |

实际命令：`pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-25-lifecycle-reaudit`。最后EDS源码变更后，只重跑受影响的CAN完整405项，保留其他8套同轮未变源码的完整结果；原400项回执先归档而非冒充最终状态。TRX在该目录`verification/*.Tests.trx`；主日志`artifacts/lifecycle-reaudit-20261025/round3-tests.log`和`round3-can-final-tests.log`。

最终构建与交付检查已通过：

| 检查与实际命令 | 结果/记录 |
| --- | --- |
| `tools/Verify-Plugins.ps1 -Mode Package -OutputRoot artifacts/Release/2026-10-25-lifecycle-reaudit` | 20个唯一核心/UI项目重编译，10模块构建日志0警告0错误；每个模块都通过类型/依赖严格加载，`round3-package.log`及`verification/*-build.log`。 |
| `tools/Verify-PluginDelivery.ps1 -OutputRoot artifacts/Release/2026-10-25-lifecycle-reaudit -BaselineRoot artifacts/Release/2026-10-24-full-lifecycle` | 10模块/120步骤ID与显示路径保持，9套1369测试全绿，私有CAN2份SHA-256一致；包内README/CHANGELOG与当前版本一致，无测试运行库混入。`verification/delivery-contract-receipt.json`。 |
| `tools/Verify-PluginBinarySources.ps1 -OutputRoot artifacts/Release/2026-10-25-lifecycle-reaudit` | 30个程序集/测试PE-PDB配对，1369次非生成本地C#源码文档出现记录与当前SHA-256匹配。`verification/delivery-source-receipt.json`；此项不独自证明XAML/SDK ABI/宿主实际加载版本。 |
| 当前完整产品库存与`verification/source-snapshot.json`逐项哈希复核 | 1016产品文件+5个根构建/手册配置，共1021项一致，包含XAML/csproj补足PDB范围。代码冻结提交8c3f82e298b199d250563e5064fd3c6f0bf9ce38，最终文档回执不改变产品源码。 |
| `tools/Verify-PluginSources.ps1`与`git diff --check`/提交前`git diff --cached --check` | 严格UTF-8、C#无BOM、120独立编辑器描述、执行状态静态检查和差异格式通过；不替代语义/运行/硬件验证。 |

交付目录：`artifacts/Release/2026-10-25-lifecycle-reaudit/Plugins`，543文件、432546056字节。新包独立生成，不覆盖旧交付或部署目录。构建清单 `build-manifest.json` 列出逐文件SHA-256；源码/实际工作区状态以额外`source-snapshot.json`为准（构建脚本的`sourceIncludesUncommittedChanges`字段原模板保守固定为true，不据此推断存在未验证产品改动）。

| 插件 | 本次版本 |
| --- | --- |
| CAN | 1.0.37 |
| CiA402 | 1.0.9 |
| LIN / SerialPort | 1.0.31 |
| Ethernet / HTTP / NI DAQ / OPC UA / VISA | 1.0.23 |
| Modbus | 1.0.22（本轮无新产品变更） |

三轮结论：本轮确定的软件缺陷已修复并复测，当前修复范围经非作者复核无新增确定缺陷；不将“检查未再发现”扩展成所有现场条件绝对无缺陷。

最后交付独立只读复核：逐文件重算全部543包文件/字节/SHA-256，核1021项源码快照、30二进制哈希及1369条PDB源码文档记录的当前内容；九份TRX逐UnitTestResult与Counters均核，合计1369 Passed，无失败、未执行、中止；120个(module,StepTypeId,DisplayName)与基线完全相同；20核心/UI版本、2份CAN及20份README/CHANGELOG哈希一致，10个构建日志0警告0错误。PE/PDB配对本身读取主线程检查器回执，非作者未再运行检查器、构建、测试或设备操作。模板字段解释和未执行硬件/实际调试标记均已核实，无交付阻断问题。

## 与云端及本轮起点的差异

- 比较冻结代码提交`8c3f82e298b199d250563e5064fd3c6f0bf9ce38`和远端master `5351a0a9d66594455b292fe65308303488a07537`：1055文件，新增41047行、删除4326行，包含此前已保存的全插件修复、AO/树分层及测试工具，不全部归为本轮新增。逐文件状态和增删行在`2026-10-25-reaudit-vs-origin-master-files.csv`。
- 只比较本轮起点备份`54ef5f5f38d64813fb409b1d000b966a8a1a2949`：141文件，新增2653行、删除226行。上文修复表解释本轮功能修改及原因；原备份和此前相对云端的功能报告保留，未改master或推送。
- 所有修复已存本地实施分支`codex/full-plugin-lifecycle-reaudit-20261025`。代码提交之后仅补最终验收说明；产物由明确的冻结代码版本编译，避免用文档提交冒充重新编译。

## 尚未执行的验证与回退

- 完整平台“中断→首次重跑”“关闭调试窗口→重新进入”尚未实际运行；用户确认“暂无，先完成软件验证”，未提供可确认无设备的隔离工程。真实SDK ResourceRegistry的CleanupExecution/CleanupAll、相同/新资源表、WPF实际控件/VM/localhost/假驱动已作软件替代，不称作完整宿主E2E。
- NI/ZLG/PEAK/Vector/其他驱动、ECU/电机/PLC/DAQ/仪器没有现场动作测试。NI已安装驱动不代表总线时序、硬件停止、固件下载和厂商动态依赖已验证。未测量覆盖率；不声明所有未知缺陷已消除。
- 未退出I/O继续持有原owner及物理占用；确认真实退出后首次重开才能进行。取消令牌/步骤等待超时不等于发送撤回、服务器请求撤销、任务退出或物理停稳；不得通过删除占用表强制重开。
- 回退保留旧交付完整目录，核心/UI/私有CAN和运行依赖成套替换；先停止平台并确认实际资源退出。软件回退不撤销已发设备动作。此轮无远端推送、部署或设备操作。
