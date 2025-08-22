// Communication/TcpCommunication.cs
// TCP通讯的具体实现类
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VisionLite.Communication
{
    /// <summary>
    /// TCP通讯的具体实现
    /// 提供基于TCP协议的可靠数据传输，适用于工业环境中的设备通讯
    /// 
    /// TCP的优势：
    /// 1. 可靠传输：自动重传丢失的数据包
    /// 2. 有序传输：保证数据按发送顺序到达
    /// 3. 流量控制：防止发送方发送过快
    /// 4. 广泛支持：几乎所有设备都支持TCP
    /// 
    /// 实现特点：
    /// - 异步操作：不阻塞UI线程
    /// - 自动重连：检测到断线后可重新连接
    /// - 协议无关：通过IMessageProtocol支持各种消息格式
    /// - 线程安全：支持多线程环境下的并发访问
    /// </summary>
    public class TcpCommunication : ICommunication
    {
        #region 私有字段
        
        /// <summary>
        /// TCP客户端对象，负责底层的Socket连接管理
        /// </summary>
        private TcpClient _client;
        
        /// <summary>
        /// 网络数据流，用于实际的数据读写操作
        /// </summary>
        private NetworkStream _stream;
        
        /// <summary>
        /// 目标服务器的IP地址
        /// </summary>
        private readonly string _ipAddress;
        
        /// <summary>
        /// 目标服务器的端口号
        /// </summary>
        private readonly int _port;
        
        /// <summary>
        /// 取消令牌源，用于优雅地停止后台接收线程
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 协议处理器，负责消息的封包和解包
        /// 通过依赖注入的方式支持不同的消息协议
        /// </summary>
        private readonly IMessageProtocol _protocol;

        /// <summary>
        /// 定义读取超时时间（毫秒）
        /// 在工业环境中，适当的超时设置可以及时发现网络异常
        /// </summary>
        private const int READ_TIMEOUT_MS = 5000; // 每次读取操作最多等待5秒
        
        #endregion

        #region 公共属性和事件
        
        /// <summary>
        /// 通讯实例的显示名称，便于在UI中区分多个连接
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 当前连接状态，只能通过内部方法修改以确保状态一致性
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 连接状态变化事件，UI可以订阅此事件来更新连接指示器
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// 消息接收事件，当收到完整的业务消息时触发
        /// 注意：事件在UI线程中触发，可以安全地更新界面
        /// </summary>
        public event Action<Message> MessageReceived;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 创建TCP通讯实例
        /// </summary>
        /// <param name="name">连接的显示名称，用于日志和UI显示</param>
        /// <param name="ipAddress">目标服务器IP地址</param>
        /// <param name="port">目标服务器端口号</param>
        public TcpCommunication(string name, string ipAddress, int port)
        {
            Name = name;
            _ipAddress = ipAddress;
            _port = port;
            // 实例化自定义的协议处理器
            // 在实际项目中，可以通过构造函数参数或依赖注入来支持不同协议
            _protocol = new VisionLiteProtocol();
        }
        
        #endregion

        #region 私有辅助方法
        
        /// <summary>
        /// 更新连接状态并触发状态变化事件
        /// 确保状态变化事件在UI线程中触发，避免跨线程操作异常
        /// </summary>
        /// <param name="newStatus">新的连接状态</param>
        private void UpdateStatus(ConnectionStatus newStatus)
        {
            if (Status == newStatus) return; // 避免重复的状态更新
            Status = newStatus;
            // 使用Dispatcher确保事件在UI线程中触发
            Application.Current?.Dispatcher.Invoke(() => StatusChanged?.Invoke(newStatus));
        }
        
        #endregion

        #region ICommunication 接口实现
        
        /// <summary>
        /// 异步打开TCP连接
        /// 支持超时控制，避免无限等待
        /// </summary>
        /// <returns>连接成功返回true，否则返回false</returns>
        public async Task<bool> OpenAsync()
        {
            if (Status == ConnectionStatus.Connected || Status == ConnectionStatus.Connecting)
                return true;

            try
            {
                UpdateStatus(ConnectionStatus.Connecting);
                _client = new TcpClient();
                // 尝试连接到指定的IP和端口
                await _client.ConnectAsync(_ipAddress, _port);

                if (_client.Connected)
                {
                    // 获取网络流用于数据传输
                    _stream = _client.GetStream();
                    // 创建取消令牌
                    _cancellationTokenSource = new CancellationTokenSource();
                    // 在后台线程开始接收和解析数据，不等待其完成
                    _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));
                    // 更新状态为已连接
                    UpdateStatus(ConnectionStatus.Connected);
                    return true;
                }

                Close();
                UpdateStatus(ConnectionStatus.Error);
                return false;
            }
            catch
            {
                Close();
                UpdateStatus(ConnectionStatus.Error);
                return false;
            }
        }

        /// <summary>
        /// 关闭TCP连接并清理所有资源
        /// 这个方法是线程安全的，可以在任何线程中调用
        /// </summary>
        public void Close()
        {
            // 优雅地停止接收线程
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            // 关闭网络流和TCP客户端
            _stream?.Close();
            _client?.Close();

            // 清理资源，避免内存泄漏
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _stream = null;
            _client = null;

            // 更新状态
            UpdateStatus(ConnectionStatus.Disconnected);
        }

        /// <summary>
        /// 异步发送消息
        /// 自动处理消息编码和网络传输
        /// </summary>
        /// <param name="message">要发送的业务消息对象</param>
        /// <returns>发送成功返回true，否则返回false</returns>
        public async Task<bool> SendAsync(Message message)
        {
            // 检查连接状态和流的可用性
            if (Status != ConnectionStatus.Connected || _stream == null || !_stream.CanWrite)
            {
                return false;
            }

            try
            {
                // 使用协议处理器将Message对象编码成字节帧
                byte[] frame = _protocol.Encode(message);
                // 异步发送数据，不阻塞当前线程
                await _stream.WriteAsync(frame, 0, frame.Length);
                return true;
            }
            catch
            {
                // 发送失败，可能是网络断开或其他异常
                UpdateStatus(ConnectionStatus.Error);
                Close();
                return false;
            }
        }
        
        #endregion

        #region 私有接收处理方法
        
        /// <summary>
        /// 后台接收数据的循环方法
        /// 持续监听网络流，接收数据并解析成消息对象
        /// </summary>
        /// <param name="token">取消令牌，用于优雅停止接收循环</param>
        private async void ReceiveLoop(CancellationToken token)
        {
            // 接收缓冲区，4KB通常足够大部分消息
            var buffer = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 异步读取数据，支持取消操作
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    
                    if (bytesRead > 0)
                    {
                        // 调试输出：显示接收到的原始字节数据
                        System.Console.WriteLine($"TCP接收到 {bytesRead} 字节数据: {BitConverter.ToString(buffer, 0, bytesRead)}");
                        
                        // 使用协议处理器解码，可能会得到0个、1个或多个完整的消息
                        // 协议层会处理粘包、分包等问题
                        var messages = _protocol.Decode(buffer, bytesRead);
                        foreach (var msg in messages)
                        {
                            // 通过事件将解析好的Message对象抛出
                            // 确保事件在UI线程中触发
                            Application.Current?.Dispatcher.Invoke(() => MessageReceived?.Invoke(msg));
                        }
                    }
                    else
                    {
                        // 对方正常关闭连接，ReadAsync返回0
                        // 这通常表示对方主动断开了连接
                        System.Console.WriteLine("TCP连接被对方关闭");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭，通过CancellationToken取消的操作
                System.Console.WriteLine("TCP接收循环被正常取消");
            }
            catch (Exception ex)
            {
                // 捕获所有其他异常（IO异常、Socket异常等），都视为连接错误
                System.Console.WriteLine($"TCP接收循环异常: {ex.Message}");
                UpdateStatus(ConnectionStatus.Error);
            }
            finally
            {
                // 无论如何都要清理资源
                Close();
            }
        }
        
        #endregion

        #region IDisposable 实现
        
        /// <summary>
        /// 释放所有资源
        /// 实现IDisposable接口，支持using语句的自动资源管理
        /// </summary>
        public void Dispose()
        {
            Close();
        }
        
        #endregion
    }
}