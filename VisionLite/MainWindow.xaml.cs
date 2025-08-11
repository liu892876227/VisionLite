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
        private Dictionary<string, ICameraDevice> openCameras = new Dictionary<string, ICameraDevice>();

        /// <summary>
        /// 管理所有用于显示图像的WPF控件。
        /// 使用列表可以方便地按顺序查找空闲窗口。
        /// </summary>
        private List<HSmartWindowControlWPF> displayWindows;

        /// <summary>
        /// 管理所有已打开的、非模态的参数窗口。
        /// 这是为了防止重复打开同一个相机的参数窗口，并确保在关闭相机时能同步关闭其参数窗口。
        /// 键(Key): 设备的唯一ID字符串。
        /// 值(Value): 该设备对应的参数窗口实例。
        /// </summary>
        private Dictionary<string, Window> openParameterWindows = new Dictionary<string, Window>();

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

            // 在窗口启动时，自动调用查找设备方法，不显示成功提示
            FindAndPopulateDevices();
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


        #region 设备查找与管理

        /// <summary>
        /// 查找并填充设备列表。采用混合驱动模式，先查找海康SDK支持的设备，再查找Halcon支持的设备，并进行去重
        /// </summary>
        /// /// <param name="showSuccessMessage">如果为true，则在查找结束后弹窗提示结果。</param>
        private void FindAndPopulateDevices(bool showSuccessMessage = false)
        {
            comboBox.Items.Clear();
            var foundDevices = new List<DeviceInfo>();

            // 使用HashSet来存储已发现的设备序列号，以实现高效去重
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
                        // 为每个找到的设备创建一个DeviceInfo对象，并标记其SDK类型为Hikvision
                        foundDevices.Add(new DeviceInfo
                        {
                            DisplayName = $"[HIK] {GetHikDisplayName(stDevInfo)} ({serialNumber})",
                            UniqueID = serialNumber,
                            SdkType = CameraSdkType.Hikvision
                        });
                        // 将序列号添加到HashSet中，用于后续去重
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

                        // 尝试从Halcon返回的设备ID字符串中解析出序列号
                        string serialNumber = deviceId.Split(' ').LastOrDefault()?.Trim('\'');

                        // 如果解析成功，并且这个序列号之前没有被海康SDK发现过
                        if (!string.IsNullOrEmpty(serialNumber) && !foundSerialNumbers.Contains(serialNumber))
                        {
                            // 才将这个设备作为一个新的、由Halcon驱动的设备添加进列表
                            foundDevices.Add(new DeviceInfo
                            {
                                DisplayName = $"[HAL] {deviceId}",
                                UniqueID = deviceId,                    // Halcon设备使用其完整的ID字符串作为唯一标识
                                SdkType = CameraSdkType.HalconMVision
                            });
                        }
                    }
                }
            }
            catch (HalconException) { /* 忽略Halcon查找时可能发生的异常，例如接口未安装等 */ }

            // --- 将最终的设备列表填充到UI的ComboBox中 ---
            if (foundDevices.Any())
            {
                foreach (var dev in foundDevices)
                {
                    comboBox.Items.Add(dev);            // ComboBox会自动调用DeviceInfo的ToString()方法来显示
                }
                comboBox.SelectedIndex = 0;
                OpenCamButton.IsEnabled = true;
                if (showSuccessMessage) MessageBox.Show($"查找成功！共发现 {foundDevices.Count} 个设备。", "完成");
            }
            else
            {
                OpenCamButton.IsEnabled = false;
                if (showSuccessMessage) MessageBox.Show("未发现任何设备！", "提示");
            }
            
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
        /// "查找设备"按钮的点击事件处理程序
        /// </summary>
        private void FindCamButtonClick(object sender, RoutedEventArgs e)
        {
           
            FindAndPopulateDevices(true);
        }

        /// <summary>
        /// "打开设备"按钮的点击事件处理程序
        /// </summary>
        private void OpenCamButtonClick(object sender, RoutedEventArgs e)
        {

            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice))
            {
                MessageBox.Show("请先选择一个有效的设备。", "提示");
                return;
            }

            if (openCameras.ContainsKey(selectedDevice.UniqueID))
            {
                MessageBox.Show($"设备 {selectedDevice.UniqueID} 已经打开了。", "提示");
                return;
            }

            // 检查是否已达到最大连接数
            if (openCameras.Count >= 4)
            {
                MessageBox.Show("最多只能打开4个设备。", "提示");
                return;
            }

            HSmartWindowControlWPF targetWindow = null;
            // 优先级 1: 寻找一个完全空闲的窗口 (Tag is null)
            targetWindow = displayWindows.FirstOrDefault(w => w.Tag == null);

            // 优先级 2: 如果没有完全空闲的，则寻找一个被本地图像占用的窗口 (Tag is HObject)，这样的窗口可以被覆盖
            if (targetWindow == null)
            {
                targetWindow = displayWindows.FirstOrDefault(w => w.Tag is HObject);
            }

            // 如果到这里 targetWindow 仍然是 null，说明所有窗口都已经被其他相机占用
            if (targetWindow == null)
            {
                MessageBox.Show("没有可用的显示窗口了。所有窗口都已连接相机。", "提示");
                return;
            }

            // --- 在绑定新相机前，清理即将被占用的窗口 ---
            // 如果这个窗口之前是被本地图像占用的，需要释放那个图像资源
            if (targetWindow.Tag is HObject oldImage)
            {
                oldImage.Dispose();
                targetWindow.Tag = null; // 清空Tag
                targetWindow.HalconWindow.ClearWindow(); // 清空显示
            }

            // --- 根据设备类型创建不同的相机实例 ---
            ICameraDevice newCamera = null;
            if (selectedDevice.SdkType == CameraSdkType.Hikvision)
            {
                newCamera = new HikvisionCameraDevice(selectedDevice, targetWindow);
            }
            else // HalconMVision
            {
                newCamera = new HalconCameraDevice(selectedDevice.UniqueID, targetWindow);
            }

            if (newCamera.Open())
            {

                int windowIndex = displayWindows.IndexOf(targetWindow);
                // 将新打开的相机添加到管理字典中
                openCameras.Add(selectedDevice.UniqueID, newCamera);
                // 使用 string 类型的 deviceId 来“锁定”这个窗口，表示这是一个不可被其他相机覆盖的硬绑定
                targetWindow.Tag = selectedDevice.UniqueID;

                string successMessage = $"设备 {selectedDevice.DisplayName} 打开成功，并绑定到窗口 {windowIndex + 1}。";
                MessageBox.Show(successMessage, "成功");
                // 打开后自动采集一帧图像，提供即时反馈
                newCamera.GrabAndDisplay(); 
            }

        }

        /// <summary>
        /// "关闭设备"按钮的点击事件处理程序
        /// </summary>
        private void CloseCamButtonClick(object sender, RoutedEventArgs e)
        {
            // 检查下拉框中是否有选中项
            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice))
            {
                MessageBox.Show("请在列表中选择一个要关闭的设备。", "提示");
                return;
            }
            // 尝试从已打开的相机字典中获取设备
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice cameraToClose))
            {
                // 如果该相机有打开的参数窗口，先将其关闭
                if (openParameterWindows.TryGetValue(cameraToClose.DeviceID, out Window paramWindow))
                {
                    // 这会自动触发之前订阅的 Closed 事件，将其从字典中移除
                    paramWindow.Close();
                }

                // 释放窗口占用
                cameraToClose.DisplayWindow.Tag = null;
                // 调用相机的关闭方法
                cameraToClose.Close();
                // 从管理字典中移除相机
                openCameras.Remove(selectedDevice.UniqueID);
                MessageBox.Show($"设备 {selectedDevice.DisplayName} 已关闭。", "成功");
            }
            else
            {
                MessageBox.Show($"设备 {selectedDevice.DisplayName} 并未打开。", "提示");
            }
        }

        /// <summary>
        /// "单次触发图像采集"按钮的点击事件处理程序
        /// </summary>
        private void SingleCaptureButtonClick(object sender, RoutedEventArgs e)
        {

            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice)) return;
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice camera))
            {
                // 进行状态检查，防止在连续采集中进行单次触发
                if (camera.IsContinuousGrabbing())
                {
                    MessageBox.Show("设备正在连续采集中，请先停止。", "操作冲突");
                    return;
                }
                camera.GrabAndDisplay();
            }
            else { MessageBox.Show("设备未打开，无法采集。", "提示"); }
        }

        /// <summary>
        /// "连续触发图像采集"按钮的点击事件处理程序
        /// </summary>
        private void ContinueCaptureButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice)) return;
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice camera))
            {
                camera.StartContinuousGrab();
            }
            else { MessageBox.Show("设备未打开，无法开始连续采集。", "提示"); }
        }

        /// <summary>
        /// "停止连续图像采集"按钮的点击事件处理程序
        /// </summary>
        private void StopContinueCaptureButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice)) return;
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice camera))
            {
                camera.StopContinuousGrab();
            }
            else { MessageBox.Show("设备未打开，无法停止。", "提示"); }
        }

        /// <summary>
        /// "查看设备参数"按钮的点击事件处理程序
        /// </summary>
        private void ViewCamParaButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(comboBox.SelectedItem is DeviceInfo selectedDevice))
            {
                MessageBox.Show("请选择一个要查看参数的设备。", "提示");
                return;
            }
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice camera))
            {
                // 检查参数窗口是否已存在
                if (openParameterWindows.TryGetValue(camera.DeviceID, out Window existingWindow))
                {
                    // 如果存在，则激活它，而不是创建新的
                    existingWindow.Activate();
                    return;
                }


                // 通过接口调用，获取设备专属的参数窗口实例
                Window paramWindow = camera.ShowParametersWindow(this);

                // 根据相机类型决定是模态还是非模态显示
                if (camera is HalconCameraDevice)
                {
                    // Halcon设备强制使用模态对话框，因为设置关键参数需要暂停程序流程
                    paramWindow.ShowDialog();
                }
                else // Hikvision设备使用非模态
                {
                    // 订阅窗口的Closed事件，以便在它关闭时，我们能将它从管理字典中移除
                    paramWindow.Closed += (s, args) => {
                        openParameterWindows.Remove(camera.DeviceID);
                    };
                    // 将新窗口添加到管理字典中
                    openParameterWindows.Add(camera.DeviceID, paramWindow);
                    // 以非模态方式显示窗口
                    paramWindow.Show();
                }

            }
            else
            {
                MessageBox.Show("设备未打开，无法查看参数。", "提示");
            }
        }

        /// <summary>
        /// “加载图像”按钮的点击事件处理程序
        /// </summary>
        private void LoadImgButtonClick(object sender, RoutedEventArgs e)
        {
            

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
                    string filePath = openFileDialog.FileName;

                    // 目标窗口固定为当前活动窗口
                    HSmartWindowControlWPF targetWindow = _activeDisplayWindow;

                    if (targetWindow != null)
                    {
                        // 在显示前，先处理旧的 Tag
                        // 如果目标窗口之前已经有本地图像，先释放它
                        if (targetWindow.Tag is HObject oldImage)
                        {
                            oldImage.Dispose();
                        }
                        // 如果之前是相机，则发出警告并停止采集，但 Tag 的最终决定权交给后续逻辑
                        else if (targetWindow.Tag is string deviceId && openCameras.ContainsKey(deviceId))
                        {
                            var camera = openCameras[deviceId];
                            if (camera.IsContinuousGrabbing())
                            {
                                camera.StopContinuousGrab();
                                MessageBox.Show($"窗口被相机 {deviceId} 占用，已停止其连续采集以加载本地图像。", "提示");
                            }
                        }

                        // 加载新图像
                        HOperatorSet.ReadImage(out loadedImage, filePath);

                        // 显示新图像并更新Tag
                        HWindow hWindow = targetWindow.HalconWindow;
                        HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                        hWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                        hWindow.ClearWindow();
                        hWindow.DispObj(loadedImage);

                        // 将新加载的 HObject 实例赋给 Tag，表示此窗口当前显示的是一个可被覆盖的本地图像
                        targetWindow.Tag = loadedImage;

                        // 将 loadedImage 的所有权转移给了 Tag，所以不能在这里 Dispose 它
                        // 并且，需要将 loadedImage 设为 null，防止 finally 块中错误地释放它
                        loadedImage = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图像时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(this, "没有活动的图像窗口被选中。", "提示");
                return; // 不再创建ROIToolWindow
            }

            if (!(_activeDisplayWindow.Tag is HObject currentImage && currentImage.IsInitialized()))
            {
                // 获取活动窗口的索引用于提示信息
                int windowIndex = displayWindows.IndexOf(_activeDisplayWindow) + 1;
                MessageBox.Show(this, $"窗口{windowIndex}中没有可用的图像，无法打开ROI工具。", "提示");
                return;
            }

            // 为ROIToolWindow创建一个图像的稳定副本
            HObject imageCopyForRoi = null;
            try
            {
                // 使用 CopyImage 创建一个内存独立的深拷贝
                HOperatorSet.CopyImage(currentImage, out imageCopyForRoi);

                roiEditorWindow = new ROIToolWindow(_activeDisplayWindow);
                
                roiEditorWindow.ROIAccepted += (HObject returnedRoi) =>
                {
                    roiEditorWindow?.Close();
                    this.Activate();
                    returnedRoi.Dispose();
                };

                roiEditorWindow.Closed += (s, args) =>
                {
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
            // 先关闭所有还开着的参数窗口
            foreach (var window in openParameterWindows.Values.ToList()) 
            {
                window.Close();
            }
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