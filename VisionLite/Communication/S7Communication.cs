// Communication/S7Communication.cs
// 西门子S7 PLC通讯核心实现类 - 集成到VisionLite统一通讯框架
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using S7.Net;

namespace VisionLite.Communication
{
    /// <summary>
    /// 西门子S7 PLC通讯实现类
    /// 实现ICommunication接口，集成到VisionLite统一通讯框架
    /// 基于S7netplus开源库实现，支持S7-200/300/400/1200/1500全系列
    /// </summary>
    public class S7Communication : ICommunication
    {
        #region 私有字段

        /// <summary>
        /// S7netplus核心PLC连接对象
        /// </summary>
        private Plc _plc;

        /// <summary>
        /// S7连接配置参数
        /// </summary>
        private readonly S7ConnectionConfig _config;

        /// <summary>
        /// 数据缓存字典，减少重复读取
        /// Key: 地址字符串, Value: 缓存的数据值
        /// </summary>
        private readonly ConcurrentDictionary<string, object> _dataCache = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 心跳检测定时器
        /// </summary>
        private Timer _heartbeatTimer;

        /// <summary>
        /// 自动重连定时器
        /// </summary>
        private Timer _reconnectTimer;

        /// <summary>
        /// 当前重连尝试次数
        /// </summary>
        private int _reconnectAttemptCount = 0;

        /// <summary>
        /// 资源释放标记
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 连接操作锁，确保线程安全
        /// </summary>
        private readonly object _connectionLock = new object();

        /// <summary>
        /// 读写操作锁，确保线程安全
        /// </summary>
        private readonly object _operationLock = new object();

        #endregion

        #region ICommunication接口实现

        /// <summary>
        /// 通讯名称标识
        /// </summary>
        public string Name => $"S7_{_config.DisplayName}_{_config.GetCpuTypeDisplayName()}_{_config.IpAddress.Replace(".", "_")}";

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;

        /// <summary>
        /// 消息接收事件（S7主要用于数据读写，此事件用于兼容性）
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
        /// <param name="config">S7连接配置</param>
        public S7Communication(S7ConnectionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 验证配置有效性
            if (!_config.IsValid(out string errorMsg))
            {
                throw new ArgumentException($"S7配置无效: {errorMsg}");
            }

            LogMessage($"S7通讯模块已初始化: {_config}");
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 异步打开连接
        /// </summary>
        public async Task<bool> OpenAsync()
        {
            return await Task.Run(() =>
            {
                lock (_connectionLock)
                {
                    try
                    {
                        if (Status == ConnectionStatus.Connected)
                        {
                            LogMessage("S7已连接，无需重复连接");
                            return true;
                        }

                        // 设置连接中状态
                        SetStatus(ConnectionStatus.Connecting);
                        LogMessage($"正在连接西门子PLC: {_config.IpAddress} ({_config.GetCpuTypeDisplayName()})");

                        // 创建S7 PLC连接对象
                        _plc = new Plc(_config.CpuType, _config.IpAddress, (short)_config.Rack, (short)_config.Slot);

                        // 设置连接超时
                        _plc.ReadTimeout = _config.ReadWriteTimeout;
                        _plc.WriteTimeout = _config.ReadWriteTimeout;

                        // 建立连接
                        _plc.Open();

                        // 验证连接状态
                        if (_plc.IsConnected)
                        {
                            SetStatus(ConnectionStatus.Connected);
                            LogMessage($"S7连接成功！目标: {_config.IpAddress} (Rack:{_config.Rack}, Slot:{_config.Slot})");

                            // 重置重连计数
                            _reconnectAttemptCount = 0;

                            // 启动心跳检测
                            if (_config.EnableHeartbeat)
                            {
                                StartHeartbeat();
                            }

                            // 清除数据缓存
                            _dataCache.Clear();

                            return true;
                        }
                        else
                        {
                            throw new Exception("连接建立后状态检查失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"S7连接失败: {ex.Message}");
                        SetStatus(ConnectionStatus.Error);

                        // 清理资源
                        CleanupConnection();

                        // 启动自动重连
                        if (_config.EnableAutoReconnect && _reconnectAttemptCount < _config.MaxReconnectAttempts)
                        {
                            StartAutoReconnect();
                        }

                        return false;
                    }
                }
            });
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            lock (_connectionLock)
            {
                if (Status == ConnectionStatus.Disconnected) return;

                try
                {
                    LogMessage("正在断开S7连接...");

                    // 停止定时器
                    StopHeartbeat();
                    StopAutoReconnect();

                    // 清理连接
                    CleanupConnection();

                    SetStatus(ConnectionStatus.Disconnected);
                    LogMessage("S7连接已断开");
                }
                catch (Exception ex)
                {
                    LogMessage($"断开S7连接时出错: {ex.Message}");
                }
            }
        }

        #endregion

        #region 消息发送（兼容ICommunication接口）

        /// <summary>
        /// 发送消息（S7通讯主要用于变量读写，此方法用于兼容性）
        /// </summary>
        public async Task<bool> SendAsync(Message message)
        {
            try
            {
                LogMessage($"收到S7消息: {message.Command}");

                // 这里可以根据message内容执行相应的S7操作
                // 例如：解析message.Data为变量地址和值，然后调用相应的读写方法

                // 触发消息接收事件（用于日志和调试）
                MessageReceived?.Invoke(message);

                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"处理S7消息失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 完整的数据读写方法

        /// <summary>
        /// 检查连接状态
        /// </summary>
        public bool IsConnected => _plc?.IsConnected ?? false;

        #region 布尔值操作

        /// <summary>
        /// 读取布尔值
        /// </summary>
        /// <param name="address">PLC地址，如"DB1.DBX0.0"、"M0.0"、"I0.0"、"Q0.0"等</param>
        /// <returns>读取的布尔值</returns>
        public bool ReadBool(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取布尔值");
                        return false;
                    }

                    // 使用正确的bool类型读取
                    var result = _plc.Read(address);
                    var value = Convert.ToBoolean(result);
                    LogMessage($"读取布尔值成功: {address} = {value}");
                    return value;
                }
                catch (Exception ex)
                {
                    LogDetailedException("ReadBool", address, ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的布尔值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteBool(string address, bool value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入布尔值");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入布尔值成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入布尔值失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 字节操作

        /// <summary>
        /// 读取字节值
        /// </summary>
        /// <param name="address">PLC地址，如"DB1.DBB0"、"MB0"等</param>
        /// <returns>读取的字节值</returns>
        public byte ReadByte(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取字节");
                        return 0;
                    }

                    var result = _plc.Read(address);
                    var value = Convert.ToByte(result);
                    LogMessage($"读取字节成功: {address} = {value}");
                    return value;
                }
                catch (Exception ex)
                {
                    LogDetailedException("ReadByte", address, ex);
                    return 0;
                }
            }
        }

        /// <summary>
        /// 写入字节值
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的字节值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteByte(string address, byte value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入字节");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入字节成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入字节失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 整数操作

        /// <summary>
        /// 读取16位整数值
        /// </summary>
        /// <param name="address">PLC地址，如"DB1.DBW0"、"MW0"等</param>
        /// <returns>读取的16位整数值</returns>
        public short ReadInt16(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取整数");
                        return 0;
                    }

                    var result = _plc.Read(address);
                    var value = Convert.ToInt16(result);
                    LogMessage($"读取整数成功: {address} = {value}");
                    return value;
                }
                catch (Exception ex)
                {
                    LogDetailedException("ReadInt16", address, ex);
                    return 0;
                }
            }
        }

        /// <summary>
        /// 写入16位整数值
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的16位整数值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteInt16(string address, short value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入整数");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入整数成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入整数失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取32位整数值
        /// </summary>
        /// <param name="address">PLC地址，如"DB1.DBD0"、"MD0"等</param>
        /// <returns>读取的32位整数值</returns>
        public int ReadInt32(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取双整数");
                        return 0;
                    }

                    var result = _plc.Read(address);
                    var value = Convert.ToInt32(result);
                    LogMessage($"读取双整数成功: {address} = {value}");
                    return value;
                }
                catch (Exception ex)
                {
                    LogDetailedException("ReadInt32", address, ex);
                    return 0;
                }
            }
        }

        /// <summary>
        /// 写入32位整数值
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的32位整数值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteInt32(string address, int value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入双整数");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入双整数成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入双整数失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 浮点数操作

        /// <summary>
        /// 读取实数值(REAL)
        /// </summary>
        /// <param name="address">PLC地址，如"DB1.DBD0"、"MD0"等</param>
        /// <returns>读取的实数值</returns>
        public float ReadReal(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取实数");
                        return 0.0f;
                    }

                    // S7.Net的Read(string)方法返回一个object，对于REAL类型，它内部是uint
                    var result = _plc.Read(address);

                    // 检查返回类型是否为uint
                    if (result is uint dwordValue)
                    {
                        // 关键步骤：将uint的二进制位重新解释为float
                        // 1. 获取uint的字节数组 (PC是小端序)
                        byte[] bytes = BitConverter.GetBytes(dwordValue);

                        // S7.Net在读取时已经处理了大端到小端的转换，
                        // 所以我们现在得到的字节序是正确的，可以直接转换为float
                        float value = BitConverter.ToSingle(bytes, 0);

                        LogMessage($"读取实数成功: {address} = {value}");
                        return value;
                    }
                    else
                    {
                        // 如果返回的不是uint，记录一个错误，因为这不符合预期
                        LogMessage($"错误: 读取地址 {address} 时，期望返回uint，但实际返回了 {result?.GetType().Name}");
                        return 0.0f;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"读取实数失败 {address}: {ex.Message}");
                    return 0.0f;
                }
            }
        }

        /// <summary>
        /// 写入实数值(REAL)
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的实数值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteReal(string address, float value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入实数");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入实数成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入实数失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取长实数值(LREAL)
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取的长实数值</returns>
        public double ReadLReal(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取长实数");
                        return 0.0;
                    }

                    var result = _plc.Read(address);

                    // 对于LREAL(64位)，S7.Net会返回一个8字节的数组
                    if (result is byte[] bytes && bytes.Length >= 8)
                    {
                        // 关键步骤：直接将字节数组的二进制位重新解释为double
                        // 我们不再手动反转字节(Array.Reverse)，因为我们假设S7.Net已经处理了字节序
                        double value = BitConverter.ToDouble(bytes, 0);

                        LogMessage($"读取长实数成功: {address} = {value}");
                        return value;
                    }
                    else
                    {
                        // 如果返回的不是byte[8]，记录一个错误，因为这不符合预期
                        LogMessage($"错误: 读取地址 {address} 时，期望返回byte[8]，但实际返回了 {result?.GetType().Name}");
                        return 0.0;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"读取长实数失败 {address}: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// 写入长实数值(LREAL)
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的长实数值</param>
        /// <returns>是否写入成功</returns>
        public bool WriteLReal(string address, double value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入长实数");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入长实数成功: {address} = {value}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入长实数失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 字符串操作

        /// <summary>
        /// 读取字符串
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="length">字符串长度</param>
        /// <returns>读取的字符串</returns>
        public string ReadString(string address, int length)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取字符串");
                        return string.Empty;
                    }

                    var result = _plc.Read(address);
                    var value = result?.ToString() ?? string.Empty;
                    LogMessage($"读取字符串成功: {address} = '{value}'");
                    return value ?? string.Empty;
                }
                catch (Exception ex)
                {
                    LogMessage($"读取字符串失败 {address}: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的字符串</param>
        /// <param name="length">字符串最大长度</param>
        /// <returns>是否写入成功</returns>
        public bool WriteString(string address, string value, int length)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入字符串");
                        return false;
                    }

                    // 确保字符串不超过指定长度
                    if (value?.Length > length)
                    {
                        value = value.Substring(0, length);
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入字符串成功: {address} = '{value}'");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入字符串失败 {address}: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 批量读取多个地址的数据
        /// </summary>
        /// <param name="addresses">地址数组</param>
        /// <returns>读取结果字典</returns>
        public Dictionary<string, object> ReadMultiple(string[] addresses)
        {
            var results = new Dictionary<string, object>();

            if (!IsConnected)
            {
                LogMessage("S7未连接，无法批量读取");
                return results;
            }

            foreach (var address in addresses)
            {
                try
                {
                    // 根据地址类型自动判断数据类型（简化版本）
                    object value = null;

                    if (address.Contains(".DBX") || address.StartsWith("M") || address.StartsWith("I") || address.StartsWith("Q"))
                    {
                        value = ReadBool(address);
                    }
                    else if (address.Contains(".DBB") || address.StartsWith("MB"))
                    {
                        value = ReadByte(address);
                    }
                    else if (address.Contains(".DBW") || address.StartsWith("MW"))
                    {
                        value = ReadInt16(address);
                    }
                    else if (address.Contains(".DBD") || address.StartsWith("MD"))
                    {
                        // 默认按实数读取，实际使用中可能需要更精确的判断
                        value = ReadReal(address);
                    }
                    else
                    {
                        // 默认尝试布尔值
                        value = ReadBool(address);
                    }

                    results[address] = value;
                }
                catch (Exception ex)
                {
                    LogMessage($"批量读取地址 {address} 失败: {ex.Message}");
                    results[address] = null;
                }
            }

            LogMessage($"批量读取完成，成功读取 {results.Count} 个地址");
            return results;
        }

        /// <summary>
        /// 批量写入多个地址的数据
        /// </summary>
        /// <param name="values">地址值字典</param>
        /// <returns>是否全部写入成功</returns>
        public bool WriteMultiple(Dictionary<string, object> values)
        {
            if (!IsConnected)
            {
                LogMessage("S7未连接，无法批量写入");
                return false;
            }

            bool allSuccess = true;
            int successCount = 0;

            foreach (var kvp in values)
            {
                try
                {
                    bool success = false;

                    // 根据值的类型选择合适的写入方法
                    switch (kvp.Value)
                    {
                        case bool boolVal:
                            success = WriteBool(kvp.Key, boolVal);
                            break;
                        case byte byteVal:
                            success = WriteByte(kvp.Key, byteVal);
                            break;
                        case short shortVal:
                            success = WriteInt16(kvp.Key, shortVal);
                            break;
                        case int intVal:
                            success = WriteInt32(kvp.Key, intVal);
                            break;
                        case float floatVal:
                            success = WriteReal(kvp.Key, floatVal);
                            break;
                        case double doubleVal:
                            success = WriteLReal(kvp.Key, doubleVal);
                            break;
                        case string stringVal:
                            success = WriteString(kvp.Key, stringVal, 255); // 默认最大长度255
                            break;
                        default:
                            LogMessage($"不支持的数据类型: {kvp.Value?.GetType().Name}");
                            success = false;
                            break;
                    }

                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        allSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"批量写入地址 {kvp.Key} 失败: {ex.Message}");
                    allSuccess = false;
                }
            }

            LogMessage($"批量写入完成，成功写入 {successCount}/{values.Count} 个地址");
            return allSuccess;
        }

        #endregion

        #region 通用读写方法

        /// <summary>
        /// 通用读取方法（泛型版本）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="address">PLC地址</param>
        /// <returns>读取的值</returns>
        public T Read<T>(string address)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法读取数据");
                        return default(T);
                    }

                    var value = (T)_plc.Read(address);
                    LogMessage($"读取数据成功: {address} = {value} ({typeof(T).Name})");
                    return value;
                }
                catch (Exception ex)
                {
                    LogMessage($"读取数据失败 {address}: {ex.Message}");
                    LogMessage($"异常详情: {ex.GetType().Name} - {ex.StackTrace?.Split('\n')[0]}");
                    return default(T);
                }
            }
        }

        /// <summary>
        /// 通用写入方法（泛型版本）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="address">PLC地址</param>
        /// <param name="value">要写入的值</param>
        /// <returns>是否写入成功</returns>
        public bool Write<T>(string address, T value)
        {
            lock (_operationLock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        LogMessage("S7未连接，无法写入数据");
                        return false;
                    }

                    _plc.Write(address, value);
                    LogMessage($"写入数据成功: {address} = {value} ({typeof(T).Name})");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"写入数据失败 {address}: {ex.Message}");
                    LogMessage($"异常详情: {ex.GetType().Name} - {ex.StackTrace?.Split('\n')[0]}");
                    return false;
                }
            }
        }

        #endregion

        #endregion

        #region 心跳检测和自动重连

        /// <summary>
        /// 启动心跳检测
        /// </summary>
        private void StartHeartbeat()
        {
            StopHeartbeat(); // 先停止已有的定时器

            _heartbeatTimer = new Timer(HeartbeatCallback, null, _config.HeartbeatInterval, _config.HeartbeatInterval);
            LogMessage($"心跳检测已启动，间隔: {_config.HeartbeatInterval}ms");
        }

        /// <summary>
        /// 停止心跳检测
        /// </summary>
        private void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        /// <summary>
        /// 心跳检测回调
        /// </summary>
        private void HeartbeatCallback(object state)
        {
            try
            {
                if (!IsConnected)
                {
                    LogMessage("心跳检测：连接已断开");
                    SetStatus(ConnectionStatus.Error);

                    // 触发自动重连
                    if (_config.EnableAutoReconnect)
                    {
                        StartAutoReconnect();
                    }
                    return;
                }

                // 如果指定了心跳地址，则读取该地址
                if (!string.IsNullOrEmpty(_config.HeartbeatAddress))
                {
                    ReadBool(_config.HeartbeatAddress);
                }

                LogMessage("心跳检测正常");
            }
            catch (Exception ex)
            {
                LogMessage($"心跳检测异常: {ex.Message}");
                SetStatus(ConnectionStatus.Error);

                if (_config.EnableAutoReconnect)
                {
                    StartAutoReconnect();
                }
            }
        }

        /// <summary>
        /// 启动自动重连
        /// </summary>
        private void StartAutoReconnect()
        {
            if (_reconnectAttemptCount >= _config.MaxReconnectAttempts)
            {
                LogMessage($"已达到最大重连次数({_config.MaxReconnectAttempts})，停止重连");
                return;
            }

            StopAutoReconnect(); // 先停止已有的重连定时器

            _reconnectTimer = new Timer(ReconnectCallback, null, _config.ReconnectInterval, Timeout.Infinite);
            LogMessage($"自动重连已启动，{_config.ReconnectInterval}ms后尝试重连 (第{_reconnectAttemptCount + 1}次)");
        }

        /// <summary>
        /// 停止自动重连
        /// </summary>
        private void StopAutoReconnect()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        /// <summary>
        /// 自动重连回调
        /// </summary>
        private void ReconnectCallback(object state)
        {
            _reconnectAttemptCount++;
            LogMessage($"开始自动重连尝试 (第{_reconnectAttemptCount}次)");

            Task.Run(async () =>
            {
                var result = await OpenAsync();
                if (!result && _reconnectAttemptCount < _config.MaxReconnectAttempts)
                {
                    // 继续尝试重连
                    StartAutoReconnect();
                }
            });
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 详细的异常日志记录
        /// </summary>
        private void LogDetailedException(string operation, string address, Exception ex)
        {
            LogMessage($"❌ {operation} 异常 [{address}]:");
            LogMessage($"   异常类型: {ex.GetType().Name}");
            LogMessage($"   异常消息: {ex.Message}");
            
            if (ex.GetType().Name.Contains("InvalidAddressException"))
            {
                LogMessage($"   ⚠️  地址格式错误！检查地址格式是否正确");
                LogMessage($"   💡 正确格式示例: DB1.DBX0.0 (Bool), DB1.DBB1 (Byte), DB1.DBW2 (Word), DB1.DBD4 (DWord)");
            }
            else if (ex is System.FormatException)
            {
                LogMessage($"   ⚠️  数据格式转换错误！");
                LogMessage($"   💡 可能是PLC返回的数据类型与期望类型不匹配");
            }
            
            if (ex.StackTrace != null)
            {
                var stackLines = ex.StackTrace.Split('\n');
                if (stackLines.Length > 0)
                {
                    LogMessage($"   调用位置: {stackLines[0]?.Trim()}");
                }
            }
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
            var logMsg = $"[S7] {DateTime.Now:HH:mm:ss} {message}";
            LogReceived?.Invoke(logMsg);
        }

        /// <summary>
        /// 清理PLC连接资源
        /// </summary>
        private void CleanupConnection()
        {
            try
            {
                _plc?.Close();
                _plc = null;
                _dataCache.Clear();
            }
            catch (Exception ex)
            {
                LogMessage($"清理连接资源时出错: {ex.Message}");
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