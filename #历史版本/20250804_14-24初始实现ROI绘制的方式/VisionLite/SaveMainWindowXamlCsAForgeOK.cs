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
////using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
////using System.Windows.Shapes;
//using Microsoft.Win32;
//using System.IO;
//using HalconDotNet;

//using AForge.Video;                  // 提供了视频相关的基础功能
//using AForge.Video.DirectShow;       // 专门用于调用DirectShow相机
//using System.Drawing;                // 用于处理Bitmap图像
//using System.Drawing.Imaging;        // 用于更高级的图像格式处理

//namespace VisionLite
//{
//    /// <summary>
//    /// MainWindow.xaml 的交互逻辑
//    /// </summary>
//    public partial class MainWindow : Window
//    {


//        // 声明一个Halcon图像对象变量，用于存储加载的图像
//        private HObject ho_Image = null;

//        // AForge.NET 相机相关的变量
//        private FilterInfoCollection videoDevices; // 用来存放找到的所有相机设备
//        private VideoCaptureDevice videoSource;    // 代表我们选中的那个相机

//        public MainWindow()
//        {
//            InitializeComponent();

//            // 初始化Halcon对象变量
//            HOperatorSet.GenEmptyObj(out ho_Image);

//            // 查找电脑上所有可用的相机设备
//            FindAvailableCameras();

//        }

//        /// <summary>
//        /// 查找电脑上所有可用的相机设备
//        /// </summary>
//        private void FindAvailableCameras()
//        {
//            try
//            {
//                // 自动扫描系统，把所有视频输入设备的信息统一
//                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

//                if (videoDevices.Count == 0)
//                {
//                    // 如果一个相机都没找到
//                    MessageBox.Show("未找到任何相机！\n请确保已连接USB相机或已启动OBS虚拟相机。", "提示");
//                    LoadCamButton.IsEnabled = false; // 禁用“读取相机画面”按钮
//                }
//                else
//                {

//                    StringBuilder cameraList = new StringBuilder();
//                    for (int i = 0; i < videoDevices.Count; i++)
//                    {
//                        // 把每个相机的名字和索引都加进去
//                        cameraList.AppendLine($"索引 {i}: {videoDevices[i].Name}");
//                    }

//                    MessageBox.Show($"找到了 {videoDevices.Count} 个相机:\n\n{cameraList.ToString()}", "相机列表");
//                }


//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show("查找相机时出错: " + ex.Message, "错误");
//                LoadCamButton.IsEnabled = false;
//            }
//        }

//        /// <summary>
//        /// “加载图像”按钮的点击事件处理程序
//        /// </summary>
//        private void LoadImgButtonClick(object sender, RoutedEventArgs e)
//        {
//            // 在加载本地图像前，如果相机是打开的，先关闭它
//            StopCamera();

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
//                    ho_Image?.Dispose();

//                    // 使用Halcon的算子从路径读取图像
//                    HOperatorSet.ReadImage(out ho_Image, filePath);

//                    // 调用封装的显示方法
//                    DisplayImageInHalconWindow(ho_Image);


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
//            // 判断相机当前是否正在运行
//            if (videoSource != null && videoSource.IsRunning)
//            {
//                // 如果正在运行，那么这次点击就是要“停止”
//                StopCamera();
//            }
//            else
//            {
//                // 如果是停止状态，那么这次点击就是要“启动”
//                StartCamera();
//            }
//        }

//        /// <summary>
//        /// 启动相机并开始接收画面
//        /// </summary>
//        private void StartCamera()
//        {
//            if (videoDevices.Count > 0)
//            {
//                // 创建一个相机实例，自定义索引找到对应的相机
//                videoSource = new VideoCaptureDevice(videoDevices[5].MonikerString);

//                // 打印相机的分辨率、帧率、比特数
//                Console.WriteLine($"--- 正在检查 '{videoDevices[5].Name}' 的能力 ---");
//                foreach (var capability in videoSource.VideoCapabilities)
//                {
//                    Console.WriteLine(
//                        $"分辨率: {capability.FrameSize.Width}x{capability.FrameSize.Height} " +
//                        $"帧率: {capability.AverageFrameRate} " +
//                        $"比特数: {capability.BitCount}"
//                    );
//                }
//                Console.WriteLine("------------------------------------------");


//                // 从所有能力中选择第一个（通常是最高分辨率），或者手动指定一个
//                if (videoSource.VideoCapabilities.Length > 0)
//                {

//                    // 自定义索引表示不同的分辨率
//                    int capabilityIndex = Math.Min(0, videoSource.VideoCapabilities.Length - 1);
//                    videoSource.VideoResolution = videoSource.VideoCapabilities[capabilityIndex];
//                    Console.WriteLine($"已选择视频格式: {videoSource.VideoResolution.FrameSize.Width}x{videoSource.VideoResolution.FrameSize.Height}");
//                }


//                // 给相机订阅一个事件。
//                // 每抓到新的一帧画面，就去调用 VideoSource_NewFrame 方法
//                videoSource.NewFrame += VideoSource_NewFrame;

//                // 相机开始工作！
//                videoSource.Start();

//                // 改变按钮的文字，提示用户现在的状态
//                LoadCamButton.Content = "停止相机";

//                // 禁用“加载本地图像”按钮，防止冲突
//                LoadImgButton.IsEnabled = false;
//            }
//        }

//        /// <summary>
//        /// 停止相机工作
//        /// </summary>
//        private void StopCamera()
//        {
//            if (videoSource != null && videoSource.IsRunning)
//            {
//                // 发出停止信号，让它平稳地结束工作
//                videoSource.SignalToStop();
//                videoSource.WaitForStop(); // 等待它完全停止

//                // 取消事件订阅，防止内存泄漏
//                videoSource.NewFrame -= VideoSource_NewFrame;
//                videoSource = null;

//                // 恢复按钮状态
//                LoadCamButton.Content = "读取相机画面";
//                LoadImgButton.IsEnabled = true;
//            }
//        }


//        /// <summary>
//        /// 当相机捕获到新的一帧画面时，AForge会自动调用这个方法
//        /// </summary>
//        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
//        {
//            // 调试输出
//            Console.WriteLine($"新的一帧来了！时间: {DateTime.Now:HH:mm:ss.fff}");
//            try
//            {
//                // 从事件中拿到这一帧的图像，它是一个 Bitmap 对象。
//                // 需要克隆一份，因为原始的会被AForge很快回收。
//                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
//                {
//                    // 调用转换函数，把 .NET 的 Bitmap 变成 HALCON 的 HObject
//                    HObject newHalconImage = ConvertBitmapToHObject(bitmap);

//                    // 在HALCON窗口中显示图像。
//                    // 注意：这个方法是在后台线程被调用的，而UI更新必须在主线程。
//                    // Dispatcher.Invoke 可以把任务安全地交给主线程去执行。
//                    Dispatcher.Invoke(() =>
//                    {
//                        ho_Image?.Dispose(); // 释放上一帧的HALCON图像
//                        ho_Image = newHalconImage; // 保存新一帧的图像
//                        DisplayImageInHalconWindow(ho_Image);
//                    });
//                }
//            }
//            catch (Exception)
//            {
//                // 在高速视频流中，偶尔的转换失败是可能的，选择忽略它，而不是让程序崩溃
//            }
//        }

//        /// <summary>
//        /// 将C#的Bitmap图像高效地转换为HALCON的HObject
//        /// </summary>
//        private HObject ConvertBitmapToHObject(Bitmap bmp)
//        {
//            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

//            // 锁定Bitmap的内存，获取其原始像素数据的指针
//            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

//            HObject image;
//            // 根据Bitmap的像素格式，调用不同的HALCON算子来创建HObject
//            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
//            {
//                // 8位灰度图
//                HOperatorSet.GenImage1(out image, "byte", bmp.Width, bmp.Height, bmpData.Scan0);
//            }
//            else
//            {
//                // 24位或32位彩色图 
//                HOperatorSet.GenImageInterleaved(out image, bmpData.Scan0, "bgr", bmp.Width, bmp.Height, -1, "byte", 0, 0, 0, 0, -1, 0);
//            }

//            // 解锁Bitmap内存
//            bmp.UnlockBits(bmpData);
//            return image;
//        }

//        /// <summary>
//        /// 在Halcon窗口中显示图像 
//        /// </summary>
//        private void DisplayImageInHalconWindow(HObject imageToShow)
//        {
//            if (imageToShow == null || !imageToShow.IsInitialized()) return;
//            HWindow window = HSmart.HalconWindow;
//            HOperatorSet.GetImageSize(imageToShow, out HTuple width, out HTuple height);
//            window.SetPart(0, 0, height.I - 1, width.I - 1);
//            window.DispObj(imageToShow); // 只显示图像，不再清除窗口，以获得流畅的视频效果
//        }

//        /// <summary>
//        /// 窗口关闭时，确保所有资源都被正确释放
//        /// </summary>
//        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
//        {
//            StopCamera();         // 确保相机停止
//            ho_Image?.Dispose();  // 确保最后的HObject被释放
//        }




//    }
//}