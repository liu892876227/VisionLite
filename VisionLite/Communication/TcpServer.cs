// Communication/TcpServer.cs
// TCP服务器的具体实现类
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
    /// TCP服务器的具体实现
    /// 提供基于TCP协议的服务器端通讯，支持多客户端并发连接
    /// 
    /// TCP服务器的特点：
    /// 1. 监听指定端口：等待客户端主动连接
    /// 2. 多客户端支持：可以同时服务多个客户端
    /// 3. 异步处理：每个客户端连接都在独立的任务中处理
    /// 4. 广播消息：可以向所有连接的客户端发送消息
    /// 
    /// 实现特点：
    /// - 异步操作：不阻塞UI线程
    /// - 线程安全：支持多线程环境下的并发访问
    /// - 协议无关：通过IMessageProtocol支持各种消息格式
    /// - 客户端管理：自动处理客户端连接和断开
    /// </summary>
    public class TcpServer : ITcpServer
    {
        #region 私有字段
        
        /// <summary>
        /// TCP监听器，负责监听指定端口上的客户端连接
        /// </summary>
        private TcpListener _listener;
        
        /// <summary>
        /// 服务器监听的端口号
        /// </summary>
        private readonly int _port;
        
        /// <summary>
        /// 取消令牌源，用于优雅地停止服务器和所有客户端连接
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 协议处理器，负责消息的封包和解包
        /// </summary>
        private readonly IMessageProtocol _protocol;

        /// <summary>
        /// 存储所有活跃的客户端连接信息
        /// 使用线程安全的字典来支持并发访问
        /// </summary>
        private readonly ConcurrentDictionary<string, ClientConnection> _clients;

        /// <summary>
        /// 服务器状态标志，用于控制监听循环
        /// </summary>
        private volatile bool _isRunning;
        
        #endregion

        #region 内部类：客户端连接管理
        
        /// <summary>
        /// 客户端连接的封装类
        /// 管理单个客户端的TCP连接、数据流和接收任务
        /// </summary>
        private class ClientConnection
        {
            public string Id { get; set; }               // 客户端唯一标识
            public TcpClient TcpClient { get; set; }     // TCP客户端对象
            public NetworkStream Stream { get; set; }    // 网络数据流
            public Task ReceiveTask { get; set; }        // 接收数据的后台任务
            public CancellationTokenSource CancelSource { get; set; } // 取消令牌
            public DateTime ConnectedTime { get; set; }   // 连接建立时间
        }
        
        #endregion

        #region 公共属性和事件
        
        /// <summary>
        /// 服务器实例的显示名称
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 当前服务器状态
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 服务器状态变化事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// 消息接收事件，当从任何客户端收到消息时触发
        /// </summary>
        public event Action<Message> MessageReceived;

        /// <summary>
        /// 客户端连接事件，当有新客户端连接时触发
        /// </summary>
        public event Action<string> ClientConnected;

        /// <summary>
        /// 客户端断开事件，当客户端断开连接时触发
        /// </summary>
        public event Action<string> ClientDisconnected;

        /// <summary>
        /// 获取当前连接的客户端数量
        /// </summary>
        public int ClientCount => _clients.Count;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 创建TCP服务器实例
        /// </summary>
        /// <param name="name">服务器的显示名称</param>
        /// <param name="port">监听端口号</param>
        public TcpServer(string name, int port)
        {
            Name = name;
            _port = port;
            _protocol = new VisionLiteProtocol();
            _clients = new ConcurrentDictionary<string, ClientConnection>();
        }
        
        #endregion

        #region 私有辅助方法
        
        /// <summary>
        /// 更新服务器状态并触发状态变化事件
        /// </summary>
        /// <param name="newStatus">新的状态</param>
        private void UpdateStatus(ConnectionStatus newStatus)
        {
            if (Status == newStatus) return;
            Status = newStatus;
            Application.Current?.Dispatcher.Invoke(() => StatusChanged?.Invoke(newStatus));
        }

        /// <summary>
        /// 生成客户端唯一标识
        /// </summary>
        /// <param name="client">TCP客户端对象</param>
        /// <returns>客户端标识字符串</returns>
        private string GenerateClientId(TcpClient client)
        {
            var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
            return $"{endpoint?.Address}:{endpoint?.Port}";
        }
        
        #endregion

        #region ICommunication 接口实现
        
        /// <summary>
        /// 异步启动TCP服务器
        /// 开始监听指定端口，等待客户端连接
        /// </summary>
        /// <returns>启动成功返回true，否则返回false</returns>
        public async Task<bool> OpenAsync()
        {
            if (Status == ConnectionStatus.Connected || Status == ConnectionStatus.Connecting)
                return true;

            try
            {
                UpdateStatus(ConnectionStatus.Connecting);
                
                // 创建监听器，监听所有网络接口上的指定端口
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                
                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;
                
                // 在后台任务中开始接受客户端连接
                _ = Task.Run(() => AcceptClientLoop(_cancellationTokenSource.Token));
                
                UpdateStatus(ConnectionStatus.Connected);
                System.Console.WriteLine($"TCP服务器已启动，监听端口: {_port}");
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"TCP服务器启动失败: {ex.Message}");
                Close();
                UpdateStatus(ConnectionStatus.Error);
                return false;
            }
        }

        /// <summary>
        /// 关闭TCP服务器并断开所有客户端连接
        /// </summary>
        public void Close()
        {
            _isRunning = false;
            
            // 停止监听新连接
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            // 关闭监听器
            _listener?.Stop();

            // 断开所有客户端连接
            foreach (var client in _clients.Values)
            {
                CleanupClientConnection(client);
            }
            _clients.Clear();

            // 清理资源
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _listener = null;

            UpdateStatus(ConnectionStatus.Disconnected);
            System.Console.WriteLine("TCP服务器已关闭");
        }

        /// <summary>
        /// 向所有连接的客户端广播消息
        /// </summary>
        /// <param name="message">要发送的消息对象</param>
        /// <returns>发送成功返回true，否则返回false</returns>
        public async Task<bool> SendAsync(Message message)
        {
            if (Status != ConnectionStatus.Connected)
                return false;

            bool anySuccess = false;
            byte[] frame = _protocol.Encode(message);

            // 向所有客户端发送消息
            foreach (var client in _clients.Values)
            {
                try
                {
                    if (client.Stream?.CanWrite == true)
                    {
                        await client.Stream.WriteAsync(frame, 0, frame.Length);
                        anySuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"向客户端 {client.Id} 发送消息失败: {ex.Message}");
                    // 发送失败的客户端将在接收循环中被自动移除
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        /// <param name="clientId">客户端标识</param>
        /// <param name="message">要发送的消息</param>
        /// <returns>发送成功返回true，否则返回false</returns>
        public async Task<bool> SendToClientAsync(string clientId, Message message)
        {
            if (Status != ConnectionStatus.Connected || !_clients.TryGetValue(clientId, out var client))
                return false;

            try
            {
                if (client.Stream?.CanWrite == true)
                {
                    byte[] frame = _protocol.Encode(message);
                    await client.Stream.WriteAsync(frame, 0, frame.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"向客户端 {clientId} 发送消息失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 断开指定客户端的连接
        /// </summary>
        /// <param name="clientId">要断开的客户端标识</param>
        /// <returns>操作成功返回true，否则返回false</returns>
        public bool DisconnectClient(string clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                CleanupClientConnection(client);
                System.Console.WriteLine($"主动断开客户端: {clientId}");
                
                // 触发客户端断开事件
                Application.Current?.Dispatcher.Invoke(() => ClientDisconnected?.Invoke(clientId));
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有连接客户端的信息
        /// </summary>
        /// <returns>客户端ID的字符串数组</returns>
        public string[] GetConnectedClients()
        {
            return _clients.Keys.ToArray();
        }
        
        #endregion

        #region 私有服务器处理方法
        
        /// <summary>
        /// 接受客户端连接的循环方法
        /// 持续监听新的客户端连接请求
        /// </summary>
        /// <param name="token">取消令牌</param>
        private async void AcceptClientLoop(CancellationToken token)
        {
            try
            {
                while (_isRunning && !token.IsCancellationRequested)
                {
                    // 等待客户端连接（这是一个阻塞操作）
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    
                    // 为新客户端创建连接管理对象
                    var clientId = GenerateClientId(tcpClient);
                    var clientConnection = new ClientConnection
                    {
                        Id = clientId,
                        TcpClient = tcpClient,
                        Stream = tcpClient.GetStream(),
                        CancelSource = new CancellationTokenSource(),
                        ConnectedTime = DateTime.Now
                    };

                    // 启动客户端数据接收任务
                    clientConnection.ReceiveTask = Task.Run(() => 
                        ClientReceiveLoop(clientConnection, clientConnection.CancelSource.Token));

                    // 将客户端添加到管理字典中
                    _clients.TryAdd(clientId, clientConnection);

                    System.Console.WriteLine($"客户端已连接: {clientId}, 总连接数: {_clients.Count}");
                    
                    // 触发客户端连接事件
                    Application.Current?.Dispatcher.Invoke(() => ClientConnected?.Invoke(clientId));
                }
            }
            catch (ObjectDisposedException)
            {
                // 监听器被关闭，正常情况
                System.Console.WriteLine("TCP监听器已关闭");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"接受客户端连接时发生异常: {ex.Message}");
                UpdateStatus(ConnectionStatus.Error);
            }
        }

        /// <summary>
        /// 单个客户端的数据接收循环
        /// </summary>
        /// <param name="client">客户端连接对象</param>
        /// <param name="token">取消令牌</param>
        private async void ClientReceiveLoop(ClientConnection client, CancellationToken token)
        {
            var buffer = new byte[4096];
            
            try
            {
                while (!token.IsCancellationRequested && client.Stream.CanRead)
                {
                    int bytesRead = await client.Stream.ReadAsync(buffer, 0, buffer.Length, token);
                    
                    if (bytesRead > 0)
                    {
                        System.Console.WriteLine($"从客户端 {client.Id} 接收到 {bytesRead} 字节数据");
                        
                        // 解析消息并触发事件
                        var messages = _protocol.Decode(buffer, bytesRead);
                        foreach (var msg in messages)
                        {
                            // 在消息中添加来源客户端信息（如果消息结构支持）
                            Application.Current?.Dispatcher.Invoke(() => MessageReceived?.Invoke(msg));
                        }
                    }
                    else
                    {
                        // 客户端正常关闭连接
                        System.Console.WriteLine($"客户端 {client.Id} 断开连接");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Console.WriteLine($"客户端 {client.Id} 接收循环被取消");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"客户端 {client.Id} 接收数据时发生异常: {ex.Message}");
            }
            finally
            {
                // 移除并清理客户端连接
                if (_clients.TryRemove(client.Id, out _))
                {
                    CleanupClientConnection(client);
                    System.Console.WriteLine($"客户端 {client.Id} 已移除，剩余连接数: {_clients.Count}");
                    
                    // 触发客户端断开事件
                    Application.Current?.Dispatcher.Invoke(() => ClientDisconnected?.Invoke(client.Id));
                }
            }
        }

        /// <summary>
        /// 断开并清理客户端连接
        /// </summary>
        /// <param name="client">要断开的客户端连接</param>
        private void CleanupClientConnection(ClientConnection client)
        {
            try
            {
                client.CancelSource?.Cancel();
                client.Stream?.Close();
                client.TcpClient?.Close();
                client.CancelSource?.Dispose();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"清理客户端连接时发生异常: {ex.Message}");
            }
        }
        
        #endregion

        #region IDisposable 实现
        
        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            Close();
        }
        
        #endregion
    }
}