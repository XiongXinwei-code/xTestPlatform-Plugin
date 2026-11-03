using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Runtime;

/// <summary>
/// CAN 专项成功状态：按 2026-10-30 用户要求显示 PASS。
/// 只用于已经成功的出口，不转换错误、取消或未完成操作。
/// </summary>
internal static class CanExecutionResults
{
    internal static ExecutionResult Passed(string? value = null) => new()
    {
        StepResult = new StepResult { Status = TestStatus.Passed, Value = value ?? string.Empty }
    };
}
