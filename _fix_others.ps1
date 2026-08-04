$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = $PSScriptRoot

function Add-LifecycleCheck($filePath, $usingNs, $usingAfter, $insertCode) {
	$path = Join-Path $root $filePath
	$content = [IO.File]::ReadAllText($path, $utf8)
	if ($content -notmatch [regex]::Escape($usingNs)) {
		$content = $content -replace [regex]::Escape($usingAfter), "$usingAfter`n$usingNs"
	}
	# Insert before the LAST "return errors;"
	$lastIdx = $content.LastIndexOf("return errors;")
	if ($lastIdx -ge 0) {
		$content = $content.Insert($lastIdx, "$insertCode`n        ")
	}
	[IO.File]::WriteAllText($path, $content, $utf8)
	Write-Host "  Done: $filePath"
}

# ── VISA ──
$visaFiles = @(
	"VISA\VisaPlugin.UI\Editors\VisaQueryEditorPlugin.cs",
	"VISA\VisaPlugin.UI\Editors\VisaBatchWriteEditorPlugin.cs",
	"VISA\VisaPlugin.UI\Editors\VisaCloseEditorPlugin.cs"
)
foreach ($f in $visaFiles) {
	Add-LifecycleCheck $f "using VISA.UI.Validation;" "using VISA.UI.Views;" "VisaLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"
}

# Check if there are other VISA editors (Read, Write, WaitOpc)
$visaExtra = @(
	"VISA\VisaPlugin.UI\Editors\VisaReadEditorPlugin.cs",
	"VISA\VisaPlugin.UI\Editors\VisaWriteEditorPlugin.cs",
	"VISA\VisaPlugin.UI\Editors\VisaWaitOpcEditorPlugin.cs"
)
foreach ($f in $visaExtra) {
	$path = Join-Path $root $f
	if (Test-Path $path) {
		Add-LifecycleCheck $f "using VISA.UI.Validation;" "using VISA.UI.Views;" "VisaLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);"
	}
}

# ── OpcUA ──
$opcuaFiles = @(
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaReadEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaWriteEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaBatchReadEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaBatchWriteEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaSubscribeEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaDataAcqStartEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaDataAcqStopEditorPlugin.cs",
	"OpcUa\OpcUaPlugin.UI\Editors\OpcUaDisconnectEditorPlugin.cs"
)
foreach ($f in $opcuaFiles) {
	$path = Join-Path $root $f
	if (Test-Path $path) {
		Add-LifecycleCheck $f "using OpcUa.UI.Validation;" "using OpcUa.UI.Views;" "OpcUaLifecycleValidator.CheckPrecedingConnect(context.Block, context.CurrentStep, s.ConnectionName, errors);"
	}
}

# ── Modbus ──
$modbusFiles = @(
	"Modbus\ModbusPlugin.UI\Editors\ModbusReadEditorPlugin.cs",
	"Modbus\ModbusPlugin.UI\Editors\ModbusWriteEditorPlugin.cs",
	"Modbus\ModbusPlugin.UI\Editors\ModbusBatchReadEditorPlugin.cs",
	"Modbus\ModbusPlugin.UI\Editors\ModbusBatchWriteEditorPlugin.cs",
	"Modbus\ModbusPlugin.UI\Editors\ModbusDisconnectEditorPlugin.cs"
)
foreach ($f in $modbusFiles) {
	$path = Join-Path $root $f
	if (Test-Path $path) {
		Add-LifecycleCheck $f "using Modbus.UI.Validation;" "using Modbus.UI.Views;" "ModbusLifecycleValidator.CheckPrecedingConnect(context.Block, context.CurrentStep, s.ConnectionName, errors);"
	}
}

# ── NiDaq ──
# TaskStart needs Config before it
$daqTaskStart = "NiDaq\NiDaqPlugin.UI\Editors\NiDaqTaskStartEditorPlugin.cs"
$path = Join-Path $root $daqTaskStart
if (Test-Path $path) {
	Add-LifecycleCheck $daqTaskStart "using NiDaq.UI.Validation;" "using NiDaq.UI.Views;" "NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);"
}

# TaskStop needs Config before it
$daqTaskStop = "NiDaq\NiDaqPlugin.UI\Editors\NiDaqTaskStopEditorPlugin.cs"
$path = Join-Path $root $daqTaskStop
if (Test-Path $path) {
	Add-LifecycleCheck $daqTaskStop "using NiDaq.UI.Validation;" "using NiDaq.UI.Views;" "NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);"
}

# AiRead needs Config + TaskStart
$daqAiRead = "NiDaq\NiDaqPlugin.UI\Editors\NiDaqAiReadEditorPlugin.cs"
$path = Join-Path $root $daqAiRead
if (Test-Path $path) {
	Add-LifecycleCheck $daqAiRead "using NiDaq.UI.Validation;" "using NiDaq.UI.Views;" "NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);`n        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.Block, context.CurrentStep, s.TaskName, errors);"
}

# SyncRead needs Config + TaskStart
$daqSyncRead = "NiDaq\NiDaqPlugin.UI\Editors\NiDaqSyncReadEditorPlugin.cs"
$path = Join-Path $root $daqSyncRead
if (Test-Path $path) {
	Add-LifecycleCheck $daqSyncRead "using NiDaq.UI.Validation;" "using NiDaq.UI.Views;" "NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);`n        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.Block, context.CurrentStep, s.TaskName, errors);"
}

# EncoderRead needs EncoderConfig
$daqEncoderRead = "NiDaq\NiDaqPlugin.UI\Editors\NiDaqEncoderReadEditorPlugin.cs"
$path = Join-Path $root $daqEncoderRead
if (Test-Path $path) {
	Add-LifecycleCheck $daqEncoderRead "using NiDaq.UI.Validation;" "using NiDaq.UI.Views;" "NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);"
}

Write-Host "`nAll protocols done."
