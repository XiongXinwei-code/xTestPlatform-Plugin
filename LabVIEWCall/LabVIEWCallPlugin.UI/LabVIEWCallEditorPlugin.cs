using LabVIEWCallPlugin.LVadapter;
using System.Collections.Generic;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Models;
using LabVIEWCallPlugin.UI.Views;
using MessagePack;
using MessagePack.Resolvers;
using StepEditor.Abstractions;
using System.IO;
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

        // ── 阶段四实现：上下文校验 ────────────────────────────────────────

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

                // 4. 返回类型与 LabVIEW 节点类型匹配
                if (TryMapLvType(node.Type, out var expectedKind) &&
                    !IsLvTypeMatch(expectedKind, result))
                {
                    var actual = result?.GetType().Name ?? "null";
                    errors.Add(StepSettingError.Error("LV_TYPE_MISMATCH",
                        $"{panelLabel} [{node.Name}] 类型不匹配: 期望 {node.Type}，实际 {actual}"));
                }
            }
        }

        // ── 辅助：LabVIEW 类型名 → 内部枚举 ──────────────────────────────

        private static bool TryMapLvType(string typeName, out LvExpectedKind kind)
        {
            if (string.IsNullOrWhiteSpace(typeName)) { kind = LvExpectedKind.Unknown; return false; }

            var n = NormalizeLvTypeName(typeName);
            if (n.StartsWith("enum", StringComparison.Ordinal)) { kind = LvExpectedKind.Enum; return true; }

            kind = n switch
            {
                "doublefloat" or "double" => LvExpectedKind.Double,
                "singlefloat" or "single" => LvExpectedKind.Single,
                "boolean" or "bool" => LvExpectedKind.Boolean,
                "string" => LvExpectedKind.String,
                "i8" or "int8" => LvExpectedKind.Int8,
                "u8" or "uint8" => LvExpectedKind.UInt8,
                "i16" or "int16" => LvExpectedKind.Int16,
                "u16" or "uint16" => LvExpectedKind.UInt16,
                "i32" or "int32" => LvExpectedKind.Int32,
                "u32" or "uint32" => LvExpectedKind.UInt32,
                "i64" or "int64" => LvExpectedKind.Int64,
                "u64" or "uint64" => LvExpectedKind.UInt64,
                "cluster" => LvExpectedKind.Cluster,
                "array" => LvExpectedKind.Array,
                _ => LvExpectedKind.Unknown
            };
            return kind != LvExpectedKind.Unknown;
        }

        private static bool IsLvTypeMatch(LvExpectedKind kind, object? result) => kind switch
        {
            LvExpectedKind.Double => result is double,
            LvExpectedKind.Single => result is float,
            LvExpectedKind.Boolean => result is bool,
            LvExpectedKind.String => result is string,
            LvExpectedKind.Int8 => result is sbyte,
            LvExpectedKind.UInt8 => result is byte,
            LvExpectedKind.Int16 => result is short,
            LvExpectedKind.UInt16 => result is ushort,
            LvExpectedKind.Int32 => result is int,
            LvExpectedKind.UInt32 => result is uint,
            LvExpectedKind.Int64 => result is long,
            LvExpectedKind.UInt64 => result is ulong,
            LvExpectedKind.Enum => result is sbyte or byte or short or ushort
                                           or int or uint or long or ulong or string,
            LvExpectedKind.Cluster => result is IDictionary<string, object?>,
            LvExpectedKind.Array => result is System.Collections.IEnumerable and not string,
            _ => true
        };

        private static string NormalizeLvTypeName(string s) =>
            new string(s.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray())
                .ToLowerInvariant();

        private static IEnumerable<LvPanelNode> EnumerateNodes(IEnumerable<LvPanelNode> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;
                foreach (var child in EnumerateNodes(node.Children))
                    yield return child;
            }
        }

        private enum LvExpectedKind
        {
            Unknown, Boolean, String, Double, Single,
            Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64,
            Enum, Cluster, Array
        }
    }
}