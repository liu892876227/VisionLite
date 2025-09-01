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
        ModbusTcpClient,
        
        /// <summary>
        /// 串口通讯
        /// </summary>
        SerialPort,
        
        /// <summary>
        /// 倍福ADS通讯
        /// </summary>
        AdsClient
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
    /// 串口数据格式枚举
    /// </summary>
    public enum SerialDataFormat
    {
        Text = 0,       // 文本格式（默认）
        Hex = 1,        // 十六进制格式
        Binary = 2      // 二进制格式
    }

    /// <summary>
    /// 串口通讯配置类
    /// </summary>
    public class SerialConfig
    {
        /// <summary>
        /// 串口名称
        /// </summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 数据位
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 停止位
        /// </summary>
        public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;

        /// <summary>
        /// 奇偶校验
        /// </summary>
        public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;

        /// <summary>
        /// 流控制
        /// </summary>
        public System.IO.Ports.Handshake Handshake { get; set; } = System.IO.Ports.Handshake.None;

        /// <summary>
        /// 读取超时时间(毫秒)
        /// </summary>
        public int ReadTimeout { get; set; } = 3000;

        /// <summary>
        /// 写入超时时间(毫秒)
        /// </summary>
        public int WriteTimeout { get; set; } = 3000;

        /// <summary>
        /// 数据格式
        /// </summary>
        public SerialDataFormat DataFormat { get; set; } = SerialDataFormat.Text;

        /// <summary>
        /// 消息结束符
        /// </summary>
        public string MessageTerminator { get; set; } = "\r\n";

        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReadBufferSize { get; set; } = 4096;

        /// <summary>
        /// 发送缓冲区大小
        /// </summary>
        public int WriteBufferSize { get; set; } = 2048;

        /// <summary>
        /// 是否启用日志
        /// </summary>
        public bool EnableLogging { get; set; } = true;

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
        /// 串口通讯配置
        /// </summary>
        public SerialConfig Serial { get; set; } = new SerialConfig();

        /// <summary>
        /// ADS通讯配置
        /// </summary>
        public AdsConnectionConfig Ads { get; set; } = new AdsConnectionConfig();

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
                    CommunicationType.SerialPort => "串口通讯",
                    CommunicationType.AdsClient => "倍福ADS",
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
                    CommunicationType.SerialPort => $"串口:{Serial.PortName}, 波特率:{Serial.BaudRate}",
                    CommunicationType.AdsClient => $"NetId:{Ads.TargetAmsNetId}, 端口:{Ads.TargetAmsPort}",
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

            // 串口通讯验证
            if (Type == CommunicationType.SerialPort)
            {
                if (string.IsNullOrWhiteSpace(Serial.PortName))
                    return (false, "串口名称不能为空");

                if (Serial.BaudRate <= 0)
                    return (false, "波特率必须大于0");

                if (Serial.DataBits < 5 || Serial.DataBits > 8)
                    return (false, "数据位必须在5-8范围内");

                if (Serial.ReadTimeout <= 0)
                    return (false, "读取超时必须大于0毫秒");

                if (Serial.WriteTimeout <= 0)
                    return (false, "写入超时必须大于0毫秒");

                if (Serial.ReconnectInterval <= 0)
                    return (false, "重连间隔必须大于0毫秒");
            }

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

            // ADS通讯验证
            if (Type == CommunicationType.AdsClient)
            {
                if (!Ads.IsValid(out string adsError))
                    return (false, $"ADS配置无效: {adsError}");
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
                },
                Serial = new SerialConfig
                {
                    PortName = this.Serial.PortName,
                    BaudRate = this.Serial.BaudRate,
                    DataBits = this.Serial.DataBits,
                    StopBits = this.Serial.StopBits,
                    Parity = this.Serial.Parity,
                    Handshake = this.Serial.Handshake,
                    ReadTimeout = this.Serial.ReadTimeout,
                    WriteTimeout = this.Serial.WriteTimeout,
                    DataFormat = this.Serial.DataFormat,
                    MessageTerminator = this.Serial.MessageTerminator,
                    ReadBufferSize = this.Serial.ReadBufferSize,
                    WriteBufferSize = this.Serial.WriteBufferSize,
                    EnableLogging = this.Serial.EnableLogging,
                    AutoReconnect = this.Serial.AutoReconnect,
                    ReconnectInterval = this.Serial.ReconnectInterval
                },
                Ads = new AdsConnectionConfig
                {
                    TargetAmsNetId = this.Ads.TargetAmsNetId,
                    TargetAmsPort = this.Ads.TargetAmsPort,
                    Timeout = this.Ads.Timeout,
                    UseSymbolicAccess = this.Ads.UseSymbolicAccess,
                    DisplayName = this.Ads.DisplayName
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
                CommunicationType.SerialPort => new SerialCommunication(Name, Serial),
                CommunicationType.AdsClient => new AdsCommunication(Ads),
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

        /// <summary>
        /// 获取默认的串口通讯配置
        /// </summary>
        /// <returns>默认串口通讯配置</returns>
        public static SimpleConnectionConfig GetDefaultSerialPort()
        {
            return new SimpleConnectionConfig
            {
                Name = "串口通讯",
                Type = CommunicationType.SerialPort,
                Serial = new SerialConfig
                {
                    PortName = "COM1",
                    BaudRate = 9600,
                    DataBits = 8,
                    StopBits = System.IO.Ports.StopBits.One,
                    Parity = System.IO.Ports.Parity.None,
                    Handshake = System.IO.Ports.Handshake.None,
                    ReadTimeout = 3000,
                    WriteTimeout = 3000,
                    DataFormat = SerialDataFormat.Text,
                    MessageTerminator = "\r\n",
                    ReadBufferSize = 4096,
                    WriteBufferSize = 2048,
                    EnableLogging = true,
                    AutoReconnect = true,
                    ReconnectInterval = 5000
                }
            };
        }

        /// <summary>
        /// 获取默认的倍福ADS通讯配置
        /// </summary>
        /// <returns>默认ADS通讯配置</returns>
        public static SimpleConnectionConfig GetDefaultAdsClient()
        {
            return new SimpleConnectionConfig
            {
                Name = "倍福PLC",
                Type = CommunicationType.AdsClient,
                Ads = AdsConnectionConfig.CreateDefault()
            };
        }
    }
}