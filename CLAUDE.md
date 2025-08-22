# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

VisionLite 是一个基于 WPF 的机器视觉应用程序，使用 C# 和 .NET Framework 4.7.2 开发。该项目主要用于相机设备管理、图像采集处理和 ROI 区域选择等机器视觉任务。

## 构建命令

### Visual Studio 构建
- **构建解决方案**: 在 Visual Studio 中打开 `VisionLite.sln`，然后使用 Build → Build Solution (Ctrl+Shift+B)
- **清理和重建**: Build → Rebuild Solution
- **命令行构建**: 
  ```cmd
  msbuild VisionLite.sln /p:Configuration=Debug /p:Platform=x64
  msbuild VisionLite.sln /p:Configuration=Release /p:Platform=x64
  ```

### 调试运行
- **Visual Studio**: 按 F5 启动调试，或 Ctrl+F5 启动不调试
- **命令行**: 构建后运行 `VisionLite\bin\x64\Debug\VisionLite.exe`

## 核心架构

### 相机设备抽象层
- **接口**: `ICameraDevice` - 定义所有相机设备的统一接口
- **实现类**: 
  - `HalconCameraDevice` - 基于 HALCON 库的相机实现
  - `HikvisionCameraDevice` - 海康威视相机的具体实现
- **设备管理**: `MainWindow.openCameras` 字典管理所有打开的相机设备

### 通信子系统
- **通信抽象**: `ICommunication` 接口定义通信协议
- **TCP 实现**: `TcpCommunication` 提供 TCP 网络通信
- **消息协议**: `VisionLiteProtocol` 实现自定义消息协议
- **消息结构**: `Message` 类定义标准消息格式

### 用户界面组件
- **主窗口**: `MainWindow` - 应用程序的主控制中心
- **相机管理**: `CameraManagementWindow` - 相机设备的配置和管理界面
- **通信窗口**: `CommunicationWindow` - 网络通信的管理界面
- **ROI 工具**: `RoiAdorner` - 图像区域选择和绘制工具

### WPF 界面增强
- **装饰器**: 
  - `InfoWindowAdorner` - 信息显示覆盖层
  - `WindowToolbarAdorner` - 工具栏装饰器
- **状态管理**: `WindowRoiState` - ROI 状态跟踪

## 重要依赖项

### 外部 SDK 依赖
- **HALCON**: 机器视觉库，需要本地安装 HALCON 24.11
  - 路径: `..\..\..\Study\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`
- **海康威视 SDK**: 相机控制库
  - 路径: `..\..\..\Study\MVS\Development\DotNet\win64\MvCameraControl.Net.dll`

### NuGet 包依赖
- `AForge` (2.2.5) - 图像处理库
- `AForge.Video` (2.2.5) - 视频处理
- `Extended.Wpf.Toolkit` (4.7.25104.5739) - WPF 扩展控件
- `Newtonsoft.Json` (13.0.3) - JSON 序列化
- `System.Drawing.Common` (9.0.7) - 图形处理

## 开发注意事项

### 平台配置
- 项目支持 x64 和 AnyCPU 两种平台配置
- HALCON 和相机 SDK 依赖于 x64 架构
- 建议使用 x64 配置进行开发和部署

### 相机设备集成
- 新的相机品牌需要实现 `ICameraDevice` 接口
- 参考 `HalconCameraDevice` 和 `HikvisionCameraDevice` 的实现模式
- 设备 ID 必须唯一，通常使用相机序列号

### ROI 功能扩展
- ROI 相关逻辑集中在 `RoiAdorner` 类中
- 支持多种 ROI 类型：矩形、圆形、自由轮廓、涂抹式
- ROI 状态通过 `WindowRoiState` 和 `RoiUpdatedEventArgs` 管理

### 通信协议扩展
- 实现 `ICommunication` 接口添加新的通信方式
- 实现 `IMessageProtocol` 接口定义自定义消息格式
- 使用 `MessageBuilder` 构建标准化消息

## 项目文件结构

```
VisionLite/
├── MainWindow.xaml(.cs)           # 主窗口和核心业务逻辑
├── Communication/                 # 通信子系统
│   ├── ICommunication.cs          # 通信接口定义
│   ├── TcpCommunication.cs        # TCP 通信实现
│   ├── Message.cs                 # 消息数据结构
│   └── VisionLiteProtocol.cs      # 自定义协议实现
├── CameraManagementWindow.xaml(.cs) # 相机管理界面
├── ICameraDevice.cs               # 相机设备接口
├── HalconCameraDevice.cs          # HALCON 相机实现
├── HikvisionCameraDevice.cs       # 海康相机实现
├── RoiAdorner.cs                  # ROI 绘制和交互
└── Properties/                    # 项目属性和资源
```

## 版本信息

当前版本基于最近的提交信息，项目持续迭代中，主要功能包括：
- 多窗口图像显示和工具栏集成
- ROI 区域选择和编辑功能
- 相机设备管理和参数配置
- TCP 网络通信支持