# NI-XNET LIN 属性 ID 诊断脚本
param([string]$Interface = "LIN1")

$src = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Nx
{
	[DllImport("nixnet.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int nxCreateSession(string db, string cluster, string list, string intf, uint mode, out uint session);
	[DllImport("nixnet.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int nxClear(uint session);
	[DllImport("nixnet.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int nxSetProperty(uint session, uint propId, uint size, ref uint value);
	[DllImport("nixnet.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int nxSetProperty(uint session, uint propId, uint size, ref byte value);
	[DllImport("nixnet.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void nxStatusToString(int status, uint size, StringBuilder sb);

	public static string Err(int s)
	{
		if (s == 0) return "OK";
		var sb = new StringBuilder(1024);
		nxStatusToString(s, 1024, sb);
		return string.Format("0x{0:X8} {1}", s, sb);
	}
}
"@
Add-Type -TypeDefinition $src

function Test-Prop($session, $name, [uint32]$propId, [uint32]$size, [uint32]$val) {
	if ($size -eq 1) {
		[byte]$b = $val
		$s = [Nx]::nxSetProperty($session, $propId, 1, [ref]$b)
	} else {
		$s = [Nx]::nxSetProperty($session, $propId, 4, [ref]$val)
	}
	Write-Host ("  {0,-40} (0x{1:X8}, size={2}) -> {3}" -f $name, $propId, $size, [Nx]::Err($s))
}

foreach ($modeInfo in @(@{Name="FrameInStream"; Mode=6}, @{Name="FrameOutStream"; Mode=9})) {
	Write-Host "=== 模式 $($modeInfo.Name) ($($modeInfo.Mode)) 接口 $Interface ==="
	[uint32]$session = 0
	$s = [Nx]::nxCreateSession(":memory:", "", "", $Interface, [uint32]$modeInfo.Mode, [ref]$session)
	Write-Host ("  nxCreateSession -> {0}" -f [Nx]::Err($s))
	if ($s -lt 0) { continue }

	Test-Prop $session "IntfBaudRate 0x00100016 u32" 0x00100016 4 19200
	Test-Prop $session "IntfLINMaster 0x00100023 u32" 0x00100023 4 1
	Test-Prop $session "IntfLINMaster 0x00100023 bool8" 0x00100023 1 1
	Test-Prop $session "IntfLINBreakLength 0x00100022 u32" 0x00100022 4 13
	Test-Prop $session "IntfLINTerm 0x00100032 u32" 0x00100032 4 1

	[Nx]::nxClear($session) | Out-Null
}
