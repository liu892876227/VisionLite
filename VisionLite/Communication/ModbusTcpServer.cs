using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Data;
using Modbus.Device;

namespace VisionLite.Communication
{
    /// <summary>
    /// ModbusTCP服务器实现
    /// 集成地址映射管理，支持标准Modbus TCP协议
    /// </summary>
    public class ModbusTcpServer : ICommunication, IDisposable
    {
        #region 私有字段
        
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly ModbusTcpConfig _config;
        private TcpListener _tcpListener;
        private ModbusTcpSlave _slave;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _serverTask;
        
        // Modbus数据存储区
        private DataStore _dataStore;
        
        // 地址映射管理器
        private ModbusAddressManager _addressManager;
        
        // 连接的客户端管理
        private readonly ConcurrentDictionary<string, TcpClient> _connectedClients 
            = new ConcurrentDictionary<string, TcpClient>();
        
        private volatile bool _disposed = false;
        
        #endregion

        #region 构造函数

        public ModbusTcpServer(string ipAddress, int port, ModbusTcpConfig config = null)
        {
            _ipAddress = ipAddress ?? "127.0.0.1";
            _port = port;
            _config = config ?? new ModbusTcpConfig();
            
            Status = ConnectionStatus.Disconnected;
            
            // 初始化地址映射管理器
            _addressManager = new ModbusAddressManager();
            _addressManager.MapChanged += OnAddressMapChanged;
            
            // 加载或创建地址映射
            LoadOrCreateAddressMap();
            
            // 初始化数据存储区
            InitializeDataStore();
        }

        #endregion

        #region ICommunication实现

        public string Name => $"ModbusTCP_{_ipAddress}_{_port}";
        
        public string RemoteAddress => $"{_ipAddress}:{_port} (Unit {_config.UnitId})";
        
        public ConnectionStatus Status { get; private set; }
        
        public event Action<ConnectionStatus> StatusChanged;
        
#pragma warning disable CS0067
        public event Action<Message> MessageReceived;
#pragma warning restore CS0067

        /// <summary>
        /// ModbusTCP操作日志事件
        /// </summary>
        public event Action<string> LogReceived;

        /// <summary>
        /// 异步启动Modbus TCP服务器
        /// </summary>
        public Task<bool> OpenAsync()
        {
            try
            {
                if (Status == ConnectionStatus.Connected)
                    return Task.FromResult(true);

                Status = ConnectionStatus.Connecting;
                OnStatusChanged();

                // 创建TCP监听器
                var ipAddr = IPAddress.Parse(_ipAddress);
                _tcpListener = new TcpListener(ipAddr, _port);
                
                // 创建Modbus从站
                _slave = ModbusTcpSlave.CreateTcp(_config.UnitId, _tcpListener);
                _slave.DataStore = _dataStore;
                
                // 订阅Modbus请求接收事件
                _slave.ModbusSlaveRequestReceived += OnModbusRequestReceived;
                
                // 启动服务器
                _cancellationTokenSource = new CancellationTokenSource();
                _serverTask = Task.Run(() => StartServer(_cancellationTokenSource.Token));

                Status = ConnectionStatus.Connected;
                OnStatusChanged();
                
                System.Diagnostics.Debug.WriteLine(
                    $"Modbus TCP服务器启动成功: {_ipAddress}:{_port}, 单元ID: {_config.UnitId}");
                System.Diagnostics.Debug.WriteLine(
                    $"地址映射: {_addressManager.CurrentMap.Items.Count(i => i.Enabled)} 项已配置");
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Status = ConnectionStatus.Error;
                OnStatusChanged();
                System.Diagnostics.Debug.WriteLine($"Modbus TCP服务器启动失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 异步发送消息（ModbusTCP服务器不支持主动发送）
        /// </summary>
        public Task<bool> SendAsync(Message message)
        {
            // ModbusTCP服务器通常不主动发送消息，只响应客户端请求
            System.Diagnostics.Debug.WriteLine("ModbusTCP服务器不支持主动发送消息");
            return Task.FromResult(false);
        }

        /// <summary>
        /// 关闭服务器
        /// </summary>
        public void Close()
        {
            if (Status == ConnectionStatus.Disconnected) return;
            
            // 开始断开连接过程
            // 注意：ConnectionStatus没有Disconnecting状态，直接进入断开逻辑
            
            try
            {
                // 取消服务器任务
                _cancellationTokenSource?.Cancel();
                
                // 关闭所有客户端连接
                foreach (var client in _connectedClients.Values)
                {
                    try
                    {
                        client?.Close();
                    }
                    catch { }
                }
                _connectedClients.Clear();
                
                // 停止Modbus从站
                if (_slave != null)
                {
                    // 取消事件订阅
                    _slave.ModbusSlaveRequestReceived -= OnModbusRequestReceived;
                    _slave.Dispose();
                    _slave = null;
                }
                
                // 停止TCP监听器
                _tcpListener?.Stop();
                _tcpListener = null;
                
                // 等待服务器任务完成（非阻塞）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_serverTask != null)
                            await _serverTask.ConfigureAwait(false);
                    }
                    catch { }
                });
                
                Status = ConnectionStatus.Disconnected;
                OnStatusChanged();
                
                System.Diagnostics.Debug.WriteLine("Modbus TCP服务器已关闭");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭Modbus TCP服务器时出错: {ex.Message}");
                Status = ConnectionStatus.Error;
                OnStatusChanged();
            }
        }

        /// <summary>
        /// 更新Modbus数据（通过消息格式）
        /// 消息格式: "COIL:地址:值" 或 "REGISTER:地址:值"
        /// </summary>
        public bool SendMessage(Message message)
        {
            if (Status != ConnectionStatus.Connected) return false;
            
            try
            {
                // Message类没有Content属性，使用Parameters或Command
                var content = message.GetStringParameter("data", "") ?? message.Command ?? "";
                return UpdateModbusData(content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Modbus数据失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 地址映射管理

        /// <summary>
        /// 获取地址映射管理器
        /// </summary>
        public ModbusAddressManager AddressManager => _addressManager;

        /// <summary>
        /// 加载或创建地址映射
        /// </summary>
        private void LoadOrCreateAddressMap()
        {
            var configPath = _addressManager.GetConfigFilePath($"ModbusTcp_{_port}");
            
            if (!_addressManager.LoadMap(configPath))
            {
                // 创建默认模板并保存
                _addressManager.CreateDefaultTemplate();
                _addressManager.SaveMap(configPath);
                System.Diagnostics.Debug.WriteLine($"创建默认地址映射: {configPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"加载地址映射: {configPath}");
            }

            // 验证映射表
            var errors = _addressManager.ValidateCurrentMap();
            if (errors.Any())
            {
                System.Diagnostics.Debug.WriteLine("地址映射验证发现错误:");
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {error}");
                }
            }
        }

        /// <summary>
        /// 地址映射变化事件处理
        /// </summary>
        private void OnAddressMapChanged(ModbusAddressMap map)
        {
            if (_dataStore != null)
            {
                ReconfigureDataStore(map);
            }
        }

        #endregion

        #region 数据存储区管理

        /// <summary>
        /// 初始化Modbus数据存储区
        /// </summary>
        private void InitializeDataStore()
        {
            _dataStore = DataStoreFactory.CreateDefaultDataStore();
            ReconfigureDataStore(_addressManager.CurrentMap);
        }

        /// <summary>
        /// 根据地址映射重新配置数据存储区
        /// </summary>
        private void ReconfigureDataStore(ModbusAddressMap map)
        {
            // 计算各功能区需要的最大地址
            var maxCoils = map.GetMaxAddress(ModbusFunctionArea.Coils);
            var maxDiscreteInputs = map.GetMaxAddress(ModbusFunctionArea.DiscreteInputs);
            var maxInputRegisters = map.GetMaxAddress(ModbusFunctionArea.InputRegisters);
            var maxHoldingRegisters = map.GetMaxAddress(ModbusFunctionArea.HoldingRegisters);

            // NModbus4 2.1.0版本不支持Resize，使用默认大小
            // 默认数据区大小通常为65536，足够使用
            System.Diagnostics.Debug.WriteLine(
                $"地址映射范围 - 线圈:{maxCoils}, 离散输入:{maxDiscreteInputs}, " +
                $"输入寄存器:{maxInputRegisters}, 保持寄存器:{maxHoldingRegisters}");

            // 设置默认值
            InitializeDefaultValues(map);

            System.Diagnostics.Debug.WriteLine(
                $"数据存储区配置 - 线圈:{_dataStore.CoilDiscretes.Count}, " +
                $"离散输入:{_dataStore.InputDiscretes.Count}, " +
                $"输入寄存器:{_dataStore.InputRegisters.Count}, " +
                $"保持寄存器:{_dataStore.HoldingRegisters.Count}");
        }

        /// <summary>
        /// 根据地址映射初始化默认值
        /// </summary>
        private void InitializeDefaultValues(ModbusAddressMap map)
        {
            foreach (var item in map.Items.Where(i => i.Enabled && !string.IsNullOrEmpty(i.DefaultValue)))
            {
                try
                {
                    SetDataStoreValue(item, item.DefaultValue);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置默认值失败 {item.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 设置数据存储区值
        /// </summary>
        private void SetDataStoreValue(ModbusAddressItem item, string value)
        {
            switch (item.FunctionArea)
            {
                case ModbusFunctionArea.Coils:
                    if (bool.TryParse(value, out var coilValue))
                        _dataStore.CoilDiscretes[item.Address] = coilValue;
                    break;
                    
                case ModbusFunctionArea.DiscreteInputs:
                    if (bool.TryParse(value, out var discreteValue))
                        _dataStore.InputDiscretes[item.Address] = discreteValue;
                    break;
                    
                case ModbusFunctionArea.InputRegisters:
                    SetRegisterValue(_dataStore.InputRegisters, item, value);
                    break;
                    
                case ModbusFunctionArea.HoldingRegisters:
                    SetRegisterValue(_dataStore.HoldingRegisters, item, value);
                    break;
            }
        }

        /// <summary>
        /// 设置寄存器值（支持不同数据类型）
        /// </summary>
        private void SetRegisterValue(Modbus.Data.ModbusDataCollection<ushort> registers, ModbusAddressItem item, string value)
        {
            switch (item.DataType)
            {
                case ModbusDataType.UInt16:
                    if (ushort.TryParse(value, out var uint16Value))
                        registers[item.Address] = uint16Value;
                    break;
                    
                case ModbusDataType.Int16:
                    if (short.TryParse(value, out var int16Value))
                        registers[item.Address] = (ushort)int16Value;
                    break;
                    
                case ModbusDataType.Float:
                    if (float.TryParse(value, out var floatValue))
                    {
                        var bytes = BitConverter.GetBytes(floatValue);
                        ushort reg1, reg2;
                        
                        // 对于小端序系统，bytes数组为: [B0, B1, B2, B3] = [低位字节...高位字节]
                        // Float的IEEE754格式: [B3 B2 B1 B0] (B3是符号位+指数高位)
                        
                        switch (_config.DataByteOrder)
                        {
                            case ByteOrder.ABCD: // 大端序，高寄存器AB，低寄存器CD
                                reg1 = BitConverter.ToUInt16(new[] { bytes[3], bytes[2] }, 0); // AB
                                reg2 = BitConverter.ToUInt16(new[] { bytes[1], bytes[0] }, 0); // CD
                                break;
                                
                            case ByteOrder.BADC: // 字节内交换，高寄存器BA，低寄存器DC  
                                reg1 = BitConverter.ToUInt16(new[] { bytes[2], bytes[3] }, 0); // BA
                                reg2 = BitConverter.ToUInt16(new[] { bytes[0], bytes[1] }, 0); // DC
                                break;
                                
                            case ByteOrder.CDAB: // 寄存器交换，高寄存器CD，低寄存器AB
                                reg1 = BitConverter.ToUInt16(new[] { bytes[1], bytes[0] }, 0); // CD
                                reg2 = BitConverter.ToUInt16(new[] { bytes[3], bytes[2] }, 0); // AB
                                break;
                                
                            case ByteOrder.DCBA: // 完全反序，高寄存器DC，低寄存器BA
                                reg1 = BitConverter.ToUInt16(new[] { bytes[0], bytes[1] }, 0); // DC
                                reg2 = BitConverter.ToUInt16(new[] { bytes[2], bytes[3] }, 0); // BA
                                break;
                                
                            default:
                                reg1 = BitConverter.ToUInt16(new[] { bytes[3], bytes[2] }, 0);
                                reg2 = BitConverter.ToUInt16(new[] { bytes[1], bytes[0] }, 0);
                                break;
                        }
                        
                        registers[item.Address] = reg1;
                        registers[item.Address + 1] = reg2;
                    }
                    break;
            }
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 更新Modbus数据
        /// 支持格式: "COIL:地址:值", "HOLDING:地址:值", "INPUT:地址:值", "变量名:值"
        /// </summary>
        private bool UpdateModbusData(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            var parts = content.Split(':');
            if (parts.Length < 2) return false;

            // 尝试按变量名更新
            if (parts.Length == 2)
            {
                return UpdateByVariableName(parts[0], parts[1]);
            }

            // 按功能区和地址更新
            if (parts.Length >= 3)
            {
                return UpdateByAddress(parts[0], parts[1], parts[2]);
            }

            return false;
        }

        /// <summary>
        /// 按变量名更新数据
        /// </summary>
        private bool UpdateByVariableName(string variableName, string value)
        {
            var item = _addressManager.CurrentMap.Items
                .FirstOrDefault(i => i.Enabled && i.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (item == null) return false;

            SetDataStoreValue(item, value);
            
            if (_config.EnableLogging)
            {
                System.Diagnostics.Debug.WriteLine($"Modbus数据更新: {item.Name} = {value}");
            }
            
            return true;
        }

        /// <summary>
        /// 按地址更新数据
        /// </summary>
        private bool UpdateByAddress(string areaName, string addressStr, string value)
        {
            if (!ushort.TryParse(addressStr, out var address)) return false;

            var area = areaName.ToUpper() switch
            {
                "COIL" => ModbusFunctionArea.Coils,
                "DISCRETE" => ModbusFunctionArea.DiscreteInputs,
                "INPUT" => ModbusFunctionArea.InputRegisters,
                "HOLDING" => ModbusFunctionArea.HoldingRegisters,
                _ => (ModbusFunctionArea?)null
            };

            if (!area.HasValue) return false;

            // 创建临时映射项用于设置值
            var tempItem = new ModbusAddressItem
            {
                FunctionArea = area.Value,
                Address = address,
                DataType = area.Value == ModbusFunctionArea.Coils || area.Value == ModbusFunctionArea.DiscreteInputs
                    ? ModbusDataType.Boolean
                    : ModbusDataType.UInt16
            };

            SetDataStoreValue(tempItem, value);
            
            if (_config.EnableLogging)
            {
                System.Diagnostics.Debug.WriteLine($"Modbus数据更新: {area}:{address} = {value}");
            }
            
            return true;
        }

        #endregion

        #region 服务器运行

        /// <summary>
        /// 启动服务器主循环
        /// </summary>
        private Task StartServer(CancellationToken cancellationToken)
        {
            return Task.Run(() => 
            {
                try
                {
                    // NModbus4 2.1.0版本使用Listen而不是ListenAsync
                    _slave.Listen();
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不需要处理
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modbus TCP服务器运行异常: {ex.Message}");
                    Status = ConnectionStatus.Error;
                    OnStatusChanged();
                }
            }, cancellationToken);
        }

        #endregion

        #region 事件处理

        private void OnStatusChanged()
        {
            StatusChanged?.Invoke(Status);
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        public string GetServerStatus()
        {
            var mapInfo = $"地址映射: {_addressManager.CurrentMap.Items.Count(i => i.Enabled)} 项";
            var clientInfo = $"连接客户端: {_connectedClients.Count}";
            return $"{mapInfo}, {clientInfo}";
        }

        /// <summary>
        /// 按变量名读取值
        /// </summary>
        public object ReadValueByName(string variableName)
        {
            var item = _addressManager.CurrentMap.Items
                .FirstOrDefault(i => i.Enabled && i.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (item == null) return null;

            return ReadDataStoreValue(item);
        }

        /// <summary>
        /// 读取数据存储区值
        /// </summary>
        private object ReadDataStoreValue(ModbusAddressItem item)
        {
            try
            {
                return item.FunctionArea switch
                {
                    ModbusFunctionArea.Coils => _dataStore.CoilDiscretes[item.Address],
                    ModbusFunctionArea.DiscreteInputs => _dataStore.InputDiscretes[item.Address],
                    ModbusFunctionArea.InputRegisters => ReadRegisterValue(_dataStore.InputRegisters, item),
                    ModbusFunctionArea.HoldingRegisters => ReadRegisterValue(_dataStore.HoldingRegisters, item),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 读取寄存器值
        /// </summary>
        private object ReadRegisterValue(Modbus.Data.ModbusDataCollection<ushort> registers, ModbusAddressItem item)
        {
            return item.DataType switch
            {
                ModbusDataType.UInt16 => registers[item.Address],
                ModbusDataType.Int16 => (short)registers[item.Address],
                ModbusDataType.Float => ReadFloatFromRegisters(registers, item.Address),
                _ => registers[item.Address]
            };
        }

        /// <summary>
        /// 从寄存器读取浮点数
        /// </summary>
        private float ReadFloatFromRegisters(Modbus.Data.ModbusDataCollection<ushort> registers, ushort address)
        {
            var reg1 = registers[address];     // 高地址寄存器
            var reg2 = registers[address + 1]; // 低地址寄存器
            
            byte[] bytes = new byte[4];
            
            // 根据字节序重新组合字节数组
            switch (_config.DataByteOrder)
            {
                case ByteOrder.ABCD: // 大端序：reg1=AB, reg2=CD
                    bytes[0] = (byte)(reg2 & 0xFF);       // D -> bytes[0] 
                    bytes[1] = (byte)(reg2 >> 8);         // C -> bytes[1]
                    bytes[2] = (byte)(reg1 & 0xFF);       // B -> bytes[2]
                    bytes[3] = (byte)(reg1 >> 8);         // A -> bytes[3]
                    break;
                    
                case ByteOrder.BADC: // 字节内交换：reg1=BA, reg2=DC
                    bytes[0] = (byte)(reg2 >> 8);         // C -> bytes[0]
                    bytes[1] = (byte)(reg2 & 0xFF);       // D -> bytes[1] 
                    bytes[2] = (byte)(reg1 >> 8);         // A -> bytes[2]
                    bytes[3] = (byte)(reg1 & 0xFF);       // B -> bytes[3]
                    break;
                    
                case ByteOrder.CDAB: // 寄存器交换：reg1=CD, reg2=AB
                    bytes[0] = (byte)(reg2 & 0xFF);       // B -> bytes[0]
                    bytes[1] = (byte)(reg2 >> 8);         // A -> bytes[1]
                    bytes[2] = (byte)(reg1 & 0xFF);       // D -> bytes[2]
                    bytes[3] = (byte)(reg1 >> 8);         // C -> bytes[3]
                    break;
                    
                case ByteOrder.DCBA: // 完全反序：reg1=DC, reg2=BA
                    bytes[0] = (byte)(reg2 >> 8);         // A -> bytes[0]
                    bytes[1] = (byte)(reg2 & 0xFF);       // B -> bytes[1]
                    bytes[2] = (byte)(reg1 >> 8);         // C -> bytes[2] 
                    bytes[3] = (byte)(reg1 & 0xFF);       // D -> bytes[3]
                    break;
                    
                default: // 默认ABCD
                    bytes[0] = (byte)(reg2 & 0xFF);
                    bytes[1] = (byte)(reg2 >> 8);
                    bytes[2] = (byte)(reg1 & 0xFF);
                    bytes[3] = (byte)(reg1 >> 8);
                    break;
            }
            
            return BitConverter.ToSingle(bytes, 0);
        }

        #endregion

        #region Modbus 事件处理
        
        /// <summary>
        /// 处理Modbus请求接收事件
        /// </summary>
        private void OnModbusRequestReceived(object sender, Modbus.Device.ModbusSlaveRequestEventArgs e)
        {
            LogModbusOperation(e.Message);
        }
        
        /// <summary>
        /// 记录Modbus操作日志
        /// </summary>
        private void LogModbusOperation(Modbus.Message.IModbusMessage message)
        {
            try
            {
                // 解析功能码和操作类型
                string operation = GetOperationDescription(message.FunctionCode);
                
                // 构建日志消息
                string logMessage = $"[客户端操作] {operation} | " +
                                   $"从站地址: {message.SlaveAddress} | " +
                                   $"功能码: 0x{message.FunctionCode:X2} | " +
                                   $"事务ID: {message.TransactionId} | " +
                                   $"报文: {BitConverter.ToString(message.MessageFrame)}";
                
                // 触发日志事件，让UI显示
                LogReceived?.Invoke(logMessage);
                
                // 保留调试输出用于开发调试
                System.Diagnostics.Debug.WriteLine($"[ModbusTCP操作] {logMessage}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"[错误] 记录操作日志时出错: {ex.Message}";
                LogReceived?.Invoke(errorMessage);
                System.Diagnostics.Debug.WriteLine($"[ModbusTCP] {errorMessage}");
            }
        }
        
        /// <summary>
        /// 根据功能码获取操作描述
        /// </summary>
        private string GetOperationDescription(byte functionCode)
        {
            return functionCode switch
            {
                0x01 => "读取线圈",
                0x02 => "读取离散输入",
                0x03 => "读取保持寄存器",
                0x04 => "读取输入寄存器",
                0x05 => "写单个线圈",
                0x06 => "写单个寄存器",
                0x0F => "写多个线圈",
                0x10 => "写多个寄存器",
                0x17 => "读写多个寄存器",
                _ => $"未知功能(0x{functionCode:X2})"
            };
        }
        
        #endregion

        #region IDisposable实现

        public void Dispose()
        {
            if (_disposed) return;
            
            Close();
            
            
            _cancellationTokenSource?.Dispose();
            _dataStore = null;
            _addressManager = null;
            
            _disposed = true;
        }

        #endregion
    }

}