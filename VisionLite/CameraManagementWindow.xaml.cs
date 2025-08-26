// CameraManagementWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HalconDotNet;
using MvCamCtrl.NET;
using System.Windows.Media;

namespace VisionLite
{
    public partial class CameraManagementWindow : Window
    {
        private readonly MainWindow m_pMainWindow; // 对主窗口的引用
        private HSmartWindowControlWPF m_pTargetWindow; // 存储当前操作的目标窗口
        private ICameraDevice m_pCurrentDevice; // 当前在参数面板中显示的设备

        public CameraManagementWindow(MainWindow owner, HSmartWindowControlWPF targetWindow)
        {
            InitializeComponent();
            this.Owner = owner;
            m_pMainWindow = owner;
            m_pTargetWindow = targetWindow;// 保存目标窗口
            // 窗口加载后立即查找设备
            Loaded += (s, e) => FindAndPopulateDevices();
            
            // 添加窗口关闭事件处理
            Closed += (s, e) =>
            {
                try
                {
                    if (m_pMainWindow != null)
                    {
                        m_pMainWindow.Activate();
                        m_pMainWindow.Focus();
                    }
                }
                catch { }
            };

            // 订阅主窗口的相机列表变化事件，以便同步UI
            m_pMainWindow.CameraListChanged += OnCameraListChanged;
        }

        // 从外部更新目标窗口
        public void SetTargetWindow(HSmartWindowControlWPF targetWindow)
        {
            m_pTargetWindow = targetWindow;
            // 目标窗口改变后，立即刷新设备列表以反映新的上下文
            FindAndPopulateDevices();
        }


        // 当主窗口的相机列表发生变化时，此方法被调用
        private void OnCameraListChanged(object sender, EventArgs e)
        {
            // 重新填充设备列表，并尝试保持当前选择
            var currentSelection = DeviceComboBox.SelectedItem as DeviceInfo;
            FindAndPopulateDevices();
            if (currentSelection != null)
            {
                // 查找并重新选中之前的设备
                var newSelection = DeviceComboBox.Items.OfType<DeviceInfo>().FirstOrDefault(d => d.UniqueID == currentSelection.UniqueID);
                if (newSelection != null)
                {
                    DeviceComboBox.SelectedItem = newSelection;
                }
            }

            // 刷新参数面板
            UpdateParametersForSelection();
        }

        #region Top Panel: Device Control

        private void FindCamButton_Click(object sender, RoutedEventArgs e)
        {
            FindAndPopulateDevices();
            // 在本窗口的状态栏显示结果
            int deviceCount = DeviceComboBox.Items.Count;
            string message = deviceCount > 0 ? $"查找成功！共发现 {deviceCount} 个设备。" : "未发现任何设备！";
            UpdateStatus(message, false);
        }

        private void OpenCamButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is DeviceInfo selectedDevice)
            {
                // 调用主窗口的公共方法，并获取返回结果
                var (success, message) = m_pMainWindow.OpenDevice(selectedDevice, m_pTargetWindow);
                // 将结果显示在状态栏
                UpdateStatus(message, !success);
            }
            else
            {
                UpdateStatus("请先选择一个有效的设备。", true);
            }
        }

        private void CloseCamButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is DeviceInfo selectedDevice)
            {
                // 调用主窗口的公共方法，并获取返回结果
                var (success, message) = m_pMainWindow.CloseDevice(m_pTargetWindow);
                // 将结果显示在状态栏
                UpdateStatus(message, !success);
            }
            else
            {
                UpdateStatus("请在列表中选择一个要关闭的设备。", true);
            }
        }

        public void FindAndPopulateDevices()
        {
            if (m_pTargetWindow == null) return;
            
            DeviceComboBox.Items.Clear();

            // 调用MainWindow的新方法，获取为【特定窗口】过滤后的设备列表
            //    这个列表只包含：
            //    a) 所有当前未被任何窗口占用的相机。
            //    b) 当前m_pTargetWindow已经连接的那个相机（如果有的话）。
            var foundDevices = m_pMainWindow.GetAvailableDevicesForWindow(m_pTargetWindow);

            if (foundDevices.Any())
            {
                foreach (var dev in foundDevices)
                {
                    DeviceComboBox.Items.Add(dev);
                }

                // 设置默认选中项
                // 查找当前目标窗口是否已经连接了相机
                var currentCameraOnTarget = m_pMainWindow.openCameras.Values
                                                .FirstOrDefault(c => c.DisplayWindow == m_pTargetWindow);
                if (currentCameraOnTarget != null)
                {
                    // 如果已连接，则在列表中找到它并设为选中项
                    var selection = foundDevices.FirstOrDefault(d => d.UniqueID == currentCameraOnTarget.DeviceID);
                    if (selection != null)
                    {
                        DeviceComboBox.SelectedItem = selection;
                    }
                   
                }
                else if (DeviceComboBox.Items.Count > 0)
                {
                    // 如果未连接，并且列表不为空，则默认选中第一项
                    DeviceComboBox.SelectedIndex = 0;
                }

                OpenCamButton.IsEnabled = true;
                
            }
            else
            {
                // 如果没有任何可用设备（所有设备都已被其他窗口占用，且当前窗口也未连接）
                DeviceComboBox.Items.Add("没有可用的设备");
                DeviceComboBox.SelectedIndex = 0;
                OpenCamButton.IsEnabled = false;

            }
        }

        #endregion

        #region Bottom Panel: Parameter Control

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParametersForSelection();
        }

        private void UpdateParametersForSelection()
        {
            if (DeviceComboBox.SelectedItem is DeviceInfo selectedDevice)
            {
                // 从主窗口的字典中查找这个设备是否已打开
                if (m_pMainWindow.openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice camera))
                {
                    m_pCurrentDevice = camera; // 更新当前设备引用
                    PopulateParameters(camera);
                }
                else
                {
                    m_pCurrentDevice = null;
                    ParametersStackPanel.Children.Clear();
                    ParametersStackPanel.Children.Add(new TextBlock
                    {
                        Text = "请先打开此设备以配置参数。",
                        Margin = new Thickness(10),
                        Foreground = Brushes.Gray
                    });
                }
            }
            else
            {
                m_pCurrentDevice = null;
                ParametersStackPanel.Children.Clear();
                ParametersStackPanel.Children.Add(new TextBlock
                {
                    Text = "请在上方选择一个已打开的设备以查看其参数。",
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Gray
                });
            }
        }

        private void PopulateParameters(ICameraDevice cameraDevice)
        {
            ParametersStackPanel.Children.Clear();
            if (cameraDevice == null) return;

            // 根据相机类型调用不同的参数填充逻辑
            if (cameraDevice is HalconCameraDevice halconCam)
            {
                PopulateHalconParameters(halconCam);
            }
            else if (cameraDevice is HikvisionCameraDevice hikCam)
            {
                PopulateHikvisionParameters(hikCam);
            }
        }

        private void SetParameter(string paramName, object value)
        {
            if (m_pCurrentDevice == null) return;

            try
            {
                bool success = m_pCurrentDevice.SetParameter(paramName, value);
                if (success)
                {
                    UpdateStatus($"参数 '{ParameterTranslator.Translate(paramName)}' 设置成功。", false);
                }
                PopulateParameters(m_pCurrentDevice);
            }
            catch (Exception ex)
            {
                UpdateStatus($"设置失败: {ex.Message}", true);
                PopulateParameters(m_pCurrentDevice);
            }
        }

        #endregion

        #region Halcon Parameter Logic
        private void PopulateHalconParameters(HalconCameraDevice cameraDevice)
        {
            HTuple acqHandle = cameraDevice.GetAcqHandle();
            if (acqHandle == null) return;

            HashSet<string> availableParamsSet;
            try
            {
                HOperatorSet.GetFramegrabberParam(acqHandle, "available_param_names", out HTuple allParamNames);
                availableParamsSet = new HashSet<string>(allParamNames.SArr);
            }
            catch (HalconException) { return; }

            var desiredParameters = new List<string>
            {
                "DeviceVendorName", "DeviceModelName", "DeviceVersion", "DeviceID", "DeviceUserID",
                "Width", "Height", "OffsetX", "OffsetY", "PixelFormat", "AcquisitionFrameRateEnable",
                "AcquisitionFrameRate", "ResultingFrameRate", "TriggerSource", "ExposureMode",
                "ExposureTime", "ExposureAuto", "Gain", "GainAuto", "BalanceWhiteAuto", "GammaEnable",
                "Gamma", "GevCurrentIPAddress", "GevPersistentIPAddress"
            };

            foreach (var paramName in desiredParameters)
            {
                if (availableParamsSet.Contains(paramName))
                {
                    string displayName = ParameterTranslator.Translate(paramName);
                    if (TryCreateHalconBooleanControl(acqHandle, paramName, displayName)) continue;
                    if (TryCreateHalconEnumControl(acqHandle, paramName, displayName)) continue;
                    if (TryCreateHalconNumericControl(acqHandle, paramName, displayName)) continue;
                    if (TryCreateHalconTextControl(acqHandle, paramName, displayName)) continue;
                }
            }
        }

        private bool TryCreateHalconBooleanControl(HTuple acqHandle, string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(acqHandle, paramName + "_values", out HTuple availableValues);
                if (availableValues == null || availableValues.Length != 2) return false;
                var values = availableValues.SArr.Select(v => v.ToLower()).ToList();
                if (!((values.Contains("true") && values.Contains("false")) || (values.Contains("on") && values.Contains("off")))) return false;

                HOperatorSet.GetFramegrabberParam(acqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var chk = new CheckBox { IsChecked = (currentValue.S.ToLower() == "true" || currentValue.S.ToLower() == "on"), VerticalAlignment = VerticalAlignment.Center };
                chk.Click += (s, e) => SetParameter(paramName, chk.IsChecked ?? false);
                sp.Children.Add(chk);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException) { return false; }
        }

        private bool TryCreateHalconEnumControl(HTuple acqHandle, string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(acqHandle, paramName + "_values", out HTuple availableValues);
                if (availableValues == null || availableValues.Length <= 1) return false;

                HOperatorSet.GetFramegrabberParam(acqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var cb = new ComboBox { Width = 230, VerticalAlignment = VerticalAlignment.Center };
                foreach (var val in availableValues.SArr) cb.Items.Add(val);
                cb.SelectedItem = currentValue.S;
                cb.SelectionChanged += (s, e) => { if (cb.SelectedItem != null && cb.IsDropDownOpen) SetParameter(paramName, cb.SelectedItem.ToString()); };
                sp.Children.Add(cb);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException) { return false; }
        }

        private bool TryCreateHalconNumericControl(HTuple acqHandle, string paramName, string displayName)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(acqHandle, paramName + "_range", out HTuple range);
                if (range == null || range.Length < 2) return false;
                double min = range[0].D; double max = range[1].D;
                if (double.IsInfinity(min) || double.IsInfinity(max)) return false;

                double step = 1.0;
                try
                {
                    HOperatorSet.GetFramegrabberParam(acqHandle, paramName + "_step", out HTuple stepVal);
                    if (stepVal > 0) step = stepVal.D;
                }
                catch (HalconException) { }

                HOperatorSet.GetFramegrabberParam(acqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var slider = new Slider { Width = 150, Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = currentValue.D, VerticalAlignment = VerticalAlignment.Center };
                var txt = new TextBox { Width = 80, Text = currentValue.D.ToString("F0"), VerticalAlignment = VerticalAlignment.Center };

                slider.ValueChanged += (s, e) => { if (slider.IsFocused) txt.Text = e.NewValue.ToString("F0"); };

                slider.Loaded += (sender, args) =>
                {
                    var thumb = GetSliderThumb(sender as Slider);
                    if (thumb != null)
                    {
                        thumb.DragCompleted += (s_thumb, e_thumb) => SetParameter(paramName, slider.Value);
                    }
                };

                txt.LostFocus += (s, e) => {
                    if (double.TryParse(txt.Text, out double newValue))
                    {
                        if (newValue < slider.Minimum) newValue = slider.Minimum;
                        if (newValue > slider.Maximum) newValue = slider.Maximum;
                        newValue = Math.Round(newValue / step) * step;
                        SetParameter(paramName, newValue);
                    }
                    else PopulateParameters(m_pCurrentDevice);
                };
                sp.Children.Add(slider); sp.Children.Add(txt);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException) { return false; }
        }

        private bool TryCreateHalconTextControl(HTuple acqHandle, string paramName, string displayName)
        {
            try
            {
                bool isReadOnly = false;
                try
                {
                    HOperatorSet.GetFramegrabberParam(acqHandle, paramName + "_query", out HTuple queryResult);
                    if (!queryResult.S.Contains("write")) isReadOnly = true;
                }
                catch (HalconException) { isReadOnly = true; }

                HOperatorSet.GetFramegrabberParam(acqHandle, paramName, out HTuple currentValue);
                var sp = CreateParameterRow(displayName);
                var txt = new TextBox { Width = 230, Text = currentValue.ToString(), VerticalAlignment = VerticalAlignment.Center, IsReadOnly = isReadOnly, Background = isReadOnly ? Brushes.LightGray : Brushes.White };
                if (!isReadOnly) txt.LostFocus += (s, e) => SetParameter(paramName, txt.Text);
                sp.Children.Add(txt);
                ParametersStackPanel.Children.Add(sp);
                return true;
            }
            catch (HalconException) { return false; }
        }
        #endregion

        #region Hikvision Parameter Logic
        private void PopulateHikvisionParameters(HikvisionCameraDevice cameraDevice)
        {
            var sdkObject = cameraDevice.CameraSdkObject;
            if (sdkObject == null || !sdkObject.MV_CC_IsDeviceConnected_NET()) return;

            var commonParameters = new List<string>
            {
                "Width", "Height", "OffsetX", "OffsetY", "PixelFormat", "AcquisitionMode",
                "AcquisitionFrameRateEnable", "AcquisitionFrameRate", "TriggerMode", "TriggerSource",
                "ExposureAuto", "ExposureTime", "GainAuto", "Gain", "BalanceWhiteAuto", "BalanceRatioSelector",
                "BalanceRatio", "GammaEnable", "Gamma", "DeviceUserID", "DeviceModelName", "DeviceVendorName"
            };

            foreach (var paramName in commonParameters)
            {
                string displayName = ParameterTranslator.Translate(paramName);
                if (TryCreateHikEnumControl(sdkObject, paramName, displayName)) continue;
                if (TryCreateHikIntegerControl(sdkObject, paramName, displayName)) continue;
                if (TryCreateHikFloatControl(sdkObject, paramName, displayName)) continue;
                if (TryCreateHikStringControl(sdkObject, paramName, displayName, true)) continue;
            }
        }

        private bool TryCreateHikIntegerControl(MyCamera sdkObject, string paramName, string displayName)
        {
            MyCamera.MVCC_INTVALUE_EX intValue = new MyCamera.MVCC_INTVALUE_EX();
            if (sdkObject.MV_CC_GetIntValueEx_NET(paramName, ref intValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var slider = new Slider { Minimum = intValue.nMin, Maximum = intValue.nMax, Value = intValue.nCurValue, Width = 150, VerticalAlignment = VerticalAlignment.Center, TickFrequency = intValue.nInc > 1 ? intValue.nInc : 1, IsSnapToTickEnabled = true };
            var txt = new TextBox { Text = intValue.nCurValue.ToString(), Width = 80, VerticalAlignment = VerticalAlignment.Center };

            slider.ValueChanged += (s, e) => { if (slider.IsFocused) txt.Text = ((long)e.NewValue).ToString(); };

            slider.Loaded += (sender, args) =>
            {
                var thumb = GetSliderThumb(sender as Slider);
                if (thumb != null)
                {
                    thumb.DragCompleted += (s_thumb, e_thumb) => SetParameter(paramName, (long)slider.Value);
                }
            };

            txt.LostFocus += (s, e) => {
                if (long.TryParse(txt.Text, out long newValue))
                {
                    if (newValue < slider.Minimum) newValue = (long)slider.Minimum;
                    if (newValue > slider.Maximum) newValue = (long)slider.Maximum;
                    SetParameter(paramName, newValue);
                }
                else { PopulateParameters(m_pCurrentDevice); }
            };

            sp.Children.Add(slider); sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        private bool TryCreateHikFloatControl(MyCamera sdkObject, string paramName, string displayName)
        {
            MyCamera.MVCC_FLOATVALUE floatValue = new MyCamera.MVCC_FLOATVALUE();
            if (sdkObject.MV_CC_GetFloatValue_NET(paramName, ref floatValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var txt = new TextBox { Text = floatValue.fCurValue.ToString("F2"), Width = 230, VerticalAlignment = VerticalAlignment.Center };
            txt.LostFocus += (s, e) => { if (float.TryParse(txt.Text, out float newValue)) SetParameter(paramName, newValue); };
            sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        private bool TryCreateHikEnumControl(MyCamera sdkObject, string paramName, string displayName)
        {
            MyCamera.MVCC_ENUMVALUE enumValue = new MyCamera.MVCC_ENUMVALUE();
            if (sdkObject.MV_CC_GetEnumValue_NET(paramName, ref enumValue) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var cb = new ComboBox { Width = 230 };
            string currentSymbolic = "";
            for (int i = 0; i < enumValue.nSupportedNum; i++)
            {
                uint nValue = enumValue.nSupportValue[i];
                MyCamera.MVCC_ENUMENTRY stEntry = new MyCamera.MVCC_ENUMENTRY { nValue = nValue };
                if (sdkObject.MV_CC_GetEnumEntrySymbolic_NET(paramName, ref stEntry) == 0)
                {
                    string symbolicName = Encoding.ASCII.GetString(stEntry.chSymbolic).TrimEnd('\0');
                    cb.Items.Add(symbolicName);
                    if (stEntry.nValue == enumValue.nCurValue) { currentSymbolic = symbolicName; }
                }
            }
            if (!string.IsNullOrEmpty(currentSymbolic)) { cb.SelectedItem = currentSymbolic; }
            cb.SelectionChanged += (s, e) => { if (cb.SelectedItem != null && cb.IsDropDownOpen) SetParameter(paramName, cb.SelectedItem.ToString()); };
            sp.Children.Add(cb);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }

        private bool TryCreateHikStringControl(MyCamera sdkObject, string paramName, string displayName, bool isReadOnly)
        {
            MyCamera.MVCC_STRINGVALUE stString = new MyCamera.MVCC_STRINGVALUE();
            if (sdkObject.MV_CC_GetStringValue_NET(paramName, ref stString) != 0) return false;

            var sp = CreateParameterRow(displayName);
            var txt = new TextBox { Text = stString.chCurValue, Width = 230, IsReadOnly = isReadOnly, Background = isReadOnly ? Brushes.LightGray : Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(txt);
            ParametersStackPanel.Children.Add(sp);
            return true;
        }
        #endregion

        #region Common Helper Methods
        private StackPanel CreateParameterRow(string displayName)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var label = new Label { Content = displayName, Width = 150, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(label);
            return sp;
        }

        private Thumb GetSliderThumb(Slider slider)
        {
            if (slider == null) return null;
            slider.ApplyTemplate();
            var track = slider.Template?.FindName("PART_Track", slider) as Track;
            return track?.Thumb;
        }

        private async void UpdateStatus(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Black;
            await Task.Delay(5000);
            if (StatusTextBlock.Text == message)
            {
                StatusTextBlock.Text = "准备就绪";
                StatusTextBlock.Foreground = Brushes.Black;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (m_pMainWindow != null)
            {
                m_pMainWindow.CameraListChanged -= OnCameraListChanged;
                m_pMainWindow.NotifyCameraManagementWindowClosed();
            }
        }
        #endregion
    }
}