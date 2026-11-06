# ZLG SDK 来源

本目录包含用户提供的 PCIe 资料包 1.6.13 中的 Windows x64 DLL 和原始头文件。文件按字节复制，未重新编码、修改二进制或加载执行。

源目录：`D:\BaiduNetdiskDownload\【网盘资料】PCIe转EtherCAT通讯卡在线资料包 V1.06\软件资料\v1.6.13\PCIe_1.6.13\Windows\examples\sdk`。

| 文件 | SHA-256 |
| --- | --- |
| `x64/pci_zecm.dll` | `C294DC4B9FD3CE1D31357B7DF950633DE361A0FBB7B2621F2DD1836304C415DF` |
| `include/pci_zecm.h` | `BA6AF5FB8D07B66FD6CB39E549EDA3DD70BABF4175BA7780D8F89365544EFED6` |
| `include/pci_diagnostic.h` | `E8BCD39A9D4570C6B11B833ADD6AECC9A9EF11A9779D7E209FE66C1424038234` |
| `include/pci_errno.h` | `35F328980C8703E94A05180C5C8F0A8DE752566E2719CFBF74EF11EADFB9338B` |

静态检查：x64 PE，151 个导出，三份头文件的 147 个去重函数声明均有对应导出。额外未声明导出不使用；废弃的冗余设置、旧映像范围及旧看门狗命令接口不使用。

头文件保留厂商版权。复制不构成新增的再分发许可声明，部署或再分发遵循厂商授权。

Windows 路径和设备标识的 `char*` 绑定明确采用系统 ANSI 代码页，拒绝无法往返表示的字符和内嵌空字符；SDK 未提供足以承诺 UTF-8 文件路径支持的契约。仓库自身 C# 文本采用 UTF-8 无 BOM。

DLL 固定从插件程序集旁的 `Native/pci_zecm.dll` 加载，不搜索用户指定 DLL 路径。SDK 和驱动调用并非线程安全保障；上层必须协调同通道生命周期、I/O、共享缓存及真实调用退出。此层仅封装原生 API，不等于硬件能力或现场停轴安全已验证。
