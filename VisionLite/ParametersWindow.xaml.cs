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
using HalconDotNet;

namespace VisionLite
{
    /// <summary>
    /// ParametersWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ParametersWindow : Window
    {
        private Dictionary<string, string> parameterTranslations = new Dictionary<string, string>
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
            { "GevPersistentIPAddress", "固定IP地址" },
            { "GevPersistentSubnetMask", "固定子网掩码" },
            { "GevPersistentDefaultGateway", "固定默认网关" },
            { "GevCurrentIPAddress", "当前IP地址" },
            { "GevCurrentSubnetMask", "当前子网掩码" },
            { "GevCurrentDefaultGateway", "当前默认网关" },
            { "GevSCPSPacketSize", "网络数据包大小" },
            { "PayloadSize", "有效负载大小" },
            { "GevSCPD", "网络包间隔" },
            { "GevLinkSpeed", "网络连接速度" },
            { "GevMACAddress", "MAC地址" },

            // --- 用户自定义设置 (User Set) ---
            { "UserSetSelector", "用户设置选择" },
            { "UserSetDefault", "默认用户设置" },
            { "UserSetCurrent", "当前用户设置" },

            // --- 其他参数 (根据截图补充) ---
            { "numbuffers", "缓冲区数量" },
            { "rotate", "旋转" },
            { "DeviceLinkHeartbeatMode", "心跳模式" },
            { "TestPattern", "测试图案" },
            { "BinningHorizontal", "水平合并" },
            { "BinningVertical", "垂直合并" },
            { "DecimationHorizontal", "水平抽取" },
            { "DecimationVertical", "垂直抽取" },

        };

        private CameraDevice _cameraDevice;
        private HTuple _acqHandle;

        public ParametersWindow(CameraDevice cameraDevice)
        {
            InitializeComponent();
            _cameraDevice = cameraDevice;
            _acqHandle = cameraDevice.AcqHandle;

            // 设置窗口标题
            TitleTextBlock.Text = $"相机参数: {cameraDevice.DeviceID}";

            // 首次加载时填充参数
            PopulateParameters();
        }

        /// <summary>
        /// 查询并动态生成参数控件
        /// </summary>
        private void PopulateParameters()
        {
            // 清空旧的控件
            ParametersStackPanel.Children.Clear();

            if (_acqHandle == null) return;

            try
            {
                // 获取所有可用的参数名称
                HOperatorSet.GetFramegrabberParam(_acqHandle, "available_param_names", out HTuple paramNames);
                
                for (int i = 0; i < paramNames.Length; i++)
                {
                    string paramName = paramNames[i].S;

                    //调试输出
                    Console.WriteLine($"paramName:{paramName}");

                    try
                    {
                        HOperatorSet.GetFramegrabberParam(_acqHandle, paramName, out HTuple currentValue);

                        // 如果能成功获取到值，就为它创建一个UI控件
                        StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

                        //在创建Label时，查字典进行翻译
                        string displayName = paramName; // 默认显示英文名
                        if (parameterTranslations.ContainsKey(paramName))
                        {
                            // 如果字典里找到了翻译，就使用中文名
                            displayName = parameterTranslations[paramName];
                        }

                        // 创建参数名称标签，显示翻译后的中文名
                        Label label = new Label
                        {
                            Content = displayName,
                            Width = 250,
                            VerticalAlignment = VerticalAlignment.Center,
                            ToolTip = $"原始参数名: {paramName}" // 附加功能：鼠标悬停时显示原始英文名
                        };
                        sp.Children.Add(label);

                        // 3. 简化处理：我们假设所有能读到的参数都可以尝试去写。
                        //    用 TextBox 来显示和编辑。
                        //    MVision接口不支持枚举值列表查询，所以我们不再创建ComboBox。
                        TextBox txt = new TextBox
                        {
                            Width = 200,
                            Text = currentValue.ToString(),
                            Tag = paramName
                        };

                        // 检查参数是否为只读（这是一个变通的方法）
                        // 我们可以尝试写入一次，如果失败，就标记为只读。
                        // 但更简单的方法是，先假设都可写，在用户编辑时再处理异常。
                        // 这里我们直接创建可编辑的TextBox。
                        txt.LostFocus += TextBox_LostFocus;

                        sp.Children.Add(txt);
                        ParametersStackPanel.Children.Add(sp);
                    }
                    catch (HalconException)
                    {
                        // 如果GetFramegrabberParam失败（例如参数是只写的），
                        // 我们就简单地忽略这个参数，不为它创建任何UI控件。
                        Console.WriteLine($"无法读取参数 '{paramName}'，已跳过。");
                    }
                }
            }
            catch (HalconException ex)
            {
                MessageBox.Show("获取相机参数失败: " + ex.GetErrorMessage(), "错误");
            }
        }

        /// <summary>
        /// 当TextBox失去焦点时，尝试更新相机参数
        /// </summary>
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var txt = sender as TextBox;
            string paramName = txt.Tag.ToString();
            string newValue = txt.Text;

            try
            {
                // 尝试将新值设置回相机
                HOperatorSet.SetFramegrabberParam(_acqHandle, paramName, new HTuple(newValue));
                txt.Background = Brushes.LightGreen; // 成功则变绿
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"设置参数 '{paramName}' 失败: {ex.GetErrorMessage()}", "错误");
                txt.Background = Brushes.LightPink; // 失败则变粉
                // 失败后恢复旧值
                HOperatorSet.GetFramegrabberParam(_acqHandle, paramName, out HTuple oldValue);
                txt.Text = oldValue.ToString();
            }
        }

        /// <summary>
        /// 当ComboBox选项改变时，更新相机参数
        /// </summary>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb.SelectedItem == null) return;

            string paramName = cb.Tag.ToString();
            string newValue = cb.SelectedItem.ToString();

            try
            {
                HOperatorSet.SetFramegrabberParam(_acqHandle, paramName, newValue);
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"设置参数 '{paramName}' 失败: {ex.GetErrorMessage()}", "错误");
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            PopulateParameters();
        }
    }
}
