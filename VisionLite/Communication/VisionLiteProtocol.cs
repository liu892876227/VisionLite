// Communication/VisionLiteProtocol.cs
// VisionLite自定义通讯协议实现
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VisionLite.Communication
{
    /// <summary>
    /// VisionLite系统的自定义通讯协议实现
    /// 实现了一个完整的帧协议，支持数据校验和消息边界检测
    /// 
    /// 协议帧格式：
    /// STX(1字节) + 长度(4字节) + 消息体(N字节) + CRC16(2字节) + ETX(1字节)
    /// 
    /// 协议特点：
    /// 1. 起始结束标记：用于帧同步和错误恢复
    /// 2. 长度字段：解决粘包和分包问题
    /// 3. CRC16校验：检测数据传输错误
    /// 4. 双模式支持：JSON格式和简单文本格式
    /// 
    /// 适用场景：
    /// - 工业以太网通讯
    /// - PLC与上位机通讯
    /// - 设备间协调通讯
    /// - 系统间集成通讯
    /// </summary>
    public class VisionLiteProtocol : IMessageProtocol
    {
        #region 协议常量定义
        
        /// <summary>
        /// 帧开始标记 - ASCII码中的STX(Start of Text)
        /// 用于标识数据帧的开始位置
        /// </summary>
        private const byte STX = 0x02;
        
        /// <summary>
        /// 帧结束标记 - ASCII码中的ETX(End of Text)
        /// 用于标识数据帧的结束位置
        /// </summary>
        private const byte ETX = 0x03;
        
        #endregion

        #region 私有字段
        
        /// <summary>
        /// 接收数据缓冲区，用于处理TCP流中的粘包和分包问题
        /// TCP是流式协议，数据可能一次收到多个消息，或者一个消息分多次收到
        /// </summary>
        private List<byte> _receiveBuffer = new List<byte>();
        
        #endregion

        #region IMessageProtocol 接口实现
        
        /// <summary>
        /// 将业务消息对象编码为可发送的字节帧
        /// 支持两种消息格式：JSON格式和简单文本格式
        /// </summary>
        /// <param name="message">要编码的业务消息</param>
        /// <returns>编码后的字节数组，可以直接通过网络发送</returns>
        public byte[] Encode(Message message)
        {
            byte[] messageBodyBytes;

            // 判断是编码JSON还是简单命令
            if (!string.IsNullOrEmpty(message.RawJsonBody))
            {
                messageBodyBytes = Encoding.UTF8.GetBytes(message.RawJsonBody);
            }
            else
            {
                var parts = new List<string> { message.Command };
                // 将Dictionary参数转换为 key=value 格式
                foreach (var param in message.Parameters)
                {
                    parts.Add($"{param.Key}={param.Value}");
                }
                parts.Add(message.Id);
                string simpleCommand = string.Join("|", parts);
                messageBodyBytes = Encoding.UTF8.GetBytes(simpleCommand);
            }

            int length = messageBodyBytes.Length;
            ushort crc = CalculateCrc16(messageBodyBytes);

            // STX(1) + Length(4) + Body(N) + CRC(2) + ETX(1)
            var frame = new List<byte>();
            frame.Add(STX);
            frame.AddRange(BitConverter.GetBytes(length));
            frame.AddRange(messageBodyBytes);
            frame.AddRange(BitConverter.GetBytes(crc));
            frame.Add(ETX);

            return frame.ToArray();
        }

        /// <summary>
        /// 从接收缓冲区中解析出完整的消息对象
        /// 处理TCP流中的粘包、分包问题，可能返回0个、1个或多个消息
        /// </summary>
        /// <param name="buffer">接收到的字节数组</param>
        /// <param name="bytesRead">本次实际接收到的字节数</param>
        /// <returns>解析成功的消息对象集合</returns>
        public IEnumerable<Message> Decode(byte[] buffer, int bytesRead)
        {
            // 调试信息：显示解码过程的详细信息
            System.Console.WriteLine($"=== 协议解码开始 ===");
            System.Console.WriteLine($"本次接收: {bytesRead}字节, 数据: {BitConverter.ToString(buffer, 0, bytesRead)}");
            System.Console.WriteLine($"解码时间: {DateTime.Now:HH:mm:ss.fff}");
            
            // 将新数据添加到接收缓冲区
            _receiveBuffer.AddRange(buffer.Take(bytesRead));
            System.Console.WriteLine($"缓冲区总长度: {_receiveBuffer.Count}字节");

            while (_receiveBuffer.Count > 8) // 8 = STX(1)+LEN(4)+CRC(2)+ETX(1) (最小帧长度)
            {
                // 1. 查找帧头
                int stxIndex = _receiveBuffer.IndexOf(STX);
                if (stxIndex == -1)
                {
                    System.Console.WriteLine("未找到STX帧头");
                    // 没找到帧头，清空缓冲区
                    _receiveBuffer.Clear();
                    break;
                }

                // 丢弃帧头前的无效数据
                if (stxIndex > 0)
                {
                    System.Console.WriteLine($"丢弃{stxIndex}字节无效数据");
                    _receiveBuffer.RemoveRange(0, stxIndex);
                }

                // 2. 检查是否有足够的数据来读取长度
                if (_receiveBuffer.Count < 5) break;

                // 3. 读取消息体长度
                int bodyLength = BitConverter.ToInt32(_receiveBuffer.ToArray(), 1);
                int frameLength = 1 + 4 + bodyLength + 2 + 1;
                System.Console.WriteLine($"期望帧长度：{frameLength}，缓冲区长度：{_receiveBuffer.Count}");
                // 4. 检查是否有足够的数据构成一个完整的帧
                if (_receiveBuffer.Count < frameLength) break;

                // 5. 提取一个完整的帧
                byte[] frame = _receiveBuffer.Take(frameLength).ToArray();
                _receiveBuffer.RemoveRange(0, frameLength);

                // 6. 校验帧尾
                byte etxByte = frame[frameLength - 1];
                System.Console.WriteLine($"ETX字节：0x{etxByte:X2}，期望：0x{ETX:X2}");
                if (etxByte != ETX)
                {
                    System.Console.WriteLine("ETX校验失败，丢弃帧");
                    continue;
                }

                // 7. 提取并校验CRC
                byte[] bodyBytes = frame.Skip(5).Take(bodyLength).ToArray();
                ushort receivedCrc = BitConverter.ToUInt16(frame, 5 + bodyLength);
                ushort calculatedCrc = CalculateCrc16(bodyBytes);

                System.Console.WriteLine($"接收CRC: {receivedCrc:X4}, 计算CRC: {calculatedCrc:X4}");
                System.Console.WriteLine($"消息体: {Encoding.UTF8.GetString(bodyBytes)}");

                if (receivedCrc != calculatedCrc)
                {
                    System.Console.WriteLine("CRC校验失败，丢弃数据包");
                    continue;
                }

                // 8. 解析消息体
                string bodyString = Encoding.UTF8.GetString(bodyBytes);
                Message message;

                if (bodyString.Trim().StartsWith("{"))
                {
                    try
                    {
                        JObject jsonObj = JObject.Parse(bodyString);
                        message = new Message();

                        // 提取命令名
                        message.Command = jsonObj["cmd"]?.ToString() ?? "UNKNOWN";

                        // 提取消息ID
                        message.Id = jsonObj["id"]?.ToString() ??
                            Guid.NewGuid().ToString("N").Substring(0, 8);

                        // 保存原始JSON
                        message.RawJsonBody = bodyString;

                        // 提取消息类型
                        if (jsonObj["type"] != null)
                        {
                            if (Enum.TryParse<MessageType>(jsonObj["type"].ToString(), true, out var msgType))
                            {
                                message.Type = msgType;
                            }
                        }

                        // 解析参数
                        if (jsonObj["params"] is JObject paramsObj)
                        {
                            foreach (var param in paramsObj)
                            {
                                object value;
                                var token = param.Value;

                                switch (token.Type)
                                {
                                    case JTokenType.String:
                                        value = token.ToString();
                                        break;
                                    case JTokenType.Integer:
                                        value = token.Value<int>();
                                        break;
                                    case JTokenType.Float:
                                        value = token.Value<double>();
                                        break;
                                    case JTokenType.Boolean:
                                        value = token.Value<bool>();
                                        break;
                                    case JTokenType.Null:
                                        value = null;
                                        break;
                                    default:
                                        value = token.ToString();
                                        break;
                                }

                                message.Parameters[param.Key] = value;
                            }
                        }

                        // 解析时间戳
                        if (jsonObj["timestamp"] != null)
                        {
                            if (DateTime.TryParse(jsonObj["timestamp"].ToString(), out var ts))
                            {
                                message.Timestamp = ts;
                            }
                        }
                    }
                    catch (Exception ex)  // Newtonsoft.Json抛出的是Exception而不是JsonException
                    {
                        message = new Message
                        {
                            Command = "JSON_PARSE_ERROR",
                            Type = MessageType.Event,
                            RawJsonBody = bodyString,
                            Parameters = new Dictionary<string, object>
                            {
                                ["error"] = ex.Message,
                                ["original_data"] = bodyString
                            }
                        };
                    }
                }
                else
                {
                    // 解析为简单命令格式
                    var parts = bodyString.Split('|');
                    if (parts.Length >= 2)
                    {
                        message = new Message
                        {
                            Command = parts[0],
                            Id = parts.Last()
                        };
                        
                        // 解析简单格式的参数：COMMAND|param1=value1|param2=value2|ID
                        var paramParts = parts.Skip(1).Take(parts.Length - 2);
                        foreach (var paramPart in paramParts)
                        {
                            if (paramPart.Contains("="))
                            {
                                var kvp = paramPart.Split('=');
                                if (kvp.Length == 2)
                                {
                                    message.Parameters[kvp[0]] = kvp[1];
                                }
                            }
                        }
                    }
                    else continue; // 格式错误，丢弃
                }

                // 解析成功，返回消息对象
                System.Console.WriteLine($"成功解析消息: {message}");
                yield return message;
            }
        }
        
        #endregion

        #region 私有辅助方法
        
        /// <summary>
        /// 计算CRC16校验值
        /// 使用CRC-16-CCITT算法，多项式0x1021，初始值0xFFFF
        /// 
        /// CRC校验的作用：
        /// 1. 检测数据传输过程中的错误
        /// 2. 可以检测单位错误和突发错误
        /// 3. 在工业环境中非常重要，可以及时发现电磁干扰
        /// 4. 算法快速，占用资源少
        /// </summary>
        /// <param name="bytes">需要计算CRC的字节数组</param>
        /// <returns>16位CRC校验值</returns>
        private ushort CalculateCrc16(byte[] bytes)
        {
            const ushort poly = 0x1021;    // CRC-16-CCITT多项式
            ushort crc = 0xFFFF;           // 标准初始值

            foreach (byte b in bytes)
            {
                // 将当前字节与CRC的高位进行异或操作
                crc ^= (ushort)(b << 8);
                
                // 对每个位进行处理
                for (int i = 0; i < 8; i++)
                {
                    bool isMostSignificantBitOne = (crc & 0x8000) != 0;
                    crc <<= 1;  // 左移一位
                    
                    // 如果最高位是1，则与多项式进行异或
                    if (isMostSignificantBitOne)
                    {
                        crc ^= poly;
                    }
                }
            }
            return crc;
        }
        
        #endregion
    }
}