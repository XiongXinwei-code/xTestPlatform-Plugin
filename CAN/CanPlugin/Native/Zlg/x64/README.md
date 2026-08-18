# ZLGCAN 原生驱动库（x64）

本目录用于存放周立功（ZLG）ZLGCAN 二次开发库的 **64 位** 文件。
插件在生成时会把本目录下的所有文件（含子目录）复制到插件输出目录的 `Native\Zlg\` 下，
运行时由 `ZlgApi` 通过 `SetDllDirectory` + `NativeLibrary.SetDllImportResolver` 从该目录加载。

## 需要放置的文件

请从 ZLG 官方二次开发包（<https://www.zlg.cn/can/down/down/id/22.html>）中取出 x64 版本，
**保持原有目录结构**拷贝到本目录，典型内容如下：

```
Native\Zlg\x64\
	zlgcan.dll
	(同级的其它依赖 dll，如 kerneldlls 之外的运行库)
	kerneldlls\
		usbcanfd.dll
		zlgcanfd.dll
		usbcan.dll
		...
```

## 注意事项

- **必须使用 x64 版本**：插件输出目标为 `win-x64`，放入 x86 文件会导致 `BadImageFormatException`。
- **`kerneldlls` 子目录不能拆散**：`zlgcan.dll` 会从自身所在目录的 `kerneldlls` 下加载各板卡的内核驱动，
  只拷贝 `zlgcan.dll` 一个文件无法工作。
- **无需修改 csproj**：`CanPlugin.csproj` 使用通配符 `Native\Zlg\x64\**\*.*` 递归包含，
  新增文件后重新生成即可自动复制。
- **USB 设备驱动仍需在目标机器上安装**：本目录只包含二次开发库（用户态 DLL），
  不包含 USBCAN 设备的 Windows 驱动程序（.inf/.sys），后者仍需在现场安装。
- **版权**：ZLGCAN 库为周立功发布的文件，入库与随产品分发前请确认符合其许可条款。
