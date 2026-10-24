# 插件能力补齐与树形分层评估

## 等级、范围与边界

- L3 只读设计评估：10 个插件、118 个现有步骤，关注新增能力及跨插件分类一致性；AO 涉及真实模拟输出，因此方案必须包含停止/中断语义。
- 用户希望先确认现有问题，再考虑新增步骤与分层。上一轮生命周期报告仍有未修问题，不得把该前置条件写成已满足。本轮先对源码指纹和代表性缺陷复核，再提供带准入条件的建议，不实施功能或改名。
- 不修改生产代码、StepTypeId、DisplayName、设置序列化、UI、版本、实际序列和交付包；不操作设备、不提交/推送。

## 阶段与验收

1. 核对现有健康基线：比较当前源码与上一轮指纹，检查代表性缺陷是否仍成立；复用未变更且有效的 545 项回归，不把通过当作无缺陷证明。记录“未修/已变更/未验证”。
2. 读取所有模块步骤描述、模型、关键执行器与树元数据：建立 118 项当前 StepTypeId/Category/DisplayName 清单，查明树层级的证据边界；不得凭空增加 CategoryPath 或假定斜杠被宿主解析。
3. 形成能力候选：每模块区分已有能力、可以复用的组合与有价值的新增步骤，注明优先级、输入输出、生命周期和硬件限制；重点细化 NI-DAQ AO，复用已有 TaskStart/TaskStop，不机械重复。
4. 形成分类方案：重点 CiA402 的会话/状态/运动/周期目标/反馈诊断等有依据分组；提供全部现有步骤的映射，保持 StepTypeId 和持久化兼容。层级不是增加步骤数量的理由。
5. 独立只读复核与交付：交叉检查重复能力、硬编码设备参数、危险停止承诺、元数据/宿主支持假设。核对覆盖计数、来源、UTF-8、最终源码无改动；保存报告与未决项。

## 能力与职责

- ECC planner：当前 Agent 做一次规划；architect：检查插件/宿主分类职责、输出任务归属、兼容与回退；product-capability：把功能想法转成可审查的能力合同。技能缓存内模板缺失，已定位并完整读取同版本 vendor/ECC/docs/examples/product-capability-template.md，按该模板组织唯一需求交付。
- C# / 异常传播方法：复核已有代码风险与新增输出方案的取消/收尾约束；没有修改行为，不启动修复 TDD 流程。
- verification-loop：静态清单、来源/哈希、定向离线探针及文档检查；没有生产更改，不重复无变化的全部构建/回归。未运行硬件或完整宿主应明确。
- 使用现有原生子 Agent 做 NI-DAQ 能力、其余协议能力、分类契约的只读分析；主 Agent 负责 CiA402 与最终去重。因涉及多模块契约和输出/停止风险，整合后安排独立只读复核；不得借此并行修改产品。

## 固定约束与验收结论格式

- 修复现有高风险生命周期缺陷优先于新增有副作用的步骤；不宣称全部插件已无问题。
- 标准步骤契约和配置四处闭环；保留现有 StepTypeId，不为显示树迁移序列协议。
- 输出命令确认不是测得实际电压或物理安全状态；释放任务不默认等于输出零，设备能力与用户明确的停止策略必须区分。
- AO 单点、缓冲输出、计数器/同步等能力按硬件资格分阶段；不假设所有 NI 设备支持 AO、任意采样率、电流输出或统一的复位行为。
- 交付 `docs/requirements/2026-10-24-plugin-capability-taxonomy.md`：健康准入、全插件候选、全步骤分类映射、重点能力合同、开放问题、验证证据。不是功能实现或硬件验收单。

## 当前状态

- 只读评估五阶段已完成，主交付：`docs/requirements/2026-10-24-plugin-capability-taxonomy.md`。现有缺陷仍未修，新能力/分类未实施；没有进行产品或交付目录写入。
- 健康准入：当前及最终源码指纹与上一轮 1080 文件完全相同；2 项离线探针重新复现迟到提交与 faulted ServiceStop 历史错误，不能解释为缺陷验证通过。其余问题只依源码未变保留“未修”，没有逐项新动态复现。
- 元数据：全部 10 模块 118 步骤；候选 54 更名、64 不变，优先 CiA40218+CANopen14+Ethernet6=38，其他 16 可选。保留 ID/Category，无大小写路径、父叶冲突，最大 3 子层。
- 能力方案：NI-DAQ 单点 AO 最小两步骤、显式复用 TaskStart/Stop；波形另阶段。CiA402 优先分层、状态查询，可信等待前补源证据；其余协议只选真缺口，不要求所有插件加步骤。

## 能力检查、独立复核与闭环

| 能力 | 实际结果 |
| --- | --- |
| planner / architect | 完成阶段规划、分类契约/兼容/回退、输出 owner/真实退出/处置边界；没有引入新框架、共享 API 或产品实现 |
| product-capability | 完成唯一主合同及 NI-DAQ/通信附录；区分当前事实、候选/储备、硬件开放条件，未把所有候选列为必做 |
| C# / silent-failure-hunter 方法 | 结合调用方与资源源码审查候选的预算、取消、迟到 I/O、SDO/TPDO 路由、TaskStop 和反馈证据；不把 ACK 视为物理状态 |
| 原生独立只读复核 | 复核者重新读取 DAQ owner/Start/Stop/资源与 CiA402 控制/反馈源码。无阻止“待实施方案”交付的问题；R-TAX-01/P2 指出关闭发起与完成措辞混淆，主方案和 READINESS 已修正并经复核关闭。产品 LCA-01 仍未修 |
| verification-loop | 最终源码指纹、全部元数据/兼容、UTF-8/静态规范、文档来源/哈希及差异检查；结果见下面实际命令。未以文档检查代替产品/现场测试 |

独立复核回执：`artifacts/plugin-capability-audit/independent-review.md`；复核对应主方案 SHA-256=`5ff30acd960c25bc97ae0c0226aa5767529a5bf1ba1fbcb26473d1b68f5fd256`。本轮唯一审查问题是证据文字误读，已修正并复审；没有新增产品缺陷修复声明。

## 实际验证与未执行项

| 命令/操作 | 结果与边界 |
| --- | --- |
| `node artifacts/plugin-lifecycle-audit/Inventory.mjs capability-current.json`、`capability-final.json` | 均 10 模块/118 步/1080 源文件，聚合 SHA256 均为 `0b79cce224b3182dca1fe5cbb00cba2fe30ec0963b1d37fe18683067eff268f7`，本轮无产品改动 |
| `dotnet artifacts/plugin-lifecycle-audit/audit-shared-net/bin/Debug/net8.0/Probe.dll` | 退出码0，复现调用超时、关闭已发起但未完成时仍迟到提交；不是释放句柄后提交。0 表示探针复现成功，不是缺陷已修 |
| `dotnet artifacts/plugin-version-audit/BehaviorProbe/bin/Release/net8.0-windows/BehaviorProbe.dll artifacts/plugin-capability-audit/can-readiness-receipt.json` | 退出码0，故障监测服务停止保留历史错误；整连接实际可释放并重用物理键。未扩大成所有 CAN_Close 失败 |
| `node artifacts/plugin-capability-audit/taxonomy/inspect.mjs` / `report.mjs` | 已提取源元数据并与上轮118项名称/ID核对；候选路径无冲突，54/38计数一致；未改 DisplayName |
| `node artifacts/plugin-capability-audit/nidaq/check-source.mjs artifacts/plugin-capability-audit/nidaq/source-receipt.json` | 子审查者实际核对98个NI-DAQ文件无改动 |
| `node artifacts/plugin-capability-audit/verify.mjs` | 通过118项映射/兼容、源指纹、主交付本地链接与严格UTF-8；同时核对主文档哈希与独立复核对应。最终机器回执 `artifacts/plugin-capability-audit/verification.json` |
| `pwsh -NoLogo -NoProfile -NonInteractive -File tools/Verify-PluginSources.ps1` | 通过1172个文本文件的UTF-8/编辑器文件/执行状态静态检查，118个独立编辑器描述；不是语义或现场通过 |
| `git diff --check` | 通过；保留所有既有未提交产品修改，未进行提交/推送 |
| 产品重新构建/全量回归/覆盖率 | 未执行：只读评估、生产代码未变。545/545仅引用上一轮回执，没有冒称本轮重跑。临时行内 `node -e` 诊断曾因cmd引号失败，已换文件式检查成功，未用于通过判定 |
| 新 AO/协议步骤测试、UI与硬件 | 未执行：功能未实施、没有相应硬件资格和宿主运行验证；树排序/搜索/收藏、AO首值/处置、物理输出和停止均未验收 |

## 自评与最终收口

按 ECC agent-self-evaluation 模板对交付物自评（非质量或现场认证）：

| 轴 | 分数 | 依据与改进 |
| --- | --- | --- |
| Accuracy | 4/5 | 指纹、118项映射、2探针和独立复核支持事实；手册树规则未获宿主运行验证，后续须实测 |
| Completeness | 4/5 | 10模块均覆盖、重点AO/CiA402合同齐全；不能满足“所有插件已无问题”前置，既有12项仍开放，未假装完成 |
| Clarity | 4/5 | 主文档有健康准入、10模块表、CiA树和AO分阶段；详细协议合同放附录，硬件条款仍需实施时逐项细化 |
| Actionability | 4/5 | 明确先修生命周期、再分类、再最小AO及验收；实际型号/输出结束策略缺失，物理实施仍需先定案 |
| Conciseness | 4/5 | 全118项逐项映射放附录，最终答复只列首选项；附录保留必要安全边界，不在答复重复长合同 |

平均4.0/5，无≤2的关键交付缺口。最高优先改进为保持证据表述准确（R-TAX-01已修并复审）；其次实施前取得设备/输出策略和宿主UI证据。自检：用户应能直接选择下一实施范围，同时清楚本轮没有修复/新增功能。交付待实施方案，不把自评分数当作插件可靠性证明。
