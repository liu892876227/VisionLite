// Communication/ModbusTcpClient.cs
// ModbusTCP客户端实现 - 主动连接PLC服务器进行数据交换
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Modbus.Data;
using Modbus.Device;

namespace VisionLite.Communication
{
    /// <summary>
    /// ModbusTCP客户端
    /// 作为客户端主动连接PLC服务器，支持读写操作和轮询功能
    /// </summary>
    public class ModbusTcpClient : ICommunication, IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 客户端名称
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// 服务器IP地址
        /// </summary>
        private readonly string _serverIp;

        /// <summary>
        /// 服务器端口
        /// </summary>
        private readonly int _serverPort;

        /// <summary>
        /// 客户端配置
        /// </summary>
        private readonly ModbusTcpClientConfig _config;

        /// <summary>
        /// TCP客户端
        /// </summary>
        private TcpClient _tcpClient;

        /// <summary>
        /// Modbus主站
        /// </summary>
        private ModbusIpMaster _master;

        /// <summary>
        /// 是否已连接
        /// </summary>
        private volatile bool _isConnected = false;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        private volatile bool _isRunning = false;

        /// <summary>
        /// 重连定时器
        /// </summary>
        private Timer _reconnectTimer;

        /// <summary>
        /// 轮询定时器
        /// </summary>
        private Timer _pollingTimer;

        /// <summary>
        /// 连接锁
        /// </summary>
        private readonly object _connectionLock = new object();

        /// <summary>
        /// 操作锁（保证读写操作线程安全）
        /// </summary>
        private readonly object _operationLock = new object();

        /// <summary>
        /// 连接状态变化时间
        /// </summary>
        private DateTime _lastConnectionTime = DateTime.MinValue;

        /// <summary>
        /// 成功读取计数
        /// </summary>
        private int _successfulReads = 0;

        /// <summary>
        /// 读取失败计数
        /// </summary>
        private int _failedReads = 0;

        /// <summary>
        /// 成功写入计数
        /// </summary>
        private int _successfulWrites = 0;

        /// <summary>
        /// 写入失败计数
        /// </summary>
        private int _failedWrites = 0;

        #endregion

        #region ICommunication接口属性

        /// <summary>
        /// 客户端名称
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionStatus Status 
        { 
            get
            {
                if (_isConnected) return ConnectionStatus.Connected;
                if (_isRunning && !_isConnected) return ConnectionStatus.Connecting;
                return ConnectionStatus.Disconnected;
            }
        }

        #endregion

        #region 扩展属性

        /// <summary>
        /// 是否已连接到服务器
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 服务器地址信息
        /// </summary>
        public string ServerInfo => $"{_serverIp}:{_serverPort}";

        /// <summary>
        /// 成功读取次数
        /// </summary>
        public int SuccessfulReads => _successfulReads;

        /// <summary>
        /// 失败读取次数
        /// </summary>
        public int FailedReads => _failedReads;

        /// <summary>
        /// 成功写入次数
        /// </summary>
        public int SuccessfulWrites => _successfulWrites;

        /// <summary>
        /// 失败写入次数
        /// </summary>
        public int FailedWrites => _failedWrites;

        /// <summary>
        /// 运行时间
        /// </summary>
        public TimeSpan Uptime => _lastConnectionTime != DateTime.MinValue ? DateTime.Now - _lastConnectionTime : TimeSpan.Zero;

        #endregion

        #region ICommunication接口事件

        /// <summary>
        /// 连接状态变化事件（ICommunication接口要求）
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;

        /// <summary>
        /// 消息接收事件（ICommunication接口要求）
        /// </summary>
        public event Action<Message> MessageReceived;

        #endregion

        #region 扩展事件

        /// <summary>
        /// 连接状态变化事件（扩展版本，包含更多信息）
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// 数据读取事件
        /// </summary>
        public event EventHandler<DataReadEventArgs> DataRead;

        /// <summary>
        /// 数据写入事件
        /// </summary>
        public event EventHandler<DataWriteEventArgs> DataWritten;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event EventHandler<ErrorEventArgs> Error;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">客户端名称</param>
        /// <param name="serverIp">服务器IP地址</param>
        /// <param name="serverPort">服务器端口</param>
        /// <param name="config">客户端配置</param>
        public ModbusTcpClient(string name, string serverIp, int serverPort, ModbusTcpClientConfig config)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _serverIp = serverIp ?? throw new ArgumentNullException(nameof(serverIp));
            _serverPort = serverPort;
            _config = config ?? throw new ArgumentNullException(nameof(config));

            LogMessage($"ModbusTCP客户端创建: {_name} -> {_serverIp}:{_serverPort}, 单元ID: {_config.UnitId}");
        }

        #endregion

        #region ICommunication接口实现

        /// <summary>
        /// 异步打开连接（ICommunication接口要求）
        /// </summary>
        public async Task<bool> OpenAsync()
        {
            return await Task.Run(() => Start());
        }

        /// <summary>
        /// 关闭连接（ICommunication接口要求）
        /// </summary>
        public void Close()
        {
            Stop();
        }

        /// <summary>
        /// 异步发送消息（ICommunication接口要求）
        /// </summary>
        public async Task<bool> SendAsync(Message message)
        {
            return await Task.Run(() => SendMessage(message));
        }

        #endregion

        #region 扩展方法

        /// <summary>
        /// 启动客户端
        /// </summary>
        /// <returns>启动是否成功</returns>
        public bool Start()
        {
            try
            {
                if (_isRunning)
                {
                    LogMessage("ModbusTCP客户端已经在运行中");
                    return true;
                }

                _isRunning = true;
                LogMessage($"启动ModbusTCP客户端: {_name}");

                // 启动连接任务
                Task.Run(ConnectAsync);

                // 如果启用轮询，启动轮询定时器
                if (_config.EnablePolling)
                {
                    StartPolling();
                }

                LogMessage("ModbusTCP客户端启动成功");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"启动ModbusTCP客户端失败: {ex.Message}");
                _isRunning = false;
                OnError(new ErrorEventArgs($"启动失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 停止客户端
        /// </summary>
        public void Stop()
        {
            try
            {
                _isRunning = false;
                LogMessage("停止ModbusTCP客户端");

                // 停止轮询
                StopPolling();

                // 停止重连定时器
                StopReconnectTimer();

                // 断开连接
                Disconnect();

                LogMessage("ModbusTCP客户端已停止");
            }
            catch (Exception ex)
            {
                LogMessage($"停止ModbusTCP客户端时出错: {ex.Message}");
                OnError(new ErrorEventArgs($"停止失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 发送消息
        /// 支持格式：
        /// "READ_COIL:地址:数量" - 读取线圈
        /// "READ_HOLDING:地址:数量" - 读取保持寄存器
        /// "READ_INPUT:地址:数量" - 读取输入寄存器
        /// "READ_DISCRETE:地址:数量" - 读取离散输入
        /// "WRITE_COIL:地址:值" - 写入单个线圈
        /// "WRITE_REGISTER:地址:值" - 写入单个寄存器
        /// "WRITE_FLOAT:地址:值" - 写入浮点数（占用2个寄存器）
        /// </summary>
        public bool SendMessage(Message message)
        {
            if (message == null || string.IsNullOrEmpty(message.Command))
            {
                LogMessage("消息为空或命令为空");
                return false;
            }

            try
            {
                // 首先按空格和冒号分隔，保留逗号分隔的值部分
                var parts = message.Command.Split(new char[] { ':', ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    LogMessage($"无效的消息格式: {message.Command}，正确格式如: WRITE_COIL 200 1 或 READ_HOLDING:100:10");
                    return false;
                }

                var command = parts[0].ToUpper();
                
                return command switch
                {
                    "READ_COIL" => HandleReadCoil(parts),
                    "READ_HOLDING" => HandleReadHolding(parts),
                    "READ_INPUT" => HandleReadInput(parts),
                    "READ_DISCRETE" => HandleReadDiscrete(parts),
                    "WRITE_COIL" => HandleWriteCoil(parts),
                    "WRITE_REGISTER" => HandleWriteRegister(parts),
                    "WRITE_FLOAT" => HandleWriteFloat(parts),
                    "CONNECT" => ConnectAsync().Result,
                    "DISCONNECT" => HandleDisconnect(),
                    _ => HandleCustomCommand(message.Command)
                };
            }
            catch (Exception ex)
            {
                LogMessage($"处理消息失败: {ex.Message}");
                OnError(new ErrorEventArgs($"消息处理失败: {ex.Message}"));
                return false;
            }
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 异步连接到服务器
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (_isConnected)
            {
                LogMessage("已经连接到服务器");
                return true;
            }

            try
            {
                LogMessage($"正在连接ModbusTCP服务器: {_serverIp}:{_serverPort}");

                // 创建TCP客户端
                _tcpClient = new TcpClient();
                
                // 设置超时
                _tcpClient.ReceiveTimeout = _config.ReadTimeout;
                _tcpClient.SendTimeout = _config.WriteTimeout;

                // 连接服务器
                await _tcpClient.ConnectAsync(_serverIp, _serverPort).ConfigureAwait(false);
                
                if (_tcpClient.Connected)
                {
                    lock (_connectionLock)
                    {
                        // 创建Modbus主站
                        _master = ModbusIpMaster.CreateIp(_tcpClient);
                        _master.Transport.ReadTimeout = _config.ReadTimeout;
                        _master.Transport.WriteTimeout = _config.WriteTimeout;

                        _isConnected = true;
                        _lastConnectionTime = DateTime.Now;
                    }

                    LogMessage($"成功连接到ModbusTCP服务器: {_serverIp}:{_serverPort}");
                    OnConnectionStatusChanged(true, "连接成功");

                    // 停止重连定时器
                    StopReconnectTimer();
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"连接ModbusTCP服务器失败: {ex.Message}");
                OnError(new ErrorEventArgs($"连接失败: {ex.Message}"));
            }

            // 连接失败，清理资源
            CleanupConnection();
            
            // 如果启用自动重连，启动重连定时器
            if (_config.AutoReconnect && _isRunning)
            {
                StartReconnectTimer();
            }

            return false;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            lock (_connectionLock)
            {
                try
                {
                    if (_isConnected)
                    {
                        LogMessage("断开ModbusTCP服务器连接");
                        _isConnected = false;
                        OnConnectionStatusChanged(false, "主动断开");
                    }

                    CleanupConnection();
                }
                catch (Exception ex)
                {
                    LogMessage($"断开连接时出错: {ex.Message}");
                    OnError(new ErrorEventArgs($"断开连接失败: {ex.Message}"));
                }
            }
        }

        /// <summary>
        /// 清理连接资源
        /// </summary>
        private void CleanupConnection()
        {
            try
            {
                _master?.Dispose();
                _master = null;

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
            catch (Exception ex)
            {
                LogMessage($"清理连接资源时出错: {ex.Message}");
            }
        }

        #endregion

        #region 重连管理

        /// <summary>
        /// 启动重连定时器
        /// </summary>
        private void StartReconnectTimer()
        {
            if (!_config.AutoReconnect || _isConnected)
                return;

            StopReconnectTimer();

            LogMessage($"启动自动重连，间隔: {_config.ReconnectInterval}ms");
            _reconnectTimer = new Timer(ReconnectCallback, null, _config.ReconnectInterval, _config.ReconnectInterval);
        }

        /// <summary>
        /// 停止重连定时器
        /// </summary>
        private void StopReconnectTimer()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        /// <summary>
        /// 重连回调
        /// </summary>
        private async void ReconnectCallback(object state)
        {
            if (!_config.AutoReconnect || !_isRunning || _isConnected)
            {
                StopReconnectTimer();
                return;
            }

            LogMessage("尝试自动重连...");
            await ConnectAsync();
        }

        #endregion

        #region 轮询功能

        /// <summary>
        /// 启动轮询
        /// </summary>
        private void StartPolling()
        {
            if (!_config.EnablePolling)
                return;

            StopPolling();

            LogMessage($"启动数据轮询，间隔: {_config.PollingInterval}ms");
            _pollingTimer = new Timer(PollingCallback, null, _config.PollingInterval, _config.PollingInterval);
        }

        /// <summary>
        /// 停止轮询
        /// </summary>
        private void StopPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        /// <summary>
        /// 轮询回调
        /// </summary>
        private void PollingCallback(object state)
        {
            if (!_config.EnablePolling || !_isRunning || !_isConnected)
                return;

            try
            {
                // 这里可以实现定期读取特定地址的逻辑
                // 目前作为示例，读取一个测试地址
                // var result = ReadHoldingRegisters(0, 1);
                // LogMessage($"轮询读取结果: {result?[0]}");
            }
            catch (Exception ex)
            {
                LogMessage($"轮询时出错: {ex.Message}");
            }
        }

        #endregion

        #region 日志和事件

        /// <summary>
        /// 记录日志
        /// </summary>
        private void LogMessage(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            // 输出到调试控制台
            if (_config.EnableLogging)
            {
                System.Diagnostics.Debug.WriteLine(logMessage);
            }
            
            // 通过ICommunication接口的MessageReceived事件发送给UI界面
            try
            {
                var logMsg = new Message
                {
                    Command = "LOG",
                    Type = MessageType.Event,
                    Timestamp = DateTime.Now
                };
                logMsg.Parameters["content"] = logMessage;
                logMsg.Parameters["source"] = _name;
                
                MessageReceived?.Invoke(logMsg);
            }
            catch (Exception ex)
            {
                // 避免日志事件异常影响主业务逻辑
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] 日志事件异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发连接状态变化事件
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected, string reason)
        {
            // 触发ICommunication接口事件
            StatusChanged?.Invoke(Status);

            // 触发扩展事件
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                IsConnected = isConnected,
                Reason = reason,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        private void OnError(ErrorEventArgs args)
        {
            Error?.Invoke(this, args);
        }

        /// <summary>
        /// 触发数据读取事件
        /// </summary>
        private void OnDataRead(DataReadEventArgs args)
        {
            DataRead?.Invoke(this, args);
        }

        /// <summary>
        /// 触发数据写入事件
        /// </summary>
        private void OnDataWritten(DataWriteEventArgs args)
        {
            DataWritten?.Invoke(this, args);
        }

        #endregion

        #region Modbus数据读写操作

        /// <summary>
        /// 处理读取线圈命令
        /// 命令格式：READ_COIL <地址> <数量>
        /// </summary>
        private bool HandleReadCoil(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("读取线圈命令格式错误，正确格式：READ_COIL <地址> <数量>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("读取线圈失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort startAddress))
                {
                    LogMessage($"读取线圈失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                if (!ushort.TryParse(parts[2], out ushort quantity))
                {
                    LogMessage($"读取线圈失败：数量'{parts[2]}'格式错误");
                    return false;
                }

                if (quantity == 0 || quantity > 2000) // Modbus协议限制
                {
                    LogMessage($"读取线圈失败：数量{quantity}超出范围(1-2000)");
                    return false;
                }

                bool[] coils;
                lock (_operationLock)
                {
                    coils = _master.ReadCoils(_config.UnitId, startAddress, quantity);
                }

                // 触发数据读取事件
                OnDataRead(new DataReadEventArgs
                {
                    Address = $"Coil_{startAddress}",
                    Value = coils,
                    Timestamp = DateTime.Now
                });

                LogMessage($"成功读取线圈：地址{startAddress}, 数量{quantity}, 结果: [{string.Join(", ", coils.Select(c => c ? "1" : "0"))}]");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取线圈异常：{ex.Message}");
                OnError(new ErrorEventArgs($"读取线圈失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理读取保持寄存器命令
        /// 命令格式：READ_HOLDING <地址> <数量>
        /// </summary>
        private bool HandleReadHolding(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("读取保持寄存器命令格式错误，正确格式：READ_HOLDING <地址> <数量>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("读取保持寄存器失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort startAddress))
                {
                    LogMessage($"读取保持寄存器失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                if (!ushort.TryParse(parts[2], out ushort quantity))
                {
                    LogMessage($"读取保持寄存器失败：数量'{parts[2]}'格式错误");
                    return false;
                }

                if (quantity == 0 || quantity > 125) // Modbus协议限制
                {
                    LogMessage($"读取保持寄存器失败：数量{quantity}超出范围(1-125)");
                    return false;
                }

                ushort[] registers;
                lock (_operationLock)
                {
                    registers = _master.ReadHoldingRegisters(_config.UnitId, startAddress, quantity);
                }

                // 触发数据读取事件
                OnDataRead(new DataReadEventArgs
                {
                    Address = $"HoldingReg_{startAddress}",
                    Value = registers,
                    Timestamp = DateTime.Now
                });

                LogMessage($"成功读取保持寄存器：地址{startAddress}, 数量{quantity}, 结果: [{string.Join(", ", registers)}]");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取保持寄存器异常：{ex.Message}");
                OnError(new ErrorEventArgs($"读取保持寄存器失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理读取输入寄存器命令
        /// 命令格式：READ_INPUT <地址> <数量>
        /// </summary>
        private bool HandleReadInput(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("读取输入寄存器命令格式错误，正确格式：READ_INPUT <地址> <数量>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("读取输入寄存器失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort startAddress))
                {
                    LogMessage($"读取输入寄存器失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                if (!ushort.TryParse(parts[2], out ushort quantity))
                {
                    LogMessage($"读取输入寄存器失败：数量'{parts[2]}'格式错误");
                    return false;
                }

                if (quantity == 0 || quantity > 125) // Modbus协议限制
                {
                    LogMessage($"读取输入寄存器失败：数量{quantity}超出范围(1-125)");
                    return false;
                }

                ushort[] registers;
                lock (_operationLock)
                {
                    registers = _master.ReadInputRegisters(_config.UnitId, startAddress, quantity);
                }

                // 触发数据读取事件
                OnDataRead(new DataReadEventArgs
                {
                    Address = $"InputReg_{startAddress}",
                    Value = registers,
                    Timestamp = DateTime.Now
                });

                LogMessage($"成功读取输入寄存器：地址{startAddress}, 数量{quantity}, 结果: [{string.Join(", ", registers)}]");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取输入寄存器异常：{ex.Message}");
                OnError(new ErrorEventArgs($"读取输入寄存器失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理读取离散输入命令
        /// 命令格式：READ_DISCRETE <地址> <数量>
        /// </summary>
        private bool HandleReadDiscrete(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("读取离散输入命令格式错误，正确格式：READ_DISCRETE <地址> <数量>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("读取离散输入失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort startAddress))
                {
                    LogMessage($"读取离散输入失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                if (!ushort.TryParse(parts[2], out ushort quantity))
                {
                    LogMessage($"读取离散输入失败：数量'{parts[2]}'格式错误");
                    return false;
                }

                if (quantity == 0 || quantity > 2000) // Modbus协议限制
                {
                    LogMessage($"读取离散输入失败：数量{quantity}超出范围(1-2000)");
                    return false;
                }

                bool[] inputs;
                lock (_operationLock)
                {
                    inputs = _master.ReadInputs(_config.UnitId, startAddress, quantity);
                }

                // 触发数据读取事件
                OnDataRead(new DataReadEventArgs
                {
                    Address = $"DiscreteInput_{startAddress}",
                    Value = inputs,
                    Timestamp = DateTime.Now
                });

                LogMessage($"成功读取离散输入：地址{startAddress}, 数量{quantity}, 结果: [{string.Join(", ", inputs.Select(i => i ? "1" : "0"))}]");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取离散输入异常：{ex.Message}");
                OnError(new ErrorEventArgs($"读取离散输入失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理写入线圈命令
        /// 命令格式：WRITE_COIL <地址> <值>（单个）或 WRITE_COILS <地址> <值1,值2,...>（多个）
        /// </summary>
        private bool HandleWriteCoil(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("写入线圈命令格式错误，正确格式：WRITE_COIL <地址> <值>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("写入线圈失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort address))
                {
                    LogMessage($"写入线圈失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                // 检查是单个还是多个值（通过检查第三个参数是否包含逗号）
                bool isMultipleValues = parts.Length > 2 && parts[2].Contains(',');
                
                if (!isMultipleValues)
                {
                    // 单个线圈
                    if (!bool.TryParse(parts[2], out bool value))
                    {
                        // 尝试解析为数字
                        if (int.TryParse(parts[2], out int intValue))
                        {
                            value = intValue != 0;
                        }
                        else
                        {
                            LogMessage($"写入线圈失败：值'{parts[2]}'格式错误，请使用true/false或0/1");
                            return false;
                        }
                    }

                    lock (_operationLock)
                    {
                        _master.WriteSingleCoil(_config.UnitId, address, value);
                    }

                    // 触发数据写入事件
                    OnDataWritten(new DataWriteEventArgs
                    {
                        Address = $"Coil_{address}",
                        Value = value,
                        Success = true,
                        Timestamp = DateTime.Now
                    });

                    LogMessage($"成功写入线圈：地址{address}, 值: {(value ? "1" : "0")}");
                    return true;
                }
                else
                {
                    // 多个线圈，值用逗号分隔
                    var valueStrings = parts[2].Split(',');
                    var values = new bool[valueStrings.Length];

                    for (int i = 0; i < valueStrings.Length; i++)
                    {
                        if (!bool.TryParse(valueStrings[i].Trim(), out values[i]))
                        {
                            if (int.TryParse(valueStrings[i].Trim(), out int intValue))
                            {
                                values[i] = intValue != 0;
                            }
                            else
                            {
                                LogMessage($"写入线圈失败：值'{valueStrings[i]}'格式错误");
                                return false;
                            }
                        }
                    }

                    lock (_operationLock)
                    {
                        _master.WriteMultipleCoils(_config.UnitId, address, values);
                    }

                    // 触发数据写入事件
                    OnDataWritten(new DataWriteEventArgs
                    {
                        Address = $"Coils_{address}",
                        Value = values,
                        Success = true,
                        Timestamp = DateTime.Now
                    });

                    LogMessage($"成功写入多个线圈：地址{address}, 数量{values.Length}, 值: [{string.Join(", ", values.Select(v => v ? "1" : "0"))}]");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"写入线圈异常：{ex.Message}");
                OnError(new ErrorEventArgs($"写入线圈失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理写入寄存器命令
        /// 命令格式：WRITE_REGISTER <地址> <值>（单个）或 WRITE_REGISTERS <地址> <值1,值2,...>（多个）
        /// </summary>
        private bool HandleWriteRegister(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("写入寄存器命令格式错误，正确格式：WRITE_REGISTER <地址> <值>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("写入寄存器失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort address))
                {
                    LogMessage($"写入寄存器失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                // 检查是单个还是多个值（通过检查第三个参数是否包含逗号）
                bool isMultipleValues = parts.Length > 2 && parts[2].Contains(',');
                
                if (!isMultipleValues)
                {
                    // 单个寄存器
                    if (!ushort.TryParse(parts[2], out ushort value))
                    {
                        LogMessage($"写入寄存器失败：值'{parts[2]}'格式错误，请使用0-65535的整数");
                        return false;
                    }

                    lock (_operationLock)
                    {
                        _master.WriteSingleRegister(_config.UnitId, address, value);
                    }

                    // 触发数据写入事件
                    OnDataWritten(new DataWriteEventArgs
                    {
                        Address = $"HoldingReg_{address}",
                        Value = value,
                        Success = true,
                        Timestamp = DateTime.Now
                    });

                    LogMessage($"成功写入寄存器：地址{address}, 值: {value}");
                    return true;
                }
                else
                {
                    // 多个寄存器，值用逗号分隔
                    var valueStrings = parts[2].Split(',');
                    var values = new ushort[valueStrings.Length];

                    for (int i = 0; i < valueStrings.Length; i++)
                    {
                        if (!ushort.TryParse(valueStrings[i].Trim(), out values[i]))
                        {
                            LogMessage($"写入寄存器失败：值'{valueStrings[i]}'格式错误");
                            return false;
                        }
                    }

                    if (values.Length > 123) // Modbus协议限制
                    {
                        LogMessage($"写入寄存器失败：数量{values.Length}超出范围(1-123)");
                        return false;
                    }

                    lock (_operationLock)
                    {
                        _master.WriteMultipleRegisters(_config.UnitId, address, values);
                    }

                    // 触发数据写入事件
                    OnDataWritten(new DataWriteEventArgs
                    {
                        Address = $"HoldingRegs_{address}",
                        Value = values,
                        Success = true,
                        Timestamp = DateTime.Now
                    });

                    LogMessage($"成功写入多个寄存器：地址{address}, 数量{values.Length}, 值: [{string.Join(", ", values)}]");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"写入寄存器异常：{ex.Message}");
                OnError(new ErrorEventArgs($"写入寄存器失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理写入浮点数命令
        /// 命令格式：WRITE_FLOAT <地址> <浮点数值>
        /// 注意：浮点数占用2个寄存器，会按照配置的字节序进行存储
        /// </summary>
        private bool HandleWriteFloat(string[] parts)
        {
            if (parts.Length < 3)
            {
                LogMessage("写入浮点数命令格式错误，正确格式：WRITE_FLOAT <地址> <浮点数值>");
                return false;
            }

            if (!IsConnected)
            {
                LogMessage("写入浮点数失败：未连接到服务器");
                return false;
            }

            try
            {
                if (!ushort.TryParse(parts[1], out ushort address))
                {
                    LogMessage($"写入浮点数失败：地址'{parts[1]}'格式错误");
                    return false;
                }

                if (!float.TryParse(parts[2], out float value))
                {
                    LogMessage($"写入浮点数失败：值'{parts[2]}'格式错误");
                    return false;
                }

                // 将浮点数转换为字节数组
                byte[] floatBytes = BitConverter.GetBytes(value);
                
                // 根据字节序配置调整字节顺序
                ushort[] registers = new ushort[2];
                switch (_config.DataByteOrder)
                {
                    case ByteOrder.ABCD: // 大端序（标准）
                        registers[0] = (ushort)((floatBytes[3] << 8) | floatBytes[2]);
                        registers[1] = (ushort)((floatBytes[1] << 8) | floatBytes[0]);
                        break;
                    case ByteOrder.BADC: // 字节内交换
                        registers[0] = (ushort)((floatBytes[2] << 8) | floatBytes[3]);
                        registers[1] = (ushort)((floatBytes[0] << 8) | floatBytes[1]);
                        break;
                    case ByteOrder.CDAB: // 寄存器交换（小端序）
                        registers[0] = (ushort)((floatBytes[1] << 8) | floatBytes[0]);
                        registers[1] = (ushort)((floatBytes[3] << 8) | floatBytes[2]);
                        break;
                    case ByteOrder.DCBA: // 完全反序
                        registers[0] = (ushort)((floatBytes[0] << 8) | floatBytes[1]);
                        registers[1] = (ushort)((floatBytes[2] << 8) | floatBytes[3]);
                        break;
                }

                lock (_operationLock)
                {
                    _master.WriteMultipleRegisters(_config.UnitId, address, registers);
                }

                // 触发数据写入事件
                OnDataWritten(new DataWriteEventArgs
                {
                    Address = $"Float_{address}",
                    Value = value,
                    Success = true,
                    Timestamp = DateTime.Now
                });

                LogMessage($"成功写入浮点数：地址{address}, 值: {value}, 字节序: {_config.DataByteOrder}, 寄存器: [{registers[0]}, {registers[1]}]");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"写入浮点数异常：{ex.Message}");
                OnError(new ErrorEventArgs($"写入浮点数失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 处理断开连接命令
        /// </summary>
        private bool HandleDisconnect()
        {
            Disconnect();
            return true;
        }

        /// <summary>
        /// 处理自定义命令
        /// </summary>
        private bool HandleCustomCommand(string command)
        {
            LogMessage($"未识别的命令: {command}");
            return false;
        }

        #endregion

        #region IDisposable实现

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    CleanupConnection();
                }
                _disposed = true;
            }
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 数据读取事件参数
    /// </summary>
    public class DataReadEventArgs : EventArgs
    {
        public string Address { get; set; }
        public object Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 数据写入事件参数
    /// </summary>
    public class DataWriteEventArgs : EventArgs
    {
        public string Address { get; set; }
        public object Value { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 错误事件参数
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; set; }
        
        public ErrorEventArgs(string message)
        {
            Message = message;
        }
    }

    #endregion
}