// Communication/ITcpServer.cs
// TCP服务器专用接口定义
using System;
using System.Threading.Tasks;

namespace VisionLite.Communication
{
    /// <summary>
    /// TCP服务器专用接口
    /// 扩展了基础的ICommunication接口，添加了服务器特有的功能
    /// 
    /// 服务器特有功能包括：
    /// 1. 客户端连接管理：监控客户端的连接和断开
    /// 2. 选择性发送：可以向特定客户端发送消息
    /// 3. 客户端统计：获取连接的客户端数量和信息
    /// 4. 广播功能：向所有客户端发送消息
    /// </summary>
    public interface ITcpServer : ICommunication
    {
        /// <summary>
        /// 获取当前连接的客户端数量
        /// 用于监控服务器负载和连接状态
        /// </summary>
        int ClientCount { get; }

        /// <summary>
        /// 客户端连接事件
        /// 当有新客户端连接到服务器时触发
        /// </summary>
        /// <param name="clientId">连接的客户端唯一标识</param>
        event Action<string> ClientConnected;

        /// <summary>
        /// 客户端断开事件
        /// 当客户端断开连接时触发
        /// </summary>
        /// <param name="clientId">断开的客户端唯一标识</param>
        event Action<string> ClientDisconnected;

        /// <summary>
        /// 向指定客户端发送消息
        /// 与SendAsync不同，这个方法只向特定客户端发送消息，而不是广播
        /// </summary>
        /// <param name="clientId">目标客户端的唯一标识</param>
        /// <param name="message">要发送的消息对象</param>
        /// <returns>发送成功返回true，否则返回false</returns>
        Task<bool> SendToClientAsync(string clientId, Message message);

        /// <summary>
        /// 断开指定客户端的连接
        /// 允许服务器主动断开特定客户端
        /// </summary>
        /// <param name="clientId">要断开的客户端标识</param>
        /// <returns>操作成功返回true，否则返回false</returns>
        bool DisconnectClient(string clientId);

        /// <summary>
        /// 获取所有连接客户端的信息
        /// 返回客户端ID列表，用于UI显示或管理
        /// </summary>
        /// <returns>客户端ID的字符串数组</returns>
        string[] GetConnectedClients();
    }
}