// Communication/Protocols/ICommunicationProtocol.cs
// 通讯协议抽象接口 - 定义所有通讯协议实现必须遵循的规范
using System;
using System.Collections.Generic;
using VisionLite.Communication.Models;

namespace VisionLite.Communication.Protocols
{
    /// <summary>
    /// 通讯协议接口
    /// 所有通讯协议实现类都必须实现此接口，以支持动态协议注册和配置
    /// </summary>
    public interface ICommunicationProtocol
    {
        /// <summary>
        /// 协议的唯一标识符
        /// 用于在系统中注册和识别协议，应保证全局唯一
        /// 建议使用大写字母和下划线的命名方式，如：TCP_CLIENT、UDP_BROADCAST等
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// 协议的友好显示名称
        /// 在用户界面中显示给用户看的协议名称，应该简洁明了
        /// 如：TCP客户端、UDP广播、串口通讯等
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 协议的详细描述信息
        /// 用于帮助用户了解协议的用途和特点
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 协议的版本号
        /// 用于版本管理和兼容性检查
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 协议是否支持服务器模式
        /// 某些协议可能只支持客户端或只支持服务器模式
        /// </summary>
        bool SupportsServerMode { get; }

        /// <summary>
        /// 协议是否支持客户端模式
        /// </summary>
        bool SupportsClientMode { get; }

        /// <summary>
        /// 协议是否支持广播模式
        /// </summary>
        bool SupportsBroadcast { get; }

        /// <summary>
        /// 协议是否支持多播模式
        /// </summary>
        bool SupportsMulticast { get; }

        /// <summary>
        /// 获取协议的参数定义列表
        /// 返回该协议需要用户配置的所有参数定义，系统将根据这些定义动态生成配置界面
        /// </summary>
        /// <returns>参数定义列表</returns>
        List<ParameterDefinition> GetParameterDefinitions();

        /// <summary>
        /// 根据参数配置创建通讯实例
        /// 这是工厂方法，根据用户的配置参数创建具体的通讯连接实例
        /// </summary>
        /// <param name="name">连接实例的名称</param>
        /// <param name="parameters">配置参数字典</param>
        /// <returns>创建的通讯连接实例</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出</exception>
        /// <exception cref="NotSupportedException">当请求的配置不被支持时抛出</exception>
        ICommunication CreateInstance(string name, Dictionary<string, object> parameters);

        /// <summary>
        /// 验证参数配置的有效性
        /// 在创建实例之前验证参数是否正确，提供更好的用户体验
        /// </summary>
        /// <param name="parameters">要验证的参数字典</param>
        /// <returns>验证结果</returns>
        ValidationResult ValidateParameters(Dictionary<string, object> parameters);

        /// <summary>
        /// 获取协议的默认配置
        /// 返回该协议的推荐默认配置，用于快速创建连接
        /// </summary>
        /// <returns>默认配置对象</returns>
        CommunicationConfig GetDefaultConfig();

        /// <summary>
        /// 获取协议支持的消息格式列表
        /// 返回协议支持的消息格式，如JSON、XML、纯文本等
        /// </summary>
        /// <returns>支持的消息格式列表</returns>
        List<string> GetSupportedMessageFormats();

        /// <summary>
        /// 测试连接参数是否正确
        /// 创建一个临时连接进行测试，不会影响正式的连接实例
        /// </summary>
        /// <param name="parameters">连接参数</param>
        /// <param name="timeoutMs">测试超时时间（毫秒）</param>
        /// <returns>测试结果，包含成功状态和错误信息</returns>
        System.Threading.Tasks.Task<TestConnectionResult> TestConnectionAsync(
            Dictionary<string, object> parameters, 
            int timeoutMs = 5000);

        /// <summary>
        /// 获取协议的配置模板
        /// 返回常用的配置模板，帮助用户快速设置
        /// </summary>
        /// <returns>配置模板列表</returns>
        List<CommunicationConfigTemplate> GetConfigTemplates();

        /// <summary>
        /// 协议是否需要特殊的安全配置
        /// 某些协议可能需要证书、密钥等安全配置
        /// </summary>
        bool RequiresSecurityConfig { get; }

        /// <summary>
        /// 获取协议的性能特征信息
        /// 返回协议的性能特点，如延迟、吞吐量等级等
        /// </summary>
        /// <returns>性能特征信息</returns>
        ProtocolPerformanceInfo GetPerformanceInfo();
    }

    /// <summary>
    /// 连接测试结果
    /// </summary>
    public class TestConnectionResult
    {
        /// <summary>
        /// 测试是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息（测试失败时）
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 连接延迟（毫秒）
        /// </summary>
        public int LatencyMs { get; set; }

        /// <summary>
        /// 测试开始时间
        /// </summary>
        public DateTime TestStartTime { get; set; }

        /// <summary>
        /// 测试结束时间
        /// </summary>
        public DateTime TestEndTime { get; set; }

        /// <summary>
        /// 服务器或对端的响应信息
        /// </summary>
        public string ResponseInfo { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="success">测试结果</param>
        /// <param name="errorMessage">错误消息</param>
        public TestConnectionResult(bool success, string errorMessage = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            TestStartTime = DateTime.Now;
            TestEndTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 通讯配置模板
    /// </summary>
    public class CommunicationConfigTemplate
    {
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 模板描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 模板参数配置
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 是否为推荐模板
        /// </summary>
        public bool IsRecommended { get; set; } = false;

        /// <summary>
        /// 模板类别（如：标准配置、高性能配置、安全配置等）
        /// </summary>
        public string Category { get; set; }
    }

    /// <summary>
    /// 协议性能特征信息
    /// </summary>
    public class ProtocolPerformanceInfo
    {
        /// <summary>
        /// 典型延迟级别（Low、Medium、High）
        /// </summary>
        public PerformanceLevel LatencyLevel { get; set; }

        /// <summary>
        /// 吞吐量级别（Low、Medium、High）
        /// </summary>
        public PerformanceLevel ThroughputLevel { get; set; }

        /// <summary>
        /// 可靠性级别（Low、Medium、High）
        /// </summary>
        public PerformanceLevel ReliabilityLevel { get; set; }

        /// <summary>
        /// 资源消耗级别（Low、Medium、High）
        /// </summary>
        public PerformanceLevel ResourceUsageLevel { get; set; }

        /// <summary>
        /// 最大并发连接数（-1表示无限制）
        /// </summary>
        public int MaxConcurrentConnections { get; set; } = -1;

        /// <summary>
        /// 建议的心跳间隔（秒，-1表示不需要心跳）
        /// </summary>
        public int RecommendedHeartbeatInterval { get; set; } = -1;

        /// <summary>
        /// 协议开销描述
        /// </summary>
        public string OverheadDescription { get; set; }
    }

    /// <summary>
    /// 性能级别枚举
    /// </summary>
    public enum PerformanceLevel
    {
        Low,
        Medium, 
        High,
        VeryHigh
    }
}