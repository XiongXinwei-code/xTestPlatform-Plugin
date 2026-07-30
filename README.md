# xTestPlatform 插件开发 — SDK 引用配置

---

## 1. 创建 nuget.config

将本仓库中的 `xTestPlatform.StepEditor.SDK.1.0.14.nupkg` 保留在一个固定目录（下文以 `<SDK包目录>` 表示），然后在插件项目根目录创建 `nuget.config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <!-- xTestPlatform 插件 SDK 本地源 -->
    <add key="xTestPlatform-SDK" value="D:\project\xTestPlatform_Plugin" />
  </packageSources>
</configuration>
```

**说明：**

- 上例路径是当前 SDK 包所在目录；若将包移动到其他位置，请将 `value` 改为实际的 `<SDK包目录>`。
- 该目录中必须包含 `xTestPlatform.StepEditor.SDK.1.0.14.nupkg`。

---

## 2. 配置插件项目 .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows7.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- ✅ 文件名必须以 .StepPlugin.dll 结尾 -->
    <AssemblyName>MyCompany.MyPlugin.StepPlugin</AssemblyName>
    <!-- ✅ 确保依赖复制到输出目录 -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <!-- ✅ 输出到 xTestPlatform 运行程序的 Plugins 目录；按实际安装目录调整 -->
    <OutputPath>D:\xTestPlatform\Plugins\MyPlugin\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.14" />
  </ItemGroup>
</Project>
```

---

## 3. 还原验证

```bash
dotnet restore
```

编译通过即表示 SDK 引用成功 ✅

---

## 4. 查看可用包版本

查看本地源中的包：

```bash
dotnet package search xTestPlatform.StepEditor.SDK --source "xTestPlatform-SDK"
```

或直接浏览 `<SDK包目录>`（本仓库默认是 `D:\project\xTestPlatform_Plugin`）查看 `.nupkg` 文件。
