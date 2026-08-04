$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = $PSScriptRoot

# ── CAN: Read, Write, Close ──
$canFiles = @{
	"CAN\CanPlugin.UI\Editors\CanReadEditorPlugin.cs" = @{using="using CAN.UI.Validation;"; before="        return errors;"; insert="        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"}
	"CAN\CanPlugin.UI\Editors\CanWriteEditorPlugin.cs" = @{using="using CAN.UI.Validation;"; before="        return errors;"; insert="        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"}
	"CAN\CanPlugin.UI\Editors\CanCloseEditorPlugin.cs" = @{using="using CAN.UI.Validation;"; before="        return errors;"; insert="        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"}
	"CAN\CanPlugin.UI\Editors\CanCyclicSendStartEditorPlugin.cs" = @{using="using CAN.UI.Validation;"; before="        return errors;"; insert="        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"}
	"CAN\CanPlugin.UI\Editors\CanCyclicSendStopEditorPlugin.cs" = @{using="using CAN.UI.Validation;"; before="        return errors;"; insert="        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);`n        CanLifecycleValidator.CheckPrecedingCyclicStart(context.Block, context.CurrentStep, s.ConnectionName, errors);"}
}

foreach ($kv in $canFiles.GetEnumerator()) {
	$path = Join-Path $root $kv.Key
	$content = [IO.File]::ReadAllText($path, $utf8)
	if ($content -notmatch 'using CAN\.UI\.Validation') {
		$content = $content -replace '(using CAN\.UI\.Views;)', "`$1`nusing CAN.UI.Validation;"
	}
	# Insert before last "return errors;"
	$lastIdx = $content.LastIndexOf($kv.Value.before)
	if ($lastIdx -ge 0) {
		$content = $content.Insert($lastIdx, $kv.Value.insert + "`n")
	}
	[IO.File]::WriteAllText($path, $content, $utf8)
	Write-Host "  CAN: $($kv.Key)"
}

# ── UDS: Rewrite the empty ones with proper validation ──
$udsPlugins = @(
	@{file="UdsClearDtcEditorPlugin.cs"; typeId="UDS.ClearDTC"; setting="UdsClearDtcSetting"; plugin="UdsClearDtcPlugin"; view="UdsClearDtcEditorView"}
	@{file="UdsReadDtcEditorPlugin.cs"; typeId="UDS.ReadDTC"; setting="UdsReadDtcSetting"; plugin="UdsReadDtcPlugin"; view="UdsReadDtcEditorView"}
	@{file="UdsReadDataByIdEditorPlugin.cs"; typeId="UDS.ReadDataByID"; setting="UdsReadDataByIdSetting"; plugin="UdsReadDataByIdPlugin"; view="UdsReadDataByIdEditorView"}
	@{file="UdsWriteDataByIdEditorPlugin.cs"; typeId="UDS.WriteDataByID"; setting="UdsWriteDataByIdSetting"; plugin="UdsWriteDataByIdPlugin"; view="UdsWriteDataByIdEditorView"}
	@{file="UdsRoutineControlEditorPlugin.cs"; typeId="UDS.RoutineControl"; setting="UdsRoutineControlSetting"; plugin="UdsRoutineControlPlugin"; view="UdsRoutineControlEditorView"}
	@{file="UdsRawRequestEditorPlugin.cs"; typeId="UDS.RawRequest"; setting="UdsRawRequestSetting"; plugin="UdsRawRequestPlugin"; view="UdsRawRequestEditorView"}
	@{file="UdsSecurityAccessEditorPlugin.cs"; typeId="UDS.SecurityAccess"; setting="UdsSecurityAccessSetting"; plugin="UdsSecurityAccessPlugin"; view="UdsSecurityAccessEditorView"}
)

foreach ($u in $udsPlugins) {
	$className = $u.file -replace '\.cs$',''
	$content = @"
using System.Windows;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class $className : IStepEditorPlugin
{
	public string StepTypeId => "$($u.typeId)";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new $($u.view)();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = ($($u.setting))new $($u.plugin)().CreateSerializer().Deserialize(context.Setting, 1);

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));

		CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
"@
	$path = Join-Path $root "CAN\CanPlugin.UI\Editors\$($u.file)"
	[IO.File]::WriteAllText($path, $content, $utf8)
	Write-Host "  UDS: $($u.file)"
}

# ── UdsDiagSessionEditorPlugin - add lifecycle check ──
$diagPath = Join-Path $root "CAN\CanPlugin.UI\Editors\UdsDiagSessionEditorPlugin.cs"
$diagContent = [IO.File]::ReadAllText($diagPath, $utf8)
if ($diagContent -notmatch 'using CAN\.UI\.Validation') {
	$diagContent = $diagContent -replace '(using CAN\.UI\.Views;)', "`$1`nusing CAN.UI.Validation;"
}
$diagContent = $diagContent -replace '(\s+return errors;)', "`n        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);`$1"
[IO.File]::WriteAllText($diagPath, $diagContent, $utf8)
Write-Host "  UDS: UdsDiagSessionEditorPlugin.cs"

Write-Host "`nCAN/UDS done."
