using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ConfigureBoardLatchSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int PortSelection { get; set; } = 0;
    public int LatchMode { get; set; } = 1;
    public bool Continuous { get; set; } = false;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if ((PortSelection < 0 || PortSelection > 255)) errors.Add("引脚选择位图范围为 0～255");
        if ((LatchMode < 1 || LatchMode > 136)) errors.Add("厂商锁存模式范围为 1～136");
        if (Continuous ? LatchMode is not (101 or 102 or 103 or 104 or 114 or 115 or 116 or 117 or 118 or 119 or 120 or 121 or 123 or 124 or 133 or 134 or 135 or 136) : LatchMode is not (1 or 2 or 3 or 4 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21)) errors.Add("锁存模式未定义或与连续锁存配置不一致");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["PortSelection"] = PortSelection,
            ["LatchMode"] = LatchMode,
            ["Continuous"] = Continuous
        });
    }
}
