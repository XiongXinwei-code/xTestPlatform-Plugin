using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using System.IO;
using LabVIEWCallPlugin.PluginInfrastructure;
using PluginCommon;
using xTestPlatform.Core.Plugins.Contracts;

namespace LabVIEWCallPlugin
{
    /// <summary>
    /// LabVIEW Call 步骤插件（核心层）。
    /// 只负责执行器创建和元数据描述，不含任何 WPF / UI 依赖。
    /// 编辑器负责上下文校验，执行器在实际调用前独立校验。
    /// </summary>
    public class LabVIEWCallStepPlugin : StrictStepPluginBase<LabVIEWCallSetting>
    {
        protected override int CurrentSettingVersion => 1;

        public const string TypeId = "Adapter.LabVIEWCall";
        public override string StepTypeId => TypeId;
        public override string DisplayName => "LabVIEW_Execution_Call VI";

        public override string Description => """
            ## 功能

            通过已安装并打开的本机 LabVIEW 开发环境调用 .vi 文件，读取输入输出端子并交换参数。目标开发环境的版本与位数不作固定限制；它必须能够打开所选 VI，并启用本机 TCP/IP VI Server。本步骤不提供仅 Runtime 或 .lvlibp 调用。

            ## 使用前

            1. 安装并打开能够运行目标 VI 的 LabVIEW 开发环境，调用期间保持打开。仅安装 Runtime 不满足本步骤要求。
            2. 在 LabVIEW 的 Tools → Options → VI Server 中启用 TCP/IP，允许本机 127.0.0.1 访问，并允许调用和读取本次需要的 VI。
            3. 将步骤的 VI Server port 填为该 LabVIEW 实例监听的端口，默认 3363。例如开发环境使用 3365，步骤也填写 3365。
            4. 确认目标 VI 能正常打开、依赖完整。先处理开发环境中的保存改动、缺失依赖等对话框；端口存在并不保证它正在处理调用请求。

            ## 参数

            | 参数 | 类型 | 必填 | 默认值 | 说明 |
            |------|------|------|--------|------|
            | ViFilePath | string | 是 | — | VI 文件完整路径 |
            | ViServerPort | int | 是 | 3363 | 已启动 LabVIEW 开发环境的 VI Server TCP 端口，范围 1 至 65535；固定连接本机 127.0.0.1 |
            | ShowPanel | bool | 否 | false | 调用时是否显示前面板 |
            | ClosePanel | bool | 否 | false | 执行完毕后是否关闭前面板 |
            | TimeoutMs | int | 否 | 5000 | 绑定、调用和输出发布的等待期限（毫秒）；显式 0 允许无限等待 |
            | InputParameters | string | 否 | 空 | 输入端子的 JSON 文本；可在编辑器生成，也可由助手按下述字段读写 |
            | OutputParameters | string | 否 | 空 | 输出端子的 JSON 文本；Variable 为已声明的完整平台变量路径，如 Locals.ViResult |

            ## 行为

            - 每个端子条目由路径段列表与节点组成。Name 是端子名称，Type 是面板返回的类型，Value 是常量文本。ValueSourceType=Variable 时，输入 Variable 填写表达式；输出 Variable 仅填写可写变量路径。ChildNodePath 依序引用簇的子端子。数组常量用 JSON 数组文本。保留端子的实际名称、类型及子字段顺序。
            - 通过随包 bridge 子目录中的独立 x64 辅助进程访问 VI；辅助进程及其原生依赖需要 x64 运行环境，与目标 LabVIEW IDE 的版本和位数选择不同。目标地址为 127.0.0.1:ViServerPort；LabVIEW 应保持运行并启用 TCP/IP。检查端口的 LabVIEW 进程身份，并在请求前后核对同一实例，禁止跨实例复用 VI 引用或发布结果。调用前校验路径、端口和参数；端子类型、长度、求值或绑定不符时返回错误。标准错误簇 status 为真时保留 code/source。
            - 打开设置页只显示已保存的端子映射，不表示已在线连接或连接失败；选择 VI、刷新端子和编辑 VI 才访问开发环境。修改端口或 VI 路径后取消旧页面请求，旧结果不得覆盖新配置。修改参数约 200 ms 后保存，失焦或离开页面时立即保存，并通过宿主命令刷新步骤描述。
            - 原生库异常退出会显示辅助进程 PID、阶段和退出码并保留配置；确认目标 VI 状态后重启平台。选择 VI、刷新端子和执行均使用同一串行入口。
            - 执行前在线核对输入、输出及簇字段。请显式配置当前 VI 的所有输入，包括需要使用默认值的端子。普通输出可选择映射；顶层标准错误簇即使不映射也会检查。status 为假但 code 非零时记录 Warning。
            - 同一插件实例所在进程的本机桥入口串行使用。中断或超时后，已提交的调用可能继续执行；在实际返回前拒绝新的 VI 调用。停止序列和关闭前面板均不能确认设备已停机。需要协作停止的 VI 应在其程序内处理。

            ## 调试日志与结果

            - “已配置输入/输出端子数”是顶层端子数量，不是参数值；簇的子项不重复计数。
            - “已写入平台变量”是本次成功发布的变量数量，可能与输出端子数量不同。未映射输出不会写入变量。
            - “总耗时”包含校验、参数传递、VI 执行和输出发布，不是 VI 的纯运行时间。完整路径、端口和调用标识见日志详情。
            - Completed 表示调用完成，不代表测试判定通过；需要判定输出时，另用序列断言步骤。

            ## 相关插件

            VI 内部负责它创建的设备资源。直接操作采集或仪器可使用 NI DAQ 或 VISA 步骤。
            """;
        public override string Category => "Adapter";
        public override string IconPath =>
                        "pack://application:,,,/LabVIEWCall.StepPlugin.UI;component/Resources/Icons/labview.png";

        public override IStepExecutor CreateExecutor() =>
            ExecutionScopedExecutor.Create("LabVIEWCall", static () => new LabVIEWCallExecutor());

        public override string GenerateDescription(byte[] setting)
        {
            var s = DeserializeSetting(setting);
            return string.IsNullOrWhiteSpace(s.ViFilePath)
                ? $"LabVIEW Call (127.0.0.1:{s.ViServerPort})"
                : $"Call: {Path.GetFileName(s.ViFilePath)} (127.0.0.1:{s.ViServerPort})";
        }
    }
}
