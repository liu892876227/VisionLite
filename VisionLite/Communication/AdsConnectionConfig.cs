// Communication/AdsConnectionConfig.cs
// 倍福ADS连接配置类 - 管理ADS通讯的所有连接参数
using System;

namespace VisionLite.Communication
{
    /// <summary>
    /// 倍福ADS通讯连接配置类
    /// 包含ADS连接所需的所有参数配置
    /// </summary>
    public class AdsConnectionConfig
    {
        /// <summary>
        /// 目标PLC的AMS NetId（如：169.254.162.172.1.1）
        /// NetId格式：IP地址 + .1.1 后缀
        /// </summary>
        public string TargetAmsNetId { get; set; } = "169.254.162.172.1.1";

        /// <summary>
        /// 目标AMS端口号（通常为801表示PLC Runtime端口）
        /// 常用端口：801 = PLC Runtime, 851 = System Service
        /// </summary>
        public int TargetAmsPort { get; set; } = 801;

        /// <summary>
        /// 连接超时时间（毫秒）
        /// 默认5000ms，可根据网络环境调整
        /// </summary>
        public int Timeout { get; set; } = 5000;

        /// <summary>
        /// 是否使用符号访问（建议启用，更便于调试）
        /// true: 通过变量名访问（如"GVL.bTestBool"）
        /// false: 仅通过地址访问
        /// </summary>
        public bool UseSymbolicAccess { get; set; } = true;

        /// <summary>
        /// 连接描述名称（用于UI显示和日志记录）
        /// </summary>
        public string DisplayName { get; set; } = "倍福PLC";

        /// <summary>
        /// 验证配置参数是否有效
        /// </summary>
        /// <param name="errorMessage">错误信息输出</param>
        /// <returns>true表示配置有效，false表示有错误</returns>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            // 验证NetId格式
            if (string.IsNullOrWhiteSpace(TargetAmsNetId))
            {
                errorMessage = "AMS NetId不能为空";
                return false;
            }

            // 简单的NetId格式验证（应该包含6个部分，用点分隔）
            var parts = TargetAmsNetId.Split('.');
            if (parts.Length != 6)
            {
                errorMessage = "AMS NetId格式错误，应为类似 192.168.1.100.1.1 的格式";
                return false;
            }

            // 验证端口范围
            if (TargetAmsPort < 1 || TargetAmsPort > 65535)
            {
                errorMessage = "AMS端口必须在1-65535范围内";
                return false;
            }

            // 验证超时时间
            if (Timeout < 1000 || Timeout > 60000)
            {
                errorMessage = "超时时间应在1000-60000毫秒范围内";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 创建默认的ADS配置
        /// </summary>
        public static AdsConnectionConfig CreateDefault()
        {
            return new AdsConnectionConfig
            {
                TargetAmsNetId = "169.254.162.172.1.1",
                TargetAmsPort = 801,
                Timeout = 5000,
                UseSymbolicAccess = true,
                DisplayName = "倍福PLC"
            };
        }

        /// <summary>
        /// 创建本地环回测试配置（用于本机TwinCAT调试）
        /// </summary>
        public static AdsConnectionConfig CreateLocalhost()
        {
            return new AdsConnectionConfig
            {
                TargetAmsNetId = "127.0.0.1.1.1",
                TargetAmsPort = 851, // 本地系统服务端口
                Timeout = 3000,
                UseSymbolicAccess = true,
                DisplayName = "本地TwinCAT"
            };
        }

        public override string ToString()
        {
            return $"{DisplayName} ({TargetAmsNetId}:{TargetAmsPort})";
        }
    }
}