//MainWindow.xaml.cs
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using HalconDotNet;
using MvCamCtrl.NET;
using System.Runtime.InteropServices;


namespace VisionLite
{
    /// <summary>
    /// 应用程序的主窗口，负责UI交互和整体逻辑协调
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// 管理所有已打开的相机设备。
        /// 使用字典可以快速通过唯一的设备ID（如序列号）找到对应的相机对象。
        /// 键(Key): 设备的唯一ID字符串。
        /// 值(Value): 实现ICameraDevice接口的相机对象实例。
        /// </summary>
        internal Dictionary<string, ICameraDevice> openCameras = new Dictionary<string, ICameraDevice>();

        // 添加一个字段来跟踪相机管理窗口实例
        private CameraManagementWindow cameraManagementWindow = null;

        // 添加一个公共事件，当相机列表（打开/关闭）发生变化时触发
        public event EventHandler CameraListChanged;

        /// <summary>
        /// 管理所有用于显示图像的WPF控件。
        /// 使用列表可以方便地按顺序查找空闲窗口。
        /// </summary>
        private List<HSmartWindowControlWPF> displayWindows;

        // 用于跟踪已打开的ROI窗口，防止重复打开
        private ROIToolWindow roiEditorWindow = null;

        // 用于跟踪当前活动窗口的字段
        private HSmartWindowControlWPF _activeDisplayWindow;

        public MainWindow()
        {
            InitializeComponent();

            // 初始化显示窗口列表
            displayWindows = new List<HSmartWindowControlWPF>
            {
                HSmart1, HSmart2, HSmart3, HSmart4
            };

            // 为每个窗口添加鼠标点击事件处理器
            foreach (var window in displayWindows)
            {
                window.PreviewMouseDown += DisplayWindow_PreviewMouseDown;
            }

            // 在启动时，默认激活第一个窗口
            // 确保在UI加载完成后再设置活动窗口
            this.Loaded += (s, e) => {
                SetActiveDisplayWindow(HSmart1);
            };
        }

        // 设置活动窗口并更新视觉效果的辅助方法
        private void SetActiveDisplayWindow(HSmartWindowControlWPF newActiveWindow)
        {
            if (newActiveWindow == null) return;

            // 更新活动窗口的引用
            _activeDisplayWindow = newActiveWindow;

            // 重置所有窗口的边框样式
            Border1.BorderBrush = Brushes.Gray;
            Border1.BorderThickness = new Thickness(1);
            Border2.BorderBrush = Brushes.Gray;
            Border2.BorderThickness = new Thickness(1);
            Border3.BorderBrush = Brushes.Gray;
            Border3.BorderThickness = new Thickness(1);
            Border4.BorderBrush = Brushes.Gray;
            Border4.BorderThickness = new Thickness(1);

            // 高亮新激活的窗口
            if (_activeDisplayWindow == HSmart1) { Border1.BorderBrush = Brushes.DodgerBlue; Border1.BorderThickness = new Thickness(2); }
            else if (_activeDisplayWindow == HSmart2) { Border2.BorderBrush = Brushes.DodgerBlue; Border2.BorderThickness = new Thickness(2); }
            else if (_activeDisplayWindow == HSmart3) { Border3.BorderBrush = Brushes.DodgerBlue; Border3.BorderThickness = new Thickness(2); }
            else if (_activeDisplayWindow == HSmart4) { Border4.BorderBrush = Brushes.DodgerBlue; Border4.BorderThickness = new Thickness(2); }
        }


        // 所有显示窗口共用的点击事件处理器
        private void DisplayWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HSmartWindowControlWPF clickedWindow)
            {
                SetActiveDisplayWindow(clickedWindow);
            }
        }

        #region 状态栏管理

        /// <summary>
        /// 更新主窗口状态栏的文本，并在一段时间后自动清除
        /// </summary>
        /// <param name="message">要显示的消息</param>
        /// <param name="isError">消息是否为错误类型（将以红色显示）</param>
        public async void UpdateStatus(string message, bool isError)
        {
            // 确保在UI线程上更新
            await Dispatcher.InvokeAsync(async () =>
            {
                StatusTextBlock.Text = message;
                StatusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Black;

                // 等待7秒
                await Task.Delay(7000);

                // 如果5秒后状态栏文本还是这个消息，就把它清除
                if (StatusTextBlock.Text == message)
                {
                    StatusTextBlock.Text = "准备就绪";
                    StatusTextBlock.Foreground = Brushes.Black;
                }
            });
        }

        #endregion

        #region 相机管理

        // 相机管理按钮的点击事件
        private void CameraManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraManagementWindow != null)
            {
                cameraManagementWindow.Activate();
            }
            else
            {
                cameraManagementWindow = new CameraManagementWindow(this);
                cameraManagementWindow.Show();
            }
        }

        // 供子窗口调用的方法，用于通知主窗口它已关闭
        public void NotifyCameraManagementWindowClosed()
        {
            cameraManagementWindow = null;
        }

        // 根据当前活动窗口获取对应的相机对象
        private ICameraDevice GetCameraForActiveWindow()
        {
            if (_activeDisplayWindow == null)
            {
                UpdateStatus("操作失败：没有选中的活动窗口。", true);
                return null;
            }

            foreach (var camera in openCameras.Values)
            {
                if (camera.DisplayWindow == _activeDisplayWindow)
                {
                    return camera;
                }
            }
            // 如果遍历完都找不到，说明这个窗口确实没有连接相机
            UpdateStatus("提示：当前活动窗口没有连接相机。", true);
            return null;
        }

        // 工具条按钮事件
        private void SingleCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var camera = GetCameraForActiveWindow();
                if (camera == null) return;

                if (camera.IsContinuousGrabbing())
                {
                    // 修改：使用状态栏提示
                    UpdateStatus("操作冲突：相机正在连续采集中，请先停止。", true);
                    return;
                }
                camera.GrabAndDisplay();
            }
            catch (Exception ex)
            {
                UpdateStatus($"单次采集失败: {ex.Message}", true);
            }
        }

        private void ContinueCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var camera = GetCameraForActiveWindow();
                camera?.StartContinuousGrab();
            }
            catch (Exception ex)
            {
                UpdateStatus($"开始连续采集失败: {ex.Message}", true);
            }
        }

        private void StopCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var camera = GetCameraForActiveWindow();
            camera?.StopContinuousGrab();
        }

        // 将查找逻辑提取到一个可重用的公共方法中
        public List<DeviceInfo> GetFoundDevices()
        {
            var foundDevices = new List<DeviceInfo>();
            var foundSerialNumbers = new HashSet<string>();

            // --- 使用海康SDK查找 ---
            var hikDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref hikDeviceList);
            if (nRet == 0 && hikDeviceList.nDeviceNum > 0)
            {
                for (int i = 0; i < hikDeviceList.nDeviceNum; i++)
                {
                    var stDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(hikDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                    string serialNumber = GetHikSerialNumber(stDevInfo);
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        foundDevices.Add(new DeviceInfo
                        {
                            DisplayName = $"[HIK] {GetHikDisplayName(stDevInfo)} ({serialNumber})",
                            UniqueID = serialNumber,
                            SdkType = CameraSdkType.Hikvision
                        });
                        foundSerialNumbers.Add(serialNumber);
                    }
                }
            }

            // --- 使用Halcon的MVision接口查找设备 ---
            try
            {
                HOperatorSet.InfoFramegrabber("MVision", "device", out _, out HTuple halconDeviceList);
                if (halconDeviceList != null && halconDeviceList.Length > 0)
                {
                    foreach (string deviceId in halconDeviceList.SArr)
                    {
                        string serialNumber = deviceId.Split(' ').LastOrDefault()?.Trim('\'');
                        if (!string.IsNullOrEmpty(serialNumber) && !foundSerialNumbers.Contains(serialNumber))
                        {
                            foundDevices.Add(new DeviceInfo
                            {
                                DisplayName = $"[HAL] {deviceId}",
                                UniqueID = deviceId,
                                SdkType = CameraSdkType.HalconMVision
                            });
                        }
                    }
                }
            }
            catch (HalconException) { /* 忽略Halcon查找时可能发生的异常 */ }
            return foundDevices;
        }
        // 将打开逻辑提取为公共方法
        public (bool Success, string Message) OpenDevice(DeviceInfo selectedDevice)
        {
            if (selectedDevice == null)
            {
                return (false, "未选择任何有效设备。");
            }
            if (openCameras.ContainsKey(selectedDevice.UniqueID))
            {
                return (false, $"设备 {selectedDevice.UniqueID} 已经打开了。");
            }
            if (openCameras.Count >= 4)
            {
                return (false, "最多只能打开4个设备。");
            }
            HSmartWindowControlWPF targetWindow = null;

            // 新的窗口选择逻辑
            // 优先级1: 尝试使用当前激活的窗口，前提是它没被占用
            if (_activeDisplayWindow != null && !(_activeDisplayWindow.Tag is string))
            {
                targetWindow = _activeDisplayWindow;
            }
            // 优先级2: 如果活动窗口已被占用，则查找第一个可用的窗口
            else
            {
                targetWindow = displayWindows.FirstOrDefault(w => w.Tag == null || w.Tag is HObject);
            }

            if (targetWindow == null) return (false, "没有可用的显示窗口了。");

            if (targetWindow.Tag is HObject oldImage)
            {
                oldImage.Dispose();
                targetWindow.HalconWindow.ClearWindow();
            }

            ICameraDevice newCamera = (selectedDevice.SdkType == CameraSdkType.Hikvision)
                ? (ICameraDevice)new HikvisionCameraDevice(selectedDevice, targetWindow)
                : new HalconCameraDevice(selectedDevice.UniqueID, targetWindow);

            try
            {
                if (newCamera.Open())
                {
                    int windowIndex = displayWindows.IndexOf(targetWindow);
                    openCameras.Add(selectedDevice.UniqueID, newCamera);
                    targetWindow.Tag = selectedDevice.UniqueID;
                    CameraListChanged?.Invoke(this, EventArgs.Empty);
                    return (true, $"设备 {selectedDevice.DisplayName} 打开成功，已绑定到窗口 {windowIndex + 1}。");
                }
                // newCamera.Open() 返回 false 的情况（虽然现在不太可能，因为我们改为抛出异常）
                return (false, $"打开设备 {selectedDevice.DisplayName} 失败。");
            }
            catch (Exception ex)
            {
                // 捕获从 Open() 抛出的异常，并将其作为返回消息
                return (false, ex.Message);
            }
        }
        // 将关闭逻辑提取为公共方法
        public (bool Success, string Message) CloseDevice(DeviceInfo selectedDevice)
        {
            if (selectedDevice == null)
            {
                return (false, "请选择一个要关闭的设备。");
            }
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice cameraToClose))
            {
                cameraToClose.DisplayWindow.Tag = null;
                cameraToClose.Close();
                openCameras.Remove(selectedDevice.UniqueID);
                CameraListChanged?.Invoke(this, EventArgs.Empty);
                return (true, $"设备 {selectedDevice.DisplayName} 已成功关闭。");
            }
            return (false, $"设备 {selectedDevice.DisplayName} 并未打开，无需关闭。");

        }

        // 从海康设备信息结构体中提取序列号
        private string GetHikSerialNumber(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                return ((MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO))).chSerialNumber;
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
                return ((MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO))).chSerialNumber;
            return null;
        }

        // 从海康设备信息结构体中提取用户自定义名称或型号名称
        private string GetHikDisplayName(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                var info = ((MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO)));
                return string.IsNullOrEmpty(info.chUserDefinedName) ? info.chModelName : info.chUserDefinedName;
            }
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                var info = ((MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO)));
                return string.IsNullOrEmpty(info.chUserDefinedName) ? info.chModelName : info.chUserDefinedName;
            }
            return "Unknown Device";
        }

        #endregion


        #region UI事件处理

        /// <summary>
        /// “加载图像”按钮的点击事件处理程序
        /// </summary>
        private void LoadImgButtonClick(object sender, RoutedEventArgs e)
        {
            // --- 在所有操作之前，先检查目标窗口的状态 ---
            HSmartWindowControlWPF targetWindow = _activeDisplayWindow;

            // 检查是否有活动窗口
            if (targetWindow == null)
            {
                UpdateStatus("请先点击选择一个显示窗口。", true);
                return;
            }

            // 检查活动窗口是否已经被相机占用
            if (targetWindow.Tag is string deviceId)
            {
                // 如果Tag是字符串，说明它被一个相机锁定
                UpdateStatus($"此窗口已被相机 '{deviceId}' 占用，无法加载图像。", true);
                return; // 中断操作
            }

            // --- 如果检查通过，才继续执行后续的文件选择和加载逻辑 ---

            // 创建文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                // 设置文件过滤器，显示所有支持格式的图片
                Filter = "所有支持的图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;" +
                         "|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg;" +
                         "|PNG 图片 (*.png)|*.png;" +
                         "|位图图片 (*.bmp)|*.bmp;" +
                         "|GIF 图片 (*.gif)|*.gif;" +
                         "|所有文件 (*.*)|*.*",

                Title = "选择图片文件",
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            // 显示文件对话框，并检查用户是否点击了“打开”
            if (openFileDialog.ShowDialog() == true)
            {
                HObject loadedImage = null; // 在 try-catch 外部声明，以便 finally 中可以访问
                try
                {
                    // 如果窗口之前有本地图像，先释放它
                    if (targetWindow.Tag is HObject oldImage)
                    {
                        oldImage.Dispose();
                    }

                    // 加载新图像
                    HOperatorSet.ReadImage(out loadedImage, openFileDialog.FileName);

                    // 显示新图像并更新Tag
                    HWindow hWindow = targetWindow.HalconWindow;
                    HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                    hWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                    hWindow.ClearWindow();
                    hWindow.DispObj(loadedImage);

                    // 将新加载的 HObject 实例赋给 Tag
                    targetWindow.Tag = loadedImage;
                    loadedImage = null;
                }
                
                catch (Exception ex)
                {
                    UpdateStatus($"加载图像时发生错误: {ex.Message}", true);
                }
                finally
                {
                    // 如果在操作过程中发生异常，loadedImage 可能还未被赋给 Tag，
                    // 这种情况下需要确保它被释放，以防内存泄漏。
                    loadedImage?.Dispose();
                }
            }

        }

        /// <summary>
        /// "ROI工具"按钮的点击事件处理程序
        /// </summary>
        private void ROIToolButtonClick(object sender, RoutedEventArgs e)
        {
            // 检查ROI窗口是否已经打开
            if (roiEditorWindow != null)
            {
                // 如果已打开，则激活它并置于顶层，而不是创建新窗口
                roiEditorWindow.Activate();
                return;
            }

            // 检查活动窗口
            if (_activeDisplayWindow == null)
            {
                // 如果没有图像，主窗口直接弹出提示，然后中断操作
                UpdateStatus("请先点击选择一个显示窗口以使用ROI工具。", true);
                return; // 不再创建ROIToolWindow
            }

            HObject currentImage = null;

            // 首先尝试从相机获取图像
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == _activeDisplayWindow);
            if (camera != null)
            {
                currentImage = camera.GetCurrentImage();
            }
            // 如果没有相机，再尝试从Tag获取本地加载的图像
            else if (_activeDisplayWindow.Tag is HObject localImage)
            {
                currentImage = localImage;
            }

            if (currentImage == null || !currentImage.IsInitialized() || currentImage.CountObj() < 1)
            {
                UpdateStatus("当前活动窗口中没有可用的图像，无法打开ROI工具。", true);
                return;
            }

            // 为ROIToolWindow创建一个图像的稳定副本
            HObject imageCopyForRoi = null;
            try
            {
                // 使用 CopyImage 创建一个内存独立的深拷贝
                HOperatorSet.CopyImage(currentImage, out imageCopyForRoi);

                roiEditorWindow = new ROIToolWindow(_activeDisplayWindow, this);

                // --- 订阅新的 RoiUpdated 事件 ---
                roiEditorWindow.RoiUpdated += OnRoiParametersUpdated;

                roiEditorWindow.ROIAccepted += (HObject returnedRoi) =>
                {
                    roiEditorWindow?.Close();
                    this.Activate();
                    returnedRoi.Dispose();
                };

                roiEditorWindow.Closed += (s, args) =>
                {
                    // 在窗口关闭时，确保取消订阅以防内存泄漏
                    if (roiEditorWindow != null)
                    {
                        roiEditorWindow.RoiUpdated -= OnRoiParametersUpdated;
                    }
                    roiEditorWindow = null;
                };

                // 将这个稳定的副本传递给子窗口
                roiEditorWindow.Show();
                roiEditorWindow.UpdateImage(imageCopyForRoi);
            }
            finally
            {
                // 确保为ROI窗口创建的副本在传递完成后被释放，
                // 因为ROIToolWindow的UpdateImage内部会再次复制一份自己管理。
                imageCopyForRoi?.Dispose();
            }
        }

        // --- 事件处理方法，用于更新主窗口UI ---
        private void OnRoiParametersUpdated(object sender, RoiUpdatedEventArgs e)
        {
            // 直接使用事件参数中已经格式化好的字符串来更新TextBlock
            RoiParametersTextBlock.Text = e.ParametersAsString;
        }

        /// <summary>
        /// 封装的图像显示方法
        /// </summary>
        /// <param name="imageToShow">需要显示的Halcon图像对象</param>
        private void DisplayImage(HObject imageToShow)
        {
            if (imageToShow == null || !imageToShow.IsInitialized())
                return;

            // 获取控件内的Halcon窗口
            HWindow window = HSmart1.HalconWindow;
            // 获取图像的宽度和高度
            HOperatorSet.GetImageSize(imageToShow, out HTuple width, out HTuple height);
            // 设置窗口的显示部分，以确保图像完整且居中显示
            window.SetPart(0, 0, height.I - 1, width.I - 1);
            // 清除窗口之前的内容
            window.ClearWindow();
            // 将图像对象显示在窗口上
            window.DispObj(imageToShow);
        }



        /// <summary>
        /// 窗口关闭事件，用于释放资源，确保主窗口关闭时，所有参数窗口也被关闭
        /// </summary>

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 关闭相机管理窗口（如果它还开着）
            cameraManagementWindow?.Close();
           
           
            // 如果ROI窗口还开着，也关闭它
            roiEditorWindow?.Close();
            // 再关闭所有相机
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
        }
        #endregion


        
    }
}