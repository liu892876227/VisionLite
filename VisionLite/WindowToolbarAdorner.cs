// WindowToolbarAdorner.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HalconDotNet;

namespace VisionLite
{
    /// <summary>
    /// 一个自定义Adorner，用于在图像窗口的右上角浮动显示上下文相关的工具栏。
    /// </summary>
    public class WindowToolbarAdorner : Adorner
    {
        // 成员变量
        private readonly Border _container; // 工具栏的背景和容器
        private readonly VisualCollection _visuals; // WPF要求Adorner必须有的可视化元素集合
        private readonly MainWindow _mainWindow; // 对主窗口的引用，用于回调
        private readonly HSmartWindowControlWPF _adornedWindow; // 此Adorner所装饰的具体窗口
        private readonly Button _btnSaveRoi;// 将保存ROI按钮设为成员变量，以便从外部控制其可用性

        /// <summary>
        /// 构造函数，在创建时初始化工具栏的所有UI元素。
        /// </summary>
        /// <param name="adornedElement">需要被装饰的UI元素（即HSmartWindowControlWPF）。</param>
        /// <param name="mainWindow">对主窗口的引用。</param>
        public WindowToolbarAdorner(UIElement adornedElement, MainWindow mainWindow) : base(adornedElement)
        {
            
            _adornedWindow = adornedElement as HSmartWindowControlWPF;
            _mainWindow = mainWindow;
            _visuals = new VisualCollection(this);
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };

            // --- 创建并添加所有按钮和控件 ---
            toolbar.Children.Add(CreateToolbarButton("CameraIcon", "打开相机管理中心", CameraManagement_Click));
            toolbar.Children.Add(new Separator { Style = Application.Current.FindResource(ToolBar.SeparatorStyleKey) as Style });
            toolbar.Children.Add(CreateToolbarButton("SingleCaptureIcon", "单次采集", SingleCapture_Click));
            toolbar.Children.Add(CreateToolbarButton("ContinueCaptureIcon", "连续采集", ContinueCapture_Click));
            toolbar.Children.Add(CreateToolbarButton("StopCaptureIcon", "停止采集", StopCapture_Click));
            toolbar.Children.Add(CreateToolbarButton("LoadImageIcon", "加载本地图像", LoadImage_Click));
            toolbar.Children.Add(CreateRoiComboBox()); // 添加ROI工具下拉框
            toolbar.Children.Add(CreateToolbarButton("SaveImageIcon", "保存原图", SaveImage_Click));
            _btnSaveRoi = CreateToolbarButton("SaveRoiImageIcon", "保存ROI区域图像", SaveRoi_Click);
            toolbar.Children.Add(_btnSaveRoi);

            // --- 创建UI容器，并将工具栏放入其中 ---
            _container = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 40, 40, 40)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(2),
                Child = toolbar
            };

            // --- 将最终的UI容器添加到可视化树中，使其能够被渲染 ---
            _visuals.Add(_container);

            // 初始化时，保存ROI按钮是禁用的
            SetSaveRoiButtonState(false);
        }

        public void SetSaveRoiButtonState(bool isEnabled)
        {
            _btnSaveRoi.IsEnabled = isEnabled;
        }

        #region Button and ComboBox Click Handlers
        // 所有按钮的点击事件都直接调用MainWindow中的公共方法，
        // 并把自己所在的窗口 (_adornedWindow) 作为参数传递，以表明操作的目标。

        private void CameraManagement_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用主窗口的公共（或internal）方法
            // 这个操作是全局的，所以不需要传递 _adornedWindow
            _mainWindow.OpenCameraManagementWindow(_adornedWindow);
        }

        private void SingleCapture_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerSingleCapture(_adornedWindow);
        private void ContinueCapture_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerContinueCapture(_adornedWindow);
        private void StopCapture_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerStopCapture(_adornedWindow);
        private void LoadImage_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerLoadImage(_adornedWindow);

        private void RoiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ComboBox的选择事件同样委托给MainWindow处理
            _mainWindow.TriggerRoiToolSelection(_adornedWindow, sender as ComboBox);
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerSaveImage(_adornedWindow);
        private void SaveRoi_Click(object sender, RoutedEventArgs e) => _mainWindow.TriggerSaveRoi(_adornedWindow);
        #endregion

        #region Helper Methods & Overrides
        /// <summary>
        /// 创建一个标准样式的工具栏按钮。
        /// </summary>
        /// <param name="iconKey">在App.xaml中定义的图标资源的Key。</param>
        /// <param name="toolTip">鼠标悬停时显示的提示文本。</param>
        /// <param name="clickHandler">按钮的点击事件处理程序。</param>
        /// <returns>一个配置好的Button实例。</returns>
        private Button CreateToolbarButton(string iconKey, string toolTip, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                ToolTip = toolTip,
                Content = Application.Current.FindResource(iconKey) as Viewbox
            };

            // 尝试从应用程序资源中查找并应用统一的按钮样式
            if (Application.Current.FindResource(ToolBar.ButtonStyleKey) is Style style)
            {
                button.Style = style;
            }
            button.Click += clickHandler;
            return button;
        }

        /// <summary>
        /// 创建并初始化ROI工具的ComboBox。
        /// </summary>
        /// <returns>一个配置好的ComboBox实例。</returns>
        private ComboBox CreateRoiComboBox()
        {
            var comboBox = new ComboBox
            {
                Width = 32,
                ToolTip = "选择或创建ROI",
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // 尝试应用全局样式
            if (Application.Current.FindResource(ToolBar.ComboBoxStyleKey) is Style style)
            {
                comboBox.Style = style;
            }

            // 添加所有下拉选项
            comboBox.Items.Add(new ComboBoxItem { Content = new ContentPresenter { Content = Application.Current.FindResource("ROIToolIcon"), Width = 18, Height = 18 } });
            comboBox.Items.Add(new ComboBoxItem { Content = "ROI工具", IsEnabled = false });
            comboBox.Items.Add(new ComboBoxItem { Content = "矩形 (Rectangle)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "带角度矩形 (Rectangle2)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "圆形 (Circle)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "椭圆 (Ellipse)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "直线 (Line)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "自由轮廓 (Contour)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "掩膜 (Mask)" });
            comboBox.Items.Add(new ComboBoxItem { Content = "橡皮擦 (Eraser)" });
            comboBox.Items.Add(new Separator());
            comboBox.Items.Add(new ComboBoxItem { Content = "清除ROI" });

            comboBox.SelectedIndex = 0; // 默认显示图标
            comboBox.SelectionChanged += RoiComboBox_SelectionChanged; // 订阅事件

            return comboBox;
        }

        // --- Adorner 必需的重写方法 ---

        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];

        /// <summary>
        /// 在WPF布局的排列阶段，计算并设置工具栏的最终位置。
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_container != null)
            {
                // 先测量控件以获取其期望尺寸
                _container.Measure(finalSize);

                double x = 5;
                double y = 5;

                // 将工具栏放置在计算出的位置
                _container.Arrange(new Rect(new Point(x, y), _container.DesiredSize));
            }
            return finalSize;
        }

        /// <summary>
        /// 在WPF布局的测量阶段，报告工具栏所需的尺寸。
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            _container?.Measure(constraint);
            return _container?.DesiredSize ?? new Size(0, 0);
        }
        #endregion
    }
}