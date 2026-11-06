const fs = require('fs'), path = require('path');
module.exports = ({specs, core, ui, write}) => {
    const groups = JSON.parse(fs.readFileSync(path.join(__dirname, 'group-catalog.json'), 'utf8'));
    const all = groups.flatMap(g => g.operations);
    if (all.length !== specs.length || new Set(all).size !== all.length || specs.some(s => !all.includes(s.op)))
        throw new Error('Each runtime operation must belong to exactly one function step.');
    if (new Set(groups.map(g => g.name)).size !== groups.length || groups.some(g => g.operations.length === 0))
        throw new Error('Function step names must be unique and each step must contain an operation.');
    const groupByOperation = new Map(groups.flatMap(g => g.operations.map(op => [op, g])));
    const stepReference = operation => {
        const owner = groupByOperation.get(operation);
        if (!owner) throw new Error(`Unknown referenced operation: ${operation}`);
        return 'ZlgEtherCat.' + owner.name + (owner.operations.length > 1 ? ' / ' + operation : '');
    };
    const mapReferences = text => text.replace(/ZlgEtherCat\.([A-Za-z0-9]+)/g, (original, operation) =>
        groupByOperation.has(operation) ? stepReference(operation) : original);
    const quote = JSON.stringify;
    const typ = f => ['expression', 'variable'].includes(f.type) ? 'string' : f.type;
    const def = f => ['expression','variable','string'].includes(f.type) ? quote(f.def)
        : ['int','double','bool'].includes(f.type) ? String(f.def) : f.type + '.' + f.def;
    const schema = f => JSON.stringify([f.type, f.def, f.min, f.max, f.evalType, f.optionalExpression, f.overrideExpressionFor, f.dynamicRange, f.typedData]);
    for (const g of groups) {
        const fixedOperation = g.operations.length === 1;
        g.specs = g.operations.map(op => specs.find(s => s.op === op));
        const fields = new Map();
        for (const s of g.specs) for (const f of s.fields) {
            if (fields.has(f.name) && schema(fields.get(f.name)) !== schema(f))
                throw new Error(`Conflicting parameter ${g.name}.${f.name} in ${s.op}`);
            fields.set(f.name, f);
        }
        const order = f => f.name === 'ConnectionName' ? -1 : f.name === 'TimeoutMs' ? 1 : f.name === 'ResultVariable' ? 2 : 0;
        g.fields = [...fields.values()].sort((a,b) => order(a) - order(b));
        g.icon = 'zlg_' + g.name.toLowerCase() + '.png';
        const n = g.name;
        const properties = g.fields.filter(f => !['TimeoutMs','ResultVariable'].includes(f.name)).map(f =>
            (f.type === 'expression' ? '    [ExpressionField]\n' : f.type === 'variable' ? '    [VariablePathField]\n' : '') +
            `    public ${typ(f)} ${f.name} { get; set; } = ${def(f)};`).join('\n');
        write(path.join(core, 'Models', n + 'Setting.cs'), `using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Models.StepSettings;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ${n}Operation { ${g.operations.map((op,i)=>op+' = '+i).join(', ')} }

[MessagePackObject(true)]
public sealed class ${n}Setting : ZlgGroupedSetting
{
    public ${n}Operation Operation { get; set; } = ${n}Operation.${g.operations[0]};
${properties}

    [IgnoreMember, JsonIgnore] public override string ActiveOperation => Operation switch
    {
${g.operations.map(op=>`        ${n}Operation.${op} => "${op}",`).join('\n')}
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    [IgnoreMember, JsonIgnore] public override IReadOnlyList<string> ActiveFields => Operation switch
    {
${g.specs.map(s=>`        ${n}Operation.${s.op} => [${s.fields.map(f=>quote(f.name)).join(', ')}],`).join('\n')}
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
    public override ZlgSetting CreateOperationSetting() => Operation switch
    {
${g.specs.map(s=>`        ${n}Operation.${s.op} => new Operations.${s.op}Setting { ${s.fields.map(f=>f.name+' = '+f.name).join(', ')} },`).join('\n')}
        _ => throw new ArgumentOutOfRangeException(nameof(Operation), "所选功能未定义")
    };
}
`);
        const summary = s => s.summaryFields.map(name=>{
            const f=s.fields.find(f=>f.name===name), over=s.fields.find(f=>f.overrideExpressionFor===name);
            return f.label+' '+(over?`{(string.IsNullOrWhiteSpace(s.${over.name}) ? s.${name}.ToString() : s.${over.name})}`:`{s.${name}}`);
        }).join('；');
        const rows = g.fields.map(f => {
            const uses = g.specs.filter(s=>s.fields.some(a=>a.name===f.name));
            const different = new Set(uses.map(s=>s.fields.find(a=>a.name===f.name).info)).size > 1;
            return `        | ${f.name} | ${f.type==='expression'?'string([ExpressionField])':f.type==='variable'?'string(VariablePathField)':f.type} | ${['ResultVariable'].includes(f.name)||f.optionalExpression?'否':fixedOperation?'是':'仅适用操作必填'} | ${String(f.def).replace(/\|/g,'/')} | ${uses.map(s=>s.op).join('、')}；${f.label}；${f.type==='expression'?`求值结果为 ${f.evalType||'string'}；`:''}${different?'不同操作的范围、方向和返回类型见下方逐操作参数说明':mapReferences(f.info||'')}${f.min!==undefined&&f.max!==undefined?`；范围 ${f.min}～${f.max}`:''} |`;
        }).join('\n');
        const details = g.specs.map(s => `        ### ${s.op} — ${s.title}
        - 适用参数：${s.fields.map(f=>f.name).join('、')}。
${s.fields.filter(f=>new Set(g.specs.filter(a=>a.fields.some(x=>x.name===f.name)).map(a=>a.fields.find(x=>x.name===f.name).info)).size>1).map(f=>`        - ${f.name}：${mapReferences(f.info||'')}`).join('\n')}
        - 目标与范围：${mapReferences(s.scope)}
        - 前置操作：${s.prerequisites.length?s.prerequisites.map(stepReference).join('、'):'无'}；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：${mapReferences(s.result)}。
        - ${mapReferences(s.behavior||'使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。')}
${s.group==='Board IO'?'        - 板载端口仅按已核实的 PCIe-1E16 能力开放，其他型号不自动推断支持。\n':''}${s.group==='Watchdog'?'        - HPCIe-2E 官方能力表与通用 SDK 接口存在支持差异，本版本拒绝该型号的看门狗操作。\n':''}${s.group==='Background Reading'?'        - 最新帧缓存可能合并中间帧；SkippedSamples 为软件样本跳过数，不是硬件丢帧。\n':''}${s.group.startsWith('Motion')?'        - 运动使用驱动器原始单位，按映射与驱动手册配置；不保证硬实时周期，不替代硬件安全链。\n':''}`).join('\n');
        write(path.join(core,'Plugins',n+'Plugin.cs'), `using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ${n}Plugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<${n}Setting>
{
    public override string StepTypeId => "ZlgEtherCat.${n}";
    public override string DisplayName => "ZlgEtherCat_${g.category}_${n}";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/${g.icon}";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ${n}Executor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (${n}Setting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
${g.specs.map(s=>`                ${n}Operation.${s.op} => $"${s.title}${summary(s)?'：'+summary(s):''}",`).join('\n')}
                _ => "${g.title}（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "${g.title}（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        ${g.title}。${fixedOperation?'本步骤为固定功能，只执行 '+g.operations[0]+'，无需选择操作；编辑参数不会执行设备动作。':'Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。'}

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ${n}Operation | ${fixedOperation?'固定值，无需选择':'是'} | ${g.operations[0]} | ${g.specs.map((s,i)=>s.op+'='+i+'（'+s.title+'）').join('；')}${fixedOperation?'；固定功能，不接受其他操作':''} |
${rows}

        ## 行为
        - ${fixedOperation?'平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。':'平台助手先选择 Operation，再读写该操作的适用参数；'}全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - ${fixedOperation?'本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。':'只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。'}
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。${g.operations.length > 1 ? '未选字段的格式错误不影响本步骤所选操作的业务校验和求值。' : '此步骤只包含其固定操作的参数。'}
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
${details}
        ## 相关插件
        ${[...new Set([...g.specs.flatMap(s=>s.prerequisites), 'Open'].map(stepReference))].join('、')}。
        """;
}
`);
        write(path.join(core,'Executors',n+'Executor.cs'), `using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class ${n}Executor : ZlgExecutorBase<${n}Setting>
{
    public ${n}Executor() : base(new ${n}Plugin()) { }
}
`);
    }
    write(path.join(core,'Plugins','ZlgStepCatalog.cs'), `using xTestPlatform.Core.Plugins.Contracts;
namespace ZlgEtherCat.Plugins;
public static class ZlgStepCatalog
{
    public static IStepPlugin Create(string stepTypeId) => stepTypeId switch
    {
${groups.map(g=>`        "ZlgEtherCat.${g.name}" => new ${g.name}Plugin(),`).join('\n')}
        _ => throw new ArgumentException($"未知 ZLG 功能步骤：{stepTypeId}")
    };
    public static IReadOnlyList<IStepPlugin> All => new IStepPlugin[]
    {
${groups.map(g=>`        new ${g.name}Plugin(),`).join('\n')}
    };
}
`);
    require('./emit-group-ui.cjs')({groups, specs, core, ui, write});
    console.log(`Generated ${groups.length} function steps covering ${all.length} operations.`);
};
