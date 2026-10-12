<#
.SYNOPSIS
	生成每套插件的无角标图标（插件管理器用）。
.DESCRIPTION
	输出两处：
	  1. 插件顶层目录 icon.png（64px，与 README.md 同级）
	  2. UI 项目 Resources\Icons\{包名}.png（24px）
	预览输出到 tools\Icons\preview\package\。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
. (Join-Path $root 'New-SerialIconStyles.ps1') -LibraryOnly
$SerialDraw = $Styles['A']
${function:New-SerialIcon} = ${function:New-StyleIcon}
. (Join-Path $root 'New-DaqIconStyles.ps1') -LibraryOnly
$DaqDraw = $Styles['D']
${function:New-DaqIcon} = ${function:New-StyleIcon}

$Packages = [ordered]@{
	CAN        = @{ Res = 'CAN\CanPlugin.UI\Resources\Icons\can.png';                Kind = 'letter'; Text = 'CAN';    Color = $Groups['CAN'].Color }
	Ethernet   = @{ Res = 'Ethernet\EthernetPlugin.UI\Resources\Icons\ethernet.png'; Kind = 'letter'; Text = 'TCP/IP'; Color = $Groups['TCP'].Color }
	Http       = @{ Res = 'Http\HttpPlugin.UI\Resources\Icons\http.png';             Kind = 'letter'; Text = 'HTTP';   Color = $Groups['HTTP'].Color }
	LIN        = @{ Res = 'LIN\LinPlugin.UI\Resources\Icons\lin.png';                Kind = 'letter'; Text = 'LIN';    Color = $Groups['LIN'].Color }
	Modbus     = @{ Res = 'Modbus\ModbusPlugin.UI\Resources\Icons\modbus.png';       Kind = 'letter'; Text = 'MB';     Color = $Groups['MB'].Color }
	OpcUa      = @{ Res = 'OpcUa\OpcUaPlugin.UI\Resources\Icons\opcua.png';          Kind = 'letter'; Text = 'OPC';    Color = $Groups['OPC'].Color }
	VISA       = @{ Res = 'VISA\VisaPlugin.UI\Resources\Icons\visa.png';             Kind = 'letter'; Text = 'VISA';   Color = $Groups['VISA'].Color }
	SerialPort = @{ Res = 'SerialPort\SerialPortPlugin.UI\Resources\Icons\serialport.png'; Kind = 'serial' }
	NiDaq      = @{ Res = 'NiDaq\NiDaqPlugin.UI\Resources\Icons\nidaq.png';          Kind = 'daq' }
}

function New-PackageIcon($Pkg, [int]$Size) {
	switch ($Pkg.Kind) {
		'letter' { New-Icon $Pkg.Text $Pkg.Color '' $Size '' }
		'serial' { New-SerialIcon $SerialDraw '' $Size }
		'daq'    { New-DaqIcon $DaqDraw 'sine' '' $Size }
	}
}

$pv = Join-Path $root 'preview\package'
if (-not (Test-Path $pv)) { New-Item -ItemType Directory -Path $pv -Force | Out-Null }

foreach ($name in $Packages.Keys) {
	$pkg = $Packages[$name]
	$b64 = New-PackageIcon $pkg 64
	$b64.Save((Join-Path $repo "$name\icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$b64.Save((Join-Path $pv "${name}_64.png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$b64.Dispose()
	$b24 = New-PackageIcon $pkg 24
	$b24.Save((Join-Path $repo $pkg.Res), [System.Drawing.Imaging.ImageFormat]::Png)
	$b24.Save((Join-Path $pv "${name}_24.png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$b24.Dispose()
	Write-Host "$name -> $name\icon.png, $($pkg.Res)"
}
