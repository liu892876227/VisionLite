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
using VisionLite.Communication;

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

        #region 通讯相关字段
        // 管理所有通讯实例
        internal Dictionary<string, ICommunication> communications = new Dictionary<string, ICommunication>();
        private SimpleCommunicationWindow communicationWindow = null;
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

        /// <summary>
        /// 存储每个显示窗口对应的浮动工具栏Adorner实例。
        /// </summary>
        private Dictionary<HSmartWindowControlWPF, WindowToolbarAdorner> _captureAdorners;

        #endregion

        #region ROI 和涂抹绘制相关字段

        /// <summary>
        /// 管理每个显示窗口各自的ROI状态。
        /// Key: HSmartWindowControlWPF 实例
        /// Value: 对应的 WindowRoiState 数据对象
        /// </summary>
        private Dictionary<HSmartWindowControlWPF, WindowRoiState> _windowRoiStates;

        /// <summary>
        /// 标记当前是否处于涂抹绘制模式。
        /// </summary>
        private bool _isPaintingMode = false;

        /// <summary>
        /// 标记当前是否处于橡皮擦模式。
        /// </summary>
        private bool _isErasingMode = false;

        /// <summary>
        /// 标记在涂抹或橡皮擦模式下，鼠标左键是否按下以激活绘制或激活擦除。
        /// </summary>
        private bool _isPaintingActive = false;

        // --- 用于涂抹绘制的辅助变量 ---
        private HTuple _lastPaintRow = -1;
        private HTuple _lastPaintCol = -1;
        private string _brushShape = "圆形";
        private int _brushCircleRadius = 40;
        private int _brushRectWidth = 40;
        private int _brushRectHeight = 40;

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
            _captureAdorners = new Dictionary<HSmartWindowControlWPF, WindowToolbarAdorner>();
            _windowRoiStates = new Dictionary<HSmartWindowControlWPF, WindowRoiState>(); // 初始化ROI状态字典 

            // 为每个窗口添加鼠标点击事件处理器
            foreach (var window in displayWindows)
            {
                window.PreviewMouseDown += DisplayWindow_PreviewMouseDown;
                // 为每个窗口创建一个数据模型
                _windowInfos.Add(window, new WindowInfo());
                // 为每个窗口创建一个空的ROI状态实例
                _windowRoiStates.Add(window, new WindowRoiState());
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
                        // 创建并附加左下角的信息Adorner
                        var infoAdorner = new InfoWindowAdorner(window);
                        adornerLayer.Add(infoAdorner);
                        _infoAdorners.Add(window, infoAdorner);

                        // 创建并附加左上角的工具栏Adorner
                        var captureAdorner = new WindowToolbarAdorner(window, this);
                        adornerLayer.Add(captureAdorner);
                        _captureAdorners.Add(window, captureAdorner);
                    }

                    // 初始化显示
                    UpdateWindowInfoOnSourceChanged(window, "空闲");
                }

                SetActiveDisplayWindow(HSmart1);
                
                // 通讯功能使用简化架构，无需加载保存的配置
            };
        }

        #region 工具栏按钮事件处理

        /// <summary>
        /// 打开或激活相机管理窗口（单例模式）。
        /// 此方法是公共的，以便被各个窗口的Adorner调用。
        /// </summary>
        public void OpenCameraManagementWindow(HSmartWindowControlWPF targetWindow)
        {
            if (cameraManagementWindow != null)
            {
                if (cameraManagementWindow.WindowState == WindowState.Minimized)
                {
                    cameraManagementWindow.WindowState = WindowState.Normal;
                }
                cameraManagementWindow.SetTargetWindow(targetWindow);
                cameraManagementWindow.Activate();
            }
            else
            {
                cameraManagementWindow = new CameraManagementWindow(this, targetWindow);
                cameraManagementWindow.Show();
            }
        }

        private void SingleCaptureToolbarButton_Click(object sender, RoutedEventArgs e) => TriggerSingleCapture(_activeDisplayWindow);
        private void ContinueCaptureToolbarButton_Click(object sender, RoutedEventArgs e) => TriggerContinueCapture(_activeDisplayWindow);
        private void StopCaptureToolbarButton_Click(object sender, RoutedEventArgs e) => TriggerStopCapture(_activeDisplayWindow);

        public void TriggerSingleCapture(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;
            // 通过窗口找到对应的相机
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == targetWindow);
            if (camera == null)
            {
                UpdateStatus("提示：目标窗口没有连接相机。", true);
                return;
            }

            try
            {
                if (camera.IsContinuousGrabbing())
                {
                    UpdateStatus("操作冲突：相机正在连续采集中，请先停止。", true);
                    return;
                }
                camera.GrabAndDisplay();
                using (HObject capturedImage = camera.GetCurrentImage()?.CopyObj(1, -1))
                {
                    UpdateWindowInfoOnSourceChanged(targetWindow, camera.DeviceID, capturedImage);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"单次采集失败: {ex.Message}", true);
            }
        }

        public void TriggerContinueCapture(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == targetWindow);
            if (camera == null) return;

            try
            {
                camera.StartContinuousGrab();
            }
            catch (Exception ex)
            {
                UpdateStatus($"开始连续采集失败: {ex.Message}", true);
            }
        }

        public void TriggerStopCapture(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == targetWindow);
            camera?.StopContinuousGrab();
        }


        /// <summary>
        /// “加载图像”按钮的点击事件处理程序
        /// </summary>
        private void LoadImgButtonClick(object sender, RoutedEventArgs e) => TriggerLoadImage(_activeDisplayWindow);


        /// <summary>
        /// 为指定的目标窗口触发加载本地图像的操作。
        /// </summary>
        /// <param name="targetWindow">用户点击了“加载图像”按钮所在的显示窗口。</param>
        public void TriggerLoadImage(HSmartWindowControlWPF targetWindow)
        {
            // 检查目标窗口是否有效
            if (targetWindow == null)
            {
                UpdateStatus("内部错误：目标窗口无效。", true);
                return;
            }

            // 确认目标窗口当前是否空闲（没有被相机占用）
            if (openCameras.Values.Any(c => c.DisplayWindow == targetWindow))
            {
                string deviceId = openCameras.First(kvp => kvp.Value.DisplayWindow == targetWindow).Key;
                UpdateStatus($"此窗口已被相机 '{deviceId}' 占用，无法加载图像。", true);
                return;
            }

            // 在加载新图像前，清除此目标窗口上可能存在的任何类型的ROI
            ClearRoiForWindow(targetWindow);

            // 配置并显示文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "所有支持的图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;" +
                         "|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg;" +
                         "|PNG 图片 (*.png)|*.png;" +
                         "|位图图片 (*.bmp)|*.bmp;" +
                         "|GIF 图片 (*.gif)|*.gif;" +
                         "|所有文件 (*.*)|*.*",
                Title = "为当前窗口选择图片文件",
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            // 处理文件选择结果
            if (openFileDialog.ShowDialog() == true)
            {
                HObject loadedImage = null;
                try
                {
                    // 如果窗口之前有加载过的本地图像，先释放它
                    if (targetWindow.Tag is HObject oldImage)
                    {
                        oldImage.Dispose();
                        targetWindow.Tag = null;
                    }

                    // 使用Halcon加载图像
                    HOperatorSet.ReadImage(out loadedImage, openFileDialog.FileName);

                    // 设置Halcon窗口的视图以适应图像大小
                    HOperatorSet.GetImageSize(loadedImage, out HTuple width, out HTuple height);
                    targetWindow.HImagePart = new Rect(0, 0, width.I, height.I);

                    // 显示图像
                    targetWindow.HalconWindow.ClearWindow();
                    targetWindow.HalconWindow.DispObj(loadedImage);

                    // 将加载的图像对象存储在窗口的Tag属性中，用于后续识别和资源管理
                    targetWindow.Tag = loadedImage;

                    // 更新窗口左下角的信息面板 (Adorner)
                    string fileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                    UpdateWindowInfoOnSourceChanged(targetWindow, fileName, loadedImage);

                    //所有权转移：将局部变量设为null，防止它在finally块中被错误地释放。
                    // 因为它的生命周期现在由 targetWindow.Tag 管理。
                    loadedImage = null;
                }
                catch (Exception ex)
                {
                    UpdateStatus($"加载图像时发生错误: {ex.Message}", true);
                    // 如果发生异常，清理可能已部分加载的图像资源
                    if (targetWindow.Tag is HObject img)
                    {
                        img.Dispose();
                        targetWindow.Tag = null;
                    }
                    // 将窗口信息重置为空闲状态
                    UpdateWindowInfoOnSourceChanged(targetWindow, "空闲");
                }
                finally
                {
                    // 确保在任何异常情况下，未成功赋给Tag的图像对象都能被释放
                    loadedImage?.Dispose();
                }
            }
        }

        /// <summary>
        /// 清除指定窗口上的所有ROI，并更新相关UI。
        /// </summary>
        /// <param name="windowToClear">要清除ROI的窗口。</param>
        private void ClearRoiForWindow(HSmartWindowControlWPF windowToClear)
        {
            if (windowToClear == null || !_windowRoiStates.TryGetValue(windowToClear, out var roiState))
            {
                return;
            }

            // 清理标准ROI
            if (roiState.DrawingObject != null)
            {
                // 只有当要清理的窗口是活动窗口时，才移除 Adorner
                if (windowToClear == _activeDisplayWindow) RemoveRoiAdorner(_activeDisplayWindow);

                try { windowToClear.HalconWindow.DetachDrawingObjectFromWindow(roiState.DrawingObject); }
                catch (HalconException) { }

                roiState.DrawingObject.Dispose();
                roiState.DrawingObject = null;
            }

            // 清理掩膜ROI
            roiState.PaintedRoi?.Dispose();
            roiState.PaintedRoi = null;

            // 如果要清理的窗口是活动窗口，则禁用相关模式并更新UI
            if (windowToClear == _activeDisplayWindow)
            {
                if (_isPaintingMode || _isErasingMode) DisablePaintMode(); // DisablePaintMode会清理两种模式
                UpdateParameterBar();
                if (_captureAdorners.TryGetValue(windowToClear, out var adorner))
                {
                    adorner.SetSaveRoiButtonState(false);
                }
            }
        }

        /// <summary>
        /// 处理来自任意窗口浮动工具栏的ROI工具选择事件。
        /// </summary>
        /// <param name="targetWindow">触发事件的源显示窗口。</param>
        /// <param name="comboBox">触发事件的ComboBox实例。</param>
        public void TriggerRoiToolSelection(HSmartWindowControlWPF targetWindow, ComboBox comboBox)
        {
            // 1. 安全性检查，确保传入的ComboBox有效且用户确实做出了选择
            if (comboBox == null || comboBox.SelectedIndex < 2) // 索引0是图标, 索引1是标题
            {
                return;
            }

            // 2. 【关键步骤】将触发事件的窗口设置为当前的活动窗口。
            // 这样可以确保后续所有的ROI操作都正确地作用于用户正在交互的窗口上。
            SetActiveDisplayWindow(targetWindow);

            // 3. 获取用户选择的工具名称
            var selectedItem = (comboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            try
            {
                // 4. 再次检查（虽然通常是多余的，但更健壮），确保活动窗口有图像可供操作
                using (HObject currentImage = GetImageFromActiveWindow())
                {
                    if (currentImage == null || !currentImage.IsInitialized() || currentImage.CountObj() < 1)
                    {
                        UpdateStatus("当前活动窗口中没有可用的图像，无法创建ROI。", true);
                        return;
                    }
                }

                // 5. 根据选择的工具执行相应的逻辑分支
                switch (selectedItem)
                {
                    case "清除ROI":
                        ClearActiveRoi(); // 清除所有类型的ROI
                        break;

                    case "橡皮擦 (Eraser)":
                        // 橡皮擦是一个“编辑”工具，它需要在现有ROI上工作
                        EnableEraseMode();
                        break;

                    case "掩膜 (Mask)":
                    case "矩形 (Rectangle)":
                    case "带角度矩形 (Rectangle2)":
                    case "圆形 (Circle)":
                    case "椭圆 (Ellipse)":
                    case "直线 (Line)":
                    case "自由轮廓 (Contour)":
                        // 对于所有“创建型”工具，逻辑统一：
                        // a. 首先，清除掉画布上所有旧的ROI
                        ClearActiveRoi();

                        // b. 然后，根据具体类型调用相应的创建方法
                        if (selectedItem == "掩膜 (Mask)")
                        {
                            EnablePaintMode(); // 进入涂抹/掩膜模式
                        }
                        else
                        {
                            CreateDrawingObject(selectedItem); // 创建一个标准的交互式ROI
                        }
                        break;
                }
            }
            finally
            {
                // 6. 操作完成后，无论成功与否，都将ComboBox重置为显示图标的默认状态
                comboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// "保存原图"按钮的点击事件处理。
        /// </summary>
        private void SaveOriginalImageButton_Click(object sender, RoutedEventArgs e) => TriggerSaveImage(_activeDisplayWindow);

        /// <summary>
        /// "保存ROI区域图像"按钮的点击事件处理。
        /// </summary>
        private void SaveRoiImageButton_Click(object sender, RoutedEventArgs e) => TriggerSaveRoi(_activeDisplayWindow);

        /// <summary>
        /// 为指定的目标窗口触发保存原图的操作。
        /// </summary>
        /// <param name="targetWindow">用户点击了“保存原图”按钮所在的显示窗口。</param>
        public void TriggerSaveImage(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            // 1. 获取目标窗口当前显示的图像
            //    我们使用一个新的辅助方法 GetImageFromWindow 来确保获取的是正确窗口的图像
            using (HObject imageToSave = GetImageFromWindow(targetWindow))
            {
                // 2. 检查图像是否有效
                if (imageToSave == null || !imageToSave.IsInitialized())
                {
                    UpdateStatus("目标窗口中没有图像可供保存。", true);
                    return;
                }

                try
                {
                    // 3. 构建保存路径和文件名
                    //    路径: D:\VisionLite图像保存\IMG
                    string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "IMG");
                    Directory.CreateDirectory(savePath); // 确保文件夹存在

                    //    文件名: IMG_年月日_时分秒_毫秒.bmp
                    string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                    string fullPath = System.IO.Path.Combine(savePath, fileName);

                    // 4. 使用Halcon保存图像
                    HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);

                    UpdateStatus($"图像已成功保存到：{fullPath}", false);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"保存失败: {ex.Message}", true);
                }
            } // using 语句会自动调用 imageToSave.Dispose()，确保资源被释放
        }

        /// <summary>
        /// 为指定的目标窗口触发保存ROI区域图像的操作。
        /// </summary>
        /// <param name="targetWindow">用户点击了“保存ROI”按钮所在的显示窗口。</param>
        public void TriggerSaveRoi(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            // 获取目标窗口的背景图像
            HObject sourceImage = GetImageFromWindow(targetWindow);
            if (sourceImage == null || !sourceImage.IsInitialized())
            {
                UpdateStatus("错误: 目标窗口没有背景图像，无法保存ROI。", true);
                return;
            }

            // 获取目标窗口的ROI状态
            if (!_windowRoiStates.TryGetValue(targetWindow, out var roiState))
            {
                sourceImage.Dispose();
                return; 
            }

            HObject regionToSave = null;
            bool isTempRegion = false; // 标记regionToSave是否是临时拷贝，需要手动释放

            HObject imageReduced = null;
            HObject imageCropped = null;

            try
            {
                // 从目标窗口的ROI状态中获取要保存的ROI区域
                // 优先检查是否存在有效的 paintedRoi (掩膜或橡皮擦的结果)
                if (roiState.PaintedRoi != null && roiState.PaintedRoi.IsInitialized() && roiState.PaintedRoi.CountObj() > 0)
                {
                    regionToSave = roiState.PaintedRoi; // 直接引用，不需要释放
                }
                // 否则，再检查是否存在标准的 _drawingObject
                else if (roiState.DrawingObject != null && roiState.DrawingObject.ID != -1)
                {
                    string roiType = roiState.DrawingObject.GetDrawingObjectParams("type").S;
                    if (roiType == "line")
                    {
                        UpdateStatus("错误: 无法保存直线ROI的图像。", true);
                        return; // 直接返回，finally会释放sourceImage
                    }

                    using (HObject iconicObject = roiState.DrawingObject.GetDrawingObjectIconic())
                    {
                        if (iconicObject.GetObjClass() == "region")
                        {
                            regionToSave = iconicObject.CopyObj(1, -1);
                        }
                        else // 认为是 xld_cont
                        {
                            HOperatorSet.GenRegionContourXld(iconicObject, out regionToSave, "filled");
                        }
                    }
                    isTempRegion = true; // 标记这是一个新生成的拷贝，需要在finally中释放
                }


                // 检查是否成功获取到ROI区域
                if (regionToSave == null || regionToSave.CountObj() == 0)
                {
                    UpdateStatus("错误: 目标窗口上没有有效的ROI可以保存。", true);
                    return;
                }

                // 执行裁剪和保存逻辑
                HOperatorSet.GenEmptyObj(out imageReduced);
                HOperatorSet.GenEmptyObj(out imageCropped);

                HOperatorSet.ReduceDomain(sourceImage, regionToSave, out imageReduced);
                HOperatorSet.CropDomain(imageReduced, out imageCropped);

                string savePath = System.IO.Path.Combine("D:\\", "VisionLite图像保存", "ROI");
                Directory.CreateDirectory(savePath);
                string fileName = $"ROI_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                string fullPath = System.IO.Path.Combine(savePath, fileName);
                HOperatorSet.WriteImage(imageCropped, "bmp", 0, fullPath);

                UpdateStatus($"ROI图像已成功保存到：{fullPath}", false);
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存ROI失败: {ex.Message}", true);
            }
            finally
            {
                // 释放所有在此方法中创建或获取的资源
                sourceImage?.Dispose();
                if (isTempRegion)
                {
                    regionToSave?.Dispose();
                }
                imageReduced?.Dispose();
                imageCropped?.Dispose();
            }
        }

        // 获取指定窗口图像的辅助方法
        private HObject GetImageFromWindow(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return null;
            var camera = openCameras.Values.FirstOrDefault(c => c.DisplayWindow == targetWindow);
            if (camera != null)
            {
                return camera.GetCurrentImage()?.CopyObj(1, -1);
            }
            if (targetWindow.Tag is HObject localImage)
            {
                return localImage.CopyObj(1, -1);
            }
            return null;
        }

        #endregion


        #region 通讯窗口管理
        private void CommunicationButton_Click(object sender, RoutedEventArgs e)
        {
            if (communicationWindow == null || !communicationWindow.IsVisible)
            {
                communicationWindow = new SimpleCommunicationWindow(this);
                communicationWindow.Closed += (s, args) => communicationWindow = null;
                communicationWindow.Show();
            }
            else
            {
                communicationWindow.Activate();
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
            if (newActiveWindow == null || newActiveWindow == _activeDisplayWindow)
            {
                return; // 如果点击的窗口无效或已经是活动窗口，则不执行任何操作
            }

            HSmartWindowControlWPF oldActiveWindow = _activeDisplayWindow;
            // 在切换窗口前，终结旧窗口的编辑状态
            // 如果旧的活动窗口正处于绘画或擦除模式，则认为该操作已完成。
            if (oldActiveWindow != null && (_isPaintingMode || _isErasingMode))
            {
                // 调用 FinalizePainting 来解绑事件，重置状态
                FinalizePainting();
            }
            _activeDisplayWindow = newActiveWindow; // 更新活动窗口的引用

            // 通过改变颜色而不是厚度来高亮，这不会触发子元素的重新布局
            Border1.BorderBrush = (newActiveWindow == HSmart1) ? Brushes.DodgerBlue : Brushes.Gray;
            Border2.BorderBrush = (newActiveWindow == HSmart2) ? Brushes.DodgerBlue : Brushes.Gray;
            Border3.BorderBrush = (newActiveWindow == HSmart3) ? Brushes.DodgerBlue : Brushes.Gray;
            Border4.BorderBrush = (newActiveWindow == HSmart4) ? Brushes.DodgerBlue : Brushes.Gray;

            // 将所有 BorderThickness 硬编码为 1，不再改变它
            Border1.BorderThickness = new Thickness(1);
            Border2.BorderThickness = new Thickness(1);
            Border3.BorderThickness = new Thickness(1);
            Border4.BorderThickness = new Thickness(1);

            // 处理ROI Adorner（浮动参数框）的“迁移”
            // 从旧的活动窗口移除 Adorner
            if (oldActiveWindow != null)
            {
                // 调用带参数的RemoveRoiAdorner
                RemoveRoiAdorner(oldActiveWindow);
            }

            // 如果新的活动窗口有标准ROI，则为其重新创建并显示 Adorner
            if (_windowRoiStates.TryGetValue(newActiveWindow, out var newRoiState) && newRoiState.DrawingObject != null && newRoiState.DrawingObject.ID != -1)
            {
                ShowRoiAdorner(newActiveWindow);
                // 【重要】切换回来后，需要立即刷新一次Adorner的位置和内容
                RefreshAdorner();
            }

            // 更新状态栏上的参数面板
            UpdateParameterBar(); // UpdateParameterBar 总是反映 _activeDisplayWindow 的状态

            
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

            // 确保事件来自于当前活动的窗口，并且该窗口上存在一个标准ROI
            if (sender == _activeDisplayWindow)
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
        public (bool Success, string Message) OpenDevice(DeviceInfo selectedDevice, HSmartWindowControlWPF targetWindow)
        {
            if (selectedDevice == null)
            {
                return (false, "未选择任何有效设备。");
            }
            if (targetWindow == null)
            {
                return (false, "内部错误：未指定目标窗口。");
            }
            // 检查设备是否已在其他窗口打开
            if (openCameras.ContainsKey(selectedDevice.UniqueID))
            {
                return (false, $"设备 {selectedDevice.UniqueID} 已经打开了。");
            }
            // 检查目标窗口是否已被其他相机占用
            if (targetWindow.Tag is string deviceId && deviceId != selectedDevice.UniqueID)
            {
                return (false, $"操作失败：目标窗口已被相机 '{deviceId}' 占用。");
            }

            // --- 准备目标窗口 ---
            // 如果目标窗口当前显示的是本地图像，先清理掉
            if (targetWindow.Tag is HObject oldImage)
            {
                oldImage.Dispose();
                targetWindow.HalconWindow.ClearWindow();
                targetWindow.Tag = null;
            }
            // 清理目标窗口上可能残留的ROI
            ClearRoiForWindow(targetWindow);
            // --- 创建并打开相机实例 ---
            // 根据SDK类型创建对应的相机设备实例
            ICameraDevice newCamera = (selectedDevice.SdkType == CameraSdkType.Hikvision)
                ? (ICameraDevice)new HikvisionCameraDevice(selectedDevice, targetWindow)
                : new HalconCameraDevice(selectedDevice.UniqueID, targetWindow);

            try
            {
                if (newCamera.Open())
                {
                    int windowIndex = displayWindows.IndexOf(targetWindow) + 1;
                    openCameras.Add(selectedDevice.UniqueID, newCamera);
                    targetWindow.Tag = selectedDevice.UniqueID; // 使用 UniqueID 标记窗口被相机占用
                    CameraListChanged?.Invoke(this, EventArgs.Empty);
                    // 调用新的更新方法，并安全地传入图像以获取初始分辨率
                    // 使用 using 语句确保临时拷贝的图像对象在使用后被立即释放，防止内存泄漏
                    using (HObject initialImage = newCamera.GetCurrentImage()?.CopyObj(1, -1))
                    {
                        UpdateWindowInfoOnSourceChanged(targetWindow, selectedDevice.DisplayName, initialImage);
                    }
                   
                    return (true, $"设备 {selectedDevice.DisplayName} 打开成功，已绑定到窗口 {windowIndex}。");
                }
                else
                {
                    // 理论上，如果Open()内部有错误会抛出异常，这部分代码可能不会执行
                    return (false, $"打开设备 {selectedDevice.DisplayName} 失败，无详细信息。");
                }
            }
            catch (Exception ex)
            {
                // 捕获从 Open() 抛出的异常，并将其作为返回消息
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 关闭【指定目标窗口】上连接的相机设备并释放相关资源。
        /// </summary>
        /// <param name="targetWindow">要关闭相机的目标窗口。</param>
        /// <returns>一个元组，包含操作是否成功(bool)和相应的提示信息(string)。</returns>
        public (bool Success, string Message) CloseDevice(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null)
            {
                return (false, "内部错误：未指定目标窗口。");
            }
            // 根据目标窗口，在已打开的相机字典中找到对应的相机条目
            var cameraEntry = openCameras.FirstOrDefault(kvp => kvp.Value.DisplayWindow == targetWindow);
            // 检查是否找到了相机
            if (cameraEntry.Value == null) // FirstOrDefault 对于值类型（字典条目）会返回默认值，其Value会是null
            {
                return (false, "此窗口没有连接任何相机，无需关闭。");
            }
            // 从找到的条目中获取设备信息 (Key是UniqueID)
            // 为了复用现有的关闭逻辑，我们构造一个临时的DeviceInfo对象
            var deviceInfoToClose = new DeviceInfo { UniqueID = cameraEntry.Key, DisplayName = cameraEntry.Key };

            // 调用通过DeviceInfo关闭相机的核心逻辑
            return CloseDevice(deviceInfoToClose);
        }


        /// <summary>
        /// 根据设备信息关闭指定的相机设备并释放相关资源。
        /// </summary>
        /// <param name="selectedDevice">要关闭的设备信息。</param>
        /// <returns>一个元组，包含操作是否成功(bool)和相应的提示信息(string)。</returns>
        public (bool Success, string Message) CloseDevice(DeviceInfo selectedDevice)
        {

            if (selectedDevice == null)
            {
                return (false, "请选择一个要关闭的设备。");
            }

            // 尝试从已打开的相机字典中找到要关闭的相机
            if (openCameras.TryGetValue(selectedDevice.UniqueID, out ICameraDevice cameraToClose))
            {
                // 在操作前，先获取该相机绑定的显示窗口
                var windowToUpdate = cameraToClose.DisplayWindow;

                // 调用针对特定窗口的ROI清理方法
                ClearRoiForWindow(windowToUpdate);

                // 解除窗口与相机的绑定关系
                windowToUpdate.Tag = null;
                // 调用相机自身的关闭和资源释放方法
                cameraToClose.Close();
                // 从管理字典中移除该相机
                openCameras.Remove(selectedDevice.UniqueID);
                // 触发事件，通知其他UI（如CameraManagementWindow）相机列表已更新
                CameraListChanged?.Invoke(this, EventArgs.Empty);
                // 更新已变为空闲的窗口左下角信息
                UpdateWindowInfoOnSourceChanged(windowToUpdate, "空闲");
                // 刷新该窗口的显示，确保ROI被彻底清除
                if (windowToUpdate.IsVisible)
                {
                    windowToUpdate.HalconWindow.ClearWindow();
                }
                // 为了在状态栏显示更友好的名称，我们可以从原始列表中查找
                var allDevices = GetFoundDevices();
                var originalDeviceInfo = allDevices.FirstOrDefault(d => d.UniqueID == selectedDevice.UniqueID);
                string displayName = originalDeviceInfo?.DisplayName ?? selectedDevice.UniqueID;

                return (true, $"设备 {selectedDevice.DisplayName} 已成功关闭。");
            }
            // 如果在字典中找不到，说明该设备并未打开
            return (false, $"设备 {selectedDevice.DisplayName} 并未打开，无需关闭。");

        }

        // --- 用于填充下拉框 ---
        public List<DeviceInfo> GetAvailableDevicesForWindow(HSmartWindowControlWPF targetWindow)
        {
            var allDevices = GetFoundDevices();
            var openDeviceIds = new HashSet<string>(openCameras.Keys);

            // 获取当前目标窗口已连接的相机ID (如果有)
            string currentDeviceOnTarget = null;
            if (openCameras.Values.FirstOrDefault(cam => cam.DisplayWindow == targetWindow) is ICameraDevice currentCam)
            {
                currentDeviceOnTarget = currentCam.DeviceID;
            }

            var availableDevices = allDevices.Where(dev =>
                    !openDeviceIds.Contains(dev.UniqueID) // 设备未被打开
                    || dev.UniqueID == currentDeviceOnTarget  // 或者，就是当前窗口已连接的那个设备
                ).ToList();

            return availableDevices;
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
                _activeDisplayWindow.Dispatcher.Invoke(() => {
                    try
                    {
                        _activeDisplayWindow.HalconWindow.ClearWindow();
                        if (image != null && image.IsInitialized())
                        {
                            _activeDisplayWindow.HalconWindow.DispObj(image);
                        }
                    }
                    catch (HalconException) { }
                });
            }
        }

        /// <summary>
        /// 对指定窗口进行一次完整的显示刷新，包括背景图像和所有存在的ROI。
        /// </summary>
        /// <param name="targetWindow">需要刷新的目标窗口。</param>
        //private void FullRefreshWindowDisplay(HSmartWindowControlWPF targetWindow)
        //{
        //    if (targetWindow == null) return;
        //    // 1. 获取并显示背景图像
        //    using (HObject image = GetImageFromWindow(targetWindow))
        //    {
        //        if (image != null && image.IsInitialized())
        //        {
        //            targetWindow.HalconWindow.DispObj(image);
        //        }
        //        else
        //        {
        //            targetWindow.HalconWindow.ClearWindow();
        //        }
        //    }

        //    // 2. 检查并显示该窗口的ROI
        //    if (_windowRoiStates.TryGetValue(targetWindow, out var roiState))
        //    {
        //        // a. 显示掩膜/橡皮擦ROI (PaintedRoi)
        //        if (roiState.PaintedRoi != null && roiState.PaintedRoi.IsInitialized() && roiState.PaintedRoi.CountObj() > 0)
        //        {
        //            // 如果是活动窗口并且正在编辑中，用半透明填充
        //            if (targetWindow == _activeDisplayWindow && (_isPaintingMode || _isErasingMode))
        //            {
        //                targetWindow.HalconWindow.SetColor("#00FF0060");
        //                targetWindow.HalconWindow.SetDraw("fill");
        //            }
        //            else // 否则，只显示轮廓
        //            {
        //                targetWindow.HalconWindow.SetColor("green");
        //                targetWindow.HalconWindow.SetDraw("margin");
        //            }
        //            targetWindow.HalconWindow.DispObj(roiState.PaintedRoi);
        //        }

        //        // b. 显示标准交互式ROI (DrawingObject)
        //        // 注意：DrawingObject 是通过 Attach 附加的，Halcon会自动管理其重绘。
        //        // 所以理论上我们不需要手动重绘它。但如果出现问题，可以在此添加相关逻辑。
        //        // 目前的设计中，SetActiveDisplayWindow 会处理 Adorner 的迁移，
        //        // 而 Halcon 会处理 DrawingObject 的显示，所以这里通常无需额外操作。
        //    }
        //}

        private void FullRefreshWindowDisplay(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            // 使用Dispatcher确保所有绘图操作都在控件所属的UI线程上执行，避免死锁
            targetWindow.Dispatcher.Invoke(() =>
            {
                try
                {
                    var hWindow = targetWindow.HalconWindow;

                    // --- 步骤1: 准备好所有要显示的对象 ---
                    // 注意：GetImageFromWindow 涉及到后台数据，可以在Invoke外部或内部调用，
                    // 但为了逻辑清晰，我们放在内部。
                    using (HObject backgroundImage = GetImageFromWindow(targetWindow))
                    {
                        // --- 步骤2: 清空窗口 ---
                        hWindow.ClearWindow();

                        // --- 步骤3: 绘制背景图 ---
                        if (backgroundImage != null && backgroundImage.IsInitialized())
                        {
                            hWindow.DispObj(backgroundImage);
                        }
                    } // backgroundImage 在此被释放

                    // --- 步骤4: 检查并依次重绘所有类型的ROI ---
                    if (_windowRoiStates.TryGetValue(targetWindow, out var roiState))
                    {
                        // 4a. 绘制掩膜/橡皮擦ROI (PaintedRoi)
                        if (roiState.PaintedRoi != null && roiState.PaintedRoi.IsInitialized() && roiState.PaintedRoi.CountObj() > 0)
                        {
                            // 在UI线程内部创建新的HTuple，避免跨线程问题
                            hWindow.SetColor("#00FF0060");
                            hWindow.SetDraw("fill");
                            hWindow.DispObj(roiState.PaintedRoi);
                        }

                        // 4b. 绘制标准交互式ROI (DrawingObject)
                        // HDrawingObject 在附加后，其显示通常由Halcon内部管理。
                        // DispObj(backgroundImage) 可能会清除它，但当窗口重新激活或交互时，
                        // Halcon通常会负责重绘它。如果它没有出现，我们才需要手动重绘。
                        // 实践证明，手动重绘是最可靠的方式。
                        if (roiState.DrawingObject != null && roiState.DrawingObject.ID != -1)
                        {
                            // 我们不直接绘制DrawingObject，而是绘制它的“快照”（iconic），
                            // 因为DrawingObject本身包含了交互逻辑，不适合直接显示。
                            using (HObject iconic = roiState.DrawingObject.GetDrawingObjectIconic())
                            {
                                // 获取并应用DrawingObject自身的颜色和线宽设置
                                HTuple color = roiState.DrawingObject.GetDrawingObjectParams("color");
                                HTuple lineWidth = roiState.DrawingObject.GetDrawingObjectParams("line_width");

                                // 在UI线程内部创建新的HTuple
                                hWindow.SetColor(new HTuple(color));
                                hWindow.SetLineWidth(new HTuple(lineWidth));
                                hWindow.SetDraw("fill");

                                hWindow.DispObj(iconic);
                            }
                        }
                    }
                }
                catch (HalconException ex)
                {
                    // 在窗口关闭或快速切换时，可能会发生绘图异常，这里可以记录日志或静默处理
                    System.Diagnostics.Debug.WriteLine($"FullRefreshWindowDisplay Error on window {displayWindows.IndexOf(targetWindow)}: {ex.GetErrorMessage()}");
                }
            });
        }
        #endregion

        #region 标准交互式ROI (HDrawingObject)

        /// <summary>
        /// 根据选择的类型，在活动窗口上创建并附加一个交互式ROI。
        /// </summary>
        private void CreateDrawingObject(string selectedType)
        {
            // 安全检查，确保活动窗口存在
            if (_activeDisplayWindow == null) return;
            // 获取当前活动窗口的ROI状态
            var roiState = _windowRoiStates[_activeDisplayWindow];

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
                HDrawingObject newDrawingObject = null; // 使用局部变量
                // 根据字符串选择确定ROI类型并直接创建 _drawingObject 实例
                switch (selectedType)
                {
                    case "矩形 (Rectangle)":
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "带角度矩形 (Rectangle2)":
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE2, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 4);
                        break;
                    case "圆形 (Circle)":
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, height.D / 2, width.D / 2, Math.Min(width.D, height.D) / 4);
                        break;
                    case "椭圆 (Ellipse)":
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.ELLIPSE, height.D / 2, width.D / 2, 0, width.D / 4, height.D / 8);
                        break;
                    case "直线 (Line)":
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, height.D / 4, width.D / 4, height.D * 0.75, width.D * 0.75);
                        break;
                    case "自由轮廓 (Contour)":
                        HTuple initialRows = new HTuple(new double[] { height.D / 4, height.D / 4, height.D * 0.75, height.D * 0.75 });
                        HTuple initialCols = new HTuple(new double[] { width.D / 4, width.D * 0.75, width.D * 0.75, width.D / 4 });
                        newDrawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.XLD_CONTOUR, initialRows, initialCols);
                        break;
                    default:
                        // 如果类型不匹配，直接退出
                        return;
                }
                // 将新创建的ROI对象存入当前窗口的ROI状态中
                roiState.DrawingObject = newDrawingObject;
                // 附加到活动窗口
                _activeDisplayWindow.HalconWindow.AttachDrawingObjectToWindow(roiState.DrawingObject);


                // 设置控制点的大小。默认值大约是3或4，设置为7或8会有明显增大的效果。
                roiState.DrawingObject.SetDrawingObjectParams(new HTuple("marker_size", "line_width"), new HTuple(13, 3));

                // 订阅交互事件
                roiState.DrawingObject.OnDrag(OnRoiUpdate);
                roiState.DrawingObject.OnResize(OnRoiUpdate);
                roiState.DrawingObject.OnSelect(OnRoiUpdate);

                // 先显示Adorner
                ShowRoiAdorner(_activeDisplayWindow);
                // 再调用中央处理器来更新所有UI
                ProcessRoiUpdate(roiState.DrawingObject);

                if (_captureAdorners.TryGetValue(_activeDisplayWindow, out var adorner))
                    adorner.SetSaveRoiButtonState(true);
            }
            catch (Exception ex)
            {
                // 捕获并显示更详细的错误信息
                UpdateStatus($"创建ROI失败: {ex.Message}", true);
                // 如果创建过程中发生异常，确保清理可能已部分创建的对象
                if (_windowRoiStates.TryGetValue(_activeDisplayWindow, out var stateToClean))
                {
                    stateToClean.DrawingObject?.Dispose();
                    stateToClean.DrawingObject = null;
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
            // 确认当前交互的ROI是否属于当前的活动窗口。
            // 这是一个重要的保险措施，防止在窗口切换的瞬间处理了错误的ROI。
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState) || roiState.DrawingObject == null || roiState.DrawingObject.ID != dobj.ID)
            {
                return;
            }
            Dispatcher.Invoke(() =>
            {
                // 将事件提供的、包含最新数据的dobj直接传递给处理函数
                ProcessRoiUpdate(dobj);
                if (_captureAdorners.TryGetValue(_activeDisplayWindow, out var adorner))
                    adorner.SetSaveRoiButtonState(true);
            });

        }

        /// <summary>
        /// 处理ROI更新的中央方法。
        /// 它负责获取ROI的最新参数，并触发状态栏和浮动Adorner的更新。
        /// </summary>
        private void ProcessRoiUpdate(HDrawingObject drawingObject)
        {
            // 安全检查：传入的对象是否有效
            if (drawingObject == null || drawingObject.ID == -1) return;
           
            // 检查活动窗口，我们需要它来更新UI和存储状态
            if (_activeDisplayWindow == null || !_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState))
            {
                return;
            }

            try
            {
                string roiType = drawingObject.GetDrawingObjectParams("type").S;
                // --- 首先处理特殊的自由轮廓 ---
                if (roiType == "xld")
                {
                    // 对于自由轮廓，我们不尝试获取任何几何参数或iconic对象，
                    // 我们只清空UI，因为在拖动过程中显示参数没有意义。
                    roiState.LastRoiArgs = null;
                    UpdateAdornerPosition(null);
                    UpdateParameterBar();
                    // 直接返回，绝对不执行下面的危险代码。
                    return;

                }
                // 根据类型获取参数字典
                var parameters = new Dictionary<string, double>();
                switch (roiType)
                {
                    case "rectangle1":
                        parameters["row1"] = drawingObject.GetDrawingObjectParams("row1").D;
                        parameters["column1"] = drawingObject.GetDrawingObjectParams("column1").D;
                        parameters["row2"] = drawingObject.GetDrawingObjectParams("row2").D;
                        parameters["column2"] = drawingObject.GetDrawingObjectParams("column2").D;
                        break;
                    case "rectangle2":
                        parameters["row"] = drawingObject.GetDrawingObjectParams("row").D;
                        parameters["column"] = drawingObject.GetDrawingObjectParams("column").D;
                        parameters["phi"] = drawingObject.GetDrawingObjectParams("phi").D; // 保持弧度
                        parameters["length1"] = drawingObject.GetDrawingObjectParams("length1").D;
                        parameters["length2"] = drawingObject.GetDrawingObjectParams("length2").D;
                        break;
                    case "circle":
                        parameters["row"] = drawingObject.GetDrawingObjectParams("row").D;
                        parameters["column"] = drawingObject.GetDrawingObjectParams("column").D;
                        parameters["radius"] = drawingObject.GetDrawingObjectParams("radius").D;
                        break;
                    case "ellipse":
                        parameters["row"] = drawingObject.GetDrawingObjectParams("row").D;
                        parameters["column"] = drawingObject.GetDrawingObjectParams("column").D;
                        parameters["phi"] = drawingObject.GetDrawingObjectParams("phi").D; // 保持弧度
                        parameters["radius1"] = drawingObject.GetDrawingObjectParams("radius1").D;
                        parameters["radius2"] = drawingObject.GetDrawingObjectParams("radius2").D;
                        break;
                    case "line":
                        parameters["row1"] = drawingObject.GetDrawingObjectParams("row1").D;
                        parameters["column1"] = drawingObject.GetDrawingObjectParams("column1").D;
                        parameters["row2"] = drawingObject.GetDrawingObjectParams("row2").D;
                        parameters["column2"] = drawingObject.GetDrawingObjectParams("column2").D;
                        break;
                  
                }

                // 使用参数字典创建一个统一的数据对象
                using (HObject iconic = drawingObject.GetDrawingObjectIconic())
                {
                    // 将这个新的数据对象存储在当前窗口的ROI状态中
                    roiState.LastRoiArgs = new RoiUpdatedEventArgs(parameters, iconic.CopyObj(1, -1));
                }

                // 使用这个统一的数据对象，更新Adorner和状态栏
                RefreshAdorner();
                UpdateParameterBar();
            }
            catch (HalconException) { /* 忽略小错误 */ }
        }
        
        /// <summary>
        /// 当参数控件的值被用户修改时触发，用于反向更新ROI对象。
        /// </summary>
        private void OnParameterControlValueChanged(object sender, RoutedEventArgs e)
        {
            // 如果是代码在更新UI，则直接返回，防止无限循环
            if (_isUpdatingFromRoi) return;

            // 检查活动窗口和ROI状态
            if (_activeDisplayWindow == null || !_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState) || roiState.DrawingObject == null || roiState.DrawingObject.ID == -1)
            {
                return;
            }


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
                if (newParams.Count == 0) return; // 如果没有收集到任何参数，则退出
                // 根据ROI类型，调用正确的SetDrawingObjectParams重载
                string roiType = roiState.DrawingObject.GetDrawingObjectParams("type").S;
                switch (roiType)
                {
                    case "rectangle1":
                        roiState.DrawingObject.SetDrawingObjectParams(new HTuple("row1", "column1", "row2", "column2"),
                            new HTuple(newParams["row1"], newParams["column1"], newParams["row2"], newParams["column2"]));
                        break;
                    case "rectangle2":
                        roiState.DrawingObject.SetDrawingObjectParams(new HTuple("row", "column", "phi", "length1", "length2"),
                            new HTuple(newParams["row"], newParams["column"], newParams["phi"] * Math.PI / 180, newParams["length1"], newParams["length2"]));
                        break;
                    case "circle":
                        roiState.DrawingObject.SetDrawingObjectParams(new HTuple("row", "column", "radius"),
                           new HTuple(newParams["row"], newParams["column"], newParams["radius"]));
                        break;
                    case "ellipse":
                        roiState.DrawingObject.SetDrawingObjectParams(new HTuple("row", "column", "phi", "radius1", "radius2"),
                           new HTuple(newParams["row"], newParams["column"], newParams["phi"] * Math.PI / 180, newParams["radius1"], newParams["radius2"]));
                        break;
                    case "line":
                        roiState.DrawingObject.SetDrawingObjectParams(new HTuple("row1", "column1", "row2", "column2"),
                           new HTuple(newParams["row1"], newParams["column1"], newParams["row2"], newParams["column2"]));
                        break;
                }

                // 调用唯一的、权威的“主更新”方法。
                // 它会从刚刚更新的ROI对象中重新读取最准确的参数，然后用这些数据
                // 来刷新数据模型(LastRoiArgs)、浮动框(Adorner)和状态栏(ParameterBar)。
                // 这保证了数据流的闭环和UI的完全同步。
                ProcessRoiUpdate(roiState.DrawingObject);
            }
            catch (HalconException) { /* 忽略设置过程中的小错误 */ }

        }

        #endregion

        #region 涂抹式ROI (Paint Mode)

        /// <summary>
        /// 启用涂抹绘制模式，初始化相关状态并订阅鼠标事件。
        /// </summary>
        private void EnablePaintMode()
        {
            if (_isPaintingMode) return;
            if (_activeDisplayWindow == null) return;
            // 获取当前活动窗口的ROI状态容器
            var roiState = _windowRoiStates[_activeDisplayWindow];
            // 根据当前图像，设置合适的默认画笔尺寸
            SetDefaultBrushSize();
            // 设置全局模式标志
            _isPaintingMode = true;
            _isErasingMode = false; // 确保橡皮擦模式关闭
            // 为当前窗口初始化一个新的、空的 paintedRoi
            roiState.PaintedRoi?.Dispose();
            HOperatorSet.GenEmptyRegion(out HObject newPaintedRoi);
            roiState.PaintedRoi = newPaintedRoi;
            // 为活动窗口订阅鼠标事件
            _activeDisplayWindow.PreviewMouseLeftButtonDown += PaintRoi_PreviewMouseLeftButtonDown;
            _activeDisplayWindow.PreviewMouseLeftButtonUp += PaintRoi_PreviewMouseLeftButtonUp;
            _activeDisplayWindow.HMouseMove += PaintRoi_HMouseMove;
            // 更新UI状态
            UpdateStatus("掩膜模式已激活：按住左键在图像上拖动以绘制。", false);
            if (_captureAdorners.TryGetValue(_activeDisplayWindow, out var adorner))
                adorner.SetSaveRoiButtonState(true);
            // 显示涂抹式ROI的参数控件
            UpdateParameterBar();
        }

        /// <summary>
        /// 禁用并清理涂抹绘制模式，解绑鼠标事件。
        /// </summary>
        private void DisablePaintMode()
        {
            if (!_isPaintingMode && !_isErasingMode) return;
            _isPaintingMode = false;
            _isErasingMode = false;
            _isPaintingActive = false;

            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.PreviewMouseLeftButtonDown -= PaintRoi_PreviewMouseLeftButtonDown;
                _activeDisplayWindow.PreviewMouseLeftButtonUp -= PaintRoi_PreviewMouseLeftButtonUp;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }
            //RefreshActiveWindowDisplay();
            // 隐藏涂抹参数控件（因为没有ROI了，会自然隐藏）
            UpdateParameterBar();
        }

        /// <summary>
        /// 涂抹模式下的鼠标按下事件。
        /// </summary>
        private void PaintRoi_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 捕获鼠标，确保即使鼠标移出窗口也能收到后续事件
            (_activeDisplayWindow as IInputElement)?.CaptureMouse();

            _isPaintingActive = true;

            // 获取当前鼠标位置作为起始点
            // HSmartWindowControlWPF没有直接的方法在此时获取Halcon坐标，
            // 我们需要借助HMouseMove中的最新坐标，或者自己计算一次
            // 一个简单的技巧是直接从WPF坐标转换
            var pos = e.GetPosition(_activeDisplayWindow);
            _activeDisplayWindow.HalconWindow.ConvertCoordinatesWindowToImage(pos.Y, pos.X, out HTuple row, out HTuple col);

            _lastPaintRow = row;
            _lastPaintCol = col;

            PaintAtCurrentPosition(row.D, col.D);
            UpdateStatus("正在涂抹... 松开左键以完成。", false); // 更新提示

            // 阻止事件继续传递，禁用背景的拖动和缩放
            e.Handled = true;
        }

        /// <summary>
        /// 涂抹模式下的鼠标左键松开事件。结束绘制。
        /// </summary>
        private void PaintRoi_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 只有在正在绘制时，松开左键才触发结束
            if (_isPaintingActive)
            {
                FinalizePainting();
                // 释放鼠标捕获
                (_activeDisplayWindow as IInputElement)?.ReleaseMouseCapture();
                e.Handled = true;
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
        /// 掩膜或橡皮擦模式的核心绘制/擦除逻辑
        /// </summary>
        private void PaintAtCurrentPosition(double row, double col)
        {
            if (_activeDisplayWindow == null) return;

            // 获取当前活动窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState) || roiState.PaintedRoi == null)
            {
                return;
            }

            HObject brush = null;
            HObject lineRegion = null;
            HObject newRoi = null;
            HObject backgroundImage = null;
            // 在开始绘制前，保存当前的视图（缩放/平移状态），以防绘制过程中视图被意外改变
            Rect currentView = _activeDisplayWindow.HImagePart;
            try
            {
                // 根据鼠标轨迹和画笔设置，生成一个“笔触”Region (brush)
                if (_lastPaintRow > -1 && _lastPaintCol > -1)
                {
                    // 连续拖动：在上一个点和当前点之间生成线，然后膨胀
                    HOperatorSet.GenRegionLine(out lineRegion, _lastPaintRow, _lastPaintCol, row, col);
                    if (_brushShape == "圆形") HOperatorSet.DilationCircle(lineRegion, out brush, _brushCircleRadius);
                    else HOperatorSet.DilationRectangle1(lineRegion, out brush, _brushRectWidth, _brushRectHeight);//矩形
                }
                else
                {
                    // 首次单击：直接在当前点生成一个画笔形状
                    if (_brushShape == "圆形") HOperatorSet.GenCircle(out brush, row, col, _brushCircleRadius);
                    else HOperatorSet.GenRectangle1(out brush, row - _brushRectHeight / 2.0, col - _brushRectWidth / 2.0, row + _brushRectHeight / 2.0, col + _brushRectWidth / 2.0);
                }

                HObject currentPaintedRoi = roiState.PaintedRoi; // 获取当前ROI的引用
                // 根据当前是绘画模式还是橡皮擦模式，决定如何将笔触合并到总ROI中
                if (_isErasingMode)
                {
                    // 橡皮擦模式：执行差集运算 (从已有区域中减去笔触)
                    HOperatorSet.Difference(currentPaintedRoi, brush, out newRoi);
                }
                else // 默认是绘画模式
                {
                    // 绘画模式：执行并集运算 (将笔触添加到已有区域中)
                    HOperatorSet.Union2(currentPaintedRoi, brush, out newRoi);
                }

                // 更新当前窗口的 paintedRoi
                roiState.PaintedRoi = newRoi;// 将新生成的ROI赋值回去
                currentPaintedRoi.Dispose();// 释放旧的ROI
                newRoi = null;
                // 刷新UI显示
                backgroundImage = GetImageFromActiveWindow();
                if (backgroundImage == null || !backgroundImage.IsInitialized()) return;

                var hWindow = _activeDisplayWindow.HalconWindow;
                hWindow.SetPart((int)currentView.Top, (int)currentView.Left, (int)currentView.Bottom - 1, (int)currentView.Right - 1); // 恢复视图，防止因计算延迟导致的视图跳动

                // 执行一次完整的“清屏 -> 画背景 -> 画掩膜”操作
                // 这会将最新的 PaintedRoi “提交”到窗口的图形堆栈中
                hWindow.ClearWindow();
                hWindow.DispObj(backgroundImage);

                // 总是以半透明填充的方式显示正在编辑的ROI
                hWindow.SetColor("#00FF0060");
                
                hWindow.SetDraw("fill");
                hWindow.DispObj(roiState.PaintedRoi);
            }
            catch (HalconException) { /* 忽略小错误 */ }
            finally
            {
                brush?.Dispose();
                lineRegion?.Dispose();
                
                backgroundImage?.Dispose();
            }
        }

        /// <summary>
        /// 结束并最终确定一次涂抹绘制。
        /// </summary>
        private void FinalizePainting()
        {
            if (!_isPaintingActive)
            {  // 即使没有主动绘制（isPaintingActive=false），
               // 只要模式是开着的（比如转换ROI后没画就切换窗口），也需要清理
                if (!_isPaintingMode && !_isErasingMode) return;
            }

            _isPaintingActive = false;
            _isPaintingMode = false; // 每次绘制完成都自动退出涂抹模式
            _isErasingMode = false; // 重置橡皮擦模式


            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.PreviewMouseLeftButtonDown -= PaintRoi_PreviewMouseLeftButtonDown;
                _activeDisplayWindow.PreviewMouseLeftButtonUp -= PaintRoi_PreviewMouseLeftButtonUp;
                _activeDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
                (_activeDisplayWindow as IInputElement)?.ReleaseMouseCapture(); // 确保鼠标捕获被释放
            }

            UpdateStatus("涂抹绘制已完成。ROI已创建。", false);

            // 结束时，刷新背景并“提交”最终的 PaintedRoi
            RefreshActiveWindowDisplay();// 刷新当前活动窗口（此时还是旧的）
            var roiState = _windowRoiStates[_activeDisplayWindow];
            if (_activeDisplayWindow != null && roiState.PaintedRoi != null && roiState.PaintedRoi.IsInitialized())
            {
                var hWindow = _activeDisplayWindow.HalconWindow;
                hWindow.SetColor("#00FF0060");
                hWindow.SetDraw("fill");
                hWindow.DispObj(roiState.PaintedRoi); // 提交最终的轮廓线
            }
            // 隐藏参数面板
            UpdateParameterBar();
        }

        /// <summary>
        /// 启用橡皮擦模式。
        /// </summary>
        private void EnableEraseMode()
        {
            if (_activeDisplayWindow == null) return;
            // 根据当前图像，设置合适的默认画笔尺寸
            SetDefaultBrushSize();
            // 尝试将当前活动窗口的标准ROI转换为可编辑的 paintedRoi
            ConvertStandardRoiToPaintedRoi(_activeDisplayWindow);
            // 获取当前活动窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState)) return;
            // --- 检查是否有可擦除的ROI ---
            // 优先使用 _paintedRoi，如果没有，则检查是否存在一个有效的 _drawingObject
            if (roiState.PaintedRoi == null || !roiState.PaintedRoi.IsInitialized() || roiState.PaintedRoi.CountObj() == 0)
            {
                UpdateStatus("画布上没有可供擦除的ROI。", true);
                return;
            }
            // 设置全局模式标志
            if (_isErasingMode) return;
            _isErasingMode = true;
            _isPaintingMode = false;

            // 为活动窗口订阅鼠标事件
            if (_activeDisplayWindow != null)
            {
                _activeDisplayWindow.PreviewMouseLeftButtonDown += PaintRoi_PreviewMouseLeftButtonDown;
                _activeDisplayWindow.PreviewMouseLeftButtonUp += PaintRoi_PreviewMouseLeftButtonUp;
                _activeDisplayWindow.HMouseMove += PaintRoi_HMouseMove;
            }

            // 更新UI状态
            UpdateStatus("橡皮擦模式已激活：按住左键在ROI上拖动以擦除。", false);
            UpdateParameterBar(); // 显示橡皮擦的参数面板
        }

        /// <summary>
        /// 检查【指定窗口】是否存在标准ROI (_drawingObject)，如果存在，则将其转换为可编辑的
        /// _paintedRoi，并清除原来的 _drawingObject。
        /// </summary>
        /// <param name="targetWindow">需要进行转换的目标窗口。</param>
        private void ConvertStandardRoiToPaintedRoi(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;
            // 获取目标窗口的ROI状态容器
            var roiState = _windowRoiStates[targetWindow];
            // 如果只有 standard ROI，需要将其转换为 _paintedRoi
            if ((roiState.PaintedRoi == null || roiState.PaintedRoi.CountObj() == 0) && roiState.DrawingObject != null && roiState.DrawingObject.ID != -1)
            {
                try
                {
                    string roiType = roiState.DrawingObject.GetDrawingObjectParams("type").S;
                    if (roiType == "line")
                    {
                        ClearStandardRoi(targetWindow);
                        return;
                    }

                    using (HObject iconic = roiState.DrawingObject.GetDrawingObjectIconic())
                    {
                        string iconicType = iconic.GetObjClass();
                        if (iconicType == "region")
                        {
                            roiState.PaintedRoi = iconic.CopyObj(1, -1);
                        }
                        else if (iconicType == "xld_cont")
                        {
                            HOperatorSet.GenRegionContourXld(iconic, out HObject paintedRoi, "filled");
                            roiState.PaintedRoi = paintedRoi;
                        }
                    }
                    ClearStandardRoi(targetWindow); // 转换成功后清除旧的
                    // 刷新显示，让用户看到可供操作的半透明区域
                    RefreshActiveWindowDisplay();

                    // 然后，立即将新转换的 _paintedRoi 用填充模式画出来
                    if (targetWindow.IsVisible && roiState.PaintedRoi != null && roiState.PaintedRoi.IsInitialized())
                    {
                        
                        _activeDisplayWindow.HalconWindow.SetColor("#00FF0060");

                        _activeDisplayWindow.HalconWindow.SetDraw("fill");
                        _activeDisplayWindow.HalconWindow.DispObj(roiState.PaintedRoi);
                    }
                }
                catch (HalconException)
                {
                    roiState.PaintedRoi?.Dispose();
                    roiState.PaintedRoi = null;
                    ClearStandardRoi(targetWindow); // 转换失败也清除旧的
                }
            }
        }

        /// <summary>
        /// 根据当前活动窗口的图像尺寸，计算并设置画笔的默认大小。
        /// </summary>
        private void SetDefaultBrushSize()
        {
            // 使用 using 语句确保图像对象被正确释放
            using (HObject image = GetImageFromActiveWindow())
            {
                if (image == null || !image.IsInitialized()) return;

                try
                {
                    HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);

                    // 找出宽高的最小值
                    double minDimension = Math.Min(width.D, height.D);

                    // 计算其2%，并向上取整
                    int defaultSize = (int)Math.Ceiling(minDimension * 0.04);

                    // 确保尺寸至少为1
                    if (defaultSize < 1) defaultSize = 1;

                    // 更新所有画笔尺寸的默认值
                    _brushCircleRadius = defaultSize;
                    _brushRectWidth = defaultSize * 2;
                    _brushRectHeight = defaultSize * 2;
                }
                catch (HalconException)
                {
                    // 如果获取尺寸失败，则不做任何事，保持原有的硬编码默认值
                }
            }
        }

        #endregion

        #region 通用UI与状态管理

        /// <summary>
        /// 清除当前活动窗口上的所有ROI。
        /// </summary>
        private void ClearActiveRoi()
        {
            if (_activeDisplayWindow == null) return;

            // 获取当前活动窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState)) return;

            // --- 清除标准 HDrawingObject ROI ---
            if (roiState.DrawingObject != null)
            {
                // 从活动窗口移除浮动参数框
                RemoveRoiAdorner(_activeDisplayWindow);
                try
                {
                    _activeDisplayWindow.HalconWindow.DetachDrawingObjectFromWindow(roiState.DrawingObject);
                }
                catch (HalconException) { /* 忽略分离错误 */ }
                roiState.DrawingObject.Dispose();
                roiState.DrawingObject = null;
               
            }

            // 清除涂抹式ROI
            roiState.PaintedRoi?.Dispose();
            roiState.PaintedRoi = null;

            // --- 禁用涂抹模式（如果正在进行中）---
            // 这个会处理事件解绑和状态重置
            if (_isPaintingMode || _isErasingMode)
            {
                FinalizePainting();
            }
            RefreshActiveWindowDisplay(); // 刷新显示，清除残留图形
            // --- 统一的收尾工作 ---
            UpdateParameterBar(); // 清理并隐藏参数面板

            // 禁用当前活动窗口工具栏上的“保存ROI”按钮
            if (_captureAdorners.TryGetValue(_activeDisplayWindow, out var adorner))
            {
                adorner.SetSaveRoiButtonState(false);
            }
        }

        // 只清除标准ROI
        private void ClearStandardRoi(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            // 获取目标窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(targetWindow, out var roiState)) return;
            // 检查该窗口是否存在一个有效的标准ROI
            if (roiState.DrawingObject != null && roiState.DrawingObject.ID != -1)
            {
                // 调用带参数的版本
                RemoveRoiAdorner(targetWindow);
                try
                {
                    // 从Halcon窗口分离ROI对象
                    targetWindow.HalconWindow.DetachDrawingObjectFromWindow(roiState.DrawingObject);
                }
                catch (HalconException) { /* 忽略分离错误 */ }
                // 释放ROI对象并从状态容器中移除
                roiState.DrawingObject.Dispose();
                roiState.DrawingObject = null;
                // 如果要清除的窗口是活动窗口，则更新参数面板
                if (targetWindow == _activeDisplayWindow)
                {
                    UpdateParameterBar();
                }
            }
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

            // 获取当前活动窗口的ROI状态
            if (_activeDisplayWindow == null || !_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState))
            {
                RoiParameterPanel.Children.Clear();
                RoiParameterPanel.Visibility = Visibility.Collapsed;
                _isUpdatingFromRoi = false;
                return;
            }

            // --- 情况1：处理涂抹式ROI或橡皮擦模式 ---
            if (_isPaintingMode || _isErasingMode)
            {
                // 对于画笔UI，逻辑简单，直接重建即可
                RoiParameterPanel.Children.Clear();

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
                        Maximum = 800, // 设置最大值
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
                        Maximum = 1600,
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
                        Maximum = 1600,
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
            else if (roiState.DrawingObject != null && roiState.DrawingObject.ID != -1 && roiState.LastRoiArgs != null)
            {
                //  直接从当前窗口的ROI状态中获取参数
                var parameters = roiState.LastRoiArgs.Parameters;

                // 检查面板是否需要重建 (例如从无到有，或ROI类型变化)
                if (RoiParameterPanel.Children.OfType<Xceed.Wpf.Toolkit.DoubleUpDown>().Count() != parameters.Count)
                {
                    RoiParameterPanel.Children.Clear(); // 只有在绝对必要时才重建
                }

                // --- 非破坏性地更新或创建控件 ---
                foreach (var kvp in parameters.OrderBy(p => p.Key)) // 排序以保证UI控件顺序稳定
                {
                    string paramName = kvp.Key;
                    double paramValue = kvp.Value;

                    var existingNumeric = RoiParameterPanel.Children.OfType<Xceed.Wpf.Toolkit.DoubleUpDown>()
                                    .FirstOrDefault(n => n.Tag as string == paramName);

                    if (existingNumeric != null)
                    {
                        // 如果控件已存在，只更新其值
                        double displayValue = paramName == "phi" ? paramValue * 180 / Math.PI : paramValue;
                        if (Math.Abs(existingNumeric.Value.GetValueOrDefault() - displayValue) > 0.001)
                        {
                            existingNumeric.Value = displayValue;
                        }
                    }
                    else
                    {
                        RoiParameterPanel.Children.Add(new Label { Content = $"{ParameterTranslator.Translate(paramName)}" });
                        var numeric = new Xceed.Wpf.Toolkit.DoubleUpDown
                        {
                            Value = (paramName == "phi" ? paramValue * 180 / Math.PI : paramValue), 
                            Width = 80,
                            Tag = paramName, // 使用Tag来标记这个控件对应哪个参数
                            Increment = 1.0,
                            FormatString = "F2"
                        };
                        numeric.ValueChanged += OnParameterControlValueChanged;
                        RoiParameterPanel.Children.Add(numeric);
                    }
                }
                RoiParameterPanel.Visibility = Visibility.Visible;
            }
            // --- 没有任何有效ROI ---
            else
            {
                RoiParameterPanel.Children.Clear();
                RoiParameterPanel.Visibility = Visibility.Collapsed;
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
        private void ShowRoiAdorner(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            if (!_windowRoiStates.TryGetValue(targetWindow, out var roiState)) return;

            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(targetWindow);
            if (adornerLayer != null)
            {
                // 如果已存在，先移除旧的
                if (roiState.RoiAdorner != null) { adornerLayer.Remove(roiState.RoiAdorner); }

                // 创建一个新的Adorner，并将其存储在当前窗口的ROI状态中
                roiState.RoiAdorner = new RoiAdorner(targetWindow);
                adornerLayer.Add(roiState.RoiAdorner);
                //// 监听 HImagePart 属性变化，以便在缩放/平移时更新Adorner位置
                //DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                //    .AddValueChanged(_activeDisplayWindow, OnDisplayWindowViewChanged);
            }

        }

        /// <summary>
        /// 从指定的窗口移除Adorner，并停止监听该窗口的视图变化。
        /// </summary>
        /// <param name="window">要移除Adorner的WPF窗口控件。</param>
        private void RemoveRoiAdorner(HSmartWindowControlWPF targetWindow)
        {
            if (targetWindow == null) return;

            if (!_windowRoiStates.TryGetValue(targetWindow, out var roiState)) return;
            if (roiState.RoiAdorner != null)
            {
                //// 停止监听这个特定窗口的视图变化事件
                //DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF))
                //    .RemoveValueChanged(window, OnDisplayWindowViewChanged);

                AdornerLayer.GetAdornerLayer(targetWindow)?.Remove(roiState.RoiAdorner);
                // 重置Adorner实例和缓存
                roiState.RoiAdorner = null;
                roiState.LastRoiArgs = null;
            }
        }

        /// <summary>
        /// 核心辅助方法：计算并更新Adorner在屏幕上的位置和显示的文本。
        /// </summary>
        private void UpdateAdornerPosition(RoiUpdatedEventArgs e)
        {
            // 检查活动窗口是否存在
            if (_activeDisplayWindow == null) return;
            // 获取当前活动窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState)) return;
            // 检查该窗口是否存在一个有效的RoiAdorner
            if (roiState.RoiAdorner == null) return;
            // 判断是否有有效的数据来更新Adorner
            // 如果传入的e为null，说明要隐藏Adorner（例如对于自由轮廓ROI）
            if (e != null && e.Position.HasValue)
            {
                // 将Halcon图像坐标转换为WPF窗口坐标
                var hWindow = _activeDisplayWindow.HalconWindow;
                hWindow.ConvertCoordinatesImageToWindow(e.Position.Value.Y, e.Position.Value.X, out double windowY, out double windowX);
                // 测量Adorner自身的大小，以便精确定位
                roiState.RoiAdorner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double adornerHeight = roiState.RoiAdorner.DesiredSize.Height;
                // 计算新位置（通常在ROI锚点的右侧，并垂直居中）
                Point newPosition = new Point(windowX + 15, windowY - adornerHeight / 2);
                // 调用Adorner的Update方法，传递新的文本和位置
                roiState.RoiAdorner.Update(e.ParametersAsString, newPosition);
            }
            else
            {
                // 如果没有有效数据，则清空Adorner内容并隐藏它
                roiState.RoiAdorner.Update("", new Point(0, 0)); 
            }
        }

        /// <summary>
        /// 轻量级更新方法，仅用于刷新浮动Adorner的位置和内容。
        /// </summary>
        private void RefreshAdorner()
        {
            // 检查活动窗口是否存在
            if (_activeDisplayWindow == null) return;
            // 获取当前活动窗口的ROI状态容器
            if (!_windowRoiStates.TryGetValue(_activeDisplayWindow, out var roiState)) return;
            // 检查该窗口是否存在有效的Adorner和缓存数据
            if (roiState.RoiAdorner != null && roiState.LastRoiArgs != null)
            {
                UpdateAdornerPosition(roiState.LastRoiArgs);
            }
        }

        #endregion

        /// <summary>
        /// 一个封装的图像显示方法 (当前代码中未使用，可移除或保留)
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
            // 关闭相机管理窗口
            cameraManagementWindow?.Close();

            communicationWindow?.Close();

            // 清理所有ROI资源
            ClearActiveRoi();

            // 再关闭所有相机
            foreach (var camera in openCameras.Values)
            {
                camera.Close();
            }
            // 关闭所有通讯连接
            foreach (var comm in communications.Values)
            {
                comm.Dispose();
            }
        }

        
    }
}