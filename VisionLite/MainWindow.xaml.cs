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
        // 声明一个Halcon图像对象变量，用于存储加载的图像

        private HObject ho_Image = null;
        public MainWindow()
        {
            InitializeComponent();
            // 初始化Halcon对象变量
            HOperatorSet.GenEmptyObj(out ho_Image);
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

                    // 释放之前可能加载的图像，防止内存泄漏
                    ho_Image.Dispose();

                    // 使用Halcon的算子从路径读取图像
                    HOperatorSet.ReadImage(out ho_Image, filePath);

                    // 在HSmartWindowControlWPF控件中显示图像
                    HWindow window = HSmart.HalconWindow; // 获取控件内的Halcon窗口

                    // 获取图像的宽度和高度
                    HOperatorSet.GetImageSize(ho_Image, out HTuple width, out HTuple height);

                    // 设置窗口的显示部分，以确保图像完整且居中显示
                    window.SetPart(0, 0, height.I - 1, width.I - 1);

                    // 清除窗口之前的内容
                    window.ClearWindow();

                    // 将图像对象显示在窗口上
                    window.DispObj(ho_Image);
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
        /// 窗口关闭事件，用于释放资源
        /// </summary>
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 确保在程序退出时释放Halcon图像对象占用的内存
            if (ho_Image != null)
            {
                ho_Image.Dispose();
            }
        }

        private void LoadCamButtonClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
