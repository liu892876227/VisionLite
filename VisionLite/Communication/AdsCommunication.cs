// Communication/AdsCommunication.cs
// 倍福ADS通讯核心实现类 - 集成到VisionLite统一通讯框架
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using TwinCAT.Ads;

namespace VisionLite.Communication
{
    /// <summary>
    /// 倍福ADS通讯实现类
    /// 实现ICommunication接口，集成到VisionLite统一通讯框架
    /// 基于经过验证的AdsClientService代码改进而成
    /// </summary>
    public class AdsCommunication : ICommunication
    {
        #region 私有字段

        /// <summary>
        /// TwinCAT ADS客户端对象
        /// </summary>
        private TcAdsClient _client;

        /// <summary>
        /// 变量句柄缓存，提高读写性能
        /// Key: 变量名, Value: 句柄值
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _handleCache = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// ADS连接配置
        /// </summary>
        private readonly AdsConnectionConfig _config;

        /// <summary>
        /// 资源释放标记
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 连接检测变量名（可选，用于心跳检测）
        /// </summary>
        private string _heartbeatVariable = "";

        #endregion

        #region ICommunication接口实现

        /// <summary>
        /// 通讯名称标识
        /// </summary>
        public string Name => $"ADS_{_config.DisplayName}_{_config.TargetAmsNetId.Replace(".", "_")}";

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;

        /// <summary>
        /// 消息接收事件（ADS主要用于数据读写，此事件用于兼容性）
        /// </summary>
        public event Action<Message> MessageReceived;

        /// <summary>
        /// 日志记录事件
        /// </summary>
        public event Action<string> LogReceived;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">ADS连接配置</param>
        public AdsCommunication(AdsConnectionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 验证配置有效性
            if (!_config.IsValid(out string errorMsg))
            {
                throw new ArgumentException($"ADS配置无效: {errorMsg}");
            }

            LogMessage($"ADS通讯模块已初始化: {_config.DisplayName}");
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 异步打开连接 - 简化版本，与test.cs保持一致
        /// </summary>
        public async Task<bool> OpenAsync()
        {
            try
            {
                if (Status == ConnectionStatus.Connected)
                {
                    LogMessage("ADS已连接，无需重复连接");
                    return true;
                }

                // 设置连接中状态
                SetStatus(ConnectionStatus.Connecting);
                LogMessage($"正在连接倍福PLC: {_config.TargetAmsNetId}:{_config.TargetAmsPort}");

                // 创建ADS客户端
                _client = new TcAdsClient();

                // 建立连接（使用与test.cs相同的同步连接方式，无超时限制）
                _client.Connect(_config.TargetAmsNetId, _config.TargetAmsPort);

                // 验证连接状态
                if (_client.IsConnected)
                {
                    SetStatus(ConnectionStatus.Connected);
                    LogMessage($"ADS连接成功！目标: {_config.TargetAmsNetId}:{_config.TargetAmsPort}");
                    
                    // 读取PLC状态作为连接验证
                    try
                    {
                        var state = _client.ReadState();
                        LogMessage($"PLC状态: {state.AdsState}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"读取PLC状态时出错: {ex.Message}");
                    }
                    
                    return true;
                }
                else
                {
                    throw new Exception("连接建立后状态检查失败");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"ADS连接失败: {ex.Message}");
                SetStatus(ConnectionStatus.Error);
                
                // 清理资源
                _client?.Dispose();
                _client = null;
                
                return false;
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            if (Status == ConnectionStatus.Disconnected) return;

            try
            {
                LogMessage("正在断开ADS连接...");

                // 清除句柄缓存
                foreach (var handle in _handleCache.Values)
                {
                    try
                    {
                        _client?.DeleteVariableHandle(handle);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"删除句柄时出错: {ex.Message}");
                    }
                }
                _handleCache.Clear();

                // 断开连接
                _client?.Dispose();
                _client = null;

                SetStatus(ConnectionStatus.Disconnected);
                LogMessage("ADS连接已断开");
            }
            catch (Exception ex)
            {
                LogMessage($"断开ADS连接时出错: {ex.Message}");
            }
        }

        #endregion

        #region 消息发送（兼容ICommunication接口）

        /// <summary>
        /// 发送消息（ADS通讯主要用于变量读写，此方法用于兼容性）
        /// </summary>
        public async Task<bool> SendAsync(Message message)
        {
            try
            {
                LogMessage($"收到ADS消息: {message.Command}");
                
                // 这里可以根据message内容执行相应的ADS操作
                // 例如：解析message.Data为变量名和值，然后调用WriteVariable
                
                // 触发消息接收事件（用于日志和调试）
                MessageReceived?.Invoke(message);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"处理ADS消息失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region ADS专用读写方法（继承原有功能）

        /// <summary>
        /// 检查连接状态
        /// </summary>
        public bool IsConnected => _client?.IsConnected ?? false;

        /// <summary>
        /// 读取PLC变量（泛型版本）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="variableName">变量名</param>
        /// <param name="value">读取的值</param>
        /// <returns>是否成功</returns>
        public bool ReadVariable<T>(string variableName, ref T value)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("ADS未连接，无法读取变量");
                    return false;
                }

                var handle = GetVariableHandle(variableName);
                value = (T)_client.ReadAny(handle, typeof(T));
                
                LogMessage($"读取变量成功: {variableName} = {value}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取变量失败 {variableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 读取PLC变量数组（一维）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="variableName">变量名</param>
        /// <param name="value">读取的数组</param>
        /// <returns>是否成功</returns>
        public bool ReadVariableArray<T>(string variableName, ref T[] value)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("ADS未连接，无法读取数组");
                    return false;
                }

                var handle = GetVariableHandle(variableName);
                value = (T[])_client.ReadAny(handle, typeof(T[]), new int[] { value.GetLength(0) });
                
                LogMessage($"读取数组成功: {variableName}, 长度: {value.Length}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取数组失败 {variableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 写入PLC变量
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="variableName">变量名</param>
        /// <param name="value">要写入的值</param>
        /// <returns>是否成功</returns>
        public bool WriteVariable<T>(string variableName, T value)
        {
            if (!IsConnected)
            {
                LogMessage("ADS未连接，无法写入变量");
                return false;
            }

            try
            {
                var handle = GetVariableHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                    LogMessage($"写入变量成功: {variableName} = {value}");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"写入变量失败 {variableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 写入PLC变量数组
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="variableName">变量名</param>
        /// <param name="value">要写入的数组</param>
        /// <returns>是否成功</returns>
        public bool WriteVariableArray<T>(string variableName, T[] value)
        {
            if (!IsConnected)
            {
                LogMessage("ADS未连接，无法写入数组");
                return false;
            }

            try
            {
                var handle = GetVariableHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                    LogMessage($"写入数组成功: {variableName}, 长度: {value.Length}");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"写入数组失败 {variableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据PLC数据类型写入变量（字符串转换版本）
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <param name="valueStr">字符串格式的值</param>
        /// <param name="plcType">PLC数据类型</param>
        /// <returns>是否成功</returns>
        public bool WriteVariableFromString(string variableName, string valueStr, string plcType)
        {
            try
            {
                // 这里需要实现字符串到PLC数据类型的转换
                // 可以参考原代码中的PLCData.ParseValueByType方法
                switch (plcType.ToUpper())
                {
                    case "BOOL":
                        return WriteVariable(variableName, bool.Parse(valueStr));
                    case "BYTE":
                        return WriteVariable(variableName, byte.Parse(valueStr));
                    case "INT":
                        return WriteVariable(variableName, short.Parse(valueStr));
                    case "DINT":
                        return WriteVariable(variableName, int.Parse(valueStr));
                    case "REAL":
                        return WriteVariable(variableName, float.Parse(valueStr));
                    case "LREAL":
                        return WriteVariable(variableName, double.Parse(valueStr));
                    case "STRING":
                        return WriteVariable(variableName, valueStr);
                    default:
                        LogMessage($"不支持的PLC数据类型: {plcType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"字符串转换写入失败 {variableName}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取或创建变量句柄（带缓存）- 基于test.cs成功实现
        /// </summary>
        private int GetVariableHandle(string variableName)
        {
            return _handleCache.GetOrAdd(variableName, name =>
            {
                try
                {
                    var handle = _client.CreateVariableHandle(name);
                    LogMessage($"创建变量句柄: {name} = {handle}");
                    return handle;
                }
                catch (Exception ex)
                {
                    LogMessage($"创建句柄失败 {name}: {ex.Message}");
                    throw;
                }
            });
        }


        /// <summary>
        /// 设置连接状态并触发事件
        /// </summary>
        /// <param name="status">新状态</param>
        private void SetStatus(ConnectionStatus status)
        {
            if (Status != status)
            {
                Status = status;
                StatusChanged?.Invoke(status);
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">日志内容</param>
        private void LogMessage(string message)
        {
            var logMsg = $"[ADS] {DateTime.Now:HH:mm:ss} {message}";
            LogReceived?.Invoke(logMsg);
        }

        /// <summary>
        /// 检查变量是否存在（基于test.cs成功实现）
        /// </summary>
        public bool VariableExists(string variableName)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("ADS未连接，无法检查变量");
                    return false;
                }

                LogMessage($"检查变量是否存在: {variableName}");
                var handle = _client.CreateVariableHandle(variableName);
                LogMessage($"变量存在，句柄: {handle}");
                _client.DeleteVariableHandle(handle);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"变量不存在: {variableName} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 简化的读取方法（基于test.cs成功实现）
        /// </summary>
        public bool Read<T>(string variableName, ref T value)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("ADS未连接，无法读取变量");
                    return false;
                }

                var handle = GetVariableHandle(variableName);
                value = (T)_client.ReadAny(handle, typeof(T));
                
                LogMessage($"读取成功: {variableName} = {value}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"读取 {variableName} 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 简化的写入方法（基于test.cs成功实现）
        /// </summary>
        public bool Write<T>(string variableName, T value)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("ADS未连接，无法写入变量");
                    return false;
                }

                var handle = GetVariableHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                    LogMessage($"写入成功: {variableName} = {value}");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"写入 {variableName} 失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            Close();
            _disposed = true;
        }

        #endregion
    }
}