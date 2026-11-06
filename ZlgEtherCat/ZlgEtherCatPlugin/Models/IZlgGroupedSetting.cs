namespace ZlgEtherCat.Models;

/// <summary>One registered function step selects exactly one strongly typed SDK/runtime operation.</summary>
public interface IZlgGroupedSetting
{
    string ActiveOperation { get; }
    IReadOnlyList<string> ActiveFields { get; }
    ZlgSetting CreateOperationSetting();
}
