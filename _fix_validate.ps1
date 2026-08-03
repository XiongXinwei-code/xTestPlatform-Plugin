$utf8 = New-Object System.Text.UTF8Encoding($false)

# Fix Category A files - rewrite completely
$files = @{
	"CAN\CanPlugin.UI\Editors\UdsClearDtcEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsClearDtcEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.ClearDTC";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsClearDtcEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
	"CAN\CanPlugin.UI\Editors\UdsReadDtcEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsReadDtcEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.ReadDTC";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsReadDtcEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
	"CAN\CanPlugin.UI\Editors\UdsWriteDataByIdEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsWriteDataByIdEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.WriteDataByID";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsWriteDataByIdEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
	"CAN\CanPlugin.UI\Editors\UdsRoutineControlEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsRoutineControlEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.RoutineControl";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsRoutineControlEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
	"CAN\CanPlugin.UI\Editors\UdsRawRequestEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsRawRequestEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.RawRequest";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsRawRequestEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
	"CAN\CanPlugin.UI\Editors\UdsReadDataByIdEditorPlugin.cs" = @"
using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsReadDataByIdEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.ReadDataByID";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsReadDataByIdEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}
"@
}

foreach ($kv in $files.GetEnumerator()) {
	$path = Join-Path $PSScriptRoot $kv.Key
	[IO.File]::WriteAllText($path, $kv.Value, $utf8)
	Write-Host "  Fixed: $($kv.Key)"
}

# Fix HasVariable references: context.HasVariable -> context.ExecutionContext.HasVariable
$allFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter "*EditorPlugin.cs" | Where-Object { $_.FullName -match '\\Editors\\' }
foreach ($f in $allFiles) {
	$content = [IO.File]::ReadAllText($f.FullName, $utf8)
	if ($content -match 'context\.HasVariable') {
		$content = $content -replace 'context\.HasVariable', 'context.ExecutionContext.HasVariable'
		[IO.File]::WriteAllText($f.FullName, $content, $utf8)
		Write-Host "  Fixed HasVariable: $($f.Name)"
	}
}

Write-Host "Done."
