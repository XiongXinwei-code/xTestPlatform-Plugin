<#
.SYNOPSIS
	生成 CAN 插件字母图标的多套风格预览（字母 "CAN" + 右下角操作角标）。
.DESCRIPTION
	所有图形在 24x24 设计坐标中绘制，按目标尺寸等比缩放。
	输出：tools\Icons\preview\can\{style}_{op}_{size}.png 以及 _can_light.png / _can_dark.png 预览拼图。
#>
[CmdletBinding()]
param([string]$OutputDir)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $root 'preview\can' }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

# 与主平台一致的基础色
$Ink       = C 34 34 38
$Paper     = C 246 246 248
$PlateEdge = C 208 208 214
$White     = C 255 255 255
# CAN 组主色
$CanColor  = C 0 120 150

# 角标颜色（沿用主平台 New/Open/Close/Save 配色）
$BadgeGreen  = C 22 150 78
$BadgeBlue   = C 32 110 214
$BadgeRed    = C 214 45 45
$BadgeOrange = C 235 120 20

$Ops = [ordered]@{
	open        = @{ Color = $BadgeBlue;   Label = 'Open' }
	close       = @{ Color = $BadgeRed;    Label = 'Close' }
	read        = @{ Color = $BadgeGreen;  Label = 'Read' }
	write       = @{ Color = $BadgeOrange; Label = 'Write' }
	cyclicstart = @{ Color = $BadgeGreen;  Label = 'CyclicStart' }
	cyclicstop  = @{ Color = $BadgeRed;    Label = 'CyclicStop' }
}

$fontCandidates = @('Bahnschrift', 'Arial Narrow', 'Segoe UI')
$installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name }
$FontName = $fontCandidates | Where-Object { $installed -contains $_ } | Select-Object -First 1
Write-Host "字体: $FontName"

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

# 把文字路径缩放到指定矩形内（保持比例，左对齐 / 居中可选）
function Add-FitText($G, [string]$Text, [System.Drawing.RectangleF]$Box, $Color, [switch]$Center) {
	$ff = New-Object System.Drawing.FontFamily($FontName)
	$style = [System.Drawing.FontStyle]::Bold
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$path.AddString($Text, $ff, [int]$style, 100, (New-Object System.Drawing.PointF(0, 0)), [System.Drawing.StringFormat]::GenericTypographic)
	$b = $path.GetBounds()
	$s = [Math]::Min($Box.Width / $b.Width, $Box.Height / $b.Height)
	$w = $b.Width * $s; $h = $b.Height * $s
	$ox = if ($Center) { $Box.X + ($Box.Width - $w) / 2 } else { $Box.X }
	$oy = $Box.Y + ($Box.Height - $h) / 2
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

# 角标：圆形底 + 白色符号，中心 (cx,cy)，半径 r
function Add-Badge($G, [string]$Op, [single]$cx, [single]$cy, [single]$r, $Ring) {
	$col = $Ops[$Op].Color
	# 外圈描边，用于与主体分隔
	$rb = New-Object System.Drawing.SolidBrush($Ring)
	$G.FillEllipse($rb, $cx - $r - 1, $cy - $r - 1, ($r + 1) * 2, ($r + 1) * 2)
	$rb.Dispose()
	$br = New-Object System.Drawing.SolidBrush($col)
	$G.FillEllipse($br, $cx - $r, $cy - $r, $r * 2, $r * 2)
	$br.Dispose()
	$k = $r * 0.5; $w = 1.5
	switch ($Op) {
		'open' {
			# 右向箭头
			Add-Line $G ($cx - $k) $cy ($cx + $k) $cy $w $White
			Add-Line $G ($cx + $k) $cy ($cx + 0.1) ($cy - $k) $w $White
			Add-Line $G ($cx + $k) $cy ($cx + 0.1) ($cy + $k) $w $White
		}
		'close' {
			Add-Line $G ($cx - $k * 0.8) ($cy - $k * 0.8) ($cx + $k * 0.8) ($cy + $k * 0.8) $w $White
			Add-Line $G ($cx + $k * 0.8) ($cy - $k * 0.8) ($cx - $k * 0.8) ($cy + $k * 0.8) $w $White
		}
		'read' {
			# 向下箭头：数据读入
			Add-Line $G $cx ($cy - $k) $cx ($cy + $k) $w $White
			Add-Line $G $cx ($cy + $k) ($cx - $k) ($cy + 0.1) $w $White
			Add-Line $G $cx ($cy + $k) ($cx + $k) ($cy + 0.1) $w $White
		}
		'write' {
			# 向上箭头：数据写出
			Add-Line $G $cx ($cy + $k) $cx ($cy - $k) $w $White
			Add-Line $G $cx ($cy - $k) ($cx - $k) ($cy - 0.1) $w $White
			Add-Line $G $cx ($cy - $k) ($cx + $k) ($cy - 0.1) $w $White
		}
		'cyclicstart' {
			Fill-Poly $G @(($cx - $k * 0.7), ($cy - $k), ($cx + $k), $cy, ($cx - $k * 0.7), ($cy + $k)) $White
		}
		'cyclicstop' {
			$sb = New-Object System.Drawing.SolidBrush($White)
			$G.FillRectangle($sb, $cx - $k * 0.8, $cy - $k * 0.8, $k * 1.6, $k * 1.6)
			$sb.Dispose()
		}
	}
}

# ── 风格定义（均在 24x24 设计坐标中） ──────────────────────────────

# A：主色实心圆角底板 + 白字，角标压在右下角
function Draw-StyleA($G, $Op) {
	$p = New-RoundRect 1 3 22 14 2.5
	$br = New-Object System.Drawing.SolidBrush($CanColor); $G.FillPath($br, $p); $br.Dispose(); $p.Dispose()
	Add-FitText $G 'CAN' (New-Object System.Drawing.RectangleF(3, 6, 18, 8)) $White -Center
	Add-Badge $G $Op 18 18 5 $White
}

# B：主色描边圆角框 + 主色字
function Draw-StyleB($G, $Op) {
	$p = New-RoundRect 1.5 3.5 21 13 2.5
	$pen = New-Object System.Drawing.Pen($CanColor, 1.6); $G.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
	Add-FitText $G 'CAN' (New-Object System.Drawing.RectangleF(4, 6.5, 16, 7)) $CanColor -Center
	Add-Badge $G $Op 18 18 5 $White
}

# C：主平台单字铭牌风格：浅底灰边铭牌 + 墨色字 + 主色底条
function Draw-StyleC($G, $Op) {
	$p = New-RoundRect 1 2 22 16 2.5
	$br = New-Object System.Drawing.SolidBrush($Paper); $G.FillPath($br, $p); $br.Dispose()
	$pen = New-Object System.Drawing.Pen($PlateEdge, 1); $G.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
	Add-FitText $G 'CAN' (New-Object System.Drawing.RectangleF(3, 5, 18, 7.5)) $Ink -Center
	$bb = New-Object System.Drawing.SolidBrush($CanColor); $G.FillRectangle($bb, 3, 14.5, 10, 1.8); $bb.Dispose()
	Add-Badge $G $Op 18 18 5 $White
}

# D：无底板，纯主色粗体字 + 下方总线线条（CAN_H / CAN_L 双线）
function Draw-StyleD($G, $Op) {
	Add-FitText $G 'CAN' (New-Object System.Drawing.RectangleF(1, 2, 22, 10)) $CanColor -Center
	Add-Line $G 2 15 12 15 1.4 $CanColor
	Add-Line $G 2 18.5 12 18.5 1.4 (C 120 120 128)
	Add-Badge $G $Op 18 18 5 $White
}

$Styles = [ordered]@{
	A = ${function:Draw-StyleA}
	B = ${function:Draw-StyleB}
	C = ${function:Draw-StyleC}
	D = ${function:Draw-StyleD}
}

function New-Icon([string]$Style, [string]$Op, [int]$Size) {
	$bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$g.Clear([System.Drawing.Color]::Transparent)
	$g.ScaleTransform($Size / 24.0, $Size / 24.0)
	& $Styles[$Style] $g $Op
	$g.Dispose()
	$bmp
}

$Sizes = @(16, 24, 32, 48)
foreach ($s in $Styles.Keys) {
	foreach ($op in $Ops.Keys) {
		foreach ($size in $Sizes) {
			$b = New-Icon $s $op $size
			$b.Save((Join-Path $OutputDir "${s}_${op}_$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
			$b.Dispose()
		}
	}
}

# 预览拼图：每行一种风格；每个操作一列，列内依次为 24 / 48 / 24 放大 4 倍（像素查看）
function New-Sheet([string]$Path, $Back, $TextColor) {
	$cellW = 24 + 8 + 48 + 8 + 96 + 16
	$cellH = 96 + 24
	$left = 40; $top = 28
	$W = $left + $cellW * $Ops.Count
	$H = $top + $cellH * $Styles.Count
	$bmp = New-Object System.Drawing.Bitmap($W, $H)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.Clear($Back)
	$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
	$font = New-Object System.Drawing.Font('Segoe UI', 10)
	$tb = New-Object System.Drawing.SolidBrush($TextColor)
	$ci = 0
	foreach ($op in $Ops.Keys) {
		$g.DrawString($Ops[$op].Label, $font, $tb, $left + $ci * $cellW, 4)
		$ci++
	}
	$ri = 0
	foreach ($s in $Styles.Keys) {
		$y = $top + $ri * $cellH
		$g.DrawString($s, (New-Object System.Drawing.Font('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)), $tb, 8, $y + 36)
		$ci = 0
		foreach ($op in $Ops.Keys) {
			$x = $left + $ci * $cellW
			$i24 = [System.Drawing.Image]::FromFile((Join-Path $OutputDir "${s}_${op}_24.png"))
			$i48 = [System.Drawing.Image]::FromFile((Join-Path $OutputDir "${s}_${op}_48.png"))
			$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
			$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
			$g.DrawImage($i24, $x, $y + 36, 24, 24)
			$g.DrawImage($i48, $x + 32, $y + 24, 48, 48)
			$g.DrawImage($i24, $x + 88, $y, 96, 96)
			$i24.Dispose(); $i48.Dispose()
			$ci++
		}
		$ri++
	}
	$bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
	$font.Dispose(); $tb.Dispose(); $g.Dispose(); $bmp.Dispose()
}

New-Sheet (Join-Path $OutputDir '_can_light.png') (C 238 238 240) $Ink
New-Sheet (Join-Path $OutputDir '_can_dark.png') (C 32 32 34) (C 220 220 224)
Write-Host "完成：$OutputDir"
