// Communication/Message.cs
using System;
using System.Collections.Generic;

namespace VisionLite.Communication
{
    /// <summary>
    /// 表示通讯系统中的一个消息对象
    /// 这个类是整个通讯协议的核心数据结构，用于封装所有类型的消息
    /// </summary>
    public class Message
    {
        /// <summary>
        /// 消息命令名称，如：
        /// "TRIGGER" - 触发相机拍照
        /// "SET_PARAMS" - 设置设备参数
        /// "GET_RESULT" - 获取检测结果
        /// "STOP" - 停止当前操作
        /// 这是消息的核心标识，告诉接收方要执行什么操作
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// 消息的唯一标识符，用于追踪和响应匹配
        /// 自动生成8位随机ID，避免手动管理ID的麻烦
        /// 例如：发送GET_RESULT命令时ID为"A1B2C3D4"，响应时也会包含这个ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// 消息创建的时间戳，用于调试、日志记录和超时检测
        /// 在工业环境中，知道消息何时发送非常重要，便于故障排查
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 消息参数字典，支持各种数据类型（int、double、bool、string等）
        /// 改用Dictionary<string, object>而不是List<string>的原因：
        /// 1. 支持键值对，更清晰：["ExposureTime"] = 1000
        /// 2. 支持多种数据类型：数字、布尔值、字符串等
        /// 3. 便于JSON序列化和反序列化
        /// 4. 便于参数验证和类型转换
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 消息类型，用于区分不同用途的消息
        /// Command: 客户端发送的操作指令
        /// Response: 服务端返回的响应结果
        /// Event: 状态变化通知（如相机断线、检测完成）
        /// Heartbeat: 心跳包，用于保持连接
        /// </summary>
        public MessageType Type { get; set; } = MessageType.Command;

        /// <summary>
        /// 原始JSON字符串，当消息使用JSON格式时保存原始内容
        /// 用于调试和特殊场景下的自定义解析
        /// </summary>
        public string RawJsonBody { get; set; }

        /// <summary>
        /// 便于调试的字符串表示
        /// 在调试器中查看消息对象时，能快速了解消息内容
        /// </summary>
        public override string ToString()
        {
            return $"[{Type}] {Command} (ID:{Id}) - {Parameters.Count} params @ {Timestamp:HH:mm:ss.fff}";
        }

        // 便捷的参数访问方法，避免类型转换的麻烦

        /// <summary>
        /// 获取整数类型的参数值
        /// 示例：int exposure = message.GetIntParameter("ExposureTime", 1000);
        /// </summary>
        /// <param name="key">参数名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值或默认值</returns>
        public int GetIntParameter(string key, int defaultValue = 0)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is int intVal) return intVal;
                if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取浮点数类型的参数值
        /// 示例：double gain = message.GetDoubleParameter("Gain", 1.0);
        /// </summary>
        public double GetDoubleParameter(string key, double defaultValue = 0.0)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is double doubleVal) return doubleVal;
                if (double.TryParse(value.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取字符串类型的参数值
        /// 示例：string mode = message.GetStringParameter("Mode", "Auto");
        /// </summary>
        public string GetStringParameter(string key, string defaultValue = "")
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取布尔类型的参数值
        /// 示例：bool enabled = message.GetBoolParameter("Enabled", false);
        /// </summary>
        public bool GetBoolParameter(string key, bool defaultValue = false)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is bool boolVal) return boolVal;
                if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// 消息类型枚举
    /// 在工业通讯中，明确区分消息类型有助于：
    /// 1. 消息路由和处理
    /// 2. 优先级管理（心跳最高，事件其次，普通命令最低）
    /// 3. 统计和监控
    /// 4. 错误处理和重试策略
    /// </summary>
    public enum MessageType
    {
        Command,    // 命令：要求对方执行某个操作
        Response,   // 响应：对命令的回复
        Event,      // 事件：状态变化通知
        Heartbeat   // 心跳：保持连接活跃
    }
}