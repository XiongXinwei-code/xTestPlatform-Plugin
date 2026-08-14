# 为每个插件目录生成 README.md / CHANGELOG.md / plugin.json
# 用法: & .\tools\Generate-PluginDocs.ps1
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = Split-Path $PSScriptRoot -Parent
$today = Get-Date -Format 'yyyy-MM-dd'

# 各插件的功能概述 / 支持的硬件或协议 / 使用前提（面向使用者）
$meta = @{
	'CAN' = @{
		Overview = '通过 CAN 总线与被测设备通信，支持 CAN 2.0 Classic 与 CAN FD 报文的收发、周期发送，并内置 UDS 诊断（ISO 14229）与 XCP 标定协议步骤。'
		Hardware = @('Vector（需安装 XL Driver Library）', 'PEAK PCAN', 'NI-XNET', 'ZLG 周立功', 'Kvaser', 'TOSUN 同星')
		Prereq   = '使用前需安装对应硬件厂商的驱动程序；UDS/XCP 步骤需先通过 CAN_Open 打开通道。'
	}
	'Ethernet' = @{
		Overview = '通过以太网与被测设备通信，提供 TCP 客户端连接与收发、UDP 收发，并支持车载以太网 DoIP 诊断（ISO 13400）与 SOME/IP 服务调用、订阅及服务发现。'
		Hardware = @('标准以太网网卡（TCP/UDP）', 'DoIP 诊断网关 / ECU', 'SOME/IP 服务节点')
		Prereq   = '需保证测试机与被测设备网络可达；DoIP/SOME/IP 步骤需按对应协议配置 IP 与端口。'
	}
	'LabVIEWCall' = @{
		Overview = '调用 LabVIEW 编写的 VI 程序并交换输入输出参数，便于复用既有 LabVIEW 测试资产。'
		Hardware = @('无特定硬件要求（取决于被调用 VI）')
		Prereq   = '需安装与 VI 版本匹配的 LabVIEW Runtime 或开发环境。'
	}
	'LIN' = @{
		Overview = '通过 LIN 总线与被测设备通信，支持主节点报文的读、写、写读组合以及调度表周期发送的启停。'
		Hardware = @('Vector（XL Driver Library）', 'PEAK PLIN', 'NI-XNET')
		Prereq   = '使用前需安装对应硬件厂商的驱动程序，并先通过 LIN_Open 打开通道。'
	}
	'Modbus' = @{
		Overview = '通过 Modbus 协议与 PLC、仪表等设备通信，支持 TCP/RTU 连接管理及线圈、寄存器的单点与批量读写。'
		Hardware = @('Modbus TCP 设备（以太网）', 'Modbus RTU 设备（串口）')
		Prereq   = '需先通过 Modbus_Connect 建立连接；RTU 模式需正确配置串口参数。'
	}
	'NiDaq' = @{
		Overview = '基于 NI-DAQmx 进行数据采集与信号输出，支持模拟量采集、数字量读写、编码器计数以及多任务同步采集。'
		Hardware = @('NI 数据采集卡（USB / PCIe / cDAQ 等，需 NI-DAQmx 驱动）')
		Prereq   = '需安装 NI-DAQmx 驱动，并在 NI MAX 中确认设备名称。'
	}
	'OpcUa' = @{
		Overview = '通过 OPC UA 协议与 PLC、SCADA 等服务器通信，支持节点的单点与批量读写、订阅监控以及后台数据采集的启停与读取。'
		Hardware = @('任何符合 OPC UA 规范的服务器（如西门子、倍福 PLC）')
		Prereq   = '需保证 OPC UA 服务器地址可达，并按服务器要求配置安全策略与凭据。'
	}
	'SerialPort' = @{
		Overview = '通过串口（RS-232/485）与设备通信，支持串口的打开关闭、数据读写以及一发一收查询。'
		Hardware = @('本机串口或 USB 转串口适配器')
		Prereq   = '需确认串口号、波特率等参数与设备一致。'
	}
	'VISA' = @{
		Overview = '通过 VISA 标准与仪器通信（SCPI 指令），支持仪器会话管理、读写、查询、批量下发以及 *OPC? 同步等待。'
		Hardware = @('GPIB / USB / LAN(LXI) / 串口接口的仪器（需 NI-VISA 或兼容运行库）')
		Prereq   = '需安装 NI-VISA（或 Keysight IO Libraries 等兼容实现），并确认仪器资源名。'
	}
}

# 从 Description 原始字符串中提取"## 功能"后的第一行非空文本作为简述
function Get-ShortDescription([string]$content) {
	$m = [regex]::Match($content, '(?s)Description\s*=>\s*"""(.*?)"""')
	if (-not $m.Success) {
		$m = [regex]::Match($content, 'Description\s*=>\s*"([^"]+)"')
		if ($m.Success) { return $m.Groups[1].Value.Trim() }
		return ''
	}
	$body = $m.Groups[1].Value
	$lines = $body -split "`r?`n"
	$inFunc = $false
	foreach ($line in $lines) {
		$t = $line.Trim()
		if ($t -match '^##\s*功能') { $inFunc = $true; continue }
		if ($inFunc) {
			if ($t -match '^##') { break }
			if ($t) { return $t }
		}
	}
	foreach ($line in $lines) { $t = $line.Trim(); if ($t -and $t -notmatch '^#') { return $t } }
	return ''
}

$pluginDirs = Get-ChildItem $root -Directory | Where-Object {
	(Get-ChildItem $_.FullName -Recurse -Filter '*Plugin*.csproj' -ErrorAction SilentlyContinue).Count -gt 0
}

foreach ($dir in $pluginDirs) {
	$name = $dir.Name
	Write-Host "处理插件: $name"

	# 版本号：取主 csproj 的 <Version>
	$csproj = Get-ChildItem $dir.FullName -Recurse -Filter '*.csproj' | Where-Object { $_.Name -notmatch '\.UI\.' } | Select-Object -First 1
	$version = '1.0.0'
	$raw = Get-Content $csproj.FullName -Raw
	if ($raw -match '<Version>([^<]+)</Version>') { $version = $Matches[1].Trim() }

	# 提取所有 Step 的 DisplayName / Description（排除 UI 项目与 obj/bin）
	$steps = @()
	$csFiles = Get-ChildItem $dir.FullName -Recurse -Filter '*.cs' |
		Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.FullName -notmatch '\.UI\\' }
	foreach ($f in $csFiles) {
		$c = Get-Content $f.FullName -Raw -Encoding UTF8
		$dn = [regex]::Match($c, 'override\s+string\s+DisplayName\s*=>\s*"([^"]+)"')
		if (-not $dn.Success) { continue }
		$desc = Get-ShortDescription $c
		$steps += [pscustomobject]@{ displayName = $dn.Groups[1].Value; description = $desc }
	}
	$steps = @($steps | Sort-Object displayName)

	# ── plugin.json ──
	if ($steps.Count -eq 0) {
		$stepsJson = '[]'
	} elseif ($steps.Count -eq 1) {
		$stepsJson = "[`n$($steps[0] | ConvertTo-Json -Depth 3)`n]"
	} else {
		$stepsJson = ($steps | ConvertTo-Json -Depth 3)
	}
	$pluginJson = "{`n  `"steps`": $($stepsJson -replace "`n", "`n  ")`n}`n"
	[IO.File]::WriteAllText((Join-Path $dir.FullName 'plugin.json'), $pluginJson, $utf8)

	# ── README.md ──
	$m = $meta[$name]
	$sb = New-Object System.Text.StringBuilder
	[void]$sb.AppendLine("# $name 插件")
	[void]$sb.AppendLine()
	[void]$sb.AppendLine('## 功能概述')
	[void]$sb.AppendLine()
	[void]$sb.AppendLine($m.Overview)
	[void]$sb.AppendLine()
	[void]$sb.AppendLine('## 支持的硬件/协议')
	[void]$sb.AppendLine()
	foreach ($h in $m.Hardware) { [void]$sb.AppendLine("- $h") }
	[void]$sb.AppendLine()
	[void]$sb.AppendLine('## 包含的步骤')
	[void]$sb.AppendLine()
	[void]$sb.AppendLine('| 步骤 | 说明 |')
	[void]$sb.AppendLine('|------|------|')
	foreach ($s in $steps) { [void]$sb.AppendLine("| $($s.displayName) | $($s.description) |") }
	[void]$sb.AppendLine()
	[void]$sb.AppendLine('## 使用前提')
	[void]$sb.AppendLine()
	[void]$sb.AppendLine($m.Prereq)
	[IO.File]::WriteAllText((Join-Path $dir.FullName 'README.md'), $sb.ToString(), $utf8)

	# ── CHANGELOG.md ──（已存在则不覆盖，避免抹掉手写记录）
	$clPath = Join-Path $dir.FullName 'CHANGELOG.md'
	if (-not (Test-Path $clPath)) {
		$cl = "# 更新记录`r`n`r`n## v$version - $today`r`n- 新增：首次发布`r`n"
		[IO.File]::WriteAllText($clPath, $cl, $utf8)
	}

	Write-Host "  ✅ $name：$($steps.Count) 个步骤，版本 v$version"
}
Write-Host '全部完成'
