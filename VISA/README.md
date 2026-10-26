# VISA 插件

## 本轮修订 v1.0.25

本次生命周期机制修订：原调用方、owner 及期限取消通过异步桥处理；原生调用与取消回调均真实退出后才释放 gate 和句柄。
软件复现、非作者复审与三轮勘误见源码仓库 docs/reviews/2026-10-26-plugin-lifecycle-mechanism.md。完整平台窗口及真实设备仍未验收。

五个前台步骤在缺会话查询后复查原始取消和执行代次，保持Aborted/Error区分及真实I/O与会话释放所有权。

设置字段、枚举与步骤身份保持兼容；非空输出须按手册显式声明完整变量路径。软件验证与三轮勘误记录在源码仓库 docs/reviews/2026-10-25-plugin-lifecycle-followup.md。真实平台调试窗口和设备尚需验收。

## 功能概述

通过 VISA 标准与仪器通信（SCPI 指令），支持仪器会话管理、读写、查询、批量下发以及 *OPC? 同步等待。

## 支持的硬件/协议

- GPIB / USB / LAN(LXI) / 串口接口的仪器（需 NI-VISA 或兼容运行库）

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| VISA_BatchWrite | 批量发送多条 SCPI 命令到 VISA 仪器，按顺序逐条发送，每条命令发送后可指定延时等待。 |
| VISA_Close | 关闭指定的 VISA 仪器会话并释放资源。 |
| VISA_Open | 打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源，打开后通过 ConnectionName 标识此连接。 |
| VISA_Query | 向 VISA 仪器发送查询命令并立即读取响应（Write+Read 一体操作），结果以字符串形式存入指定变量。适用于查询类命令如 *IDN?、:MEAS:VOLT:DC? 等。 |
| VISA_Read | 从 VISA 仪器读取响应数据（用于之前 Write 后延迟读取的场景），结果以字符串形式存入指定变量。 |
| VISA_WaitOPC | 等待仪器当前操作完成（发送 *OPC? 并等待返回 '1'），用于校准、测量等耗时操作的同步。 |
| VISA_Write | 向 VISA 仪器发送 SCPI 命令（只写不读），不等待响应。适用于设置类命令如 *RST、:CONF:VOLT:DC 等。 |

## 使用前提

需安装 NI-VISA（或 Keysight IO Libraries 等兼容实现），并确认仪器资源名。
