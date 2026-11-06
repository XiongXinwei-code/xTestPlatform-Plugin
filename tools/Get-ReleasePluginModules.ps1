# Each installable module has its own publish directory and version history.
$ErrorActionPreference = 'Stop'
$paths = @(git -C (Join-Path $PSScriptRoot '..') ls-files -- '*Plugin.csproj' '*Plugin.UI.csproj')
if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) { throw '无法获取发布插件项目清单。' }
$modules = [ordered]@{}
foreach ($path in $paths) {
    # <module>/<project> or Widgets/<module>/<project>
    if ($path -notmatch '^(?<source>(?:Widgets/)?(?<name>[^/]+))/[^/]+/[^/]+\.csproj$') {
        throw "插件项目不符合模块目录约定：$path"
    }
    $name = $Matches.name
    $source = $Matches.source
    if (-not $modules.Contains($name)) {
        $modules[$name] = [pscustomobject]@{
            Name = $name; SourcePath = $source; Projects = @(); StepProjects = @(); WidgetProjects = @()
        }
    }
    $module = $modules[$name]
    if ($module.SourcePath -ne $source) { throw "同名发布目录冲突：$source 与 $($module.SourcePath)" }
    $module.Projects += $path
    if ($path -like '*WidgetPlugin.csproj') { $module.WidgetProjects += $path }
    elseif ($path -like '*Plugin.csproj') { $module.StepProjects += $path }
}
foreach ($module in $modules.Values) {
    if ($module.StepProjects.Count + $module.WidgetProjects.Count -eq 0) { throw "$($module.Name) 缺少插件主项目。" }
    $module
}
