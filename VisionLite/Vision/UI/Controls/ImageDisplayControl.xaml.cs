using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HalconDotNet;
using Microsoft.Win32;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.UI.Controls
{
    /// <summary>
    /// 图像显示控件
    /// 封装Halcon显示控件，提供图像显示和交互功能
    /// </summary>
    public partial class ImageDisplayControl : UserControl
    {
        #region 私有字段
        
        private VisionImage _currentImage;
        private bool _isImageLoaded;
        
        #endregion
        
        #region 依赖属性
        
        /// <summary>
        /// 图像源依赖属性
        /// </summary>
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource), 
                typeof(VisionImage), 
                typeof(ImageDisplayControl),
                new PropertyMetadata(null, OnImageSourceChanged));
        
        /// <summary>
        /// 图像源属性
        /// </summary>
        public VisionImage ImageSource
        {
            get => (VisionImage)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }
        
        /// <summary>
        /// 是否自动适应窗口属性
        /// </summary>
        public bool AutoFitToWindow { get; set; } = true;
        
        /// <summary>
        /// 是否显示图像信息属性
        /// </summary>
        public bool ShowImageInfo { get; set; } = true;
        
        #endregion
        
        #region 事件定义
        
        /// <summary>
        /// 图像点击事件
        /// </summary>
        public event EventHandler<ImageClickEventArgs> ImageClicked;
        
        /// <summary>
        /// 图像双击事件
        /// </summary>
        public event EventHandler<ImageClickEventArgs> ImageDoubleClicked;
        
        #endregion
        
        #region 构造函数
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ImageDisplayControl()
        {
            InitializeComponent();
            
            // 将Halcon控件初始化延迟到Loaded事件
            this.Loaded += ImageDisplayControl_Loaded;
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 显示图像
        /// </summary>
        /// <param name="image">要显示的图像</param>
        public void DisplayImage(VisionImage image)
        {
            try
            {
                if (image?.HImage == null)
                {
                    ClearDisplay();
                    return;
                }
                
                _currentImage = image;
                _isImageLoaded = true;
                
                // 检查HalconDisplay是否已初始化
                if (HalconDisplay?.HalconWindow == null)
                {
                    System.Diagnostics.Debug.WriteLine("HalconDisplay或HalconWindow尚未初始化，延迟显示图像");
                    // 延迟执行，等待控件完全加载
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        if (HalconDisplay?.HalconWindow != null)
                        {
                            DisplayImageInternal(image);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    return;
                }
                
                DisplayImageInternal(image);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearDisplay();
            }
        }
        
        /// <summary>
        /// 内部图像显示方法
        /// </summary>
        private void DisplayImageInternal(VisionImage image)
        {
            try
            {
                // 清空并显示图像
                HalconDisplay.HalconWindow.ClearWindow();
                HalconDisplay.HalconWindow.DispObj(image.HImage);
                
                // 自动适应窗口
                if (AutoFitToWindow)
                {
                    FitImageToWindow();
                }
                
                // 更新界面状态
                UpdateDisplayState();
                
                // 更新图像信息
                UpdateImageInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"内部显示图像失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清空显示
        /// </summary>
        public void ClearDisplay()
        {
            try
            {
                HalconDisplay?.HalconWindow?.ClearWindow();
                _currentImage = null;
                _isImageLoaded = false;
                UpdateDisplayState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清空显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 适合窗口大小
        /// </summary>
        public void FitImageToWindow()
        {
            if (_isImageLoaded && _currentImage != null)
            {
                try
                {
                    HalconDisplay.HalconWindow.SetPart(0, 0, _currentImage.Height - 1, _currentImage.Width - 1);
                    // 暂时禁用缩放信息更新
                    // UpdateZoomInfo();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"适合窗口失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 原始大小显示
        /// </summary>
        public void ShowOriginalSize()
        {
            if (_isImageLoaded && _currentImage != null)
            {
                try
                {
                    // 计算居中显示的位置
                    var centerX = _currentImage.Width / 2;
                    var centerY = _currentImage.Height / 2;
                    var halfWidth = (int)(HalconDisplay.ActualWidth / 2);
                    var halfHeight = (int)(HalconDisplay.ActualHeight / 2);
                    
                    HalconDisplay.HalconWindow.SetPart(
                        centerY - halfHeight, centerX - halfWidth,
                        centerY + halfHeight, centerX + halfWidth);
                    
                    // 暂时禁用缩放信息更新
                    // UpdateZoomInfo();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"原始大小显示失败: {ex.Message}");
                }
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 控件加载完成事件处理
        /// </summary>
        private void ImageDisplayControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeHalconDisplay();
        }
        
        /// <summary>
        /// 初始化Halcon显示控件
        /// </summary>
        private void InitializeHalconDisplay()
        {
            try
            {
                // 检查控件是否已经加载
                if (HalconDisplay == null)
                {
                    System.Diagnostics.Debug.WriteLine("HalconDisplay控件尚未加载");
                    return;
                }
                
                // 设置基本事件
                HalconDisplay.MouseDoubleClick += OnHalconMouseDoubleClick;
                HalconDisplay.MouseLeftButtonUp += OnHalconMouseLeftButtonUp;
                
                // 设置窗口参数
                HalconDisplay.Loaded += (s, e) =>
                {
                    try
                    {
                        HalconDisplay.HalconWindow.SetWindowParam("background_color", "black");
                        HalconDisplay.HalconWindow.SetColor("green");
                        HalconDisplay.HalconWindow.SetLineWidth(2);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"设置Halcon窗口参数失败: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化Halcon显示控件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 更新显示状态
        /// </summary>
        private void UpdateDisplayState()
        {
            if (_isImageLoaded)
            {
                NoImageText.Visibility = Visibility.Collapsed;
                ImageInfoPanel.Visibility = ShowImageInfo ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                NoImageText.Visibility = Visibility.Visible;
                ImageInfoPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        /// <summary>
        /// 更新图像信息显示
        /// </summary>
        private void UpdateImageInfo()
        {
            if (_currentImage != null && ShowImageInfo)
            {
                ImageSizeText.Text = $"尺寸: {_currentImage.Width} × {_currentImage.Height}";
                ImageTypeText.Text = $"通道: {_currentImage.Channels}";
                // 暂时禁用缩放信息更新
                // UpdateZoomInfo();
            }
        }
        
        /// <summary>
        /// 更新缩放信息
        /// </summary>
        private void UpdateZoomInfo()
        {
            // 暂时禁用缩放信息功能
            /*
            try
            {
                if (HalconDisplay?.HalconWindow != null)
                {
                    HalconDisplay.HalconWindow.GetPart(out int row1, out int column1, out int row2, out int column2);
                    var displayWidth = column2 - column1;
                    var displayHeight = row2 - row1;
                    
                    if (_currentImage != null && displayWidth > 0 && displayHeight > 0)
                    {
                        var zoomX = (double)_currentImage.Width / displayWidth;
                        var zoomY = (double)_currentImage.Height / displayHeight;
                        var zoom = Math.Min(zoomX, zoomY);
                        
                        ZoomLevelText.Text = $"缩放: {zoom:P0}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新缩放信息失败: {ex.Message}");
            }
            */
        }
        
        #endregion
        
        #region 事件处理
        
        /// <summary>
        /// 图像源变化事件处理
        /// </summary>
        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageDisplayControl control)
            {
                control.DisplayImage(e.NewValue as VisionImage);
            }
        }
        
        /// <summary>
        /// Halcon鼠标双击事件
        /// </summary>
        private void OnHalconMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isImageLoaded)
            {
                var position = e.GetPosition(HalconDisplay);
                ImageDoubleClicked?.Invoke(this, new ImageClickEventArgs
                {
                    X = position.X,
                    Y = position.Y
                });
            }
        }
        
        /// <summary>
        /// Halcon鼠标左键释放事件
        /// </summary>
        private void OnHalconMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isImageLoaded)
            {
                var position = e.GetPosition(HalconDisplay);
                ImageClicked?.Invoke(this, new ImageClickEventArgs
                {
                    X = position.X,
                    Y = position.Y
                });
            }
        }
        
        /// <summary>
        /// 适合窗口菜单点击
        /// </summary>
        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitImageToWindow();
        }
        
        /// <summary>
        /// 原始大小菜单点击
        /// </summary>
        private void OriginalSize_Click(object sender, RoutedEventArgs e)
        {
            ShowOriginalSize();
        }
        
        /// <summary>
        /// 保存图像菜单点击
        /// </summary>
        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;
            
            try
            {
                // 自动保存到指定路径
                SaveImageToDefaultPath();
            }
            catch (Exception ex)
            {
                // 通知父窗口更新状态栏
                NotifyParentStatus($"保存图像失败: {ex.Message}", false);
            }
        }
        
        /// <summary>
        /// 保存图像到默认路径
        /// </summary>
        private void SaveImageToDefaultPath()
        {
            // 默认保存路径
            string saveDirectory = @"D:\VisionLite图像保存\ALG";
            
            // 检查并创建目录
            if (!System.IO.Directory.Exists(saveDirectory))
            {
                System.IO.Directory.CreateDirectory(saveDirectory);
            }
            
            // 生成文件名：ALG_年月日_时分秒.bmp 格式
            string fileName = $"ALG_{DateTime.Now:yyyyMMdd_HHmmss}.bmp";
            string fullPath = System.IO.Path.Combine(saveDirectory, fileName);
            
            // 如果文件已存在，添加序号
            int counter = 1;
            while (System.IO.File.Exists(fullPath))
            {
                fileName = $"ALG_{DateTime.Now:yyyyMMdd_HHmmss}_{counter:00}.bmp";
                fullPath = System.IO.Path.Combine(saveDirectory, fileName);
                counter++;
            }
            
            // 保存图像
            _currentImage.SaveToFile(fullPath);
            
            // 通知父窗口更新状态栏
            NotifyParentStatus($"图像已保存: {fileName}", true);
        }
        
        /// <summary>
        /// 通知父窗口更新状态
        /// </summary>
        /// <param name="message">状态消息</param>
        /// <param name="isSuccess">是否成功</param>
        private void NotifyParentStatus(string message, bool isSuccess)
        {
            // 查找父窗口中的状态栏
            var parent = FindParent<Window>(this);
            if (parent != null)
            {
                var statusText = FindChild<System.Windows.Controls.TextBlock>(parent, "StatusText");
                if (statusText != null)
                {
                    statusText.Text = message;
                    
                    // 根据成功或失败设置颜色
                    statusText.Foreground = isSuccess ? 
                        System.Windows.Media.Brushes.Green : 
                        System.Windows.Media.Brushes.Red;
                    
                    // 3秒后恢复默认状态
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(3);
                    timer.Tick += (s, e) =>
                    {
                        statusText.Text = "就绪";
                        statusText.Foreground = System.Windows.Media.Brushes.Black;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
        
        /// <summary>
        /// 查找父级控件
        /// </summary>
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            
            if (parentObject == null) return null;
            
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
        
        /// <summary>
        /// 查找子控件
        /// </summary>
        private static T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;
            
            T foundChild = null;
            
            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                T childType = child as T;
                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }
            
            return foundChild;
        }
        
        /// <summary>
        /// 复制图像菜单点击
        /// </summary>
        private void CopyImage_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("复制功能待实现", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 图像点击事件参数
    /// </summary>
    public class ImageClickEventArgs : EventArgs
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}