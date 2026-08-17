# 更新日志

本插件族的所有重要变更都会记录在此文件中。

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
