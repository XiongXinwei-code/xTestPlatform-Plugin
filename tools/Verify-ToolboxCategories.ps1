param(
    [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$records = @(Get-Content -LiteralPath (Join-Path $repository 'docs/PLUGIN_TOOLBOX_CATEGORIES_20261030.json') -Raw -Encoding utf8 | ConvertFrom-Json)
$stepTypes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

function Read-SourceSteps([string]$source) {
    $pattern = 'public override string StepTypeId\s*=>\s*"([^"]+)";\s*public override string DisplayName\s*=>\s*"([^"]+)"'
    foreach ($pair in [regex]::Matches($source, $pattern)) {
        [pscustomobject]@{ Id = $pair.Groups[1].Value; Name = $pair.Groups[2].Value }
    }
    # CANopen 的六个独立步骤复用基类，ID 来自 Operation，不能只检查第一个字面量。
    if ($source.Contains('public override string StepTypeId => $"CANopen.{Operation}"')) {
        foreach ($pair in [regex]::Matches($source, 'CyclicOperation\.(\w+)\s*=>\s*"([^"]+)"')) {
            [pscustomobject]@{ Id = "CANopen.$($pair.Groups[1].Value)"; Name = $pair.Groups[2].Value }
        }
    }
}

foreach ($record in $records) {
    $source = [IO.File]::ReadAllText((Join-Path $repository $record.source))
    $matches = @(Read-SourceSteps $source | Where-Object Id -CEQ $record.stepTypeId)
    if ($matches.Count -ne 1) { throw "无法唯一定位步骤：$($record.stepTypeId) / $($record.source)" }
    $type = $matches[0].Id
    $name = $matches[0].Name
    if ($type -cne $record.stepTypeId -or $name -cne $record.displayName) {
        throw "工具箱元数据与兼容清单不一致：$($record.source)"
    }
    if ($record.category) {
        $category = [regex]::Match($source, 'public override string Category\s*=>\s*"([^"]+)"').Groups[1].Value
        if ($category -cne $record.category) { throw "步骤 Category 意外改变：$type" }
    }
    if (!$stepTypes.Add($type) -or !$paths.Add($name)) { throw "步骤类型或工具箱路径重复：$type / $name" }
    $segments = $name.Split('_')
    if ($segments.Count -lt 3 -or @($segments | Where-Object { $_ -cnotmatch '^[A-Za-z][A-Za-z0-9 +()/.-]*$' }).Count -gt 0) {
        throw "工具箱分类及步骤名称必须使用英文，且包含功能分组：$name"
    }
    if ($record.category -and $record.category -cnotmatch '^[A-Za-z][A-Za-z0-9 +()/.-]*$') {
        throw "工具箱 Category 必须使用英文：$type"
    }
    if ($type.StartsWith('IO.CanDbc', [StringComparison]::Ordinal) -and !$segments[-1].StartsWith('DBC ', [StringComparison]::Ordinal)) {
        throw "DBC 步骤叶节点须显式包含 DBC：$name"
    }
}

$sourceIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$coreDirectories = @($records.source | ForEach-Object { ($_.Split('/')[0..1] -join '/') } | Sort-Object -Unique)
foreach ($directory in $coreDirectories) {
    foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $repository $directory) -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })) {
        foreach ($step in (Read-SourceSteps ([IO.File]::ReadAllText($file.FullName)))) {
            if (!$sourceIds.Add($step.Id)) { throw "源码中步骤 ID 重复：$($step.Id)" }
            if (!$stepTypes.Contains($step.Id)) { throw "工具箱清单遗漏步骤：$($step.Id)" }
        }
    }
}
if (!$sourceIds.SetEquals($stepTypes)) { throw '工具箱清单与源码步骤集合不一致' }

if ($PackageRoot) {
    foreach ($package in ($records.package | Sort-Object -Unique)) {
        $expected = @($records | Where-Object package -EQ $package)
        $metadataPath = Join-Path $PackageRoot "verification/$package-steps.json"
        $actual = @(Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8 | ConvertFrom-Json)
        if ($actual.Count -ne $expected.Count) { throw "$package 编译后步骤数量不匹配" }
        foreach ($record in $expected) {
            $matched = @($actual | Where-Object stepTypeId -CEQ $record.stepTypeId)
            if ($matched.Count -ne 1 -or $matched[0].displayName -cne $record.displayName) {
                throw "编译后工具箱名称不匹配：$($record.stepTypeId)"
            }
        }
    }
}

[pscustomobject]@{
    Steps = $records.Count
    Packages = @($records.package | Sort-Object -Unique).Count
    SourceVerified = $true
    CompiledMetadataVerified = [bool]$PackageRoot
} | ConvertTo-Json
