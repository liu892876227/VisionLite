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


namespace VisionLite
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        // 使用字典来管理所有打开的相机。
        // 键(Key)是相机的ID字符串，值(Value)是创建的CameraDevice对象。
        private Dictionary<string, CameraDevice> openCameras = new Dictionary<string, CameraDevice>();

        // 将四个显示窗口放入一个列表中，方便按顺序访问。
        private List<HSmartWindowControlWPF> displayWindows;
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
        /// 查找所有可用的MVision设备，并填充到ComboBox中。
        /// </summary>
        /// <param name="showSuccessMessage">如果为true，则在查找成功后显示提示信息。</param>
        private void FindAndPopulateDevices(bool showSuccessMessage = false)
        {
            try
            {
                // 清空之前的设备列表和ComboBox内容，防止重复添加
                comboBox.Items.Clear();

                // 使用MVision接口查找所有可用的设备
                HOperatorSet.InfoFramegrabber("MVision", "device", out HTuple info, out HTuple deviceList);

               

                if (deviceList != null && deviceList.Length > 0)
                {
                    // 遍历HALCON返回的HTuple设备列表
                    for (int i = 0; i < deviceList.Length; i++)
                    {
                        string deviceId = deviceList[i].S;
                       
                        // 将每个设备的ID添加到界面上的ComboBox中，让用户能看到
                        comboBox.Items.Add(deviceId);
                    }

                    // 默认选中第一个找到的设备
                    if (comboBox.Items.Count > 0)
                    {
                        comboBox.SelectedIndex = 0;
                    }

                    // 启用/禁用相关按钮
                    OpenCamButton.IsEnabled = true;
                    
                }
                else
                {
                    MessageBox.Show("未发现任何海康MVision设备！", "提示");
                    // 禁用相关按钮
                    OpenCamButton.IsEnabled = false;
                }

                //根据参数决定是否显示成功提示
                if (showSuccessMessage)
                {
                    MessageBox.Show($"查找成功！共发现 {deviceList.Length} 个设备。", "完成");
                }
            }
            catch (HalconException ex)
            {
                MessageBox.Show("查找设备时发生HALCON错误: " + ex.GetErrorMessage(), "错误");
            }
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
            // 如果已经有相机打开，先提示用户关闭
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请先选择一个要打开的设备。", "提示");
                return;
            }

            string selectedDeviceID = comboBox.SelectedItem.ToString();

            // 检查设备是否已打开
            if (openCameras.ContainsKey(selectedDeviceID))
            {
                MessageBox.Show($"设备 {selectedDeviceID} 已经打开了。", "提示");
                return;
            }

            // 检查是否已达到最大连接数
            if (openCameras.Count >= 4)
            {
                MessageBox.Show("最多只能打开4个设备。", "提示");
                return;
            }

            // 找到一个空闲的显示窗口
            HSmartWindowControlWPF freeWindow = null;
            foreach (var window in displayWindows)
            {
                if (window.Tag == null) // 用Tag属性来标记窗口是否被占用
                {
                    freeWindow = window;
                    break;
                }
            }

            // 如果循环结束后，一个空闲窗口都没找到
            if (freeWindow == null)
            {
                MessageBox.Show("没有空闲的显示窗口了。", "提示");
                return;
            }

            // 创建并打开新的相机设备
            var newCamera = new CameraDevice(selectedDeviceID, freeWindow);
            if (newCamera.Open())
            {
                // 打开成功
                openCameras.Add(selectedDeviceID, newCamera);
                freeWindow.Tag = selectedDeviceID; // 将窗口标记为被占用
                MessageBox.Show($"设备 {selectedDeviceID} 打开成功，并绑定到窗口 {displayWindows.IndexOf(freeWindow) + 1}。");

                // 自动采集一帧
                newCamera.GrabAndDisplay();
            }

        }

        /// <summary>
        /// "关闭设备"按钮的点击事件处理程序
        /// </summary>
        private void CloseCamButtonClick(object sender, RoutedEventArgs e)
        {
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个要关闭的设备。", "提示");
                return;
            }
            string selectedDeviceID = comboBox.SelectedItem.ToString();

            // 检查设备是否真的打开了
            if (openCameras.TryGetValue(selectedDeviceID, out CameraDevice cameraToClose))
            {
                // 关闭相机并释放资源
                cameraToClose.Close();

                // 将窗口的Tag标记清除，表示它现在空闲了
                cameraToClose.DisplayWindow.Tag = null;

                // 从字典中移除该相机
                openCameras.Remove(selectedDeviceID);
                MessageBox.Show($"设备 {selectedDeviceID} 已关闭。");
            }
            else
            {
                MessageBox.Show($"设备 {selectedDeviceID} 并未打开。", "提示");
            }
        }

        /// <summary>
        /// "单次触发图像采集"按钮的点击事件处理程序
        /// </summary>
        private void SingleCaptureButtonClick(object sender, RoutedEventArgs e)
        {

            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个要触发的设备。", "提示");
                return;
            }
            string selectedDeviceID = comboBox.SelectedItem.ToString();

            if (openCameras.TryGetValue(selectedDeviceID, out CameraDevice cameraToGrab))
            {
                // 对选中的相机执行采集和显示操作
                cameraToGrab.GrabAndDisplay();
            }
            else
            {
                MessageBox.Show($"设备 {selectedDeviceID} 未打开，无法采集。", "提示");
            }

           
        }

        /// <summary>
        /// "连续触发图像采集"按钮的点击事件处理程序
        /// </summary>
        private void ContinueCaptureButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("连续触发功能待实现。");
        }

        /// <summary>
        /// "查看设备参数"按钮的点击事件处理程序
        /// </summary>
        private void ViewCamParaButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("查看设备参数功能待实现。");
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

        ///// <summary>
        ///// 封装的关闭相机方法
        ///// </summary>
        //private void CloseCamera()
        //{
        //    if (hv_AcqHandle != null)
        //    {
        //        try
        //        {
        //            // 在关闭设备前，先确保采集已停止。
        //            // 对于MVision接口，直接调用CloseFramegrabber通常就足够了，
        //            // 它会隐式地停止采集。
        //            HOperatorSet.CloseFramegrabber(hv_AcqHandle);
        //        }
        //        catch (HalconException)
        //        {
        //            // 即使关闭失败，我们也要继续执行，以确保UI状态被更新
        //        }
        //        finally
        //        {
        //            // 无论成功还是失败，都将句柄设为null并更新UI状态
        //            hv_AcqHandle = null;
        //            OpenCamButton.IsEnabled = true;
        //            CloseCamButton.IsEnabled = false;
        //            SingleCaptureButton.IsEnabled = false;
        //            ContinueCaptureButton.IsEnabled = false;
        //        }
               
        //    }
        //}

        /// <summary>
        /// 窗口关闭事件，用于释放资源
        /// </summary>

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 遍历所有已打开的相机并逐个关闭
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
        }


        

        

        
    }
}