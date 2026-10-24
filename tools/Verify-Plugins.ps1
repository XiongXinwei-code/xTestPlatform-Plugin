[CmdletBinding()]
param(
    [ValidateSet('Inventory', 'Test', 'Package', 'Validate')][string]$Mode = 'Inventory',
    [string]$OutputRoot = 'artifacts/Release/2026-10-23-audit-fixes'
)

$ErrorActionPreference = 'Stop'
$utf8Console = [Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8Console
[Console]::OutputEncoding = $utf8Console
$OutputEncoding = $utf8Console
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
if (-not $outputPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw '验证/编译输出必须位于本仓库 artifacts 的子目录。此工具不删除或替换已部署插件。'
}
Set-Location -LiteralPath $repoRoot
$modules = @(
    @{ Name = 'CAN'; Project = 'CanPlugin' },
    @{ Name = 'CiA402'; Project = 'Cia402Plugin' },
    @{ Name = 'Ethernet'; Project = 'EthernetPlugin' },
    @{ Name = 'Http'; Project = 'HttpPlugin' },
    @{ Name = 'LIN'; Project = 'LinPlugin' },
    @{ Name = 'Modbus'; Project = 'ModbusPlugin' },
    @{ Name = 'NiDaq'; Project = 'NiDaqPlugin' },
    @{ Name = 'OpcUa'; Project = 'OpcUaPlugin' },
    @{ Name = 'SerialPort'; Project = 'SerialPortPlugin' },
    @{ Name = 'VISA'; Project = 'VisaPlugin' }
)
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet 失败，退出码=$LASTEXITCODE，参数=$($Arguments -join ' ')" }
}
function Read-Project([string]$Path) {
    [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $version = @($document.Project.PropertyGroup.Version | Where-Object { $_ })[0]
    $assembly = @($document.Project.PropertyGroup.AssemblyName | Where-Object { $_ })[0]
    if (-not $version -or -not $assembly) { throw "缺少显式版本/程序集名称：$Path" }
    return @{ Version = [string]$version; Assembly = [string]$assembly }
}
foreach ($module in $modules) {
    $module.CorePath = "$($module.Name)/$($module.Project)/$($module.Project).csproj"
    $module.UiPath = "$($module.Name)/$($module.Project).UI/$($module.Project).UI.csproj"
    $module.Core = Read-Project $module.CorePath
    $module.UI = Read-Project $module.UiPath
    if ($module.Core.Version -ne $module.UI.Version) { throw "$($module.Name) 核心/UI 版本不一致" }
}
if ($Mode -eq 'Inventory') {
    $modules | ForEach-Object { Write-Output "$($_.Name) $($_.Core.Version) $($_.CorePath) $($_.UiPath)" }
    exit 0
}
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$logs = Join-Path $outputPath 'verification'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
if ($Mode -eq 'Test') {
    $tests = @(foreach ($root in @($modules.Name) + @('tests')) {
        Get-ChildItem -LiteralPath $root -Recurse -Filter '*.Tests.csproj' -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }
    })
    if ($tests.Count -lt 9) { throw "测试项目库存异常：$($tests.Count)" }
    foreach ($test in $tests) {
        $testOutput = Join-Path $logs "test-bin/$($test.BaseName)"
        Invoke-Dotnet @('test', $test.FullName, '-c', 'Release', '--no-restore', '-m:1', '-v', 'minimal',
            "-p:OutputPath=$testOutput", '--logger', "trx;LogFileName=$($test.BaseName).trx", '--results-directory', $logs,
            '--blame-hang-timeout', '60s')
    }
    exit 0
}
if ($Mode -eq 'Package') {
    foreach ($module in $modules) {
        $destination = Join-Path $outputPath "Plugins/$($module.Name)"
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Invoke-Dotnet @('build', $module.UiPath, '-c', 'Release', '-t:Rebuild', '-m:1', '-v', 'minimal', '--no-restore',
            '-r', 'win-x64', '-p:Platform=x64', '-p:SelfContained=false', '-p:AppendRuntimeIdentifierToOutputPath=false',
            "-p:OutputPath=$destination", "-flp:logfile=$logs/$($module.Name)-build.log;verbosity=normal")
        foreach ($file in @('icon.png', 'README.md', 'CHANGELOG.md')) {
            $source = Join-Path $module.Name $file
            if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $destination -Force }
        }
    }
}

Invoke-Dotnet @('build', 'tools/StepInfoExtractor/StepInfoExtractor.csproj', '-c', 'Release', '--no-restore',
    '-v', 'minimal', "-p:OutputPath=$logs/inspector")
$records = @()
foreach ($module in $modules) {
    $destination = Join-Path $outputPath "Plugins/$($module.Name)"
    foreach ($project in @($module.Core, $module.UI)) {
        $dll = Join-Path $destination "$($project.Assembly).dll"
        if (-not (Test-Path -LiteralPath $dll)) { throw "缺少插件程序集：$dll" }
        $actualVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
        if ($actualVersion -ne [version]"$($project.Version).0") { throw "产物版本错误：$dll ($actualVersion)" }
    }
    $testFiles = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Where-Object {
        $_.Name -match '(\.Tests\.dll$|^xunit|^testhost|^Microsoft\.TestPlatform)'
    })
    if ($testFiles.Count) { throw "部署包混入测试文件：$($testFiles.FullName -join ', ')" }
    $metadata = & dotnet (Join-Path $logs 'inspector/StepInfoExtractor.dll') $destination '--strict'
    if ($LASTEXITCODE -ne 0) { throw "$($module.Name) 核心/UI 类型与依赖加载失败" }
    $metadataJson = $metadata -join [Environment]::NewLine
    [IO.File]::WriteAllText((Join-Path $logs "$($module.Name)-steps.json"), $metadataJson, [Text.UTF8Encoding]::new($false))
    $steps = @($metadataJson | ConvertFrom-Json)
    if (-not $steps.Count) { throw "$($module.Name) 未发现插件步骤" }
    $portsDll = Join-Path $destination 'System.IO.Ports.dll'
    if (Test-Path -LiteralPath $portsDll) {
        # Both core and UI must use the same Windows RID asset, not the package's throw-only placeholder.
        $packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
        $windowsPorts = Join-Path $packageRoot 'system.io.ports/8.0.0/runtimes/win/lib/net8.0/System.IO.Ports.dll'
        if (-not (Test-Path -LiteralPath $windowsPorts)) { throw '无法校验 System.IO.Ports 的 Windows 运行时资产' }
        if ((Get-FileHash -LiteralPath $portsDll).Hash -ne (Get-FileHash -LiteralPath $windowsPorts).Hash) {
            throw "$($module.Name) 的 System.IO.Ports 不是 Windows 运行实现"
        }
    }
    $files = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($destination, $_.FullName).Replace('\', '/');
           bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $records += @{ name = $module.Name; version = $module.Core.Version; stepCount = $steps.Count; files = $files }
}
$manifest = @{ generatedUtc = [DateTime]::UtcNow.ToString('o'); target = 'net8.0-windows/win-x64';
    hardwareValidated = $false; sourceCommit = (& git rev-parse HEAD); sourceIncludesUncommittedChanges = $true; plugins = $records }
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $outputPath 'build-manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
Write-Output "已验证 $($records.Count) 个独立插件文件夹；清单：$outputPath/build-manifest.json"
