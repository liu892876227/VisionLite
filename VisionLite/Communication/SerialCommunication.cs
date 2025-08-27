// Communication/SerialCommunication.cs
// 串口通讯的具体实现类
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace VisionLite.Communication
{
    /// <summary>
    /// 串口通讯的具体实现
    /// 提供基于串口(RS232/RS485)的数据传输，适用于工业设备通讯
    /// 
    /// 串口通讯特点：
    /// 1. 简单可靠：点对点通讯，协议简单
    /// 2. 抗干扰：适合工业环境
    /// 3. 远距离：RS485支持长距离传输
    /// 4. 成本低：硬件成本低廉
    /// 
    /// 实现特点：
    /// - 异步操作：不阻塞UI线程
    /// - 自动重连：检测到断开后可重新连接
    /// - 多格式支持：文本、十六进制、二进制
    /// - 线程安全：支持多线程环境下的并发访问
    /// </summary>
    public class SerialCommunication : ICommunication
    {
        #region 私有字段
        
        /// <summary>
        /// 串口对象，负责底层的串口通讯
        /// </summary>
        private SerialPort _serialPort;
        
        /// <summary>
        /// 串口配置参数
        /// </summary>
        private readonly SerialConfig _config;
        
        /// <summary>
        /// 取消令牌源，用于优雅地停止后台接收线程
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 协议处理器，负责消息的封包和解包
        /// </summary>
        private readonly IMessageProtocol _protocol;

        /// <summary>
        /// 数据接收缓冲区
        /// </summary>
        private readonly StringBuilder _receiveBuffer = new StringBuilder();

        /// <summary>
        /// 锁对象，用于线程同步
        /// </summary>
        private readonly object _lockObject = new object();
        
        #endregion

        #region 公共属性和事件
        
        /// <summary>
        /// 通讯实例的显示名称
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 当前连接状态
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event Action<Message> MessageReceived;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 创建串口通讯实例
        /// </summary>
        /// <param name="name">连接的显示名称</param>
        /// <param name="config">串口配置参数</param>
        public SerialCommunication(string name, SerialConfig config)
        {
            Name = name ?? "串口通讯";
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _protocol = new SerialTextProtocol(config);
            
            InitializeSerialPort();
        }
        
        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化串口参数
        /// </summary>
        private void InitializeSerialPort()
        {
            try
            {
                _serialPort = new SerialPort
                {
                    PortName = _config.PortName,
                    BaudRate = _config.BaudRate,
                    DataBits = _config.DataBits,
                    StopBits = _config.StopBits,
                    Parity = _config.Parity,
                    Handshake = _config.Handshake,
                    ReadTimeout = _config.ReadTimeout,
                    WriteTimeout = _config.WriteTimeout,
                    ReadBufferSize = _config.ReadBufferSize,
                    WriteBufferSize = _config.WriteBufferSize
                };

                // 注册数据接收事件
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.ErrorReceived += SerialPort_ErrorReceived;
                
                LogMessage($"串口初始化完成: {_config.PortName} - {_config.BaudRate},{_config.DataBits},{_config.Parity},{_config.StopBits}");
            }
            catch (Exception ex)
            {
                LogError($"串口初始化失败: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region ICommunication接口实现

        /// <summary>
        /// 异步打开串口连接
        /// </summary>
        public async Task<bool> OpenAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    SetStatus(ConnectionStatus.Connecting);
                    LogMessage($"正在打开串口: {_config.PortName}");

                    if (_serialPort?.IsOpen == true)
                    {
                        LogMessage("串口已经打开，先关闭现有连接");
                        _serialPort.Close();
                    }

                    // 检查串口是否存在
                    var availablePorts = SerialPort.GetPortNames();
                    if (!availablePorts.Contains(_config.PortName))
                    {
                        LogError($"串口 {_config.PortName} 不存在。可用串口: {string.Join(", ", availablePorts)}");
                        SetStatus(ConnectionStatus.Error);
                        return false;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                    _serialPort.Open();

                    if (_serialPort.IsOpen)
                    {
                        LogMessage($"串口连接成功: {_config.PortName}");
                        SetStatus(ConnectionStatus.Connected);
                        
                        // 启动自动重连监控
                        if (_config.AutoReconnect)
                        {
                            Task.Run(MonitorConnection, _cancellationTokenSource.Token);
                        }
                        
                        return true;
                    }
                    else
                    {
                        LogError("串口打开失败");
                        SetStatus(ConnectionStatus.Error);
                        return false;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogError($"串口访问被拒绝 (可能被其他程序占用): {ex.Message}");
                    SetStatus(ConnectionStatus.Error);
                    return false;
                }
                catch (Exception ex)
                {
                    LogError($"串口连接失败: {ex.Message}");
                    SetStatus(ConnectionStatus.Error);
                    return false;
                }
            });
        }

        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void Close()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                    LogMessage("串口连接已关闭");
                }
                
                SetStatus(ConnectionStatus.Disconnected);
            }
            catch (Exception ex)
            {
                LogError($"关闭串口时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        public async Task<bool> SendAsync(Message message)
        {
            if (_serialPort?.IsOpen != true)
            {
                LogError("串口未连接，无法发送数据");
                return false;
            }

            try
            {
                byte[] data = _protocol.Encode(message);
                
                await Task.Run(() =>
                {
                    _serialPort.Write(data, 0, data.Length);
                });
                
                if (_config.EnableLogging)
                {
                    string dataStr = FormatDataForDisplay(data, _config.DataFormat);
                    LogMessage($"发送: {dataStr}");
                }
                
                return true;
            }
            catch (TimeoutException)
            {
                LogError("发送数据超时");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"发送数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close();
            _serialPort?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        #endregion

        #region 数据接收处理

        /// <summary>
        /// 串口数据接收事件处理
        /// </summary>
        private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort?.IsOpen != true) return;

                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead == 0) return;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                
                if (bytesRead > 0)
                {
                    await ProcessReceivedData(buffer, bytesRead);
                }
            }
            catch (Exception ex)
            {
                LogError($"数据接收处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        private async Task ProcessReceivedData(byte[] buffer, int length)
        {
            try
            {
                byte[] actualData = new byte[length];
                Array.Copy(buffer, actualData, length);

                if (_config.EnableLogging)
                {
                    string dataStr = FormatDataForDisplay(actualData, _config.DataFormat);
                    LogMessage($"接收: {dataStr}");
                }

                // 根据数据格式处理接收的数据
                switch (_config.DataFormat)
                {
                    case SerialDataFormat.Text:
                        await ProcessTextData(actualData);
                        break;
                    case SerialDataFormat.Hex:
                    case SerialDataFormat.Binary:
                        await ProcessBinaryData(actualData);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"处理接收数据时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文本格式数据
        /// </summary>
        private async Task ProcessTextData(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);
            
            lock (_lockObject)
            {
                _receiveBuffer.Append(text);
            }

            // 查找完整的消息（以消息结束符结尾）
            await ExtractCompleteMessages();
        }

        /// <summary>
        /// 处理二进制数据
        /// </summary>
        private async Task ProcessBinaryData(byte[] data)
        {
            // 直接作为一条消息处理
            var messages = _protocol.Decode(data, data.Length);
            foreach (var message in messages)
            {
                await DispatchMessage(message);
            }
        }

        /// <summary>
        /// 提取完整的消息
        /// </summary>
        private async Task ExtractCompleteMessages()
        {
            string bufferContent;
            lock (_lockObject)
            {
                bufferContent = _receiveBuffer.ToString();
            }

            string terminator = _config.MessageTerminator;
            int terminatorIndex;

            while ((terminatorIndex = bufferContent.IndexOf(terminator)) >= 0)
            {
                // 提取一条完整消息
                string messageText = bufferContent.Substring(0, terminatorIndex);
                
                // 从缓冲区中移除已处理的消息
                bufferContent = bufferContent.Substring(terminatorIndex + terminator.Length);
                
                if (!string.IsNullOrWhiteSpace(messageText))
                {
                    var messages = _protocol.Decode(Encoding.UTF8.GetBytes(messageText), Encoding.UTF8.GetBytes(messageText).Length);
                    foreach (var message in messages)
                    {
                        await DispatchMessage(message);
                    }
                }
            }

            // 更新缓冲区
            lock (_lockObject)
            {
                _receiveBuffer.Clear();
                _receiveBuffer.Append(bufferContent);
            }
        }

        /// <summary>
        /// 分发消息到UI线程
        /// </summary>
        private async Task DispatchMessage(Message message)
        {
            try
            {
                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageReceived?.Invoke(message);
                    });
                }
                else
                {
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                LogError($"分发消息时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region 错误处理和重连

        /// <summary>
        /// 串口错误事件处理
        /// </summary>
        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            LogError($"串口错误: {e.EventType}");
            
            if (_config.AutoReconnect)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(_config.ReconnectInterval);
                    await TryReconnect();
                });
            }
        }

        /// <summary>
        /// 连接监控（自动重连）
        /// </summary>
        private async Task MonitorConnection()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    
                    if (_serialPort?.IsOpen != true && Status == ConnectionStatus.Connected)
                    {
                        LogMessage("检测到串口连接断开，尝试重连...");
                        await TryReconnect();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"连接监控错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 尝试重连
        /// </summary>
        private async Task TryReconnect()
        {
            if (!_config.AutoReconnect) return;
            
            try
            {
                SetStatus(ConnectionStatus.Connecting);
                LogMessage("正在尝试重新连接...");
                
                // 关闭现有连接
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                
                await Task.Delay(1000); // 等待一秒后重连
                
                bool reconnected = await OpenAsync();
                if (reconnected)
                {
                    LogMessage("重连成功");
                }
                else
                {
                    LogError("重连失败");
                    SetStatus(ConnectionStatus.Error);
                }
            }
            catch (Exception ex)
            {
                LogError($"重连过程中发生错误: {ex.Message}");
                SetStatus(ConnectionStatus.Error);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置连接状态并触发事件
        /// </summary>
        private void SetStatus(ConnectionStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        StatusChanged?.Invoke(newStatus);
                    });
                }
                catch
                {
                    // 忽略UI线程调用错误
                }
            }
        }

        /// <summary>
        /// 记录普通日志消息
        /// </summary>
        private void LogMessage(string message)
        {
            if (!_config.EnableLogging) return;

            try
            {
                var logMessage = new Message
                {
                    Command = "LOG",
                    Type = MessageType.Event,
                    Parameters = { ["Message"] = $"[{Name}] {message}" }
                };

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MessageReceived?.Invoke(logMessage);
                });
            }
            catch
            {
                // 忽略日志记录错误
            }
        }

        /// <summary>
        /// 记录错误日志消息
        /// </summary>
        private void LogError(string error)
        {
            try
            {
                var logMessage = new Message
                {
                    Command = "LOG",
                    Type = MessageType.Event,
                    Parameters = { ["Message"] = $"[{Name}] 错误: {error}" }
                };

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MessageReceived?.Invoke(logMessage);
                });
            }
            catch
            {
                // 忽略日志记录错误
            }
        }

        /// <summary>
        /// 格式化数据用于显示
        /// </summary>
        private string FormatDataForDisplay(byte[] data, SerialDataFormat format)
        {
            switch (format)
            {
                case SerialDataFormat.Text:
                    return Encoding.UTF8.GetString(data).Replace("\r", "\\r").Replace("\n", "\\n");
                
                case SerialDataFormat.Hex:
                    return BitConverter.ToString(data).Replace("-", " ");
                
                case SerialDataFormat.Binary:
                    return string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
                
                default:
                    return Encoding.UTF8.GetString(data);
            }
        }

        #endregion
    }

    /// <summary>
    /// 串口专用的文本协议实现
    /// </summary>
    public class SerialTextProtocol : IMessageProtocol
    {
        private readonly SerialConfig _config;

        public SerialTextProtocol(SerialConfig config)
        {
            _config = config;
        }

        public Message ParseMessage(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);
            
            return new Message
            {
                Command = "SERIAL_DATA",
                Type = MessageType.Response,
                Parameters = 
                {
                    ["RawData"] = text,
                    ["Length"] = data.Length,
                    ["Format"] = _config.DataFormat.ToString(),
                    ["HexData"] = BitConverter.ToString(data).Replace("-", " ")
                }
            };
        }

        public byte[] Encode(Message message)
        {
            string dataToSend;
            
            if (message.Parameters.ContainsKey("Data"))
            {
                dataToSend = message.Parameters["Data"].ToString();
            }
            else
            {
                dataToSend = message.Command;
            }

            // 根据数据格式进行转换
            byte[] data;
            switch (_config.DataFormat)
            {
                case SerialDataFormat.Hex:
                    // 十六进制格式: "41 42 43" -> {0x41, 0x42, 0x43}
                    try
                    {
                        string[] hexValues = dataToSend.Split(new char[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        data = hexValues.Select(hex => Convert.ToByte(hex, 16)).ToArray();
                    }
                    catch
                    {
                        // 如果十六进制解析失败，按文本处理
                        data = Encoding.UTF8.GetBytes(dataToSend);
                    }
                    break;
                
                case SerialDataFormat.Binary:
                    // 二进制格式暂时按文本处理
                    data = Encoding.UTF8.GetBytes(dataToSend);
                    break;
                
                default: // Text
                    data = Encoding.UTF8.GetBytes(dataToSend);
                    break;
            }

            // 添加消息结束符（如果不是二进制格式且数据中没有结束符）
            if (_config.DataFormat == SerialDataFormat.Text && !dataToSend.EndsWith(_config.MessageTerminator))
            {
                byte[] terminator = Encoding.UTF8.GetBytes(_config.MessageTerminator);
                byte[] result = new byte[data.Length + terminator.Length];
                Array.Copy(data, 0, result, 0, data.Length);
                Array.Copy(terminator, 0, result, data.Length, terminator.Length);
                return result;
            }

            return data;
        }

        public System.Collections.Generic.IEnumerable<Message> Decode(byte[] buffer, int bytesRead)
        {
            var messages = new List<Message>();
            
            if (bytesRead > 0)
            {
                byte[] actualData = new byte[bytesRead];
                Array.Copy(buffer, actualData, bytesRead);
                
                var message = ParseMessage(actualData);
                if (message != null)
                {
                    messages.Add(message);
                }
            }
            
            return messages;
        }

        public byte[] FormatMessage(Message message)
        {
            return Encode(message);
        }
    }
}