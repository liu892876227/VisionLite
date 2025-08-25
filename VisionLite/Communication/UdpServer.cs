// Communication/UdpServer.cs
// UDP服务器的具体实现类
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VisionLite.Communication
{
    /// <summary>
    /// UDP服务器的具体实现
    /// 提供基于UDP协议的服务器端通讯，支持多客户端数据收发
    /// 
    /// UDP服务器的特点：
    /// 1. 无连接监听：监听指定端口，接收来自任何客户端的数据
    /// 2. 多客户端支持：可以同时接收多个客户端的数据
    /// 3. 快速响应：无需建立连接，可以立即响应客户端
    /// 4. 广播功能：可以向多个客户端发送数据
    /// 
    /// UDP服务器的限制：
    /// 1. 无连接状态：无法确定客户端是否真正在线
    /// 2. 不可靠传输：数据包可能丢失或重复
    /// 3. 客户端管理：需要应用层维护客户端列表
    /// 4. 无序传输：数据包可能乱序到达
    /// 
    /// 实现特点：
    /// - 异步操作：不阻塞UI线程
    /// - 线程安全：支持多线程环境下的并发访问
    /// - 协议无关：通过IMessageProtocol支持各种消息格式
    /// - 客户端跟踪：基于最后通讯时间管理客户端列表
    /// </summary>
    public class UdpServer : ICommunication
    {
        #region 常量定义
        
        /// <summary>
        /// UDP消息的最大尺寸（考虑MTU限制）
        /// </summary>
        private const int MAX_UDP_MESSAGE_SIZE = 1400;
        
        /// <summary>
        /// 客户端超时时间（秒）
        /// 超过此时间未收到数据的客户端将被认为已断开
        /// </summary>
        private const int CLIENT_TIMEOUT_SECONDS = 60;
        
        #endregion

        #region 私有字段
        
        /// <summary>
        /// UDP服务器对象，用于监听和收发数据
        /// </summary>
        private UdpClient _server;
        
        /// <summary>
        /// 服务器监听的端口号
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
        /// 客户端清理任务
        /// </summary>
        private Task _cleanupTask;
        
        /// <summary>
        /// 客户端列表，记录每个客户端的最后通讯时间
        /// Key: 客户端的IP终结点，Value: 最后通讯时间
        /// </summary>
        private readonly ConcurrentDictionary<IPEndPoint, DateTime> _clients = new ConcurrentDictionary<IPEndPoint, DateTime>();
        
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
        /// 初始化UDP服务器实例
        /// </summary>
        /// <param name="name">服务器名称</param>
        /// <param name="port">监听端口</param>
        public UdpServer(string name, int port)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _port = port;
            _protocol = new VisionLiteProtocol(); // 使用默认协议
            
            // 验证端口号
            if (_port < 1 || _port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535范围内");
        }
        
        #endregion

        #region ICommunication接口实现
        
        /// <summary>
        /// 获取服务器名称
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// 获取当前服务器状态
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
        /// 当服务器状态发生变化时触发的事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// 当接收到消息时触发的事件
        /// </summary>
        public event Action<Message> MessageReceived;
        
        /// <summary>
        /// 异步启动UDP服务器
        /// </summary>
        /// <returns>启动是否成功</returns>
        public async Task<bool> OpenAsync()
        {
            try
            {
                Status = ConnectionStatus.Connecting;
                
                // 创建UDP服务器，监听指定端口
                _server = new UdpClient(_port);
                
                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 启动接收数据的后台任务
                _receiveTask = ReceiveLoop(_cancellationTokenSource.Token);
                
                // 启动客户端清理任务
                _cleanupTask = ClientCleanupLoop(_cancellationTokenSource.Token);
                
                Status = ConnectionStatus.Connected;
                return true;
            }
            catch (Exception ex)
            {
                Status = ConnectionStatus.Error;
                System.Diagnostics.Debug.WriteLine($"UDP服务器启动失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 关闭服务器
        /// </summary>
        public void Close()
        {
            try
            {
                // 立即更新状态，避免重复调用
                Status = ConnectionStatus.Disconnected;
                
                // 取消后台任务
                _cancellationTokenSource?.Cancel();
                
                // 关闭UDP服务器，这会中断ReceiveAsync操作
                try
                {
                    _server?.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"关闭UDP服务器异常: {ex.Message}");
                }
                
                try
                {
                    _server?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放UDP服务器异常: {ex.Message}");
                }
                
                _server = null;
                
                // 清空客户端列表
                _clients.Clear();
                
                // 异步清理任务，不阻塞UI线程
                Task.Run(() =>
                {
                    try
                    {
                        // 等待任务结束，最多等待500毫秒
                        _receiveTask?.Wait(500);
                        _cleanupTask?.Wait(500);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"等待UDP服务器任务结束异常: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"释放UDP服务器取消令牌异常: {ex.Message}");
                        }
                    }
                });
                
                // 立即清空引用
                _cancellationTokenSource = null;
                _receiveTask = null;
                _cleanupTask = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UDP服务器关闭异常: {ex.Message}");
                Status = ConnectionStatus.Error;
            }
        }
        
        /// <summary>
        /// 异步发送消息
        /// 对于UDP服务器，这个方法会向所有已知的客户端广播消息
        /// 如果没有已知客户端，则向默认测试地址发送（用于测试）
        /// </summary>
        /// <param name="message">要发送的消息对象</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> SendAsync(Message message)
        {
            if (Status != ConnectionStatus.Connected || _server == null)
                return false;
                
            // 获取当前所有活跃的客户端
            var activeClients = _clients.Keys.ToArray();
            
            // 如果没有活跃客户端，使用默认测试地址（用于调试）
            if (activeClients.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("UDP服务器：没有活跃的客户端，尝试发送到默认测试地址");
                var defaultClient = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1024);
                return await SendToClientAsync(message, defaultClient);
            }
                
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
                
                // 向所有客户端发送消息
                int successCount = 0;
                foreach (var client in activeClients)
                {
                    try
                    {
                        var sentBytes = await _server.SendAsync(data, data.Length, client);
                        if (sentBytes == data.Length)
                        {
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"向客户端 {client} 发送UDP消息失败: {ex.Message}");
                    }
                }
                
                return successCount > 0; // 至少成功发送给一个客户端
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UDP广播发送失败: {ex.Message}");
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

        #region 服务器特有的公共方法
        
        /// <summary>
        /// 获取当前连接的客户端数量
        /// </summary>
        public int ConnectedClientCount => _clients.Count;
        
        /// <summary>
        /// 获取所有活跃客户端的终结点列表
        /// </summary>
        /// <returns>客户端终结点数组</returns>
        public IPEndPoint[] GetActiveClients()
        {
            return _clients.Keys.ToArray();
        }
        
        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        /// <param name="message">要发送的消息</param>
        /// <param name="clientEndPoint">目标客户端终结点</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> SendToClientAsync(Message message, IPEndPoint clientEndPoint)
        {
            if (Status != ConnectionStatus.Connected || _server == null)
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
                
                // 发送到指定客户端
                var sentBytes = await _server.SendAsync(data, data.Length, clientEndPoint);
                return sentBytes == data.Length;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"向客户端 {clientEndPoint} 发送UDP消息失败: {ex.Message}");
                return false;
            }
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
                    if (_server == null) break;
                    
                    // 接收UDP数据包
                    var result = await _server.ReceiveAsync();
                    
                    // 更新客户端的最后通讯时间
                    _clients.AddOrUpdate(result.RemoteEndPoint, DateTime.Now, 
                        (key, oldValue) => DateTime.Now);
                    
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
                    // UDP服务器已被释放，正常退出循环
                    break;
                }
                catch (SocketException ex)
                {
                    // 网络异常，记录日志但继续监听
                    System.Diagnostics.Debug.WriteLine($"UDP服务器接收网络异常: {ex.Message}");
                    
                    // 如果是严重的网络错误，更新连接状态
                    if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || 
                        ex.SocketErrorCode == SocketError.AccessDenied)
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
                    System.Diagnostics.Debug.WriteLine($"UDP服务器接收异常: {ex.Message}");
                    
                    // 短暂延迟后继续
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        
        /// <summary>
        /// 客户端清理循环任务
        /// 定期清理超时的客户端
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ClientCleanupLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 每30秒清理一次
                    await Task.Delay(30000, cancellationToken);
                    
                    var now = DateTime.Now;
                    var timeoutClients = _clients
                        .Where(kvp => (now - kvp.Value).TotalSeconds > CLIENT_TIMEOUT_SECONDS)
                        .Select(kvp => kvp.Key)
                        .ToArray();
                    
                    // 移除超时的客户端
                    foreach (var client in timeoutClients)
                    {
                        if (_clients.TryRemove(client, out var lastTime))
                        {
                            System.Diagnostics.Debug.WriteLine($"UDP客户端超时被移除: {client} (最后通讯时间: {lastTime})");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UDP客户端清理异常: {ex.Message}");
                }
            }
        }
        
        #endregion
    }
}