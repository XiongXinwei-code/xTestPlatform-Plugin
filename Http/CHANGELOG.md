# 更新日志

## v1.0.23 - 2026-10-25
- null表达式不再回退到源码；默认/单次请求头拒绝换行并检查添加结果，保留重复默认头追加。创建/关闭用原配置总预算覆盖登记排队及旧实例关闭。日志仅保留摘要、不含正文/Action/提取值；SOAP1.2非法Action也使用脱敏错误。实际请求页面补齐已有日志/容错配置。
- 软件复现、修复、独立复审与最终勘误见 docs/reviews/2026-10-25-plugin-lifecycle-reaudit.md。完整宿主调试退出/重入、第三方原生依赖动态加载及实际设备仍未现场验证；回退需成套替换核心、UI和依赖，不强清未知I/O，不将ACK视为物理停止。

## v1.0.22 - 2026-10-25
- 6步增加执行代次/精确owner围栏；持久客户端不绑定创建步骤token。Close后台等待实际请求与异步取消回调，迟到构建只释放自身。修复XML部分写、未知枚举、负创建期限、Header表达式列和集合Clear订阅。
- 软件验证与三轮勘误见 docs/reviews/2026-10-25-full-plugin-lifecycle-three-rounds.md；真实设备与完整平台调试窗口未验。回退成套替换核心/UI/依赖，不强清真实占用，不把取消/ACK当作动作撤回或物理停止。

## v1.0.21 - 2026-10-24
- 重新集成共享生命周期修复：精确旧 owner 已释放时保留历史错误诊断但不阻断首次重开；仍未释放的 owner 继续隔离。
- 本次未新增 HTTP 协议功能；随 core/UI 和全插件离线回归成对交付。
- 验证：定向 RED/GREEN、跨模块只读复审及最终全量软件验收见 docs/reviews/2026-10-24-plugin-remediation-acceptance.md；真实硬件和完整平台未验证。回退应整目录成对替换 core/UI，保留原包；不得回退为强制清空未释放资源，软件回退不撤销设备输出。

## v1.0.20 - 2026-10-24
- ClientClose 登记等待总期限可配置，ClientCreate 登记使用原配置期限；统一 Dispatcher 防抖/卸载 Flush，补齐实际页面、设置解析异常与旧设置默认回读。
- 完整预检 JSON 路径、一次解析/提取后提交；客户端创建和替换可回滚并释放临时资源；默认认证限制同源且不自动重定向，日志脱敏；增加可配置连接复用，修复 UI 保存。
- 核心/UI 同步按补丁版本递增；回退须整目录成对替换，不能回退到未确认 I/O 退出也继续访问的行为。新增软件回归及页面绑定检查；真实驱动、设备时序与动作安全尚未实测。

本插件族的所有重要变更都会记录在此文件中。

## 未发布
- 迁移：六个共享错误入口使用 SDK 1.2.8 工厂，HTTP/SOAP 超时保留原异常及脱敏主机、请求方法、超时值，保留实际值/条件/耗时；最终错误由宿主统一记录。
- 调整：非成功状态和 SOAP Fault 不再把响应正文放入错误日志；响应仍写入用户已配置的变量，原容错开关保持不变。需先部署契约匹配的宿主。
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；现有容错开关不变，依赖原通过/失败状态的序列需相应调整。

## [1.0.0] - 2025-01-01

### 新增

- `Http_ClientCreate`：创建命名 HTTP 客户端，支持基地址、超时、默认请求头配置
  - 认证方式支持 `None` / `Basic` / `BearerToken` / `ClientCertificate` 四种可选模式
  - 支持忽略服务端证书校验，用于内网自签证书环境
  - 客户端以 Engine 生命周期注册到 `Http.Client.{ClientName}`
- `Http_Request`：REST(JSON) 请求步骤
  - 支持 GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS
  - 请求体类型支持 `None` / `Json` / `Xml` / `Text` / `FormUrlEncoded`
  - 可将响应体、状态码、请求耗时分别写入变量
- `Http_SoapRequest`：SOAP(XML) 请求步骤
  - 支持 SOAP 1.1（`SOAPAction` 独立请求头 + `text/xml`）
  - 支持 SOAP 1.2（`action` 作为 `application/soap+xml` 的参数）
  - 自动解析响应中的 SOAP Fault 并提取 `faultstring` 或 `Reason/Text`
- `Http_JsonExtract`：按点号路径批量提取 JSON 字段写入变量，支持数组索引与默认值回填
- `Http_XmlExtract`：按 XPath 批量提取 XML 字段写入变量，可选剥离命名空间
- `Http_ClientClose`：释放命名客户端，可配置客户端不存在时是否忽略
- 全部六个步骤的 WPF 编辑器，含表达式编辑、请求头/提取映射表格编辑与 200ms 防抖保存
- `HttpLifecycleValidator`：校验请求步骤之前是否存在同名 `Http_ClientCreate`
- `HttpEditorValidationHelper`：共用的变量类型校验、请求头校验与提取映射校验
