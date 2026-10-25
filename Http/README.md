# HTTP 步骤插件

## 本轮修订 v1.0.24

修复旧关闭误删新客户端、逐项及最终发布检查；REST/SOAP输出在发送前预检，JSON/XML先校验全部目标，诊断保持敏感数据保护。

设置字段、枚举与步骤身份保持兼容；非空输出须按手册显式声明完整变量路径。软件验证与三轮勘误记录在源码仓库 docs/reviews/2026-10-25-plugin-lifecycle-followup.md。真实平台调试窗口和设备尚需验收。

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

客户端以默认的 `Execution` 生命周期注册到 `context.Resources`，键为 `Http.Client.{ClientName}`。本次执行（主线程与所有子线程）结束时会自动释放，下次执行需重新创建；`Http_ClientClose` 用于提前主动释放。

- 中断或调试上下文回收会先冻结该客户端的新请求，再异步收尾；不会在平台的同步资源回收线程中等待网络请求或执行任意取消回调。
- 已提交的请求及取消回调必须真实退出后才释放原客户端。`ClientClose.TimeoutMs` 可配置，超时只表示本次等待结束，原客户端仍保留并继续收尾，不假报已关闭；显式替换同名客户端也先等待捕获的旧实例。
- 原请求即使晚返回，也不能向已取消步骤、已回收资源表或新客户端代次发布结果。独立新执行可创建新客户端；这不等于旧 HTTP 请求已被服务器撤销，插件不会自动重发可能已执行的请求。
- 创建期限包含客户端构建和替换等待；既有 `TimeoutMs=0` 的无限等待语义保持兼容，负值拒绝。请求、关闭和上层序列表达式并非无条件共用一个期限，详见对应步骤参数说明。

已完成受控 HTTP handler、真实 SDK 资源表回收和 WPF 组件测试；完整平台调试窗口重入、服务端事务撤回、TLS/代理/证书及真实网络故障仍需现场验证。回退时整目录替换同一版本的核心、UI及依赖，不混用旧 DLL。

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
