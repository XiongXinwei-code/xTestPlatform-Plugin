# CiA402 全步骤检查与功能优化

任务等级：L3（运动控制、协议兼容、多帧反馈和 WPF 集合编辑）。基于当前 master 工作区继续保留上一轮生命周期修复，不修改父 CAN 插件。

## 范围与阶段

1. [x] 检查全部 18 个步骤的设置、执行器、公共状态机、传输层、编辑器及校验，记录缺陷和不适合无硬件扩大实现的项目。
2. [x] 添加回归：旧启动位、PV/PT 遗留目标、模式显示正确但状态已故障、单 TPDO 附带其他对象；旧实现 6 个展开用例全部失败，修复后通过。
3. [x] 扩展 BindAxis 的多 TPDO 配置与兼容校验；使用 ObservableCollection 和可通知子项，UI 支持增删、逐项编辑和防抖保存。
4. [x] 实现按所需字段收集多 TPDO，逐 COB-ID 检查重复时间戳、队列年龄和合并时间窗口，所有帧共用通信期限；不使用跨调用旧值补缺。
5. [x] 修复使能前遗留启动位和 PV/PT 非零目标；模式确认同步检查驱动状态；提前校验 PDO 语义和诊断空输出。保留取消、延后释放和原步骤标识。
6. [x] 完成核心、兼容、WPF STA 编辑器及全部回归，审查差异、UTF-8、构建执行/UI，生成 1.0.2 插件目录并记录验收。

## 设计与兼容

- 新增 UseMultipleFeedbackPdos 和 FeedbackPdos；旧 FeedbackPdoNumber/Layout 原样保留，默认继续走旧单帧或 SDO 行为，不静默切换。
- BindAxis 设置结构版本升级至 2，迁移压缩格式及解析器与开发手册一致，UI 保存写入序列化器版本；回退旧包须还原旧序列备份。上一轮 1.0.1 二进制已备份至 artifacts/backups/CiA402-1.0.1-before-multi-tpdo.zip。
- 按开发手册 §11.5.1/§12.1，集合子项自身实现 INotifyPropertyChanged，增删、清空、刷新和卸载时正确管理订阅与保存；不额外包装集合子项 ViewModel。
- 反馈集合整体必须提供状态字，不要求每个 TPDO 都有状态字；允许显式映射并核对额外对象，只解码已支持的反馈对象。跨 TPDO 重复字段按配置顺序选择第一个来源，不能用后到值静默覆盖。
- 帧长度和对象类型必须与实际对象字典一致；绑定只读取核对，不自动重配驱动。COB-ID 重复的反馈配置明确拒绝。
- 只收集本次请求所需的 TPDO。所有必需帧均须在队列有效期及可用时间戳的合并窗口内；窗口内不代表同一 SYNC 周期，严格一致性仍需要设备周期计数适配。
- Enable 只执行使能，不隐式启动旧运动：清除旧启动/模式选项，PV/PT 预置零目标；周期模式保留原有安全初值核对。已使能时保持幂等，不覆盖正在执行的目标。
- 不新增主站、实时循环、自动 SYNC、安全停机或厂商专有模式。父 CAN 资源的创建、关闭与顺序仍由用户序列管理。
- 最终审查补充同名轴生命周期门：绑定/解绑的注册表操作在本插件内串行，避免旧解绑误删除新会话；等待可取消，不持有或修改 CAN 适配器。

## 参考与验证边界

- CAN in Automation PDO 说明：https://www.can-cia.org/can-knowledge/pdo-protocol （经典 CAN PDO 最多 8 字节，独立通信及映射参数）。
- Kollmorgen 6040h/Homing 说明：bit 4 是 PP 新目标/HM 启动相关位。现场兼容性不能由通用说明代替驱动手册验证。
- 构建和测试输出均使用工作区 artifacts，避免 CAN 工程默认输出到工作区外。
- 没有真实驱动器/板卡，不进行运动试验；成功声明仅覆盖编译、自动测试和可观察的仿真协议行为。

## 检查结果与验收

### 全步骤检查范围

| 步骤 | 检查重点与处理 |
|---|---|
| BindAxis | 多 TPDO、额外映射对象、三处校验、旧格式迁移、绑定失败释放；本轮修改 |
| UnbindAxis | 共用截止时间、终止会话移除、延后释放、仅本轴资源；保留上一轮修复并回归 |
| Enable | 旧启动位及 PV/PT 旧目标误恢复；本轮修复并验证已使能幂等 |
| Disable | 撤销使能与状态确认，不代替减速停止；维持原语义并回归 |
| ResetFault | Fault/FaultReactionActive、复位上升沿及撤销、不自动使能；回归 |
| SetMode | 模式显示匹配时漏查故障及旧控制字意外写回运行使能；本轮修复，保留默认关闭的使能态切换保护 |
| ReadState | 读取与解码、不因诊断到故障冒充通信失败；回归 |
| ReadMode | 有符号模式值、只读与输出类型；回归 |
| ReadFeedback | 多帧路由、完整性、年龄和时间窗口、超时与取消；本轮修改 |
| ReadDiagnostics | 显式对象/掩码和变量路径、空输出；统一公共校验 |
| MovePosition | PP 新目标确认、WaitForTarget 语义、故障/限幅/跟随错误；回归 |
| RunVelocity | 有符号速度与轮廓参数、Completed 不等于停轴；回归 |
| RunTorque | 转矩原始单位、有符号边界及斜率；回归 |
| Home | 清除旧启动、等待本次回零及回零错误；回归，厂商特例仍需适配 |
| WriteCspTarget | 初始位置防突变、显式映射和 SYNC；回归 |
| WriteCsvTarget | 首次零速度与单次发送；回归 |
| WriteCstTarget | 首次零转矩与单次发送；回归 |
| StopMotion | 0x605A 策略及零速/退出运行使能确认；回归，不变更硬件停止策略 |

全部 18 个 UI 入口的设置字段、强类型属性、绑定、刷新和图标契约纳入既有测试；本轮变更的 BindAxis 额外执行 WPF Measure/Arrange、DataGrid BeginEdit/CommitEdit、持久化、清空/刷新退订及显隐切换测试。

### 保留的边界与未解锁功能

- CANopen 后端仍为经典 CAN 标准帧、8 字节 PDO 和 32 位位置接口，不将 CAN FD、64 位位置或任意厂商对象写入伪装成已支持。
- 状态机和 ReadState/ReadMode 仍走 SDO，TPDO 路由用于 ReadFeedback；不隐式重配 PDO、不创建后台接收/周期服务、不把多帧窗口当作同一 SYNC 原子快照。
- EtherCAT 只提供工厂契约，不包含主站 SDK。永久阻塞的后端调用只能保留占用，不能用强杀线程证明停轴；父 CAN 由序列管理。
- 运动单位、回零特例、使能态模式切换能力、停止选项、SYNC 时序和真实退出仍需现场验证。

### 最终验收

- Release 全部 169/169 通过（原有 132 项＋本轮 37 项）。最初 6 项缺陷回归及终审追加的 3 项未使能状态模式切换用例在修复前失败，修复后通过；WPF 用例真实执行布局、单元格提交和持久化核对。
- 多帧乱序、逐 COB-ID 时间戳、单元格保存、同名轴绑定/解绑及排队取消共 5 项额外重复 5 轮，25/25 通过。
- 测试命令：`dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:\project\xTestPlatform_Plugin\artifacts\verification\ -p:NuGetAudit=false`。
- 构建命令：`dotnet build CiA402/Cia402Plugin.UI/Cia402Plugin.UI.csproj -c Release --no-restore -p:OutputPath=D:\project\xTestPlatform_Plugin\artifacts\Release\Plugins\CiA402\ -p:NuGetAudit=false`。核心/UI 及原有 CAN 依赖均成功，0 警告、0 错误（增量构建）；不为处理基线的 CAN 既有警告而修改其他插件。
- 当前会话按 ECC Planning/Architecture、C# Testing、Verification Loop 和 Code Reviewer 方法实施、检查；未启动子 Agent、未安装依赖或启用自动 Hook。审查重点为控制字写入是否可能隐式使能、旧会话能否误删新会话、单帧限制是否残留、取消及释放竞态、旧配置迁移和列表保存。
- 审查结果：Critical 0、High 0、Medium 0、Low 0 个未处理的确定性问题，APPROVE（仅针对本地代码和自动验证，不是设备准入结论）。已发现的问题均修复并回归；不宣称所有硬件或宿主问题已穷尽。
- 已检查变更文本严格 UTF-8、既有 CRLF/BOM 兼容及 `git diff --check`；`git diff --exit-code -- CAN` 无差异。定向检查未新增同步清理等待、原生驱动入口或外部进程启动，保留的 Wait(0) 是非阻塞门检查。
- 产物核心/UI 均为 1.0.2，不含测试程序集，文档与源码同步。保留上一轮插件备份，不自动部署、提交或推送。实际硬件、物理停轴、SYNC 时序和宿主退出仍未验证。
