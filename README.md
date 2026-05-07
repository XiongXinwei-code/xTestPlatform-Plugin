# xTestPlatform 插件开发 — SDK 引用配置

---

## 1. 创建 nuget.config

在插件项目根目录创建 `nuget.config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <!-- xTestPlatform 插件 SDK 本地源 -->
    <add key="xTestPlatform-SDK" value="替换成实际存放本地包的文件夹路径" />
  </packageSources>
</configuration>
```

**说明：**

- 包源地址：本地文件夹，例如： `D:\xTestPlatform-PluginDev\LocalPackages`
- 将 `.nupkg` 文件放入该目录即可被识别

---

## 2. 配置插件项目 .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- ✅ 文件名必须以 .StepPlugin.dll 结尾 -->
    <AssemblyName>MyCompany.MyPlugin.StepPlugin</AssemblyName>
    <!-- ✅ 确保依赖复制到输出目录 -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <!-- ✅ 输出到主程序 Plugins 目录 -->
    <OutputPath>主程序路径\Plugins\MyPlugin</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.0" />
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

或直接浏览文件夹 `D:\xTestPlatform-PluginDev\LocalPackages` 查看 `.nupkg` 文件。
```
