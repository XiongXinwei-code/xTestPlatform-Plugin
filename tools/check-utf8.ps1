$root = Split-Path $PSScriptRoot -Parent
$strict = New-Object System.Text.UTF8Encoding($false, $true)
$exts = '.cs','.xaml','.csproj','.md','.ps1','.yml'
$files = Get-ChildItem $root -Recurse -File | Where-Object { $exts -contains $_.Extension -and $_.FullName -notmatch '\\(bin|obj|\.git|\.vs)\\' }
$bad = @(); $bom = @()
foreach ($f in $files) {
	$b = [IO.File]::ReadAllBytes($f.FullName)
	if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { $bom += $f.FullName.Substring($root.Length + 1) }
	try { [void]$strict.GetString($b) } catch { $bad += $f.FullName.Substring($root.Length + 1) }
}
"Total: $($files.Count)"
"Invalid UTF-8: $($bad.Count)"; $bad | ForEach-Object { "  $_" }
"BOM: $($bom.Count)"; $bom | ForEach-Object { "  $_" }
