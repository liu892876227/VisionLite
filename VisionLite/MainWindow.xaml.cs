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
        // 添加一个变量来存储相机句柄
        private HTuple hv_AcqHandle = null;

        // 声明一个Halcon图像对象变量，用于存储加载的图像
        private HObject ho_Image = null;

        // 存储找到的相机设备列表
        private List<string> deviceListStrings = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            // 初始化Halcon对象变量
            HOperatorSet.GenEmptyObj(out ho_Image);

            // 在窗口启动时，自动调用查找设备方法,，不显示成功提示
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
                deviceListStrings.Clear();
                comboBox.Items.Clear();

                // 使用MVision接口查找所有可用的设备
                HOperatorSet.InfoFramegrabber("MVision", "device", out HTuple info, out HTuple deviceList);

                if (deviceList != null && deviceList.Length > 0)
                {
                    // 遍历HALCON返回的HTuple设备列表
                    for (int i = 0; i < deviceList.Length; i++)
                    {
                        string deviceId = deviceList[i].S;
                        // 将每个设备的ID添加到C#的List中
                        deviceListStrings.Add(deviceId);
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
                    FindCamButton.IsEnabled = true;

                    //根据参数决定是否显示成功提示
                    if (showSuccessMessage)
                    {
                        MessageBox.Show($"查找成功！共发现 {deviceList.Length} 个设备。", "完成");
                    }
                }
                else
                {
                    MessageBox.Show("未发现任何海康MVision设备！", "提示");
                    // 禁用相关按钮
                    OpenCamButton.IsEnabled = false;
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
            if (hv_AcqHandle != null)
            {
                MessageBox.Show("已有设备打开，请先关闭。", "提示");
                return;
            }

            // 检查ComboBox中是否有选中的项
            if (comboBox.SelectedIndex < 0)
            {
                MessageBox.Show("请先从列表中选择一个设备。", "提示");
                return;
            }

            try
            {
                // 从ComboBox获取用户选中的设备ID
                string selectedDevice = comboBox.SelectedItem.ToString();

                // 使用从HDevelop中找到的、正确的参数列表来打开指定的相机
                HOperatorSet.OpenFramegrabber(
                    "MVision",
                    1, 1, 0, 0, 0, 0, "progressive", 8, "default", -1, "false", "auto",
                    selectedDevice, // 使用用户选择的设备ID
                    0, -1,
                    out hv_AcqHandle);

                // 打开相机后，立即设置触发模式为"On"，开启触发功能
                HOperatorSet.SetFramegrabberParam(hv_AcqHandle, "TriggerMode", "On");

                // 设置触发源为"Software"，即软触发
                HOperatorSet.SetFramegrabberParam(hv_AcqHandle, "TriggerSource", "Software");

                // 告诉相机开始异步采集。-1 表示它会一直处于采集状态，直到关闭它。
                HOperatorSet.GrabImageStart(hv_AcqHandle, -1);

                MessageBox.Show($"设备 {selectedDevice} 打开成功！\n已设置为软触发模式。", "成功");

                // 更新按钮状态
                OpenCamButton.IsEnabled = false;
                CloseCamButton.IsEnabled = true;
                SingleCaptureButton.IsEnabled = true;
                ContinueCaptureButton.IsEnabled = true;

                //调用“单次采集”按钮的事件处理程序
                //SingleCaptureButtonClick(null, null);

            }
            catch (HalconException ex)
            {
                MessageBox.Show("打开设备失败: " + ex.GetErrorMessage(), "错误");
            }
        }

        /// <summary>
        /// "关闭设备"按钮的点击事件处理程序
        /// </summary>
        private void CloseCamButtonClick(object sender, RoutedEventArgs e)
        {
            // 调用封装的关闭相机方法
            CloseCamera();
            MessageBox.Show("设备已关闭。");
        }

        /// <summary>
        /// "单次触发图像采集"按钮的点击事件处理程序
        /// </summary>
        private void SingleCaptureButtonClick(object sender, RoutedEventArgs e)
        {
            
            if (hv_AcqHandle == null)
            {
                MessageBox.Show("请先打开一个设备。", "提示");
                return;
            }

            try
            {
                ho_Image?.Dispose();
                // 在GrabImage之前，先执行一次软触发命令
                HOperatorSet.SetFramegrabberParam(hv_AcqHandle, "TriggerSoftware", "do_it");

                //使用 GrabImageAsync 来获取图像
                //    最后一个参数是超时时间（毫秒），5000 表示最多等待5秒。
                HOperatorSet.GrabImageAsync(out ho_Image, hv_AcqHandle, 5000);

                //HOperatorSet.GrabImage(out ho_Image, hv_AcqHandle);
                DisplayImage(ho_Image);
            }
            catch (HalconException ex)
            {
                MessageBox.Show("采集图像失败: " + ex.GetErrorMessage(), "错误");
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
            // 在加载本地图像前，如果相机是打开的，先关闭它
            CloseCamera();

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

                    // 释放之前可能加载的图像，防止内存泄漏
                    ho_Image.Dispose();

                    // 使用Halcon的算子从路径读取图像
                    HOperatorSet.ReadImage(out ho_Image, filePath);

                    // 调用封装的显示方法
                    DisplayImage(ho_Image);


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

        ///// <summary>
        ///// “读取相机画面”按钮的点击事件处理程序(已弃用)
        ///// </summary>
        //private void LoadCamButtonClick(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        // 检查相机是否已经打开，如果没有，则打开它
        //        if (hv_AcqHandle == null)
        //        {

        //            // 'device' 参数可以获取到连接到此接口的所有设备
        //            HOperatorSet.InfoFramegrabber("MVision", "device", out HTuple info, out HTuple deviceList);


        //            // 检查 MVision 接口是否找到了设备
        //            if (deviceList == null || deviceList.Length == 0)
        //            {
        //                MessageBox.Show("海康MVision接口未发现任何设备！\n请确保：\n1. hAcqMVision.dll已正确复制。\n2. 虚拟相机已运行。", "提示");
        //                return;
        //            }

        //            // 调试信息：打印找到的设备列表
        //            StringBuilder cameraList = new StringBuilder();
        //            for (int i = 0; i < deviceList.Length; i++)
        //            {
        //                cameraList.AppendLine($"索引 {i}: {deviceList[i].S}");
        //            }
        //            MessageBox.Show($"通过MVision接口找到了 {deviceList.Length} 个设备:\n\n{cameraList.ToString()}", "设备列表");

        //            // 使用从 deviceList 中获取到的第一个设备ID。
        //            HTuple deviceIdentifier = deviceList[0];

        //            //调试信息：输出设备ID
        //            Console.WriteLine($"设备ID：{deviceIdentifier}");

        //            // 打开找到的第一个相机，并明确传入它的ID
        //            HOperatorSet.OpenFramegrabber(
        //                "MVision",       // 接口名称
        //               1, 1, 0, 0, 0, 0, "progressive", 8, "default",-1, "false", "auto",        
        //               deviceIdentifier,//"GEV:Vir07207178 Vir-CA013-20GC", 
        //               0,-1,
        //                out hv_AcqHandle); // 输出相机句柄
        //        }

        //        // 从已打开的相机采集一帧图像
        //        ho_Image.Dispose(); // 释放旧图像
        //        HOperatorSet.GrabImage(out ho_Image, hv_AcqHandle);

        //        // 调用显示方法来显示采集到的图像
        //        DisplayImage(ho_Image);
        //    }
        //    catch (HalconException ex)
        //    {
        //        MessageBox.Show("相机操作失败: " + ex.GetErrorMessage(), "Halcon错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //        // 如果出错，尝试关闭相机以便下次重试
        //        CloseCamera();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("发生未知错误: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        /// <summary>
        /// 封装的图像显示方法
        /// </summary>
        /// <param name="imageToShow">需要显示的Halcon图像对象</param>
        private void DisplayImage(HObject imageToShow)
        {
            if (imageToShow == null || !imageToShow.IsInitialized())
                return;

            // 获取控件内的Halcon窗口
            HWindow window = HSmart.HalconWindow;
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
        /// 封装的关闭相机方法
        /// </summary>
        private void CloseCamera()
        {
            if (hv_AcqHandle != null)
            {
                try
                {
                    // 在关闭设备前，先确保采集已停止。
                    // 对于MVision接口，直接调用CloseFramegrabber通常就足够了，
                    // 它会隐式地停止采集。
                    HOperatorSet.CloseFramegrabber(hv_AcqHandle);
                }
                catch (HalconException)
                {
                    // 即使关闭失败，我们也要继续执行，以确保UI状态被更新
                }
                finally
                {
                    // 无论成功还是失败，都将句柄设为null并更新UI状态
                    hv_AcqHandle = null;
                    OpenCamButton.IsEnabled = true;
                    CloseCamButton.IsEnabled = false;
                    SingleCaptureButton.IsEnabled = false;
                    ContinueCaptureButton.IsEnabled = false;
                }
               
            }
        }

        /// <summary>
        /// 窗口关闭事件，用于释放资源
        /// </summary>

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 确保在程序退出时，图像对象和相机句柄都被正确释放
            ho_Image?.Dispose();
            CloseCamera();
        }


        

        

        
    }
}