# 部署

在工作区根目录执行：

```powershell
dotnet build .\UdpCommunicationStepPlugin.UI\UdpCommunicationStepPlugin.UI.csproj --configuration Release -p:xTestPlatformAppDir='D:\xTestPlatform'
```

将生成同一目录下的 `UdpCommunication.StepPlugin.dll`（执行插件）和 `UdpCommunication.StepPlugin.UI.dll`（编辑器插件）。复制整个 `UdpCommunication` 文件夹，保留两份 `.deps.json` 和所有构建复制的 DLL；不要只复制执行插件 DLL。

重启 xTestPlatform 后，在工具箱的“示例/网络”分类中确认“UDP 通信”步骤出现。加载日志应包含该程序集和 `Example.Network.UdpCommunication` 的注册记录。
