// ParametersWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MvCamCtrl.NET;
using HalconDotNet;
using System.Diagnostics;


namespace VisionLite
{
    public partial class ParametersWindow : Window
    {
        
        private readonly CameraDevice m_pCameraDevice;
        private readonly HTuple m_pAcqHandle;
        private readonly Dictionary<string, string> parameterTranslations = new Dictionary<string, string> // 可以添加您的翻译字典

        {
            // --- 基本信息 (Device Information) ---
            { "DeviceID", "设备物理ID" },
            { "DeviceUserID", "设备用户ID" },
            { "DeviceVendorName", "设备制造商" },
            { "DeviceModelName", "设备型号" },
            { "DeviceVersion", "设备版本" },
            { "DeviceFirmwareVersion", "固件版本" },
            { "DeviceSerialNumber", "设备序列号" },
            { "DeviceUptime", "设备运行时长" },
            { "DeviceType", "设备类型" },
            { "DeviceScanType", "扫描类型" }, // e.g., "Areascan"
            { "DeviceManufacturerInfo", "制造商信息" },
            { "DeviceConnectionSelector", "设备连接选择器" },
            { "DeviceLinkSelector", "设备链接选择器" },
            { "DeviceConnectionSpeed", "设备连接速度（Mbps）" },
            { "DeviceLinkSpeed", "设备链路速度（Mbps）" },
            {"DeviceLinkConnectionCount", "设备链接连接计数" },
            {"DeviceStreamChannelCount", "设备流通道计数" },
            {"DeviceStreamChannelSelector", "设备流通道选择器" },
            {"DeviceStreamChannelType", "设备流通道类型" },
            {"DeviceStreamChannelLink", "设备流信道链路" },
            {"DeviceStreamChannelEndianness", "设备流通道字节次序" },
            {"DeviceStreamChannelPacketSize", "设备流通道数据包大小（B）" },
            {"DeviceEventChannelCount", "设备事件通道计数" },
            {"DeviceCharacterSet", "设备字符集" },
            {"DeviceReset", "设备重置" },
            {"FindMe", "查找当前设备" },
            {"DeviceMaxThroughput", "设备最大吞吐量（Kbps）" },
            {"TestPatternGeneratorSelector", "测试模式生成器选择器" },
            {"BinningSelector", "合并选择器" },
            {"FrameSpecInfo", "图像嵌入信息使能" },
            {"FrameSpecInfoSelector", "图像嵌入信息选择器" },
            {"HDREnable", "高动态范围使能" },
            {"HDRSelector", "高动态范围选择器" },
            {"HDRShutter", "高动态范围快门（us）" },
            {"HDRGain", "高动态范围增益" },
            {"GammaSelector", "伽马校正选择器" },
            {"AutoFunctionAOISelector", "自动调节选择器" },
            {"AutoFunctionAOIWidth", "自动功能AOI宽度" },
            {"AutoFunctionAOIHeight", "自动功能AOI高度" },
            {"AutoFunctionAOIOffsetX", "自动功能AOI水平偏移" },
            {"AutoFunctionAOIOffsetY", "自动功能AOI垂直偏移" },
            {"AutoFunctionAOIUsageIntensity", "自动功能AOI使用强度" },
            {"AutoFunctionAOIUsageWhiteBalance", "自动功能AOI使用白平衡" },
            {"LUTSelector", "用户查找表选择器" },
            {"LUTEnable", "用户查找表使能" },
            {"LUTIndex", "用户查找表系数" },
            {"LUTValue", "用户查找表值" },
            {"LineSelector", "线路选择器" },
            {"LineMode", "线路模式" },
            {"LineStatus", "线路状态" },
            {"LineStatusAll", "所有线路状态" },
            {"LineDebouncerTime", "线路防抖时间（us）" },
            {"CounterSelector", "计数器选择" },
            {"CounterEventSource", "计数器事件源" },
            {"CounterResetSource", "计数器复位源" },
            {"CounterValue", "计数器值" },
            {"CounterCurrentValue", "计数器当前值" },
            {"DeviceTemperatureSeletor", "设备温度选择器" },
            {"DeviceTemperature", "设备温度" },
            {"BoardDeviceType", "设备单板类型" },



            // --- 图像格式与ROI (Image Format & Region of Interest) ---
            { "Width", "宽度" },
            { "Height", "高度" },
            { "WidthMax", "最大宽度" },
            { "HeightMax", "最大高度" },
            { "OffsetX", "X偏移 (ROI)" },
            { "OffsetY", "Y偏移 (ROI)" },
            { "PixelFormat", "像素格式" },
            { "PixelSize", "像素尺寸" },
            { "ReverseX", "X轴翻转" },
            { "ReverseY", "Y轴翻转" },
            { "RegionSelector", "区域选择器" },
            { "RegionDestination", "区域目标" },

            // --- 采集控制 (Acquisition Control) ---
            { "AcquisitionMode", "采集模式" }, // "Continuous", "SingleFrame"
            { "AcquisitionStart", "开始采集(命令)" },
            { "AcquisitionStop", "停止采集(命令)" },
            { "AcquisitionFrameRate", "采集帧率" },
            { "AcquisitionFrameRateEnable", "帧率使能" },
            { "ResultingFrameRate", "结果帧率" },
            { "AcquisitionBurstFrameCount", "突发模式帧数" },
            { "grabtimeout", "采集超时(ms)" },

            // --- 曝光与增益 (Exposure & Gain) ---
            { "ExposureMode", "曝光模式" },
            { "ExposureTime", "曝光时间(us)" },
            { "ExposureAuto", "自动曝光" },
            { "AutoExposureTimeLowerLimit", "自动曝光下限" },
            { "AutoExposureTimeUpperLimit", "自动曝光上限" },
            { "Gain", "增益" },
            { "GainAuto", "自动增益" },
            { "AutoGainLowerLimit", "自动增益下限" },
            { "AutoGainUpperLimit", "自动增益上限" },

            // --- 触发 (Trigger) ---
            { "TriggerMode", "触发模式" },
            { "TriggerSource", "触发源" },
            { "TriggerSelector", "触发选择器" },
            { "TriggerSoftware", "软触发(命令)" },
            { "TriggerActivation", "触发激活方式" },
            { "TriggerDelay", "触发延迟" },
            { "TriggerCacheEnable", "触发缓存使能" },

            // --- 白平衡 (White Balance) ---
            { "BalanceWhiteAuto", "自动白平衡" },
            { "BalanceRatioSelector", "白平衡通道选择" },
            { "BalanceRatio", "白平衡系数值" },
            { "BalanceRatioRed", "红平衡系数" },
            { "BalanceRatioGreen", "绿平衡系数" },
            { "BalanceRatioBlue", "蓝平衡系数" },

            // --- 图像处理与色彩 (Image Processing & Color) ---
            { "Gamma", "伽马值" },
            { "GammaEnable", "伽马使能" },
            { "BlackLevel", "黑电平" },
            { "BlackLevelEnable", "黑电平使能" },
            { "Hue", "色调" },
            { "HueEnable", "色调使能" },
            { "Saturation", "饱和度" },
            { "SaturationEnable", "饱和度使能" },
            { "DigitalShift", "数字移位" },
            { "DigitalShiftEnable", "数字移位使能" },
            { "IsBayer_AvaNot", "Bayer格式可用" }, // "Is Bayer Available Notifier"的简写

            // --- 网络配置 (GigE Vision Transport Layer) ---
            { "GevPersistentIPAddress", "静态IP地址" },
            { "GevPersistentSubnetMask", "静态子网掩码" },
            { "GevPersistentDefaultGateway", "静态默认网关" },
            { "GevCurrentIPAddress", "当前IP地址" },
            { "GevCurrentSubnetMask", "当前子网掩码" },
            { "GevCurrentDefaultGateway", "当前默认网关" },
            { "GevSCPSPacketSize", "网络数据包大小" },
            { "PayloadSize", "有效负载大小" },
            { "GevSCPD", "网络包间隔" },
            { "GevLinkSpeed", "网络连接速度" },
            { "GevMACAddress", "GEV MAC地址" },
            {"GevVersionMajor", "GEV主要版本" },
            {"GevVersionMinor", "GEV次要版本" },
            {"GevDeviceModeIsBigEndian", "GEV设备模式字节次序是大端" },
            {"GevDeviceModeCharacterSet", "GEV设备模式字符集" },
            {"GevInterfaceSelector", "GEV接口选择器" },
            {"GevSupportedOptionSelector", "GEV支持选项选择器" },
            {"GevSupportedOption", "GEV支持选项" },
            {"GevCurrentIPConfigurationLLA", "GEV当前IP配置LLA" },
            {"GevCurrentIPConfigurationDHCP", "GEV当前IP配置DHCP" },
            {"GevCurrentIPConfigurationPersistentIP", "GEV当前IP配置静态IP" },
            {"GevPAUSEFrameReception", "GEV暂停帧接收" },
            {"GevNumberOfInterfaces", "GEV接口数量" },
            {"GevMessageChannelCount", "GEV消息通道计数" },
            {"GevStreamChannelCount", "GEV流信道计数" },
            {"GevHeartbeatTimeout", "GEV心跳超时（ms）" },
            {"GevGVCPHeartbeatDisable", "GEV心跳禁用" },
            {"GevTimestampTickFrequency", "GEV时间戳刻度频率（Hz）" },
            {"GevTimestampControlLatch", "时间戳控制锁存" },
            {"GevTimestampControlReset", "时间戳控制重置" },
            {"GevTimestampControlLatchReset", "时间戳控制锁存重置" },
            {"GevTimestampValue", "时间戳值" },
            {"GevCCP", "GEV CCP" },
            {"GevMCPHostPort", "GEV MCP主机端口" },
            {"GevMCDA", "GEV MCDA" },
            {"GevStreamChannelSelector", "GEV流通道选择器" },
            {"GevSCPInterfaceIndex", "GEV SCP接口索引" },
            {"GevSCPHostPort", "GEV SCP主机端口" },
            {"GevSCPDirection", "GEV SCP方向" },
            {"GevSCPSFireTestPacket", "GEV SCPS防火测试包" },
            {"GevSCPSDoNotFragment", "GEV SCP不分段" },
            {"GevSCPSBigEndian", "GEV SCPS大端" },
            {"GevSCDA", "GEV SCDA" },
            {"GevSCSP", "GEV SCSP" },

            // --- 用户自定义设置 (User Set) ---
            { "UserSetSelector", "用户设置选择" },
            { "UserSetDefault", "默认用户设置" },
            { "UserSetCurrent", "当前用户设置" },

            // --- 其他参数 ---
            { "numbuffers", "缓冲区数量" },
            { "rotate", "旋转" },
            { "DeviceLinkHeartbeatMode", "心跳模式" },
            { "TestPattern", "测试图案" },
            { "BinningHorizontal", "水平合并" },
            { "BinningVertical", "垂直合并" },
            { "DecimationHorizontal", "水平抽取" },
            { "DecimationVertical", "垂直抽取" },

        };

            // 构造函数，接收我们自己的 CameraDevice 对象
            public ParametersWindow(CameraDevice cameraDevice)
        {
            InitializeComponent();
            m_pCameraDevice = cameraDevice;
            m_pAcqHandle = cameraDevice.AcqHandle; // 获取HALCON句柄

            TitleTextBlock.Text = $"相机参数: {m_pCameraDevice.DeviceID}";
            Loaded += (s, e) => PopulateParameters();
        }



        private void PopulateParameters()
        {
            ParametersStackPanel.Children.Clear();
            if (m_pAcqHandle == null) return;

            try
            {
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, "available_param_names", out HTuple allParamNames);

                for (int i = 0; i < allParamNames.Length; i++)
                {
                    string paramName = allParamNames[i].S;

                    // 跳过一些内部或只写参数
                    if (paramName.StartsWith("do_") || paramName.Contains("abort") || paramName.Contains("flush")) continue;

                    string displayName = parameterTranslations.ContainsKey(paramName) ? parameterTranslations[paramName] : paramName;

                    // *** 核心逻辑：按优先级尝试创建最合适的控件 ***
                    if (TryCreateEnumControl(paramName, displayName)) continue;
                    if (TryCreateNumericControlWithRange(paramName, displayName)) continue;
                    if (TryCreateGenericTextControl(paramName, displayName)) continue;
                }
            }
            catch (HalconException ex)
            {
                MessageBox.Show("获取参数列表时发生错误: " + ex.GetErrorMessage(), "错误");
            }
        }

        /// <summary>
        /// 尝试创建枚举控件 (ComboBox)。
        /// </summary>
        private bool TryCreateEnumControl(string paramName, string displayName)
        {
            try
            {
                // 1. 尝试查询 "_values" 后缀，获取所有可用选项
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_values", out HTuple availableValues);
                if (availableValues == null || availableValues.Length == 0) return false;

                // 2. 查询当前值
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);

                // 3. 创建 ComboBox
                var sp = CreateParameterRow(displayName);
                var cb = new ComboBox { Width = 230, Tag = paramName, VerticalAlignment = VerticalAlignment.Center };

                foreach (var val in availableValues.SArr)
                {
                    cb.Items.Add(val);
                }
                cb.SelectedItem = currentValue.S;

                cb.SelectionChanged += (s, e) =>
                {
                    if (cb.SelectedItem == null) return;
                    try
                    {
                        HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, cb.SelectedItem.ToString());
                    }
                    catch (HalconException hex) { Debug.WriteLine($"设置 {paramName} 失败: {hex.GetErrorMessage()}"); }
                };

                sp.Children.Add(cb);
                ParametersStackPanel.Children.Add(sp);
                return true; // 创建成功
            }
            catch (HalconException)
            {
                return false; // 查询 "_values" 失败，说明它不是一个受支持的枚举参数
            }
        }

        /// <summary>
        /// 尝试创建带范围的数值控件 (Slider + TextBox)。
        /// </summary>
        private bool TryCreateNumericControlWithRange(string paramName, string displayName)
        {
            try
            {
                // 1. 尝试查询 "_range" 后缀，获取范围 [min, max, step, default]
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_range", out HTuple range);
                if (range == null || range.Length < 2) return false;

                double min = range[0].D;
                double max = range[1].D;

                // *** 新增的安全检查 ***
                // 如果HALCON返回的范围是无穷大，WPF的Slider无法处理，所以我们放弃创建滑块。
                if (double.IsInfinity(min) || double.IsInfinity(max))
                {
                    return false;
                }

                double step = (range.Length > 2 && range[2].D > 0) ? range[2].D : 1.0;

                // 2. 查询当前值
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);

                // 3. 创建 Slider 和 TextBox
                var sp = CreateParameterRow(displayName);
                var slider = new Slider { Width = 150, Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = currentValue.D, VerticalAlignment = VerticalAlignment.Center };
                var txt = new TextBox { Width = 80, Text = currentValue.D.ToString("F2"), VerticalAlignment = VerticalAlignment.Center };

                slider.ValueChanged += (s, e) =>
                {
                    txt.Text = e.NewValue.ToString("F2");
                    try { HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, e.NewValue); }
                    catch (HalconException hex) { Debug.WriteLine($"设置 {paramName} 失败: {hex.GetErrorMessage()}"); }
                };

                sp.Children.Add(slider);
                sp.Children.Add(txt);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException)
            {
                return false;
            }
        }

        /// <summary>
        /// 创建通用的文本框控件（作为保底方案）。
        /// </summary>
        private bool TryCreateGenericTextControl(string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);

                var sp = CreateParameterRow(displayName);
                var txt = new TextBox { Width = 230, Tag = paramName, Text = currentValue.ToString(), VerticalAlignment = VerticalAlignment.Center };

                txt.LostFocus += (s, e) =>
                {
                    try
                    {
                        HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, new HTuple(txt.Text));
                        txt.Background = Brushes.LightGreen;
                    }
                    catch (HalconException)
                    {
                        txt.Background = Brushes.LightPink;
                        // 恢复旧值
                        try { HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple oldVal); txt.Text = oldVal.ToString(); } catch { }
                    }
                };

                sp.Children.Add(txt);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException)
            {
                return false; // 不可读的参数，彻底放弃
            }
        }

        // 辅助方法，保持不变
        private StackPanel CreateParameterRow(string displayName)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var label = new Label { Content = displayName, Width = 200, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(label);
            return sp;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => PopulateParameters();
    }
}




