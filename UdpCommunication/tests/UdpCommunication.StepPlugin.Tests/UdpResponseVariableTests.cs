using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpResponseVariableTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("UdpReply", "Step.UdpReply")]
    [InlineData(" Locals.UdpReply ", "Locals.UdpReply")]
    public void NormalizePath_ReturnsCanonicalVariablePath(string? configured, string? expected)
    {
        Assert.Equal(expected, UdpResponseVariable.NormalizePath(configured));
    }

    [Fact]
    public void Validate_RejectsUnsupportedScope()
    {
        var (context, _) = TestExecutionContextFactory.CreateWithProxy(new Step());

        var error = UdpResponseVariable.Validate("Unknown.UdpReply", context);

        Assert.Contains("作用域", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsUndefinedScopedVariable()
    {
        var (context, _) = TestExecutionContextFactory.CreateWithProxy(new Step());

        var error = UdpResponseVariable.Validate("Locals.UdpReply", context);

        Assert.Contains("未定义", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsReadOnlyVariable()
    {
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(new Step());
        proxy.Locals.Add(CreateVariable(VariableDataType.String, VariableAccessMode.ReadOnly));

        var error = UdpResponseVariable.Validate("Locals.UdpReply", context);

        Assert.Contains("只读", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsNonStringCompatibleVariable()
    {
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(new Step());
        proxy.Locals.Add(CreateVariable(VariableDataType.Int, VariableAccessMode.ReadWrite));

        var error = UdpResponseVariable.Validate("Locals.UdpReply", context);

        Assert.Contains("String", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VariableDataType.String)]
    [InlineData(VariableDataType.Dynamic)]
    [InlineData(VariableDataType.Object)]
    public void Validate_AcceptsWritableStringCompatibleVariable(VariableDataType dataType)
    {
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(new Step());
        proxy.Locals.Add(CreateVariable(dataType, VariableAccessMode.ReadWrite));

        Assert.Null(UdpResponseVariable.Validate("Locals.UdpReply", context));
    }

    [Fact]
    public void Validate_AcceptsDynamicStepVariable()
    {
        var (context, _) = TestExecutionContextFactory.CreateWithProxy(new Step());

        Assert.Null(UdpResponseVariable.Validate("UdpReply", context));
    }

    private static Variables CreateVariable(
        VariableDataType dataType,
        VariableAccessMode accessMode) =>
        new()
        {
            Name = "UdpReply",
            DataType = dataType,
            AccessMode = accessMode
        };
}
