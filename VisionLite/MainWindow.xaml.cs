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
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        // --- 使用接口来管理所有相机 ---
        private Dictionary<string, ICameraDevice> openCameras = new Dictionary<string, ICameraDevice>();
        private List<HSmartWindowControlWPF> displayWindows;
        // --- 创建一个字典来管理已打开的参数窗口 ---
        private Dictionary<string, Window> openParameterWindows = new Dictionary<string, Window>();

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

        /// <summary>
        /// 混合查找设备：先用海康SDK，再用Halcon MVision，并去重
        /// </summary>
        private void FindAndPopulateDevices(bool showSuccessMessage = false)
        {
            comboBox.Items.Clear();
            var foundDevices = new List<DeviceInfo>();
            var foundSerialNumbers = new HashSet<string>();

            // --- 1. 使用海康SDK查找 ---
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

            // --- 2. 使用Halcon MVision查找 ---
            try
            {
                HOperatorSet.InfoFramegrabber("MVision", "device", out _, out HTuple halconDeviceList);
                if (halconDeviceList != null && halconDeviceList.Length > 0)
                {
                    foreach (string deviceId in halconDeviceList.SArr)
                    {
                        
                        // 尝试从Halcon的设备ID中提取序列号来进行去重
                        string serialNumber = deviceId.Split(' ').LastOrDefault()?.Trim('\'');
                        if (!string.IsNullOrEmpty(serialNumber) && !foundSerialNumbers.Contains(serialNumber))
                        {
                            foundDevices.Add(new DeviceInfo
                            {
                                DisplayName = $"[HAL] {deviceId}",
                                UniqueID = deviceId, // Halcon直接使用完整的ID
                                SdkType = CameraSdkType.HalconMVision
                            });
                        }
                    }
                }
            }
            catch (HalconException) { /* 忽略Halcon查找错误 */ }

            // --- 3. 填充ComboBox ---
            if (foundDevices.Any())
            {
                foreach (var dev in foundDevices)
                {
                    comboBox.Items.Add(dev);
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

        // 辅助方法：从海康设备信息中获取序列号和显示名
        private string GetHikSerialNumber(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                return ((MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO))).chSerialNumber;
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
                return ((MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO))).chSerialNumber;
            return null;
        }
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

        /// <summary>
        /// "查找设备"按钮的点击事件处理程序
        /// </summary>
        private void FindCamButtonClick(object sender, RoutedEventArgs e)
        {
            // 再次调用查找和填充方法
            //MessageBox.Show("正在重新查找设备...");
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
                // 获取分配到的窗口的索引 (0, 1, 2, or 3)
                int windowIndex = displayWindows.IndexOf(freeWindow);
                // 将设备和窗口进行绑定
                openCameras.Add(selectedDevice.UniqueID, newCamera);
                freeWindow.Tag = selectedDevice.UniqueID;
                // 创建并显示包含窗口编号的提示信息 (索引+1 得到 1, 2, 3, 4)
                string successMessage = $"设备 {selectedDevice.DisplayName} 打开成功，并绑定到窗口 {windowIndex + 1}。";
                MessageBox.Show(successMessage, "成功");
                newCamera.GrabAndDisplay(); // 自动采集一帧
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
                // **检查是否有对应的参数窗口，并关闭它**
                if (openParameterWindows.TryGetValue(cameraToClose.DeviceID, out Window paramWindow))
                {
                    // 这会自动触发之前订阅的 Closed 事件，将其从字典中移除
                    paramWindow.Close();
                }

                // **关闭相机设备本身**
                cameraToClose.DisplayWindow.Tag = null;
                cameraToClose.Close();
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
                if (camera.IsContinuousGrabbing())
                {
                    MessageBox.Show("设备正在连续采集中，请先停止。", "操作冲突");
                    return;
                }
                camera.GrabAndDisplay();
            }
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
                // **检查该相机的参数窗口是否已经打开**
                if (openParameterWindows.TryGetValue(camera.DeviceID, out Window existingWindow))
                {
                    // 如果已打开，则激活它并带到最前，然后返回
                    existingWindow.Activate();
                    return;
                }


                // 通过接口调用，获取设备专属的参数窗口实例
                Window paramWindow = camera.ShowParametersWindow(this);

                // 根据相机类型决定是模态还是非模态显示
                if (camera is HalconCameraDevice)
                {
                    // Halcon设备强制使用模态对话框
                    paramWindow.ShowDialog();
                }
                else // Hikvision设备使用非模态
                {
                    paramWindow.Closed += (s, args) => {
                        openParameterWindows.Remove(camera.DeviceID);
                    };
                    openParameterWindows.Add(camera.DeviceID, paramWindow);
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
                    // 获取用户选择的文件的完整路径
                    string filePath = openFileDialog.FileName;

                    // 3. 决定要在哪个窗口显示图像
                    HSmartWindowControlWPF targetWindow = null;

                    // 首先，尝试寻找一个空闲的窗口（即Tag为null的窗口）
                    foreach (var window in displayWindows)
                    {
                        if (window.Tag == null)
                        {
                            targetWindow = window;
                            break; // 找到第一个就停止
                        }
                    }

                    // 如果所有窗口都已被相机占用，则默认使用第一个窗口
                    if (targetWindow == null)
                    {
                        targetWindow = displayWindows[0];
                    }

                    // 4. 在目标窗口中加载和显示图像
                    if (targetWindow != null)
                    {
                        // 创建一个临时的 HObject 来加载图像
                        HOperatorSet.ReadImage(out HObject loadedImage, filePath);

                        // 获取目标窗口的Halcon窗口对象
                        HWindow hWindow = targetWindow.HalconWindow;

                        // 在该窗口中显示图像
                        HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                        hWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                        hWindow.ClearWindow();
                        hWindow.DispObj(loadedImage);

                        // 记得释放临时图像对象的内存
                        loadedImage.Dispose();
                    }
                }
                catch (HalconException ex)
                {
                    // 如果Halcon操作失败，弹出错误提示
                    MessageBox.Show("加载图像失败。\nHalcon错误: " + ex.GetErrorMessage(), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    // 捕获其他可能的异常
                    MessageBox.Show("发生未知错误: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            // 在关闭所有相机之前或之后，关闭所有还开着的参数窗口
            // 由于 CloseCamButtonClick 已经处理了同步关闭，这里作为双保险
            foreach (var window in openParameterWindows.Values.ToList()) // 使用 ToList() 避免在迭代时修改集合
            {
                window.Close();
            }

            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
        }

       
    }
}