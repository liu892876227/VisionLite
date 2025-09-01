# VisionLite ADS通讯使用指南

## 概述
VisionLite现在集成了倍福ADS通讯功能，支持与TwinCAT PLC进行实时数据交换。该功能基于TwinCAT.Ads库实现，并集成到VisionLite统一通讯框架中。

## 核心特性

### 🚀 主要功能
- **统一接口**：实现ICommunication接口，与其他通讯模块保持一致
- **句柄缓存**：自动缓存变量句柄，提高读写性能
- **异步连接**：支持异步连接操作，避免阻塞UI线程
- **状态监控**：实时监控连接状态，支持事件通知
- **类型安全**：支持泛型读写操作，包括数组支持
- **资源管理**：自动管理ADS资源，确保正确释放

### 📋 支持的数据类型
- `BOOL` → `bool`
- `BYTE` → `byte`
- `INT` → `short`
- `DINT` → `int`
- `REAL` → `float`
- `LREAL` → `double`
- `STRING` → `string`
- 一维和二维数组支持

## 快速开始

### 1. 创建ADS连接配置
```csharp
using VisionLite.Communication;

// 创建默认配置
var config = AdsConnectionConfig.CreateDefault();

// 或自定义配置
var customConfig = new AdsConnectionConfig
{
    TargetAmsNetId = "192.168.1.100.1.1",  // PLC的NetId
    TargetAmsPort = 801,                    // Runtime端口
    Timeout = 5000,                         // 5秒超时
    UseSymbolicAccess = true,               // 启用符号访问
    DisplayName = "生产线PLC"
};
```

### 2. 建立连接
```csharp
// 创建ADS通讯对象
var adsComm = new AdsCommunication(config);

// 订阅事件
adsComm.StatusChanged += (status) => 
{
    Console.WriteLine($"连接状态变化: {status}");
};

adsComm.LogReceived += (logMsg) => 
{
    Console.WriteLine(logMsg);
};

// 异步连接
bool connected = await adsComm.OpenAsync();
if (connected)
{
    Console.WriteLine("ADS连接成功！");
}
```

### 3. 读写PLC变量
```csharp
// 读取布尔变量
bool motorRunning = false;
if (adsComm.ReadVariable("MAIN.bMotorRunning", ref motorRunning))
{
    Console.WriteLine($"电机运行状态: {motorRunning}");
}

// 写入整数变量
int targetSpeed = 1500;
if (adsComm.WriteVariable("MAIN.nTargetSpeed", targetSpeed))
{
    Console.WriteLine("目标速度设置成功");
}

// 读取浮点数组
float[] temperatures = new float[10];
if (adsComm.ReadVariableArray("MAIN.arrTemperatures", ref temperatures))
{
    Console.WriteLine($"温度数据读取成功，共{temperatures.Length}个值");
}
```

### 4. 字符串转换写入
```csharp
// 从字符串写入不同类型的值
adsComm.WriteVariableFromString("MAIN.bEnable", "true", "BOOL");
adsComm.WriteVariableFromString("MAIN.nCount", "100", "DINT");
adsComm.WriteVariableFromString("MAIN.fSpeed", "123.45", "REAL");
```

## 集成到VisionLite

### 在主窗口中使用ADS通讯
```csharp
public partial class MainWindow : Window
{
    private AdsCommunication _adsComm;
    
    private async void InitializeADS()
    {
        try
        {
            var config = new AdsConnectionConfig
            {
                TargetAmsNetId = "192.168.1.100.1.1",
                TargetAmsPort = 801,
                DisplayName = "主控PLC"
            };
            
            _adsComm = new AdsCommunication(config);
            
            // 绑定到UI日志
            _adsComm.LogReceived += (msg) => 
            {
                Dispatcher.Invoke(() => 
                {
                    // 将日志显示到UI的日志窗口
                    LogTextBox.AppendText(msg + "\n");
                });
            };
            
            // 连接状态更新UI
            _adsComm.StatusChanged += (status) => 
            {
                Dispatcher.Invoke(() => 
                {
                    StatusLabel.Content = $"ADS状态: {status}";
                    ConnectButton.IsEnabled = (status == ConnectionStatus.Disconnected);
                });
            };
            
            bool connected = await _adsComm.OpenAsync();
            if (connected)
            {
                // 启动数据监控定时器
                StartDataMonitoring();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ADS初始化失败: {ex.Message}");
        }
    }
    
    private void StartDataMonitoring()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // 500ms刷新
        };
        
        timer.Tick += (s, e) => 
        {
            try
            {
                // 读取PLC状态
                bool systemReady = false;
                if (_adsComm.ReadVariable("MAIN.bSystemReady", ref systemReady))
                {
                    SystemStatusLight.Fill = systemReady ? Brushes.Green : Brushes.Red;
                }
                
                // 读取计数器
                int partCount = 0;
                if (_adsComm.ReadVariable("MAIN.nPartCount", ref partCount))
                {
                    PartCountLabel.Content = $"产品数量: {partCount}";
                }
            }
            catch (Exception ex)
            {
                // 处理读取错误，但不中断定时器
                Console.WriteLine($"数据监控错误: {ex.Message}");
            }
        };
        
        timer.Start();
    }
    
    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        _ = Task.Run(async () => await InitializeADS());
    }
    
    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        _adsComm?.Close();
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _adsComm?.Dispose();
        base.OnClosed(e);
    }
}
```

## 连接测试

### 使用内置测试工具
```csharp
// 完整连接测试
var testConfig = AdsConnectionTest.CreateTestConfig();
testConfig.TargetAmsNetId = "192.168.1.100.1.1"; // 修改为你的PLC地址

string testResult = await AdsConnectionTest.TestConnection(testConfig);
Console.WriteLine(testResult);

// 快速连接测试
bool isConnectable = await AdsConnectionTest.QuickConnectionTest("192.168.1.100.1.1");
Console.WriteLine($"连接测试结果: {(isConnectable ? "成功" : "失败")}");

// 批量测试多个PLC
string[] plcAddresses = {
    "192.168.1.100.1.1",
    "192.168.1.101.1.1",
    "192.168.1.102.1.1"
};
string batchResult = await AdsConnectionTest.BatchConnectionTest(plcAddresses);
Console.WriteLine(batchResult);
```

## 错误处理和调试

### 常见连接问题
1. **连接超时**
   - 检查网络连通性：`ping 192.168.1.100`
   - 确认PLC运行状态
   - 检查Windows防火墙设置

2. **NetId格式错误**
   - 正确格式：`IP地址.1.1`（如：`192.168.1.100.1.1`）
   - 避免使用主机名，使用IP地址

3. **端口问题**
   - PLC Runtime通常使用端口801
   - System Service使用端口851
   - 确认PLC项目已下载并运行

4. **变量不存在**
   - 确认PLC程序中存在对应变量
   - 检查变量名拼写（区分大小写）
   - 确认变量作用域（MAIN、GVL等）

### 调试技巧
```csharp
// 启用详细日志
adsComm.LogReceived += (msg) => 
{
    Debug.WriteLine(msg);
    // 或写入文件
    File.AppendAllText("ads_log.txt", msg + "\n");
};

// 监控连接状态变化
adsComm.StatusChanged += (status) => 
{
    Debug.WriteLine($"ADS状态变化: {DateTime.Now:HH:mm:ss} -> {status}");
};
```

## 性能优化

### 句柄缓存
- 系统自动缓存变量句柄，避免重复创建
- 首次访问变量较慢，后续访问性能显著提升
- 缓存在连接断开时自动清理

### 批量操作
```csharp
// 避免频繁单独读取，考虑批量操作
var variables = new[] { "MAIN.var1", "MAIN.var2", "MAIN.var3" };
foreach (var varName in variables)
{
    // 这种方式会复用缓存的句柄，性能较好
    bool value = false;
    adsComm.ReadVariable(varName, ref value);
}
```

## 注意事项

⚠️ **重要提醒**
- 确保在应用程序关闭时调用 `Dispose()` 释放资源
- 避免在UI线程进行同步连接操作，使用 `OpenAsync()`
- PLC变量名区分大小写，请确保准确性
- 建议在生产环境中使用异常处理包装所有ADS操作
- 定时读取操作建议间隔不少于50ms，避免过于频繁

## 与现有代码集成

### 替换原有AdsClientService
如果你有使用原来的`AdsClientService_other.cs`代码，可以这样迁移：

```csharp
// 原代码
var oldService = new AdsClientService("192.168.1.100.1.1", 801);
bool value = false;
oldService.Read("MAIN.bTest", ref value);

// 新代码
var config = new AdsConnectionConfig 
{ 
    TargetAmsNetId = "192.168.1.100.1.1", 
    TargetAmsPort = 801 
};
var newComm = new AdsCommunication(config);
await newComm.OpenAsync();
bool value = false;
newComm.ReadVariable("MAIN.bTest", ref value);
```

主要差异：
- 需要先调用 `OpenAsync()` 建立连接
- 方法名从 `Read/Write` 改为 `ReadVariable/WriteVariable`
- 支持异步操作和事件通知
- 更好的错误处理和资源管理

现在你的VisionLite项目已经成功集成了倍福ADS通讯功能！🎉