$utf8 = New-Object System.Text.UTF8Encoding($true)
$root = "D:\xTestPlatform-PluginDev\LXI\LxiPlugin"

# LxiOpenPlugin.cs
$desc = [char]0x901A + [char]0x8FC7 + " TCP " + [char]0x8FDE + [char]0x63A5 + [char]0x5230 + " LXI/SCPI " + [char]0x4EEA + [char]0x5668 + [char]0x3002 + "Setting " + [char]0x5B57 + [char]0x6BB5 + [char]0xFF1A + "IpAddress(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + "," + [char]0x4EEA + [char]0x5668 + "IP), Port(int," + [char]0x7AEF + [char]0x53E3 + [char]0x9ED8 + [char]0x8BA4 + "5025), ConnectTimeoutMs(int," + [char]0x8FDE + [char]0x63A5 + [char]0x8D85 + [char]0x65F6 + "ms), Terminator(string," + [char]0x7EC8 + [char]0x6B62 + [char]0x7B26 + ")" + [char]0x3002
$file = "$root\LxiOpenPlugin.cs"
$c = [System.IO.File]::ReadAllText($file)
$c = $c.Replace("Connect to LXI/SCPI instrument via TCP. Setting: IpAddress(string,expression,IP), Port(int,default 5025), ConnectTimeoutMs(int,timeout ms), Terminator(string,line terminator).", $desc)
[System.IO.File]::WriteAllText($file, $c, $utf8)

# LxiClosePlugin.cs
$desc = [char]0x65AD + [char]0x5F00 + [char]0x4E0E + " LXI/SCPI " + [char]0x4EEA + [char]0x5668 + [char]0x7684 + " TCP " + [char]0x8FDE + [char]0x63A5 + [char]0x3002 + "Setting " + [char]0x5B57 + [char]0x6BB5 + [char]0xFF1A + "IpAddress(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + "," + [char]0x4EEA + [char]0x5668 + "IP)" + [char]0x3002
$file = "$root\LxiClosePlugin.cs"
$c = [System.IO.File]::ReadAllText($file)
$c = $c.Replace("Disconnect from LXI/SCPI instrument. Setting: IpAddress(string,expression,IP).", $desc)
[System.IO.File]::WriteAllText($file, $c, $utf8)

# LxiWritePlugin.cs
$desc = [char]0x5411 + [char]0x5DF2 + [char]0x8FDE + [char]0x63A5 + [char]0x7684 + " LXI/SCPI " + [char]0x4EEA + [char]0x5668 + [char]0x53D1 + [char]0x9001 + [char]0x547D + [char]0x4EE4 + [char]0xFF08 + [char]0x4E0D + [char]0x7B49 + [char]0x5F85 + [char]0x54CD + [char]0x5E94 + [char]0xFF09 + [char]0x3002 + "Setting " + [char]0x5B57 + [char]0x6BB5 + [char]0xFF1A + "IpAddress(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + "," + [char]0x4EEA + [char]0x5668 + "IP), Command(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + ",SCPI" + [char]0x547D + [char]0x4EE4 + "), Terminator(string," + [char]0x7EC8 + [char]0x6B62 + [char]0x7B26 + ")" + [char]0x3002
$file = "$root\LxiWritePlugin.cs"
$c = [System.IO.File]::ReadAllText($file)
$c = $c.Replace("Send SCPI command to LXI instrument (no response read). Setting: IpAddress(string,expression,IP), Command(string,expression,SCPI command), Terminator(string,line terminator).", $desc)
[System.IO.File]::WriteAllText($file, $c, $utf8)

# LxiReadPlugin.cs
$desc = [char]0x4ECE + [char]0x5DF2 + [char]0x8FDE + [char]0x63A5 + [char]0x7684 + " LXI/SCPI " + [char]0x4EEA + [char]0x5668 + [char]0x8BFB + [char]0x53D6 + [char]0x54CD + [char]0x5E94 + [char]0x6570 + [char]0x636E + [char]0x5E76 + [char]0x5B58 + [char]0x5165 + [char]0x53D8 + [char]0x91CF + [char]0x3002 + "Setting " + [char]0x5B57 + [char]0x6BB5 + [char]0xFF1A + "IpAddress(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + "," + [char]0x4EEA + [char]0x5668 + "IP), ReadTimeoutMs(int," + [char]0x8BFB + [char]0x53D6 + [char]0x8D85 + [char]0x65F6 + "ms), Terminator(string," + [char]0x7EC8 + [char]0x6B62 + [char]0x7B26 + "), ResultVariable(string," + [char]0x7ED3 + [char]0x679C + [char]0x53D8 + [char]0x91CF + [char]0x8DEF + [char]0x5F84 + ")" + [char]0x3002
$file = "$root\LxiReadPlugin.cs"
$c = [System.IO.File]::ReadAllText($file)
$c = $c.Replace("Read response from LXI instrument and store in variable. Setting: IpAddress(string,expression,IP), ReadTimeoutMs(int,read timeout ms), Terminator(string,line terminator), ResultVariable(string,target variable path).", $desc)
[System.IO.File]::WriteAllText($file, $c, $utf8)

# LxiQueryPlugin.cs
$desc = [char]0x5411 + [char]0x5DF2 + [char]0x8FDE + [char]0x63A5 + [char]0x7684 + " LXI/SCPI " + [char]0x4EEA + [char]0x5668 + [char]0x53D1 + [char]0x9001 + [char]0x67E5 + [char]0x8BE2 + [char]0x547D + [char]0x4EE4 + [char]0x5E76 + [char]0x8BFB + [char]0x53D6 + [char]0x54CD + [char]0x5E94 + [char]0xFF08 + "Write+Read" + [char]0xFF09 + [char]0x3002 + "Setting " + [char]0x5B57 + [char]0x6BB5 + [char]0xFF1A + "IpAddress(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + "," + [char]0x4EEA + [char]0x5668 + "IP), Command(string," + [char]0x8868 + [char]0x8FBE + [char]0x5F0F + ",SCPI" + [char]0x67E5 + [char]0x8BE2 + [char]0x547D + [char]0x4EE4 + "), Terminator(string," + [char]0x7EC8 + [char]0x6B62 + [char]0x7B26 + "), ReadTimeoutMs(int," + [char]0x8BFB + [char]0x53D6 + [char]0x8D85 + [char]0x65F6 + "ms), ResultVariable(string," + [char]0x7ED3 + [char]0x679C + [char]0x53D8 + [char]0x91CF + [char]0x8DEF + [char]0x5F84 + ")" + [char]0x3002
$file = "$root\LxiQueryPlugin.cs"
$c = [System.IO.File]::ReadAllText($file)
$c = $c.Replace("Send SCPI query and read response (Write+Read). Setting: IpAddress(string,expression,IP), Command(string,expression,SCPI query), Terminator(string,line terminator), ReadTimeoutMs(int,read timeout ms), ResultVariable(string,target variable path).", $desc)
[System.IO.File]::WriteAllText($file, $c, $utf8)

Write-Host "Done - all descriptions changed to Chinese!"
