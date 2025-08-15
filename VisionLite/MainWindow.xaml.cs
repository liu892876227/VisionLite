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
using System.ComponentModel;
using System.Windows.Threading;

namespace VisionLite
{
    /// <summary>
    /// 应用程序的主窗口，是所有UI交互和业务逻辑的协调中心。
    /// 负责管理：
    /// - 多个图像显示窗口的布局和激活状态。
    /// - 所有已连接的相机设备实例。
    /// - 与子窗口（如相机管理、ROI工具）的通信。
    /// - 用户通过顶部工具栏发起的各种操作。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 核心字段和属性

        /// <summary>
        /// 管理所有已打开的相机设备。
        /// Key: 设备的唯一ID (string)。
        /// Value: 实现ICameraDevice接口的相机对象实例。
        /// 'internal'修饰符允许同程序集中的其他类（如CameraManagementWindow）访问它。
        /// </summary>
        internal Dictionary<string, ICameraDevice> openCameras = new Dictionary<string, ICameraDevice>();

        /// <summary>
        /// 跟踪非模态的相机管理窗口实例，以实现单例模式（只打开一个）。
        /// </summary>
        private CameraManagementWindow cameraManagementWindow = null;
        
        /// <summary>
        /// 当相机列表（打开/关闭相机）发生变化时触发的事件。
        /// 用于通知其他窗口（如CameraManagementWindow）同步其UI状态。
        /// </summary>
        public event EventHandler CameraListChanged;
        
        /// <summary>
        /// 管理所有用于显示图像的WPF控件的列表。
        /// </summary>
        private List<HSmartWindowControlWPF> displayWindows;
        
        /// <summary>
        /// 存储当前被用户点击选中的活动显示窗口。
        /// 所有针对窗口的操作（如采集、加载图像）都将作用于此窗口。
        /// </summary>
        private HSmartWindowControlWPF _activeDisplayWindow;

        #endregion

        #region 窗口信息显示相关字段

        /// <summary>
        /// 存储每个显示窗口对应的数据模型。
        /// Key: HSmartWindowControlWPF 实例
        /// Value: 对应的 WindowInfo 数据对象
        /// </summary>
        private Dictionary<HSmartWindowControlWPF, WindowInfo> _windowInfos;

        /// <summary>
        /// 存储每个显示窗口对应的Adorner实例。
        /// </summary>
        private Dictionary<HSmartWindowControlWPF, InfoWindowAdorner> _infoAdorners;

        #endregion

        #region ROI 和涂抹绘制相关字段

        /// <summary>
        /// 当前在活动窗口上显示的交互式ROI对象。
        /// </summary>
        private HDrawingObject _drawingObject;

        /// <summary>
        /// 记录当前ROI所在的显示窗口，以便在切换活动窗口时进行管理。
        /// </summary>
        private HSmartWindowControlWPF _drawingObjectHost;

        /// <summary>
        /// 用于存储涂抹模式下绘制的ROI区域。
        /// </summary>
        private HObject _paintedRoi;

        /// <summary>
        /// 标记当前是否处于涂抹绘制模式。
        /// </summary>
        private bool _isPaintingMode = false;

        /// <summary>
        /// 标记在涂抹模式下，鼠标左键是否按下以激活绘制。
        /// </summary>
        private bool _isPaintingActive = false;

        /// <summary>
        /// 缓存由ROI更新事件生成的最新参数，用于驱动所有UI。
        /// </summary>
        private RoiUpdatedEventArgs _lastRoiArgs;

        /// <summary>
        /// 持有当前在活动窗口上显示的ROI参数浮动框 (Adorner) 的实例。
        /// </summary>
        private RoiAdorner _roiAdorner;

        //  添加防抖计时器 
        private DispatcherTimer _roiUpdateDebounceTimer;

        // --- 用于涂抹绘制的辅助变量 ---
        private HTuple _lastPaintRow = -1;
        private HTuple _lastPaintCol = -1;
        private string _brushShape = "圆形";
        private int _brushCircleRadius = 20;
        private int _brushRectWidth = 20;
        private int _brushRectHeight = 20;

        /// <summary>
        /// 防止UI控件和ROI对象之间双向更新导致无限循环的标志位。
        /// </summary>
        private bool _isUpdatingFromRoi = false;

        #endregion

        //构造函数
        public MainWindow()
        {
            InitializeComponent();

            // 初始化显示窗口列表
            displayWindows = new List<HSmartWindowControlWPF>
            {
                HSmart1, HSmart2, HSmart3, HSmart4
            };

            // 初始化信息字典
            _windowInfos = new Dictionary<HSmartWindowControlWPF, WindowInfo>();
            _infoAdorners = new Dictionary<HSmartWindowControlWPF, InfoWindowAdorner>();

            // 为每个窗口添加鼠标点击事件处理器
            foreach (var window in displayWindows)
            {
                window.PreviewMouseDown += DisplayWindow_PreviewMouseDown;
                // 为每个窗口创建一个数据模型
                _windowInfos.Add(window, new WindowInfo());
                // 订阅鼠标移动事件
                window.HMouseMove += DisplayWindow_HMouseMove;

            }
            // 在窗口加载完成后，默认激活第一个显示窗口
            this.Loaded += (s, e) => {
                // 新增：附加Adorner并初始化显示
                foreach (var window in displayWindows)
                {
                    // 附加视图变化事件，用于更新缩放比例
                    DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                        .AddValueChanged(window, OnDisplayWindowViewChanged);

                    // 附加尺寸变化事件，也用于更新缩放比例
                    window.SizeChanged += DisplayWindow_SizeChanged;

                    // 创建并附加Adorner
                    var adornerLayer = AdornerLayer.GetAdornerLayer(window);
                    if (adornerLayer != null)
                    {
                        var infoAdorner = new InfoWindowAdorner(window);
                        adornerLayer.Add(infoAdorner);
                        _infoAdorners.Add(window, infoAdorner);
                    }

                    // 初始化显示
                    UpdateWindowInfoOnSourceChanged(window, "空闲");
                }

                SetActiveDisplayWindow(HSmart1);
            };

            // 第2步：初始化防抖计时器
            _roiUpdateDebounceTimer = new DispatcherTimer
            {
                // 设置一个较短的延迟，例如50毫秒
                Interval = TimeSpan.FromMilliseconds(50)
            };
            // 订阅Tick事件，这是我们真正执行更新的地方
            _roiUpdateDebounceTimer.Tick += RoiUpdateDebounceTimer_Tick;
        }

        #region 工具栏按钮事件处理

        /// <summary>
        /// "相机管理"工具栏按钮的点击事件处理程序。
        /// 实现相机管理窗口的单例模式。
        /// </summary>
        private void CameraManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraManagementWindow != null)
            {
                cameraManagementWindow.Activate();// 如果已存在，则激活并置于顶层
            }
            else
            {
                cameraManagementWindow = new CameraManagementWindow(this);// 否则创建新实例
                cameraManagementWindow.Show();
            }
        }

        /// <summary>
        /// “单次采集”工具栏按钮的点击事件。
        /// </summary>
        private void SingleCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == _activeDisplayWindow);
            if (camera == null)
            {
                UpdateStatus("提示：当前活动窗口没有连接相机。", true);
                return;
            }

            try
            {

                if (camera.IsContinuousGrabbing())
                {
                    // 修改：使用状态栏提示
                    UpdateStatus("操作冲突：相机正在连续采集中，请先停止。", true);
                    return;
                }
                camera.GrabAndDisplay();
                using (HObject capturedImage = camera.GetCurrentImage()?.CopyObj(1, -1))
                {
                    // 传入相机ID和安全的图像副本
                    UpdateWindowInfoOnSourceChanged(_activeDisplayWindow, camera.DeviceID, capturedImage);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"单次采集失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// “连续采集”工具栏按钮的点击事件。
        /// </summary>
        private void ContinueCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var camera = GetCameraForActiveWindow();
                camera?.StartContinuousGrab();
            }
            catch (Exception ex)
            {
                UpdateStatus($"开始连续采集失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// “停止采集”工具栏按钮的点击事件。
        /// </summary>
        private void StopCaptureToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var camera = GetCameraForActiveWindow();
            camera?.StopContinuousGrab();
        }

        /// <summary>
        /// “加载图像”按钮的点击事件处理程序
        /// </summary>
        private void LoadImgButtonClick(object sender, RoutedEventArgs e)
        {
            // --- 在所有操作之前，先检查目标窗口的状态 ---
            HSmartWindowControlWPF targetWindow = _activeDisplayWindow;

            // 检查是否有活动窗口
            if (targetWindow == null)
            {
                UpdateStatus("请先点击选择一个显示窗口。", true);
                return;
            }

            // 检查活动窗口是否已经被相机占用
            if (openCameras.Values.Any(c => c.DisplayWindow == targetWindow))
            {
                string deviceId = openCameras.First(kvp => kvp.Value.DisplayWindow == targetWindow).Key;
                UpdateStatus($"此窗口已被相机 '{deviceId}' 占用，无法加载图像。", true);
                return;
            }

            // --- 如果检查通过，才继续执行后续的文件选择和加载逻辑 ---

            // 在加载新图像前，清除当前窗口的任何ROI
            if (_activeDisplayWindow == _drawingObjectHost || _isPaintingMode)
            {
                ClearActiveRoi();
            }

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
                HObject loadedImage = null; // 在 try-catch 外部声明，以便 finally 中可以访问
                try
                {
                    // 如果窗口之前有本地图像，先释放它
                    if (targetWindow.Tag is HObject oldImage)
                    {
                        oldImage.Dispose();
                        targetWindow.Tag = null; // 清理Tag
                    }

                    // 加载新图像
                    HOperatorSet.ReadImage(out loadedImage, openFileDialog.FileName);

                    HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                    // 通过设置WPF属性来设置视图
                    targetWindow.HImagePart = new Rect(0, 0, width.I, height.I);

                    // 清除窗口并显示
                    targetWindow.HalconWindow.ClearWindow();
                    targetWindow.HalconWindow.DispObj(loadedImage);
                    // 将加载的图像对象存储在Tag中，以便后续操作（如保存）可以找到它
                    targetWindow.Tag = loadedImage;

                    // 调用新的更新方法，传入文件名和加载的图像对象
                    string fileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                    UpdateWindowInfoOnSourceChanged(targetWindow, fileName, loadedImage);

                    // 将局部变量设为null，防止它在finally块中被错误地释放。
                    // 因为它的所有权已经转移给了targetWindow.Tag。
                    loadedImage = null;

                }

                catch (Exception ex)
                {
                    UpdateStatus($"加载图像时发生错误: {ex.Message}", true);
                    // 如果发生异常，需要清理可能已加载的本地图像，并重置窗口状态
                    if (targetWindow.Tag is HObject)
                    {
                        (targetWindow.Tag as HObject)?.Dispose();
                        targetWindow.Tag = null;
                    }
                    UpdateWindowInfoOnSourceChanged(targetWindow, "空闲");

                }
                finally
                {
                    // 如果在操作过程中发生异常，loadedImage 可能还未被赋给 Tag，
                    // 这种情况下需要确保它被释放，以防内存泄漏。
                    loadedImage?.Dispose();
                }
            }

        }

        /// <summary>
        /// ROI形状下拉框选择变化时的事件处理。
        /// </summary>
        private void RoiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;

            // 你的ComboBox有两个标题项（索引0和1），所以只有当索引大于1时才响应
            if (comboBox == null || comboBox.SelectedIndex < 2 || !this.IsLoaded)
            {
                return;
            }

            // 获取选择的ROI类型
            var selectedItem = (comboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            try
            {
                // 检查是否有活动窗口
                if (_activeDisplayWindow == null)
                {
                    UpdateStatus("请先点击选择一个显示窗口以使用ROI工具。", true);
                    return;
                }

                // 检查活动窗口是否有图像
                using (HObject currentImage = GetImageFromActiveWindow())
                {
                    if (currentImage == null || !currentImage.IsInitialized() || currentImage.CountObj() < 1)
                    {
                        UpdateStatus("当前活动窗口中没有可用的图像，无法创建ROI。", true);
                        return;
                    }
                } // using语句确保currentImage被释放

                // 在创建新ROI之前，先清除当前活动窗口上任何已存在的ROI
                ClearActiveRoi();

                // 根据选择执行操作
                switch (selectedItem)
                {
                    case "清除ROI":
                        // ClearActiveRoi 已经在上面调用过了，这里不需要额外操作
                        break;
                    case "涂抹式ROI (Paint)":
                        EnablePaintMode();
                        break;
                    default:
                        // 创建标准的HDrawingObject ROI
                        CreateDrawingObject(selectedItem);
                        break;
                }
            }
            finally
            {
                // 操作完成后重置下拉框，使其变回一个按钮的样子
                comboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// "保存原图"按钮的点击事件处理。
        /// </summary>
        private void SaveOriginalImageButton_Click(object sender, RoutedEventArgs e)
        {
            HObject imageToSave = GetImageFromActiveWindow();
            if (imageToSave == null || !imageToSave.IsInitialized())
            {
                UpdateStatus("活动窗口中没有图像可供保存。", true);
                return;
            }

            try
            {
                // 保存路径
                string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "IMG");
                // 确保这个子文件夹存在，如果不存在则自动创建
                Directory.CreateDirectory(savePath);
                // 文件名
                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                // 组合成完整路径
                string fullPath = System.IO.Path.Combine(savePath, fileName);

                // 保存图像
                HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);

                UpdateStatus($"图像已成功保存到：{fullPath}", false);
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", true);
            }
            finally
            {
                imageToSave?.Dispose();
            }
        }

        /// <summary>
        /// "保存ROI区域图像"按钮的点击事件处理。
        /// </summary>
        private void SaveRoiImageButton_Click(object sender, RoutedEventArgs e)
        {
            HObject sourceImage = GetImageFromActiveWindow();
            if (sourceImage == null || !sourceImage.IsInitialized())
            {
                UpdateStatus("错误: 没有背景图像，无法保存ROI。", true);
                return;
            }

            HObject regionToSave = null;
            bool isTempRegion = false;

            // 重新排序判断逻辑：优先检查是否存在有效的 _paintedRoi。
            // 这与 _isPaintingMode 的状态无关，只关心是否有绘制结果存在。
            if (_paintedRoi != null && _paintedRoi.IsInitialized() && _paintedRoi.CountObj() > 0)
            {
                regionToSave = _paintedRoi;
                // _paintedRoi 是类成员，由ClearActiveRoi管理，所以这里不需要临时标志
                isTempRegion = false;
            }
            // 如果没有涂抹式ROI，再检查是否存在标准的 HDrawingObject ROI。
            else if (_drawingObject != null && _drawingObject.ID != -1)
            {
                try
                {
                    using (HObject iconicObject = _drawingObject.GetDrawingObjectIconic())
                    {
                        string roiType = _drawingObject.GetDrawingObjectParams("type").S;
                        if (roiType == "line")
                        {
                            UpdateStatus("错误: 无法保存直线ROI的图像。", true);
                            sourceImage.Dispose();
                            return;
                        }
                        if (roiType.Contains("xld"))
                        {
                            HOperatorSet.GenRegionContourXld(iconicObject, out regionToSave, "filled");
                        }
                        else
                        {
                            regionToSave = iconicObject.CopyObj(1, -1);
                        }
                    }
                    // 从 DrawingObject 获取的 Region 是一个临时的拷贝，需要在使用后释放。
                    isTempRegion = true;
                }
                catch (HalconException hex)
                {
                    UpdateStatus($"获取ROI区域时出错: {hex.GetErrorMessage()}", true);
                    sourceImage.Dispose();
                    return;
                }
            }
            // 如果以上两种情况都没有找到有效的ROI，则报错。
            if (regionToSave == null || !regionToSave.IsInitialized() || regionToSave.CountObj() == 0)
            {
                UpdateStatus("错误: 没有有效的ROI可以保存。", true);
                sourceImage.Dispose();
                return;
            }

            HObject imageReduced = null;
            HObject imageToSave = null;
            // 执行保存逻辑
            try
            {
                HOperatorSet.ReduceDomain(sourceImage, regionToSave, out imageReduced);
                HOperatorSet.CropDomain(imageReduced, out imageToSave);

                // ROI保存路径
                string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "ROI");

                Directory.CreateDirectory(savePath);
                string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                string fullPath = System.IO.Path.Combine(savePath, fileName);
                HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);
                UpdateStatus($"ROI图像已成功保存到：{fullPath}", false);

            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", true);
            }
            finally
            {
                // 释放所有在此方法中创建或获取的对象
                sourceImage?.Dispose();
                imageReduced?.Dispose();
                imageToSave?.Dispose();
                if (isTempRegion)
                {
                    regionToSave?.Dispose();
                }
            }
        }
        #endregion

        #region 窗口激活与视图管理
        /// <summary>
        /// 设置指定的显示窗口为活动窗口，并更新其UI（高亮边框）。
        /// </summary>
        /// <param name="newActiveWindow">要被激活的显示窗口。</param>
        private void SetActiveDisplayWindow(HSmartWindowControlWPF newActiveWindow)
        {
            if (newActiveWindow == null) return;

            // 如果点击的恰好就是当前已经激活的窗口，则什么都不做，直接返回。
            // 这是防止不必要重绘和逻辑错误的关键。
            if (newActiveWindow == _activeDisplayWindow)
            {
                return;
            }

            HSmartWindowControlWPF oldActiveWindow = _activeDisplayWindow;
            _activeDisplayWindow = newActiveWindow; // 更新活动窗口的引用

            // 更新所有窗口的UI边框
            Border1.BorderBrush = (newActiveWindow == HSmart1) ? Brushes.DodgerBlue : Brushes.Gray;
            Border1.BorderThickness = new Thickness((newActiveWindow == HSmart1) ? 2 : 1);
            Border2.BorderBrush = (newActiveWindow == HSmart2) ? Brushes.DodgerBlue : Brushes.Gray;
            Border2.BorderThickness = new Thickness((newActiveWindow == HSmart2) ? 2 : 1);
            Border3.BorderBrush = (newActiveWindow == HSmart3) ? Brushes.DodgerBlue : Brushes.Gray;
            Border3.BorderThickness = new Thickness((newActiveWindow == HSmart3) ? 2 : 1);
            Border4.BorderBrush = (newActiveWindow == HSmart4) ? Brushes.DodgerBlue : Brushes.Gray;
            Border4.BorderThickness = new Thickness((newActiveWindow == HSmart4) ? 2 : 1);

            // 处理Adorner的“迁移”
            // 如果旧的活动窗口是ROI的宿主窗口，则从它上面移除Adorner
            if (oldActiveWindow == _drawingObjectHost)
            {
                RemoveRoiAdorner(oldActiveWindow);
            }

            // 如果新的活动窗口是ROI的宿主窗口，则在它上面重新创建Adorner
            if (newActiveWindow == _drawingObjectHost)
            {
                ShowRoiAdorner();
            }

            // 统一更新状态栏参数面板
            // 这段逻辑能优雅地处理所有情况：
            // - 如果新窗口有ROI，则显示参数。
            // - 如果新窗口没有ROI，则_drawingObjectHost不匹配，会进入else，从而隐藏参数。
            if (_activeDisplayWindow == _drawingObjectHost)
            {
                ProcessRoiUpdate(); // 更新UI（Adorner和状态栏）
            }
            else
            {
                RoiParameterPanel.Children.Clear();
                RoiParameterPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 所有显示窗口共用的鼠标点击事件处理器。
        /// </summary>
        private void DisplayWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is HSmartWindowControlWPF clickedWindow)
            {
                SetActiveDisplayWindow(clickedWindow);
            }
        }

        /// <summary>
        /// 任意显示窗口的 HImagePart (视图) 变化时触发。
        /// 用于更新缩放比例和浮动ROI参数框的位置。
        /// </summary>
        private void OnDisplayWindowViewChanged(object sender, EventArgs e)
        {
            if (sender is HSmartWindowControlWPF window)
            {
                // 更新该窗口的信息
                UpdateWindowZoom(window);
            }

            // 确保事件来自于当前活动的窗口，并且缓存的ROI参数有效
            if (sender == _activeDisplayWindow && _roiAdorner != null && _lastRoiArgs != null)
            {
                RefreshAdorner();
            }
        }
        
        /// <summary>
        /// 当任一显示窗口的尺寸变化时触发，用于更新缩放比例。
        /// </summary>
        private void DisplayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is HSmartWindowControlWPF window)
            {
                // 更新该窗口的信息
                UpdateWindowZoom(window);
            }
        }

        /// <summary>
        /// 任意显示窗口的鼠标移动事件处理器。
        /// 用于实时更新Adorner中显示的坐标和像素值。
        /// </summary>
        private void DisplayWindow_HMouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (!(sender is HSmartWindowControlWPF window) || !_windowInfos.TryGetValue(window, out var info))
            {
                return;
            }

            // --- 1. 更新坐标 (这部分逻辑不变) ---
            // 将坐标四舍五入为整数，因为像素索引是整数
            long row = (long)Math.Round(e.Row);
            long col = (long)Math.Round(e.Column);
            info.MouseCoordinates = $"R: {row}, C: {col}";

            // --- 2. 获取当前图像并查询像素值 ---
            HObject currentImage = null;
            try
            {
                // 使用我们之前编写的健壮的 GetImageFromActiveWindow 方法
                // 注意：这里我们只对鼠标所在的活动窗口查询像素值，以优化性能
                if (window == _activeDisplayWindow)
                {
                    currentImage = GetImageFromActiveWindow();
                }

                if (currentImage != null && currentImage.IsInitialized() && currentImage.CountObj() > 0)
                {
                    // 检查坐标是否在图像范围内
                    HOperatorSet.GetImageSize(currentImage, out HTuple width, out HTuple height);
                    if (row >= 0 && row < height && col >= 0 && col < width)
                    {
                        // 查询像素值
                        HOperatorSet.GetGrayval(currentImage, row, col, out HTuple grayval);

                        // 判断图像通道数来决定如何显示
                        if (grayval.Length == 1) // 单通道（灰度）图像
                        {
                            info.PixelValue = $"Gray: {grayval.I}";
                        }
                        else if (grayval.Length == 3) // 三通道（彩色）图像
                        {
                            info.PixelValue = $"R:{grayval[0].I} G:{grayval[1].I} B:{grayval[2].I}";
                        }
                        else // 其他通道数
                        {
                            info.PixelValue = "Val: -";
                        }
                    }
                    else
                    {
                        // 鼠标在图像范围外
                        info.PixelValue = "Val: Out of bounds";
                    }
                }
                else
                {
                    // 当前窗口没有图像
                    info.PixelValue = "Val: -";
                }
            }
            catch (HalconException)
            {
                // 在快速移动或图像更新时可能发生异常，静默处理
                info.PixelValue = "Val: Error";
            }
            finally
            {
                // 确保释放从 GetImageFromActiveWindow 获取的图像拷贝
                currentImage?.Dispose();
            }

            // --- 3. 触发UI重绘 ---
            RefreshInfoWindowAdorner(window);
        }

        #endregion

        #region 相机与图像管理

        /// <summary>
        /// 枚举所有连接到PC的、受支持的相机设备（海康和Halcon MVision）。
        /// </summary>
        /// <returns>一个包含所有被发现设备信息的列表。</returns>
        public List<DeviceInfo> GetFoundDevices()
        {
            var foundDevices = new List<DeviceInfo>();
            var foundSerialNumbers = new HashSet<string>();

            // --- 使用海康SDK查找 ---
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

            // --- 使用Halcon的MVision接口查找设备 ---
            try
            {
                HOperatorSet.InfoFramegrabber("MVision", "device", out _, out HTuple halconDeviceList);
                if (halconDeviceList != null && halconDeviceList.Length > 0)
                {
                    foreach (string deviceId in halconDeviceList.SArr)
                    {
                        string serialNumber = deviceId.Split(' ').LastOrDefault()?.Trim('\'');
                        if (!string.IsNullOrEmpty(serialNumber) && !foundSerialNumbers.Contains(serialNumber))
                        {
                            foundDevices.Add(new DeviceInfo
                            {
                                DisplayName = $"[HAL] {deviceId}",
                                UniqueID = deviceId,
                                SdkType = CameraSdkType.HalconMVision
                            });
                        }
                    }
                }
            }
            catch (HalconException) { /* 忽略Halcon查找时可能发生的异常 */ }
            return foundDevices;
        }

        /// <summary>
        /// 打开指定的相机设备，并将其绑定到一个可用的显示窗口。
        /// </summary>
        /// <param name="selectedDevice">要打开的设备信息。</param>
        /// <returns>一个元组，包含操作是否成功(bool)和相应的提示信息(string)。</returns>
        public (bool Success, string Message) OpenDevice(DeviceInfo selectedDevice)
        {
            if (selectedDevice == null)
            {
                return (false, "未选择任何有效设备。");
            }
            if (openCameras.ContainsKey(selectedDevice.UniqueID))
            {
                return (false, $"设备 {selectedDevice.UniqueID} 已经打开了。");
            }
            if (openCameras.Count >= 4)
            {
                return (false, "最多只能打开4个设备。");
            }
            HSmartWindowControlWPF targetWindow = null;

            // 新的窗口选择逻辑
            // 优先级1: 尝试使用当前激活的窗口，前提是它没被占用
            if (_activeDisplayWindow != null && !(_activeDisplayWindow.Tag is string))
            {
                targetWindow = _activeDisplayWindow;
            }
            // 优先级2: 如果活动窗口已被占用，则查找第一个可用的窗口
            else
            {
                targetWindow = displayWindows.FirstOrDefault(w => w.Tag == null || w.Tag is HObject);
            }

            if (targetWindow == null) return (false, "没有可用的显示窗口了。");

            if (targetWindow.Tag is HObject oldImage)
            {
                oldImage.Dispose();
                targetWindow.HalconWindow.ClearWindow();
                targetWindow.Tag = null; // 清理Tag
            }

            ICameraDevice newCamera = (selectedDevice.SdkType == CameraSdkType.Hikvision)
                ? (ICameraDevice)new HikvisionCameraDevice(selectedDevice, targetWindow)
                : new HalconCameraDevice(selectedDevice.UniqueID, targetWindow);

            try
            {
                if (newCamera.Open())
                {
                    int windowIndex = displayWindows.IndexOf(targetWindow);
                    openCameras.Add(selectedDevice.UniqueID, newCamera);
                    targetWindow.Tag = selectedDevice.UniqueID; // 使用 UniqueID 标记窗口被相机占用
                    CameraListChanged?.Invoke(this, EventArgs.Empty);
                    // 调用新的更新方法，并安全地传入图像以获取初始分辨率
                    // 使用 using 语句确保临时拷贝的图像对象在使用后被立即释放，防止内存泄漏
                    using (HObject initialImage = newCamera.GetCurrentImage()?.CopyObj(1, -1))
                    {
                        UpdateWindowInfoOnSourceChanged(targetWindow, selectedDevice.UniqueID, initialImage);
                    }

                    return (true, $"设备 {selectedDevice.DisplayName} 打开成功，已绑定到窗口 {windowIndex + 1}。");
                }
                // newCamera.Open() 返回 false 的情况（虽然现在不太可能，因为我们改为抛出异常）
                return (false, $"打开设备 {selectedDevice.DisplayName} 失败。");
            }
            catch (Exception ex)
            {
                // 捕获从 Open() 抛出的异常，并将其作为返回消息
                return (false, ex.Message);
            }
        }
        /// <summary>
        /// 关闭指定的相机设备并释放相关资源。
        /// </summary>
        public (bool Success, string Message) CloseDevice(DeviceInfo selectedDevice)
        {

            if (selectedDevice == null)
            {
                return (false, "请选择一个要关闭的设备。");
            }
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice cameraToClose))
            {
                var windowToUpdate = cameraToClose.DisplayWindow; // 先保存窗口引用

                // 在关闭相机前，检查是否需要清除该相机窗口的ROI
                if (cameraToClose.DisplayWindow == _drawingObjectHost || (cameraToClose.DisplayWindow == _activeDisplayWindow && _isPaintingMode))
                {
                    ClearActiveRoi();
                }

                cameraToClose.DisplayWindow.Tag = null;
                cameraToClose.Close();
                openCameras.Remove(selectedDevice.UniqueID);
                CameraListChanged?.Invoke(this, EventArgs.Empty);
                // 更新已变为空闲的窗口
                UpdateWindowInfoOnSourceChanged(windowToUpdate, "空闲");


                return (true, $"设备 {selectedDevice.DisplayName} 已成功关闭。");
            }
            return (false, $"设备 {selectedDevice.DisplayName} 并未打开，无需关闭。");

        }

        /// <summary>
        /// 供子窗口（CameraManagementWindow）调用的方法，
        /// 用于在子窗口关闭时通知主窗口，以便主窗口可以重置实例跟踪字段。
        /// </summary>
        public void NotifyCameraManagementWindowClosed()
        {
            cameraManagementWindow = null;
        }

        /// <summary>
        /// 辅助方法：从海康设备信息结构体中提取序列号。
        /// </summary>
        private string GetHikSerialNumber(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                return ((MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO))).chSerialNumber;
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
                return ((MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO))).chSerialNumber;
            return null;
        }

        /// <summary>
        /// 辅助方法：从海康设备信息结构体中提取用户自定义名称或型号名称。
        /// </summary>
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
        /// 根据当前活动窗口获取其对应的相机对象实例。
        /// </summary>
        /// <returns>如果找到则返回ICameraDevice实例，否则返回null并更新状态栏。</returns>
        private ICameraDevice GetCameraForActiveWindow()
        {
            if (_activeDisplayWindow == null)
            {
                UpdateStatus("操作失败：没有选中的活动窗口。", true);
                return null;
            }
            // 通过遍历已打开的相机列表，查找哪个相机的DisplayWindow与当前活动窗口匹配
            // 这是比使用Tag属性更可靠的方式
            foreach (var camera in openCameras.Values)
            {
                if (camera.DisplayWindow == _activeDisplayWindow)
                {
                    return camera;
                }
            }
            // 如果遍历完都找不到，说明这个窗口确实没有连接相机
            UpdateStatus("提示：当前活动窗口没有连接相机。", true);
            return null;
        }

        /// <summary>
        /// 健壮的辅助方法，用于获取活动窗口当前应该显示的图像。
        /// </summary>
        /// <returns>返回图像的拷贝，使用后需要释放。</returns>
        private HObject GetImageFromActiveWindow()
        {
            if (_activeDisplayWindow == null) return null;

            // 逻辑1: 检查是否有相机连接到此窗口
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == _activeDisplayWindow);
            if (camera != null)
            {
                HObject camImage = camera.GetCurrentImage();
                return camImage?.CopyObj(1, -1);
            }

            // 逻辑2: 如果没有相机，检查Tag是否为本地图像
            if (_activeDisplayWindow.Tag is HObject localImage)
            {
                return localImage.CopyObj(1, -1);
            }

            return null;
        }

        /// <summary>
        /// 重新显示活动窗口的背景图像，清除所有图形叠加。
        /// </summary>
        private void RefreshActiveWindowDisplay()
        {
            if (_activeDisplayWindow == null) return;

            using (HObject image = GetImageFromActiveWindow())
            {
                if (image != null && image.IsInitialized())
                {
                    _activeDisplayWindow.HalconWindow.DispObj(image);
                }
                else
                {
                    _activeDisplayWindow.HalconWindow.ClearWindow();
                }
            }
        }

        #endregion

        #region 标准交互式ROI (HDrawingObject)

        /// <summary>
        /// 根据选择的类型，在活动窗口上创建并附加一个交互式ROI。
        /// </summary>
        private void CreateDrawingObject(string selectedType)
        {
            HObject image = GetImageFromActiveWindow();
            if (image == null)
            {
                UpdateStatus("活动窗口中没有图像，无法创建ROI。", true);
                return;
            }

            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            image.Dispose();

            try
            {
                // 根据字符串选择确定ROI类型并直接创建 _drawingObject 实例
                switch (selectedType)
                {
                    case "矩形 (Rectangle)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "带角度矩形 (Rectangle2)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE2, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 4);
                        break;
                    case "圆形 (Circle)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, height.D / 2, width.D / 2, Math.Min(width.D, height.D) / 4);
                        break;
                    case "椭圆 (Ellipse)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.ELLIPSE, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 8);
                        break;
                    case "直线 (Line)":
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "自由轮廓 (Contour)":
                        HTuple initialRows = new HTuple(new double[] { height.D / 4, height.D / 4, height.D * 0.75, height.D * 0.75 });
                        HTuple initialCols = new HTuple(new double[] { width.D / 4, width.D * 0.75, width.D * 0.75, width.D / 4 });
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.XLD_CONTOUR, initialRows, initialCols);
                        break;
                    default:
                        // 如果类型不匹配，直接退出
                        return;
                }

                // 附加到活动窗口
                _drawingObjectHost = _activeDisplayWindow;
                _drawingObjectHost.HalconWindow.AttachDrawingObjectToWindow(_drawingObject);

                // 订阅交互事件
                _drawingObject.OnDrag(OnRoiUpdate);
                _drawingObject.OnResize(OnRoiUpdate);
                _drawingObject.OnSelect(OnRoiUpdate);

                // 先显示Adorner
                ShowRoiAdorner();
                // 再调用中央处理器来更新所有UI
                ProcessRoiUpdate();

                // 在ROI创建成功后，立即启用保存按钮
                SaveRoiImageButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                // 捕获并显示更详细的错误信息
                UpdateStatus($"创建ROI失败: {ex.Message}", true);
                // 如果创建失败，确保清理可能已创建的对象
                if (_drawingObject != null)
                {
                    _drawingObject.Dispose();
                    _drawingObject = null;
                }
            }
        }

        /// <summary>
        /// 当ROI被拖动或缩放时的回调函数。
        /// </summary>
        private void OnRoiUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 首先，检查当前操作的ROI对象是否存在且有效
            if (dobj == null || dobj.ID == -1) return;

            // 获取ROI的类型
            string roiType;
            try
            {
                roiType = dobj.GetDrawingObjectParams("type").S;
            }
            catch (HalconException)
            {
                // 在极少数情况下，如果连获取类型都失败，则直接返回
                return;
            }

            // --- 智能判断 ---
            if (roiType == "xld_contour")
            {
                // 【策略1：对于自由轮廓】
                // 使用防抖计时器。无论事件触发多频繁，我们都只是重置计时器。
                // 这样可以避免大量异常，因为真正的更新只在拖动停止后执行一次。
                _roiUpdateDebounceTimer.Stop();
                _roiUpdateDebounceTimer.Start();
            }
            else
            {
                // 【策略2：对于所有其他简单ROI（矩形、圆形等）】
                // 立即在UI线程上执行更新，以保证参数的实时反馈。
                Dispatcher.Invoke(() =>
                {
                    ProcessRoiUpdate();
                    SaveRoiImageButton.IsEnabled = true;
                });
            }
        }

        /// <summary>
        /// 处理ROI更新的中央方法。
        /// 它负责获取ROI的最新参数，并触发状态栏和浮动Adorner的更新。
        /// </summary>
        private void ProcessRoiUpdate()
        {
            if (_drawingObject == null || _drawingObject.ID == -1) return;

            var parameters = new Dictionary<string, double>();
            string roiType = _drawingObject.GetDrawingObjectParams("type").S;

            try
            {
                // 根据类型获取参数字典
                switch (roiType)
                {
                    case "rectangle1":
                        parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                        parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                        parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                        parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
                        break;
                    case "rectangle2":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")); // 保持弧度
                        parameters["length1"] = _drawingObject.GetDrawingObjectParams("length1");
                        parameters["length2"] = _drawingObject.GetDrawingObjectParams("length2");
                        break;
                    case "circle":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["radius"] = _drawingObject.GetDrawingObjectParams("radius");
                        break;
                    case "ellipse":
                        parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                        parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                        parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")); // 保持弧度
                        parameters["radius1"] = _drawingObject.GetDrawingObjectParams("radius1");
                        parameters["radius2"] = _drawingObject.GetDrawingObjectParams("radius2");
                        break;
                    case "line":
                        parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                        parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                        parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                        parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
                        break;
                    case "xld_contour":
                        // 对于自由轮廓，我们不需要参数，但需要清空UI
                        _lastRoiArgs = null;
                        UpdateAdornerPosition(null);
                        UpdateParameterBar();
                        return;
                }

                // 使用参数字典创建一个统一的数据对象
                using (HObject iconic = _drawingObject.GetDrawingObjectIconic())
                {
                    _lastRoiArgs = new RoiUpdatedEventArgs(parameters, iconic.CopyObj(1, -1));
                }

                // 使用这个统一的数据对象，更新Adorner和状态栏
                RefreshAdorner();
                UpdateParameterBar();
            }
            catch (HalconException) { /* 忽略小错误 */ }
        }
        
        /// <summary>
        /// 防抖计时器的Tick事件，用于处理自由轮廓ROI的更新。
        /// </summary>
        private void RoiUpdateDebounceTimer_Tick(object sender, EventArgs e)
        {
            // 计时器触发后，首先将它停止，因为它是一次性的
            _roiUpdateDebounceTimer.Stop();

            // 在这里执行我们真正的、重量级的更新逻辑
            ProcessRoiUpdate();
            SaveRoiImageButton.IsEnabled = true;
        }

        /// <summary>
        /// 当参数控件的值被用户修改时触发，用于反向更新ROI对象。
        /// </summary>
        private void OnParameterControlValueChanged(object sender, RoutedEventArgs e)
        {
            // 如果是代码在更新UI，则直接返回，防止无限循环
            if (_isUpdatingFromRoi) return;

            if (_drawingObject != null && _drawingObject.ID != -1)
            {
                try
                {
                    // 收集面板上所有控件的当前值
                    var newParams = new Dictionary<string, double>();
                    foreach (var child in RoiParameterPanel.Children)
                    {
                        if (child is Xceed.Wpf.Toolkit.DoubleUpDown numeric && numeric.Value.HasValue)
                        {
                            newParams[numeric.Tag.ToString()] = numeric.Value.Value;
                        }
                    }

                    // 根据ROI类型，调用正确的SetDrawingObjectParams重载
                    string roiType = _drawingObject.GetDrawingObjectParams("type").S;
                    switch (roiType)
                    {
                        case "rectangle1":
                            _drawingObject.SetDrawingObjectParams(new HTuple("row1", "column1", "row2", "column2"),
                                new HTuple(newParams["row1"], newParams["column1"], newParams["row2"], newParams["column2"]));
                            break;
                        case "rectangle2":
                            _drawingObject.SetDrawingObjectParams(new HTuple("row", "column", "phi", "length1", "length2"),
                                new HTuple(newParams["row"], newParams["column"], newParams["phi"] * Math.PI / 180, newParams["length1"], newParams["length2"]));
                            break;
                        case "circle":
                            _drawingObject.SetDrawingObjectParams(new HTuple("row", "column", "radius"),
                               new HTuple(newParams["row"], newParams["column"], newParams["radius"]));
                            break;
                        case "ellipse":
                            _drawingObject.SetDrawingObjectParams(new HTuple("row", "column", "phi", "radius1", "radius2"),
                               new HTuple(newParams["row"], newParams["column"], newParams["phi"] * Math.PI / 180, newParams["radius1"], newParams["radius2"]));
                            break;
                        case "line":
                            _drawingObject.SetDrawingObjectParams(new HTuple("row1", "column1", "row2", "column2"),
                               new HTuple(newParams["row1"], newParams["column1"], newParams["row2"], newParams["column2"]));
                            break;
                    }

                    // 手动重新生成缓存数据 _lastRoiArgs
                    using (HObject iconic = _drawingObject.GetDrawingObjectIconic())
                    {
                        // 我们需要从修改后的ROI对象中重新获取参数来构建EventArgs
                        var currentParameters = new Dictionary<string, double>();
                        foreach (var key in newParams.Keys)
                        {
                            currentParameters[key] = _drawingObject.GetDrawingObjectParams(key).D;
                        }
                        _lastRoiArgs = new RoiUpdatedEventArgs(currentParameters, iconic.CopyObj(1, -1));
                    }

                    // 调用轻量级的 RefreshAdorner 方法，只更新浮动框，不触碰状态栏
                    RefreshAdorner();
                }
                catch (HalconException) { /* 忽略设置过程中的小错误 */ }
            }
        }

        #endregion

        #region 涂抹式ROI (Paint Mode)

        /// <summary>
        /// 启用涂抹绘制模式，初始化相关状态并订阅鼠标事件。
        /// </summary>
        private void EnablePaintMode()
        {
            if (_isPaintingMode) return;
            _isPaintingMode = true;

            _paintedRoi?.Dispose();
            HOperatorSet.GenEmptyRegion(out _paintedRoi);

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown += PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove += PaintRoi_HMouseMove;
            }

            UpdateStatus("涂抹模式已激活：在图像上左键开始绘制，右键结束。", false);
            SaveRoiImageButton.IsEnabled = true; // 允许在绘制过程中保存
            // 显示涂抹式ROI的参数控件
            UpdateParameterBar();
        }

        /// <summary>
        /// 禁用并清理涂抹绘制模式，解绑鼠标事件。
        /// </summary>
        private void DisablePaintMode()
        {
            if (!_isPaintingMode) return;
            _isPaintingMode = false;
            _isPaintingActive = false;

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown -= PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }
            RefreshActiveWindowDisplay();
            // 隐藏涂抹参数控件（因为没有ROI了，会自然隐藏）
            UpdateParameterBar();
        }

        /// <summary>
        /// 涂抹模式下的鼠标按下事件。
        /// </summary>
        private void PaintRoi_HMouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (e.Button == MouseButton.Right)
            {
                FinalizePainting();
                return;
            }

            if (e.Button == MouseButton.Left)
            {
                _isPaintingActive = true;
                _lastPaintRow = e.Row;
                _lastPaintCol = e.Column;
                PaintAtCurrentPosition(e.Row, e.Column);
                UpdateStatus("正在涂抹... 右键单击以完成。", false);
            }
        }
       
        /// <summary>
        /// 涂抹模式下的鼠标移动事件。
        /// </summary>
        private void PaintRoi_HMouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (_isPaintingActive)
            {
                PaintAtCurrentPosition(e.Row, e.Column);
                _lastPaintRow = e.Row;
                _lastPaintCol = e.Column;
            }
        }

        /// <summary>
        /// 涂抹模式的核心绘制逻辑。
        /// </summary>
        private void PaintAtCurrentPosition(double row, double col)
        {
            if (_activeDisplayWindow == null || _paintedRoi == null) return;

            HObject brush = null;
            HObject lineRegion = null;
            HObject newRoi = null;
            HObject backgroundImage = null;
            // 在开始绘制前，保存当前的视图（缩放/平移状态）。
            Rect currentView = _activeDisplayWindow.HImagePart;
            try
            {
                // 计算新的ROI形状
                if (_lastPaintRow > -1 && _lastPaintCol > -1)
                {
                    HOperatorSet.GenRegionLine(out lineRegion, _lastPaintRow, _lastPaintCol, row, col);
                    if (_brushShape == "圆形") HOperatorSet.DilationCircle(lineRegion, out brush, _brushCircleRadius);
                    else HOperatorSet.DilationRectangle1(lineRegion, out brush, _brushRectWidth, _brushRectHeight);
                }
                else
                {
                    if (_brushShape == "圆形") HOperatorSet.GenCircle(out brush, row, col, _brushCircleRadius);
                    else HOperatorSet.GenRectangle1(out brush, row - _brushRectHeight / 2.0, col - _brushRectWidth / 2.0, row + _brushRectHeight / 2.0, col + _brushRectWidth / 2.0);
                }
                HOperatorSet.Union2(_paintedRoi, brush, out newRoi);
                _paintedRoi.Dispose();
                _paintedRoi = newRoi;
                newRoi = null;

                backgroundImage = GetImageFromActiveWindow();
                if (backgroundImage == null || !backgroundImage.IsInitialized()) return;

                // 重新应用之前保存的视图，而不是重置它。
                // 这一步既保证了坐标系被正确设置，又保留了用户的缩放状态。
                _activeDisplayWindow.HImagePart = currentView;

                // 先显示背景
                _activeDisplayWindow.HalconWindow.DispObj(backgroundImage);

                // 2. 再叠加ROI
                _activeDisplayWindow.HalconWindow.SetDraw("fill");
                _activeDisplayWindow.HalconWindow.SetColor("green");
                _activeDisplayWindow.HalconWindow.DispObj(_paintedRoi);
            }
            catch (HalconException) { /* 忽略小错误 */ }
            finally
            {
                brush?.Dispose();
                lineRegion?.Dispose();
                newRoi?.Dispose();
                backgroundImage?.Dispose();
            }
        }

        /// <summary>
        /// 结束并最终确定一次涂抹绘制。
        /// </summary>
        private void FinalizePainting()
        {
            _isPaintingActive = false;
            _isPaintingMode = false;

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.HMouseDown -= PaintRoi_HMouseDown;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }

            UpdateStatus("涂抹绘制已完成。ROI已创建。", false);

            // 同样，在结束时也要用正确的逻辑刷新显示
            RefreshActiveWindowDisplay();

            if (_activeDisplayWindow != null && _paintedRoi != null && _paintedRoi.IsInitialized())
            {
                // 在背景图上把最终的ROI轮廓画出来
                _activeDisplayWindow.HalconWindow.SetColor("green");
                _activeDisplayWindow.HalconWindow.SetDraw("margin");
                _activeDisplayWindow.HalconWindow.DispObj(_paintedRoi);
            }
        }

        #endregion

        #region 通用UI与状态管理

        /// <summary>
        /// 清除当前活动窗口上的所有ROI。
        /// </summary>
        private void ClearActiveRoi()
        {
            // --- 清除标准 HDrawingObject ROI ---
            if (_drawingObject != null && _drawingObject.ID != -1 && _drawingObjectHost != null)
            {
                RemoveRoiAdorner(_drawingObjectHost);
                try
                {
                    _drawingObjectHost.HalconWindow.DetachDrawingObjectFromWindow(_drawingObject);
                }
                catch (HalconException) { /* 忽略分离错误 */ }
                _drawingObject.Dispose();
                _drawingObject = null;
                _drawingObjectHost = null;
            }

            // --- 【核心修正】清除涂抹式ROI ---
            if (_paintedRoi != null)
            {
                _paintedRoi.Dispose();
                _paintedRoi = null;
            }

            // --- 禁用涂抹模式（如果正在进行中）---
            // 这个会处理事件解绑和状态重置
            if (_isPaintingMode)
            {
                DisablePaintMode();
            }

            // --- 统一的收尾工作 ---
            UpdateParameterBar(); // 清理并隐藏参数面板
            SaveRoiImageButton.IsEnabled = false; // 禁用保存按钮，因为没有ROI了
            RefreshActiveWindowDisplay(); // 刷新显示，清除残留图形
        }

        /// <summary>
        /// 更新主窗口状态栏的文本，并在一段时间后自动清除
        /// </summary>
        /// <param name="message">要显示的消息</param>
        /// <param name="isError">消息是否为错误类型（将以红色显示）</param>
        public async void UpdateStatus(string message, bool isError)
        {
            // 确保在UI线程上更新
            await Dispatcher.InvokeAsync(async () =>
            {
                StatusTextBlock.Text = message;
                StatusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Black;

                // 等待7秒
                await Task.Delay(7000);

                // 如果5秒后状态栏文本还是这个消息，就把它清除
                if (StatusTextBlock.Text == message)
                {
                    StatusTextBlock.Text = "准备就绪";
                    StatusTextBlock.Foreground = Brushes.Black;
                }
            });
        }

        /// <summary>
        /// 核心方法：根据当前ROI的状态，动态生成或更新状态栏中的参数控件。
        /// </summary>
        private void UpdateParameterBar()
        {
            // 在操作UI前，先设置标志位，防止触发不必要的事件
            _isUpdatingFromRoi = true;

            RoiParameterPanel.Children.Clear();
            RoiParameterPanel.Visibility = Visibility.Collapsed;

            // --- 情况1：处理涂抹式ROI ---
            if (_isPaintingMode)
            {
                RoiParameterPanel.Children.Add(new Label { Content = "画笔形状:" });
                var shapeCombo = new ComboBox { Width = 80, VerticalContentAlignment = VerticalAlignment.Center };
                shapeCombo.Items.Add("圆形");
                shapeCombo.Items.Add("矩形");
                shapeCombo.SelectedItem = _brushShape;
                // 当形状改变时，重新生成整个参数面板
                shapeCombo.SelectionChanged += (s, e) => {
                    if (shapeCombo.SelectedItem != null)
                    {
                        _brushShape = shapeCombo.SelectedItem.ToString();
                        UpdateParameterBar(); // 重新调用此方法以更新UI
                    }
                };
                RoiParameterPanel.Children.Add(shapeCombo);
                // --- 根据当前选择的形状，创建对应的尺寸编辑器 ---
                if (_brushShape == "圆形")
                {
                    // 为圆形画笔创建半径编辑器
                    RoiParameterPanel.Children.Add(new Label { Content = "半径:", Margin = new Thickness(10, 0, 0, 0) });
                    var sizeNumeric = new Xceed.Wpf.Toolkit.IntegerUpDown
                    {
                        Value = _brushCircleRadius,
                        Minimum = 1,
                        Maximum = 100, // 遵从您的要求，设置最大值
                        Width = 60
                    };
                    sizeNumeric.ValueChanged += (s, e) => {
                        if (sizeNumeric.Value.HasValue)
                            _brushCircleRadius = sizeNumeric.Value.Value;
                    };
                    RoiParameterPanel.Children.Add(sizeNumeric);
                }
                else // "矩形"
                {
                    // 为矩形画笔创建宽度和高度编辑器
                    // 宽度
                    RoiParameterPanel.Children.Add(new Label { Content = "宽度:", Margin = new Thickness(10, 0, 0, 0) });
                    var widthNumeric = new Xceed.Wpf.Toolkit.IntegerUpDown
                    {
                        Value = _brushRectWidth,
                        Minimum = 1,
                        Maximum = 100,
                        Width = 60
                    };
                    widthNumeric.ValueChanged += (s, e) => {
                        if (widthNumeric.Value.HasValue)
                            _brushRectWidth = widthNumeric.Value.Value;
                    };
                    RoiParameterPanel.Children.Add(widthNumeric);

                    // 高度
                    RoiParameterPanel.Children.Add(new Label { Content = "高度:", Margin = new Thickness(10, 0, 0, 0) });
                    var heightNumeric = new Xceed.Wpf.Toolkit.IntegerUpDown
                    {
                        Value = _brushRectHeight,
                        Minimum = 1,
                        Maximum = 100,
                        Width = 60
                    };
                    heightNumeric.ValueChanged += (s, e) => {
                        if (heightNumeric.Value.HasValue)
                            _brushRectHeight = heightNumeric.Value.Value;
                    };
                    RoiParameterPanel.Children.Add(heightNumeric);
                }

                RoiParameterPanel.Visibility = Visibility.Visible;
            }
            // --- 情况2：处理标准的 HDrawingObject ROI ---
            // 修改了判断条件，现在它依赖于缓存的 _lastRoiArgs 是否存在
            else if (_drawingObject != null && _drawingObject.ID != -1 && _lastRoiArgs != null)
            {
                // 直接从 _lastRoiArgs 中获取参数字典，不再自己去查询ROI对象
                var parameters = _lastRoiArgs.Parameters;

                // "自由轮廓"的判断已移至ProcessRoiUpdate, 这里无需处理

                // 遍历参数字典来创建UI控件
                foreach (var kvp in parameters)
                {
                    string paramName = kvp.Key;
                    double paramValue = kvp.Value;

                    // 角度(phi)需要特殊处理（从弧度转为度），所以在这里跳过
                    if (paramName == "phi") continue;

                    RoiParameterPanel.Children.Add(new Label { Content = $"{ParameterTranslator.Translate(paramName)}" });
                    var numeric = new Xceed.Wpf.Toolkit.DoubleUpDown
                    {
                        Value = paramValue, // 直接使用字典中的值
                        Width = 80,
                        Tag = paramName, // 使用Tag来标记这个控件对应哪个参数
                        Increment = 1.0,
                        FormatString = "F2"
                    };
                    numeric.ValueChanged += OnParameterControlValueChanged;
                    RoiParameterPanel.Children.Add(numeric);
                }

                // 单独处理角度 phi
                if (parameters.ContainsKey("phi"))
                {
                    double phiValueRad = parameters["phi"]; // 从字典获取弧度值
                    RoiParameterPanel.Children.Add(new Label { Content = $"{ParameterTranslator.Translate("phi")}" });
                    var numericPhi = new Xceed.Wpf.Toolkit.DoubleUpDown
                    {
                        Value = phiValueRad * 180 / Math.PI, // 转换为度进行显示
                        Width = 80,
                        Tag = "phi",
                        Increment = 1.0,
                        FormatString = "F2"
                    };
                    numericPhi.ValueChanged += OnParameterControlValueChanged;
                    RoiParameterPanel.Children.Add(numericPhi);
                }

                RoiParameterPanel.Visibility = Visibility.Visible;
            }

            // 所有UI操作完成后，解除标志位
            _isUpdatingFromRoi = false;
        }

        /// <summary>
        /// 当图像源变化时（如打开相机、加载图像），更新窗口左下角Adorner的基础信息。
        /// </summary>
        private void UpdateWindowInfoOnSourceChanged(HSmartWindowControlWPF window, string sourceName, HObject image = null)
        {
            if (window == null || !_windowInfos.ContainsKey(window)) return;
            var info = _windowInfos[window];

            info.SourceName = sourceName;

            if (image != null && image.IsInitialized() && image.CountObj() > 0)
            {
                try
                {
                    HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                    info.Resolution = $"{width.I} x {height.I}";
                    // 【重要】存储原始尺寸
                    info.OriginalImageSize = new Size(width.D, height.D);
                }
                catch (HalconException)
                {
                    info.Resolution = "N/A";
                    info.OriginalImageSize = new Size(0, 0);
                }
            }
            else
            {
                info.Resolution = "N/A";
                info.OriginalImageSize = new Size(0, 0);
            }

            // 基础信息更新后，立即更新一次缩放并刷新UI
            UpdateWindowZoom(window);
        }

        /// <summary>
        /// 仅计算并更新指定窗口的缩放比例。
        /// </summary>
        private void UpdateWindowZoom(HSmartWindowControlWPF window)
        {
            if (window == null || !_windowInfos.ContainsKey(window)) return;
            var info = _windowInfos[window];
            // 检查是否存在有效的原始图像尺寸
            if (info.OriginalImageSize.Width > 0)
            {
                // 获取HSmartWindowControlWPF当前显示的图像部分（视图）
                Rect imagePart = window.HImagePart;
                if (imagePart.Width > 0)
                {
                    // 核心计算公式 
                    double zoom = info.OriginalImageSize.Width / imagePart.Width;
                    //Console.WriteLine("OriginalImageSize:{0}    imagePart:{1}", info.OriginalImageSize.Width, imagePart.Width);
                    
                    // 格式化为百分比字符串并更新数据模型
                    info.ZoomFactor = $"{zoom:P0}";// P0格式化为无小数的百分比，例如 1.25 -> "125 %"

                }
                else
                {
                    info.ZoomFactor = "N/A";
                }
            }
            else
            {
                info.ZoomFactor = "100%"; // 如果没有图像，默认100%
            }

            // 触发Adorner重绘
            RefreshInfoWindowAdorner(window);
        }

        /// <summary>
        /// 仅负责触发Adorner的重绘，从数据模型读取数据。
        /// </summary>
        private void RefreshInfoWindowAdorner(HSmartWindowControlWPF window)
        {
            if (_infoAdorners.TryGetValue(window, out var adorner) && _windowInfos.TryGetValue(window, out var info))
            {
                adorner.Update(info);
            }
        }
       
        /// <summary>
        /// 创建并显示Adorner，同时开始监听窗口视图的变化。
        /// </summary>
        private void ShowRoiAdorner()
        {
            if (_activeDisplayWindow != null)
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(_activeDisplayWindow);
                if (adornerLayer != null)
                {
                    // 如果已存在，先移除旧的
                    if (_roiAdorner != null)
                    {
                        adornerLayer.Remove(_roiAdorner);
                        DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                            .RemoveValueChanged(_activeDisplayWindow, OnDisplayWindowViewChanged);
                    }

                    _roiAdorner = new RoiAdorner(_activeDisplayWindow);
                    adornerLayer.Add(_roiAdorner);

                    // 监听 HImagePart 属性变化，以便在缩放/平移时更新Adorner位置
                    DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                        .AddValueChanged(_activeDisplayWindow, OnDisplayWindowViewChanged);
                }
            }
        }

        /// <summary>
        /// 从指定的窗口移除Adorner，并停止监听该窗口的视图变化。
        /// </summary>
        /// <param name="window">要移除Adorner的WPF窗口控件。</param>
        private void RemoveRoiAdorner(HSmartWindowControlWPF window)
        {
            if (window != null && _roiAdorner != null)
            {
                // 停止监听这个特定窗口的视图变化事件
                DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                    .RemoveValueChanged(window, OnDisplayWindowViewChanged);

                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(window);
                adornerLayer?.Remove(_roiAdorner); // 从这个窗口的Adorner层移除
                // 重置Adorner实例和缓存
                _roiAdorner = null;
                _lastRoiArgs = null;
            }
        }

        /// <summary>
        /// 核心辅助方法：计算并更新Adorner在屏幕上的位置和显示的文本。
        /// </summary>
        private void UpdateAdornerPosition(RoiUpdatedEventArgs e)
        {
            if (_roiAdorner != null && e != null && e.Position.HasValue)
            {
                var hWindow = _activeDisplayWindow.HalconWindow;
                hWindow.ConvertCoordinatesImageToWindow(e.Position.Value.Y, e.Position.Value.X, out double windowY, out double windowX);

                _roiAdorner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double adornerHeight = _roiAdorner.DesiredSize.Height;

                Point newPosition = new Point(windowX + 15, windowY - adornerHeight / 2);
                _roiAdorner.Update(e.ParametersAsString, newPosition);
            }
            else if (_roiAdorner != null)
            {
                _roiAdorner.Update("", new Point(0, 0)); // 隐藏
            }
        }

        /// <summary>
        /// 轻量级更新方法，仅用于刷新浮动Adorner的位置和内容。
        /// </summary>
        private void RefreshAdorner()
        {
            // 直接使用缓存的 _lastRoiArgs 来更新 Adorner
            if (_roiAdorner != null && _lastRoiArgs != null)
            {
                UpdateAdornerPosition(_lastRoiArgs);
            }
        }

        #endregion

        /// <summary>
        /// 【调试用】一个封装的图像显示方法 (当前代码中未使用，可移除或保留)
        /// </summary>
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
        /// 主窗口关闭时触发，用于安全地释放所有资源。
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            // 【新增】在所有操作之前，取消事件订阅，防止关闭过程中触发UI更新
            foreach (var window in displayWindows)
            {
                DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                    .RemoveValueChanged(window, OnDisplayWindowViewChanged);
                window.SizeChanged -= DisplayWindow_SizeChanged;
            }
            // 关闭相机管理窗口（如果它还开着）
            cameraManagementWindow?.Close();

            // 清理所有ROI资源
            ClearActiveRoi();

            // 再关闭所有相机
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
        }

        
    }
}