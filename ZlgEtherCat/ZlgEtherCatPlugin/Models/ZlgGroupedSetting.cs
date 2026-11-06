using MessagePack;
using System.Text.Json.Serialization;
using xTestPlatform.Core.Engine;
using ZlgEtherCat.Contracts;

namespace ZlgEtherCat.Models;

public abstract class ZlgGroupedSetting : ZlgSetting, IZlgGroupedSetting
{
    [IgnoreMember, JsonIgnore] public abstract string ActiveOperation { get; }
    [IgnoreMember, JsonIgnore] public abstract IReadOnlyList<string> ActiveFields { get; }
    [IgnoreMember, JsonIgnore] public override string ConnectionExpression => CreateOperationSetting().ConnectionExpression;
    [IgnoreMember, JsonIgnore] public override string ResourceExpression => CreateOperationSetting().ResourceExpression;
    public abstract ZlgSetting CreateOperationSetting();

    public override IReadOnlyList<string> Validate()
    {
        try { return CreateOperationSetting().Validate(); }
        catch (ArgumentOutOfRangeException ex) { return ["所选功能未定义：" + ex.Message]; }
    }

    public override Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var selected = CreateOperationSetting();
        var errors = selected.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join("；", errors));
        return selected.ToRequestAsync(context, ct);
    }
}
