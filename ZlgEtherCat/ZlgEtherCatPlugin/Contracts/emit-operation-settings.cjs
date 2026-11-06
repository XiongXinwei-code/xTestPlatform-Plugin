const fs=require('fs'),path=require('path');
module.exports=({specs,core,ui,write})=>{
const esc=s=>JSON.stringify(s), typ=f=>['expression','variable'].includes(f.type)?'string':f.type;
const def=f=>['expression','variable','string'].includes(f.type)?esc(f.def):['int','double','bool'].includes(f.type)?String(f.def):f.type+'.'+f.def;
const own=s=>s.fields.filter(f=>!['TimeoutMs','ResultVariable'].includes(f.name));
const expr=s=>s.fields.filter(f=>f.type==='expression');
const vars=s=>s.fields.filter(f=>f.type==='variable');
const extra=s=>s.fields.filter(f=>!['TimeoutMs','ResultVariable','ConnectionName'].includes(f.name));
const xml=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/"/g,'&quot;').replace(/\n/g,'&#10;');
const overridden=(s,f)=>s.fields.find(x=>x.overrideExpressionFor===f.name);
const prop=f=>(f.type==='expression'?'    [ExpressionField]\n':f.type==='variable'?'    [VariablePathField]\n':'')+`    public ${typ(f)} ${f.name} { get; set; } = ${def(f)};`;
const condition=(f,s)=>{
 const gate=overridden(s,f)?`string.IsNullOrWhiteSpace(${overridden(s,f).name}) && `:'';
 return f.type==='expression'?(f.optionalExpression?'':`if (string.IsNullOrWhiteSpace(${f.name})) errors.Add("${f.label}不能为空");`):f.type==='variable'?`CheckVariable(${f.name}, ${f.name==='DataVariable'}, "${f.label}", errors);`:f.type==='int'?`if (${gate}(${f.name} < ${f.min} || ${f.name} > ${f.max})) errors.Add("${f.label}范围为 ${f.min}～${f.max}");`:f.type==='double'?`if (${gate}(!double.IsFinite(${f.name}) || ${f.name} < ${f.min} || ${f.name} > ${f.max}${f.name==='Scale'?` || ${f.name} == 0`:''})) errors.Add("${f.label}必须为有效有限值${f.name==='Scale'?'且不能为0':''}");`:!['string','bool'].includes(f.type)?`if (!Enum.IsDefined(${f.name})) errors.Add("${f.label}未定义");`:'';
};
const prerequisite=op=>op==='EnumerateBoards'||op==='Open'?null:op.includes('Pool')&&!op.endsWith('Create')?(op.startsWith('Background')?'BackgroundPoolCreate':'PeriodicPoolCreate'):['ReadPdo','WaitNextPdo','ReadPdoBits','ReadHistory','ReadDigitalInput','ReadAnalogInput','StopAcquisition','BackgroundPoolCreate','PeriodicPoolCreate'].includes(op)?'StartAcquisition':specs.find(s=>s.op===op)?.group.startsWith('Motion')&&op!=='BindAxis'?'BindAxis':'Open';
for(const s of specs){
 const op=s.op, display='ZlgEtherCat_'+s.group.replace(/ /g,'')+'_'+s.op, icon='zlg_'+op.toLowerCase()+'.png';
 const specific=op==='SetAxisMode'?['if (Mode is not (1 or 3 or 4 or 6 or 8 or 9 or 10)) errors.Add("模式仅支持 1、3、4、6、8、9、10");']:op==='ConfigureBoardLatch'?['if (Continuous ? LatchMode is not (101 or 102 or 103 or 104 or 114 or 115 or 116 or 117 or 118 or 119 or 120 or 121 or 123 or 124 or 133 or 134 or 135 or 136) : LatchMode is not (1 or 2 or 3 or 4 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21)) errors.Add("锁存模式未定义或与连续锁存配置不一致");']:[];
 if(s.fields.some(f=>f.name==='Length')&&s.fields.some(f=>f.name==='DataType'))specific.push('CheckDataWidth(DataType, Length, errors);');
 if(op==='ReadSdo')specific.push('if (Capacity < ZlgMappingValidation.DataWidth(DataType)) errors.Add("接收容量不能小于所选类型宽度；实际返回长度仍须与类型一致");');
 if(op==='ReadAnalogInput'||op==='WriteAnalogOutput')specific.push('if (DataType is PdoDataType.Bytes or PdoDataType.Boolean) errors.Add("模拟量必须选择数值类型");');
 if(op==='StartCyclicOutput')specific.push('if (CommandLeaseMs <= IntervalMs) errors.Add("目标续期有效时间必须大于目标更新周期");');
 if(op==='ReadSdo'||op==='WriteSdo'){specific.push('if (CompleteAccess && SubIndex is not (0 or 1)) errors.Add("完全访问的子索引只能为 0 或 1");');specific.push('if (CompleteAccess && DataType != PdoDataType.Bytes) errors.Add("完全访问返回/写入整个对象块，数据类型必须为 Bytes");');}
 if(op==='BindAxis')specific.push('ZlgMappingValidation.Validate(this, errors);');
 for(const f of s.fields.filter(f=>f.type==='expression'&&(f.dynamicRange||f.optionalExpression)&&(f.min!==undefined||f.max!==undefined||f.nonZero)))specific.push(`ZlgMappingValidation.CheckNumericLiteral(${f.name}, ${f.min??'null'}, ${f.max??'null'}, ${!!f.nonZero}, "${f.label}", errors);`);
 const checks=[...own(s).map(f=>condition(f,s)).filter(Boolean),...specific].map(x=>'        '+x).join('\n');
 const dynamic=extra(s).filter(f=>f.overrideExpressionFor||f.dynamicRange);
 const evaluated=dynamic.map(f=>{
  const name=f.overrideExpressionFor||f.name, t=f.evalType||'int', call=f.overrideExpressionFor?`EvalOptional<${t}>(${f.name}, ${name}, context, ct)`:`Eval<${t}>(${f.name}, context, ct)`;
  const conditions=[t==='double'?`!double.IsFinite(evaluated${name})`:null,f.min!==undefined?`evaluated${name} < ${f.min}`:null,f.max!==undefined?`evaluated${name} > ${f.max}`:null,f.nonZero?`evaluated${name} == 0`:null].filter(Boolean);
  return `        var evaluated${name} = await ${call}.ConfigureAwait(false);\n${conditions.length?`        if (${conditions.join(' || ')}) throw new ArgumentOutOfRangeException(nameof(${f.name}), "${f.label}求值结果超出允许范围");`:''}`;
 }).join('\n');
 const args=extra(s).filter(f=>!f.overrideExpressionFor).map(f=>`            ["${f.name}"] = ${f.typedData?`await EvalWriteData(${f.name}, DataType, context, ct).ConfigureAwait(false)`:overridden(s,f)||f.dynamicRange?'evaluated'+f.name:f.type==='expression'?`await Eval<${f.evalType||'string'}>(${f.name}, context, ct).ConfigureAwait(false)`:f.name}`).join(',\n');
 write(path.join(core,'Models','Operations',op+'Setting.cs'),`using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ${op}Setting : ZlgSetting
{
${s.needsConnection?'    [IgnoreMember] public override string ConnectionExpression => ConnectionName;':''}
${s.fields.some(f=>f.name==='PoolName')?'    [IgnoreMember] public override string ResourceExpression => PoolName;':s.fields.some(f=>f.name==='AxisName')?'    [IgnoreMember] public override string ResourceExpression => AxisName;':''}
${own(s).map(prop).join('\n')}

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
${checks}
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
${evaluated}
        return new ZlgRequest(${s.needsConnection?'await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false)':'""'}, TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
${args}
        });
    }
}
`);

}
};
