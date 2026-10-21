# CiA402 开发交付检查

依据 `xTestPlatform_StepPlugin_Development_Guide.md` 3.4.0 开头的开发交付检查清单逐项核对。

## 项目与构建

- [x] 执行层和 WPF UI 层为两个独立项目；另外提供不进入发布包的契约测试工程。
- [x] 程序集命名为 `CiA402.StepPlugin` / `CiA402.StepPlugin.UI`。
- [x] 两层设置 `CopyLocalLockFileAssemblies=true`，使用工作区中央包版本，未升级包。
- [x] 未新增带 RID 原生资产的 NuGet 包；CAN 原生依赖沿用现有 CAN 工程，不新增 EtherCAT SDK。
- [x] 本次新增/修改的 C# 源码检查无 UTF-8 BOM。
- [x] 全工作区构建成功，覆盖新执行/UI/测试工程以及受影响的 CAN 执行/UI 工程。

## 执行层与设置

- [x] 十八个独立 Setting/Plugin/Executor；Setting 及公共基类均标记 MessagePackObject(true)。
- [x] StepTypeId 全局唯一，18 项反射提取与 README 步骤表一致。
- [x] DisplayName、Category、IconPath 和四章节中文 Description 完整。
- [x] 自定义设置枚举使用 JsonStringEnumConverter。
- [x] 运行时字符串表达式标记 ExpressionField，字符串默认值使用合法带引号表达式。
- [x] 输出标记 VariablePathField，要求用户声明且明确配置的完整路径，不新增隐式变量。
- [x] 参数说明、编辑器、静态校验和运行时求值共享字段元数据。
- [x] 正常 Completed、错误 Error、用户取消 Aborted，不返回 Passed/Failed/Warning 作为执行结果。
- [x] 异常被执行器捕获；中文错误信息；日志使用 ctx.Log，异常详情进入 detail。
- [x] 异步操作传递取消令牌；阻塞 CAN 收发使用 Task.Run 和有界 WaitAsync。
- [x] 步骤总超时、单次通信超时、轮询间隔在编辑器中可配置，超时不冒充 Completed。
- [x] 资源重名时报错、解除绑定不存在时 Completed，行为与 Description 一致。
- [x] 同轴互斥、同节点独占及执行期清理；取消/控制错误后不继续使用终止会话。

## 三处校验

- [x] 每个独立 EditorPlugin 继承实际实现的 ValidateWithContextAsync。
- [x] 参数范围、表达式语法、输出声明与类型检查。
- [x] 执行器重新检查表达式结果、会话存在、模式、状态和可用能力。
- [x] 生命周期检查 CAN_Open、BindAxis、SetMode、Enable；动态分支静态结果为警告，运行时严格验证。
- [x] 诊断对象、异常掩码、PDO 布局与所需反馈数据校验。

## UI

- [x] 十八个 EditorPlugin 各自独立文件，同前缀 View/ViewModel 入口。
- [x] 共享 XAML 含 TabControlExt 和业务 Tab，框架仍负责 Properties。
- [x] TabItemExt 绑定各步骤独立图标并设置宽高。
- [x] View 实现 IRefreshableEditor。
- [x] SequenceFile/EditPosition 为依赖属性，CreateEditor 传入 SequenceFile。
- [x] 表达式字段使用 ExpressionTextBox，其他配置使用对应控件。
- [x] 防抖保存、固定字段集合项 PropertyChanged 订阅及退订。
- [x] 回归验证隐藏控件不会覆盖其他字段，刷新不会覆盖外部已保存修改。
- [ ] 尚未在主程序实际编辑器界面做完整人工交互验收。

## 图标、文档与发布

- [x] 十八个独立 24px 带操作角标的步骤图标；UI 包图标 24px，顶层 icon.png 为 64px。
- [x] README.md 与 CHANGELOG.md 已提供，说明范围、序列示例、单位、停止清理和设备限制。
- [x] 发布仅构建执行/UI 项目，测试程序不进入发布包。
- [x] StepInfoExtractor 已构建并验证：CiA402 目录只提取 18 个本插件步骤，不混入私有 CAN 依赖的步骤。
- [x] 构建产物在工作区 artifacts 目录，该目录已加入忽略规则。
- [ ] 未执行发布工作流、上传或向主程序部署。

## 测试与设备边界

- [x] 45 项无硬件仿真/契约测试通过。
- [x] 覆盖状态掩码、转换、复位脉冲、模式确认、PP/PV/PT/HM 和三种周期目标。
- [x] 覆盖并发拒绝、取消清理、延后释放、超时结果、输出路径、SDO 编码及 PDO 核对。
- [x] 覆盖取消读取、SDO 迟到响应隔离、畸形响应及过期接收队列反馈。
- [x] EtherCAT 未注册工厂时明确失败，不伪装已支持主站通信。
- [x] 明确序列周期为非实时，不保证确定性周期，不引入后台周期服务。
- [x] 明确 TPDO 队列驻留年龄不等于硬件采样年龄，SDO 多次读取不是原子快照。
- [x] 功能安全由硬件链保障；软件只提供诊断和 best-effort 工艺清理。
- [ ] 未进行真实 CAN 板卡/驱动器联调；PDO/SYNC、模式兼容性、停止参数与周期抖动容忍度需现场验证。
- [ ] EtherCAT 主站仍未选型，实际适配及硬件验证不属于本次已完成交付。
