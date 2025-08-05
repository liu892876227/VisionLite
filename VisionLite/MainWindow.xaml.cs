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
using VisionLite.ROIs;


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

        private List<RoiBase> activeRois = new List<RoiBase>();

        private HObject currentImage; // 用于持有当前显示的图像
        private RoiToolWindow activeRoiWindow = null; // 用于持有当前打开的ROI工具窗口

        /// <summary>
        /// 定义交互模式的状态机
        /// </summary>
        private enum InteractionMode
        {
            None,      // 无操作
            
            Moving,    // 正在移动选中的ROI
            Resizing   // 正在缩放选中的ROI
        }
        private InteractionMode currentMode = InteractionMode.None;
        private RoiBase selectedRoi = null;     // 当前选中的ROI对象
        //private int activeHandleIndex = -1;     // 被激活的句柄索引 (我们暂时用不到，为未来缩放功能预留)
        private double lastMouseX, lastMouseY;  // 用于计算鼠标位移

        public MainWindow()
        {
            InitializeComponent();

            // 初始化显示窗口列表
            displayWindows = new List<HSmartWindowControlWPF>
            {
                HSmart1, HSmart2, HSmart3, HSmart4
            };
            
            // 在窗口启动时，自动调用查找设备方法，不显示成功提示
            FindAndPopulateDevices();
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
            //创建一个海康SDK定义的数据结构，用来接收设备列表
            var hikDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            //调用海康SDK的函数来枚举所有通过网线（MV_GIGE_DEVICE）和USB（MV_USB_DEVICE）连接的设备
            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref hikDeviceList);

            if (nRet == 0 && hikDeviceList.nDeviceNum > 0)
            {
                for (int i = 0; i < hikDeviceList.nDeviceNum; i++)
                {
                    var stDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(hikDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                    //调用辅助函数从复杂的设备信息结构中提取出唯一的序列号
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


        /// <summary>
        /// 【统一显示入口】负责在指定窗口上绘制图像和所有图形。
        /// 这是解决黑屏问题的关键。
        /// </summary>
        /// <param name="targetWindow">要绘制的目标窗口</param>
        /// <param name="imageToDisplay">要显示的背景图像</param>
        private void DisplayInWindow(HSmartWindowControlWPF targetWindow, HObject imageToDisplay)
        {
            if (targetWindow?.HalconWindow == null || imageToDisplay == null || !imageToDisplay.IsInitialized())
                return;

            HWindow hWindow = targetWindow.HalconWindow;

            try
            {
                // 每次显示都重新开启Overlay模式，确保状态正确
                hWindow.SetPaint(new HTuple("overlay"));
            }
            catch (HalconException) { /* 忽略 */ }

            // 设置坐标系
            HOperatorSet.GetImageSize(imageToDisplay, out HTuple width, out HTuple height);
            hWindow.SetPart(0, 0, height.I - 1, width.I - 1);   
            
            hWindow.DispObj(imageToDisplay);

            // 绘制ROI
            if (activeRois.Any())
            {
                foreach (var roi in activeRois)
                {
                    hWindow.SetColor(roi == selectedRoi ? "yellow" : "green");
                    roi.Draw(hWindow);
                }
            }
        }

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

            // 找到一个空闲的显示窗口
            HSmartWindowControlWPF freeWindow = displayWindows.FirstOrDefault(w => w.Tag == null);

            // 如果循环结束后，一个空闲窗口都没找到
            if (freeWindow == null)
            {
                MessageBox.Show("没有空闲的显示窗口了。", "提示");
                return;
            }

            // --- 根据设备类型创建不同的相机实例 ---
            ICameraDevice newCamera = null;
            if (selectedDevice.SdkType == CameraSdkType.Hikvision)
            {
                newCamera = new HikvisionCameraDevice(selectedDevice, freeWindow);
            }
            else // HalconMVision
            {
                newCamera = new HalconCameraDevice(selectedDevice.UniqueID, freeWindow);
            }

            if (newCamera.Open())
            {
                // 在相机成功打开后，订阅它的 ImageAcquired 事件
                newCamera.ImageAcquired += OnCameraImageAcquired;

                int windowIndex = displayWindows.IndexOf(freeWindow);
                // 将新打开的相机添加到管理字典中
                openCameras.Add(selectedDevice.UniqueID, newCamera);
                // 使用Tag属性将窗口标记为“被占用”，并记录下占用它的设备ID
                freeWindow.Tag = selectedDevice.UniqueID;

                string successMessage = $"设备 {selectedDevice.DisplayName} 打开成功，并绑定到窗口 {windowIndex + 1}。";
                MessageBox.Show(successMessage, "成功");
                // 打开后自动采集一帧图像，提供即时反馈
                newCamera.GrabAndDisplay();
            }

        }


        #region UI事件处理
        /// <summary>
        /// 这是所有相机 ImageAcquired 事件的统一处理器。
        /// 当任何相机采集到新图像时，此方法会被调用。
        /// </summary>
        /// <param name="newImage">从相机设备传来的新图像对象</param>
        private void OnCameraImageAcquired(ICameraDevice senderCamera, HObject newImage)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnCameraImageAcquired(senderCamera, newImage));
                return;
            }


            try
            {
                currentImage?.Dispose();
                currentImage = newImage;

                var targetWindow = senderCamera.DisplayWindow;

                DisplayInWindow(targetWindow, currentImage);
            }
            catch (HalconException)
            {
                newImage?.Dispose();
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

                // 取消订阅
                cameraToClose.ImageAcquired -= OnCameraImageAcquired;

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
                try
                {
                    currentImage?.Dispose();
                    HOperatorSet.ReadImage(out currentImage, openFileDialog.FileName);

                    var targetWindow = displayWindows.FirstOrDefault(w => w.Tag == null) ?? HSmart1;

                    DisplayInWindow(targetWindow, currentImage);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("加载图像失败: " + ex.Message, "错误");
                }
            }

        }

        #endregion

        #region ROI 相关方法

        /// <summary>
        /// “开启ROI窗口”按钮的点击事件处理程序
        /// </summary>
        private void StartROIButtonClick(object sender, RoutedEventArgs e)
        {
            if (currentImage == null || !currentImage.IsInitialized())
            {
                MessageBox.Show("请先加载一张图像或从相机采集一帧。", "提示");
                return;
            }

            if (activeRoiWindow != null && activeRoiWindow.IsLoaded)
            {
                activeRoiWindow.Activate();
                return;
            }

            // --- 修改：如果已经有ROI，则用现有的ROI打开窗口，否则创建新的 ---
            if (!activeRois.Any())
            {
                HOperatorSet.GetImageSize(currentImage, out HTuple width, out HTuple height);
                var defaultRectRoi = new RectangleRoi
                {
                    Row1 = (height.D / 2) - 100,
                    Column1 = (width.D / 2) - 100,
                    Row2 = (height.D / 2) + 100,
                    Column2 = (width.D / 2) + 100
                };
                activeRois.Add(defaultRectRoi);
            }

            // --- 核心修正：调用统一的显示方法来刷新ROI ---
            // 我们假设ROI操作总是在 HSmart1 上进行
            DisplayInWindow(HSmart1, currentImage);

            activeRoiWindow = new RoiToolWindow(currentImage, activeRois.First());
            activeRoiWindow.Owner = this;
            activeRoiWindow.Closed += (s, args) => activeRoiWindow = null;
            activeRoiWindow.OnRoiShapeChanged += HandleRoiShapeChange;
            activeRoiWindow.Show();
        }

       

        // --- 新增代码：步骤5 ---
        /// <summary>
        /// 处理从 RoiToolWindow 传来的形状改变请求
        /// </summary>
        private void HandleRoiShapeChange(RoiBase newRoi)
        {
            activeRois.Clear();
            activeRois.Add(newRoi);
            selectedRoi = newRoi;
            // --- 核心修正：调用统一的显示方法来刷新ROI ---
            DisplayInWindow(HSmart1, currentImage);
        }

        // --- 新增代码：步骤4 ---
        #region 鼠标交互事件处理器

        private void HSmart1_MouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (activeRoiWindow == null || !activeRoiWindow.IsLoaded) return;

            // --- 新增：记录下点击前的选中状态 ---
            var previousSelectedRoi = selectedRoi;

            // 先假设没有点中任何东西
            selectedRoi = null;
            currentMode = InteractionMode.None;

            // 倒序遍历，优先选择顶层的ROI
            for (int i = activeRois.Count - 1; i >= 0; i--)
            {
                var rect = activeRois[i] as RectangleRoi;
                if (rect != null && e.Row >= rect.Row1 && e.Row <= rect.Row2 && e.Column >= rect.Column1 && e.Column <= rect.Column2)
                {
                    selectedRoi = rect;
                    currentMode = InteractionMode.Moving;
                    lastMouseX = e.Column;
                    lastMouseY = e.Row;
                    break;
                }
            }

            // --- 核心修改：只有在选中状态发生改变时，才进行重绘 ---
            if (previousSelectedRoi != selectedRoi)
            {
                // 如果之前的选中ROI和现在的不一样了（例如从一个ROI变成null，或者从null变成一个ROI）
                // 这意味着UI需要更新（比如高亮颜色变了），所以我们调用重绘。
                DisplayInWindow(HSmart1, currentImage);
            }
            // 如果 previousSelectedRoi 和 selectedRoi 都是 null（即点击在了空白处），
            // 那么什么也不做，界面就不会黑屏了。
        }

        private void HSmart1_MouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (e.Button == MouseButton.Left && currentMode == InteractionMode.Moving && selectedRoi != null)
            {
                double deltaX = e.Column - lastMouseX;
                double deltaY = e.Row - lastMouseY;
                if (selectedRoi is RectangleRoi rect)
                {
                    rect.Row1 += deltaY; rect.Row2 += deltaY;
                    rect.Column1 += deltaX; rect.Column2 += deltaX;
                }

                DisplayInWindow(HSmart1, currentImage);
                activeRoiWindow?.UpdatePreview();

                lastMouseX = e.Column;
                lastMouseY = e.Row;
            }
        }
        private void HSmart1_MouseUp(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            currentMode = InteractionMode.None;
        }

        #endregion

        #endregion

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

            // 再关闭所有相机
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
            currentImage?.Dispose();
        }
    }
}