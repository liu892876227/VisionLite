// Communication/UdpCommunication.cs
// UDP通讯的具体实现类
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VisionLite.Communication
{
    /// <summary>
    /// UDP通讯的具体实现
    /// 提供基于UDP协议的数据传输，适用于对延迟敏感、能容忍偶尔丢包的通讯场景
    /// 
    /// UDP的优势：
    /// 1. 低延迟：无需建立连接，直接发送数据
    /// 2. 高性能：开销小，适合高频率通讯
    /// 3. 广播支持：可以发送广播和多播消息
    /// 4. 简单高效：协议简单，处理快速
    /// 
    /// UDP的限制：
    /// 1. 不可靠：不保证数据到达
    /// 2. 无序：数据包可能乱序到达
    /// 3. 无连接状态：需要应用层管理连接状态
    /// 4. MTU限制：单个消息大小受限
    /// 
    /// 实现特点：
    /// - 异步操作：不阻塞UI线程
    /// - 协议无关：通过IMessageProtocol支持各种消息格式
    /// - 线程安全：支持多线程环境下的并发访问
    /// - 消息边界：UDP天然具有消息边界，无需特殊处理
    /// </summary>
    public class UdpCommunication : ICommunication
    {
        #region 常量定义
        
        /// <summary>
        /// UDP消息的最大尺寸（考虑MTU限制）
        /// 标准以太网MTU为1500字节，减去IP头（20字节）和UDP头（8字节）后的安全值
        /// </summary>
        private const int MAX_UDP_MESSAGE_SIZE = 1400;
        
        #endregion

        #region 私有字段
        
        /// <summary>
        /// UDP客户端对象，负责UDP数据包的收发
        /// </summary>
        private UdpClient _client;
        
        /// <summary>
        /// 远程终结点（服务器地址和端口）
        /// </summary>
        private IPEndPoint _remoteEndPoint;
        
        /// <summary>
        /// 目标服务器的IP地址
        /// </summary>
        private readonly string _ipAddress;
        
        /// <summary>
        /// 目标服务器的端口号
        /// </summary>
        private readonly int _port;
        
        /// <summary>
        /// 消息协议处理器，负责消息的序列化和反序列化
        /// </summary>
        private readonly IMessageProtocol _protocol;
        
        /// <summary>
        /// 取消令牌源，用于控制异步操作的取消
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;
        
        /// <summary>
        /// 接收数据的后台任务
        /// </summary>
        private Task _receiveTask;
        
        /// <summary>
        /// 当前连接状态
        /// </summary>
        private ConnectionStatus _status = ConnectionStatus.Disconnected;
        
        /// <summary>
        /// 状态更新的锁对象，确保状态变更的线程安全
        /// </summary>
        private readonly object _statusLock = new object();
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化UDP通讯实例
        /// </summary>
        /// <param name="name">连接名称</param>
        /// <param name="ipAddress">目标IP地址</param>
        /// <param name="port">目标端口</param>
        public UdpCommunication(string name, string ipAddress, int port)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            _port = port;
            _protocol = new VisionLiteProtocol(); // 使用默认协议
            
            // 验证参数
            if (!IPAddress.TryParse(_ipAddress, out _))
                throw new ArgumentException($"无效的IP地址: {_ipAddress}");
                
            if (_port < 1 || _port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535范围内");
        }
        
        #endregion

        #region ICommunication接口实现
        
        /// <summary>
        /// 获取通讯实例的名称
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// 获取UDP客户端的本地监听端口（连接后可用）
        /// </summary>
        public int? LocalPort 
        { 
            get 
            { 
                try
                {
                    return _client?.Client?.LocalEndPoint != null ? 
                        ((IPEndPoint)_client.Client.LocalEndPoint).Port : null;
                }
                catch
                {
                    return null;
                }
            } 
        }
        
        /// <summary>
        /// 获取当前连接状态
        /// </summary>
        public ConnectionStatus Status 
        { 
            get 
            { 
                lock (_statusLock) 
                { 
                    return _status; 
                } 
            } 
            private set 
            { 
                lock (_statusLock) 
                { 
                    if (_status != value) 
                    { 
                        _status = value; 
                        // 在UI线程上触发状态变化事件
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() => StatusChanged?.Invoke(_status)));
                    } 
                } 
            } 
        }
        
        /// <summary>
        /// 当连接状态发生变化时触发的事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// 当接收到消息时触发的事件
        /// </summary>
        public event Action<Message> MessageReceived;
        
        /// <summary>
        /// 异步打开UDP连接
        /// </summary>
        /// <returns>连接是否成功</returns>
        public Task<bool> OpenAsync()
        {
            try
            {
                Status = ConnectionStatus.Connecting;
                
                // 创建UDP客户端，绑定任意可用的本地端口用于接收数据
                _client = new UdpClient(0); // 0表示系统自动分配可用端口
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
                
                // 注意：对于UDP客户端，不使用Connect方法，这样可以接收来自任何地址的数据
                // 发送时手动指定目标地址即可
                
                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 启动接收数据的后台任务
                _receiveTask = ReceiveLoop(_cancellationTokenSource.Token);
                
                Status = ConnectionStatus.Connected;
                int localPort = ((IPEndPoint)_client.Client.LocalEndPoint).Port;
                System.Diagnostics.Debug.WriteLine($"UDP客户端启动成功，本地监听端口: {localPort}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Status = ConnectionStatus.Error;
                System.Diagnostics.Debug.WriteLine($"UDP连接失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            try
            {
                // 立即更新状态，避免重复调用
                Status = ConnectionStatus.Disconnected;
                
                // 取消后台任务
                _cancellationTokenSource?.Cancel();
                
                // 关闭UDP客户端，这会中断ReceiveAsync操作
                try
                {
                    _client?.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"关闭UDP客户端异常: {ex.Message}");
                }
                
                try
                {
                    _client?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放UDP客户端异常: {ex.Message}");
                }
                
                _client = null;
                
                // 异步清理任务，不阻塞UI线程（明确忽略返回值）
                _ = Task.Run(() =>
                {
                    try
                    {
                        // 等待接收任务结束，最多等待500毫秒
                        _receiveTask?.Wait(500);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"等待接收任务结束异常: {ex.Message}");
                    }
                    finally
                    {
                        // 清理资源
                        try
                        {
                            _cancellationTokenSource?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"释放取消令牌异常: {ex.Message}");
                        }
                    }
                });
                
                // 立即清空引用
                _cancellationTokenSource = null;
                _receiveTask = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UDP关闭异常: {ex.Message}");
                Status = ConnectionStatus.Error;
            }
        }
        
        /// <summary>
        /// 异步发送消息
        /// </summary>
        /// <param name="message">要发送的消息对象</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> SendAsync(Message message)
        {
            if (Status != ConnectionStatus.Connected || _client == null)
                return false;
                
            try
            {
                // 使用协议编码消息
                var data = _protocol.Encode(message);
                
                // 检查消息大小
                if (data.Length > MAX_UDP_MESSAGE_SIZE)
                {
                    System.Diagnostics.Debug.WriteLine($"UDP消息过大 ({data.Length}字节)，超过最大限制({MAX_UDP_MESSAGE_SIZE}字节)");
                    return false;
                }
                
                // 发送数据到指定的远程终结点
                var sentBytes = await _client.SendAsync(data, data.Length, _remoteEndPoint);
                return sentBytes == data.Length;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UDP发送失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close();
        }
        
        #endregion

        #region 私有方法
        
        /// <summary>
        /// 接收数据的循环任务
        /// 在后台持续监听UDP数据包的到达
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_client == null) break;
                    
                    // 接收UDP数据包
                    var result = await _client.ReceiveAsync();
                    
                    // UDP客户端可以接收来自任何地址的数据，但记录来源地址用于调试
                    System.Diagnostics.Debug.WriteLine($"UDP客户端收到来自 {result.RemoteEndPoint} 的数据");
                    
                    // UDP每个包就是一个完整消息，直接解码
                    var messages = _protocol.Decode(result.Buffer, result.Buffer.Length);
                    foreach (var message in messages)
                    {
                        // 在UI线程上触发消息接收事件
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() => MessageReceived?.Invoke(message)));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // UDP客户端已被释放，正常退出循环
                    break;
                }
                catch (SocketException ex)
                {
                    // 网络异常，记录日志但继续监听
                    System.Diagnostics.Debug.WriteLine($"UDP接收网络异常: {ex.Message}");
                    
                    // 如果是严重的网络错误，更新连接状态
                    if (ex.SocketErrorCode == SocketError.NetworkDown || 
                        ex.SocketErrorCode == SocketError.NetworkUnreachable)
                    {
                        Status = ConnectionStatus.Error;
                        break;
                    }
                    
                    // 短暂延迟后继续
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 其他异常，记录日志
                    System.Diagnostics.Debug.WriteLine($"UDP接收异常: {ex.Message}");
                    
                    // 短暂延迟后继续
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        
        #endregion
    }
}