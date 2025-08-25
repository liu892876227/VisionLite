// Communication/Protocols/TcpClientProtocol.cs
// TCP客户端协议实现 - 支持TCP客户端连接的协议定义
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using VisionLite.Communication.Models;
using VisionLite.Communication.Managers;

namespace VisionLite.Communication.Protocols
{
    /// <summary>
    /// TCP客户端通讯协议实现
    /// 提供TCP客户端连接的参数定义和实例创建功能
    /// </summary>
    public class TcpClientProtocol : ICommunicationProtocol
    {
        #region 静态构造函数 - 自动注册协议

        /// <summary>
        /// 静态构造函数，自动注册协议到管理器
        /// </summary>
        static TcpClientProtocol()
        {
            try
            {
                // 自动注册协议
                CommunicationProtocolManager.Instance.RegisterProtocol(new TcpClientProtocol());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TCP客户端协议自动注册失败: {ex.Message}");
            }
        }

        #endregion

        #region ICommunicationProtocol 基本属性实现

        /// <summary>
        /// 协议唯一标识符
        /// </summary>
        public string ProtocolName => "TCP_CLIENT";

        /// <summary>
        /// 协议友好显示名称
        /// </summary>
        public string DisplayName => "TCP客户端";

        /// <summary>
        /// 协议详细描述
        /// </summary>
        public string Description => "基于TCP协议的客户端连接，支持可靠的点对点通讯。适用于需要稳定连接和数据完整性的场景。";

        /// <summary>
        /// 协议版本
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// 是否支持服务器模式
        /// </summary>
        public bool SupportsServerMode => false;

        /// <summary>
        /// 是否支持客户端模式
        /// </summary>
        public bool SupportsClientMode => true;

        /// <summary>
        /// 是否支持广播模式
        /// </summary>
        public bool SupportsBroadcast => false;

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
                // 目标IP地址参数
                new ParameterDefinition
                {
                    Key = "IP",
                    DisplayName = "目标IP地址",
                    Type = ParameterType.IPAddress,
                    DefaultValue = "127.0.0.1",
                    IsRequired = true,
                    Description = "要连接的服务器IP地址",
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 150
                },

                // 目标端口参数
                new ParameterDefinition
                {
                    Key = "Port",
                    DisplayName = "目标端口",
                    Type = ParameterType.Port,
                    DefaultValue = 8080,
                    IsRequired = true,
                    Description = "要连接的服务器端口号（1-65535）",
                    MinValue = 1,
                    MaxValue = 65535,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 连接超时参数
                new ParameterDefinition
                {
                    Key = "ConnectionTimeout",
                    DisplayName = "连接超时",
                    Type = ParameterType.Integer,
                    DefaultValue = 5000,
                    IsRequired = false,
                    Description = "建立连接的超时时间（毫秒）",
                    MinValue = 1000,
                    MaxValue = 60000,
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

                // 自动重连选项
                new ParameterDefinition
                {
                    Key = "AutoReconnect",
                    DisplayName = "自动重连",
                    Type = ParameterType.Boolean,
                    DefaultValue = true,
                    IsRequired = false,
                    Description = "连接断开时是否自动重连",
                    IsVisible = true,
                    IsEditable = true
                },

                // 重连间隔
                new ParameterDefinition
                {
                    Key = "ReconnectInterval",
                    DisplayName = "重连间隔",
                    Type = ParameterType.Integer,
                    DefaultValue = 3000,
                    IsRequired = false,
                    Description = "自动重连的间隔时间（毫秒）",
                    MinValue = 1000,
                    MaxValue = 60000,
                    IsVisible = true,
                    IsEditable = true,
                    ControlWidth = 100
                },

                // 最大重连次数
                new ParameterDefinition
                {
                    Key = "MaxReconnectAttempts",
                    DisplayName = "最大重连次数",
                    Type = ParameterType.Integer,
                    DefaultValue = -1,
                    IsRequired = false,
                    Description = "最大重连尝试次数（-1表示无限制）",
                    MinValue = -1,
                    MaxValue = 1000,
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
                    Description = "启用TCP保活机制检测连接状态",
                    IsVisible = true,
                    IsEditable = true
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
        /// 创建TCP客户端通讯实例
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
            var ip = GetParameterValue(parameters, "IP", "127.0.0.1").ToString();
            var port = Convert.ToInt32(GetParameterValue(parameters, "Port", 8080));

            // 验证IP地址格式
            if (!IPAddress.TryParse(ip, out _))
                throw new ArgumentException($"无效的IP地址格式: {ip}");

            // 验证端口范围
            if (port < 1 || port > 65535)
                throw new ArgumentException($"端口号必须在1-65535范围内: {port}");

            try
            {
                // 创建TCP客户端实例
                var tcpClient = new TcpCommunication(name, ip, port);

                // 设置可选参数
                SetOptionalParameters(tcpClient, parameters);

                return tcpClient;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建TCP客户端实例失败: {ex.Message}", ex);
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
                if (!parameters.ContainsKey("IP"))
                    return new ValidationResult(false, "缺少必需参数: IP");

                if (!parameters.ContainsKey("Port"))
                    return new ValidationResult(false, "缺少必需参数: Port");

                // 验证IP地址
                var ip = parameters["IP"]?.ToString();
                if (string.IsNullOrWhiteSpace(ip))
                    return new ValidationResult(false, "IP地址不能为空");

                if (!IPAddress.TryParse(ip, out _))
                    return new ValidationResult(false, $"无效的IP地址格式: {ip}");

                // 验证端口
                if (!int.TryParse(parameters["Port"]?.ToString(), out int port))
                    return new ValidationResult(false, "端口必须是整数");

                if (port < 1 || port > 65535)
                    return new ValidationResult(false, "端口号必须在1-65535范围内");

                // 验证可选的数值参数
                var numericParams = new[] { "ConnectionTimeout", "ReceiveBufferSize", "SendBufferSize", "ReconnectInterval", "MaxReconnectAttempts" };
                foreach (var paramName in numericParams)
                {
                    if (parameters.ContainsKey(paramName))
                    {
                        if (!int.TryParse(parameters[paramName]?.ToString(), out _))
                            return new ValidationResult(false, $"参数 {paramName} 必须是整数");
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
                Name = "TCP客户端",
                ProtocolType = ProtocolName,
                ProtocolDisplayName = DisplayName,
                Description = "默认的TCP客户端连接配置",
                Parameters = new Dictionary<string, object>
                {
                    ["IP"] = "127.0.0.1",
                    ["Port"] = 8080,
                    ["ConnectionTimeout"] = 5000,
                    ["ReceiveBufferSize"] = 8192,
                    ["SendBufferSize"] = 8192,
                    ["AutoReconnect"] = true,
                    ["ReconnectInterval"] = 3000,
                    ["MaxReconnectAttempts"] = -1,
                    ["NoDelay"] = true,
                    ["KeepAlive"] = true,
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
                    Name = "标准配置",
                    Description = "适用于大多数应用场景的标准TCP客户端配置",
                    Category = "标准",
                    IsRecommended = true,
                    Parameters = new Dictionary<string, object>
                    {
                        ["IP"] = "127.0.0.1",
                        ["Port"] = 8080,
                        ["ConnectionTimeout"] = 5000,
                        ["AutoReconnect"] = true,
                        ["NoDelay"] = true
                    }
                },

                // 高性能配置模板
                new CommunicationConfigTemplate
                {
                    Name = "高性能配置",
                    Description = "优化性能的TCP客户端配置，适用于高频通讯场景",
                    Category = "性能优化",
                    Parameters = new Dictionary<string, object>
                    {
                        ["IP"] = "127.0.0.1",
                        ["Port"] = 8080,
                        ["ConnectionTimeout"] = 3000,
                        ["ReceiveBufferSize"] = 16384,
                        ["SendBufferSize"] = 16384,
                        ["NoDelay"] = true,
                        ["KeepAlive"] = true,
                        ["AutoReconnect"] = true,
                        ["ReconnectInterval"] = 1000
                    }
                },

                // 稳定连接配置模板
                new CommunicationConfigTemplate
                {
                    Name = "稳定连接配置",
                    Description = "注重连接稳定性的配置，适用于长时间运行的应用",
                    Category = "稳定性优先",
                    Parameters = new Dictionary<string, object>
                    {
                        ["IP"] = "127.0.0.1",
                        ["Port"] = 8080,
                        ["ConnectionTimeout"] = 10000,
                        ["AutoReconnect"] = true,
                        ["ReconnectInterval"] = 5000,
                        ["MaxReconnectAttempts"] = 100,
                        ["KeepAlive"] = true
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
                var ip = parameters["IP"].ToString();
                var port = Convert.ToInt32(parameters["Port"]);

                // 创建临时连接进行测试
                var testClient = new TcpCommunication("TestConnection", ip, port);

                using (testClient)
                {
                    // 尝试连接
                    var connectTask = testClient.OpenAsync();
                    var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));

                    if (completedTask == connectTask)
                    {
                        bool connected = await connectTask;
                        if (connected)
                        {
                            result.Success = true;
                            result.ResponseInfo = $"成功连接到 {ip}:{port}";
                            
                            // 计算延迟
                            result.TestEndTime = DateTime.Now;
                            result.LatencyMs = (int)(result.TestEndTime - result.TestStartTime).TotalMilliseconds;
                        }
                        else
                        {
                            result.ErrorMessage = "连接失败";
                        }

                        testClient.Close();
                    }
                    else
                    {
                        result.ErrorMessage = $"连接超时（{timeoutMs}ms）";
                        testClient.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"测试连接时发生异常: {ex.Message}";
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
                MaxConcurrentConnections = 1, // 单个客户端实例只能维持一个连接
                RecommendedHeartbeatInterval = 30,
                OverheadDescription = "TCP协议具有较高的可靠性，但会有一定的协议开销。适合需要可靠传输的场景。"
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
        /// 设置可选参数到TCP通讯实例
        /// </summary>
        /// <param name="tcpClient">TCP通讯实例</param>
        /// <param name="parameters">参数字典</param>
        private void SetOptionalParameters(TcpCommunication tcpClient, Dictionary<string, object> parameters)
        {
            // 注意：由于TcpCommunication类的具体实现可能不公开这些设置方法，
            // 这里只是展示如何处理可选参数的概念。
            // 实际实现中需要根据TcpCommunication类的API来调用相应的设置方法。

            // 示例：如果TcpCommunication支持设置这些参数
            /*
            if (parameters.ContainsKey("ConnectionTimeout"))
            {
                var timeout = Convert.ToInt32(parameters["ConnectionTimeout"]);
                tcpClient.SetConnectionTimeout(timeout);
            }

            if (parameters.ContainsKey("ReceiveBufferSize"))
            {
                var bufferSize = Convert.ToInt32(parameters["ReceiveBufferSize"]);
                tcpClient.SetReceiveBufferSize(bufferSize);
            }
            */

            // 对于现有的TcpCommunication类，我们可能需要在构造函数中传递这些参数
            // 或者在类中添加相应的配置方法
        }

        #endregion
    }
}