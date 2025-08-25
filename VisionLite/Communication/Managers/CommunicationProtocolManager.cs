// Communication/Managers/CommunicationProtocolManager.cs
// 通讯协议管理器 - 负责协议的注册、发现和实例化管理
using System;
using System.Collections.Generic;
using System.Linq;
using VisionLite.Communication.Models;
using VisionLite.Communication.Protocols;

namespace VisionLite.Communication.Managers
{
    /// <summary>
    /// 通讯协议管理器
    /// 提供协议的注册、查找、创建等核心功能
    /// 采用单例模式，确保全局统一的协议管理
    /// </summary>
    public class CommunicationProtocolManager
    {
        #region 单例模式实现

        private static readonly object _lockObject = new object();
        private static CommunicationProtocolManager _instance;

        /// <summary>
        /// 获取协议管理器的单例实例
        /// </summary>
        public static CommunicationProtocolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new CommunicationProtocolManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 私有字段和属性

        /// <summary>
        /// 已注册的协议字典，键为协议名称，值为协议实现实例
        /// </summary>
        private readonly Dictionary<string, ICommunicationProtocol> _protocols = 
            new Dictionary<string, ICommunicationProtocol>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 协议注册事件，当新协议被注册时触发
        /// </summary>
        public event EventHandler<ProtocolRegisteredEventArgs> ProtocolRegistered;

        /// <summary>
        /// 协议注销事件，当协议被移除时触发
        /// </summary>
        public event EventHandler<ProtocolUnregisteredEventArgs> ProtocolUnregistered;

        /// <summary>
        /// 获取已注册的协议数量
        /// </summary>
        public int RegisteredProtocolCount => _protocols.Count;

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 私有构造函数，防止外部直接创建实例
        /// </summary>
        private CommunicationProtocolManager()
        {
            InitializeBuiltInProtocols();
        }

        /// <summary>
        /// 初始化内置协议
        /// 注册系统预定义的通讯协议
        /// </summary>
        private void InitializeBuiltInProtocols()
        {
            try
            {
                // 延迟加载具体协议实现，避免循环依赖
                // 具体协议将在协议实现类中自动注册
                System.Diagnostics.Debug.WriteLine("通讯协议管理器初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"协议管理器初始化异常: {ex.Message}");
            }
        }

        #endregion

        #region 协议注册和管理方法

        /// <summary>
        /// 注册新的通讯协议
        /// </summary>
        /// <param name="protocol">要注册的协议实例</param>
        /// <returns>注册是否成功</returns>
        /// <exception cref="ArgumentNullException">协议实例为空时抛出</exception>
        /// <exception cref="ArgumentException">协议名称无效时抛出</exception>
        public bool RegisterProtocol(ICommunicationProtocol protocol)
        {
            if (protocol == null)
                throw new ArgumentNullException(nameof(protocol), "协议实例不能为空");

            if (string.IsNullOrWhiteSpace(protocol.ProtocolName))
                throw new ArgumentException("协议名称不能为空", nameof(protocol));

            lock (_lockObject)
            {
                try
                {
                    // 检查是否已存在同名协议
                    if (_protocols.ContainsKey(protocol.ProtocolName))
                    {
                        System.Diagnostics.Debug.WriteLine($"协议 {protocol.ProtocolName} 已存在，跳过注册");
                        return false;
                    }

                    // 验证协议的完整性
                    var validationResult = ValidateProtocol(protocol);
                    if (!validationResult.IsValid)
                    {
                        System.Diagnostics.Debug.WriteLine($"协议验证失败: {validationResult.ErrorMessage}");
                        return false;
                    }

                    // 注册协议
                    _protocols[protocol.ProtocolName] = protocol;

                    System.Diagnostics.Debug.WriteLine($"协议 {protocol.ProtocolName} 注册成功");

                    // 触发协议注册事件
                    ProtocolRegistered?.Invoke(this, new ProtocolRegisteredEventArgs(protocol));

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"注册协议 {protocol.ProtocolName} 时发生异常: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 注销指定的通讯协议
        /// </summary>
        /// <param name="protocolName">要注销的协议名称</param>
        /// <returns>注销是否成功</returns>
        public bool UnregisterProtocol(string protocolName)
        {
            if (string.IsNullOrWhiteSpace(protocolName))
                return false;

            lock (_lockObject)
            {
                if (_protocols.TryGetValue(protocolName, out ICommunicationProtocol protocol))
                {
                    _protocols.Remove(protocolName);
                    
                    System.Diagnostics.Debug.WriteLine($"协议 {protocolName} 注销成功");
                    
                    // 触发协议注销事件
                    ProtocolUnregistered?.Invoke(this, new ProtocolUnregisteredEventArgs(protocol));
                    
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 获取指定名称的协议实例
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <returns>协议实例，如果不存在则返回null</returns>
        public ICommunicationProtocol GetProtocol(string protocolName)
        {
            if (string.IsNullOrWhiteSpace(protocolName))
                return null;

            lock (_lockObject)
            {
                _protocols.TryGetValue(protocolName, out ICommunicationProtocol protocol);
                return protocol;
            }
        }

        /// <summary>
        /// 获取所有已注册的协议实例
        /// </summary>
        /// <returns>协议实例列表</returns>
        public List<ICommunicationProtocol> GetAllProtocols()
        {
            lock (_lockObject)
            {
                return _protocols.Values.ToList();
            }
        }

        /// <summary>
        /// 获取所有已注册的协议名称
        /// </summary>
        /// <returns>协议名称列表</returns>
        public List<string> GetProtocolNames()
        {
            lock (_lockObject)
            {
                return _protocols.Keys.ToList();
            }
        }

        /// <summary>
        /// 检查指定协议是否已注册
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <returns>是否已注册</returns>
        public bool IsProtocolRegistered(string protocolName)
        {
            if (string.IsNullOrWhiteSpace(protocolName))
                return false;

            lock (_lockObject)
            {
                return _protocols.ContainsKey(protocolName);
            }
        }

        #endregion

        #region 协议查找和过滤方法

        /// <summary>
        /// 根据支持的模式查找协议
        /// </summary>
        /// <param name="serverMode">是否支持服务器模式</param>
        /// <param name="clientMode">是否支持客户端模式</param>
        /// <param name="broadcast">是否支持广播</param>
        /// <param name="multicast">是否支持多播</param>
        /// <returns>匹配的协议列表</returns>
        public List<ICommunicationProtocol> FindProtocolsByMode(
            bool? serverMode = null, 
            bool? clientMode = null, 
            bool? broadcast = null, 
            bool? multicast = null)
        {
            lock (_lockObject)
            {
                var query = _protocols.Values.AsQueryable();

                if (serverMode.HasValue)
                    query = query.Where(p => p.SupportsServerMode == serverMode.Value);

                if (clientMode.HasValue)
                    query = query.Where(p => p.SupportsClientMode == clientMode.Value);

                if (broadcast.HasValue)
                    query = query.Where(p => p.SupportsBroadcast == broadcast.Value);

                if (multicast.HasValue)
                    query = query.Where(p => p.SupportsMulticast == multicast.Value);

                return query.ToList();
            }
        }

        /// <summary>
        /// 根据性能特征查找协议
        /// </summary>
        /// <param name="minLatencyLevel">最低延迟要求</param>
        /// <param name="minThroughputLevel">最低吞吐量要求</param>
        /// <param name="minReliabilityLevel">最低可靠性要求</param>
        /// <returns>匹配的协议列表</returns>
        public List<ICommunicationProtocol> FindProtocolsByPerformance(
            PerformanceLevel? minLatencyLevel = null,
            PerformanceLevel? minThroughputLevel = null,
            PerformanceLevel? minReliabilityLevel = null)
        {
            lock (_lockObject)
            {
                var protocols = new List<ICommunicationProtocol>();

                foreach (var protocol in _protocols.Values)
                {
                    try
                    {
                        var perfInfo = protocol.GetPerformanceInfo();
                        
                        bool matches = true;

                        if (minLatencyLevel.HasValue && perfInfo.LatencyLevel < minLatencyLevel.Value)
                            matches = false;

                        if (minThroughputLevel.HasValue && perfInfo.ThroughputLevel < minThroughputLevel.Value)
                            matches = false;

                        if (minReliabilityLevel.HasValue && perfInfo.ReliabilityLevel < minReliabilityLevel.Value)
                            matches = false;

                        if (matches)
                            protocols.Add(protocol);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"获取协议 {protocol.ProtocolName} 性能信息时异常: {ex.Message}");
                    }
                }

                return protocols;
            }
        }

        #endregion

        #region 实例创建和管理方法

        /// <summary>
        /// 创建指定协议的通讯实例
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="instanceName">实例名称</param>
        /// <param name="parameters">配置参数</param>
        /// <returns>创建的通讯实例</returns>
        /// <exception cref="ArgumentException">协议不存在或参数无效时抛出</exception>
        public ICommunication CreateCommunicationInstance(
            string protocolName, 
            string instanceName, 
            Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(protocolName))
                throw new ArgumentException("协议名称不能为空", nameof(protocolName));

            if (string.IsNullOrWhiteSpace(instanceName))
                throw new ArgumentException("实例名称不能为空", nameof(instanceName));

            var protocol = GetProtocol(protocolName);
            if (protocol == null)
                throw new ArgumentException($"未找到协议: {protocolName}", nameof(protocolName));

            try
            {
                // 验证参数
                var validationResult = protocol.ValidateParameters(parameters ?? new Dictionary<string, object>());
                if (!validationResult.IsValid)
                    throw new ArgumentException($"参数验证失败: {validationResult.ErrorMessage}");

                // 创建实例
                var instance = protocol.CreateInstance(instanceName, parameters ?? new Dictionary<string, object>());
                
                System.Diagnostics.Debug.WriteLine($"创建 {protocolName} 协议实例 {instanceName} 成功");
                
                return instance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建协议实例失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 根据配置对象创建通讯实例
        /// </summary>
        /// <param name="config">通讯配置对象</param>
        /// <returns>创建的通讯实例</returns>
        public ICommunication CreateCommunicationInstance(CommunicationConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "配置对象不能为空");

            return CreateCommunicationInstance(config.ProtocolType, config.Name, config.Parameters);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 验证协议的完整性和有效性
        /// </summary>
        /// <param name="protocol">要验证的协议</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateProtocol(ICommunicationProtocol protocol)
        {
            try
            {
                // 检查基本属性
                if (string.IsNullOrWhiteSpace(protocol.ProtocolName))
                    return new ValidationResult(false, "协议名称不能为空");

                if (string.IsNullOrWhiteSpace(protocol.DisplayName))
                    return new ValidationResult(false, "协议显示名称不能为空");

                // 检查参数定义
                var parameters = protocol.GetParameterDefinitions();
                if (parameters == null)
                    return new ValidationResult(false, "参数定义列表不能为null");

                // 验证参数定义的有效性
                foreach (var param in parameters)
                {
                    if (string.IsNullOrWhiteSpace(param.Key))
                        return new ValidationResult(false, "参数键名不能为空");

                    if (string.IsNullOrWhiteSpace(param.DisplayName))
                        return new ValidationResult(false, "参数显示名称不能为空");
                }

                // 检查默认配置
                var defaultConfig = protocol.GetDefaultConfig();
                if (defaultConfig == null)
                    return new ValidationResult(false, "默认配置不能为null");

                return new ValidationResult(true);
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"协议验证时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取协议的详细信息字符串
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <returns>协议信息字符串</returns>
        public string GetProtocolInfo(string protocolName)
        {
            var protocol = GetProtocol(protocolName);
            if (protocol == null)
                return $"协议 {protocolName} 未找到";

            try
            {
                var info = $"协议名称: {protocol.ProtocolName}\n";
                info += $"显示名称: {protocol.DisplayName}\n";
                info += $"版本: {protocol.Version}\n";
                info += $"描述: {protocol.Description}\n";
                info += $"支持服务器模式: {protocol.SupportsServerMode}\n";
                info += $"支持客户端模式: {protocol.SupportsClientMode}\n";
                info += $"支持广播: {protocol.SupportsBroadcast}\n";
                info += $"支持多播: {protocol.SupportsMulticast}\n";
                info += $"需要安全配置: {protocol.RequiresSecurityConfig}\n";

                var paramCount = protocol.GetParameterDefinitions()?.Count ?? 0;
                info += $"配置参数数量: {paramCount}\n";

                return info;
            }
            catch (Exception ex)
            {
                return $"获取协议信息时异常: {ex.Message}";
            }
        }

        #endregion

        #region 清理和释放

        /// <summary>
        /// 清空所有已注册的协议
        /// </summary>
        public void ClearAllProtocols()
        {
            lock (_lockObject)
            {
                var protocolNames = _protocols.Keys.ToList();
                _protocols.Clear();
                
                foreach (var protocolName in protocolNames)
                {
                    System.Diagnostics.Debug.WriteLine($"协议 {protocolName} 已清除");
                }
            }
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 协议注册事件参数
    /// </summary>
    public class ProtocolRegisteredEventArgs : EventArgs
    {
        public ICommunicationProtocol Protocol { get; }

        public ProtocolRegisteredEventArgs(ICommunicationProtocol protocol)
        {
            Protocol = protocol;
        }
    }

    /// <summary>
    /// 协议注销事件参数
    /// </summary>
    public class ProtocolUnregisteredEventArgs : EventArgs
    {
        public ICommunicationProtocol Protocol { get; }

        public ProtocolUnregisteredEventArgs(ICommunicationProtocol protocol)
        {
            Protocol = protocol;
        }
    }

    #endregion
}