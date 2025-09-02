// Communication/S7ConnectionConfig.cs
// 西门子S7 PLC连接配置类
using System;
using Newtonsoft.Json;
using S7.Net;

namespace VisionLite.Communication
{
    /// <summary>
    /// 西门子S7 PLC连接配置类
    /// 支持S7-200/300/400/1200/1500系列PLC的连接参数配置
    /// </summary>
    public class S7ConnectionConfig
    {
        #region 基础连接参数

        /// <summary>
        /// 显示名称，用于标识不同的PLC连接
        /// </summary>
        public string DisplayName { get; set; } = "西门子PLC";

        /// <summary>
        /// PLC的IP地址
        /// </summary>
        public string IpAddress { get; set; } = "192.168.1.10";

        /// <summary>
        /// 机架号，通常为0
        /// S7-200: 0, S7-300: 0, S7-400: 0, S7-1200: 0, S7-1500: 0
        /// </summary>
        public int Rack { get; set; } = 0;

        /// <summary>
        /// 槽位号，根据PLC型号不同
        /// S7-200: 1, S7-300: 2, S7-400: 3, S7-1200: 1, S7-1500: 1
        /// </summary>
        public int Slot { get; set; } = 1;

        /// <summary>
        /// CPU类型，决定通讯协议的具体实现
        /// </summary>
        public CpuType CpuType { get; set; } = CpuType.S71500;

        #endregion

        #region 高级连接参数

        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// 读写超时时间（毫秒）
        /// </summary>
        public int ReadWriteTimeout { get; set; } = 3000;

        /// <summary>
        /// 是否启用心跳检测
        /// </summary>
        public bool EnableHeartbeat { get; set; } = true;

        /// <summary>
        /// 心跳检测间隔（毫秒）
        /// </summary>
        public int HeartbeatInterval { get; set; } = 30000;

        /// <summary>
        /// 心跳检测使用的PLC变量地址，为空则使用连接状态检测
        /// 例如: "DB1.DBX0.0" 或 "M0.0"
        /// </summary>
        public string HeartbeatAddress { get; set; } = "";

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// 自动重连最大尝试次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// 自动重连间隔（毫秒）
        /// </summary>
        public int ReconnectInterval { get; set; } = 10000;

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public S7ConnectionConfig()
        {
        }

        /// <summary>
        /// 构造函数，指定基本连接参数
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="ipAddress">PLC IP地址</param>
        /// <param name="cpuType">CPU类型</param>
        /// <param name="rack">机架号</param>
        /// <param name="slot">槽位号</param>
        public S7ConnectionConfig(string displayName, string ipAddress, CpuType cpuType, int rack = 0, int slot = 1)
        {
            DisplayName = displayName;
            IpAddress = ipAddress;
            CpuType = cpuType;
            Rack = rack;
            Slot = slot;

            // 根据CPU类型设置默认槽位号
            SetDefaultSlotByCpuType();
        }

        #endregion

        #region 验证方法

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        /// <returns>配置是否有效</returns>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            // 验证显示名称
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                errorMessage = "显示名称不能为空";
                return false;
            }

            // 验证IP地址
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                errorMessage = "IP地址不能为空";
                return false;
            }

            // 简单的IP地址格式验证
            if (!System.Net.IPAddress.TryParse(IpAddress, out _))
            {
                errorMessage = $"IP地址格式无效: {IpAddress}";
                return false;
            }

            // 验证机架和槽位参数
            if (Rack < 0 || Rack > 7)
            {
                errorMessage = $"机架号无效，应在0-7范围内: {Rack}";
                return false;
            }

            if (Slot < 0 || Slot > 31)
            {
                errorMessage = $"槽位号无效，应在0-31范围内: {Slot}";
                return false;
            }

            // 验证超时参数
            if (ConnectionTimeout <= 0 || ConnectionTimeout > 60000)
            {
                errorMessage = $"连接超时时间无效，应在1-60000毫秒范围内: {ConnectionTimeout}";
                return false;
            }

            if (ReadWriteTimeout <= 0 || ReadWriteTimeout > 30000)
            {
                errorMessage = $"读写超时时间无效，应在1-30000毫秒范围内: {ReadWriteTimeout}";
                return false;
            }

            // 验证心跳参数
            if (EnableHeartbeat)
            {
                if (HeartbeatInterval <= 0 || HeartbeatInterval > 300000)
                {
                    errorMessage = $"心跳间隔无效，应在1-300000毫秒范围内: {HeartbeatInterval}";
                    return false;
                }
            }

            // 验证重连参数
            if (EnableAutoReconnect)
            {
                if (MaxReconnectAttempts <= 0 || MaxReconnectAttempts > 100)
                {
                    errorMessage = $"重连尝试次数无效，应在1-100范围内: {MaxReconnectAttempts}";
                    return false;
                }

                if (ReconnectInterval <= 0 || ReconnectInterval > 600000)
                {
                    errorMessage = $"重连间隔无效，应在1-600000毫秒范围内: {ReconnectInterval}";
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据CPU类型设置默认槽位号
        /// </summary>
        private void SetDefaultSlotByCpuType()
        {
            switch (CpuType)
            {
                case CpuType.S7200:
                    Slot = 1;
                    break;
                case CpuType.S7300:
                    Slot = 2;
                    break;
                case CpuType.S7400:
                    Slot = 3;
                    break;
                case CpuType.S71200:
                case CpuType.S71500:
                    Slot = 1;
                    break;
                default:
                    Slot = 1;
                    break;
            }
        }

        /// <summary>
        /// 获取CPU类型的显示名称
        /// </summary>
        /// <returns>CPU类型显示名称</returns>
        public string GetCpuTypeDisplayName()
        {
            switch (CpuType)
            {
                case CpuType.S7200:
                    return "S7-200";
                case CpuType.S7300:
                    return "S7-300";
                case CpuType.S7400:
                    return "S7-400";
                case CpuType.S71200:
                    return "S7-1200";
                case CpuType.S71500:
                    return "S7-1500";
                default:
                    return CpuType.ToString();
            }
        }

        /// <summary>
        /// 克隆当前配置
        /// </summary>
        /// <returns>配置副本</returns>
        public S7ConnectionConfig Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<S7ConnectionConfig>(json);
        }

        /// <summary>
        /// 获取连接描述信息
        /// </summary>
        /// <returns>连接描述</returns>
        public override string ToString()
        {
            return $"{DisplayName} ({GetCpuTypeDisplayName()}) - {IpAddress}:{Rack}.{Slot}";
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建S7-1500的默认配置
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="ipAddress">IP地址</param>
        /// <returns>S7-1500配置</returns>
        public static S7ConnectionConfig CreateS71500Config(string displayName, string ipAddress)
        {
            return new S7ConnectionConfig(displayName, ipAddress, CpuType.S71500, 0, 1);
        }

        /// <summary>
        /// 创建S7-1200的默认配置
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="ipAddress">IP地址</param>
        /// <returns>S7-1200配置</returns>
        public static S7ConnectionConfig CreateS71200Config(string displayName, string ipAddress)
        {
            return new S7ConnectionConfig(displayName, ipAddress, CpuType.S71200, 0, 1);
        }

        /// <summary>
        /// 创建S7-300的默认配置
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="ipAddress">IP地址</param>
        /// <returns>S7-300配置</returns>
        public static S7ConnectionConfig CreateS7300Config(string displayName, string ipAddress)
        {
            return new S7ConnectionConfig(displayName, ipAddress, CpuType.S7300, 0, 2);
        }

        #endregion
    }
}