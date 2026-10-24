[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$BaselineRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$delivery = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
if (-not $delivery.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw '仅验证本仓库 artifacts 子目录的交付，不操作已部署插件。'
}
$verification = Join-Path $delivery 'verification'
$modules = [ordered]@{ CAN = 33; CiA402 = 18; Ethernet = 14; Http = 6; LIN = 9; Modbus = 6; NiDaq = 12; OpcUa = 10; SerialPort = 5; VISA = 7 }
function Hash([string]$File) { (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant() }

$manifest = Get-Content -LiteralPath (Join-Path $delivery 'build-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (@($manifest.plugins).Count -ne 10) { throw '交付模块数量不是10。' }
$steps = @(foreach ($entry in $modules.GetEnumerator()) {
    $rows = @(Get-Content -LiteralPath (Join-Path $verification "$($entry.Key)-steps.json") -Raw -Encoding UTF8 | ConvertFrom-Json)
    if ($rows.Count -ne $entry.Value) { throw "$($entry.Key) 步骤数量异常：$($rows.Count)" }
    foreach ($row in $rows) {
        [pscustomobject]@{ module = $entry.Key; stepTypeId = $row.stepTypeId; displayName = $row.displayName }
    }
})
if ($steps.Count -ne 120 -or @($steps.stepTypeId | Sort-Object -Unique).Count -ne 120 -or
    @($steps.displayName | Sort-Object -Unique).Count -ne 120) { throw '步骤ID或显示路径重复/缺失。' }

$baselineCount = $null
if ($BaselineRoot) {
    $baseline = [IO.Path]::GetFullPath((Join-Path $repoRoot $BaselineRoot))
    if (-not $baseline.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '基线必须为 artifacts 内的历史元数据。' }
    $old = @(foreach ($module in $modules.Keys) {
        foreach ($row in @(Get-Content -LiteralPath (Join-Path $baseline "verification/$module-steps.json") -Raw -Encoding UTF8 | ConvertFrom-Json)) {
            [pscustomobject]@{ module = $module; stepTypeId = $row.stepTypeId; displayName = $row.displayName }
        }
    })
    $baselineCount = $old.Count
    foreach ($previous in $old) {
        $current = @($steps | Where-Object stepTypeId -CEQ $previous.stepTypeId)
        if ($current.Count -ne 1 -or $current[0].module -cne $previous.module -or $current[0].displayName -cne $previous.displayName) {
            throw "本次非树变更任务丢失/改变历史步骤身份或显示路径：$($previous.stepTypeId)"
        }
    }
    if ($steps.Count -ne $old.Count) { throw '本次任务不应增加/删除步骤。' }
}

foreach ($plugin in $manifest.plugins) {
    if (-not $modules.Contains($plugin.name)) { throw "未知模块：$($plugin.name)" }
    $directory = Join-Path $delivery "Plugins/$($plugin.name)"
    foreach ($file in $plugin.files) {
        $actual = [IO.Path]::GetFullPath((Join-Path $directory $file.path))
        if (-not $actual.StartsWith($directory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '清单文件越界。' }
        if ((Hash $actual) -cne $file.sha256) { throw "清单哈希不匹配：$actual" }
    }
    foreach ($name in @('README.md', 'CHANGELOG.md')) {
        if ((Hash (Join-Path $repoRoot "$($plugin.name)/$name")) -cne (Hash (Join-Path $directory $name))) {
            throw "包内文档不是当前源码：$($plugin.name)/$name"
        }
    }
    $changelog = Get-Content -LiteralPath (Join-Path $directory 'CHANGELOG.md') -Raw -Encoding UTF8
    if ($changelog -notmatch "(?m)^## v$([regex]::Escape($plugin.version)) - ") { throw "缺少当前版本更新记录：$($plugin.name)" }
}

$canCopies = @(Get-ChildItem -LiteralPath (Join-Path $delivery 'Plugins') -Recurse -File -Filter 'CAN.StepPlugin.dll')
if ($canCopies.Count -lt 2 -or @($canCopies | ForEach-Object { Hash $_.FullName } | Sort-Object -Unique).Count -ne 1) {
    throw 'CiA402等消费者的私有CAN不一致。'
}

$tests = @(foreach ($file in Get-ChildItem -LiteralPath $verification -File -Filter '*.Tests.trx') {
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $counter = $xml.TestRun.ResultSummary.Counters
    if (-not $counter -or [int]$counter.total -le 0 -or [int]$counter.total -ne [int]$counter.passed -or
        [int]$counter.failed -ne 0 -or [int]$counter.notExecuted -ne 0) { throw "测试不是全量通过：$($file.Name)" }
    [pscustomobject]@{ file = $file.Name; total = [int]$counter.total; passed = [int]$counter.passed;
        failed = [int]$counter.failed; skipped = [int]$counter.notExecuted; sha256 = Hash $file.FullName }
})
if ($tests.Count -ne 9) { throw "应有9个本轮完整测试项目回执，实际=$($tests.Count)" }
$receipt = [ordered]@{
    capturedAt = [DateTimeOffset]::Now.ToString('O')
    scope = 'Current artifact manifest, metadata identities, documents, private CAN copies and fresh full TRX; not live host/device acceptance.'
    moduleCount = $modules.Count; stepCount = $steps.Count; baselineMetadataCount = $baselineCount
    hardwareValidated = $false; realDebugWindowValidated = $false; privateCanCopies = $canCopies.Count
    projects = $tests.Count; total = ($tests | Measure-Object total -Sum).Sum
    passed = ($tests | Measure-Object passed -Sum).Sum; failed = 0; skipped = 0; tests = $tests; steps = $steps
}
$receipt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $verification 'delivery-contract-receipt.json') -Encoding utf8NoBOM
Write-Output "交付契约通过：10模块/120步骤；$($receipt.projects)套/$($receipt.total)测试；私有CAN $($canCopies.Count)份一致。"
