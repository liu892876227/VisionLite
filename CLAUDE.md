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
系统已实现多种工业通讯协议，采用统一的接口设计：

#### 核心接口
- **ICommunication**: 统一通讯接口，定义连接状态管理和消息收发
- **IMessageProtocol**: 消息协议接口，用于协议解析
- **ITcpServer**: TCP服务器接口
- **Message**: 结构化消息对象

#### 已实现的通讯协议
- **TcpCommunication**: TCP 客户端实现，支持异步通讯
- **TcpServer**: TCP 服务器端实现，支持多客户端
- **UdpCommunication**: UDP 客户端实现
- **UdpServer**: UDP 服务器端实现
- **VisionLiteProtocol**: 自定义 TCP 消息协议
- **ModbusTcpClient**: ModbusTCP 客户端，基于 NModbus4 库
- **ModbusTcpServer**: ModbusTCP 服务器端实现
- **SerialCommunication**: 串口通讯实现
- **AdsCommunication**: 倍福 ADS 通讯实现，支持 TwinCAT PLC 变量读写

#### 辅助组件
- **ModbusAddressManager**: Modbus 地址管理器
- **ModbusAddressMap**: 地址映射配置
- **SimpleConnectionConfig**: 简化的连接配置
- **SimpleCommunicationWindow**: 通讯配置界面
- **AdsConnectionConfig**: ADS 连接参数配置
- **AdsConnectionTest**: ADS 连接测试工具

### 主要依赖库
- **HalconDotNet**: 机器视觉算法库，用于图像处理和显示
- **MvCameraControl.Net**: 海康威视相机 SDK
- **AForge**: 视频采集和处理框架（v2.2.5）
- **Extended.Wpf.Toolkit**: WPF 扩展控件库（v4.7.x）
- **Newtonsoft.Json**: JSON 序列化库（v13.0.3）
- **NModbus4**: ModbusTCP 通讯库（v2.1.0）
- **System.Drawing.Common**: 图形处理支持（v9.0.7）
- **TwinCAT.Ads**: 倍福 ADS 通讯库，用于与 TwinCAT PLC 通讯

### UI 架构
- **MainWindow**: 主窗口，管理所有相机显示和工具栏交互
- **CameraManagementWindow**: 相机管理窗口，处理设备枚举和连接
- **SimpleCommunicationWindow**: 简化的通讯配置窗口
- **SimpleAddConnectionWindow**: 添加通讯连接的配置对话框
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
- 新通讯方式需实现 `ICommunication` 接口，包含连接管理和消息收发
- 消息协议通过 `IMessageProtocol` 进行解析
- 使用异步模式处理网络 I/O 操作，避免阻塞UI线程
- 连接状态通过 `ConnectionStatus` 枚举管理（Disconnected/Connecting/Connected/Error）
- 状态变化通过事件通知上层，支持UI实时更新
- ModbusTCP 支持标准功能码，通过 `ModbusAddressMap` 进行地址映射
- 所有通讯模块都实现 `IDisposable`，确保资源正确释放

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
- **推荐构建命令**:
  ```bash
  # 使用完整路径避免版本冲突
  cd "VisionLite" && "C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\amd64\MSBuild.exe" VisionLite.csproj /p:Configuration=Debug /p:Platform=x64
  ```

### 测试和验证
- 目前项目暂无自动化测试框架
- 通讯模块测试主要通过 `SimpleCommunicationWindow` 进行手动验证
- ADS 通讯可通过 `AdsConnectionTest` 工具验证连接和变量读写
- 相机功能通过 `CameraManagementWindow` 进行设备枚举和连接测试

## 通讯协议支持

### ModbusTCP
- 基于 NModbus4 库实现
- 支持客户端和服务器模式
- 提供标准 Modbus 功能码支持
- 通过 `ModbusAddressManager` 管理设备地址映射

### TCP/UDP 通讯  
- 支持异步客户端和服务器模式
- 使用自定义 `VisionLiteProtocol` 消息格式
- 支持多客户端并发连接

### 串口通讯
- 支持标准串口参数配置
- 异步读写操作，避免阻塞

### ADS 通讯（倍福 PLC）
- 基于 TwinCAT.Ads 库实现，支持倍福 TwinCAT PLC 通讯
- 支持变量句柄缓存机制，提高读写性能
- 实现变量存在性检查和类型安全的读写操作
- 支持多种 PLC 数据类型（BOOL、BYTE、INT、DINT、REAL、LREAL、STRING）
- 提供连接状态监控和异常处理机制