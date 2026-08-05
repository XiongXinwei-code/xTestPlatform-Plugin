$exprFields = @("CanId","Channel","ChannelName","Command","ConnectionName","CounterChannel","CsvFilePath","Data","Did","DtcGroup","EndpointUrl","ExpectedValue","FilePath","FilterId","IpAddress","KeyExpression","NodeId","OptionRecord","OutputDirectory","Password","PortName","Quantity","RefAtPeakVariable","ReferenceChannel","RequestData","ResourceString","ResultVariable","RoutineId","RxId","StartAddress","TaskName","TxId","UserName","Value","Values","WriteData","WriteValue")
$root = "D:\xTestPlatform-PluginDev"

Get-ChildItem -Recurse -Path $root -Filter "*.xaml" | Where-Object { $_.FullName -notmatch "\\obj\\" } | ForEach-Object {
	$content = [IO.File]::ReadAllText($_.FullName)
	$file = $_.FullName.Substring($root.Length + 1)
	$rx = [regex]'<TextBox[^>]*\bText="\{Binding\s+(\w+)[^}]*\}"'
	$rx.Matches($content) | ForEach-Object {
		$field = $_.Groups[1].Value
		if ($exprFields -contains $field) {
			Write-Host "MISSING | $file | $field"
		}
	}
}
Write-Host "Done."
