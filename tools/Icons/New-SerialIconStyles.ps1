<#
.SYNOPSIS
	生成串口插件图标的多套候选方案预览（串口图形 + 右下角操作角标）。
.DESCRIPTION
	复用 New-PluginLetterIcons.ps1 中的角标与绘图函数，仅输出到 tools\Icons\preview\serial\，不覆盖正式图标。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
. (Join-Path $root 'New-PluginLetterIcons.ps1') -LibraryOnly

$Navy  = C 40 70 120
$Steel = C 120 130 145
$Pin   = C 250 250 252

$Ops = [ordered]@{
	serialport_open  = 'open'
	serialport_close = 'close'
	serialport_read  = 'read'
	serialport_write = 'write'
	serialport_query = 'raw'
}

function New-Trap([single]$x, [single]$y, [single]$wTop, [single]$wBot, [single]$h, [single]$r) {
	# DB9 外形：上宽下窄的梯形，四角圆滑
	$d = ($wTop - $wBot) / 2
	$p = New-Object System.Drawing.Drawing2D.GraphicsPath
	$p.AddPolygon([System.Drawing.PointF[]]@(
		(New-Object System.Drawing.PointF($x, $y)),
		(New-Object System.Drawing.PointF(($x + $wTop), $y)),
		(New-Object System.Drawing.PointF(($x + $wTop - $d), ($y + $h))),
		(New-Object System.Drawing.PointF(($x + $d), ($y + $h)))))
	$p
}

function Fill-Path($G, $Path, $Color) {
	$br = New-Object System.Drawing.SolidBrush($Color); $G.FillPath($br, $Path); $br.Dispose()
}

function Draw-Path($G, $Path, $Color, [single]$W) {
	$pen = New-Object System.Drawing.Pen($Color, $W)
	$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
	$G.DrawPath($pen, $Path); $pen.Dispose()
}

function Dot($G, [single]$cx, [single]$cy, [single]$r, $Color) {
	$br = New-Object System.Drawing.SolidBrush($Color)
	$G.FillEllipse($br, $cx - $r, $cy - $r, $r * 2, $r * 2); $br.Dispose()
}

function Draw-Pins($G, [single]$x0, [single]$y0, [single]$step, [single]$r, $Color) {
	for ($i = 0; $i -lt 5; $i++) { Dot $G ($x0 + $i * $step) $y0 $r $Color }
	for ($i = 0; $i -lt 4; $i++) { Dot $G ($x0 + $step / 2 + $i * $step) ($y0 + $step * 0.95) $r $Color }
}

$Styles = [ordered]@{
	# A：实心 DB9 正面，白色针脚
	A = {
		param($G)
		# 内缩梯形 + 圆角粗描边，得到四角圆滑的外形
		$p = New-Trap 3 5.5 16 12.2 6.5 0
		Fill-Path $G $p $Navy
		$pen = New-Object System.Drawing.Pen($Navy, 3)
		$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
		$G.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
		Draw-Pins $G 4.6 7.2 3.2 0.95 $Pin
	}
	# B：线框 DB9 正面，深色针脚（较轻）
	B = {
		param($G)
		$p = New-Trap 2 4.5 18 14 9 0
		Fill-Path $G $p (C 236 240 246)
		Draw-Path $G $p $Navy 1.4; $p.Dispose()
		Draw-Pins $G 5 7.6 3 0.9 $Navy
	}
	# C：带安装耳的 DB9 正面（更像实物接口）
	C = {
		param($G)
		Fill-Rect $G 0.5 3.5 21 11 $Steel
		Dot $G 2.3 9 0.9 $Pin
		Dot $G 19.7 9 0.9 $Pin
		$p = New-Trap 4 5 14 10.5 7.5 0
		Fill-Path $G $p $Navy; $p.Dispose()
		Draw-Pins $G 6.2 7.3 2.4 0.75 $Pin
	}
	# D：串口插头侧视 + 线缆（强调"线"的含义）
	D = {
		param($G)
		$pen = New-Object System.Drawing.Pen($Navy, 2.2)
		$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
		$G.DrawBezier($pen, 1.5, 17, 4, 17, 5, 11, 7.5, 11); $pen.Dispose()
		Fill-Rect $G 7 7 7 8 $Navy
		Fill-Rect $G 14 8.5 4 5 $Steel
		Fill-Rect $G 18 9.3 2.5 0.9 $Steel
		Fill-Rect $G 18 11.8 2.5 0.9 $Steel
		Fill-Rect $G 8.5 9 4 0.8 $Pin
		Fill-Rect $G 8.5 11 4 0.8 $Pin
		Fill-Rect $G 8.5 13 4 0.8 $Pin
	}
}

function New-StyleIcon($Draw, [string]$Badge, [int]$Size) {
	$bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$g.Clear([System.Drawing.Color]::Transparent)
	$g.ScaleTransform($Size / 24.0, $Size / 24.0)
	& $Draw $g
	Add-Badge $g $Badge 18 18 5
	$g.Dispose()
	$bmp
}

$pv = Join-Path $root 'preview\serial'
if (-not (Test-Path $pv)) { New-Item -ItemType Directory -Path $pv -Force | Out-Null }

$names = [string[]]@($Ops.Keys)
$cell = 120
foreach ($bg in @(@{ N = 'light'; B = (C 238 238 240); T = $Ink }, @{ N = 'dark'; B = (C 32 32 34); T = (C 220 220 224) })) {
	$sheet = New-Object System.Drawing.Bitmap((60 + $cell * $names.Count), (30 + 110 * $Styles.Count))
	$sg = [System.Drawing.Graphics]::FromImage($sheet)
	$sg.Clear($bg.B)
	$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
	$sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
	$font = New-Object System.Drawing.Font('Segoe UI', 9)
	$tb = New-Object System.Drawing.SolidBrush($bg.T)
	for ($c = 0; $c -lt $names.Count; $c++) { $sg.DrawString(($names[$c] -replace 'serialport_', ''), $font, $tb, (60 + $c * $cell), 6) }
	$row = 0
	foreach ($key in $Styles.Keys) {
		$y = 30 + $row * 110
		$sg.DrawString($key, (New-Object System.Drawing.Font('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)), $tb, 14, ($y + 30))
		for ($c = 0; $c -lt $names.Count; $c++) {
			$x = 60 + $c * $cell
			$i24 = New-StyleIcon $Styles[$key] $Ops[$names[$c]] 24
			$i48 = New-StyleIcon $Styles[$key] $Ops[$names[$c]] 48
			$sg.DrawImage($i24, $x, ($y + 12), 24, 24)
			$sg.DrawImage($i48, $x, ($y + 44), 48, 48)
			$sg.DrawImage($i24, ($x + 30), $y, 72, 72)
			if ($bg.N -eq 'light') {
				$i24.Save((Join-Path $pv "${key}_$($names[$c])_24.png"), [System.Drawing.Imaging.ImageFormat]::Png)
				$i48.Save((Join-Path $pv "${key}_$($names[$c])_48.png"), [System.Drawing.Imaging.ImageFormat]::Png)
			}
			$i24.Dispose(); $i48.Dispose()
		}
		$row++
	}
	$sheet.Save((Join-Path $pv "_serial_styles_$($bg.N).png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$font.Dispose(); $tb.Dispose(); $sg.Dispose(); $sheet.Dispose()
}
Write-Host "预览 -> $pv"
