$root = "D:\xTestPlatform-PluginDev"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$results = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Recurse -Path $root -Filter "*.xaml" | ForEach-Object {
	$content = [IO.File]::ReadAllText($_.FullName)
	$file = $_.FullName.Replace($root + '\', '')
	# TextBox Text="{Binding XXX...}"
	$rx = [regex]'(?s)<TextBox[^>]*Text="\{Binding\s+(\w+)[^}]*\}"'
	$ms = $rx.Matches($content)
	foreach ($m in $ms) {
		$field = $m.Groups[1].Value
		$results.Add("TEXTBOX  | $file | $field")
	}
}
$results | Sort-Object | ForEach-Object { Write-Host $_ }
