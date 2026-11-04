using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Simulates the installed agent's format/convert calls on detached settings. No executor or CAN resources.
internal static class DbcBusinessProbe
{
    private const string TxTask = "\"BusinessTx\"";
    private const string RxTask = "\"BusinessRx\"";
    private const string Connection = "\"BenchCAN\"";
    private const string Fixture = """
        BO_ 817 StandardCmd: 16 ECU
         SG_ Demand : 16|16@1+ (0.5,0) [0|100] "V" ECU
        BO_ 2147484465 ExtendedCmd: 8 ECU
         SG_ Sample : 16|16@1+ (1,0) [0|65535] "rpm" ECU
        BA_ "GenMsgCycleTime" BO_ 817 20;
        """;

    internal static bool Supports(string id) => id.StartsWith("IO.CanDbc", StringComparison.Ordinal)
        || id == "IO.CanCyclicSendStop";

    internal static string Verify(string id, object serializer, Type contract, MethodInfo convert, MethodInfo format)
    {
        object step = contract.GetMethod("CreateDefault")!.Invoke(serializer, null)!;
        bool stop = id is "IO.CanDbcReadBackgroundStop" or "IO.CanCyclicSendStop";
        bool write = id is "IO.CanDbcWrite" or "IO.CanDbcCyclicWrite";
        if (id is "IO.CanDbcReadBackgroundStart" or "IO.CanDbcReadBackgroundStop")
        {
            var task = step.GetType().GetProperty("TaskName")!;
            if (!Equals(task.GetValue(step), "\"DbcRx1\"")) throw new InvalidOperationException("DBC Listen 默认启停任务名不一致");
        }
        string file = Path.Combine(Path.GetTempPath(), $"xtest-agent-{Guid.NewGuid():N}.dbc");
        try
        {
            var inputs = new Dictionary<string, string> { ["ConnectionName"] = Connection, ["TimeoutMs"] = "2700" };
            if (stop)
            {
                inputs["TaskName"] = id == "IO.CanCyclicSendStop" ? TxTask : RxTask;
                if (id == "IO.CanDbcReadBackgroundStop") inputs["IgnoreIfMissing"] = "true";
            }
            else
            {
                File.WriteAllText(file, Fixture, new UTF8Encoding(false));
                inputs["DbcFilePath"] = file; inputs["DbcFileEncoding"] = "UTF-8";
                inputs["CatalogQuery"] = "signals Extended 0x331 1";
                inputs["UseMessageList"] = "true";
                inputs["Messages"] = Rows(write).ToJsonString();
                if (!write) inputs["ResultVariable"] = "Locals.Decoded";
                if (id == "IO.CanDbcCyclicWrite") inputs["TaskName"] = TxTask;
                if (id == "IO.CanDbcReadBackgroundStart")
                {
                    inputs["TaskName"] = RxTask; inputs["PublishIntervalMs"] = "32"; inputs["MaxAgeMs"] = "127";
                }
                if (id == "IO.CanDbcCapture") { inputs["DurationMs"] = "1250"; inputs["MaxFrames"] = "137"; }
            }
            foreach (var (name, text) in inputs)
            {
                var field = step.GetType().GetProperty(name) ?? throw new MissingMemberException(name);
                field.SetValue(step, convert.Invoke(null, [text, field.PropertyType]));
            }
            object saved = RoundTrip(step);
            if (!stop) CheckCatalogDiscovery(saved, id);
            foreach (string name in inputs.Keys)
            {
                var field = saved.GetType().GetProperty(name)!;
                string text = (string)format.Invoke(null, [field, saved])!;
                if (name == "Messages") text = "[" + text + "]";
                var read = convert.Invoke(null, [text, field.PropertyType]);
                var expected = convert.Invoke(null, [inputs[name], field.PropertyType]);
                if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, field.PropertyType),
                    JsonSerializer.SerializeToNode(read, field.PropertyType)))
                    throw new InvalidOperationException($"{id}.{name} 有效业务参数回读不一致");
            }
            if (stop) return "启动约定的连接名、任务名与关闭期限通过实际宿主读写与 SDK 存储";
            var messages = saved.GetType().GetProperty("Messages")!;
            string firstRead = (string)format.Invoke(null, [messages, saved])!;
            var update = JsonNode.Parse("[" + firstRead + "]")!.AsArray();
            RequireSubset(Rows(write), update);
            var unchanged = update[1]!.DeepClone();
            update[0]!["Protection"]!["CrcDataIdEnabled"] = false;
            messages.SetValue(saved, convert.Invoke(null, [update.ToJsonString(), messages.PropertyType]));
            var modified = RoundTrip(saved);
            var secondRead = JsonNode.Parse("[" + (string)format.Invoke(null, [messages, modified])! + "]")!.AsArray();
            if (!JsonNode.DeepEquals(update, secondRead) || !JsonNode.DeepEquals(unchanged, secondRead[1]))
                throw new InvalidOperationException("修改第一个 ID 的 Data ID 开关改变了同批其他设置");
            return "同号标准/扩展两行、信号表达式或变量、每 ID 周期及独立保护、16项 Data ID 表、读取后定点修改均通过";

            object RoundTrip(object value)
            {
                var bytes = (byte[])contract.GetMethod("Serialize")!.Invoke(serializer, [value])!;
                return contract.GetMethod("Deserialize")!.Invoke(serializer, [bytes, (int)contract.GetProperty("SettingVersion")!.GetValue(serializer)!])!;
            }
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    private static void CheckCatalogDiscovery(object saved, string stepId)
    {
        var core = saved.GetType().Assembly;
        var resolution = core.GetType("CAN.Dbc.DbcDocumentIO", true)!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "ResolveAsync" && m.GetParameters().Length == 2);
        var read = (Task)resolution.Invoke(null, [saved, CancellationToken.None])!;
        read.GetAwaiter().GetResult();
        object document = read.GetType().GetProperty("Result")!.GetValue(read)!;
        string text = (string)document.GetType().GetProperty("Text")!.GetValue(document)!;
        object database = core.GetType("CAN.Dbc.DbcDatabase", true)!.GetMethod("ParseCatalog")!.Invoke(null, [text, CancellationToken.None])!;
        var ui = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(core.Location)!, "CAN.StepPlugin.UI.dll"));
        var describe = ui.GetType("CAN.UI.Dbc.DbcCatalogQuery", true)!.GetMethod("Describe", BindingFlags.Static | BindingFlags.NonPublic)!;
        string command = (string)saved.GetType().GetProperty("CatalogQuery")!.GetValue(saved)!;
        object info = describe.Invoke(null, [database, command, CancellationToken.None])!;
        string message = (string)info.GetType().GetProperty("Message")!.GetValue(info)!;
        var page = JsonNode.Parse(message)!;
        if ((string?)page["items"]?[0]?["name"] != "Sample" || (int?)page["total"] != 1)
            throw new InvalidOperationException($"{stepId} 查询未返回扩展帧 0x331 中的 Sample");
        VerifyInstalledIssueProjection(info);
    }

    // Invokes the installed workspace's pure issue mapper and result factory with a detached row.
    // The real bridge forwards each editor issue, including Info, through these two methods.
    private static void VerifyInstalledIssueProjection(object info)
    {
        var validation = Assembly.Load("FileValidation");
        var workspace = Assembly.Load("WorkspaceShell");
        var fileItemType = validation.GetType("FileValidation.ValidationErrorItem", true)!;
        var fileItem = Activator.CreateInstance(fileItemType)!;
        string code = (string)info.GetType().GetProperty("Code")!.GetValue(info)!;
        string text = (string)info.GetType().GetProperty("Message")!.GetValue(info)!;
        var severityProperty = fileItemType.GetProperty("Severity")!;
        object severity = info.GetType().GetProperty("Severity")!.GetValue(info)!;
        severityProperty.SetValue(fileItem, Enum.ToObject(severityProperty.PropertyType, Convert.ToInt32(severity)));
        fileItemType.GetProperty("Code")!.SetValue(fileItem, code);
        fileItemType.GetProperty("Message")!.SetValue(fileItem, text);
        var closure = workspace.GetType("WorkspaceShell.WorkspaceShellView+<>c", true)!;
        var mapper = closure.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name.Contains("ValidateProjectForAgentAsync", StringComparison.Ordinal) &&
                m.ReturnType.FullName == "xTestAgent.Tools.AgentValidationIssue" &&
                m.GetParameters() is [{ ParameterType: var t }] && t == fileItemType);
        object issue = mapper.Invoke(Activator.CreateInstance(closure, true), [fileItem])!;
        var issueArray = Array.CreateInstance(issue.GetType(), 1);
        issueArray.SetValue(issue, 0);
        var agent = issue.GetType().Assembly;
        var files = Array.CreateInstance(agent.GetType("xTestAgent.Tools.AgentValidatedFile", true)!, 0);
        var factory = agent.GetType("xTestAgent.Tools.AgentProjectValidation", true)!.GetMethod("Create")!;
        object report = factory.Invoke(null, [files, Array.Empty<string>(), issueArray])!;
        var issues = ((System.Collections.IEnumerable)report.GetType().GetProperty("Issues")!.GetValue(report)!).Cast<object>().ToArray();
        if (issues.Length != 1 || (string?)issues[0].GetType().GetProperty("Severity")!.GetValue(issues[0]) != "Info" ||
            (string?)issues[0].GetType().GetProperty("Code")!.GetValue(issues[0]) != "DBC_CATALOG" ||
            (string?)issues[0].GetType().GetProperty("Message")!.GetValue(issues[0]) != text)
            throw new InvalidOperationException("安装宿主项目校验未完整保留目录 Info");
    }

    private static JsonArray Rows(bool write)
    {
        var result = new JsonArray();
        foreach (bool extended in new[] { false, true })
        {
            var row = new JsonObject { ["MessageId"] = 817, ["FrameType"] = extended ? "Extended" : "Standard",
                ["FrameFormat"] = extended ? "Classic" : "Fd", ["Enabled"] = true,
                ["UseDbcCycleTime"] = !extended, ["CycleTimeMs"] = extended ? 55 : 20 };
            string signal = extended ? "Sample" : "Demand";
            row[write ? "Signals" : "SignalOutputs"] = new JsonArray(write
                ? new JsonObject { ["Name"] = signal, ["Selected"] = true, ["Value"] = extended ? "Locals.Rpm" : "12.5", ["ValueKind"] = extended ? "Raw" : "Physical" }
                : new JsonObject { ["Name"] = signal, ["Selected"] = true, ["TargetVariable"] = extended ? "Locals.Sample" : "Locals.Demand", ["ValueKind"] = extended ? "Raw" : "Physical" });
            var protection = new JsonObject { ["RcEnabled"] = true, ["RcStartByte"] = 1, ["RcEndByte"] = 1,
                ["RcUseBitField"] = true, ["RcBitOffset"] = 0, ["RcBitLength"] = 4, ["RcMaximum"] = 15,
                ["RcInitialValue"] = extended ? 1 : 0, ["CrcEnabled"] = true, ["CrcStartByte"] = 0, ["CrcEndByte"] = 0,
                ["CrcDataStartByte"] = 1, ["CrcDataEndByte"] = extended ? 7 : 15,
                ["CrcDataIdEnabled"] = true, ["CrcDataIdMode"] = extended ? "Fixed" : "Profile2",
                ["CrcAlgorithm"] = extended ? "Crc8SaeJ1850" : "Crc8Autosar" };
            if (extended) { protection["CrcDataIdHex"] = "BE EF"; protection["CrcDataIdPlacement"] = "BeforeData"; }
            else protection["CrcDataIdTable"] = JsonSerializer.SerializeToNode(Enumerable.Range(0xA0, 16).Select(i => i.ToString("X2")));
            row["Protection"] = protection;
            result.Add(row);
        }
        return result;
    }

    private static void RequireSubset(JsonNode expected, JsonNode? actual)
    {
        if (expected is JsonObject obj && actual is JsonObject target)
        {
            foreach (var p in obj) RequireSubset(p.Value!, target[p.Key]);
        }
        else if (expected is JsonArray array && actual is JsonArray other && array.Count == other.Count)
        {
            for (int index = 0; index < array.Count; index++) RequireSubset(array[index]!, other[index]);
        }
        else if (!JsonNode.DeepEquals(expected, actual)) throw new InvalidOperationException("DBC 助手回读缺少已指定的值：" + expected);
    }
}
