<#
.SYNOPSIS
	生成插件字母图标（C 方案：浅底铭牌 + 墨色字母 + 组主色底条 + 右下角操作角标）。
.DESCRIPTION
	所有图形在 24x24 设计坐标中绘制，按目标尺寸等比缩放。
	$Groups 中每组定义：铭牌文字、主色、输出目录、{文件名 = 角标} 映射。
	输出：每组目录下 {name}.png（24px），以及 tools\Icons\preview\{group}\ 下 48px 与预览拼图。
.EXAMPLE
	powershell -NoProfile -File tools\Icons\New-PluginLetterIcons.ps1 -Group UDS
#>
[CmdletBinding()]
param([string[]]$Group, [switch]$LibraryOnly, [switch]$Square, [switch]$PreviewOnly)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repo = Split-Path -Parent (Split-Path -Parent $root)

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

$Ink       = C 34 34 38
$Paper     = C 246 246 248
$PlateEdge = C 208 208 214
$White     = C 255 255 255

$BadgeGreen  = C 22 150 78
$BadgeBlue   = C 32 110 214
$BadgeRed    = C 214 45 45
$BadgeOrange = C 235 120 20
$BadgeAmber  = C 230 160 0
$BadgeTeal   = C 0 150 136
$BadgeGray   = C 110 110 120
$BadgeGold   = C 190 140 20
$BadgePink   = C 220 60 110

# 角标：颜色 + 符号
$Badges = @{
	open        = @{ Color = $BadgeBlue;   Mark = 'arrowRight' }
	close       = @{ Color = $BadgeRed;    Mark = 'cross' }
	read        = @{ Color = $BadgeGreen;  Mark = 'arrowDown' }
	write       = @{ Color = $BadgeOrange; Mark = 'arrowUp' }
	start       = @{ Color = $BadgeGreen;  Mark = 'play' }
	stop        = @{ Color = $BadgeRed;    Mark = 'square' }
	session     = @{ Color = $BadgeBlue;   Mark = 'arrowRight' }
	dtcRead     = @{ Color = $BadgeAmber;  Mark = 'exclaim' }
	dtcClear    = @{ Color = $BadgeRed;    Mark = 'minus' }
	routine     = @{ Color = $BadgeTeal;   Mark = 'play' }
	security    = @{ Color = $BadgeGold;   Mark = 'lock' }
	raw         = @{ Color = $BadgeGray;   Mark = 'swap' }
	send        = @{ Color = $BadgeOrange; Mark = 'arrowRight' }
	receive     = @{ Color = $BadgeGreen;  Mark = 'arrowLeft' }
	nmt         = @{ Color = $BadgeBlue;   Mark = 'power' }
	heartbeat   = @{ Color = $BadgePink;   Mark = 'heart' }
	sync        = @{ Color = $BadgeTeal;   Mark = 'pulse' }
	emcy        = @{ Color = $BadgeRed;    Mark = 'exclaim' }
	flash       = @{ Color = $BadgeAmber;  Mark = 'bolt' }
	batchWrite  = @{ Color = $BadgeOrange; Mark = 'arrowUp2' }
	wait        = @{ Color = $BadgeTeal;   Mark = 'clock' }
	discover    = @{ Color = $BadgeTeal;   Mark = 'search' }
	subscribe   = @{ Color = $BadgeAmber;  Mark = 'plus' }
	batchRead   = @{ Color = $BadgeGreen;  Mark = 'arrowDown2' }
	writeRead   = @{ Color = $BadgeGray;   Mark = 'swap' }
	sleep       = @{ Color = $BadgeGray;   Mark = 'moon' }
	wakeup      = @{ Color = $BadgeAmber;  Mark = 'sun' }
	extract     = @{ Color = $BadgeTeal;   Mark = 'brackets' }
	config      = @{ Color = $BadgeGray;   Mark = 'gear' }
}

$Groups = [ordered]@{
	CAN = @{
		Text  = 'CAN'
		Color = C 0 120 150
		Dir   = 'CAN\CanPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			can_open         = 'open'
			can_close        = 'close'
			can_read         = 'read'
			can_write        = 'write'
			can_cyclic_start = 'start'
			can_cyclic_stop  = 'stop'
		}
	}
	UDS = @{
		Text  = 'UDS'
		Color = C 120 70 170
		Dir   = 'CAN\CanPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			uds_diag_session    = 'session'
			uds_read_did        = 'read'
			uds_write_did       = 'write'
			uds_read_dtc        = 'dtcRead'
			uds_clear_dtc       = 'dtcClear'
			uds_routine_control = 'routine'
			uds_security_access = 'security'
			uds_raw_request     = 'raw'
			uds_flash           = 'flash'
		}
	}
	XCP = @{
		Text  = 'XCP'
		Color = C 200 110 20
		Dir   = 'CAN\CanPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			xcp_connect        = 'open'
			xcp_disconnect     = 'close'
			xcp_short_upload   = 'read'
			xcp_short_download = 'write'
		}
	}
	CANopen = @{
		Text  = 'CAN'
		Bar   = 'double'
		Color = C 40 140 60
		Dir   = 'CAN\CanPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			canopen_sdo_read       = 'read'
			canopen_sdo_write      = 'write'
			canopen_nmt_control    = 'nmt'
			canopen_wait_heartbeat = 'heartbeat'
			canopen_pdo_send       = 'send'
			canopen_pdo_receive    = 'receive'
			canopen_sync_send      = 'sync'
			canopen_emcy_receive   = 'emcy'
		}
	}
	VISA = @{
		Text  = 'VISA'
		Color = C 30 90 180
		Dir   = 'VISA\VisaPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			visa_open        = 'open'
			visa_close       = 'close'
			visa_read        = 'read'
			visa_write       = 'write'
			visa_batch_write = 'batchWrite'
			visa_query       = 'raw'
			visa_wait_opc    = 'wait'
		}
	}
	# 网口：统一靛蓝色系，底条样式区分协议
	TCP = @{
		Text  = 'TCP'
		Bar   = 'solid'
		Color = C 60 70 190
		Dir   = 'Ethernet\EthernetPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			tcp_open    = 'open'
			tcp_close   = 'close'
			tcp_send    = 'send'
			tcp_receive = 'receive'
		}
	}
	UDP = @{
		Text  = 'UDP'
		Bar   = 'dashed'
		Color = C 60 70 190
		Dir   = 'Ethernet\EthernetPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			udp_send    = 'send'
			udp_receive = 'receive'
		}
	}
	DoIP = @{
		Text  = 'DoIP'
		Bar   = 'double'
		Color = C 60 70 190
		Dir   = 'Ethernet\EthernetPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			doip_connect           = 'open'
			doip_disconnect        = 'close'
			doip_diag_request      = 'raw'
			doip_vehicle_discovery = 'discover'
		}
	}
	SOMEIP = @{
		Text  = 'SOME'
		Bar   = 'triple'
		Color = C 60 70 190
		Dir   = 'Ethernet\EthernetPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			someip_request         = 'raw'
			someip_fire_and_forget = 'send'
			someip_sd_discover     = 'discover'
			someip_subscribe       = 'subscribe'
		}
	}
	LIN = @{
		Text  = 'LIN'
		Color = C 200 120 0
		Dir   = 'LIN\LinPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			lin_open         = 'open'
			lin_close        = 'close'
			lin_read         = 'read'
			lin_write        = 'write'
			lin_write_read   = 'writeRead'
			lin_cyclic_start = 'start'
			lin_cyclic_stop  = 'stop'
			lin_sleep        = 'sleep'
			lin_wakeup       = 'wakeup'
		}
	}
	HTTP = @{
		Text  = 'HTTP'
		Color = C 0 140 120
		Dir   = 'Http\HttpPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			http_client_create = 'open'
			http_client_close  = 'close'
			http_request       = 'raw'
			http_soap_request  = 'raw'
			http_json_extract  = 'extract'
			http_xml_extract   = 'extract'
		}
	}
	OPC = @{
		Text  = 'OPC'
		Color = C 90 110 40
		Dir   = 'OpcUa\OpcUaPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			opcua_connect        = 'open'
			opcua_disconnect     = 'close'
			opcua_read           = 'read'
			opcua_write          = 'write'
			opcua_batch_read     = 'batchRead'
			opcua_batch_write    = 'batchWrite'
			opcua_subscribe      = 'subscribe'
			opcua_data_acq_start = 'start'
			opcua_data_acq_stop  = 'stop'
			opcua_data_acq_read  = 'batchRead'
		}
	}
	MB = @{
		Text  = 'MB'
		Color = C 160 50 40
		Dir   = 'Modbus\ModbusPlugin.UI\Resources\Icons'
		Icons = [ordered]@{
			modbus_connect     = 'open'
			modbus_disconnect  = 'close'
			modbus_read        = 'read'
			modbus_write       = 'write'
			modbus_batch_read  = 'batchRead'
			modbus_batch_write = 'batchWrite'
		}
	}
}

$fontCandidates = @('Bahnschrift', 'Arial Narrow', 'Segoe UI')
$installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name }
$FontName = $fontCandidates | Where-Object { $installed -contains $_ } | Select-Object -First 1

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
	$p = New-Object System.Drawing.Drawing2D.GraphicsPath
	$d = $r * 2
	$p.AddArc($x, $y, $d, $d, 180, 90)
	$p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
	$p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$p.CloseFigure()
	$p
}

function Add-FitText($G, [string]$Text, [System.Drawing.RectangleF]$Box, $Color) {
	$ff = New-Object System.Drawing.FontFamily($FontName)
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$path.AddString($Text, $ff, [int][System.Drawing.FontStyle]::Bold, 100, (New-Object System.Drawing.PointF(0, 0)), [System.Drawing.StringFormat]::GenericTypographic)
	$b = $path.GetBounds()
	$s = [Math]::Min($Box.Width / $b.Width, $Box.Height / $b.Height)
	$ox = $Box.X + ($Box.Width - $b.Width * $s) / 2
	$oy = $Box.Y + ($Box.Height - $b.Height * $s) / 2
	$m = New-Object System.Drawing.Drawing2D.Matrix
	$m.Translate($ox, $oy)
	$m.Scale($s, $s)
	$m.Translate(-$b.X, -$b.Y)
	$path.Transform($m)
	$br = New-Object System.Drawing.SolidBrush($Color)
	$G.FillPath($br, $path)
	$br.Dispose(); $path.Dispose(); $ff.Dispose()
}

function Add-Line($G, [single]$x1, [single]$y1, [single]$x2, [single]$y2, [single]$w, $Color) {
	$pen = New-Object System.Drawing.Pen($Color, $w)
	$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
	$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
	$G.DrawLine($pen, $x1, $y1, $x2, $y2)
	$pen.Dispose()
}

function Fill-Poly($G, [single[]]$pts, $Color) {
	$arr = for ($i = 0; $i -lt $pts.Count; $i += 2) { New-Object System.Drawing.PointF($pts[$i], $pts[$i + 1]) }
	$br = New-Object System.Drawing.SolidBrush($Color)
	$G.FillPolygon($br, [System.Drawing.PointF[]]$arr)
	$br.Dispose()
}

function Fill-Rect($G, [single]$x, [single]$y, [single]$w, [single]$h, $Color) {
	$br = New-Object System.Drawing.SolidBrush($Color)
	$G.FillRectangle($br, $x, $y, $w, $h)
	$br.Dispose()
}

function Add-Badge($G, [string]$Badge, [single]$cx, [single]$cy, [single]$r) {
	$def = $Badges[$Badge]
	$rb = New-Object System.Drawing.SolidBrush($White)
	$G.FillEllipse($rb, $cx - $r - 1, $cy - $r - 1, ($r + 1) * 2, ($r + 1) * 2)
	$rb.Dispose()
	$br = New-Object System.Drawing.SolidBrush($def.Color)
	$G.FillEllipse($br, $cx - $r, $cy - $r, $r * 2, $r * 2)
	$br.Dispose()
	$k = $r * 0.5; $w = 1.5
	switch ($def.Mark) {
		'arrowRight' {
			Add-Line $G ($cx - $k) $cy ($cx + $k) $cy $w $White
			Add-Line $G ($cx + $k) $cy ($cx + 0.1) ($cy - $k) $w $White
			Add-Line $G ($cx + $k) $cy ($cx + 0.1) ($cy + $k) $w $White
		}
		'cross' {
			Add-Line $G ($cx - $k * 0.8) ($cy - $k * 0.8) ($cx + $k * 0.8) ($cy + $k * 0.8) $w $White
			Add-Line $G ($cx + $k * 0.8) ($cy - $k * 0.8) ($cx - $k * 0.8) ($cy + $k * 0.8) $w $White
		}
		'arrowDown' {
			Add-Line $G $cx ($cy - $k) $cx ($cy + $k) $w $White
			Add-Line $G $cx ($cy + $k) ($cx - $k) ($cy + 0.1) $w $White
			Add-Line $G $cx ($cy + $k) ($cx + $k) ($cy + 0.1) $w $White
		}
		'arrowUp' {
			Add-Line $G $cx ($cy + $k) $cx ($cy - $k) $w $White
			Add-Line $G $cx ($cy - $k) ($cx - $k) ($cy - 0.1) $w $White
			Add-Line $G $cx ($cy - $k) ($cx + $k) ($cy - 0.1) $w $White
		}
		'play' {
			Fill-Poly $G @(($cx - $k * 0.7), ($cy - $k), ($cx + $k), $cy, ($cx - $k * 0.7), ($cy + $k)) $White
		}
		'square' {
			Fill-Rect $G ($cx - $k * 0.8) ($cy - $k * 0.8) ($k * 1.6) ($k * 1.6) $White
		}
		'minus' {
			Add-Line $G ($cx - $k) $cy ($cx + $k) $cy 1.8 $White
		}
		'exclaim' {
			Add-Line $G $cx ($cy - $k * 1.1) $cx ($cy + $k * 0.25) 1.6 $White
			Fill-Rect $G ($cx - 0.8) ($cy + $k * 0.65) 1.6 1.6 $White
		}
		'lock' {
			# 锁体 + 锁梁
			Fill-Rect $G ($cx - $k * 0.95) ($cy - $k * 0.1) ($k * 1.9) ($k * 1.25) $White
			$pen = New-Object System.Drawing.Pen($White, 1.1)
			$G.DrawArc($pen, $cx - $k * 0.6, $cy - $k * 1.05, $k * 1.2, $k * 1.4, 180, 180)
			$G.DrawLine($pen, $cx - $k * 0.6, $cy - $k * 0.35, $cx - $k * 0.6, $cy - $k * 0.05)
			$G.DrawLine($pen, $cx + $k * 0.6, $cy - $k * 0.35, $cx + $k * 0.6, $cy - $k * 0.05)
			$pen.Dispose()
		}
		'swap' {
			# 上：右向请求；下：左向响应
			$u = $cy - $k * 0.5; $d = $cy + $k * 0.5
			Add-Line $G ($cx - $k) $u ($cx + $k) $u 1.2 $White
			Add-Line $G ($cx + $k) $u ($cx + $k * 0.35) ($u - $k * 0.5) 1.2 $White
			Add-Line $G ($cx + $k) $d ($cx - $k) $d 1.2 $White
			Add-Line $G ($cx - $k) $d ($cx - $k * 0.35) ($d + $k * 0.5) 1.2 $White
		}
		'arrowLeft' {
			Add-Line $G ($cx + $k) $cy ($cx - $k) $cy $w $White
			Add-Line $G ($cx - $k) $cy ($cx - 0.1) ($cy - $k) $w $White
			Add-Line $G ($cx - $k) $cy ($cx - 0.1) ($cy + $k) $w $White
		}
		'power' {
			# 电源符号：开口圆弧 + 竖线，表示节点状态控制
			$pen = New-Object System.Drawing.Pen($White, 1.3)
			$G.DrawArc($pen, $cx - $k * 0.95, $cy - $k * 0.75, $k * 1.9, $k * 1.9, 300, 300)
			$pen.Dispose()
			Add-Line $G $cx ($cy - $k * 1.1) $cx ($cy + 0.1) 1.3 $White
		}
		'heart' {
			$hb = New-Object System.Drawing.SolidBrush($White)
			$hr = $k * 0.55
			$G.FillEllipse($hb, $cx - $hr * 2, $cy - $hr * 1.6, $hr * 2.1, $hr * 2.1)
			$G.FillEllipse($hb, $cx - $hr * 0.1, $cy - $hr * 1.6, $hr * 2.1, $hr * 2.1)
			$hb.Dispose()
			Fill-Poly $G @(($cx - $hr * 1.95), ($cy - $hr * 0.3), ($cx + $hr * 1.95), ($cy - $hr * 0.3), $cx, ($cy + $hr * 1.9)) $White
		}
		'pulse' {
			# 方波：同步时钟
			$pen = New-Object System.Drawing.Pen($White, 1.1)
			$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
			$pts = [System.Drawing.PointF[]]@(
				(New-Object System.Drawing.PointF(($cx - $k * 1.1), ($cy + $k * 0.6))),
				(New-Object System.Drawing.PointF(($cx - $k * 0.4), ($cy + $k * 0.6))),
				(New-Object System.Drawing.PointF(($cx - $k * 0.4), ($cy - $k * 0.6))),
				(New-Object System.Drawing.PointF(($cx + $k * 0.4), ($cy - $k * 0.6))),
				(New-Object System.Drawing.PointF(($cx + $k * 0.4), ($cy + $k * 0.6))),
				(New-Object System.Drawing.PointF(($cx + $k * 1.1), ($cy + $k * 0.6))))
			$G.DrawLines($pen, $pts)
			$pen.Dispose()
		}
		'arrowUp2' {
			# 双层上箭头：批量写入
			foreach ($dy in @(-0.45, 0.45)) {
				$ty = $cy + $k * $dy
				Add-Line $G ($cx - $k) ($ty + $k * 0.45) $cx ($ty - $k * 0.45) 1.3 $White
				Add-Line $G $cx ($ty - $k * 0.45) ($cx + $k) ($ty + $k * 0.45) 1.3 $White
			}
		}
		'clock' {
			$pen = New-Object System.Drawing.Pen($White, 1.1)
			$G.DrawEllipse($pen, $cx - $k * 1.1, $cy - $k * 1.1, $k * 2.2, $k * 2.2)
			$pen.Dispose()
			Add-Line $G $cx ($cy - $k * 0.65) $cx $cy 1.1 $White
			Add-Line $G $cx $cy ($cx + $k * 0.5) ($cy + $k * 0.3) 1.1 $White
		}
		'search' {
			$pen = New-Object System.Drawing.Pen($White, 1.2)
			$G.DrawEllipse($pen, $cx - $k * 0.95, $cy - $k * 0.95, $k * 1.3, $k * 1.3)
			$pen.Dispose()
			Add-Line $G ($cx + $k * 0.3) ($cy + $k * 0.3) ($cx + $k * 0.95) ($cy + $k * 0.95) 1.5 $White
		}
		'arrowDown2' {
			foreach ($dy in @(-0.45, 0.45)) {
				$ty = $cy + $k * $dy
				Add-Line $G ($cx - $k) ($ty - $k * 0.45) $cx ($ty + $k * 0.45) 1.3 $White
				Add-Line $G $cx ($ty + $k * 0.45) ($cx + $k) ($ty - $k * 0.45) 1.3 $White
			}
		}
		'moon' {
			$mb = New-Object System.Drawing.SolidBrush($White)
			$G.FillEllipse($mb, $cx - $k, $cy - $k, $k * 2, $k * 2)
			$mb.Dispose()
			$cb = New-Object System.Drawing.SolidBrush($Badges['sleep'].Color)
			$G.FillEllipse($cb, $cx - $k * 0.4, $cy - $k * 1.3, $k * 2, $k * 2)
			$cb.Dispose()
		}
		'sun' {
			$sb = New-Object System.Drawing.SolidBrush($White)
			$G.FillEllipse($sb, $cx - $k * 0.5, $cy - $k * 0.5, $k, $k)
			$sb.Dispose()
			foreach ($a in 0, 45, 90, 135, 180, 225, 270, 315) {
				$r1 = [Math]::PI * $a / 180
				Add-Line $G ($cx + [Math]::Cos($r1) * $k * 0.85) ($cy + [Math]::Sin($r1) * $k * 0.85) ($cx + [Math]::Cos($r1) * $k * 1.15) ($cy + [Math]::Sin($r1) * $k * 1.15) 0.9 $White
			}
		}
		'brackets' {
			# { } 数据提取
			Add-Line $G ($cx - $k * 0.4) ($cy - $k) ($cx - $k) $cy 1.3 $White
			Add-Line $G ($cx - $k) $cy ($cx - $k * 0.4) ($cy + $k) 1.3 $White
			Add-Line $G ($cx + $k * 0.4) ($cy - $k) ($cx + $k) $cy 1.3 $White
			Add-Line $G ($cx + $k) $cy ($cx + $k * 0.4) ($cy + $k) 1.3 $White
		}
		'gear' {
			foreach ($a in 0, 60, 120) {
				$r1 = [Math]::PI * $a / 180
				Add-Line $G ($cx - [Math]::Cos($r1) * $k * 1.1) ($cy - [Math]::Sin($r1) * $k * 1.1) ($cx + [Math]::Cos($r1) * $k * 1.1) ($cy + [Math]::Sin($r1) * $k * 1.1) 1.4 $White
			}
			$gb = New-Object System.Drawing.SolidBrush($White)
			$G.FillEllipse($gb, $cx - $k * 0.8, $cy - $k * 0.8, $k * 1.6, $k * 1.6)
			$gb.Dispose()
			$hb = New-Object System.Drawing.SolidBrush($Badges['config'].Color)
			$G.FillEllipse($hb, $cx - $k * 0.32, $cy - $k * 0.32, $k * 0.64, $k * 0.64)
			$hb.Dispose()
		}
		'plus' {
			Add-Line $G ($cx - $k) $cy ($cx + $k) $cy 1.8 $White
			Add-Line $G $cx ($cy - $k) $cx ($cy + $k) 1.8 $White
		}
		'bell' {
			$hb = New-Object System.Drawing.SolidBrush($White)
			$G.FillPie($hb, $cx - $k * 0.7, $cy - $k * 1.0, $k * 1.4, $k * 1.6, 180, 180)
			$hb.Dispose()
			Fill-Poly $G @(($cx - $k * 0.7), ($cy - $k * 0.2), ($cx + $k * 0.7), ($cy - $k * 0.2), ($cx + $k), ($cy + $k * 0.5), ($cx - $k), ($cy + $k * 0.5)) $White
			Fill-Rect $G ($cx - $k * 0.3) ($cy + $k * 0.6) ($k * 0.6) ($k * 0.45) $White
		}
		'bolt' {
			Fill-Poly $G @(($cx + $k * 0.3), ($cy - $k * 1.2), ($cx - $k * 0.8), ($cy + $k * 0.15), ($cx - $k * 0.05), ($cy + $k * 0.15), ($cx - $k * 0.3), ($cy + $k * 1.2), ($cx + $k * 0.8), ($cy - $k * 0.15), ($cx + $k * 0.05), ($cy - $k * 0.15)) $White
		}
	}
}

function Draw-Icon($G, [string]$Text, $GroupColor, [string]$Badge, [string]$Bar) {
	if ($Square) {
		# 正方形底板：文字更大，底条下移
		$p = New-RoundRect 1 1 22 22 3.5
		$dy = 1.5
		if ($Text.Length -ge 4) { $tr = New-Object System.Drawing.RectangleF(2.5, 4, 19, 8.5) } else { $tr = New-Object System.Drawing.RectangleF(3, 3.5, 18, 10) }
	}
	else {
		$p = New-RoundRect 1 2 22 16 2.5
		$dy = 0
		if ($Text.Length -ge 4) { $tr = New-Object System.Drawing.RectangleF(2, 4, 20, 8) } else { $tr = New-Object System.Drawing.RectangleF(3, 5, 18, 7.5) }
	}
	$br = New-Object System.Drawing.SolidBrush($Paper); $G.FillPath($br, $p); $br.Dispose()
	$pen = New-Object System.Drawing.Pen($PlateEdge, 1); $G.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
	if ($Square -and $Text.Contains('|')) {
		# 正方形两行显示全名：用 | 分行
		$parts = $Text.Split('|')
		Add-FitText $G $parts[0] (New-Object System.Drawing.RectangleF(3, 2.6, 18, 5.4)) $Ink
		Add-FitText $G $parts[1] (New-Object System.Drawing.RectangleF(3, 9, 18, 5.4)) $Ink
		$Text = ''
	}
	if ($Text) { Add-FitText $G $Text $tr $Ink }
	if ($Bar -eq 'double') {
		Fill-Rect $G 3 (13 + $dy) 10 1 $GroupColor
		Fill-Rect $G 3 (15 + $dy) 10 2 $GroupColor
	}
	elseif ($Bar -eq 'solid') {
		Fill-Rect $G 3 (14 + $dy) 10 2 $GroupColor
	}
	elseif ($Bar -eq 'dashed') {
		foreach ($x in @(3, 7, 11)) { Fill-Rect $G $x (14 + $dy) 2 2 $GroupColor }
	}
	elseif ($Bar -eq 'triple') {
		foreach ($y in @(13, 15, 17)) { Fill-Rect $G 3 ($y + $dy) 8 1 $GroupColor }
	}
	elseif (-not $Badge) {
		# 无角标（插件管理器用）：底条通栏
		Fill-Rect $G 3 (14.5 + $dy) 18 1.8 $GroupColor
	}
	else {
		Fill-Rect $G 3 (14.5 + $dy) 10 1.8 $GroupColor
	}
	if ($Badge) { Add-Badge $G $Badge 18 18 5 }
}

function New-Icon([string]$Text, $GroupColor, [string]$Badge, [int]$Size, [string]$Bar) {
	$bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$g.Clear([System.Drawing.Color]::Transparent)
	$g.ScaleTransform($Size / 24.0, $Size / 24.0)
	Draw-Icon $g $Text $GroupColor $Badge $Bar
	$g.Dispose()
	$bmp
}

function New-Sheet([string]$Path, [string]$Dir, [string[]]$Names, $Back, $TextColor) {
	$cellW = 210; $rowH = 104
	$bmp = New-Object System.Drawing.Bitmap(($cellW + 8), ($rowH * $Names.Count + 8))
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.Clear($Back)
	$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
	$font = New-Object System.Drawing.Font('Segoe UI', 8)
	$tb = New-Object System.Drawing.SolidBrush($TextColor)
	for ($i = 0; $i -lt $Names.Count; $i++) {
		$y = 4 + $i * $rowH
		$i24 = [System.Drawing.Image]::FromFile((Join-Path $Dir "$($Names[$i])_24.png"))
		$i48 = [System.Drawing.Image]::FromFile((Join-Path $Dir "$($Names[$i])_48.png"))
		$g.DrawImage($i24, 4, $y + 36, 24, 24)
		$g.DrawImage($i48, 36, $y + 24, 48, 48)
		$g.DrawImage($i24, 92, $y, 96, 96)
		$g.DrawString($Names[$i], $font, $tb, 4, $y + 80)
		$i24.Dispose(); $i48.Dispose()
	}
	$bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
	$font.Dispose(); $tb.Dispose(); $g.Dispose(); $bmp.Dispose()
}

if ($LibraryOnly) { return }
$targets = if ($Group) { $Group } else { @($Groups.Keys) }
foreach ($key in $targets) {
	$grp = $Groups[$key]
	$outDir = Join-Path $repo $grp.Dir
	$pvDir = Join-Path $root ("preview\" + $(if ($Square) { 'square\' } else { '' }) + $key.ToLowerInvariant())
	foreach ($d in @($outDir, $pvDir)) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null } }

	foreach ($name in $grp.Icons.Keys) {
		foreach ($size in @(24, 48)) {
			$b = New-Icon $grp.Text $grp.Color $grp.Icons[$name] $size $(if ($grp.ContainsKey('Bar')) { $grp.Bar } else { '' })
			$b.Save((Join-Path $pvDir "${name}_$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
			if ($size -eq 24 -and -not $PreviewOnly) { $b.Save((Join-Path $outDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
			$b.Dispose()
		}
	}
	$names = [string[]]@($grp.Icons.Keys)
	New-Sheet (Join-Path $pvDir "_$($key.ToLowerInvariant())_light.png") $pvDir $names (C 238 238 240) $Ink
	New-Sheet (Join-Path $pvDir "_$($key.ToLowerInvariant())_dark.png") $pvDir $names (C 32 32 34) (C 220 220 224)
	Write-Host "$key -> $outDir"
}
