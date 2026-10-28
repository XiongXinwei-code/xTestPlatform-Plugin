using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.UI.Validation;

internal static class EthernetOutputValidator
{
    internal static void Check(StepEditorValidationContext context, string path, List<StepSettingError> errors)
    {
        if (Runtime.EthernetStringOutput.GetError(context.ExecutionContext, path) is { } error)
            errors.Add(StepSettingError.Error(error.Code, error.Message));
    }
}
