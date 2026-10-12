<#
.SYNOPSIS
	生成 NI-DAQ 插件图标候选方案预览（示波器图形 + 屏幕波形区分信号类型 + 右下角操作角标）。
.DESCRIPTION
	屏幕波形：AI 正弦、DI/DO 方波、编码器 窄脉冲串、同步 双通道、任务 正弦。
	角标：配置 齿轮、读取 ↓、写入 ↑、启动 ▶、停止 ■。
	仅输出到 tools\Icons\preview\daq\，不覆盖正式图标。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
. (Join-Path $root 'New-PluginLetterIcons.ps1') -LibraryOnly

$Body   = C 55 60 70
$Screen = C 20 30 28
$Trace  = C 60 220 120
$Trace2 = C 250 200 40
$Bezel  = C 236 238 242
$Knob   = C 150 155 165

$Ops = [ordered]@{
	nidaq_ai_config      = @{ Wave = 'sine';   Badge = 'config' }
	nidaq_ai_read        = @{ Wave = 'sine';   Badge = 'read' }
	nidaq_di_read        = @{ Wave = 'square'; Badge = 'read' }
	nidaq_do_write       = @{ Wave = 'square'; Badge = 'write' }
	nidaq_encoder_config = @{ Wave = 'pulse';  Badge = 'config' }
	nidaq_encoder_read   = @{ Wave = 'pulse';  Badge = 'read' }
	nidaq_sync_config    = @{ Wave = 'dual';   Badge = 'config' }
	nidaq_sync_read      = @{ Wave = 'dual';   Badge = 'read' }
	nidaq_task_start     = @{ Wave = 'sine';   Badge = 'start' }
	nidaq_task_stop      = @{ Wave = 'sine';   Badge = 'stop' }
}

function Fill-RR($G, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, $Color) {
	$p = New-RoundRect $x $y $w $h $r
	$br = New-Object System.Drawing.SolidBrush($Color); $G.FillPath($br, $p); $br.Dispose(); $p.Dispose()
}

function Stroke-RR($G, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, $Color, [single]$PenW) {
	$p = New-RoundRect $x $y $w $h $r
	$pen = New-Object System.Drawing.Pen($Color, $PenW); $G.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
}

function Draw-Lines($G, [single[]]$pts, $Color, [single]$PenW) {
	$arr = for ($i = 0; $i -lt $pts.Count; $i += 2) { New-Object System.Drawing.PointF($pts[$i], $pts[$i + 1]) }
	$pen = New-Object System.Drawing.Pen($Color, $PenW)
	$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
	$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
	$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
	$G.DrawLines($pen, [System.Drawing.PointF[]]$arr); $pen.Dispose()
}

# 在矩形 (x,y,w,h) 内画波形
function Draw-Wave($G, [string]$Wave, [single]$x, [single]$y, [single]$w, [single]$h, $Color, $Color2, [single]$PenW) {
	$mid = $y + $h / 2; $amp = $h * 0.32
	switch ($Wave) {
		'sine' {
			$pts = @(); for ($i = 0; $i -le 24; $i++) { $t = $i / 24; $pts += ($x + $t * $w); $pts += ($mid - [Math]::Sin($t * 2 * [Math]::PI * 1.25) * $amp) }
			Draw-Lines $G $pts $Color $PenW
		}
		'square' {
			$lo = $mid + $amp; $hi = $mid - $amp; $s = $w / 4
			Draw-Lines $G @($x, $lo, ($x + $s * 0.5), $lo, ($x + $s * 0.5), $hi, ($x + $s * 1.5), $hi, ($x + $s * 1.5), $lo, ($x + $s * 2.5), $lo, ($x + $s * 2.5), $hi, ($x + $s * 3.5), $hi, ($x + $s * 3.5), $lo, ($x + $w), $lo) $Color $PenW
		}
		'pulse' {
			$lo = $mid + $amp; $hi = $mid - $amp; $s = $w / 5
			$pts = @($x, $lo)
			for ($i = 0; $i -lt 5; $i++) { $px = $x + $s * ($i + 0.3); $pts += @($px, $lo, $px, $hi, ($px + $s * 0.35), $hi, ($px + $s * 0.35), $lo) }
			$pts += @(($x + $w), $lo)
			Draw-Lines $G $pts $Color ($PenW * 0.85)
		}
		'dual' {
			$s = $w / 4
			$h1 = $y + $h * 0.12; $l1 = $y + $h * 0.42; $h2 = $y + $h * 0.58; $l2 = $y + $h * 0.88
			Draw-Lines $G @($x, $l1, ($x + $s), $l1, ($x + $s), $h1, ($x + $s * 2.5), $h1, ($x + $s * 2.5), $l1, ($x + $w), $l1) $Color ($PenW * 0.85)
			Draw-Lines $G @($x, $l2, ($x + $s), $l2, ($x + $s), $h2, ($x + $s * 2.5), $h2, ($x + $s * 2.5), $l2, ($x + $w), $l2) $Color2 ($PenW * 0.85)
		}
	}
}

$Styles = [ordered]@{
	# A：经典台式示波器：深色机身、黑屏绿迹、右侧旋钮
	A = {
		param($G, $Wave)
		Fill-RR $G 1 3 22 15 2 $Body
		Fill-RR $G 2.5 4.5 13.5 12 1 $Screen
		Draw-Wave $G $Wave 3.5 5.5 11.5 10 $Trace $Trace2 1.2
		Dot $G 19 7 1.4 $Knob
		Dot $G 19 11.2 1.0 $Knob
	}
	# B：浅色机身描边 + 黑屏（与铭牌系列的浅底风格一致）
	B = {
		param($G, $Wave)
		Fill-RR $G 1 3 22 15 2.5 $Bezel
		Stroke-RR $G 1 3 22 15 2.5 $Body 1
		Fill-RR $G 3 5 13 11 1 $Screen
		Draw-Wave $G $Wave 4 6 11 9 $Trace $Trace2 1.2
		Dot $G 19 7.5 1.3 $Body
		Dot $G 19 11.5 0.9 $Body
	}
	# C：只画屏幕 + 网格（极简，波形最大）
	C = {
		param($G, $Wave)
		Fill-RR $G 1 2.5 22 16 2.5 $Screen
		$gp = New-Object System.Drawing.Pen((C 60 90 80), 0.5)
		foreach ($gx in 6.5, 12, 17.5) { $G.DrawLine($gp, $gx, 3.5, $gx, 17.5) }
		foreach ($gy in 6.5, 10.5, 14.5) { $G.DrawLine($gp, 2, $gy, 22, $gy) }
		$gp.Dispose()
		Draw-Wave $G $Wave 2.5 4 19 11 $Trace $Trace2 1.5
	}
	# D：浅底屏幕 + 深色波形（不用黑屏，和平台浅色界面更融合）
	D = {
		param($G, $Wave)
		Fill-RR $G 1 2.5 22 16 2.5 (C 246 246 248)
		Stroke-RR $G 1 2.5 22 16 2.5 (C 208 208 214) 1
		Fill-Rect $G 3 10.5 18 0.6 (C 210 214 222)
		Draw-Wave $G $Wave 3 4.5 18 12 (C 30 110 214) (C 235 120 20) 1.5
	}
}

function Dot($G, [single]$cx, [single]$cy, [single]$r, $Color) {
	$br = New-Object System.Drawing.SolidBrush($Color)
	$G.FillEllipse($br, $cx - $r, $cy - $r, $r * 2, $r * 2); $br.Dispose()
}

function New-StyleIcon($Draw, [string]$Wave, [string]$Badge, [int]$Size) {
	$bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$g.Clear([System.Drawing.Color]::Transparent)
	$g.ScaleTransform($Size / 24.0, $Size / 24.0)
	& $Draw $g $Wave
	Add-Badge $g $Badge 18 18 5
	$g.Dispose()
	$bmp
}

$pv = Join-Path $root 'preview\daq'
if (-not (Test-Path $pv)) { New-Item -ItemType Directory -Path $pv -Force | Out-Null }

$names = [string[]]@($Ops.Keys)
$cell = 112
foreach ($bg in @(@{ N = 'light'; B = (C 238 238 240); T = $Ink }, @{ N = 'dark'; B = (C 32 32 34); T = (C 220 220 224) })) {
	$sheet = New-Object System.Drawing.Bitmap((50 + $cell * $names.Count), (40 + 110 * $Styles.Count))
	$sg = [System.Drawing.Graphics]::FromImage($sheet)
	$sg.Clear($bg.B)
	$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
	$sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
	$font = New-Object System.Drawing.Font('Segoe UI', 8)
	$big = New-Object System.Drawing.Font('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)
	$tb = New-Object System.Drawing.SolidBrush($bg.T)
	for ($c = 0; $c -lt $names.Count; $c++) { $sg.DrawString(($names[$c] -replace 'nidaq_', ''), $font, $tb, (50 + $c * $cell), 8) }
	$row = 0
	foreach ($key in $Styles.Keys) {
		$y = 36 + $row * 110
		$sg.DrawString($key, $big, $tb, 12, ($y + 30))
		for ($c = 0; $c -lt $names.Count; $c++) {
			$x = 50 + $c * $cell
			$op = $Ops[$names[$c]]
			$i24 = New-StyleIcon $Styles[$key] $op.Wave $op.Badge 24
			$i48 = New-StyleIcon $Styles[$key] $op.Wave $op.Badge 48
			$sg.DrawImage($i24, $x, ($y + 8), 24, 24)
			$sg.DrawImage($i48, $x, ($y + 40), 48, 48)
			$sg.DrawImage($i24, ($x + 28), $y, 72, 72)
			if ($bg.N -eq 'light') {
				$i24.Save((Join-Path $pv "${key}_$($names[$c])_24.png"), [System.Drawing.Imaging.ImageFormat]::Png)
				$i48.Save((Join-Path $pv "${key}_$($names[$c])_48.png"), [System.Drawing.Imaging.ImageFormat]::Png)
			}
			$i24.Dispose(); $i48.Dispose()
		}
		$row++
	}
	$sheet.Save((Join-Path $pv "_daq_styles_$($bg.N).png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$font.Dispose(); $big.Dispose(); $tb.Dispose(); $sg.Dispose(); $sheet.Dispose()
}
Write-Host "preview -> $pv"
