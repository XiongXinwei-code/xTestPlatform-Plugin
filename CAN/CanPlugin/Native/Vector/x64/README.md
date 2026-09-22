# Vector XL Driver Library 原生库目录

把 **vxlapi64.dll**（x64）放在本目录下，构建时会自动复制到插件输出目录的 `Native\Vector\`，
随插件一起发布，现场无需再手动拷贝 DLL。

来源：安装 Vector XL Driver Library 后，从其安装目录（通常是
`C:\Users\Public\Documents\Vector\XL Driver Library\bin\x64\`）或 `C:\Windows\System32\` 取得。

说明：

- 本目录为空时不影响构建。运行时会回退到系统默认搜索顺序，即使用现场已安装的 Vector 驱动。
- **仅放 DLL 并不能免除安装驱动**：Vector 设备还需要内核驱动（Vector Driver Setup）以及
  Vector Hardware Configuration 中的通道配置，`CAN_Open` 的 `Channel` 参数即该工具中的全局通道索引。
- 随产品分发 `vxlapi64.dll` 前，请确认符合 Vector XL Driver Library 的许可条款。
- 建议使用与现场驱动版本相近的 DLL，避免新旧版本不匹配。
