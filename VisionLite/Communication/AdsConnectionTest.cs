// Communication/AdsConnectionTest.cs  
// ADS连接测试工具类 - 用于验证ADS通讯功能
using System;
using System.Threading.Tasks;

namespace VisionLite.Communication
{
    /// <summary>
    /// ADS连接测试工具类
    /// 提供简单的测试方法来验证ADS通讯是否正常工作
    /// </summary>
    public class AdsConnectionTest
    {
        /// <summary>
        /// 测试ADS连接的基本功能
        /// </summary>
        /// <param name="config">ADS连接配置</param>
        /// <returns>测试结果信息</returns>
        public static async Task<string> TestConnection(AdsConnectionConfig config)
        {
            var result = "ADS连接测试开始...\n";
            AdsCommunication adsComm = null;

            try
            {
                // 步骤1: 创建ADS通讯对象
                result += "1. 创建ADS通讯对象...\n";
                adsComm = new AdsCommunication(config);
                
                // 监听日志事件
                adsComm.LogReceived += (msg) => result += $"   {msg}\n";
                
                result += "   ✓ ADS对象创建成功\n";

                // 步骤2: 尝试连接
                result += "2. 尝试建立连接...\n";
                bool connected = await adsComm.OpenAsync();
                
                if (connected)
                {
                    result += "   ✓ 连接建立成功\n";
                    result += $"   连接状态: {adsComm.Status}\n";
                    result += $"   IsConnected: {adsComm.IsConnected}\n";

                    // 步骤3: 测试读写操作（可选）
                    result += "3. 测试基本读写操作...\n";
                    try
                    {
                        // 尝试读取一个测试变量（如果PLC中有这个变量的话）
                        bool testValue = false;
                        bool readSuccess = adsComm.ReadVariable("MAIN.bTestVar", ref testValue);
                        
                        if (readSuccess)
                        {
                            result += $"   ✓ 读取测试变量成功: MAIN.bTestVar = {testValue}\n";
                            
                            // 尝试写入相同的值
                            bool writeSuccess = adsComm.WriteVariable("MAIN.bTestVar", !testValue);
                            if (writeSuccess)
                            {
                                result += $"   ✓ 写入测试变量成功: MAIN.bTestVar = {!testValue}\n";
                                
                                // 再次读取验证
                                bool verifyValue = false;
                                if (adsComm.ReadVariable("MAIN.bTestVar", ref verifyValue))
                                {
                                    result += $"   ✓ 验证写入成功: MAIN.bTestVar = {verifyValue}\n";
                                }
                            }
                        }
                        else
                        {
                            result += "   ⚠ 读取测试变量失败，可能PLC中不存在MAIN.bTestVar变量\n";
                            result += "   这是正常的，请确保PLC程序中有对应的测试变量\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        result += $"   ⚠ 读写测试出现异常: {ex.Message}\n";
                    }

                    // 步骤4: 断开连接
                    result += "4. 断开连接...\n";
                    adsComm.Close();
                    result += $"   ✓ 连接已断开，状态: {adsComm.Status}\n";
                }
                else
                {
                    result += "   ❌ 连接建立失败\n";
                    result += $"   连接状态: {adsComm.Status}\n";
                    result += "   请检查:\n";
                    result += "   - PLC是否运行且网络可达\n";
                    result += "   - NetId格式是否正确\n";
                    result += "   - AMS端口是否正确（通常为801）\n";
                    result += "   - Windows防火墙设置\n";
                }
            }
            catch (Exception ex)
            {
                result += $"❌ 测试过程中发生异常: {ex.Message}\n";
                result += $"异常类型: {ex.GetType().Name}\n";
                
                if (ex.InnerException != null)
                {
                    result += $"内部异常: {ex.InnerException.Message}\n";
                }
            }
            finally
            {
                // 确保资源被释放
                adsComm?.Dispose();
                result += "\n测试完成。\n";
            }

            return result;
        }

        /// <summary>
        /// 快速连接测试（仅测试连接，不测试读写）
        /// </summary>
        /// <param name="netId">PLC的NetId</param>
        /// <param name="port">AMS端口</param>
        /// <returns>是否连接成功</returns>
        public static async Task<bool> QuickConnectionTest(string netId, int port = 801)
        {
            try
            {
                var config = new AdsConnectionConfig
                {
                    TargetAmsNetId = netId,
                    TargetAmsPort = port,
                    Timeout = 3000, // 快速测试，3秒超时
                    DisplayName = "快速测试"
                };

                using (var adsComm = new AdsCommunication(config))
                {
                    return await adsComm.OpenAsync();
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建默认测试配置
        /// </summary>
        /// <returns>测试用的ADS配置</returns>
        public static AdsConnectionConfig CreateTestConfig()
        {
            return new AdsConnectionConfig
            {
                TargetAmsNetId = "169.254.162.172.1.1", // 修改为你的PLC IP
                TargetAmsPort = 801,
                Timeout = 5000,
                UseSymbolicAccess = true,
                DisplayName = "测试PLC"
            };
        }

        /// <summary>
        /// 批量测试多个PLC连接
        /// </summary>
        /// <param name="netIds">要测试的NetId列表</param>
        /// <returns>测试结果</returns>
        public static async Task<string> BatchConnectionTest(string[] netIds)
        {
            var result = $"批量连接测试开始，共{netIds.Length}个目标...\n\n";

            for (int i = 0; i < netIds.Length; i++)
            {
                result += $"[{i + 1}/{netIds.Length}] 测试 {netIds[i]}:\n";
                
                try
                {
                    bool connected = await QuickConnectionTest(netIds[i]);
                    result += connected ? "  ✓ 连接成功\n" : "  ❌ 连接失败\n";
                }
                catch (Exception ex)
                {
                    result += $"  ❌ 异常: {ex.Message}\n";
                }
                
                result += "\n";
            }

            result += "批量测试完成。\n";
            return result;
        }
    }
}