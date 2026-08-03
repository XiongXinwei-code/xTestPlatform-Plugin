[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$uiProjectPath = Join-Path $projectRoot 'UdpCommunication.StepPlugin.UI\UdpCommunication.StepPlugin.UI.csproj'
$sourceDirectory = Join-Path $projectRoot "UdpCommunication.StepPlugin.UI\bin\$Configuration\net8.0-windows7.0"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot '..\Plugins\UdpCommunication'
}
$destinationDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$packageFiles = @(
    'UdpCommunication.StepPlugin.dll',
    'UdpCommunication.StepPlugin.UI.dll',
    'MessagePack.dll',
    'MessagePack.Annotations.dll',
    'Microsoft.NET.StringTools.dll',
    'Syncfusion.Licensing.dll',
    'Syncfusion.SfSkinManager.WPF.dll',
    'Syncfusion.Shared.WPF.dll',
    'Syncfusion.Themes.Windows11Light.WPF.dll',
    'Syncfusion.Tools.WPF.dll'
)

& dotnet build $uiProjectPath -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "插件编译失败，退出代码：$LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $destinationDirectory)) {
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
}

$destinationItem = Get-Item -LiteralPath $destinationDirectory
$isDedicatedPluginDirectory = $destinationItem.Name -ieq 'UdpCommunication' -and $destinationItem.Parent.Name -ieq 'Plugins'
if ($isDedicatedPluginDirectory) {
    Get-ChildItem -LiteralPath $destinationDirectory -File | Remove-Item -Force
}

foreach ($fileName in $packageFiles) {
    $sourceFile = Join-Path $sourceDirectory $fileName
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "生成目录缺少部署所需文件：$sourceFile"
    }

    Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $destinationDirectory $fileName) -Force
}

$forbiddenFiles = @(
    'xTestPlatform.Core.dll',
    'xTestPlatform.StepEditor.SDK.dll',
    'xTestPlatform.Dialogs.dll',
    'Abstractions.dll',
    'ExpressionEditor.dll',
    'ExpressionTextBox.dll'
)
foreach ($fileName in $forbiddenFiles) {
    if (Test-Path -LiteralPath (Join-Path $destinationDirectory $fileName) -PathType Leaf) {
        throw "部署目录不应包含宿主程序集：$fileName"
    }
}

Write-Host "插件已发布到：$destinationDirectory"
