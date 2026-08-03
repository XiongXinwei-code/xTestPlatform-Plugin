using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.LVadapter.Interop;
using MessagePack;
using MessagePack.Resolvers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace LabVIEWCallPlugin.Execution
{
    /// <summary>
    /// LabVIEW Call 步骤执行器。
    /// 支持树形参数结构（标量、数组、簇及嵌套簇）。
    /// 使用 LvFlattenHelper（Flatten To String 格式）与 lvLib.dll 交互，无非托管内存分配。
    /// </summary>
    public sealed class LabVIEWCallExecutor : IStepExecutor
    {
        private static readonly MessagePackSerializerOptions _msgpackOptions =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<ExecutionResult> ExecuteAsync(
            IExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            // ── 1. 反序列化步骤设置 ──────────────────────────────────────
            var rawSetting = context.CurrentStep?.Step?.StepSetting?.Setting;
            LabVIEWCallSetting setting;
            try
            {
                setting = rawSetting is { Length: > 0 }
                    ? MessagePackSerializer.Deserialize<LabVIEWCallSetting>(rawSetting, _msgpackOptions)
                    : new LabVIEWCallSetting();
            }
            catch (Exception ex)
            {
                return ErrorResult($"反序列化 LabVIEWCallSetting 失败: {ex.Message}");
            }

            // ── 2. 前置校验 ──────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(setting.ViFilePath))
                return ErrorResult("VI 文件路径未配置");

            if (!File.Exists(setting.ViFilePath))
                return ErrorResult($"VI 文件不存在: {setting.ViFilePath}");

            // ── 3. 解析树形参数 JSON ─────────────────────────────────────
            List<ViParameterNode> inputs;
            List<ViParameterNode> outputs;
            try
            {
                inputs = ParseParameterNodes(setting.InputParameters);
                outputs = ParseParameterNodes(setting.OutputParameters);
            }
            catch (Exception ex)
            {
                return ErrorResult($"参数 JSON 解析失败: {ex.Message}");
            }

            // ── 4. 运行时变量绑定：ValueSourceType == Variable 时从 context 取实际值 ──
            ResolveInputVariables(inputs, context);

            // ── 5. 在线程池执行 VI ───────────────────────────────────────
            try
            {
                var outputValues = await Task.Run(
                    () => ExecuteVI(setting, inputs, outputs),
                    cancellationToken);

                foreach (var (path, value) in outputValues)
                    context.SetVariable(path, value);

                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Passed }
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return ErrorResult($"VI 执行异常: {ex.Message}"); }
        }

        // ── VI 执行核心 ───────────────────────────────────────────────────

        /// <summary>
        /// 加载 VI → 设置输入参数 → 运行 → 读取输出参数。
        /// 全程使用 Flatten To String 格式，无非托管内存分配，无需手动释放。
        /// </summary>
        private static Dictionary<string, object?> ExecuteVI(
            LabVIEWCallSetting setting,
            List<ViParameterNode> inputs,
            List<ViParameterNode> outputs)
        {
            IntPtr viRef = LvLibHelper.LoadVI(setting.ViFilePath);

            // 设置输入参数
            foreach (var input in inputs)
            {
                var (ts, ds) = EncodeNodeToFlat(input);
                LvLibHelper.SetVIParameter(viRef, input.Name, ts, ds);
            }

            // 运行 VI
            LvLibHelper.RunVI(viRef,
                openPanel: setting.ShowPanel,
                closePanel: setting.ClosePanel);

            // 读取输出参数
            var result = new Dictionary<string, object?>();
            foreach (var output in outputs)
            {
                var (_, ds) = LvLibHelper.GetVIParameterRaw(viRef, output.Name);
                int offset = 0;
                object? value = DecodeNodeFromFlat(output, ds, ref offset);
                CollectOutputVariables(output, value, result);
            }

            return result;
        }

        // ── 运行时变量绑定 ────────────────────────────────────────────────

        /// <summary>
        /// 递归遍历输入节点树，对 ValueSourceType == "Variable" 的叶节点
        /// 从 context 读取运行时值覆盖静态 Value，确保变量引用（如 RunState.ThisContext）
        /// 在执行时使用真实值而非编辑器保存的静态占位值。
        /// </summary>
        private static void ResolveInputVariables(
            List<ViParameterNode> nodes, IExecutionContext context)
        {
            foreach (var node in nodes)
            {
                if (node.DataType == ViDataType.Cluster)
                {
                    // 簇节点递归处理子字段
                    if (node.Children is { Count: > 0 })
                        ResolveInputVariables(node.Children, context);
                    continue;
                }

                // 只有 ValueSourceType == "Variable" 才从 context 取值
                if (!string.Equals(node.ValueSourceType, "Variable",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(node.TargetVariable))
                    continue;

                var runtimeVal = context.GetVariable(node.TargetVariable);
                if (runtimeVal is null) continue;

                // 将运行时值转换为对应的 JsonElement，供 GetXxx() 读取
                node.Value = ConvertToJsonElement(
                    Convert.ToString(runtimeVal,
                        System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    MapViDataTypeToLvType(node.DataType));
            }
        }

        /// <summary>ViDataType 常量 → LV 类型字符串（供 ConvertToJsonElement 使用）</summary>
        private static string MapViDataTypeToLvType(string viDataType) => viDataType switch
        {
            ViDataType.Int32 => "I32",
            ViDataType.Float64 => "Double Float",
            ViDataType.Boolean => "Boolean",
            ViDataType.String => "String",
            ViDataType.Int32Array => "I32 Array",
            ViDataType.Float64Array => "DBL Array",
            ViDataType.StringArray => "String Array",
            _ => "String"
        };

        // ── Flatten 编码（节点树 → typeString + dataString） ──────────────

        /// <summary>
        /// 根据节点类型递归编码为 LabVIEW Flatten To String 格式。
        /// 返回 (typeString, dataString) 元组，均为纯托管数组，无需释放。
        /// </summary>
        private static (short[] TypeStr, byte[] DataStr) EncodeNodeToFlat(ViParameterNode node)
        {
            return node.DataType switch
            {
                ViDataType.Boolean => (LvFlattenHelper.BooleanTypeString,
                                           LvFlattenHelper.Encode(node.GetBool())),

                ViDataType.Int32 => (LvFlattenHelper.I32TypeString,
                                           LvFlattenHelper.Encode(node.GetInt32())),

                ViDataType.Float64 => (LvFlattenHelper.Float64TypeString,
                                           LvFlattenHelper.Encode(node.GetDouble())),

                ViDataType.String => (LvFlattenHelper.StringTypeString,
                                           LvFlattenHelper.Encode(node.GetString())),

                ViDataType.Int32Array => (LvFlattenHelper.I32Array1DTypeString,
                                           LvFlattenHelper.EncodeArray(node.GetInt32Array())),

                ViDataType.Float64Array => (LvFlattenHelper.Float64Array1DTypeString,
                                            LvFlattenHelper.EncodeArray(node.GetFloat64Array())),

                ViDataType.StringArray => (LvFlattenHelper.StringArray1DTypeString,
                                           LvFlattenHelper.EncodeArray(node.GetStringArray())),

                ViDataType.Cluster => EncodeClusterNode(node.Children ?? []),

                _ => throw new NotSupportedException(
                    $"节点 [{node.Name}] 的 DataType [{node.DataType}] 不支持编码")
            };
        }

        /// <summary>
        /// 递归编码簇节点：
        /// 1. 递归编码每个子字段 → 得到各自的 (typeStr, dataStr)
        /// 2. 用 MakeClusterTypeStringFromDescs 拼接簇类型描述符
        /// 3. 用 EncodeCluster 拼接簇数据（直接串联，无长度前缀）
        /// </summary>
        private static (short[], byte[]) EncodeClusterNode(List<ViParameterNode> children)
        {
            if (children.Count == 0)
                throw new InvalidOperationException("Cluster 节点的 Children 不能为空");

            var fieldTDs = new short[children.Count][];
            var fieldDatas = new byte[children.Count][];

            for (int i = 0; i < children.Count; i++)
            {
                var (ts, ds) = EncodeNodeToFlat(children[i]);  // 递归
                fieldTDs[i] = ts;
                fieldDatas[i] = ds;
            }

            return (
                LvFlattenHelper.MakeClusterTypeStringFromDescs(fieldTDs),
                LvFlattenHelper.EncodeCluster(fieldDatas)
            );
        }

        // ── 输出变量收集 ──────────────────────────────────────────────────

        /// <summary>
        /// 递归收集输出变量赋值。
        /// - 叶节点：直接按 TargetVariable（或 Step.Name 兜底）写入 result。
        /// - 簇节点：若自身有 TargetVariable 则整体写入；
        ///           同时递归处理每个子节点，子节点有 TargetVariable 时单独写入。
        /// 这样既支持"整簇赋给一个变量"，也支持"只取簇中某个字段赋给变量"。
        /// </summary>
        private static void CollectOutputVariables(
            ViParameterNode node,
            object? value,
            Dictionary<string, object?> result)
        {
            if (node.DataType == ViDataType.Cluster)
            {
                // 簇本身有 TargetVariable → 整体赋值
                if (!string.IsNullOrWhiteSpace(node.TargetVariable))
                    result[node.TargetVariable] = value;

                // 递归处理子节点（子节点有独立 TargetVariable 时单独赋值）
                var clusterDict = value as Dictionary<string, object?>;
                foreach (var child in node.Children ?? [])
                {
                    object? childValue = clusterDict is not null &&
                                        clusterDict.TryGetValue(child.Name, out var cv)
                                        ? cv : null;
                    CollectOutputVariables(child, childValue, result);
                }
            }
            else
            {
                // 叶节点
                if (string.IsNullOrWhiteSpace(node.TargetVariable))
                    return;

                result[node.TargetVariable] = value;
            }
        }


        // ── Flatten 解码（dataString → 节点树值） ─────────────────────────

        /// <summary>
        /// 根据节点结构定义，从展开字节流的 <paramref name="offset"/> 处顺序解码。
        /// 解码完成后 <paramref name="offset"/> 自动推进到下一字段起始位置。
        /// 簇返回 <see cref="Dictionary{String, Object}"/>；叶节点返回对应 .NET 值。
        /// </summary>
        private static object? DecodeNodeFromFlat(
            ViParameterNode node, byte[] data, ref int offset)
        {
            switch (node.DataType)
            {
                case ViDataType.Boolean:
                    {
                        bool v = LvFlattenHelper.DecodeBoolean(data, offset);
                        offset += 1;
                        return v;
                    }
                case ViDataType.Int32:
                    {
                        int v = LvFlattenHelper.DecodeInt32(data, offset);
                        offset += 4;
                        return v;
                    }
                case ViDataType.Float64:
                    {
                        double v = LvFlattenHelper.DecodeFloat64(data, offset);
                        offset += 8;
                        return v;
                    }
                case ViDataType.String:
                    {
                        // LabVIEW String: 4字节长度（大端）+ 字节流
                        int byteLen = LvFlattenHelper.DecodeInt32(data, offset);
                        string v = LvFlattenHelper.DecodeString(data, offset);
                        offset += 4 + byteLen;
                        return v;
                    }
                case ViDataType.Int32Array:
                    {
                        // 4字节计数 + count × 4字节元素
                        int count = LvFlattenHelper.DecodeInt32(data, offset);
                        offset += 4;
                        var arr = new int[count];
                        for (int j = 0; j < count; j++)
                        {
                            arr[j] = LvFlattenHelper.DecodeInt32(data, offset);
                            offset += 4;
                        }
                        return arr;
                    }
                case ViDataType.Float64Array:
                    {
                        int count = LvFlattenHelper.DecodeInt32(data, offset);
                        offset += 4;
                        var arr = new double[count];
                        for (int j = 0; j < count; j++)
                        {
                            arr[j] = LvFlattenHelper.DecodeFloat64(data, offset);
                            offset += 8;
                        }
                        return arr;
                    }
                case ViDataType.StringArray:
                    {
                        int count = LvFlattenHelper.DecodeInt32(data, offset);
                        offset += 4;
                        var arr = new string[count];
                        for (int j = 0; j < count; j++)
                        {
                            int byteLen = LvFlattenHelper.DecodeInt32(data, offset);
                            arr[j] = LvFlattenHelper.DecodeString(data, offset);
                            offset += 4 + byteLen;
                        }
                        return arr;
                    }
                case ViDataType.Cluster:
                    {
                        // 簇：按 Children 定义顺序依次解码（与编码时的字段顺序一致）
                        var dict = new Dictionary<string, object?>();
                        foreach (var child in node.Children ?? [])
                            dict[child.Name] = DecodeNodeFromFlat(child, data, ref offset);  // 递归
                        return dict;
                    }
                default:
                    throw new NotSupportedException(
                        $"节点 [{node.Name}] 的 DataType [{node.DataType}] 不支持解码");
            }
        }

        // ── JSON 解析 ─────────────────────────────────────────────────────

        /// <summary>
        /// 解析 LvPanelConverter 扁平格式 JSON → ViParameterNode 树。
        /// 格式: [ [pathArray, {Name, Type, Value, Variable, ValueSourceType, ChildNodePath, ...}], ... ]
        /// </summary>
        private static List<ViParameterNode> ParseParameterNodes(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array) return [];

            // ── 第一步：建立 pathKey → (节点, 子路径键列表) 字典 ──────────────
            var nodeDict = new Dictionary<string, (ViParameterNode Node, List<string> ChildKeys)>();

            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2) continue;

                var pathSegments = new List<string>();
                if (item[0].ValueKind == JsonValueKind.Array)
                    foreach (var seg in item[0].EnumerateArray())
                        pathSegments.Add(seg.GetString() ?? "");

                if (pathSegments.Count == 0) continue;
                var pathKey = string.Join("|", pathSegments);

                var data = item[1];
                string name = data.TryGetProperty("Name", out var np) ? np.GetString() ?? "" : "";
                string type = data.TryGetProperty("Type", out var tp) ? tp.GetString() ?? "" : "";
                string value = data.TryGetProperty("Value", out var vp) ? vp.GetString() ?? "" : "";
                string variable = data.TryGetProperty("Variable", out var vr) ? vr.GetString() ?? "" : "";
                string valueSourceType = data.TryGetProperty("ValueSourceType", out var vs) ? vs.GetString() ?? "Constant" : "Constant";

                var childKeys = new List<string>();
                if (data.TryGetProperty("ChildNodePath", out var cnp) &&
                    cnp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cp in cnp.EnumerateArray())
                    {
                        if (cp.ValueKind != JsonValueKind.Array) continue;
                        var segs = cp.EnumerateArray()
                                     .Select(s => s.GetString() ?? "")
                                     .ToList();
                        if (segs.Count > 0)
                            childKeys.Add(string.Join("|", segs));
                    }
                }

                var dataType = MapLvType(type);
                var viNode = new ViParameterNode
                {
                    Name = name,
                    DataType = dataType,
                    TargetVariable = string.IsNullOrWhiteSpace(variable) ? null : variable,
                    ValueSourceType = valueSourceType,
                    Value = dataType != ViDataType.Cluster
                                         ? ConvertToJsonElement(value, type)
                                         : default,
                };

                nodeDict[pathKey] = (viNode, childKeys);
            }

            // ── 第二步：为 Cluster 节点挂载子节点 ──────────────────────────────
            foreach (var (node, childKeys) in nodeDict.Values)
            {
                if (node.DataType != ViDataType.Cluster || childKeys.Count == 0) continue;

                node.Children = [];
                foreach (var ck in childKeys)
                    if (nodeDict.TryGetValue(ck, out var child))
                        node.Children.Add(child.Node);
            }

            // ── 第三步：返回根节点（不出现在任何 ChildKeys 中的节点）─────────────
            var childSet = new HashSet<string>(nodeDict.Values.SelectMany(v => v.ChildKeys));

            return nodeDict
                .Where(kv => !childSet.Contains(kv.Key))
                .Select(kv => kv.Value.Node)
                .ToList();
        }

        /// <summary>LV 类型字符串 → ViDataType 常量</summary>
        private static string MapLvType(string lvType) => lvType switch
        {
            "Double Float" or "Single Float" => ViDataType.Float64,
            "I32" or "I16" or "I8" or "U32" or "U16" or "U8" => ViDataType.Int32,
            "Boolean" => ViDataType.Boolean,
            "String" => ViDataType.String,
            "Cluster" => ViDataType.Cluster,
            "I32 Array" or "Int32[]" => ViDataType.Int32Array,
            "DBL Array" or "Float64[]" => ViDataType.Float64Array,
            "String Array" or "String[]" => ViDataType.StringArray,
            _ => ViDataType.Float64
        };

        /// <summary>LvPanelNode.Value 字符串 → JsonElement（供 GetXxx() 调用）</summary>
        private static JsonElement ConvertToJsonElement(string value, string lvType)
        {
            try
            {
                return lvType switch
                {
                    "Boolean" =>
                        JsonSerializer.SerializeToElement(
                            string.Equals(value, "True", StringComparison.OrdinalIgnoreCase)),

                    "I32" or "I16" or "I8" or "U32" or "U16" or "U8" =>
                        JsonSerializer.SerializeToElement(
                            int.TryParse(value, out int i) ? i : 0),

                    "Double Float" or "Single Float" =>
                        JsonSerializer.SerializeToElement(
                            double.TryParse(value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double d) ? d : 0.0),

                    "String" =>
                        JsonSerializer.SerializeToElement(value.Trim('"')),

                    _ => JsonSerializer.SerializeToElement(value)
                };
            }
            catch { return default; }
        }

        // ── 公共辅助 ─────────────────────────────────────────────────────────

        private static ExecutionResult ErrorResult(string message) => new()
        {
            StepResult = new StepResult
            {
                Status = TestStatus.Error,
                Error = new ErrorInfo { Message = message }
            }
        };

        // ── 参数类型枚举 ─────────────────────────────────────────────────────

        internal static class ViDataType
        {
            public const string Boolean = "Boolean";
            public const string Int32 = "Int32";
            public const string Float64 = "Float64";
            public const string String = "String";
            public const string Cluster = "Cluster";
            public const string Int32Array = "Int32[]";
            public const string Float64Array = "Float64[]";
            public const string StringArray = "String[]";
        }

        // ── 树形参数节点 ─────────────────────────────────────────────────────

        /// <summary>
        /// 树形参数节点（递归）。
        /// 叶节点：Value 携带值；簇节点：Children 携带子字段。
        /// </summary>
        internal sealed class ViParameterNode
        {
            /// <summary>控件 / 指示器 / 簇字段名称</summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>数据类型（见 <see cref="ViDataType"/>）</summary>
            public string DataType { get; set; } = ViDataType.Float64;

            /// <summary>
            /// 叶节点的值（JSON 原始元素）。
            /// 簇节点此字段为 null，使用 Children。
            /// ValueSourceType == "Variable" 时，由 ResolveInputVariables 在运行时覆盖此值。
            /// </summary>
            public JsonElement? Value { get; set; }

            /// <summary>簇的子字段（DataType = Cluster 时有效，可嵌套）</summary>
            public List<ViParameterNode>? Children { get; set; }

            /// <summary>
            /// 变量路径。
            /// 输入节点：ValueSourceType == "Variable" 时，从此路径读取运行时值。
            /// 输出节点：VI 运行结束后将结果写入此路径。
            /// </summary>
            public string? TargetVariable { get; set; }

            /// <summary>
            /// 数据来源类型："Constant" | "Variable" | "FromParameterFile"
            /// 与 UI 层 ValueSourceType 枚举对应。
            /// "Variable" 时 ResolveInputVariables 从 context 取运行时值覆盖 Value。
            /// </summary>
            public string ValueSourceType { get; set; } = "Constant";

            // ── 值提取辅助 ────────────────────────────────────────────────

            public bool GetBool() => Value?.GetBoolean() ?? false;
            public int GetInt32() => Value?.GetInt32() ?? 0;
            public double GetDouble() => Value?.GetDouble() ?? 0.0;
            public string GetString() => Value?.GetString() ?? string.Empty;

            public int[] GetInt32Array() => Value?.Deserialize<int[]>() ?? [];
            public double[] GetFloat64Array() => Value?.Deserialize<double[]>() ?? [];
            public string[] GetStringArray() => Value?.Deserialize<string[]>() ?? [];
        }
    }
}