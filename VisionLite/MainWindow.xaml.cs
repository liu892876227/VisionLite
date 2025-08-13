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
using System.ComponentModel;



namespace VisionLite
{
    /// <summary>
    /// 应用程序的主窗口，是所有UI交互和业务逻辑的协调中心。
    /// 负责管理：
    /// - 多个图像显示窗口的布局和激活状态。
    /// - 所有已连接的相机设备实例。
    /// - 与子窗口（如相机管理、ROI工具）的通信。
    /// - 用户通过顶部工具栏发起的各种操作。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 核心字段和属性

        /// <summary>
        /// 管理所有已打开的相机设备。
        /// Key: 设备的唯一ID (string)。
        /// Value: 实现ICameraDevice接口的相机对象实例。
        /// 'internal'修饰符允许同程序集中的其他类（如CameraManagementWindow）访问它。
        /// </summary>
        internal Dictionary<string, ICameraDevice> openCameras = new Dictionary<string, ICameraDevice>();

        /// <summary>
        /// 跟踪非模态的相机管理窗口实例，以实现单例模式（只打开一个）。
        /// </summary>
        private CameraManagementWindow cameraManagementWindow = null;

        /// <summary>
        /// 当相机列表（打开/关闭相机）发生变化时触发的事件。
        /// 用于通知其他窗口（如CameraManagementWindow）同步其UI状态。
        /// </summary>
        public event EventHandler CameraListChanged;

        /// <summary>
        /// 管理所有用于显示图像的WPF控件的列表。
        /// </summary>
        private List<HSmartWindowControlWPF> displayWindows;


        /// <summary>
        /// 存储当前被用户点击选中的活动显示窗口。
        /// 所有针对窗口的操作（如采集、加载图像）都将作用于此窗口。
        /// </summary>
        private HSmartWindowControlWPF _activeDisplayWindow;

        /// <summary>
        /// 缓存由ROIToolWindow发送的最新一次ROI更新事件的参数。
        /// 用于在主窗口视图变化时，能够重新计算并更新Adorner的位置。
        /// </summary>
        private RoiUpdatedEventArgs _lastRoiArgs;

        /// <summary>
        /// 持有当前在活动窗口上显示的ROI参数浮动框 (Adorner) 的实例。
        /// </summary>
        private RoiAdorner _roiAdorner;

        #endregion

        #region ROI 和涂抹绘制相关字段

        /// <summary>
        /// 当前在活动窗口上显示的交互式ROI对象。
        /// </summary>
        private HDrawingObject _drawingObject;

        /// <summary>
        /// 记录当前ROI所在的显示窗口，以便在切换活动窗口时进行管理。
        /// </summary>
        private HSmartWindowControlWPF _drawingObjectHost;

        /// <summary>
        /// 用于存储涂抹模式下绘制的ROI区域。
        /// </summary>
        private HObject _paintedRoi;

        /// <summary>
        /// 标记当前是否处于涂抹绘制模式。
        /// </summary>
        private bool _isPaintingMode = false;

        /// <summary>
        /// 标记在涂抹模式下，鼠标左键是否按下以激活绘制。
        /// </summary>
        private bool _isPaintingActive = false;

        // --- 用于涂抹绘制的辅助变量 ---
        private HTuple _lastPaintRow = -1;
        private HTuple _lastPaintCol = -1;
        private readonly string _brushShape = "圆形";
        private readonly int _brushRadius = 20;
        private readonly int _brushRectWidth = 40;
        private readonly int _brushRectHeight = 20;

        #endregion

        //构造函数
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
            // 在窗口加载完成后，默认激活第一个显示窗口
            this.Loaded += (s, e) => {
                SetActiveDisplayWindow(HSmart1);
            };
        }

        #region 窗口激活与视图管理
        /// <summary>
        /// 设置指定的显示窗口为活动窗口，并更新其UI（高亮边框）。
        /// </summary>
        /// <param name="newActiveWindow">要被激活的显示窗口。</param>
        private void SetActiveDisplayWindow(HSmartWindowControlWPF newActiveWindow)
        {
            if (newActiveWindow == null) return;

            // 更新活动窗口的引用
            _activeDisplayWindow = newActiveWindow;

            // 统一视觉反馈：先重置所有窗口的边框样式为默认灰色
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

        /// <summary>
        /// 所有显示窗口共用的鼠标点击事件处理器。
        /// </summary>
        private void DisplayWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HSmartWindowControlWPF clickedWindow)
            {
                SetActiveDisplayWindow(clickedWindow);
            }
        }

        /// <summary>
        /// 处理显示窗口视图变化（由拖动或缩放图像引起）的事件。
        /// </summary>
        private void OnDisplayWindowViewChanged(object sender, EventArgs e)
        {
            // 确保事件来自于当前活动的窗口，并且ROI工具正处于打开状态
            if (sender == _activeDisplayWindow && _roiAdorner != null && _lastRoiArgs != null)
            {
                // 使用缓存的最后一次ROI参数，重新计算并更新Adorner的位置
                UpdateAdornerPosition(_lastRoiArgs);
            }
        }
        #endregion

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

        /// <summary>
        /// "相机管理"工具栏按钮的点击事件处理程序。
        /// 实现相机管理窗口的单例模式。
        /// </summary>
        private void CameraManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraManagementWindow != null)
            {
                cameraManagementWindow.Activate();// 如果已存在，则激活并置于顶层
            }
            else
            {
                cameraManagementWindow = new CameraManagementWindow(this);// 否则创建新实例
                cameraManagementWindow.Show();
            }
        }

        /// <summary>
        /// 供子窗口（CameraManagementWindow）调用的方法，
        /// 用于在子窗口关闭时通知主窗口，以便主窗口可以重置实例跟踪字段。
        /// </summary>
        public void NotifyCameraManagementWindowClosed()
        {
            cameraManagementWindow = null;
        }

        /// <summary>
        /// 根据当前活动窗口获取其对应的相机对象实例。
        /// </summary>
        /// <returns>如果找到则返回ICameraDevice实例，否则返回null并更新状态栏。</returns>
        private ICameraDevice GetCameraForActiveWindow()
        {
            if (_activeDisplayWindow == null)
            {
                UpdateStatus("操作失败：没有选中的活动窗口。", true);
                return null;
            }
            // 通过遍历已打开的相机列表，查找哪个相机的DisplayWindow与当前活动窗口匹配
            // 这是比使用Tag属性更可靠的方式
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

        /// <summary>
        /// 枚举所有连接到PC的、受支持的相机设备（海康和Halcon MVision）。
        /// </summary>
        /// <returns>一个包含所有被发现设备信息的列表。</returns>
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

        /// <summary>
        /// 打开指定的相机设备，并将其绑定到一个可用的显示窗口。
        /// </summary>
        /// <param name="selectedDevice">要打开的设备信息。</param>
        /// <returns>一个元组，包含操作是否成功(bool)和相应的提示信息(string)。</returns>
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
        /// <summary>
        /// 关闭指定的相机设备并释放相关资源。
        /// </summary>
        public (bool Success, string Message) CloseDevice(DeviceInfo selectedDevice)
        {
            if (selectedDevice == null)
            {
                return (false, "请选择一个要关闭的设备。");
            }
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice cameraToClose))
            {
                // 在关闭相机前，检查是否需要清除该相机窗口的ROI
                if (cameraToClose.DisplayWindow == _drawingObjectHost || (cameraToClose.DisplayWindow == _activeDisplayWindow && _isPaintingMode))
                {
                    ClearActiveRoi();
                }

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

        #region 工具栏按钮事件处理
        /// <summary>
        /// “单次采集”工具栏按钮的点击事件。
        /// </summary>
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
        /// <summary>
        /// “连续采集”工具栏按钮的点击事件。
        /// </summary>
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
        /// <summary>
        /// “停止采集”工具栏按钮的点击事件。
        /// </summary>
        private void StopCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var camera = GetCameraForActiveWindow();
            camera?.StopContinuousGrab();
        }

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

            // 在加载新图像前，清除当前窗口的任何ROI
            if (_activeDisplayWindow == _drawingObjectHost || _isPaintingMode)
            {
                ClearActiveRoi();
            }

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

                    HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                    // 通过设置WPF属性来设置视图
                    targetWindow.HImagePart = new Rect(0, 0, width.I, height.I);

                    // 清除窗口并显示
                    targetWindow.HalconWindow.ClearWindow();
                    targetWindow.HalconWindow.DispObj(loadedImage);

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


        #endregion


        #region ROI Toolbar Handlers

        /// <summary>
        /// ROI形状下拉框选择变化时的事件处理。
        /// </summary>
        private void RoiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;

            // 你的ComboBox有两个标题项（索引0和1），所以只有当索引大于1时才响应
            if (comboBox == null || comboBox.SelectedIndex < 2 || !this.IsLoaded)
            {
                return;
            }

            // 获取选择的ROI类型
            var selectedItem = (comboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            try
            {
                // 检查是否有活动窗口
                if (_activeDisplayWindow == null)
                {
                    UpdateStatus("请先点击选择一个显示窗口以使用ROI工具。", true);
                    return;
                }

                // 检查活动窗口是否有图像
                using (HObject currentImage = GetImageFromActiveWindow())
                {
                    if (currentImage == null || !currentImage.IsInitialized() || currentImage.CountObj() < 1)
                    {
                        UpdateStatus("当前活动窗口中没有可用的图像，无法创建ROI。", true);
                        return;
                    }
                } // using语句确保currentImage被释放

                // 在创建新ROI之前，先清除当前活动窗口上任何已存在的ROI
                ClearActiveRoi();

                // 根据选择执行操作
                switch (selectedItem)
                {
                    case "清除ROI":
                        // ClearActiveRoi 已经在上面调用过了，这里不需要额外操作
                        break;
                    case "涂抹式ROI (Paint)":
                        EnablePaintMode();
                        break;
                    default:
                        // 创建标准的HDrawingObject ROI
                        CreateDrawingObject(selectedItem);
                        break;
                }
            }
            finally
            {
                // 操作完成后重置下拉框，使其变回一个按钮的样子
                comboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// "保存原图"按钮的点击事件处理。
        /// </summary>
        private void SaveOriginalImageButton_Click(object sender, RoutedEventArgs e)
        {
            HObject imageToSave = GetImageFromActiveWindow();
            if (imageToSave == null || !imageToSave.IsInitialized())
            {
                UpdateStatus("活动窗口中没有图像可供保存。", true);
                return;
            }

            try
            {
                // 保存路径
                string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "IMG");
                // 确保这个子文件夹存在，如果不存在则自动创建
                Directory.CreateDirectory(savePath);
                // 文件名
                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                // 组合成完整路径
                string fullPath = System.IO.Path.Combine(savePath, fileName);

                // 保存图像
                HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);

                UpdateStatus($"图像已成功保存到：{fullPath}", false);
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", true);
            }
            finally
            {
                imageToSave?.Dispose();
            }
        }

        /// <summary>
        /// "保存ROI区域图像"按钮的点击事件处理。
        /// </summary>
        private void SaveRoiImageButton_Click(object sender, RoutedEventArgs e)
        {
            HObject sourceImage = GetImageFromActiveWindow();
            if (sourceImage == null || !sourceImage.IsInitialized())
            {
                UpdateStatus("错误: 没有背景图像，无法保存ROI。", true);
                return;
            }

            HObject regionToSave = null;
            bool isTempRegion = false;

            // 重新排序判断逻辑：优先检查是否存在有效的 _paintedRoi。
            // 这与 _isPaintingMode 的状态无关，只关心是否有绘制结果存在。
            if (_paintedRoi != null && _paintedRoi.IsInitialized() && _paintedRoi.CountObj() > 0)
            {
                regionToSave = _paintedRoi;
                // _paintedRoi 是类成员，由ClearActiveRoi管理，所以这里不需要临时标志
                isTempRegion = false;
            }
            // 如果没有涂抹式ROI，再检查是否存在标准的 HDrawingObject ROI。
            else if (_drawingObject != null && _drawingObject.ID != -1)
            {
                try
                {
                    using (HObject iconicObject = _drawingObject.GetDrawingObjectIconic())
                    {
                        string roiType = _drawingObject.GetDrawingObjectParams("type").S;
                        if (roiType == "line")
                        {
                            UpdateStatus("错误: 无法保存直线ROI的图像。", true);
                            sourceImage.Dispose();
                            return;
                        }
                        if (roiType.Contains("xld"))
                        {
                            HOperatorSet.GenRegionContourXld(iconicObject, out regionToSave, "filled");
                        }
                        else
                        {
                            regionToSave = iconicObject.CopyObj(1, -1);
                        }
                    }
                    // 从 DrawingObject 获取的 Region 是一个临时的拷贝，需要在使用后释放。
                    isTempRegion = true;
                }
                catch (HalconException hex)
                {
                    UpdateStatus($"获取ROI区域时出错: {hex.GetErrorMessage()}", true);
                    sourceImage.Dispose();
                    return;
                }
            }
            // 如果以上两种情况都没有找到有效的ROI，则报错。
            if (regionToSave == null || !regionToSave.IsInitialized() || regionToSave.CountObj() == 0)
            {
                UpdateStatus("错误: 没有有效的ROI可以保存。", true);
                sourceImage.Dispose();
                return;
            }

            HObject imageReduced = null;
            HObject imageToSave = null;
            // 执行保存逻辑
            try
            {
                HOperatorSet.ReduceDomain(sourceImage, regionToSave, out imageReduced);
                HOperatorSet.CropDomain(imageReduced, out imageToSave);

                // ROI保存路径
                string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "ROI");

                Directory.CreateDirectory(savePath);
                string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                string fullPath = System.IO.Path.Combine(savePath, fileName);
                HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);
                UpdateStatus($"ROI图像已成功保存到：{fullPath}", false);
            
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", true);
            }
            finally
            {
                // 释放所有在此方法中创建或获取的对象
                sourceImage?.Dispose();
                imageReduced?.Dispose();
                imageToSave?.Dispose();
                if (isTempRegion)
                {
                    regionToSave?.Dispose();
                }
            }
        }

        #endregion

        #region Ported ROI Logic

        /// <summary>
        /// 根据选择的类型，在活动窗口上创建并附加一个交互式ROI。
        /// </summary>
        private void CreateDrawingObject(string selectedType)
        {
            HObject image = GetImageFromActiveWindow();
            if (image == null)
            {
                UpdateStatus("活动窗口中没有图像，无法创建ROI。", true);
                return;
            }

            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            image.Dispose();

            try
            {
                // 根据字符串选择确定ROI类型并直接创建 _drawingObject 实例
                switch (selectedType)
                {
                    case "矩形 (Rectangle)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "带角度矩形 (Rectangle2)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE2, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 4);
                        break;
                    case "圆形 (Circle)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, height.D / 2, width.D / 2, Math.Min(width.D, height.D) / 4);
                        break;
                    case "椭圆 (Ellipse)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.ELLIPSE, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 8);
                        break;
                    case "直线 (Line)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "自由轮廓 (Contour)":
                        HTuple initialRows = new HTuple(new double[] { height.D / 4, height.D / 4, height.D * 0.75, height.D * 0.75 });
                        HTuple initialCols = new HTuple(new double[] { width.D / 4, width.D * 0.75, width.D * 0.75, width.D / 4 });
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.XLD_CONTOUR, initialRows, initialCols);
                        break;
                    default:
                        // 如果类型不匹配，直接退出
                        return;
                }

                // 附加到活动窗口
                _drawingObjectHost = _activeDisplayWindow;
                _drawingObjectHost.HalconWindow.AttachDrawingObjectToWindow(_drawingObject);

                // 订阅交互事件
                _drawingObject.OnDrag(OnRoiUpdate);
                _drawingObject.OnResize(OnRoiUpdate);
                _drawingObject.OnSelect(OnRoiUpdate);

                // 先创建并显示 Adorner 实例
                ShowRoiAdorner();

                // 然后再调用更新方法，此时 _roiAdorner 已存在，可以被成功更新
                OnRoiUpdate(_drawingObject, _drawingObjectHost.HalconWindow, "create");
            }
            catch (Exception ex)
            {
                // 捕获并显示更详细的错误信息
                UpdateStatus($"创建ROI失败: {ex.Message}", true);
                // 如果创建失败，确保清理可能已创建的对象
                if (_drawingObject != null)
                {
                    _drawingObject.Dispose();
                    _drawingObject = null;
                }
            }
        }

        /// <summary>
        /// 当ROI被拖动或缩放时的回调函数。
        /// </summary>
        private void OnRoiUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            Dispatcher.Invoke(() =>
            {
                NotifyRoiUpdate();
                SaveRoiImageButton.IsEnabled = true;
            });
        }

        /// <summary>
        /// 收集ROI参数，创建EventArgs，并更新UI。
        /// </summary>
        private void NotifyRoiUpdate()
        {
            if (_drawingObject == null || _drawingObject.ID == -1) return;

            var parameters = new Dictionary<string, double>();
            string roiType = _drawingObject.GetDrawingObjectParams("type").S;

            try
            {
                switch (roiType)
                {
                    case "rectangle1":
                        parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                        parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                        parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                        parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
                        break;
                    case "rectangle2":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")) * 180 / Math.PI;
                        parameters["length1"] = _drawingObject.GetDrawingObjectParams("length1");
                        parameters["length2"] = _drawingObject.GetDrawingObjectParams("length2");
                        break;
                    case "circle":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["radius"] = _drawingObject.GetDrawingObjectParams("radius");
                        break;
                    case "ellipse":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")) * 180 / Math.PI;
                        parameters["radius1"] = _drawingObject.GetDrawingObjectParams("radius1");
                        parameters["radius2"] = _drawingObject.GetDrawingObjectParams("radius2");
                        break;
                    case "line":
                        parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                        parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                        parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                        parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
                        break;
                }

                using (HObject iconic = _drawingObject.GetDrawingObjectIconic())
                using (HObject contour = iconic.CopyObj(1, -1))
                {
                    var args = new RoiUpdatedEventArgs(parameters, contour);
                    // 更新Adorner和状态栏
                    RoiParametersTextBlock.Text = args.ParametersAsString.Replace(Environment.NewLine, " | ");
                    _lastRoiArgs = args;
                    UpdateAdornerPosition(args);
                }
            }
            catch (HalconException) { /* 忽略小错误 */ }
        }

        /// <summary>
        /// 清除当前活动窗口上的所有ROI。
        /// </summary>
        private void ClearActiveRoi()
        {
            // 如果处于涂抹模式，先禁用它
            if (_isPaintingMode)
            {
                DisablePaintMode();
            }

            // 如果存在标准ROI对象，分离并销毁它
            if (_drawingObject != null && _drawingObject.ID != -1 && _drawingObjectHost != null)
            {
                try
                {
                    _drawingObjectHost.HalconWindow.DetachDrawingObjectFromWindow(_drawingObject);
                }
                catch (HalconException) { /* 忽略分离错误 */ }
                _drawingObject.Dispose();
                _drawingObject = null;
                _drawingObjectHost = null;
            }

            // 清理UI
            RemoveRoiAdorner(); // 隐藏浮动框
            RoiParametersTextBlock.Text = ""; // 清空状态栏参数
            SaveRoiImageButton.IsEnabled = false;

            // 重新显示干净的图像
            RefreshActiveWindowDisplay();
        }

        /// <summary>
        /// 健壮的辅助方法，用于获取活动窗口当前应该显示的图像。
        /// </summary>
        /// <returns>返回图像的拷贝，使用后需要释放。</returns>
        private HObject GetImageFromActiveWindow()
        {
            if (_activeDisplayWindow == null) return null;

            // 逻辑1: 检查是否有相机连接到此窗口
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == _activeDisplayWindow);
            if (camera != null)
            {
                HObject camImage = camera.GetCurrentImage();
                return camImage?.CopyObj(1, -1);
            }

            // 逻辑2: 如果没有相机，检查Tag是否为本地图像
            if (_activeDisplayWindow.Tag is HObject localImage)
            {
                return localImage.CopyObj(1, -1);
            }

            return null;
        }

        /// <summary>
        /// 重新显示活动窗口的背景图像，清除所有图形叠加。
        /// </summary>
        private void RefreshActiveWindowDisplay()
        {
            if (_activeDisplayWindow == null) return;

            using (HObject image = GetImageFromActiveWindow())
            {
                if (image != null && image.IsInitialized())
                {
                    _activeDisplayWindow.HalconWindow.DispObj(image);
                }
                else
                {
                    _activeDisplayWindow.HalconWindow.ClearWindow();
                }
            }
        }
        #endregion

        #region Paint ROI Logic

        /// <summary>
        /// 启用涂抹绘制模式。
        /// </summary>
        private void EnablePaintMode()
        {
            if (_isPaintingMode) return;
            _isPaintingMode = true;

            _paintedRoi?.Dispose();
            HOperatorSet.GenEmptyRegion(out _paintedRoi);

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown += PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove += PaintRoi_HMouseMove;
            }

            UpdateStatus("涂抹模式已激活：在图像上左键开始绘制，右键结束。", false);
            SaveRoiImageButton.IsEnabled = true; // 允许在绘制过程中保存
        }

        /// <summary>
        /// 禁用并清理涂抹绘制模式。
        /// </summary>
        private void DisablePaintMode()
        {
            if (!_isPaintingMode) return;
            _isPaintingMode = false;
            _isPaintingActive = false;

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown -= PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }

            _paintedRoi?.Dispose();
            _paintedRoi = null;

            RefreshActiveWindowDisplay();
        }

        private void PaintRoi_HMouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (e.Button == MouseButton.Right)
            {
                FinalizePainting();
                return;
            }

            if (e.Button == MouseButton.Left)
            {
                _isPaintingActive = true;
                _lastPaintRow = e.Row;
                _lastPaintCol = e.Column;
                PaintAtCurrentPosition(e.Row, e.Column);
                UpdateStatus("正在涂抹... 右键单击以完成。", false);
            }
        }

        private void PaintRoi_HMouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (_isPaintingActive)
            {
                PaintAtCurrentPosition(e.Row, e.Column);
                _lastPaintRow = e.Row;
                _lastPaintCol = e.Column;
            }
        }

        
        private void PaintAtCurrentPosition(double row, double col)
        {
            if (_activeDisplayWindow == null || _paintedRoi == null) return;

            HObject brush = null;
            HObject lineRegion = null;
            HObject newRoi = null;
            HObject backgroundImage = null;
            // 在开始绘制前，保存当前的视图（缩放/平移状态）。
            Rect currentView = _activeDisplayWindow.HImagePart;
            try
            {
                // 步骤1: 计算新的ROI形状
                
                if (_lastPaintRow > -1 && _lastPaintCol > -1)
                {
                    HOperatorSet.GenRegionLine(out lineRegion, _lastPaintRow, _lastPaintCol, row, col);
                    if (_brushShape == "圆形") HOperatorSet.DilationCircle(lineRegion, out brush, _brushRadius);
                    else HOperatorSet.DilationRectangle1(lineRegion, out brush, _brushRectWidth, _brushRectHeight);
                }
                else
                {
                    if (_brushShape == "圆形") HOperatorSet.GenCircle(out brush, row, col, _brushRadius);
                    else HOperatorSet.GenRectangle1(out brush, row - _brushRectHeight / 2.0, col - _brushRectWidth / 2.0, row + _brushRectHeight / 2.0, col + _brushRectWidth / 2.0);
                }
                HOperatorSet.Union2(_paintedRoi, brush, out newRoi);
                _paintedRoi.Dispose();
                _paintedRoi = newRoi;
                newRoi = null;

                backgroundImage = GetImageFromActiveWindow();
                if (backgroundImage == null || !backgroundImage.IsInitialized()) return;

                // 重新应用之前保存的视图，而不是重置它。
                // 这一步既保证了坐标系被正确设置，又保留了用户的缩放状态。
                _activeDisplayWindow.HImagePart = currentView;

                // 先显示背景
                _activeDisplayWindow.HalconWindow.DispObj(backgroundImage);

                // 2. 再叠加ROI
                _activeDisplayWindow.HalconWindow.SetDraw("fill");
                _activeDisplayWindow.HalconWindow.SetColor("green");
                _activeDisplayWindow.HalconWindow.DispObj(_paintedRoi);
            }
            catch (HalconException) { /* 忽略小错误 */ }
            finally
            {
                brush?.Dispose();
                lineRegion?.Dispose();
                newRoi?.Dispose();
                backgroundImage?.Dispose();
            }
        }

        private void FinalizePainting()
        {
            _isPaintingActive = false;
            _isPaintingMode = false;

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown -= PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }

            UpdateStatus("涂抹绘制已完成。ROI已创建。", false);

            // 同样，在结束时也要用正确的逻辑刷新显示
            RefreshActiveWindowDisplay();

            if (_activeDisplayWindow != null && _paintedRoi != null && _paintedRoi.IsInitialized())
            {
                // 在背景图上把最终的ROI轮廓画出来
                _activeDisplayWindow.HalconWindow.SetColor("green");
                _activeDisplayWindow.HalconWindow.SetDraw("margin");
                _activeDisplayWindow.HalconWindow.DispObj(_paintedRoi);
            }
        }
        

        #endregion

        
        #region Adorner (浮动参数框) 管理
        

        /// <summary>
        /// 核心辅助方法：计算并更新Adorner在屏幕上的位置和显示的文本。
        /// </summary>
        private void UpdateAdornerPosition(RoiUpdatedEventArgs e)
        {
            if (_roiAdorner != null && e.Position.HasValue)
            {
                var hWindow = _activeDisplayWindow.HalconWindow;
                // 将Halcon的图像坐标转换为WPF控件的像素坐标
                hWindow.ConvertCoordinatesImageToWindow(e.Position.Value.Y, e.Position.Value.X, out double windowY, out double windowX);
                // 预先测量Adorner的尺寸，以便进行垂直居中对齐
                _roiAdorner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double adornerHeight = _roiAdorner.DesiredSize.Height;
                // 计算最终位置：在ROI最右侧再加一点偏移，并垂直居中
                Point newPosition = new Point(windowX + 15, windowY - adornerHeight / 2);
                _roiAdorner.Update(e.ParametersAsString, newPosition);
            }
            else if (_roiAdorner != null)
            {
                _roiAdorner.Update("", new Point(0, 0));
            }
        }

        /// <summary>
        /// 创建并显示Adorner，同时开始监听窗口视图的变化。
        /// </summary>
        private void ShowRoiAdorner()
        {
            if (_activeDisplayWindow != null)
            {
                // 获取 Adorner 层
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(_activeDisplayWindow);
                if (adornerLayer != null)
                {
                    // 创建 Adorner 实例并添加到层中
                    _roiAdorner = new RoiAdorner(_activeDisplayWindow);
                    adornerLayer.Add(_roiAdorner);
                    // --- 添加对 HImagePart 属性变化的监听 ---
                    DependencyPropertyDescriptor dpd = DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF));
                    dpd.AddValueChanged(_activeDisplayWindow, OnDisplayWindowViewChanged);
                }
            }
        }
        /// <summary>
        /// 移除Adorner，并停止监听窗口视图变化，同时清空缓存。
        /// </summary>
        private void RemoveRoiAdorner()
        {
            if (_activeDisplayWindow != null && _roiAdorner != null)
            {
                // --- 移除对 HImagePart 属性变化的监听 ---
                DependencyPropertyDescriptor dpd = DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF));
                dpd.RemoveValueChanged(_activeDisplayWindow, OnDisplayWindowViewChanged);

                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(_activeDisplayWindow);
                adornerLayer?.Remove(_roiAdorner);
                _roiAdorner = null;
                // --- 清空缓存的ROI参数 ---
                _lastRoiArgs = null;
            }
        }
        #endregion


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
        /// 主窗口关闭时触发，用于安全地释放所有资源。
        /// </summary>

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 关闭相机管理窗口（如果它还开着）
            cameraManagementWindow?.Close();

            // 清理所有ROI资源
            ClearActiveRoi();

            // 再关闭所有相机
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
        }

        
    }
}