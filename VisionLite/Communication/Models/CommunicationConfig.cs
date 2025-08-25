// Communication/Models/CommunicationConfig.cs
// 通讯配置模型 - 存储通讯连接的完整配置信息
using System;
using System.Collections.Generic;

namespace VisionLite.Communication.Models
{
    /// <summary>
    /// 通讯连接配置模型
    /// 用于存储和传递通讯连接的完整配置信息
    /// </summary>
    public class CommunicationConfig
    {
        /// <summary>
        /// 连接的唯一名称，用于标识和管理连接
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 协议类型标识符，对应注册的协议类型
        /// </summary>
        public string ProtocolType { get; set; }

        /// <summary>
        /// 协议显示名称，用于界面显示
        /// </summary>
        public string ProtocolDisplayName { get; set; }

        /// <summary>
        /// 连接参数字典，存储协议特定的配置参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 配置创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModifiedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 配置描述信息
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 是否启用自动连接（程序启动时自动连接）
        /// </summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连间隔时间（秒）
        /// </summary>
        public int ReconnectInterval { get; set; } = 5;

        /// <summary>
        /// 最大重连次数（-1表示无限制）
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = -1;

        /// <summary>
        /// 配置是否已保存到文件
        /// </summary>
        public bool IsSaved { get; set; } = false;

        /// <summary>
        /// 获取指定参数的字符串值
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public string GetStringParameter(string key, string defaultValue = "")
        {
            if (Parameters.TryGetValue(key, out object value) && value != null)
            {
                return value.ToString();
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取指定参数的整数值
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public int GetIntParameter(string key, int defaultValue = 0)
        {
            if (Parameters.TryGetValue(key, out object value))
            {
                if (value is int intValue)
                    return intValue;
                if (int.TryParse(value?.ToString(), out int parsedValue))
                    return parsedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取指定参数的双精度浮点数值
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public double GetDoubleParameter(string key, double defaultValue = 0.0)
        {
            if (Parameters.TryGetValue(key, out object value))
            {
                if (value is double doubleValue)
                    return doubleValue;
                if (double.TryParse(value?.ToString(), out double parsedValue))
                    return parsedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取指定参数的布尔值
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public bool GetBoolParameter(string key, bool defaultValue = false)
        {
            if (Parameters.TryGetValue(key, out object value))
            {
                if (value is bool boolValue)
                    return boolValue;
                if (bool.TryParse(value?.ToString(), out bool parsedValue))
                    return parsedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置参数值
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <param name="value">参数值</param>
        public void SetParameter(string key, object value)
        {
            Parameters[key] = value;
            LastModifiedTime = DateTime.Now;
            IsSaved = false; // 标记为未保存状态
        }

        /// <summary>
        /// 移除指定参数
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveParameter(string key)
        {
            bool result = Parameters.Remove(key);
            if (result)
            {
                LastModifiedTime = DateTime.Now;
                IsSaved = false;
            }
            return result;
        }

        /// <summary>
        /// 检查是否包含指定参数
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <returns>是否包含该参数</returns>
        public bool HasParameter(string key)
        {
            return Parameters.ContainsKey(key);
        }

        /// <summary>
        /// 清空所有参数
        /// </summary>
        public void ClearParameters()
        {
            Parameters.Clear();
            LastModifiedTime = DateTime.Now;
            IsSaved = false;
        }

        /// <summary>
        /// 获取配置的简要信息字符串
        /// </summary>
        /// <returns>配置信息字符串</returns>
        public string GetSummary()
        {
            var summary = $"{Name} ({ProtocolDisplayName ?? ProtocolType})";
            
            // 根据协议类型添加关键信息
            switch (ProtocolType?.ToUpper())
            {
                case "TCP_CLIENT":
                    var ip = GetStringParameter("IP", "");
                    var port = GetIntParameter("Port", 0);
                    if (!string.IsNullOrEmpty(ip) && port > 0)
                    {
                        summary += $" -> {ip}:{port}";
                    }
                    break;
                
                case "TCP_SERVER":
                    var serverPort = GetIntParameter("Port", 0);
                    if (serverPort > 0)
                    {
                        summary += $" :{serverPort}";
                    }
                    break;
                
                case "UDP":
                    var udpPort = GetIntParameter("Port", 0);
                    if (udpPort > 0)
                    {
                        summary += $" UDP:{udpPort}";
                    }
                    break;
            }

            return summary;
        }

        /// <summary>
        /// 创建配置的深拷贝
        /// </summary>
        /// <returns>配置的副本</returns>
        public CommunicationConfig Clone()
        {
            var clone = new CommunicationConfig
            {
                Name = this.Name,
                ProtocolType = this.ProtocolType,
                ProtocolDisplayName = this.ProtocolDisplayName,
                Status = this.Status,
                CreatedTime = this.CreatedTime,
                LastModifiedTime = this.LastModifiedTime,
                Description = this.Description,
                AutoConnect = this.AutoConnect,
                AutoReconnect = this.AutoReconnect,
                ReconnectInterval = this.ReconnectInterval,
                MaxReconnectAttempts = this.MaxReconnectAttempts,
                IsSaved = this.IsSaved
            };

            // 深拷贝参数字典
            foreach (var param in this.Parameters)
            {
                clone.Parameters[param.Key] = param.Value;
            }

            return clone;
        }

        /// <summary>
        /// 重写ToString方法，返回配置的字符串表示
        /// </summary>
        /// <returns>配置的字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        /// <summary>
        /// 验证配置的完整性
        /// </summary>
        /// <param name="protocolDefinitions">协议参数定义列表</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateConfig(List<ParameterDefinition> protocolDefinitions)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return new ValidationResult(false, "连接名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(ProtocolType))
            {
                return new ValidationResult(false, "协议类型不能为空");
            }

            // 验证所有参数
            if (protocolDefinitions != null)
            {
                foreach (var paramDef in protocolDefinitions)
                {
                    if (Parameters.TryGetValue(paramDef.Key, out object value))
                    {
                        var paramValidation = paramDef.ValidateValue(value);
                        if (!paramValidation.IsValid)
                        {
                            return paramValidation;
                        }
                    }
                    else if (paramDef.IsRequired)
                    {
                        return new ValidationResult(false, $"缺少必需参数: {paramDef.DisplayName}");
                    }
                }
            }

            return new ValidationResult(true);
        }
    }
}