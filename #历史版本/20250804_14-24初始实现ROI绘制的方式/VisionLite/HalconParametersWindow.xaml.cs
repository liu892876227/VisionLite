// HalconParametersWindow.xaml.cs 

using System.Windows.Controls.Primitives;

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
    public partial class HalconParametersWindow : Window
    {
        private readonly HalconCameraDevice m_pCameraDevice;
        private readonly HTuple m_pAcqHandle;
        
        public HalconParametersWindow(HalconCameraDevice cameraDevice)
        {
            InitializeComponent();
            m_pCameraDevice = cameraDevice;
            m_pAcqHandle = cameraDevice.GetAcqHandle();

            TitleTextBlock.Text = $"相机参数: {m_pCameraDevice.DeviceID}";
            Loaded += (s, e) => PopulateParameters();
        }

        private void PopulateParameters()
        {
            // 记录当前拥有焦点的控件的名称，以便刷新后恢复焦点
            string focusedControlName = (FocusManager.GetFocusedElement(this) as FrameworkElement)?.Name;

            ParametersStackPanel.Children.Clear();
            if (m_pAcqHandle == null) return;

            // --- 获取相机支持的所有参数 ---
            HashSet<string> availableParamsSet;

            try
            {
                // 先查询相机所有可用的参数名称
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, "available_param_names", out HTuple allParamNames);
                // 转换为HashSet以便快速查询 (O(1)的查询效率)
                availableParamsSet = new HashSet<string>(allParamNames.SArr);
            }
            catch (HalconException)
            {
                // 如果连查询可用参数列表都失败了，就直接返回，不再继续
                return;
            }

            var desiredParameters = new List<string>
            {
                "DeviceVendorName", "DeviceModelName", "DeviceVersion",
                "DeviceID", "DeviceUserID",
                "Width", "Height", "OffsetX", "OffsetY", "PixelFormat",
                "AcquisitionFrameRateEnable", "AcquisitionFrameRate", "ResultingFrameRate",
                "TriggerSource", "ExposureMode", "ExposureTime", "ExposureAuto", "Gain", "GainAuto",
                "BalanceWhiteAuto", "GammaEnable", "Gamma",
                "GevCurrentIPAddress", "GevPersistentIPAddress"
            };

            foreach (var paramName in desiredParameters)
            {
                // --- 修正点 2: 在尝试创建控件前，先检查该参数是否受支持 ---
                if (availableParamsSet.Contains(paramName))
                {
                    string displayName = ParameterTranslator.Translate(paramName);

                    if (TryCreateBooleanControl(paramName, displayName)) continue;
                    if (TryCreateEnumControl(paramName, displayName)) continue;
                    if (TryCreateNumericControlWithRange(paramName, displayName)) continue;
                    if (TryCreateGenericTextControl(paramName, displayName)) continue;
                }

            }

            // 尝试恢复焦点
            if (!string.IsNullOrEmpty(focusedControlName))
            {
                var controlToFocus = (UIElement)ParametersStackPanel.FindName(focusedControlName);
                controlToFocus?.Focus();
            }
        }

        // --- 修正后的 SetParameter 方法 ---
        private void SetParameter(string paramName, object value)
        {
            // 暂时禁用窗口防止用户连续操作
            this.IsEnabled = false;

            try
            {
                // 调用设备层的方法来执行设置，只调用一次！
                m_pCameraDevice.SetParameter(paramName, value);

                // 无论成功与否，都完全刷新参数列表
                // - 如果成功，会显示新值以及可能连锁改变的其他参数
                // - 如果失败，会从相机重新读取旧的、实际的值，纠正UI
                PopulateParameters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"在调用 SetParameter 时发生未知错误: {ex.Message}");
                PopulateParameters(); // 出错时也刷新以恢复UI
            }
            finally
            {
                // 确保在操作完成后重新启用窗口
                this.IsEnabled = true;
            }
        }

        private bool TryCreateBooleanControl(string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_values", out HTuple availableValues);
                if (availableValues == null || availableValues.Length != 2) return false;

                var values = availableValues.SArr.Select(v => v.ToLower()).ToList();
                if (!((values.Contains("true") && values.Contains("false")) || (values.Contains("on") && values.Contains("off"))))
                {
                    return false;
                }

                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var chk = new CheckBox { IsChecked = (currentValue.S.ToLower() == "true" || currentValue.S.ToLower() == "on"), VerticalAlignment = VerticalAlignment.Center };
                chk.Name = paramName; // 给控件命名以便恢复焦点

                chk.Click += (s, e) => SetParameter(paramName, chk.IsChecked ?? false);

                sp.Children.Add(chk);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException) { return false; }
        }

        private bool TryCreateEnumControl(string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_values", out HTuple availableValues);
                if (availableValues == null || availableValues.Length <= 1) return false;

                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var cb = new ComboBox { Width = 230, VerticalAlignment = VerticalAlignment.Center };
                cb.Name = paramName; // 给控件命名

                foreach (var val in availableValues.SArr)
                {
                    cb.Items.Add(val);
                }
                cb.SelectedItem = currentValue.S;

                cb.SelectionChanged += (s, e) =>
                {
                    if (cb.SelectedItem == null || !cb.IsDropDownOpen) return;
                    SetParameter(paramName, cb.SelectedItem.ToString());
                };

                sp.Children.Add(cb);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException)
            {
                return false;
            }
        }

        private bool TryCreateNumericControlWithRange(string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_range", out HTuple range);
                if (range == null || range.Length < 2) return false;

                double min = range[0].D;
                double max = range[1].D;
                if (double.IsInfinity(min) || double.IsInfinity(max)) return false;

                double step = 1.0;
                try
                {
                    HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_step", out HTuple stepVal);
                    if (stepVal > 0) step = stepVal.D;
                }
                catch (HalconException) { /* 忽略错误 */ }

                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var slider = new Slider { Width = 150, Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = currentValue.D, VerticalAlignment = VerticalAlignment.Center };
                var txt = new TextBox { Width = 80, Text = currentValue.D.ToString("F0"), VerticalAlignment = VerticalAlignment.Center };

                // 给控件命名
                slider.Name = paramName + "_slider";
                txt.Name = paramName + "_text";

                slider.ValueChanged += (s, e) =>
                {
                    if (slider.IsFocused)
                        txt.Text = e.NewValue.ToString("F0");
                };

                slider.Loaded += (sender, args) =>
                {
                    var thumb = GetSliderThumb(sender as Slider);
                    if (thumb != null)
                    {
                        thumb.DragCompleted += (s_thumb, e_thumb) =>
                        {
                            SetParameter(paramName, slider.Value);
                        };
                    }
                };

                txt.LostFocus += (s, e) =>
                {
                    if (double.TryParse(txt.Text, out double newValue))
                    {
                        if (newValue < slider.Minimum) newValue = slider.Minimum;
                        if (newValue > slider.Maximum) newValue = slider.Maximum;
                        newValue = Math.Round(newValue / step) * step;
                        SetParameter(paramName, newValue);
                    }
                    else
                    {
                        PopulateParameters();
                    }
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

        private bool TryCreateGenericTextControl(string paramName, string displayName)
        {
            try
            {
                bool isReadOnly = false;
                try
                {
                    HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName + "_query", out HTuple queryResult);
                    if (!queryResult.S.Contains("write")) isReadOnly = true;
                }
                catch (HalconException) { isReadOnly = true; }

                HOperatorSet.GetFramegrabberParam(m_pAcqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var txt = new TextBox
                {
                    Width = 230,
                    Text = currentValue.ToString(),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsReadOnly = isReadOnly,
                    Background = isReadOnly ? Brushes.LightGray : Brushes.White
                };
                txt.Name = paramName; // 给控件命名

                if (!isReadOnly)
                {
                    txt.LostFocus += (s, e) => SetParameter(paramName, txt.Text);
                }

                sp.Children.Add(txt);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException)
            {
                return false;
            }
        }

        private StackPanel CreateParameterRow(string displayName)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var label = new Label { Content = displayName, Width = 200, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(label);
            return sp;
        }

        private Thumb GetSliderThumb(Slider slider)
        {
            if (slider == null) return null;
            var track = slider.Template?.FindName("PART_Track", slider) as Track;
            return track?.Thumb;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => PopulateParameters();
    }
}