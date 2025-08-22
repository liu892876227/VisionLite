// Communication/IMessageProtocol.cs
// 消息协议处理接口定义
using System;
using System.Collections.Generic;

namespace VisionLite.Communication
{
    /// <summary>
    /// 消息协议处理接口
    /// 负责业务消息对象与网络传输字节流之间的转换
    /// 
    /// 为什么需要协议层：
    /// 1. 网络传输的是字节流，需要定义消息边界（帧同步）
    /// 2. 需要处理粘包、分包问题
    /// 3. 可能需要校验、压缩、加密等功能
    /// 4. 支持不同的序列化格式（JSON、XML、二进制等）
    /// 
    /// 常见的帧格式：
    /// - 固定长度：每条消息都是固定字节数
    /// - 分隔符：用特殊字符（如\n）分隔消息
    /// - 长度前缀：消息头包含数据长度信息
    /// - 起止标记：消息有固定的开始和结束标记
    /// </summary>
    public interface IMessageProtocol
    {
        /// <summary>
        /// 将业务消息对象编码成待发送的字节帧
        /// </summary>
        /// <param name="message">业务消息</param>
        /// <returns>编码后的字节数组</returns>
        byte[] Encode(Message message);

        /// <summary>
        /// 尝试从接收到的字节流中解码出一个或多个完整的消息帧
        /// </summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="bytesRead">本次新读取的字节数</param>
        /// <returns>解码出的完整消息列表</returns>
        IEnumerable<Message> Decode(byte[] buffer, int bytesRead);
    }
}