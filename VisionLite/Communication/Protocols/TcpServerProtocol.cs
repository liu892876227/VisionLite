// Communication/Protocols/TcpServerProtocol.cs
// TCP服务器协议实现 - 支持TCP服务器端连接的协议定义
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using VisionLite.Communication.Models;
using VisionLite.Communication.Managers;

namespace VisionLite.Communication.Protocols
{
    /// <summary>
    /// TCP服务器通讯协议实现
    /// 提供TCP服务器连接的参数定义和实例创建功能
    /// </summary>
    public class TcpServerProtocol : ICommunicationProtocol
    {
        #region 静态构造函数 - 自动注册协议

        /// <summary>
        /// 静态构造函数，自动注册协议到管理器
        /// </summary>
        static TcpServerProtocol()
        {
            try
            {
                // 自动注册协议
                CommunicationProtocolManager.Instance.RegisterProtocol(new TcpServerProtocol());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TCP服务器协议自动注册失败: {ex.Message}");
            }
        }

        #endregion

        #region ICommunicationProtocol 基本属性实现

        /// <summary>
        /// 协议唯一标识符
        /// </summary>
        public string ProtocolName => "TCP_SERVER";

        /// <summary>
        /// 协议友好显示名称
        /// </summary>
        public string DisplayName => "TCP服务器";

        /// <summary>
        /// 协议详细描述
        /// </summary>
        public string Description => "基于TCP协议的服务器端实现，支持多客户端连接和可靠的数据传输。适用于需要向多个客户端提供服务的场景。";

        /// <summary>
        /// 协议版本
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// 是否支持服务器模式
        /// </summary>
        public bool SupportsServerMode => true;

        /// <summary>
        /// 是否支持客户端模式
        /// </summary>
        public bool SupportsClientMode => false;

        /// <summary>
        /// 是否支持广播模式
        /// </summary>
        public bool SupportsBroadcast => true; // 服务器可以向多个客户端广播

        /// <summary>
        /// 是否支持多播模式
        /// </summary>
        public bool SupportsMulticast => false;

        /// <summary>
        /// 是否需要安全配置
        /// </summary>
        public bool RequiresSecurityConfig => false;

        #endregion

        #region 参数定义

        /// <summary>
        /// 获取协议的参数定义列表
        /// </summary>
        /// <returns>参数定义列表</returns>
        public List<ParameterDefinition> GetParameterDefinitions()
        {
            return new List<ParameterDefinition>
            {
                // 监听IP地址参数
                new ParameterDefinition
                {
                    Key = "ListenIP",
                    DisplayName = "监听IP地址",
                    Type = ParameterType.IPAddress,
                    DefaultValue = "0.0.0.0",
                    IsRequired = true,
                    Description = "服务器监听的IP地址（0.0.0.0表示监听所有网卡）",
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 150
                },

                // 监听端口参数
                new ParameterDefinition
                {
                    Key = "Port",
                    DisplayName = "监听端口",
                    Type = ParameterType.Port,
                    DefaultValue = 8080,
                    IsRequired = true,
                    Description = "服务器监听的端口号（1-65535）",
                    MinValue = 1,
                    MaxValue = 65535,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 最大客户端连接数
                new ParameterDefinition
                {
                    Key = "MaxClients",
                    DisplayName = "最大客户端数",
                    Type = ParameterType.Integer,
                    DefaultValue = 10,
                    IsRequired = false,
                    Description = "允许的最大客户端连接数",
                    MinValue = 1,
                    MaxValue = 1000,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 连接超时参数
                new ParameterDefinition
                {
                    Key = "ClientTimeout",
                    DisplayName = "客户端超时",
                    Type = ParameterType.Integer,
                    DefaultValue = 30000,
                    IsRequired = false,
                    Description = "客户端连接超时时间（毫秒）",
                    MinValue = 5000,
                    MaxValue = 300000,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 接收缓冲区大小
                new ParameterDefinition
                {
                    Key = "ReceiveBufferSize",
                    DisplayName = "接收缓冲区",
                    Type = ParameterType.Integer,
                    DefaultValue = 8192,
                    IsRequired = false,
                    Description = "接收数据的缓冲区大小（字节）",
                    MinValue = 1024,
                    MaxValue = 65536,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 发送缓冲区大小
                new ParameterDefinition
                {
                    Key = "SendBufferSize",
                    DisplayName = "发送缓冲区",
                    Type = ParameterType.Integer,
                    DefaultValue = 8192,
                    IsRequired = false,
                    Description = "发送数据的缓冲区大小（字节）",
                    MinValue = 1024,
                    MaxValue = 65536,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 启用Nagle算法
                new ParameterDefinition
                {
                    Key = "NoDelay",
                    DisplayName = "禁用Nagle算法",
                    Type = ParameterType.Boolean,
                    DefaultValue = true,
                    IsRequired = false,
                    Description = "禁用Nagle算法以降低延迟（推荐启用）",
                    IsVisible = true,
                    IsEditable = true
                },

                // 保活机制
                new ParameterDefinition
                {
                    Key = "KeepAlive",
                    DisplayName = "启用保活",
                    Type = ParameterType.Boolean,
                    DefaultValue = true,
                    IsRequired = false,
                    Description = "启用TCP保活机制检测客户端连接状态",
                    IsVisible = true,
                    IsEditable = true
                },

                // 连接队列长度
                new ParameterDefinition
                {
                    Key = "Backlog",
                    DisplayName = "连接队列长度",
                    Type = ParameterType.Integer,
                    DefaultValue = 100,
                    IsRequired = false,
                    Description = "监听socket的挂起连接队列的最大长度",
                    MinValue = 1,
                    MaxValue = 1000,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 客户端认证
                new ParameterDefinition
                {
                    Key = "RequireAuth",
                    DisplayName = "需要客户端认证",
                    Type = ParameterType.Boolean,
                    DefaultValue = false,
                    IsRequired = false,
                    Description = "是否要求客户端连接时进行身份认证",
                    IsVisible = true,
                    IsEditable = true
                },

                // 心跳检测间隔
                new ParameterDefinition
                {
                    Key = "HeartbeatInterval",
                    DisplayName = "心跳间隔",
                    Type = ParameterType.Integer,
                    DefaultValue = 30000,
                    IsRequired = false,
                    Description = "服务器发送心跳的间隔时间（毫秒，0表示禁用）",
                    MinValue = 0,
                    MaxValue = 300000,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 编码格式
                new ParameterDefinition
                {
                    Key = "Encoding",
                    DisplayName = "字符编码",
                    Type = ParameterType.ComboBox,
                    DefaultValue = "UTF-8",
                    IsRequired = false,
                    Description = "文本消息的字符编码格式",
                    ValidValues = new object[] { "UTF-8", "ASCII", "Unicode", "GBK", "GB2312" },
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                }
            };
        }

        #endregion

        #region 实例创建和验证

        /// <summary>
        /// 创建TCP服务器通讯实例
        /// </summary>
        /// <param name="name">实例名称</param>
        /// <param name="parameters">配置参数</param>
        /// <returns>创建的通讯实例</returns>
        public ICommunication CreateInstance(string name, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("实例名称不能为空", nameof(name));

            if (parameters == null)
                parameters = new Dictionary<string, object>();

            // 获取必需参数
            var listenIP = GetParameterValue(parameters, "ListenIP", "0.0.0.0").ToString();
            var port = Convert.ToInt32(GetParameterValue(parameters, "Port", 8080));

            // 验证IP地址格式
            if (!IPAddress.TryParse(listenIP, out _))
                throw new ArgumentException($"无效的IP地址格式: {listenIP}");

            // 验证端口范围
            if (port < 1 || port > 65535)
                throw new ArgumentException($"端口号必须在1-65535范围内: {port}");

            try
            {
                // 创建TCP服务器实例
                var tcpServer = new TcpServer(name, port);

                // 设置可选参数
                SetOptionalParameters(tcpServer, parameters);

                return tcpServer;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建TCP服务器实例失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证参数配置的有效性
        /// </summary>
        /// <param name="parameters">要验证的参数</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null)
                return new ValidationResult(false, "参数字典不能为空");

            try
            {
                // 验证必需参数
                if (!parameters.ContainsKey("Port"))
                    return new ValidationResult(false, "缺少必需参数: Port");

                // 验证监听IP地址（如果提供）
                if (parameters.ContainsKey("ListenIP"))
                {
                    var listenIP = parameters["ListenIP"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(listenIP) && !IPAddress.TryParse(listenIP, out _))
                        return new ValidationResult(false, $"无效的IP地址格式: {listenIP}");
                }

                // 验证端口
                if (!int.TryParse(parameters["Port"]?.ToString(), out int port))
                    return new ValidationResult(false, "端口必须是整数");

                if (port < 1 || port > 65535)
                    return new ValidationResult(false, "端口号必须在1-65535范围内");

                // 验证可选的数值参数
                var numericParams = new[] 
                { 
                    "MaxClients", "ClientTimeout", "ReceiveBufferSize", 
                    "SendBufferSize", "Backlog", "HeartbeatInterval" 
                };

                foreach (var paramName in numericParams)
                {
                    if (parameters.ContainsKey(paramName))
                    {
                        if (!int.TryParse(parameters[paramName]?.ToString(), out int value))
                            return new ValidationResult(false, $"参数 {paramName} 必须是整数");

                        // 特殊的范围检查
                        if (paramName == "MaxClients" && value < 1)
                            return new ValidationResult(false, "最大客户端数必须大于0");

                        if (paramName == "ClientTimeout" && value < 1000)
                            return new ValidationResult(false, "客户端超时时间不能少于1000毫秒");

                        if (paramName == "HeartbeatInterval" && value < 0)
                            return new ValidationResult(false, "心跳间隔不能为负数");
                    }
                }

                return new ValidationResult(true);
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"参数验证时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 配置和模板

        /// <summary>
        /// 获取协议的默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        public CommunicationConfig GetDefaultConfig()
        {
            return new CommunicationConfig
            {
                Name = "TCP服务器",
                ProtocolType = ProtocolName,
                ProtocolDisplayName = DisplayName,
                Description = "默认的TCP服务器配置",
                Parameters = new Dictionary<string, object>
                {
                    ["ListenIP"] = "0.0.0.0",
                    ["Port"] = 8080,
                    ["MaxClients"] = 10,
                    ["ClientTimeout"] = 30000,
                    ["ReceiveBufferSize"] = 8192,
                    ["SendBufferSize"] = 8192,
                    ["NoDelay"] = true,
                    ["KeepAlive"] = true,
                    ["Backlog"] = 100,
                    ["RequireAuth"] = false,
                    ["HeartbeatInterval"] = 30000,
                    ["Encoding"] = "UTF-8"
                }
            };
        }

        /// <summary>
        /// 获取支持的消息格式列表
        /// </summary>
        /// <returns>消息格式列表</returns>
        public List<string> GetSupportedMessageFormats()
        {
            return new List<string>
            {
                "JSON",
                "XML",
                "Plain Text",
                "Binary",
                "Custom Protocol"
            };
        }

        /// <summary>
        /// 获取配置模板列表
        /// </summary>
        /// <returns>配置模板列表</returns>
        public List<CommunicationConfigTemplate> GetConfigTemplates()
        {
            return new List<CommunicationConfigTemplate>
            {
                // 标准配置模板
                new CommunicationConfigTemplate
                {
                    Name = "标准服务器配置",
                    Description = "适用于大多数应用场景的标准TCP服务器配置",
                    Category = "标准",
                    IsRecommended = true,
                    Parameters = new Dictionary<string, object>
                    {
                        ["ListenIP"] = "0.0.0.0",
                        ["Port"] = 8080,
                        ["MaxClients"] = 10,
                        ["NoDelay"] = true,
                        ["KeepAlive"] = true
                    }
                },

                // 高并发配置模板
                new CommunicationConfigTemplate
                {
                    Name = "高并发配置",
                    Description = "优化高并发连接的TCP服务器配置",
                    Category = "性能优化",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ListenIP"] = "0.0.0.0",
                        ["Port"] = 8080,
                        ["MaxClients"] = 100,
                        ["ClientTimeout"] = 60000,
                        ["ReceiveBufferSize"] = 16384,
                        ["SendBufferSize"] = 16384,
                        ["Backlog"] = 200,
                        ["NoDelay"] = true,
                        ["KeepAlive"] = true,
                        ["HeartbeatInterval"] = 15000
                    }
                },

                // 本地测试配置模板
                new CommunicationConfigTemplate
                {
                    Name = "本地测试配置",
                    Description = "用于本地开发测试的TCP服务器配置",
                    Category = "测试",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ListenIP"] = "127.0.0.1",
                        ["Port"] = 8080,
                        ["MaxClients"] = 3,
                        ["ClientTimeout"] = 10000,
                        ["NoDelay"] = true,
                        ["HeartbeatInterval"] = 5000
                    }
                },

                // 安全配置模板
                new CommunicationConfigTemplate
                {
                    Name = "安全配置",
                    Description = "注重安全性的TCP服务器配置",
                    Category = "安全",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ListenIP"] = "127.0.0.1", // 只监听本地
                        ["Port"] = 8443,
                        ["MaxClients"] = 5,
                        ["ClientTimeout"] = 15000,
                        ["RequireAuth"] = true,
                        ["KeepAlive"] = true,
                        ["HeartbeatInterval"] = 10000
                    }
                }
            };
        }

        #endregion

        #region 测试和性能信息

        /// <summary>
        /// 测试连接参数
        /// </summary>
        /// <param name="parameters">连接参数</param>
        /// <param name="timeoutMs">测试超时时间</param>
        /// <returns>测试结果</returns>
        public async Task<TestConnectionResult> TestConnectionAsync(Dictionary<string, object> parameters, int timeoutMs = 5000)
        {
            var result = new TestConnectionResult(false);
            result.TestStartTime = DateTime.Now;

            try
            {
                // 验证参数
                var validation = ValidateParameters(parameters);
                if (!validation.IsValid)
                {
                    result.ErrorMessage = validation.ErrorMessage;
                    return result;
                }

                // 获取连接参数
                var listenIP = GetParameterValue(parameters, "ListenIP", "0.0.0.0").ToString();
                var port = Convert.ToInt32(GetParameterValue(parameters, "Port", 8080));

                // 创建临时服务器进行测试
                var testServer = new TcpServer("TestServer", port);

                try
                {
                    // 尝试启动服务器
                    var startTask = testServer.OpenAsync();
                    var completedTask = await Task.WhenAny(startTask, Task.Delay(timeoutMs));

                    if (completedTask == startTask)
                    {
                        bool started = await startTask;
                        if (started)
                        {
                            result.Success = true;
                            result.ResponseInfo = $"成功启动服务器，监听 {listenIP}:{port}";

                            // 计算启动时间
                            result.TestEndTime = DateTime.Now;
                            result.LatencyMs = (int)(result.TestEndTime - result.TestStartTime).TotalMilliseconds;
                        }
                        else
                        {
                            result.ErrorMessage = "服务器启动失败";
                        }
                    }
                    else
                    {
                        result.ErrorMessage = $"服务器启动超时（{timeoutMs}ms）";
                    }
                }
                finally
                {
                    // 确保关闭测试服务器
                    testServer.Close();
                    testServer.Dispose();
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"测试服务器时发生异常: {ex.Message}";
            }

            result.TestEndTime = DateTime.Now;
            return result;
        }

        /// <summary>
        /// 获取协议性能特征信息
        /// </summary>
        /// <returns>性能信息</returns>
        public ProtocolPerformanceInfo GetPerformanceInfo()
        {
            return new ProtocolPerformanceInfo
            {
                LatencyLevel = PerformanceLevel.Medium,
                ThroughputLevel = PerformanceLevel.High,
                ReliabilityLevel = PerformanceLevel.High,
                ResourceUsageLevel = PerformanceLevel.Medium,
                MaxConcurrentConnections = 1000, // 根据配置可以支持多个客户端
                RecommendedHeartbeatInterval = 30,
                OverheadDescription = "TCP服务器可以同时处理多个客户端连接，具有良好的并发性能和可靠性。资源使用量随连接数增加而增长。"
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取参数值，如果不存在则返回默认值
        /// </summary>
        /// <param name="parameters">参数字典</param>
        /// <param name="key">参数键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        private object GetParameterValue(Dictionary<string, object> parameters, string key, object defaultValue)
        {
            if (parameters.TryGetValue(key, out object value) && value != null)
                return value;
            return defaultValue;
        }

        /// <summary>
        /// 设置可选参数到TCP服务器实例
        /// </summary>
        /// <param name="tcpServer">TCP服务器实例</param>
        /// <param name="parameters">参数字典</param>
        private void SetOptionalParameters(TcpServer tcpServer, Dictionary<string, object> parameters)
        {
            // 注意：由于TcpServer类的具体实现可能不公开这些设置方法，
            // 这里只是展示如何处理可选参数的概念。
            // 实际实现中需要根据TcpServer类的API来调用相应的设置方法。

            // 示例：如果TcpServer支持设置这些参数
            /*
            if (parameters.ContainsKey("MaxClients"))
            {
                var maxClients = Convert.ToInt32(parameters["MaxClients"]);
                tcpServer.SetMaxClients(maxClients);
            }

            if (parameters.ContainsKey("ClientTimeout"))
            {
                var timeout = Convert.ToInt32(parameters["ClientTimeout"]);
                tcpServer.SetClientTimeout(timeout);
            }
            */

            // 对于现有的TcpServer类，我们可能需要在构造函数中传递这些参数
            // 或者在类中添加相应的配置方法
        }

        #endregion
    }
}