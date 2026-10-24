[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$consoleEncoding = [Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $consoleEncoding
[Console]::OutputEncoding = $consoleEncoding
$OutputEncoding = $consoleEncoding
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$roots = @('CAN', 'CiA402', 'Ethernet', 'Http', 'LIN', 'Modbus', 'NiDaq', 'OpcUa', 'SerialPort', 'VISA', 'Shared', 'tests', 'tools', 'docs', '.github')
# Match release.yml's conventional module/core-project discovery. Test projects
# and deep artifact snapshots must not become phantom plugins in CI.
$expectedModules = @($roots[0..9] | Sort-Object)
$discoveredModules = @(Get-ChildItem -LiteralPath $repoRoot -Directory | Where-Object {
    (Get-ChildItem -LiteralPath $_.FullName -Depth 1 -File -Filter '*Plugin.csproj').Count -gt 0
} | Select-Object -ExpandProperty Name | Sort-Object)
if (Compare-Object $expectedModules $discoveredModules) {
    throw "发布模块发现不一致：$($discoveredModules -join ', ')"
}
$extensions = @('.cs', '.xaml', '.csproj', '.props', '.targets', '.md', '.yml', '.yaml', '.json', '.ps1', '.slnx')
$files = @(foreach ($root in $roots) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $root) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts|TestResults)[\\/]' -and $_.Extension -in $extensions }
})
$files += @(Get-ChildItem -LiteralPath $repoRoot -File | Where-Object { $_.Extension -in $extensions })
$issues = [Collections.Generic.List[string]]::new()
$bomFiles = [Collections.Generic.List[string]]::new()
$editors = 0
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($repoRoot, $file.FullName)
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    try { $text = $utf8.GetString($bytes) }
    catch { $issues.Add("非 UTF-8 文件：$relative"); continue }
    if ($text.Contains([char]0xFFFD)) { $issues.Add("包含替换字符：$relative") }
    if ($file.Extension -eq '.cs') {
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            $bomFiles.Add($relative)
        }
        if ($relative -match '[\\/]Editors[\\/]') {
            $count = [regex]::Matches($text, '\bclass\s+\w+EditorPlugin\s*:').Count
            $editors += $count
            if ($count -gt 1) { $issues.Add("多个步骤编辑器在同一文件：$relative") }
        }
        if ($relative -notmatch '[\\/]UI[\\/]|Plugin\.UI[\\/]|\.Tests[\\/]' -and
            $text -match '\bTestStatus\.(Passed|Failed)\b') {
            $issues.Add("插件执行器不得代替判定步骤返回 Passed/Failed：$relative")
        }
    }
}
foreach ($file in $bomFiles) { $issues.Add("C# 文件应为 UTF-8 无 BOM：$file") }
if ($issues.Count) { throw ($issues -join [Environment]::NewLine) }
Write-Output "UTF-8/编辑器文件/执行状态静态检查通过：$($files.Count) 个文本文件，$editors 个独立编辑器描述。"
Write-Output '此检查不替代语义审查、WPF 运行验证或真实硬件验收。'
