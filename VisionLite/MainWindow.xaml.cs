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
                // 在执行采集前，先检查该相机是否正处于连续采集模式
                if (cameraToGrab.IsContinuousGrabbing())
                {
                    // 如果是，则弹窗提示用户，并终止本次操作
                    MessageBox.Show("设备正在连续采集中，请先停止。", "操作冲突");
                    return;
                }

                // 如果没有在连续采集，则正常执行单次采集和显示操作
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
            // 首先确保用户在下拉框中选择了一个设备
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个要操作的设备。", "提示");
                return;
            }
            string selectedDeviceID = comboBox.SelectedItem.ToString();

            // 检查这个设备是否已经打开
            if (openCameras.TryGetValue(selectedDeviceID, out CameraDevice camera))
            {
                // 检查这个特定的相机是否已经在连续采集中
                if (camera.IsContinuousGrabbing())
                {
                    MessageBox.Show($"设备 {selectedDeviceID} 已经在连续采集中。", "提示");
                    return;
                }

                // 命令选中的相机开始连续采集
                camera.StartContinuousGrab();

            }
            else
            {
                MessageBox.Show($"设备 {selectedDeviceID} 未打开，无法开始连续采集。", "提示");
            }
        }

        /// <summary>
        /// "停止连续图像采集"按钮的点击事件处理程序
        /// </summary>
        private void StopContinueCaptureButtonClick(object sender, RoutedEventArgs e)
        {
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个要停止采集的设备。", "提示");
                return;
            }

            string selectedDeviceID = comboBox.SelectedItem.ToString();

            // 检查这个设备是否已经打开
            if (openCameras.TryGetValue(selectedDeviceID, out CameraDevice camera))
            {
                // 检查这个特定的相机是否真的在连续采集中
                if (!camera.IsContinuousGrabbing())
                {
                    MessageBox.Show($"设备 {selectedDeviceID} 并未处于连续采集模式。", "提示");
                    return;
                }

                // 命令选中的相机停止连续采集
                camera.StopContinuousGrab();
                Console.WriteLine($"设备 {selectedDeviceID} 已停止连续采集。");
            }
            else
            {
                MessageBox.Show($"设备 {selectedDeviceID} 未打开，无法操作。", "提示");
            }
        }

        /// <summary>
        /// "查看设备参数"按钮的点击事件处理程序
        /// </summary>
        private void ViewCamParaButtonClick(object sender, RoutedEventArgs e)
        {
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请选择一个要查看参数的设备。", "提示");
                return;
            }
            string selectedDeviceID = comboBox.SelectedItem.ToString();

            // 检查这个设备是否已经打开
            if (openCameras.TryGetValue(selectedDeviceID, out CameraDevice camera))
            {
                // 创建参数窗口的实例，并将选中的相机对象传递给它
                ParametersWindow paramWindow = new ParametersWindow(camera);

                // 将主窗口设置为参数窗口的所有者，这样参数窗口会显示在主窗口前面
                paramWindow.Owner = this;

                // 以模态对话框的形式显示窗口。
                // 这意味着在关闭参数窗口前，用户无法操作主窗口。
                paramWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show($"设备 {selectedDeviceID} 未打开，无法查看参数。", "提示");
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