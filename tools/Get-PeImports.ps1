# Parse PE import table to list DLL dependencies (no dumpbin required).
param(
	[string]$Path = 'D:\xTestPlatform-PluginDev\CAN\CanPlugin\Native\Zlg\x64\zlgcan.dll'
)

function Get-PeImports([string]$file) {
	$b = [IO.File]::ReadAllBytes($file)
	$peOff = [BitConverter]::ToInt32($b, 0x3C)
	$magic = [BitConverter]::ToUInt16($b, $peOff + 24)          # 0x20B = PE32+
	$numSections = [BitConverter]::ToUInt16($b, $peOff + 6)
	$optSize = [BitConverter]::ToUInt16($b, $peOff + 20)
	$dataDirOff = if ($magic -eq 0x20B) { $peOff + 24 + 112 } else { $peOff + 24 + 96 }
	$importRva = [BitConverter]::ToUInt32($b, $dataDirOff + 8) # entry[1] = import
	if ($importRva -eq 0) { return @() }

	# section headers
	$secOff = $peOff + 24 + $optSize
	$sections = @()
	for ($i = 0; $i -lt $numSections; $i++) {
		$o = $secOff + $i * 40
		$sections += [pscustomobject]@{
			VA   = [BitConverter]::ToUInt32($b, $o + 12)
			VSz  = [BitConverter]::ToUInt32($b, $o + 8)
			Raw  = [BitConverter]::ToUInt32($b, $o + 20)
		}
	}
	function RvaToOff($rva) {
		foreach ($s in $sections) {
			if ($rva -ge $s.VA -and $rva -lt ($s.VA + [Math]::Max($s.VSz, 1))) {
				return $s.Raw + ($rva - $s.VA)
			}
		}
		return 0
	}

	$names = @()
	$off = RvaToOff $importRva
	while ($true) {
		$nameRva = [BitConverter]::ToUInt32($b, $off + 12)
		if ($nameRva -eq 0) { break }
		$no = RvaToOff $nameRva
		$sb = New-Object Text.StringBuilder
		while ($b[$no] -ne 0) { [void]$sb.Append([char]$b[$no]); $no++ }
		$names += $sb.ToString()
		$off += 20
	}
	return $names
}

Write-Host "=== Imports of: $Path"
$imports = Get-PeImports $Path
$sysDir = Join-Path $env:SystemRoot 'System32'
$localDir = Split-Path $Path -Parent

foreach ($n in $imports) {
	$inSys = Test-Path (Join-Path $sysDir $n)
	$inLocal = Test-Path (Join-Path $localDir $n)
	$mark = if ($inSys -or $inLocal) { 'OK  ' } else { 'MISS' }
	$where = if ($inLocal) { 'local' } elseif ($inSys) { 'system32' } else { 'NOT FOUND' }
	Write-Host ("  [{0}] {1,-28} {2}" -f $mark, $n, $where)
}
