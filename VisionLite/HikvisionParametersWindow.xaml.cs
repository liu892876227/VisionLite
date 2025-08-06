// HikvisionParametersWindow.cs
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

namespace VisionLite
{
    /// <summary>
    /// HikvisionParametersWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HikvisionParametersWindow : Window
    {
        private ICameraDevice m_pCameraDevice;
        private MyCamera m_pMyCamera;
        public HikvisionParametersWindow(ICameraDevice cameraDevice, MyCamera sdkObject)
        {
            InitializeComponent();
            m_pCameraDevice = cameraDevice;
            m_pMyCamera = sdkObject; // 保存SDK对象引用
            // --- 增加对相机对象的有效性检查 ---
            if (m_pMyCamera == null || !m_pMyCamera.MV_CC_IsDeviceConnected_NET())
            {
                // 如果传入的相机对象是无效的，直接显示错误并阻止后续操作
                MessageBox.Show("传入的相机句柄无效或设备未连接，无法加载参数！", "错误");
                // 可以在这里直接关闭窗口，或者显示一个错误信息
                this.Content = new TextBlock
                {
                    Text = "无法加载参数，请关闭窗口重试。",
                    Margin = new Thickness(20),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                return; // 阻止 Loaded 事件的订阅
            }
            Loaded += (s, e) => PopulateParameters();
        }


        /// <summary>
        /// 核心方法：动态发现并创建参数UI控件
        /// </summary>
        private void PopulateParameters()
        {
            ParametersStackPanel.Children.Clear();

            // 1. 定义一个包含所有我们关心的、常见参数的列表
            var commonParameters = new List<string>
            {
                // --- 图像格式与ROI ---
                "Width", "Height", "OffsetX", "OffsetY", "PixelFormat",
                // --- 采集与触发 ---
                "AcquisitionMode", "AcquisitionFrameRateEnable", "AcquisitionFrameRate", "TriggerMode", "TriggerSource",
                // --- 曝光与增益 ---
                "ExposureAuto", "ExposureTime", "GainAuto", "Gain",
                // --- 白平衡 ---
                "BalanceWhiteAuto", "BalanceRatioSelector", "BalanceRatio",
                // --- 图像处理 ---
                "GammaEnable", "Gamma", "SharpnessEnable", "Sharpness", "SaturationEnable", "Saturation",
                // --- 只读设备信息 ---
                "DeviceUserID", "DeviceModelName", "DeviceVendorName", "DeviceFirmwareVersion", "DeviceSerialNumber"
            };
            int createdControls = 0;

            // 2. 遍历这个列表
            foreach (var paramName in commonParameters)
            {
                string displayName = ParameterTranslator.Translate(paramName);
                // 按顺序尝试为每个参数创建合适的UI控件。
                // 如果一种类型创建成功 (返回true)，就用 continue 跳到列表中的下一个参数。
                if (TryCreateEnumControl(paramName, displayName)) { createdControls++; continue; }
                if (TryCreateIntegerControl(paramName, displayName)) { createdControls++; continue; }
                if (TryCreateFloatControl(paramName, displayName)) { createdControls++; continue; }
                if (TryCreateStringControl(paramName, displayName, true)) { createdControls++; continue; }
                // 如果以上都不是，它可能是一个我们未处理的类型或当前不可用，程序会静默跳过。
            }

            // 如果遍历完后一个控件都没能创建成功，给用户一个提示
            if (createdControls == 0)
            {
                ParametersStackPanel.Children.Add(new TextBlock
                {
                    Text = "未能从此设备加载任何可配置的参数。",
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        private void SetParameter(string paramName, object value)
        {
            try
            {
                // 调用设备层的方法
                bool success = m_pCameraDevice.SetParameter(paramName, value);

                // 成功后刷新UI并给出提示
                if (success)
                {
                    UpdateStatus($"参数 '{ParameterTranslator.Translate(paramName)}' 设置成功。");
                }
                PopulateParameters();
            }
            catch (Exception ex)
            {
                // 捕获从设备层抛出的异常
                UpdateStatus($"设置失败: {ex.Message}", true);
                // 出错时也刷新UI，以恢复到正确的参数状态
                PopulateParameters();
            }
        }

        /// <summary>
        /// 尝试创建一个整数(Integer)参数的UI控件（滑块+文本框）。
        /// </summary>
        /// <returns>如果成功创建则返回true，否则返回false。</returns>
        private bool TryCreateIntegerControl(string paramName, string displayName)
        {
            MyCamera.MVCC_INTVALUE_EX intValue = new MyCamera.MVCC_INTVALUE_EX();
            if (m_pMyCamera.MV_CC_GetIntValueEx_NET(paramName, ref intValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var slider = new Slider { Minimum = intValue.nMin, Maximum = intValue.nMax, Value = intValue.nCurValue, Width = 150, VerticalAlignment = VerticalAlignment.Center, Tag = paramName, TickFrequency = intValue.nInc > 1 ? intValue.nInc : 1, IsSnapToTickEnabled = true };
            var txt = new TextBox { Text = intValue.nCurValue.ToString(), Width = 80, VerticalAlignment = VerticalAlignment.Center };

            slider.ValueChanged += (s, e) => { if (slider.IsFocused) txt.Text = ((long)e.NewValue).ToString(); };

            slider.Loaded += (sender, args) =>
            {
                var thumb = (sender as Slider)?.GetThumb();
                if (thumb != null)
                {
                    thumb.DragCompleted += (s_thumb, e_thumb) => { SetParameter(paramName, (long)slider.Value); };
                }
            };

            txt.LostFocus += (s, e) => {
                if (long.TryParse(txt.Text, out long newValue))
                {
                    if (newValue < slider.Minimum) newValue = (long)slider.Minimum;
                    if (newValue > slider.Maximum) newValue = (long)slider.Maximum;
                    SetParameter(paramName, newValue);
                }
                else { txt.Text = ((long)slider.Value).ToString(); }
            };

            sp.Children.Add(slider);
            sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }


        /// <summary>
        /// 尝试创建一个浮点数(Float)参数的UI控件（文本框）。
        /// </summary>
        /// <returns>如果成功创建则返回true，否则返回false。</returns>
        private bool TryCreateFloatControl(string paramName, string displayName)
        {
            MyCamera.MVCC_FLOATVALUE floatValue = new MyCamera.MVCC_FLOATVALUE();
            if (m_pMyCamera.MV_CC_GetFloatValue_NET(paramName, ref floatValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var txt = new TextBox { Text = floatValue.fCurValue.ToString("F2"), Width = 230, VerticalAlignment = VerticalAlignment.Center, Tag = paramName };
            txt.LostFocus += (s, e) => { if (float.TryParse(txt.Text, out float newValue)) { SetParameter(paramName, newValue); } };
            sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        /// <summary>
        /// 尝试创建一个枚举(Enum)参数的UI控件（下拉框）。
        /// </summary>
        /// <returns>如果成功创建则返回true，否则返回false。</returns>
        private bool TryCreateEnumControl(string paramName, string displayName)
        {
            MyCamera.MVCC_ENUMVALUE enumValue = new MyCamera.MVCC_ENUMVALUE();
            if (m_pMyCamera.MV_CC_GetEnumValue_NET(paramName, ref enumValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var cb = new ComboBox { Width = 230, Tag = paramName };
            string currentSymbolic = "";
            for (int i = 0; i < enumValue.nSupportedNum; i++)
            {
                uint nValue = enumValue.nSupportValue[i];
                MyCamera.MVCC_ENUMENTRY stEntry = new MyCamera.MVCC_ENUMENTRY { nValue = nValue };
                if (m_pMyCamera.MV_CC_GetEnumEntrySymbolic_NET(paramName, ref stEntry) == 0)
                {
                    string symbolicName = Encoding.ASCII.GetString(stEntry.chSymbolic).TrimEnd('\0');
                    cb.Items.Add(symbolicName);
                    if (stEntry.nValue == enumValue.nCurValue) { currentSymbolic = symbolicName; }
                }
            }
            if (!string.IsNullOrEmpty(currentSymbolic)) { cb.SelectedItem = currentSymbolic; }
            cb.SelectionChanged += (s, e) => { if (cb.SelectedItem != null) { SetParameter(paramName, cb.SelectedItem.ToString()); } };
            sp.Children.Add(cb);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        /// <summary>
        /// 尝试创建一个字符串(String)参数的UI控件（只读文本框）。
        /// </summary>
        /// <returns>如果成功创建则返回true，否则返回false。</returns>
        private bool TryCreateStringControl(string paramName, string displayName, bool isReadOnly)
        {
            MyCamera.MVCC_STRINGVALUE stString = new MyCamera.MVCC_STRINGVALUE();
            if (m_pMyCamera.MV_CC_GetStringValue_NET(paramName, ref stString) != 0) return false;
            var sp = CreateParameterRow(displayName);
            var txt = new TextBox { Text = stString.chCurValue, Width = 230, IsReadOnly = isReadOnly, Background = isReadOnly ? Brushes.LightGray : Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        /// <summary>
        /// 辅助方法：创建一行UI（标签 + ...），保持代码整洁
        /// </summary>
        private StackPanel CreateParameterRow(string displayName)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var label = new Label { Content = displayName, Width = 150, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(label);
            return sp;
        }

        private async void UpdateStatus(string message, bool isError = false)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Black;
            await System.Threading.Tasks.Task.Delay(5000);
            if (StatusTextBlock.Text == message)
            {
                StatusTextBlock.Text = "准备就绪";
                StatusTextBlock.Foreground = Brushes.Black;
            }
        }

    }
    // 辅助扩展方法，用于获取Slider内部的Thumb控件
    public static class SliderExtensions
    {
        public static System.Windows.Controls.Primitives.Thumb GetThumb(this Slider slider)
        {
            // 增加一个模板是否为空的检查，让代码更健壮
            if (slider.Template == null)
            {
                // 应用模板，以便可以找到子控件。这在某些时序下可能是必要的。
                slider.ApplyTemplate();
            }
            var track = slider.Template.FindName("PART_Track", slider) as System.Windows.Controls.Primitives.Track;
            return track?.Thumb;
        }
    }
}
