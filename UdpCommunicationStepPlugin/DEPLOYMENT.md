# 部署

在工作区根目录执行：

```powershell
dotnet build .\UdpCommunicationStepPlugin\UdpCommunicationStepPlugin.csproj --configuration Release -p:xTestPlatformAppDir='D:\xTestPlatform'
```

将生成 `D:\xTestPlatform\Plugins\UdpCommunication\Example.Network.UdpCommunication.StepPlugin.dll`。确认文件名以 `.StepPlugin.dll` 结尾，并保留该目录中构建复制的私有 DLL；不要只复制插件主体 DLL。

重启 xTestPlatform 后，在工具箱的“示例/网络”分类中确认“UDP 通信”步骤出现。加载日志应包含该程序集和 `Example.Network.UdpCommunication` 的注册记录。
