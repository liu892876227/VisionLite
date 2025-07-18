//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
//using Microsoft.Win32;
//using System.IO;
//using HalconDotNet;

//namespace VisionLite
//{
//    /// <summary>
//    /// MainWindow.xaml 的交互逻辑
//    /// </summary>
//    public partial class MainWindow : Window
//    {
//        // 添加一个变量来存储相机句柄
//        private HTuple hv_AcqHandle = null;

//        // 声明一个Halcon图像对象变量，用于存储加载的图像
//        private HObject ho_Image = null;
//        public MainWindow()
//        {
//            InitializeComponent();
//            // 初始化Halcon对象变量
//            HOperatorSet.GenEmptyObj(out ho_Image);
//        }

//        /// <summary>
//        /// “加载图像”按钮的点击事件处理程序
//        /// </summary>
//        private void LoadImgButtonClick(object sender, RoutedEventArgs e)
//        {
//            // 在加载本地图像前，如果相机是打开的，先关闭它
//            CloseCamera();

//            // 创建文件选择对话框
//            OpenFileDialog openFileDialog = new OpenFileDialog
//            {
//                // 设置文件过滤器，显示所有支持格式的图片
//                Filter = "所有支持的图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;" +
//                         "|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg;" +
//                         "|PNG 图片 (*.png)|*.png;" +
//                         "|位图图片 (*.bmp)|*.bmp;" +
//                         "|GIF 图片 (*.gif)|*.gif;" +
//                         "|所有文件 (*.*)|*.*",

//                Title = "选择图片文件",
//                Multiselect = false,
//                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
//            };

//            // 显示文件对话框，并检查用户是否点击了“打开”
//            if (openFileDialog.ShowDialog() == true)
//            {
//                try
//                {
//                    // 获取用户选择的文件的完整路径
//                    string filePath = openFileDialog.FileName;

//                    // 释放之前可能加载的图像，防止内存泄漏
//                    ho_Image.Dispose();

//                    // 使用Halcon的算子从路径读取图像
//                    HOperatorSet.ReadImage(out ho_Image, filePath);

//                    // 调用封装的显示方法
//                    DisplayImage(ho_Image);


//                }
//                catch (HalconException ex)
//                {
//                    // 如果Halcon操作失败，弹出错误提示
//                    MessageBox.Show("加载图像失败。\nHalcon错误: " + ex.GetErrorMessage(), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//                catch (Exception ex)
//                {
//                    // 捕获其他可能的异常
//                    MessageBox.Show("发生未知错误: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//            }



//        }

//        /// <summary>
//        /// “读取相机画面”按钮的点击事件处理程序
//        /// </summary>
//        private void LoadCamButtonClick(object sender, RoutedEventArgs e)
//        {
//            try
//            {
//                // 2. 检查相机是否已经打开，如果没有，则打开它
//                if (hv_AcqHandle == null)
//                {

//                    // 'device' 参数可以获取到连接到此接口的所有设备
//                    HOperatorSet.InfoFramegrabber("GenICamTL", "device", out HTuple info, out HTuple deviceList);

//                    // 检查是否找到了任何相机
//                    if (deviceList == null || deviceList.Length == 0)
//                    {
//                        MessageBox.Show("未找到任何相机设备！\n请确保虚拟相机已启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
//                        return;
//                    }

//                    // 打开找到的第一个相机。你的虚拟相机 "MV-CA013-20GC (Vir...)" 会在这里被找到
//                    // 后面的参数使用默认值-1或'default'即可

//                    HOperatorSet.OpenFramegrabber(
//                        "GenICamTL",       // 接口名称
//                        -1, -1, 0, 0, 0, 0, "default", -1, "default", -1, "false", "default",
//                        "default",         // 使用 "default" 会自动选择找到的第一个相机设备
//                        -1, -1,
//                        out hv_AcqHandle); // 输出相机句柄
//                }

//                // 3. 从已打开的相机采集一帧图像
//                ho_Image.Dispose(); // 释放旧图像
//                HOperatorSet.GrabImage(out ho_Image, hv_AcqHandle);

//                // 4. 调用显示方法来显示采集到的图像
//                DisplayImage(ho_Image);
//            }
//            catch (HalconException ex)
//            {
//                MessageBox.Show("相机操作失败: " + ex.GetErrorMessage(), "Halcon错误", MessageBoxButton.OK, MessageBoxImage.Error);
//                // 如果出错，尝试关闭相机以便下次重试
//                CloseCamera();
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show("发生未知错误: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        /// <summary>
//        /// 封装的图像显示方法，避免代码重复
//        /// </summary>
//        /// <param name="imageToShow">需要显示的Halcon图像对象</param>
//        private void DisplayImage(HObject imageToShow)
//        {
//            if (imageToShow == null || !imageToShow.IsInitialized())
//                return;

//            // 获取控件内的Halcon窗口
//            HWindow window = HSmart.HalconWindow;
//            // 获取图像的宽度和高度
//            HOperatorSet.GetImageSize(imageToShow, out HTuple width, out HTuple height);
//            // 设置窗口的显示部分，以确保图像完整且居中显示
//            window.SetPart(0, 0, height.I - 1, width.I - 1);
//            // 清除窗口之前的内容
//            window.ClearWindow();
//            // 将图像对象显示在窗口上
//            window.DispObj(imageToShow);
//        }

//        /// <summary>
//        /// 封装的关闭相机方法
//        /// </summary>
//        private void CloseCamera()
//        {
//            if (hv_AcqHandle != null)
//            {
//                HOperatorSet.CloseFramegrabber(hv_AcqHandle);
//                hv_AcqHandle = null;
//            }
//        }

//        /// <summary>
//        /// 窗口关闭事件，用于释放资源
//        /// </summary>

//        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
//        {
//            // 确保在程序退出时，图像对象和相机句柄都被正确释放
//            ho_Image?.Dispose();
//            CloseCamera();
//        }


//    }
//}