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
        TcpServer,
        
        /// <summary>
        /// UDP客户端
        /// </summary>
        UdpClient,
        
        /// <summary>
        /// UDP服务器
        /// </summary>
        UdpServer,
        
        /// <summary>
        /// ModbusTCP服务器
        /// </summary>
        ModbusTcpServer,
        
        /// <summary>
        /// ModbusTCP客户端
        /// </summary>
        ModbusTcpClient
    }

    /// <summary>
    /// 32位数据字节序枚举
    /// 针对Float、Long等32位数据在两个16位寄存器中的存储方式
    /// </summary>
    public enum ByteOrder
    {
        ABCD = 0,    // 大端序（Modbus标准）：高寄存器=AB, 低寄存器=CD
        BADC = 1,    // 字节内交换：高寄存器=BA, 低寄存器=DC
        CDAB = 2,    // 寄存器交换（小端序）：高寄存器=CD, 低寄存器=AB
        DCBA = 3     // 完全反序：高寄存器=DC, 低寄存器=BA
    }

    /// <summary>
    /// ModbusTCP服务器配置类
    /// </summary>
    public class ModbusTcpConfig
    {
        /// <summary>
        /// 设备单元ID
        /// </summary>
        public byte UnitId { get; set; } = 1;

        /// <summary>
        /// 是否启用请求日志
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 最大客户端连接数
        /// </summary>
        public int MaxClients { get; set; } = 10;

        /// <summary>
        /// 数据字节序
        /// </summary>
        public ByteOrder DataByteOrder { get; set; } = ByteOrder.ABCD;
    }

    /// <summary>
    /// ModbusTCP客户端配置类
    /// </summary>
    public class ModbusTcpClientConfig
    {
        /// <summary>
        /// 服务器单元ID
        /// </summary>
        public byte UnitId { get; set; } = 1;

        /// <summary>
        /// 是否启用请求日志
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 数据字节序
        /// </summary>
        public ByteOrder DataByteOrder { get; set; } = ByteOrder.ABCD;

        /// <summary>
        /// 连接超时时间(毫秒)
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// 读取超时时间(毫秒)
        /// </summary>
        public int ReadTimeout { get; set; } = 3000;

        /// <summary>
        /// 写入超时时间(毫秒)
        /// </summary>
        public int WriteTimeout { get; set; } = 3000;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连间隔(毫秒)
        /// </summary>
        public int ReconnectInterval { get; set; } = 5000;
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
        /// ModbusTCP服务器配置
        /// </summary>
        public ModbusTcpConfig ModbusTcp { get; set; } = new ModbusTcpConfig();

        /// <summary>
        /// ModbusTCP客户端配置
        /// </summary>
        public ModbusTcpClientConfig ModbusTcpClient { get; set; } = new ModbusTcpClientConfig();

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
                    CommunicationType.UdpClient => "UDP客户端",
                    CommunicationType.UdpServer => "UDP服务器",
                    CommunicationType.ModbusTcpServer => "ModbusTCP服务器",
                    CommunicationType.ModbusTcpClient => "ModbusTCP客户端",
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
                    CommunicationType.UdpClient => $"{IpAddress}:{Port}",
                    CommunicationType.UdpServer => $"监听端口:{Port}",
                    CommunicationType.ModbusTcpServer => $"监听端口:{Port}, 单元ID:{ModbusTcp.UnitId}",
                    CommunicationType.ModbusTcpClient => $"服务器:{IpAddress}:{Port}, 单元ID:{ModbusTcpClient.UnitId}",
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

            // TCP客户端、UDP客户端和ModbusTCP客户端需要验证IP地址
            if (Type == CommunicationType.TcpClient || Type == CommunicationType.UdpClient || Type == CommunicationType.ModbusTcpClient)
            {
                if (string.IsNullOrWhiteSpace(IpAddress))
                    return (false, "IP地址不能为空");

                if (!System.Net.IPAddress.TryParse(IpAddress, out _))
                    return (false, "IP地址格式不正确");
            }

            // ModbusTCP服务器验证
            if (Type == CommunicationType.ModbusTcpServer)
            {
                if (ModbusTcp.UnitId == 0)
                    return (false, "ModbusTCP单元ID不能为0");
                
                if (ModbusTcp.MaxClients <= 0)
                    return (false, "最大客户端数必须大于0");
            }

            // ModbusTCP客户端验证
            if (Type == CommunicationType.ModbusTcpClient)
            {
                if (ModbusTcpClient.UnitId == 0)
                    return (false, "ModbusTCP单元ID不能为0");
                
                if (ModbusTcpClient.ConnectionTimeout <= 0)
                    return (false, "连接超时必须大于0毫秒");
                    
                if (ModbusTcpClient.ReadTimeout <= 0)
                    return (false, "读取超时必须大于0毫秒");
                    
                if (ModbusTcpClient.WriteTimeout <= 0)
                    return (false, "写入超时必须大于0毫秒");
                    
                if (ModbusTcpClient.ReconnectInterval <= 0)
                    return (false, "重连间隔必须大于0毫秒");
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
                Port = this.Port,
                ModbusTcp = new ModbusTcpConfig
                {
                    UnitId = this.ModbusTcp.UnitId,
                    EnableLogging = this.ModbusTcp.EnableLogging,
                    MaxClients = this.ModbusTcp.MaxClients,
                    DataByteOrder = this.ModbusTcp.DataByteOrder
                },
                ModbusTcpClient = new ModbusTcpClientConfig
                {
                    UnitId = this.ModbusTcpClient.UnitId,
                    EnableLogging = this.ModbusTcpClient.EnableLogging,
                    DataByteOrder = this.ModbusTcpClient.DataByteOrder,
                    ConnectionTimeout = this.ModbusTcpClient.ConnectionTimeout,
                    ReadTimeout = this.ModbusTcpClient.ReadTimeout,
                    WriteTimeout = this.ModbusTcpClient.WriteTimeout,
                    AutoReconnect = this.ModbusTcpClient.AutoReconnect,
                    ReconnectInterval = this.ModbusTcpClient.ReconnectInterval
                }
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
                CommunicationType.UdpClient => new UdpCommunication(Name, IpAddress, Port),
                CommunicationType.UdpServer => new UdpServer(Name, Port),
                CommunicationType.ModbusTcpServer => new ModbusTcpServer(IpAddress, Port, ModbusTcp),
                CommunicationType.ModbusTcpClient => new ModbusTcpClient(Name, IpAddress, Port, ModbusTcpClient),
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

        /// <summary>
        /// 获取默认的UDP客户端配置
        /// </summary>
        /// <returns>默认UDP客户端配置</returns>
        public static SimpleConnectionConfig GetDefaultUdpClient()
        {
            return new SimpleConnectionConfig
            {
                Name = "UDP客户端",
                Type = CommunicationType.UdpClient,
                IpAddress = "127.0.0.1",
                Port = 8081
            };
        }

        /// <summary>
        /// 获取默认的UDP服务器配置
        /// </summary>
        /// <returns>默认UDP服务器配置</returns>
        public static SimpleConnectionConfig GetDefaultUdpServer()
        {
            return new SimpleConnectionConfig
            {
                Name = "UDP服务器",
                Type = CommunicationType.UdpServer,
                Port = 8081
            };
        }

        /// <summary>
        /// 获取默认的ModbusTCP服务器配置
        /// </summary>
        /// <returns>默认ModbusTCP服务器配置</returns>
        public static SimpleConnectionConfig GetDefaultModbusTcpServer()
        {
            return new SimpleConnectionConfig
            {
                Name = "ModbusTCP服务器",
                Type = CommunicationType.ModbusTcpServer,
                IpAddress = "127.0.0.1",
                Port = 502,
                ModbusTcp = new ModbusTcpConfig
                {
                    UnitId = 1,
                    EnableLogging = true,
                    MaxClients = 10,
                    DataByteOrder = ByteOrder.ABCD
                }
            };
        }

        /// <summary>
        /// 获取默认的ModbusTCP客户端配置
        /// </summary>
        /// <returns>默认ModbusTCP客户端配置</returns>
        public static SimpleConnectionConfig GetDefaultModbusTcpClient()
        {
            return new SimpleConnectionConfig
            {
                Name = "ModbusTCP客户端",
                Type = CommunicationType.ModbusTcpClient,
                IpAddress = "192.168.1.10",
                Port = 502,
                ModbusTcpClient = new ModbusTcpClientConfig
                {
                    UnitId = 1,
                    EnableLogging = true,
                    DataByteOrder = ByteOrder.ABCD,
                    ConnectionTimeout = 5000,
                    ReadTimeout = 3000,
                    WriteTimeout = 3000,
                    AutoReconnect = true,
                    ReconnectInterval = 5000
                }
            };
        }
    }
}