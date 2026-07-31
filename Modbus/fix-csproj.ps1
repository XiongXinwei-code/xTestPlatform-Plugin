# Fix: create csproj and files that failed due to XML here-string issues
$modbusRoot = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Create UI directories
New-Item -Path "$modbusRoot\ModbusPlugin.UI\ViewModels" -ItemType Directory -Force | Out-Null
New-Item -Path "$modbusRoot\ModbusPlugin.UI\Views" -ItemType Directory -Force | Out-Null
New-Item -Path "$modbusRoot\ModbusPlugin.UI\Resources\Icons" -ItemType Directory -Force | Out-Null

# csproj - use WriteAllText to avoid PowerShell XML parsing
$csproj = '<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
	<TargetFramework>net8.0-windows</TargetFramework>
	<Nullable>enable</Nullable>
	<UseWPF>true</UseWPF>
	<ImplicitUsings>enable</ImplicitUsings>
	<AssemblyName>Modbus.StepPlugin.UI</AssemblyName>
	<RootNamespace>Modbus.UI</RootNamespace>
	<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
	<OutputPath>..\..\..\xTestPlatform\xTestPlatform\bin\$(Configuration)\$(TargetFramework)\win-x64\Plugins\Modbus\</OutputPath>
	<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
	<Resource Include="Resources\Icons\modbus.png">
	  <CopyToOutputDirectory>Never</CopyToOutputDirectory>
	</Resource>
  </ItemGroup>

  <ItemGroup>
	<ProjectReference Include="..\ModbusPlugin\ModbusPlugin.csproj" />
	<PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.14" />
	<PackageReference Include="MessagePack" Version="3.1.7" />
	<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
	<PackageReference Include="Syncfusion.Themes.Windows11Light.WPF" Version="32.1.25" />
	<PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="32.1.25" />
	<PackageReference Include="Syncfusion.Tools.WPF" Version="32.1.25" />
  </ItemGroup>

</Project>'
[IO.File]::WriteAllText("$modbusRoot\ModbusPlugin.UI\ModbusPlugin.UI.csproj", $csproj, $utf8)

# Copy icon
if (Test-Path "$modbusRoot\..\CAN\CanPlugin.UI\Resources\Icons\can.png") {
	Copy-Item "$modbusRoot\..\CAN\CanPlugin.UI\Resources\Icons\can.png" "$modbusRoot\ModbusPlugin.UI\Resources\Icons\modbus.png" -Force
}

Write-Host "UI csproj created: $(Test-Path "$modbusRoot\ModbusPlugin.UI\ModbusPlugin.UI.csproj")" -ForegroundColor Green
