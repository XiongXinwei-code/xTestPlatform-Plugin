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
- **设备驱动仍需在目标机器上安装**：本目录的二次开发库（用户态 DLL）不能替代
  USB / PCI / PCIe 板卡的 Windows 驱动程序（.inf/.sys），后者仍需在现场安装。
- **版权**：ZLGCAN 库为周立功发布的文件，入库与随产品分发前请确认符合其许可条款。

## PCI / PCIe 型号与依赖库

通道表达式示例：`"PCI5010U/0/0"`、`"PCIe9120/0/1"`、`"PCIE-CANFD-200U/0/1"`。
设备索引、通道索引从 0 开始，具体通道数量以实卡为准。型号不区分大小写，
支持 SDK 名称（如 `ZCAN_PCIE_CANFD_200U`）以及短横线 / 下划线写法。
以下设备码来自本目录的 `zlgcan.h`，板卡库映射来自 `kerneldlls/dll_cfg.ini`：

| 型号 | 设备码 | 协议 | kerneldlls 中的板卡库 |
|---|---|---|---|
| PCI5121 / PCI5110 | 1 / 7 | Classic | PCI51XXE.dll |
| PCI9810 | 2 | Classic | PCI9810.dll |
| PCI9820 | 5 | Classic | PCI9820.dll |
| PCI9840 | 14 | Classic | PCI9840B.dll |
| PCI9820I | 16 | Classic | PCI9820I.dll |
| PCIe9220 | 18 | Classic | 当前配置未映射，需支持该型号的官方 SDK |
| PCI5010U / PCI5020U（也接受 PCI-5010-U / PCI-5020-U） | 19 / 22 | Classic | pci50xx_u.dll |
| PCIe9221 | 24 | Classic | pcie9221.dll |
| PCIe9120 / PCIe9110 / PCIe9140 | 27 / 28 / 29 | Classic | pcie9120.dll / pcie9110.dll / pcie9140.dll |
| PCIE-CANFD-100U / 200U / 400U | 38 / 39 / 40 | Classic / FD | zpcfd.dll |
| PCIE-CANFD-100U-EX / 400U-EX | 60 / 61 | Classic / FD | xpcfd.dll |
| PCIE-CANFD-200U-MINI | 62 | Classic / FD | xpcfd.dll |
| PCIE-CANFD-200U-EX / 200U-M2 | 63（SDK 共用） | Classic / FD | xpcfd.dll |
| PCIE-CANFD-800U / 1200U | 82 / 83 | Classic / FD | xpcfd.dll |
| MINI-PCIE-CANFD | 84 | Classic / FD | xpcfd.dll |

**当前仓库尚未包含上述 PCI / PCIe 专用板卡 DLL。** 型号解析与初始化代码已接入，
但使用实卡前必须从官方 x64 二次开发包补齐所需板卡库及其依赖，保持目录结构，
然后重新构建。仅有 XML / 头文件 / zlgcan.dll 不代表板卡依赖完整。
缺少板卡库时，打开失败诊断会指出预期的 DLL 路径；不要用 x86 DLL 替代。

### 波特率与采样点

- 旧 PCI / PCIe Classic 型号沿用 BTR0 / BTR1 时序；PCI5010U / PCI5020U 除外。
- PCI5010U / PCI5020U 使用 `baud_rate` 标准属性，支持 1000000、800000、500000、
  250000、125000、100000、50000、20000、10000、5000 bps。
  **采样点由驱动决定，不应用 ArbitrationSamplePoint**，打开日志会明确提示，
  不报告未经确认的实际采样点。自定义时序需另行提供厂商编码规则。
- PCIe CAN FD 型号在 Classic / FD 下均通过 SDK 的 `protocol`、`canfd_exp`、
  `canfd_abit_baud_rate` 属性配置；FD 模式额外设置 `canfd_dbit_baud_rate`。
  新系列 XML 仅提供 FD 控制器模式，Classic 会话仍使用该控制器收发经典帧，
  不发送 FD 帧；不使用 USB 系列的 `canfd_standard` 或直接时序回退。
- 所有 PCIe CAN FD 型号的标准仲裁段采样点为 **80%**。
  100U / 200U / 400U 的标准仲裁波特率为 1000000、800000、500000、250000、125000 bps；
  EX / MINI / M2 / 800U / 1200U / MINI-PCIE-CANFD 额外支持 100000、50000 bps。
- FD 标准数据段波特率为 5000000、4000000、2000000、1000000 bps；新系列额外支持
  800000、500000、250000 bps。**5 Mbps 使用 75% 数据段采样点**，其他标准速率使用 **80%**。
  超出对应 XML 标准波特率 / 采样点范围时明确报错，不猜测自定义时序编码。
- 内置终端电阻仍按设备能力处理；明确启用但驱动属性不支持时会报错，应改用外接电阻。

本次仅进行代码和 SDK 配置验证，未进行 PCI / PCIe 实卡收发验证。
