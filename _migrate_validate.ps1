$utf8 = New-Object System.Text.UTF8Encoding($false)

# ── Category A: Files with NO ValidateWithContextAsync at all ──
# These just need an empty implementation added before the closing brace
$noValidateFiles = @(
	"CAN\CanPlugin.UI\Editors\UdsClearDtcEditorPlugin.cs",
	"CAN\CanPlugin.UI\Editors\UdsReadDtcEditorPlugin.cs",
	"CAN\CanPlugin.UI\Editors\UdsWriteDataByIdEditorPlugin.cs",
	"CAN\CanPlugin.UI\Editors\UdsRoutineControlEditorPlugin.cs",
	"CAN\CanPlugin.UI\Editors\UdsRawRequestEditorPlugin.cs",
	"CAN\CanPlugin.UI\Editors\UdsReadDataByIdEditorPlugin.cs"
)

foreach ($f in $noValidateFiles) {
	$path = Join-Path $PSScriptRoot $f
	$content = [IO.File]::ReadAllText($path, $utf8)

	# Add using for StepEditor.Abstractions if not present (already has it)
	# Add empty ValidateWithContextAsync before the last closing brace of the class
	$stub = @"

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
"@

	# Insert before the last "}" 
	$lastBrace = $content.LastIndexOf("}")
	$secondLastBrace = $content.LastIndexOf("}", $lastBrace - 1)
	$content = $content.Insert($secondLastBrace, $stub + "`n")

	# Add missing usings if needed
	if ($content -notmatch 'using xTestPlatform\.Core\.Plugins\.Contracts;') {
		$content = $content.Replace("using StepEditor.Abstractions;", "using StepEditor.Abstractions;`nusing xTestPlatform.Core.Plugins.Contracts;")
	}

	[IO.File]::WriteAllText($path, $content, $utf8)
	Write-Host "  [A] $f"
}

# ── Category B: Files with old signature that DON'T use evaluator/context ──
# Pattern: replace old signature with new, and replace "setting" usage with "context.Setting"

# First, get ALL editor plugin files
$allEditorFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter "*EditorPlugin.cs" | 
	Where-Object { $_.FullName -match '\\Editors\\' -or $_.Name -eq 'LabVIEWCallEditorPlugin.cs' }

$skipFiles = $noValidateFiles | ForEach-Object { (Resolve-Path (Join-Path $PSScriptRoot $_)).Path }

foreach ($file in $allEditorFiles) {
	if ($file.FullName -in $skipFiles) { continue }
	if ($file.Name -eq 'LabVIEWCallEditorPlugin.cs') { continue } # Handle separately

	$content = [IO.File]::ReadAllText($file.FullName, $utf8)

	if ($content -notmatch 'ValidateWithContextAsync') { continue }

	# Replace various old signature patterns with new signature
	# Pattern 1: multi-line with explicit params
	$content = $content -replace 'public\s+async\s+Task<IReadOnlyList<StepSettingError>>\s+ValidateWithContextAsync\s*\(\s*\r?\n\s*byte\[\]\s+setting\s*,\s*IExpressionEvaluator\s+evaluator\s*,\s*IExecutionContext\s+context\s*,\s*CancellationToken\s+\w+\s*(?:=\s*default)?\s*\)', 'public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)'

	# Pattern 2: single-line with explicit params  
	$content = $content -replace 'public\s+async\s+Task<IReadOnlyList<StepSettingError>>\s+ValidateWithContextAsync\s*\(\s*byte\[\]\s+setting\s*,\s*IExpressionEvaluator\s+evaluator\s*,\s*IExecutionContext\s+context\s*,\s*CancellationToken\s+\w+\s*(?:=\s*default)?\s*\)', 'public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)'

	# Pattern 3: non-async Task (SerialPort style) multi-line
	$content = $content -replace 'public\s+Task<IReadOnlyList<StepSettingError>>\s+ValidateWithContextAsync\s*\(\s*\r?\n\s*byte\[\]\s+setting\s*,\s*\r?\n\s*IExpressionEvaluator\s+evaluator\s*,\s*\r?\n\s*IExecutionContext\s+context\s*,\s*\r?\n\s*CancellationToken\s+\w+\s*(?:=\s*default)?\s*\)', 'public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)'

	# Pattern 4: non-async single-line with tab indent (SerialPortQuery)
	$content = $content -replace 'public\s+Task<IReadOnlyList<StepSettingError>>\s+ValidateWithContextAsync\s*\(\s*byte\[\]\s+setting\s*,\s*IExpressionEvaluator\s+evaluator\s*,\s*IExecutionContext\s+context\s*,\s*CancellationToken\s+\w+\s*(?:=\s*default)?\s*\)', 'public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)'

	# Now replace variable references inside the method body:
	# For files that use evaluator/context (CanOpen, UdsDiagSession, UdsSecurityAccess):
	$content = $content -replace 'evaluator\.ValidateExpression\(([^,]+),\s*context,', 'context.Evaluator.ValidateExpression($1, context.ExecutionContext,'

	# Replace setting variable references - but be careful:
	# "Deserialize(setting," -> "Deserialize(context.Setting,"
	$content = $content -replace '\.Deserialize\(setting,', '.Deserialize(context.Setting,'

	# "setting is { Length: > 0 }" -> "context.Setting is { Length: > 0 }"
	$content = $content -replace '\bsetting\s+is\s+\{', 'context.Setting is {'

	# Remove now-unused usings (IExecutionContext, IExpressionEvaluator) only if not used elsewhere
	if ($content -notmatch 'IExecutionContext[^;]' -or ($content -match 'context\.ExecutionContext')) {
		# Check if IExecutionContext is used anywhere other than the using
		$withoutUsing = $content -replace 'using xTestPlatform\.Core\.Engine;', ''
		if ($withoutUsing -notmatch 'IExecutionContext') {
			$content = $content -replace '\r?\nusing xTestPlatform\.Core\.Engine;\r?\n', "`n"
		}
	}
	if ($content -notmatch 'IExpressionEvaluator[^;]' -or ($content -match 'context\.Evaluator')) {
		$withoutUsing = $content -replace 'using xTestPlatform\.Core\.Services\.ExpressionEngine;', ''
		if ($withoutUsing -notmatch 'IExpressionEvaluator') {
			$content = $content -replace '\r?\nusing xTestPlatform\.Core\.Services\.ExpressionEngine;\r?\n', "`n"
		}
	}

	[IO.File]::WriteAllText($file.FullName, $content, $utf8)
	Write-Host "  [B] $($file.Name)"
}

Write-Host "`nDone. LabVIEWCallEditorPlugin.cs needs manual handling."
