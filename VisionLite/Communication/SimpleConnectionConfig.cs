// Communication/SimpleConnectionConfig.cs  
// 简化的连接配置 - 替代复杂的配置管理系统
using System;

namespace VisionLite.Communication
{
    /// <summary>
    /// 通讯协议类型枚举
    /// </summary>
    public enum CommunicationType
    {
        /// <summary>
        /// TCP客户端
        /// </summary>
        TcpClient,
        
        /// <summary>
        /// TCP服务器
        /// </summary>
        TcpServer
    }

    /// <summary>
    /// 简化的连接配置类
    /// 包含创建通讯连接所需的基本参数
    /// </summary>
    public class SimpleConnectionConfig
    {
        /// <summary>
        /// 连接名称（用户自定义）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 通讯协议类型
        /// </summary>
        public CommunicationType Type { get; set; }

        /// <summary>
        /// IP地址（TCP客户端使用）
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// 端口号
        /// TCP客户端：目标端口
        /// TCP服务器：监听端口
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// 获取协议类型的显示名称
        /// </summary>
        public string TypeDisplayName
        {
            get
            {
                return Type switch
                {
                    CommunicationType.TcpClient => "TCP客户端",
                    CommunicationType.TcpServer => "TCP服务器",
                    _ => "未知"
                };
            }
        }

        /// <summary>
        /// 获取连接描述信息
        /// </summary>
        public string Description
        {
            get
            {
                return Type switch
                {
                    CommunicationType.TcpClient => $"{IpAddress}:{Port}",
                    CommunicationType.TcpServer => $"监听端口:{Port}",
                    _ => ""
                };
            }
        }

        /// <summary>
        /// 验证配置参数是否有效
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        public (bool isValid, string errorMessage) Validate()
        {
            // 验证连接名称
            if (string.IsNullOrWhiteSpace(Name))
                return (false, "连接名称不能为空");

            // 验证端口范围
            if (Port < 1 || Port > 65535)
                return (false, "端口号必须在1-65535范围内");

            // TCP客户端需要验证IP地址
            if (Type == CommunicationType.TcpClient)
            {
                if (string.IsNullOrWhiteSpace(IpAddress))
                    return (false, "IP地址不能为空");

                if (!System.Net.IPAddress.TryParse(IpAddress, out _))
                    return (false, "IP地址格式不正确");
            }

            return (true, null);
        }

        /// <summary>
        /// 克隆配置对象
        /// </summary>
        /// <returns>克隆的配置对象</returns>
        public SimpleConnectionConfig Clone()
        {
            return new SimpleConnectionConfig
            {
                Name = this.Name,
                Type = this.Type,
                IpAddress = this.IpAddress,
                Port = this.Port
            };
        }

        /// <summary>
        /// 创建具体的通讯实例
        /// </summary>
        /// <returns>通讯实例</returns>
        public ICommunication CreateCommunication()
        {
            var validation = Validate();
            if (!validation.isValid)
                throw new ArgumentException(validation.errorMessage);

            return Type switch
            {
                CommunicationType.TcpClient => new TcpCommunication(Name, IpAddress, Port),
                CommunicationType.TcpServer => new TcpServer(Name, Port),
                _ => throw new NotSupportedException($"不支持的协议类型: {Type}")
            };
        }

        /// <summary>
        /// 获取默认的TCP客户端配置
        /// </summary>
        /// <returns>默认TCP客户端配置</returns>
        public static SimpleConnectionConfig GetDefaultTcpClient()
        {
            return new SimpleConnectionConfig
            {
                Name = "TCP客户端",
                Type = CommunicationType.TcpClient,
                IpAddress = "127.0.0.1",
                Port = 8080
            };
        }

        /// <summary>
        /// 获取默认的TCP服务器配置
        /// </summary>
        /// <returns>默认TCP服务器配置</returns>
        public static SimpleConnectionConfig GetDefaultTcpServer()
        {
            return new SimpleConnectionConfig
            {
                Name = "TCP服务器",
                Type = CommunicationType.TcpServer,
                Port = 8080
            };
        }
    }
}