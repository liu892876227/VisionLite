# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述
VisionLite 是一个基于 WPF 的工业机器视觉系统，使用 C# .NET Framework 4.7.2 开发。系统集成了多家相机厂商的 SDK，提供图像采集、ROI 管理、通讯模块等功能。

## 开发环境和构建
- **开发工具**: Visual Studio 2017 或更高版本
- **目标框架**: .NET Framework 4.7.2
- **构建命令**:
  ```
  # 使用 MSBuild 构建解决方案
  msbuild VisionLite.sln /p:Configuration=Debug /p:Platform=x64
  msbuild VisionLite.sln /p:Configuration=Release /p:Platform=x64
  
  # 或在 Visual Studio 中直接构建
  # 支持 Debug|Any CPU, Debug|x64, Release|Any CPU, Release|x64 配置
  ```

## 核心架构

### 硬件抽象层
- **ICameraDevice**: 所有相机设备的统一接口，支持不同厂商的相机
- **HalconCameraDevice**: Halcon 相机设备实现  
- **HikvisionCameraDevice**: 海康威视相机设备实现
- 相机设备通过 `MainWindow.openCameras` 字典统一管理

### 通讯模块 (Communication/)
- **ICommunication**: 通讯接口抽象，支持多种通讯协议
- **TcpCommunication**: TCP 客户端通讯实现
- **TcpServer**: TCP 服务器端实现
- **VisionLiteProtocol**: 自定义消息协议
- **Message**: 结构化消息对象

### 主要依赖库
- **HalconDotNet**: 机器视觉算法库，用于图像处理和显示
- **MvCameraControl.Net**: 海康威视相机 SDK
- **AForge**: 视频采集和处理框架
- **Xceed.Wpf.Toolkit**: WPF 扩展控件库
- **Newtonsoft.Json**: JSON 序列化

### UI 架构
- **MainWindow**: 主窗口，管理所有相机显示和工具栏交互
- **CameraManagementWindow**: 相机管理窗口，处理设备枚举和连接
- **CommunicationWindow**: 通讯配置窗口
- **Adorner 系统**: 用于 ROI 绘制和工具栏显示
  - `RoiAdorner`: ROI 区域绘制和编辑
  - `WindowToolbarAdorner`: 窗口内嵌工具栏
  - `InfoWindowAdorner`: 信息显示层

## 开发注意事项

### 相机设备开发
- 新相机类型需实现 `ICameraDevice` 接口
- 必须提供 `HSmartWindowControlWPF` 显示控件
- 确保线程安全，图像回调在工作线程执行
- 参数设置需处理不同采集状态的限制

### 通讯模块开发
- 新通讯方式需实现 `ICommunication` 接口
- 消息协议通过 `IMessageProtocol` 进行解析
- 使用异步模式处理网络 I/O 操作
- 状态变化通过事件通知上层

### ROI 和图像处理
- ROI 操作基于 Halcon 的坐标系统
- 图像显示使用 `HSmartWindowControlWPF` 控件
- ROI 数据通过 `RoiUpdatedEventArgs` 传递

### 项目文件说明
- `VisionLite.sln`: Visual Studio 解决方案文件
- `VisionLite/VisionLite.csproj`: 项目文件，包含所有依赖和配置
- `VisionLite/packages.config`: NuGet 包配置
- `VisionLite/Properties/AssemblyInfo.cs`: 程序集信息

### 外部 SDK 依赖
- Halcon SDK 路径: `C:\Study\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\`
- 海康威视 SDK 路径: `C:\Study\MVS\Development\DotNet\win64\`
- 确保开发环境已安装对应 SDK

### 调试配置
- 主要使用 x64 平台配置进行开发和部署
- Debug 配置输出到 `bin\x64\Debug\`
- Release 配置输出到 `bin\x64\Release\`
- 可以尝试使用Bash(cd "E:\mySoftware\VisionLite\VisionLite" && "C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\amd64\MSBuild.exe" VisionLite.csproj)来编译
- 可以尝试用Bash(cd "E:\mySoftware\VisionLite\VisionLite" && "C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\amd64\MSBuild.exe" VisionLite.csproj)来编译这个项目