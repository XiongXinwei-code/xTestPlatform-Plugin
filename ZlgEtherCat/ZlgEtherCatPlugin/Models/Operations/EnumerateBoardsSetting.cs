using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class EnumerateBoardsSetting : ZlgSetting
{




    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();

        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest("", TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {

        });
    }
}
