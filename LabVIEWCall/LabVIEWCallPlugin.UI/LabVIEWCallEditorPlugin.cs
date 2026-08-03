using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Helper;          // 引入 LabVIEWTypeConverter
using LabVIEWCallPlugin.UI.Models;
using LabVIEWCallPlugin.UI.Views;
using MessagePack;
using MessagePack.Resolvers;
using StepEditor.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LabVIEWCallPlugin.UI
{
    /// <summary>
    /// LabVIEW Call 编辑器插件（UI 层）。
    /// 实现 WPF 编辑器创建和完整的步骤校验逻辑。
    /// </summary>
    public sealed class LabVIEWCallEditorPlugin : IStepEditorPlugin
    {
        private static readonly MessagePackSerializerOptions _opts =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        // ── IStepEditorPlugin ─────────────────────────────────────────

        public string StepTypeId => "LabVIEWCall";

        public string IconPath =>
            "pack://application:,,,/LabVIEWCall.StepPlugin.UI;component/Resources/Icons/labview.png";

        public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
            => new LabVIEWCallEditorView(step, sequenceFile);

        // ── 完整校验（三个阶段）─────────────────────────────────────────

        public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
            byte[] setting,
            IExpressionEvaluator evaluator,
            IExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var errors = new List<StepSettingError>();

            LabVIEWCallSetting s;
            try
            {
                s = setting is { Length: > 0 }
                    ? MessagePackSerializer.Deserialize<LabVIEWCallSetting>(setting, _opts)
                    : new LabVIEWCallSetting();
            }
            catch { return errors; }

            // ── 阶段零：路径 / 文件存在性校验（无需 DLL）───────────────────
            if (string.IsNullOrWhiteSpace(s.ViFilePath))
            {
                errors.Add(StepSettingError.Error("LV_VI_PATH_EMPTY",
                    "VI 文件路径为空，请选择要调用的 VI 文件。"));
                return errors;
            }

            if (!File.Exists(s.ViFilePath))
            {
                errors.Add(StepSettingError.Error("LV_VI_PATH_NOT_FOUND",
                    $"VI 文件不存在: {s.ViFilePath}"));
                return errors;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ── 阶段一：检查 LabVIEW 开发环境是否已打开（ConnectLabVIEW）───
            bool ideConnected = await Task.Run(() => LvLibHelper.ConnectLabVIEW(), cancellationToken);

            if (!ideConnected)
            {
                errors.Add(StepSettingError.Error("LV_NOT_CONNECTED",
                    "LabVIEW 开发环境未打开或无法连接，已跳过 DLL 相关校验。"));
                return errors;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ── 阶段二：VI 状态校验（依赖 DLL）──────────────────────────────
            try
            {
                var viState = await Task.Run(
                    () => LvLibHelper.GetConnectPanel(viPath: s.ViFilePath).VIState,
                    cancellationToken);

                if (viState == VIState.Bad)
                    errors.Add(StepSettingError.Error("LV_VI_STATE_BAD",
                        $"VI 状态为 Bad，无法正常调用: {Path.GetFileName(s.ViFilePath)}"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                errors.Add(StepSettingError.Warning("LV_VI_LOAD_ERROR",
                    $"VI 加载时发生异常: {ex.Message}"));
            }

            // VI 状态有 Error 级别时无需继续
            if (errors.Any(e => e.Severity == StepSettingErrorSeverity.Error))
                return errors;

            // ── 阶段三：面板节点基础校验（源类型 / 变量为空）─────────────────
            ValidatePanelNodesBasic(s.InputParameters, "输入控件", errors, isOutput: false);
            ValidatePanelNodesBasic(s.OutputParameters, "输出指示器", errors, isOutput: true);

            // ── 阶段四：上下文校验（变量定义 / 表达式 / 类型匹配）────────────
            await ValidatePanelNodesWithContextAsync(
                s.InputParameters, "输入控件", evaluator, context, errors, cancellationToken, isOutput: false);
            await ValidatePanelNodesWithContextAsync(
                s.OutputParameters, "输出指示器", evaluator, context, errors, cancellationToken, isOutput: true);

            return errors;
        }

        // ── 阶段三实现：基础校验 ──────────────────────────────────────────

        private static void ValidatePanelNodesBasic(
            string json, string panelLabel, List<StepSettingError> errors, bool isOutput = false)
        {
            foreach (var node in EnumerateNodes(LvPanelConverter.ConvertFromJson(json)))
            {
                if (isOutput)
                {
                    // 输出指示器：忽略源类型，只检查 Variable 是否填写
                    if (string.IsNullOrWhiteSpace(node.Variable))
                        errors.Add(StepSettingError.Warning("LV_VARIABLE_EMPTY",
                            $"{panelLabel} [{node.Name}] 未指定目标变量，输出结果将被丢弃。"));
                }
                else
                {
                    // 输入控件：检查源类型与 Variable 的一致性
                    if (node.ValueSourceTypeEnum != ValueSourceType.Variable)
                    {
                        if (!string.IsNullOrWhiteSpace(node.Variable))
                            errors.Add(StepSettingError.Warning("LV_VALUE_SOURCE_MISMATCH",
                                $"{panelLabel} [{node.Name}] 源类型不是变量，但 Variable 已填写: {node.Variable}"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.Variable))
                        errors.Add(StepSettingError.Warning("LV_VARIABLE_EMPTY",
                            $"{panelLabel} [{node.Name}] 源类型是变量，但 Variable 为空。"));
                }
            }
        }

        // ── 阶段四实现：上下文校验（使用 LabVIEWTypeConverter）───────────

        private static async Task ValidatePanelNodesWithContextAsync(
            string json, string panelLabel,
            IExpressionEvaluator evaluator, IExecutionContext context,
            List<StepSettingError> errors, CancellationToken ct,
            bool isOutput = false)
        {
            foreach (var node in EnumerateNodes(LvPanelConverter.ConvertFromJson(json)))
            {
                // 输入控件：仅源类型为 Variable 时校验
                // 输出指示器：只要 Variable 有值就校验（输出只能写入变量）
                if (!isOutput && node.ValueSourceTypeEnum != ValueSourceType.Variable) continue;
                if (string.IsNullOrWhiteSpace(node.Variable)) continue;

                ct.ThrowIfCancellationRequested();
                var expr = node.Variable;

                // 1. 引用变量是否已定义
                foreach (var varName in evaluator.GetReferencedVariables(expr).Distinct())
                    if (!context.HasVariable(varName))
                        errors.Add(StepSettingError.Error("LV_VARIABLE_NOT_FOUND",
                            $"{panelLabel} [{node.Name}] 使用了未定义变量: {varName}"));

                // 2. 表达式语法校验
                if (!evaluator.ValidateExpression(expr, context, out var syntaxErr))
                {
                    errors.Add(StepSettingError.Error("LV_SCRIPT_SYNTAX_ERROR",
                        $"{panelLabel} [{node.Name}] 脚本语法错误: {syntaxErr}"));
                    continue;
                }

                // 3. 表达式执行
                object? result;
                try
                {
                    result = await evaluator.EvaluateAsync(expr, context);
                }
                catch (Exception ex)
                {
                    errors.Add(StepSettingError.Error("LV_SCRIPT_EXECUTION_ERROR",
                        $"{panelLabel} [{node.Name}] 脚本执行错误: {ex.Message}"));
                    continue;
                }

                // 4. 返回类型与 LabVIEW 节点类型匹配（使用平台 VariableDataType）
                var expectedType = LabVIEWTypeConverter.Convert(node.Type);
                if (expectedType != VariableDataType.Dynamic &&   // Dynamic 匹配任何类型
                    !IsCompatibleWith(expectedType, result))
                {
                    var actual = result?.GetType().Name ?? "null";
                    errors.Add(StepSettingError.Error("LV_TYPE_MISMATCH",
                        $"{panelLabel} [{node.Name}] 类型不匹配: 期望 {node.Type}（平台 {expectedType}），实际 {actual}"));
                }
            }
        }

        // ── 类型兼容性检查（基于 VariableDataType）────────────────────

        /// <summary>
        /// 检查表达式计算结果是否与 LabVIEW 控件期望的数据类型兼容。
        /// LabVIEW 通过其 .NET 适配器接收数据，不同类型的接收规则如下。
        /// </summary>
        private static bool IsCompatibleWith(VariableDataType expected, object? result)
        {
            if (result == null)
                return true; // null 可以传给任何控件（LabVIEW 会使用默认值）

            Type t = result.GetType();

            return expected switch
            {
                // 整数类型
                VariableDataType.SByte => t == typeof(sbyte),
                VariableDataType.Byte => t == typeof(byte),
                VariableDataType.Short => t == typeof(short),
                VariableDataType.UShort => t == typeof(ushort),
                VariableDataType.Int => t == typeof(int),
                VariableDataType.UInt => t == typeof(uint),
                VariableDataType.Long => t == typeof(long),
                VariableDataType.ULong => t == typeof(ulong),

                // 浮点类型
                VariableDataType.Float => t == typeof(float),
                VariableDataType.Double => t == typeof(double),

                // 基础类型
                VariableDataType.Bool => t == typeof(bool),
                VariableDataType.String => t == typeof(string),

                // 枚举：LabVIEW 接收字符串（当前值）或对应的底层整数
                VariableDataType.Enum => t == typeof(string) ||
                                         t == typeof(sbyte) || t == typeof(byte) ||
                                         t == typeof(short) || t == typeof(ushort) ||
                                         t == typeof(int) || t == typeof(uint) ||
                                         t == typeof(long) || t == typeof(ulong),

                // 结构体（Cluster）：接受 IDictionary<string, object?> 或具有无参构造的复合对象
                VariableDataType.Struct => (t.IsGenericType &&
                                            t.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                                            t.GetGenericArguments()[0] == typeof(string)) ||
                                           t.GetConstructor(Type.EmptyTypes) != null,

                // 列表（1D 数组）：接受 IEnumerable（非字符串），且元素类型可进一步细化
                VariableDataType.List or VariableDataType.ListBool or VariableDataType.ListInt or
                VariableDataType.ListLong or VariableDataType.ListFloat or VariableDataType.ListDouble or
                VariableDataType.ListString or VariableDataType.ListDynamic or VariableDataType.ListByte
                    => t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t),

                // 矩阵（2D 数组）：接受 rank=2 的数组或多维集合
                VariableDataType.Matrix or VariableDataType.MatrixBool or VariableDataType.MatrixInt or
                VariableDataType.MatrixLong or VariableDataType.MatrixFloat or VariableDataType.MatrixDouble or
                VariableDataType.MatrixString
                    => t.IsArray && t.GetArrayRank() == 2,

                // Dynamic / Object / Expression / Reference：不做限制
                VariableDataType.Dynamic or VariableDataType.Object or
                VariableDataType.Expression or VariableDataType.Reference
                    => true,

                _ => false
            };
        }

        // ── 辅助：枚举所有子节点 ────────────────────────────────────────

        private static IEnumerable<LvPanelNode> EnumerateNodes(IEnumerable<LvPanelNode> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;
                foreach (var child in EnumerateNodes(node.Children))
                    yield return child;
            }
        }
    }
}