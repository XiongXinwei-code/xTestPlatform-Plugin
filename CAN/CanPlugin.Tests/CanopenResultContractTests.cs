using System.Globalization;
using CAN.CANopen;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class CanopenResultContractTests
{
    [Theory]
    [InlineData(TestStatus.Completed)]
    [InlineData(TestStatus.Aborted)]
    [InlineData(TestStatus.Error)]
    public void Missing_optional_result_keeps_the_SDK_non_null_string_contract(TestStatus status)
    {
        var result = CanopenExecutorHelper.Result(status, error: "test");
        Assert.Equal(status, result.StepResult.Status);
        Assert.Equal(string.Empty, result.StepResult.Value);
    }

    [Fact]
    public void Numeric_results_remain_invariant_and_error_context_is_preserved()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var result = CanopenExecutorHelper.Result(TestStatus.Error, 1.25m, "OD rejected");
            Assert.Equal("1.25", result.StepResult.Value);
            Assert.Contains("OD rejected", result.StepResult.Error!.Message);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
