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
	msvcr120.dll           ← VC++ 2013 运行库（zlgcan.dll 依赖）
	msvcp120.dll           ← VC++ 2013 运行库（zlgcan.dll 依赖）
	kerneldlls\
		usbcanfd.dll
		zlgcanfd.dll
		usbcan.dll
		...
		devices_property\   ← 各型号设备属性配置
```

## 关于 VC++ 运行库

`zlgcan.dll` 导入表依赖 `MSVCR120.dll` / `MSVCP120.dll`（**Visual C++ 2013** 运行库）。
Windows 不自带这套运行库，缺失时 `LoadLibrary` 会失败，而 .NET 会把这种情况
**同样报成 `DllNotFoundException`**（提示“找不到 zlgcan.dll”），极具误导性。

为避免现场还需单独安装运行库，这两个文件已随库一并放入本目录（app-local 部署）。
`ZlgApi` 以绝对路径调用 `NativeLibrary.Load`，其内部使用 `LOAD_WITH_ALTERED_SEARCH_PATH`，
会优先从 `zlgcan.dll` 所在目录解析依赖，因此本目录的副本会生效。

> 注：`kerneldlls` 中有少量旧板卡驱动（如 CANET系列）依赖 **VC++ 2008**（`msvcr90.dll`）。
> 该版本运行库是 SxS 并行程序集，无法通过 app-local 方式部署，
> 如需使用这些型号需在现场安装周立功官方驱动包。USBCANFD 系列不受此影响。

## 注意事项

- **必须使用 x64 版本**：插件输出目标为 `win-x64`，放入 x86 文件会导致 `BadImageFormatException`。
- **`kerneldlls` 子目录不能拆散**：`zlgcan.dll` 会从自身所在目录的 `kerneldlls` 下加载各板卡的内核驱动，
  只拷贝 `zlgcan.dll` 一个文件无法工作。
- **无需修改 csproj**：`CanPlugin.csproj` 使用通配符 `Native\Zlg\x64\**\*.*` 递归包含，
  新增文件后重新生成即可自动复制。
- **USB 设备驱动仍需在目标机器上安装**：本目录只包含二次开发库（用户态 DLL），
  不包含 USBCAN 设备的 Windows 驱动程序（.inf/.sys），后者仍需在现场安装。
- **版权**：ZLGCAN 库为周立功发布的文件，入库与随产品分发前请确认符合其许可条款。
