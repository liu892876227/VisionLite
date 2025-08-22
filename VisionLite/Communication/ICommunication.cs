// Communication/ICommunication.cs
// 通讯系统的核心接口定义
using System;
using System.Threading.Tasks;

namespace VisionLite.Communication
{
    /// <summary>
    /// 定义通讯连接的状态枚举
    /// 在工业通讯中，清晰的状态管理对于故障诊断和用户体验至关重要
    /// </summary>
    public enum ConnectionStatus
    {
        Disconnected,  // 未连接：初始状态或已断开
        Connecting,    // 连接中：正在尝试建立连接
        Connected,     // 已连接：连接正常，可以收发数据
        Error          // 错误状态：连接失败或异常断开
    }

    /// <summary>
    /// 通讯接口的核心抽象
    /// 这个接口定义了所有通讯方式（TCP、UDP、串口等）必须实现的统一规范
    /// 使用这个接口的好处：
    /// 1. 业务层代码与具体通讯实现解耦
    /// 2. 便于单元测试（可以mock通讯层）
    /// 3. 支持运行时切换通讯方式
    /// 4. 便于扩展新的通讯协议
    /// </summary>
    public interface ICommunication : IDisposable
    {
        /// <summary>
        /// 获取通讯实例的唯一名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取当前连接状态
        /// </summary>
        ConnectionStatus Status { get; }

        /// <summary>
        /// 当连接状态发生变化时触发的事件
        /// </summary>
        event Action<ConnectionStatus> StatusChanged;

        /// <summary>
        /// 当接收到解析好的消息时触发的事件
        /// </summary>
        event Action<Message> MessageReceived; 

        /// <summary>
        /// 异步打开连接
        /// </summary>
        Task<bool> OpenAsync();

        /// <summary>
        /// 关闭连接
        /// </summary>
        void Close();

        /// <summary>
        /// 异步发送一个结构化的消息对象
        /// </summary>
        /// <param name="message">要发送的消息对象</param>
        Task<bool> SendAsync(Message message);
    }
}