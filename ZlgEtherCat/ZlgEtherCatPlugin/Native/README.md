# 原生封装与型号边界

`IZlgNativeApi` 提供可注入的同步 API，`ZlgNativeApi` 仅封装随包声明的厂商入口。插件运行时负责唯一工作线程、原生调用实际退出、通道占用、维护门禁和步骤期限；软超时不表示设备命令已撤回。

DLL 固定使用插件程序集绝对目录下的 `Native/pci_zecm.dll`，依赖仅从该 DLL 目录和 Windows System32 查找。供应文件来源、字节哈希与编码规则见 [vendor/SOURCE.md](vendor/SOURCE.md)。离线测试只检查 PE 导出、ABI、边界和替身行为，不加载厂商 DLL。

## HPCIe-2E

2026-11-05 查阅的 [ZLG 官方 EtherCAT 主站卡页面](https://www.zlg.com/ethercatnet/ethercatnet/product/id/357.html)列出 HPCIe-2E 为一个主站、两个以太网接口，支持 CoE、FoE、线缆冗余和分布式时钟（DC）。同页把该系列控制周期写为最低 125 μs，并介绍板卡 TTS 机制；这不能直接证明 Windows 插件每 250 μs 或 500 μs 生成、提交新目标的能力。目标总线周期仍须由实际 ENI、板卡/固件及从站支持，并通过现场周期与 DC 诊断验证。

该官方型号表同时将 PDO watchdog 标为不支持；当前 v1.6.13 头文件与 DLL 包含新看门狗接口，二者缺少可核实的型号、固件兼容说明。插件因此拒绝名称以 `HPCIe-2E` 开头的板卡的配置、查询、启停和喂狗步骤，不把通用导出存在当作该型号支持。此限制只能在取得明确兼容依据并补充测试后调整；不能用软件停止续发等同于硬件看门狗响应。

随包板载 I/O 示例仅确认 PCIe-1E16。插件只对 `PCIe-1E16` 和 `PCIe-1E16-SM` 开放板载 DI/DO、脉冲、PWM 与锁存，HPCIe-2E 不开放这些接口。EtherCAT 从站 I/O 与此板载 I/O 限制无关。

## 队列与诊断

`EcatPIInputQueuePop` 与 `EcatPIOutputQueuePush` 的零超时语义没有明确非阻塞依据，因此封装拒绝零值，后台输入使用有限等待。LOCAL 输入队列只由通道采集器消费；其他读取使用其快照。FAST/SHADOW 异步访问不提供相同的新队列样本证据。

DC 诊断保留原始 `MasterDiagnostics.State`，并分别解释 `Updated=0x1`、内部同步 `0x200`、外部同步 `0x400`、传播延迟初始化 `0x800`，依据 `pci_diagnostic.h`。诊断位成立不构成端到端周期、机械动作或功能安全验证。
