using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ConfigureBoardPulseSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public int PulseMode { get; set; } = 0;
    public int FrequencyHz { get; set; } = 1000;
    public int Direction { get; set; } = 0;
    public int Duty { get; set; } = 50;
    public bool Continuous { get; set; } = false;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if ((Port < 0 || Port > 3)) errors.Add("输出通道范围为 0～3");
        if ((PulseMode < 0 || PulseMode > 3)) errors.Add("脉冲模式范围为 0～3");
        if ((FrequencyHz < 1 || FrequencyHz > 10000000)) errors.Add("频率（Hz）范围为 1～10000000");
        if ((Direction < 0 || Direction > 1)) errors.Add("输出方向范围为 0～1");
        if ((Duty < 0 || Duty > 100)) errors.Add("占空比范围为 0～100");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["Port"] = Port,
            ["PulseMode"] = PulseMode,
            ["FrequencyHz"] = FrequencyHz,
            ["Direction"] = Direction,
            ["Duty"] = Duty,
            ["Continuous"] = Continuous
        });
    }
}
