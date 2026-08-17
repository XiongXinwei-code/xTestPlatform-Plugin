# HTTP 步骤插件

面向工厂 MES / ERP 系统集成的 HTTP 通信插件族，同时支持 REST(JSON) 与 SOAP(XML)，认证方式全部做成可选参数。

## 项目结构

| 项目 | 输出程序集 | 说明 |
|------|-----------|------|
| `HttpPlugin` | `Http.StepPlugin.dll` | 执行层，包含步骤定义、Setting 模型与执行器 |
| `HttpPlugin.UI` | `Http.StepPlugin.UI.dll` | 编辑器层，包含 WPF 编辑视图、ViewModel 与校验 |

输出目录：`Plugins\Http\`

## 步骤清单

| 步骤 | StepTypeId | 说明 |
|------|-----------|------|
| `Http_ClientCreate` | `IO.HttpClientCreate` | 创建命名客户端，配置基地址、超时、认证与 TLS |
| `Http_Request` | `IO.HttpRequest` | REST 请求，支持 GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS |
| `Http_SoapRequest` | `IO.HttpSoapRequest` | SOAP 1.1 / 1.2 请求，自动识别 SOAP Fault |
| `Http_JsonExtract` | `IO.HttpJsonExtract` | 按点号路径批量提取 JSON 字段到变量 |
| `Http_XmlExtract` | `IO.HttpXmlExtract` | 按 XPath 批量提取 XML 字段到变量 |
| `Http_ClientClose` | `IO.HttpClientClose` | 释放命名客户端 |

## 认证方式

`Http_ClientCreate` 的 `AuthMode` 支持四种模式，未使用的认证字段会被忽略：

| AuthMode | 需要填写的字段 | 生成的请求头 |
|----------|--------------|-------------|
| `None` | — | 无 |
| `Basic` | `UserName`、`Password` | `Authorization: Basic {base64}` |
| `BearerToken` | `Token` | `Authorization: Bearer {token}` |
| `ClientCertificate` | `ClientCertPath`、`ClientCertPassword` | 双向 TLS，无额外请求头 |

`IgnoreServerCertificateErrors` 可跳过服务端证书校验，仅建议在内网自签证书环境使用，编辑器会给出警告。

## 典型用法

### REST 上报测试结果到 MES

```
1. Http_ClientCreate   ClientName="Mes"  BaseUrl="http://mes.factory.local:8080/api"  AuthMode=BearerToken
2. Http_Request        Method=Post  Path="/result"  ContentType=Json  Body=拼接的 JSON  ResponseVariable=HttpResponse
3. Http_JsonExtract    SourceJson=HttpResponse  Path="data.code" => Locals.MesCode
4. Http_ClientClose    ClientName="Mes"
```

### SOAP 调用旧版 MES 服务

```
1. Http_ClientCreate   ClientName="Mes"  BaseUrl="http://mes.factory.local"  AuthMode=Basic
2. Http_SoapRequest    Path="/service.asmx"  SoapVersion=Soap11  SoapAction="http://tempuri.org/ReportResult"  Envelope=完整 XML
3. Http_XmlExtract     SourceXml=SoapResponse  Path="//ReportResultResult/code" => Locals.MesCode
4. Http_ClientClose    ClientName="Mes"
```

## 资源生命周期

客户端以 `Engine` 生命周期注册到 `context.Resources`，键为 `Http.Client.{ClientName}`。引擎停止时会自动释放，`Http_ClientClose` 用于提前主动释放。

## 提取路径语法

- **JSON**：点号属性访问 + 方括号数组索引，例如 `data.items[0].sn`；路径留空表示取整个文档。不支持 JSONPath 的过滤器与递归下降语法。
- **XML**：标准 XPath。`IgnoreNamespaces=true`（默认）会先剥离命名空间，XPath 无需写前缀。

## 校验规则

编辑器提供实时校验，错误码前缀 `HTTP_`：

- `HTTP_001~010`：客户端创建参数与认证字段
- `HTTP_020~026`：REST 请求参数与输出变量
- `HTTP_040~047`：SOAP 请求参数
- `HTTP_060~062`：JSON 提取
- `HTTP_080~083`：XML 提取（含 XPath 语法编译校验）
- `HTTP_100`：客户端关闭
- `HTTP_LC01`：生命周期警告，请求步骤之前未找到同名 `Http_ClientCreate`
