using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VisionLite.Communication
{
    /// <summary>
    /// 消息构建器工厂类
    /// 提供便捷的方法来创建各种类型的消息，避免重复的样板代码
    /// 使用工厂模式的好处：
    /// 1. 简化消息创建过程
    /// 2. 确保消息格式的一致性
    /// 3. 减少创建消息时的出错概率
    /// 4. 便于统一修改消息格式
    /// </summary>
    public static class MessageBuilder
    {
        /// <summary>
        /// 创建命令类型的消息
        /// 用于向设备或服务发送操作指令
        /// </summary>
        /// <param name="command">命令名称，如"TRIGGER"、"SET_PARAMS"</param>
        /// <param name="parameters">参数列表，使用元组简化语法</param>
        /// <returns>构建好的命令消息</returns>
        /// <example>
        /// // 创建触发命令
        /// var triggerMsg = MessageBuilder.CreateCommand("TRIGGER");
        ///
        /// // 创建带参数的设置命令
        /// var setMsg = MessageBuilder.CreateCommand("SET_PARAMS",
        ///     ("ExposureTime", 1000),
        ///     ("Gain", 2.5));
        /// </example>
        public static Message CreateCommand(string command, params (string key, object value)[] parameters)
        {
            // 创建消息对象并设置基本属性
            var msg = new Message
            {
                Command = command,
                Type = MessageType.Command
            };

            // 遍历所有参数并添加到字典中
            foreach (var (key, value) in parameters)
            {
                msg.Parameters[key] = value;
            }

            return msg;
        }

        /// <summary>
        /// 创建响应类型的消息
        /// 用于回复收到的命令，告知执行结果
        /// </summary>
        /// <param name="originalId">原始命令的ID，用于消息匹配</param>
        /// <param name="result">执行结果描述</param>
        /// <param name="success">是否执行成功</param>
        /// <param name="additionalData">额外的返回数据</param>
        /// <returns>构建好的响应消息</returns>
        /// <example>
        /// // 成功响应
        /// var okResponse = MessageBuilder.CreateResponse("A1B2C3D4", "操作完成", true);
        ///
        /// // 失败响应
        /// var errorResponse = MessageBuilder.CreateResponse("A1B2C3D4", "参数错误", false);
        ///
        /// // 带数据的响应
        /// var dataResponse = MessageBuilder.CreateResponse("A1B2C3D4", "获取成功", true,
        ///     ("count", 5), ("items", new[] {"A", "B", "C"}));
        /// </example>
        public static Message CreateResponse(string originalId, string result, bool success = true,
            params (string key, object value)[] additionalData)
        {
            var msg = new Message
            {
                Command = success ? "RESPONSE_OK" : "RESPONSE_ERROR",// 根据成功与否设置命令
                Type = MessageType.Response,// 设置为响应类型
                Parameters = new Dictionary<string, object>
                {
                    ["original_id"] = originalId,  // 关联原始命令
                    ["result"] = result,           // 结果描述
                    ["success"] = success,         // 成功标志
                    ["timestamp"] = DateTime.Now   // 响应时间
                }
            };

            // 添加额外数据
            foreach (var (key, value) in additionalData)
            {
                msg.Parameters[key] = value;
            }

            return msg;
        }

        /// <summary>
        /// 创建事件通知消息
        /// 用于主动通知状态变化，如设备连接/断开、检测完成等
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件相关数据</param>
        /// <returns>构建好的事件消息</returns>
        /// <example>
        /// // 设备状态事件
        /// var deviceEvent = MessageBuilder.CreateEvent("DEVICE_STATUS_CHANGED",
        ///     ("device_id", "Camera_01"),
        ///     ("status", "Connected"));
        ///
        /// // 检测完成事件
        /// var inspectionEvent = MessageBuilder.CreateEvent("INSPECTION_COMPLETED",
        ///     ("result", "PASS"),
        ///     ("score", 95.6));
        /// </example>
        public static Message CreateEvent(string eventName, params (string key, object value)[] data)
        {
            var msg = new Message
            {
                Command = eventName,
                Type = MessageType.Event
            };

            foreach (var (key, value) in data)
            {
                msg.Parameters[key] = value;
            }

            return msg;
        }

        /// <summary>
        /// 创建心跳消息
        /// 用于保持连接活跃，检测网络状态
        /// </summary>
        /// <param name="includeSystemInfo">是否包含系统信息</param>
        /// <returns>构建好的心跳消息</returns>
        /// <example>
        /// // 简单心跳
        /// var heartbeat = MessageBuilder.CreateHeartbeat();
        ///
        /// // 带系统信息的心跳
        /// var detailedHeartbeat = MessageBuilder.CreateHeartbeat(true);
        /// </example>
        public static Message CreateHeartbeat(bool includeSystemInfo = false)
        {
            var msg = new Message
            {
                Command = "HEARTBEAT",
                Type = MessageType.Heartbeat
            };

            if (includeSystemInfo)
            {
                // 添加系统状态信息，便于远程监控
                msg.Parameters["cpu_usage"] = GetCpuUsage();
                msg.Parameters["memory_usage"] = GetMemoryUsage();
                msg.Parameters["uptime"] = Environment.TickCount;
            }

            return msg;
        }

        /// <summary>
        /// 创建JSON格式的消息
        /// 用于复杂数据结构或与第三方系统兼容
        /// </summary>
        /// <param name="command">命令名称</param>
        /// <param name="data">要序列化为JSON的数据对象</param>
        /// <returns>构建好的JSON消息</returns>
        /// <example>
        /// // 复杂数据的JSON消息
        /// var complexMsg = MessageBuilder.CreateJsonMessage("BATCH_OPERATION", new {
        ///     operations = new[] {
        ///         new { type = "SET", param = "ExposureTime", value = 1000 },
        ///         new { type = "SET", param = "Gain", value = 2.5 },
        ///         new { type = "TRIGGER" }
        ///     },
        ///     mode = "sequential"
        /// });
        /// </example>
        public static Message CreateJsonMessage(string command, object data)
        {
            var jsonData = new
            {
                cmd = command,
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                @params = data  // @params是C#关键字转义
            };

            return new Message
            {
                Command = command,
                Type = MessageType.Command,
                RawJsonBody = JsonConvert.SerializeObject(jsonData, Formatting.None)
                
            };
        }

        // 辅助方法：获取CPU使用率（简化版）
        private static double GetCpuUsage()
        {
            // 实际项目中可以使用PerformanceCounter或其他方法获取真实CPU使用率
            // 这里返回随机值作为示例
            return new Random().NextDouble() * 100;
        }

        // 辅助方法：获取内存使用率（简化版）
        private static double GetMemoryUsage()
        {
            // 获取当前进程的内存使用量
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var usedMemoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
            return usedMemoryMB;
        }
    }
}