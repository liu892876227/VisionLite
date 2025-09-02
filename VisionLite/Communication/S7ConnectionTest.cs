// Communication/S7ConnectionTest.cs
// 西门子S7 PLC连接测试工具类
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using S7.Net;

namespace VisionLite.Communication
{
    /// <summary>
    /// S7 PLC连接测试工具类
    /// 提供连接测试、数据读写验证等功能
    /// </summary>
    public class S7ConnectionTest
    {
        #region 私有字段

        /// <summary>
        /// S7通讯实例
        /// </summary>
        private S7Communication _s7Communication;

        /// <summary>
        /// 测试配置
        /// </summary>
        private S7ConnectionConfig _config;

        /// <summary>
        /// 测试结果事件
        /// </summary>
        public event Action<string> TestMessageReceived;

        /// <summary>
        /// 测试完成事件
        /// </summary>
        public event Action<bool, string> TestCompleted;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">连接配置</param>
        public S7ConnectionTest(S7ConnectionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion

        #region 基础连接测试

        /// <summary>
        /// 执行基础连接测试
        /// </summary>
        /// <returns>测试是否成功</returns>
        public async Task<bool> RunBasicConnectionTestAsync()
        {
            try
            {
                LogTestMessage("=== 开始S7基础连接测试 ===");
                LogTestMessage($"目标PLC: {_config.IpAddress} ({_config.GetCpuTypeDisplayName()})");
                LogTestMessage($"Rack: {_config.Rack}, Slot: {_config.Slot}");

                // 创建S7通讯实例
                _s7Communication = new S7Communication(_config);
                _s7Communication.LogReceived += LogTestMessage;
                _s7Communication.StatusChanged += (status) => 
                {
                    LogTestMessage($"连接状态变化: {status}");
                };

                // 测试连接
                var stopwatch = Stopwatch.StartNew();
                LogTestMessage("正在尝试连接...");

                bool connected = await _s7Communication.OpenAsync();
                stopwatch.Stop();

                if (connected)
                {
                    LogTestMessage($"✅ 连接成功！耗时: {stopwatch.ElapsedMilliseconds}ms");
                    LogTestMessage($"连接状态: {_s7Communication.Status}");
                    LogTestMessage($"PLC连接状态: {_s7Communication.IsConnected}");
                    
                    // 等待一秒确保连接稳定
                    await Task.Delay(1000);
                    
                    // 测试断开连接
                    LogTestMessage("测试断开连接...");
                    _s7Communication.Close();
                    LogTestMessage($"✅ 断开连接成功");
                    
                    TestCompleted?.Invoke(true, "基础连接测试通过");
                    return true;
                }
                else
                {
                    LogTestMessage($"❌ 连接失败！耗时: {stopwatch.ElapsedMilliseconds}ms");
                    TestCompleted?.Invoke(false, "连接失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTestMessage($"❌ 测试异常: {ex.Message}");
                TestCompleted?.Invoke(false, $"测试异常: {ex.Message}");
                return false;
            }
            finally
            {
                _s7Communication?.Dispose();
                _s7Communication = null;
                LogTestMessage("=== S7基础连接测试结束 ===");
            }
        }

        #endregion

        #region 数据读写测试

        /// <summary>
        /// 执行数据读写测试
        /// </summary>
        /// <param name="testAddress">测试地址（默认为DB1.DBX0.0）</param>
        /// <returns>测试是否成功</returns>
        public async Task<bool> RunDataReadWriteTestAsync(string testAddress = "DB1.DBX0.0")
        {
            try
            {
                LogTestMessage("=== 开始S7数据读写测试 ===");
                LogTestMessage($"测试地址: {testAddress}");

                // 创建并连接
                _s7Communication = new S7Communication(_config);
                _s7Communication.LogReceived += LogTestMessage;

                bool connected = await _s7Communication.OpenAsync();
                if (!connected)
                {
                    LogTestMessage("❌ 连接失败，无法进行数据读写测试");
                    TestCompleted?.Invoke(false, "连接失败");
                    return false;
                }

                LogTestMessage("✅ 连接成功，开始数据读写测试");
                bool allTestsPassed = true;

                // 测试布尔值读写
                allTestsPassed &= await TestBooleanReadWrite(testAddress);
                
                // 如果有其他测试地址，继续测试其他数据类型
                // 这里可以扩展更多的数据类型测试

                if (allTestsPassed)
                {
                    LogTestMessage("✅ 所有数据读写测试通过");
                    TestCompleted?.Invoke(true, "数据读写测试通过");
                }
                else
                {
                    LogTestMessage("❌ 部分数据读写测试失败");
                    TestCompleted?.Invoke(false, "部分测试失败");
                }

                return allTestsPassed;
            }
            catch (Exception ex)
            {
                LogTestMessage($"❌ 数据读写测试异常: {ex.Message}");
                TestCompleted?.Invoke(false, $"测试异常: {ex.Message}");
                return false;
            }
            finally
            {
                _s7Communication?.Close();
                _s7Communication?.Dispose();
                _s7Communication = null;
                LogTestMessage("=== S7数据读写测试结束 ===");
            }
        }

        /// <summary>
        /// 测试布尔值读写
        /// </summary>
        /// <param name="address">测试地址</param>
        /// <returns>测试是否成功</returns>
        private async Task<bool> TestBooleanReadWrite(string address)
        {
            try
            {
                LogTestMessage($"--- 测试布尔值读写: {address} ---");

                // 读取当前值
                LogTestMessage("读取当前值...");
                bool currentValue = _s7Communication.ReadBool(address);
                LogTestMessage($"当前值: {currentValue}");

                // 写入相反值
                bool newValue = !currentValue;
                LogTestMessage($"写入新值: {newValue}");
                bool writeResult = _s7Communication.WriteBool(address, newValue);

                if (!writeResult)
                {
                    LogTestMessage("❌ 写入失败");
                    return false;
                }

                // 等待写入完成
                await Task.Delay(200);

                // 读取验证
                LogTestMessage("读取验证...");
                bool verifyValue = _s7Communication.ReadBool(address);
                LogTestMessage($"验证值: {verifyValue}");

                if (verifyValue == newValue)
                {
                    LogTestMessage("✅ 布尔值读写测试通过");
                    
                    // 恢复原始值
                    _s7Communication.WriteBool(address, currentValue);
                    LogTestMessage("已恢复原始值");
                    
                    return true;
                }
                else
                {
                    LogTestMessage($"❌ 验证失败，期望值: {newValue}, 实际值: {verifyValue}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTestMessage($"❌ 布尔值测试异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 性能测试

        /// <summary>
        /// 执行性能测试
        /// </summary>
        /// <param name="testAddress">测试地址</param>
        /// <param name="testCount">测试次数</param>
        /// <returns>测试是否成功</returns>
        public async Task<bool> RunPerformanceTestAsync(string testAddress = "DB1.DBX0.0", int testCount = 100)
        {
            try
            {
                LogTestMessage("=== 开始S7性能测试 ===");
                LogTestMessage($"测试地址: {testAddress}, 测试次数: {testCount}");

                // 创建并连接
                _s7Communication = new S7Communication(_config);
                _s7Communication.LogReceived += (msg) => { }; // 屏蔽详细日志避免干扰性能测试

                bool connected = await _s7Communication.OpenAsync();
                if (!connected)
                {
                    LogTestMessage("❌ 连接失败，无法进行性能测试");
                    return false;
                }

                LogTestMessage("✅ 连接成功，开始性能测试");

                // 读取性能测试
                var readStopwatch = Stopwatch.StartNew();
                int readSuccessCount = 0;

                for (int i = 0; i < testCount; i++)
                {
                    try
                    {
                        _s7Communication.ReadBool(testAddress);
                        readSuccessCount++;
                    }
                    catch
                    {
                        // 忽略单次失败
                    }
                }

                readStopwatch.Stop();
                double avgReadTime = readStopwatch.ElapsedMilliseconds / (double)testCount;
                LogTestMessage($"读取性能: {testCount}次读取, 成功{readSuccessCount}次, 总耗时{readStopwatch.ElapsedMilliseconds}ms");
                LogTestMessage($"平均读取时间: {avgReadTime:F2}ms/次");

                // 写入性能测试
                var writeStopwatch = Stopwatch.StartNew();
                int writeSuccessCount = 0;
                bool testValue = true;

                for (int i = 0; i < testCount; i++)
                {
                    try
                    {
                        _s7Communication.WriteBool(testAddress, testValue);
                        writeSuccessCount++;
                        testValue = !testValue; // 交替写入true/false
                    }
                    catch
                    {
                        // 忽略单次失败
                    }
                }

                writeStopwatch.Stop();
                double avgWriteTime = writeStopwatch.ElapsedMilliseconds / (double)testCount;
                LogTestMessage($"写入性能: {testCount}次写入, 成功{writeSuccessCount}次, 总耗时{writeStopwatch.ElapsedMilliseconds}ms");
                LogTestMessage($"平均写入时间: {avgWriteTime:F2}ms/次");

                // 性能评估
                bool performanceAcceptable = avgReadTime < 100 && avgWriteTime < 100; // 100ms以内认为可接受
                if (performanceAcceptable)
                {
                    LogTestMessage("✅ 性能测试通过");
                    TestCompleted?.Invoke(true, "性能测试通过");
                }
                else
                {
                    LogTestMessage("⚠️ 性能测试警告：读写速度较慢，可能影响实际应用");
                    TestCompleted?.Invoke(true, "性能测试完成但速度较慢");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogTestMessage($"❌ 性能测试异常: {ex.Message}");
                TestCompleted?.Invoke(false, $"性能测试异常: {ex.Message}");
                return false;
            }
            finally
            {
                _s7Communication?.Close();
                _s7Communication?.Dispose();
                _s7Communication = null;
                LogTestMessage("=== S7性能测试结束 ===");
            }
        }

        #endregion

        #region 综合测试

        /// <summary>
        /// 运行完整的综合测试
        /// </summary>
        /// <param name="testAddress">测试地址</param>
        /// <returns>测试结果</returns>
        public async Task<TestResult> RunComprehensiveTestAsync(string testAddress = "DB1.DBX0.0")
        {
            var result = new TestResult();
            
            try
            {
                LogTestMessage("=== 开始S7综合测试 ===");
                result.StartTime = DateTime.Now;

                // 1. 基础连接测试
                LogTestMessage("1. 执行基础连接测试...");
                result.ConnectionTestPassed = await RunBasicConnectionTestAsync();
                
                if (!result.ConnectionTestPassed)
                {
                    result.OverallResult = false;
                    result.ErrorMessage = "基础连接测试失败";
                    return result;
                }

                // 2. 数据读写测试
                LogTestMessage("2. 执行数据读写测试...");
                result.DataReadWriteTestPassed = await RunDataReadWriteTestAsync(testAddress);

                // 3. 性能测试（即使前面测试失败也执行，用于诊断）
                LogTestMessage("3. 执行性能测试...");
                result.PerformanceTestPassed = await RunPerformanceTestAsync(testAddress, 50);

                // 综合评估
                result.OverallResult = result.ConnectionTestPassed && result.DataReadWriteTestPassed;
                result.EndTime = DateTime.Now;
                result.TotalDuration = result.EndTime - result.StartTime;

                if (result.OverallResult)
                {
                    LogTestMessage("✅ 综合测试全部通过！S7通讯功能正常");
                }
                else
                {
                    LogTestMessage("❌ 综合测试存在问题，请检查PLC配置和网络连接");
                }

                return result;
            }
            catch (Exception ex)
            {
                LogTestMessage($"❌ 综合测试异常: {ex.Message}");
                result.OverallResult = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                LogTestMessage("=== S7综合测试结束 ===");
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 记录测试消息
        /// </summary>
        /// <param name="message">消息内容</param>
        private void LogTestMessage(string message)
        {
            var logMsg = $"[S7Test] {DateTime.Now:HH:mm:ss} {message}";
            TestMessageReceived?.Invoke(logMsg);
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _s7Communication?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 测试结果类
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// 测试开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 测试结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 总测试时长
        /// </summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>
        /// 连接测试是否通过
        /// </summary>
        public bool ConnectionTestPassed { get; set; }

        /// <summary>
        /// 数据读写测试是否通过
        /// </summary>
        public bool DataReadWriteTestPassed { get; set; }

        /// <summary>
        /// 性能测试是否通过
        /// </summary>
        public bool PerformanceTestPassed { get; set; }

        /// <summary>
        /// 总体测试结果
        /// </summary>
        public bool OverallResult { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取测试结果摘要
        /// </summary>
        /// <returns>结果摘要</returns>
        public string GetSummary()
        {
            return $"连接测试: {(ConnectionTestPassed ? "通过" : "失败")}, " +
                   $"读写测试: {(DataReadWriteTestPassed ? "通过" : "失败")}, " +
                   $"性能测试: {(PerformanceTestPassed ? "通过" : "失败")}, " +
                   $"总体结果: {(OverallResult ? "成功" : "失败")}, " +
                   $"耗时: {TotalDuration.TotalSeconds:F1}秒";
        }
    }
}