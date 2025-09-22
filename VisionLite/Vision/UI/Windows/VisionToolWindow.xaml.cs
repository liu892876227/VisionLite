using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using HalconDotNet;
using VisionLite.Vision.Core.Interfaces;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Core.Enums;
using VisionLite.Vision.Core.Utils;
using VisionLite.Vision.Processors.Preprocessing.FilterProcessors;
using VisionLite.Vision.Processors.Preprocessing.ThresholdProcessors;
using VisionLite.Vision.Processors.Preprocessing.MorphologyProcessors;
using VisionLite.Vision.Processors.Preprocessing.EnhancementProcessors;
using VisionLite.Vision.Processors.Preprocessing.EdgeProcessors;
using VisionLite.Vision.Processors.Measurement.CaliperProcessors;
using VisionLite.Vision.Processors.ImageMatching;
using VisionLite.Vision.Calibration.NinePoint.Core;
using VisionLite.Vision.Calibration.NinePoint.UI;
using VisionLite.Vision.UI.Controls;

namespace VisionLite.Vision.UI.Windows
{
    /// <summary>
    /// 视觉算法工具主窗口
    /// </summary>
    public partial class VisionToolWindow : Window
    {
        #region 私有字段
        
        private readonly Dictionary<string, IVisionProcessor> _algorithmProcessors;
        private IVisionProcessor _currentProcessor;
        private VisionImage _originalImage;
        private VisionImage _resultImage;
        private bool _isProcessing;
        
        
        // Halcon图像对象
        private HObject _hImage;
        private HObject _hResultImage;
        
        // HDrawingObject交互系统字段
        private HDrawingObject _interactiveCaliper;
        
        // 保存最近的处理结果用于重新显示轮廓
        private ProcessResult _lastProcessResult;

        // ROI交互管理
        // ROI管理器（使用可编辑的HDrawingObject）
        private ImageMatchingROIManager _roiManager;
        private bool _isImageMatchingMode = false;
        private string _currentAlgorithmKey;

        #endregion
        
        #region 构造函数
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public VisionToolWindow()
        {
            InitializeComponent();
            _algorithmProcessors = new Dictionary<string, IVisionProcessor>();
            InitializeAlgorithms();

            // 初始化ROI管理器
            InitializeROIManager();

            // 设置窗口加载事件
            this.Loaded += VisionToolWindow_Loaded;
        }
        
        #endregion
        
        #region 初始化方法
        
        /// <summary>
        /// 初始化算法处理器
        /// </summary>
        private void InitializeAlgorithms()
        {
            try
            {
                // 注册高斯滤波算法
                var gaussianProcessor = new GaussianFilterProcessor();
                var testParams = gaussianProcessor.GetParameters();
                if (testParams != null)
                {
                    _algorithmProcessors["GaussianFilter"] = gaussianProcessor;
                }
                else
                {
                    throw new InvalidOperationException("高斯滤波器参数获取失败");
                }
                
                // 注册中值滤波算法
                var medianProcessor = new MedianFilterProcessor();
                var medianParams = medianProcessor.GetParameters();
                if (medianParams != null)
                {
                    _algorithmProcessors["MedianFilter"] = medianProcessor;
                }
                else
                {
                    throw new InvalidOperationException("中值滤波器参数获取失败");
                }
                
                // 注册均值滤波算法
                var meanProcessor = new MeanFilterProcessor();
                var meanParams = meanProcessor.GetParameters();
                if (meanParams != null)
                {
                    _algorithmProcessors["MeanFilter"] = meanProcessor;
                }
                else
                {
                    throw new InvalidOperationException("均值滤波器参数获取失败");
                }
                
                // 注册固定阈值二值化算法
                var fixedThresholdProcessor = new FixedThresholdProcessor();
                var fixedThresholdParams = fixedThresholdProcessor.GetParameters();
                if (fixedThresholdParams != null)
                {
                    _algorithmProcessors["FixedThreshold"] = fixedThresholdProcessor;
                }
                else
                {
                    throw new InvalidOperationException("固定阈值二值化处理器参数获取失败");
                }
                
                // 注册OTSU自动阈值二值化算法
                var otsuThresholdProcessor = new OTSUThresholdProcessor();
                var otsuThresholdParams = otsuThresholdProcessor.GetParameters();
                if (otsuThresholdParams != null)
                {
                    _algorithmProcessors["OTSUThreshold"] = otsuThresholdProcessor;
                }
                else
                {
                    throw new InvalidOperationException("OTSU自动阈值二值化处理器参数获取失败");
                }
                
                // 注册基于局部方差的自适应阈值二值化算法
                var varThresholdProcessor = new VarThresholdProcessor();
                var varThresholdParams = varThresholdProcessor.GetParameters();
                if (varThresholdParams != null)
                {
                    _algorithmProcessors["VarThreshold"] = varThresholdProcessor;
                }
                else
                {
                    throw new InvalidOperationException("基于局部方差的自适应阈值二值化处理器参数获取失败");
                }
                
                // 注册基于局部统计的自适应阈值二值化算法
                var dynThresholdProcessor = new DynThresholdProcessor();
                var dynThresholdParams = dynThresholdProcessor.GetParameters();
                if (dynThresholdParams != null)
                {
                    _algorithmProcessors["DynThreshold"] = dynThresholdProcessor;
                }
                else
                {
                    throw new InvalidOperationException("基于局部统计的自适应阈值二值化处理器参数获取失败");
                }
                
                // 注册形态学腐蚀算法
                var erosionProcessor = new ErosionProcessor();
                var erosionParams = erosionProcessor.GetParameters();
                if (erosionParams != null)
                {
                    _algorithmProcessors["MorphologyErosion"] = erosionProcessor;
                }
                else
                {
                    throw new InvalidOperationException("形态学腐蚀处理器参数获取失败");
                }
                
                // 注册形态学膨胀算法
                var dilationProcessor = new DilationProcessor();
                var dilationParams = dilationProcessor.GetParameters();
                if (dilationParams != null)
                {
                    _algorithmProcessors["MorphologyDilation"] = dilationProcessor;
                }
                else
                {
                    throw new InvalidOperationException("形态学膨胀处理器参数获取失败");
                }
                
                // 注册形态学开运算算法
                var openingProcessor = new OpeningProcessor();
                var openingParams = openingProcessor.GetParameters();
                if (openingParams != null)
                {
                    _algorithmProcessors["MorphologyOpening"] = openingProcessor;
                }
                else
                {
                    throw new InvalidOperationException("形态学开运算处理器参数获取失败");
                }
                
                // 注册形态学闭运算算法
                var closingProcessor = new ClosingProcessor();
                var closingParams = closingProcessor.GetParameters();
                if (closingParams != null)
                {
                    _algorithmProcessors["MorphologyClosing"] = closingProcessor;
                }
                else
                {
                    throw new InvalidOperationException("形态学闭运算处理器参数获取失败");
                }
                
                // 注册直方图均衡算法
                var histogramEqualizationProcessor = new HistogramEqualizationProcessor();
                var histogramEqualizationParams = histogramEqualizationProcessor.GetParameters();
                if (histogramEqualizationParams != null)
                {
                    _algorithmProcessors["HistogramEqualization"] = histogramEqualizationProcessor;
                }
                else
                {
                    throw new InvalidOperationException("直方图均衡处理器参数获取失败");
                }
                
                // 注册圆形卡尺算法
                var circleCaliperProcessor = new CircleCaliperProcessor();
                var circleCaliperParams = circleCaliperProcessor.GetParameters();
                if (circleCaliperParams != null)
                {
                    _algorithmProcessors["CircleCaliper"] = circleCaliperProcessor;
                }
                else
                {
                    throw new InvalidOperationException("圆形卡尺处理器参数获取失败");
                }
                
                // 注册直线卡尺算法
                var lineCaliperProcessor = new LineCaliperProcessor();
                var lineCaliperParams = lineCaliperProcessor.GetParameters();
                if (lineCaliperParams != null)
                {
                    _algorithmProcessors["LineCaliper"] = lineCaliperProcessor;
                }
                else
                {
                    throw new InvalidOperationException("直线卡尺处理器参数获取失败");
                }
                
                // 注册九点标定算法
                var ninePointCalibrationProcessor = new NinePointCalibrationProcessor();
                var ninePointCalibrationParams = ninePointCalibrationProcessor.GetParameters();
                if (ninePointCalibrationParams != null)
                {
                    _algorithmProcessors["NinePointCalibration"] = ninePointCalibrationProcessor;
                }
                else
                {
                    throw new InvalidOperationException("九点标定处理器参数获取失败");
                }

                // 注册灰度匹配处理器
                var grayValueMatchingProcessor = new GrayValueMatchingProcessor();
                var grayValueMatchingParams = grayValueMatchingProcessor.GetParameters();
                if (grayValueMatchingParams != null)
                {
                    _algorithmProcessors["GrayValueMatching"] = grayValueMatchingProcessor;
                }
                else
                {
                    throw new InvalidOperationException("灰度匹配处理器参数获取失败");
                }

                // 注册形状匹配处理器
                var shapeMatchingProcessor = new ShapeMatchingProcessor();
                var shapeMatchingParams = shapeMatchingProcessor.GetParameters();
                if (shapeMatchingParams != null)
                {
                    _algorithmProcessors["ShapeMatching"] = shapeMatchingProcessor;
                }
                else
                {
                    throw new InvalidOperationException("形状匹配处理器参数获取失败");
                }

                // 注册Canny边缘检测处理器
                var cannyProcessor = new CannyEdgeDetector();
                var cannyParams = cannyProcessor.GetParameters();
                if (cannyParams != null)
                {
                    _algorithmProcessors["CannyEdge"] = cannyProcessor;
                }
                else
                {
                    throw new InvalidOperationException("Canny边缘检测处理器参数获取失败");
                }

                // 注册Sobel边缘检测处理器
                var sobelProcessor = new SobelEdgeDetector();
                var sobelParams = sobelProcessor.GetParameters();
                if (sobelParams != null)
                {
                    _algorithmProcessors["SobelEdge"] = sobelProcessor;
                }
                else
                {
                    throw new InvalidOperationException("Sobel边缘检测处理器参数获取失败");
                }

                // 注册Laplacian边缘检测处理器
                var laplacianProcessor = new LaplacianEdgeDetector();
                var laplacianParams = laplacianProcessor.GetParameters();
                if (laplacianParams != null)
                {
                    _algorithmProcessors["LaplacianEdge"] = laplacianProcessor;
                }
                else
                {
                    throw new InvalidOperationException("Laplacian边缘检测处理器参数获取失败");
                }

                // 后续可以通过反射自动加载所有算法
                // LoadAllProcessorsByReflection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化算法失败: {ex.Message}\n\n详细信息: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化ROI管理器
        /// </summary>
        private void InitializeROIManager()
        {
            // 初始化ROI管理器（使用可编辑的HDrawingObject）
            _roiManager = new ImageMatchingROIManager();
            _roiManager.TemplateROICompleted += OnTemplateROICompleted;
            _roiManager.SearchROICompleted += OnSearchROICompleted;
            _roiManager.ROIUpdated += OnROIUpdated;

            // 绑定参数面板的ROI相关事件
            AlgorithmParameterPanel.ROIModeChanged += OnParameterPanelROIModeChanged;
            AlgorithmParameterPanel.SwitchToSearchROIRequested += OnSwitchToSearchROIRequested;
            AlgorithmParameterPanel.SwitchToTemplateROIRequested += OnSwitchToTemplateROIRequested;
            AlgorithmParameterPanel.ParametersApplied += OnParametersApplied;
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// </summary>
        private void VisionToolWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeEvents();
        }
        
        /// <summary>
        /// 初始化事件处理
        /// </summary>
        private void InitializeEvents()
        {
            try
            {
                // 参数面板事件
                if (AlgorithmParameterPanel != null)
                {
                    AlgorithmParameterPanel.ParameterChanged += OnParameterChanged;
                    AlgorithmParameterPanel.ParametersApplied += OnParametersApplied;
                }
                
                // Halcon显示控件事件绑定
                if (HalconDisplay != null)
                {
                    // HDrawingObject交互将在需要时初始化
                    // 初始化ROI管理器的Halcon窗口
                    _roiManager?.Initialize(HalconDisplay.HalconWindow);
                }
                
                // 窗口关闭事件
                this.Closing += OnWindowClosing;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化事件处理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
        
        #region 事件处理方法
        
        /// <summary>
        /// 加载图像按钮点击
        /// </summary>
        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图像文件",
                Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|" +
                        "BMP图像|*.bmp|" +
                        "JPEG图像|*.jpg;*.jpeg|" +
                        "PNG图像|*.png|" +
                        "TIFF图像|*.tif;*.tiff|" +
                        "所有文件|*.*",
                FilterIndex = 1
            };
            
            if (dialog.ShowDialog() == true)
            {
                LoadImage(dialog.FileName);
            }
        }
        
        /// <summary>
        /// 执行算法按钮点击
        /// </summary>
        private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCurrentAlgorithm();
        }
        
        /// <summary>
        /// 清空结果按钮点击
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearResults();
        }
        
        /// <summary>
        /// 保存结果按钮点击
        /// </summary>
        private void SaveResultButton_Click(object sender, RoutedEventArgs e)
        {
            SaveResult();
        }
        
        /// <summary>
        /// 算法项双击事件
        /// </summary>
        private void AlgorithmItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item && item.Tag is string algorithmKey)
            {
                // 检查是否为图像匹配算法
                if (IsImageMatchingAlgorithm(algorithmKey))
                {
                    StartImageMatchingROIWorkflow(algorithmKey);
                    return;
                }

                // 对于九点标定，直接打开标定窗口
                if (algorithmKey == "NinePointCalibration")
                {
                    OpenNinePointCalibrationWindow();
                    return;
                }

                SelectAlgorithm(algorithmKey);

                // 对于圆查找算法，初始化交互式圆形卡尺
                if (algorithmKey == "CircleCaliper")
                {
                    InitializeInteractiveCircleCaliper();
                }
                // 对于直线查找算法，初始化交互式直线卡尺
                else if (algorithmKey == "LineCaliper")
                {
                    InitializeInteractiveLineCaliper();
                }
            }
        }
        
        /// <summary>
        /// 参数变化事件
        /// </summary>
        private void OnParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            // 防止在ROI更新过程中再次触发更新
            if (_isUpdatingFromROI) 
            {
                return;
            }
            
            try
            {
                // 首先，确保将参数面板的新值同步到处理器
                if (_currentProcessor != null)
                {
                    _currentProcessor.SetParameter(e.ParameterName, e.NewValue);
                }
                
                // 动态获取当前处理器的实时参数列表
                var realtimeParameters = GetCurrentProcessorRealtimeParameters();
                
                // 处理ROI参数更新（支持圆查找和直线查找）
                if (_interactiveCaliper != null && IsROIParameter(e.ParameterName))
                {
                    UpdateInteractiveCaliper();
                }
                
                // 立即显示卡尺工具的实时预览（参数面板修改）
                if (IsROIParameter(e.ParameterName) && _hImage != null)
                {
                    if (_currentProcessor is CircleCaliperProcessor || _currentProcessor is LineCaliperProcessor)
                    {
                        ShowRealtimeCaliperPreview();
                    }
                }
                
                // 实时预览触发（适用于所有处理器，受用户配置控制）
                if (ShouldTriggerRealtimePreview(e.ParameterName, realtimeParameters) && _originalImage != null)
                {
                    TriggerRealtimePreview(e.ParameterName);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"参数更新失败: {ex.Message}", true);
            }
        }
        
        // 添加一个标志位用于防止递归更新
        private bool _isUpdatingFromROI = false;
        
        // 统一防抖定时器，用于所有实时预览操作
        private System.Windows.Threading.DispatcherTimer _unifiedDebounceTimer;
        
        // 实时预览处理标志，独立于手动执行的处理标志
        private bool _isRealtimeProcessing = false;
        
        // 实时预览级别控制
        private RealtimePreviewLevel _realtimePreviewLevel = RealtimePreviewLevel.All;
        
        // 统一防抖延迟常量
        private const int UNIFIED_DEBOUNCE_DELAY_MS = 50;
        
        #region 实时参数处理辅助方法
        
        /// <summary>
        /// 获取当前处理器的实时参数列表
        /// </summary>
        /// <returns>实时参数名称数组</returns>
        private string[] GetCurrentProcessorRealtimeParameters()
        {
            if (_currentProcessor == null) 
                return new string[0];
                
            try
            {
                return ReflectionCache.GetRealtimeParameters(_currentProcessor.GetType());
            }
            catch (Exception)
            {
                // 记录错误但不中断流程，返回空数组作为降级方案
                return new string[0];
            }
        }
        
        /// <summary>
        /// 检查是否为ROI参数
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否为ROI参数</returns>
        private bool IsROIParameter(string parameterName)
        {
            return parameterName == "CenterRow" || 
                   parameterName == "CenterCol" || 
                   parameterName == "ExpectedRadius" ||
                   parameterName == "StartRow" ||
                   parameterName == "StartCol" ||
                   parameterName == "EndRow" ||
                   parameterName == "EndCol";
        }
        
        /// <summary>
        /// 触发实时预览（使用统一防抖机制）
        /// </summary>
        /// <param name="changedParameter">发生变化的参数名称</param>
        private void TriggerRealtimePreview(string changedParameter = null)
        {
            // 使用统一防抖机制
            TriggerUnifiedDebounce(changedParameter);
        }
        
        /// <summary>
        /// 统一防抖触发器（所有实时预览操作的统一入口）
        /// </summary>
        /// <param name="changedParameter">发生变化的参数名称</param>
        private void TriggerUnifiedDebounce(string changedParameter = null)
        {
            // 标准防抖机制：重用定时器对象
            if (_unifiedDebounceTimer == null)
            {
                // 仅第一次创建定时器，使用构造函数一次性设置所有参数
                _unifiedDebounceTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(UNIFIED_DEBOUNCE_DELAY_MS),
                    System.Windows.Threading.DispatcherPriority.Normal,
                    async (s, args) =>
                    {
                        _unifiedDebounceTimer.Stop();
                        await ExecuteUnifiedDebounceCallback(changedParameter);
                    },
                    Dispatcher);
            }
            else
            {
                // 重用现有定时器，只需停止（延迟固定为50ms）
                _unifiedDebounceTimer.Stop();
            }
            
            // 启动防抖定时器
            _unifiedDebounceTimer.Start();
        }
        
        /// <summary>
        /// 统一防抖回调执行（合并原有的两个回调逻辑）
        /// </summary>
        /// <param name="changedParameter">发生变化的参数名称</param>
        private async Task ExecuteUnifiedDebounceCallback(string changedParameter)
        {
            // 防止并发执行
            if (_isRealtimeProcessing)
            {
                return;
            }
            
            try
            {
                _isRealtimeProcessing = true;
                
                // 检查是否为显示相关的参数（轻量级预览）
                if (IsVisualizationParameter(changedParameter))
                {
                    // 显示参数：仅更新可视化，不重新计算
                    await UpdateVisualizationOnly();
                }
                else
                {
                    // 算法参数：完整重新执行
                    await ExecuteRealtimeAlgorithm();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"实时预览失败: {ex.Message}", true);
            }
            finally
            {
                _isRealtimeProcessing = false;
            }
        }
        
        /// <summary>
        /// 执行实时预览（支持轻量级模式）
        /// </summary>
        /// <param name="changedParameter">发生变化的参数名称</param>
        private async Task ExecuteRealtimePreview(string changedParameter)
        {
            // 检查是否为显示相关的参数（轻量级预览）
            if (IsVisualizationParameter(changedParameter))
            {
                // 显示参数：仅更新可视化，不重新计算
                await UpdateVisualizationOnly();
            }
            else
            {
                // 算法参数：完整重新执行
                await ExecuteCurrentAlgorithm();
            }
        }
        
        /// <summary>
        /// 检查是否为可视化参数（仅影响显示，不影响计算）
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否为可视化参数</returns>
        private bool IsVisualizationParameter(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName))
                return false;
                
            // 显示选项参数，只需更新显示，不需重新计算
            var visualizationParams = new string[]
            {
                "ShowCalipers", "ShowFittedCircle",
                "ShowContours", "ShowResults", "ShowMeasurements"
            };
            
            return visualizationParams.Contains(parameterName);
        }
        
        /// <summary>
        /// 仅更新可视化显示（轻量级预览）
        /// </summary>
        private async Task UpdateVisualizationOnly()
        {
            try
            {
                // 如果有上次的处理结果，重新显示轮廓
                if (_lastProcessResult != null && _lastProcessResult.Success)
                {
                    await Task.Run(() =>
                    {
                        // 在UI线程中更新显示
                        Dispatcher.Invoke(() =>
                        {
                            DisplayHalconContours(_lastProcessResult);
                        });
                    });
                    
                    UpdateStatus($"显示已更新", false);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"更新显示失败: {ex.Message}", true);
            }
        }
        
        // CalculateOptimalDebounceDelay 方法已删除，统一使用 UNIFIED_DEBOUNCE_DELAY_MS = 50ms
        
        /// <summary>
        /// 判断是否应该触发实时预览
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="realtimeParameters">实时参数列表</param>
        /// <returns>是否应该触发实时预览</returns>
        private bool ShouldTriggerRealtimePreview(string parameterName, string[] realtimeParameters)
        {
            // 检查用户设置的实时预览级别
            switch (_realtimePreviewLevel)
            {
                case RealtimePreviewLevel.Disabled:
                    return false; // 禁用所有实时预览
                    
                case RealtimePreviewLevel.Essential:
                    // 仅关键参数：ROI参数和显示参数
                    return IsEssentialParameter(parameterName);
                    
                case RealtimePreviewLevel.All:
                default:
                    // 所有标记为实时的参数
                    return realtimeParameters.Contains(parameterName);
            }
        }
        
        /// <summary>
        /// 检查是否为关键参数（Essential级别时使用）
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否为关键参数</returns>
        private bool IsEssentialParameter(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName))
                return false;
                
            // 关键参数：ROI参数和显示参数
            var essentialParams = new string[]
            {
                // ROI相关参数（用户交互频繁）
                "CenterRow", "CenterCol", "ExpectedRadius",
                "Width", "Height", "X", "Y", "Angle",
                
                // 显示选项（即时视觉反馈重要）
                "ShowCalipers", "ShowFittedCircle",
                "ShowContours", "ShowResults", "ShowMeasurements"
            };
            
            return essentialParams.Contains(parameterName);
        }
        
        /// <summary>
        /// 设置实时预览级别
        /// </summary>
        /// <param name="level">预览级别</param>
        public void SetRealtimePreviewLevel(RealtimePreviewLevel level)
        {
            _realtimePreviewLevel = level;
            
            // 记录设置变化
            var levelDescription = level switch
            {
                RealtimePreviewLevel.Disabled => "禁用",
                RealtimePreviewLevel.Essential => "仅关键参数",
                RealtimePreviewLevel.All => "所有参数",
                _ => "未知"
            };
            
            UpdateStatus($"实时预览级别已设置为: {levelDescription}");
            
            // 如果当前有处理器，记录实时参数信息
            LogRealtimeParameterInfo();
        }
        
        /// <summary>
        /// 获取当前实时预览级别
        /// </summary>
        /// <returns>当前预览级别</returns>
        public RealtimePreviewLevel GetRealtimePreviewLevel()
        {
            return _realtimePreviewLevel;
        }
        
        /// <summary>
        /// 记录实时参数信息（用于调试）
        /// </summary>
        private void LogRealtimeParameterInfo()
        {
            if (_currentProcessor != null)
            {
                var cacheInfo = ReflectionCache.GetCacheInfo(_currentProcessor.GetType());
                var levelInfo = $"当前级别: {_realtimePreviewLevel}";
            }
        }
        
        #endregion
        
        /// <summary>
        /// 更新交互式卡尺ROI
        /// </summary>
        private void UpdateInteractiveCaliper()
        {
            try
            {
                // 设置标志位，防止递归更新
                _isUpdatingFromROI = true;
                
                if (_currentProcessor is CircleCaliperProcessor circleProcessor && _interactiveCaliper != null)
                {
                    // 直接从处理器属性获取最新参数值
                    var centerRow = circleProcessor.CenterRow;
                    var centerCol = circleProcessor.CenterCol;
                    var radius = circleProcessor.ExpectedRadius;
                    
                    // 只更新HDrawingObject参数，不重绘整个窗口（优化性能）
                    _interactiveCaliper.SetDrawingObjectParams("row", centerRow);
                    _interactiveCaliper.SetDrawingObjectParams("column", centerCol);
                    _interactiveCaliper.SetDrawingObjectParams("radius", radius);
                    
                    UpdateStatus($"ROI已更新: 圆心({centerRow:F1}, {centerCol:F1}) 半径:{radius:F1}");
                }
                else if (_currentProcessor is LineCaliperProcessor lineProcessor && _interactiveCaliper != null)
                {
                    // 直接从处理器属性获取最新参数值
                    var startRow = lineProcessor.StartRow;
                    var startCol = lineProcessor.StartCol;
                    var endRow = lineProcessor.EndRow;
                    var endCol = lineProcessor.EndCol;
                    
                    // 更新HDrawingObject参数（直线使用row1,column1,row2,column2）
                    _interactiveCaliper.SetDrawingObjectParams("row1", startRow);
                    _interactiveCaliper.SetDrawingObjectParams("column1", startCol);
                    _interactiveCaliper.SetDrawingObjectParams("row2", endRow);
                    _interactiveCaliper.SetDrawingObjectParams("column2", endCol);
                    
                    UpdateStatus($"ROI已更新: 起点({startRow:F1}, {startCol:F1}) 终点({endRow:F1}, {endCol:F1})");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"更新交互式卡尺失败: {ex.Message}", true);
            }
            finally
            {
                // 清除标志位
                _isUpdatingFromROI = false;
            }
        }
        
        /// <summary>
        /// 切换到搜索ROI请求事件
        /// </summary>
        private void OnSwitchToSearchROIRequested()
        {
            _roiManager?.SwitchToSearchROI();
        }

        /// <summary>
        /// 切换到模板ROI请求事件
        /// </summary>
        private void OnSwitchToTemplateROIRequested()
        {
            _roiManager?.SwitchToTemplateROI();
        }

        /// <summary>
        /// 参数应用事件
        /// </summary>
        private async void OnParametersApplied(object sender, EventArgs e)
        {
            // 应用所有ROI设置
            _roiManager?.ApplyAllROI();

            // 直接设置处理器的ROI数据（确保数据传递）
            if (_currentProcessor is ImageMatchingProcessorBase processor && _roiManager != null)
            {
                if (_roiManager.TemplateROI != null)
                {
                    processor.TemplateROI = _roiManager.TemplateROI;
                }
                if (_roiManager.SearchROI != null)
                {
                    processor.SearchROI = _roiManager.SearchROI;
                }
            }

            // 参数应用后自动执行算法
            if (_originalImage != null && _currentProcessor != null)
            {
                await ExecuteCurrentAlgorithm();
            }
        }
        
        /// <summary>
        /// 图像点击事件
        /// </summary>
        private void OnImageClicked(object sender, ImageClickEventArgs e)
        {
            // 可以在此处添加图像交互功能，如ROI选择等
            UpdateStatus($"图像坐标: ({e.X:F1}, {e.Y:F1})");
        }
        
        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 清理统一防抖定时器
                if (_unifiedDebounceTimer != null)
                {
                    _unifiedDebounceTimer.Stop();
                    _unifiedDebounceTimer = null;
                }
                
                // 释放图像资源
                _originalImage?.Dispose();
                _resultImage?.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus($"释放资源失败: {ex.Message}", true);
            }
        }
        
        #endregion
        
        #region 九点标定方法
        
        /// <summary>
        /// 打开九点标定窗口
        /// </summary>
        private void OpenNinePointCalibrationWindow()
        {
            try
            {
                var calibrationWindow = new NinePointCalibrationWindow();
                calibrationWindow.Owner = this;
                calibrationWindow.ShowDialog();
                
                UpdateStatus("九点标定窗口已打开");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开九点标定窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("打开九点标定窗口失败", true);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 加载图像
        /// </summary>
        /// <param name="imagePath">图像路径</param>
        private void LoadImage(string imagePath)
        {
            try
            {
                UpdateStatus("正在加载图像...");
                ProcessingProgressBar.Visibility = Visibility.Visible;
                ProcessingProgressBar.IsIndeterminate = true;
                
                // 释放之前的图像
                _originalImage?.Dispose();
                _resultImage?.Dispose();
                _hImage?.Dispose();
                _hResultImage?.Dispose();
                
                // 加载新图像
                _originalImage = VisionImage.FromFile(imagePath);
                _hImage = _originalImage.HImage.Clone();
                
                // 显示图像到Halcon控件
                if (OriginalImageMode.IsChecked == true)
                {
                    HalconDisplay.HalconWindow.DispObj(_hImage);
                    HalconDisplay.HalconWindow.SetPart(0, 0, -1, -1);
                }
                
                // 清空结果信息
                ClearResultInfo();
                
                // 更新界面状态
                UpdateImageInfo();
                UpdateStatus($"图像加载成功: {Path.GetFileName(imagePath)}");

                // 如果处于图像匹配模式，更新ROI管理器的当前图像
                if (_isImageMatchingMode && _roiManager != null)
                {
                    _roiManager.SetCurrentImage(_originalImage);
                }

                // 启用执行按钮（如果有选中的算法）
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("图像加载失败");
            }
            finally
            {
                ProcessingProgressBar.Visibility = Visibility.Collapsed;
                ProcessingProgressBar.IsIndeterminate = false;
            }
        }
        
        /// <summary>
        /// 选择算法
        /// </summary>
        /// <param name="algorithmKey">算法键</param>
        private void SelectAlgorithm(string algorithmKey)
        {
            try
            {
                
                if (_algorithmProcessors.TryGetValue(algorithmKey, out var processor))
                {

                    // 算法切换时自动清理之前的状态
                    ResetToInitialState();

                    _currentProcessor = processor;
                    CurrentAlgorithmText.Text = processor.ProcessorName;

                    // 在ResetToInitialState之后设置当前算法键
                    _currentAlgorithmKey = algorithmKey;
                    
                    
                    // 设置参数面板
                    if (AlgorithmParameterPanel != null)
                    {
                        AlgorithmParameterPanel.SetProcessor(processor);
                    }
                    
                    
                    // 更新按钮状态
                    UpdateButtonStates();
                    
                    UpdateStatus($"已选择算法: {processor.ProcessorName}");
                }
                else
                {
                    MessageBox.Show("该算法尚未实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择算法失败: {ex.Message}\n\n堆栈跟踪: {ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 执行当前算法
        /// </summary>
        private async Task ExecuteCurrentAlgorithm()
        {
            if (_isProcessing || _currentProcessor == null || _originalImage == null)
                return;
                
            try
            {
                _isProcessing = true;
                UpdateStatus("正在执行算法...");
                ProcessingProgressBar.Visibility = Visibility.Visible;
                ProcessingProgressBar.IsIndeterminate = true;
                UpdateButtonStates();
                
                var startTime = DateTime.Now;
                
                // 应用参数面板的设置到处理器
                AlgorithmParameterPanel.ApplyParametersToProcessor();
                
                // 对于圆形查找算法，从HDrawingObject获取参数
                if (_currentProcessor is CircleCaliperProcessor circleProcessor && _interactiveCaliper != null)
                {
                    try
                    {
                        // 从HDrawingObject获取参数
                        HTuple param = _interactiveCaliper.GetDrawingObjectParams("row");
                        double row = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column");
                        double col = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("radius");
                        double radius = param.D;
                        
                        // 设置CircleCaliperProcessor的参数
                        circleProcessor.SetParameter("CenterRow", row);
                        circleProcessor.SetParameter("CenterCol", col);
                        circleProcessor.SetParameter("ExpectedRadius", radius);
                        
                        UpdateStatus($"圆形参数: 中心({row:F1},{col:F1}) 半径:{radius:F1}");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"获取圆形参数失败: {ex.Message}");
                    }
                }
                
                // 对于直线查找算法，从HDrawingObject获取参数
                if (_currentProcessor is LineCaliperProcessor lineProcessor && _interactiveCaliper != null)
                {
                    try
                    {
                        // 从HDrawingObject获取直线参数
                        HTuple param = _interactiveCaliper.GetDrawingObjectParams("row1");
                        double row1 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column1");
                        double col1 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("row2");
                        double row2 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column2");
                        double col2 = param.D;
                        
                        // 设置LineCaliperProcessor的参数
                        lineProcessor.SetParameter("StartRow", row1);
                        lineProcessor.SetParameter("StartCol", col1);
                        lineProcessor.SetParameter("EndRow", row2);
                        lineProcessor.SetParameter("EndCol", col2);
                        
                        UpdateStatus($"直线参数: 起点({row1:F1},{col1:F1}) 终点({row2:F1},{col2:F1})");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"获取直线参数失败: {ex.Message}");
                    }
                }
                
                // 执行算法
                ProcessResult result;
                
                result = await _currentProcessor.ProcessAsync(_originalImage);
                
                var endTime = DateTime.Now;
                var processingTime = endTime - startTime;
                
                if (result.Success)
                {
                    // 保存处理结果用于重新显示轮廓
                    _lastProcessResult = result;
                    
                    // 释放之前的结果图像
                    _resultImage?.Dispose();
                    _hResultImage?.Dispose();
                    _resultImage = result.OutputImage;
                    _hResultImage = _resultImage.HImage.Clone();
                    
                    // 无论什么模式，都要先清除之前的显示内容，防止重叠
                    HalconDisplay.HalconWindow.ClearWindow();
                    
                    // 显示结果图像
                    if (ResultImageMode.IsChecked == true)
                    {
                        // 显示处理后的图像
                        HalconDisplay.HalconWindow.DispObj(_hResultImage);
                        // 不调用SetPart，保持用户设置的显示区域
                        
                        // 显示Halcon原生轮廓（如果是圆形卡尺结果）
                        DisplayHalconContours(result);
                        
                        // 绘制几何元素（用于其他算法的兼容）
                        DrawGeometryElements(result.GeometryElements);
                    }
                    else
                    {
                        // 显示原始图像
                        HalconDisplay.HalconWindow.DispObj(_hImage);
                        // 不调用SetPart，保持用户设置的显示区域
                        
                        // 显示Halcon原生轮廓（如果是圆形卡尺结果）
                        DisplayHalconContours(result);
                        
                        // 绘制几何元素（用于其他算法的兼容）
                        DrawGeometryElements(result.GeometryElements);
                    }
                    
                    // 显示结果信息
                    DisplayResultInfo(result);
                    
                    // 切换到结果显示模式
                    ResultImageMode.IsChecked = true;
                    
                    UpdateStatus("算法执行成功");
                    ProcessingTimeText.Text = $"总响应时间: {processingTime.TotalMilliseconds:F2}ms";
                    
                    SaveResultButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show($"算法执行失败: {result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("算法执行失败");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行算法时发生异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("算法执行异常");
            }
            finally
            {
                _isProcessing = false;
                ProcessingProgressBar.Visibility = Visibility.Collapsed;
                ProcessingProgressBar.IsIndeterminate = false;
                UpdateButtonStates();
            }
        }
        
        /// <summary>
        /// 清空结果
        /// </summary>
        private void ClearResults()
        {
            _resultImage?.Dispose();
            _hResultImage?.Dispose();
            _resultImage = null;
            _hResultImage = null;
            _lastProcessResult = null; // 确保清除上一次结果
            
            ClearResultInfo();
            
            // 切换到原始图像模式
            OriginalImageMode.IsChecked = true;
            if (_hImage != null)
            {
                HalconDisplay.HalconWindow.DispObj(_hImage);
                // 保持用户当前的显示区域，不自动调用SetPart(0,0,-1,-1)
            }
            
            SaveResultButton.IsEnabled = false;
            ProcessingTimeText.Text = "";
            
            UpdateStatus("已清空结果");
        }
        
        /// <summary>
        /// 保存结果
        /// </summary>
        private void SaveResult()
        {
            if (_resultImage == null) return;
            
            try
            {
                // 自动保存到指定路径
                SaveResultToDefaultPath();
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存结果失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 保存结果图像到默认路径
        /// </summary>
        private void SaveResultToDefaultPath()
        {
            // 默认保存路径
            string saveDirectory = @"D:\VisionLite图像保存\ALG";
            
            // 检查并创建目录
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
            
            // 生成文件名：ALG_年月日_时分秒.bmp 格式
            string fileName = $"ALG_{DateTime.Now:yyyyMMdd_HHmmss}.bmp";
            string fullPath = Path.Combine(saveDirectory, fileName);
            
            // 如果文件已存在，添加序号
            int counter = 1;
            while (File.Exists(fullPath))
            {
                fileName = $"ALG_{DateTime.Now:yyyyMMdd_HHmmss}_{counter:00}.bmp";
                fullPath = Path.Combine(saveDirectory, fileName);
                counter++;
            }
            
            // 保存图像
            _resultImage.SaveToFile(fullPath);
            
            // 更新状态栏
            UpdateStatus($"结果已保存: {fileName}");
        }
        
        /// <summary>
        /// 显示结果信息
        /// </summary>
        /// <param name="result">处理结果</param>
        private void DisplayResultInfo(ProcessResult result)
        {
            ResultInfoPanel.Children.Clear();
            NoResultText.Visibility = Visibility.Collapsed;
            
            // 基本信息
            var basicInfoText = new TextBlock
            {
                Text = $"算法: {result.ProcessorName}\n算法执行时间: {result.ProcessingTime.TotalMilliseconds:F2}ms",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ResultInfoPanel.Children.Add(basicInfoText);
            
            // 测量结果
            if (result.Measurements != null && result.Measurements.Count > 0)
            {
                var measurementsHeader = new TextBlock
                {
                    Text = "测量结果:",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                ResultInfoPanel.Children.Add(measurementsHeader);
                
                foreach (var measurement in result.Measurements)
                {
                    var measurementText = new TextBlock
                    {
                        Text = $"  • {measurement.Key}: {measurement.Value}",
                        Margin = new Thickness(10, 0, 0, 2)
                    };
                    ResultInfoPanel.Children.Add(measurementText);
                }
            }
            
            // 几何元素
            if (result.GeometryElements != null && result.GeometryElements.Count > 0)
            {
                // 【关键修改】在显示之前，从列表中过滤掉所有的 PointElement
                var elementsToReport = result.GeometryElements.Where(elem => !(elem is PointElement)).ToList();

                // 只在有需要报告的元素时，才显示标题和内容
                if (elementsToReport.Any())
                {
                    var geometryHeader = new TextBlock
                    {
                        Text = $"检测到的几何元素 ({elementsToReport.Count}个):",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 5)
                    };
                    ResultInfoPanel.Children.Add(geometryHeader);

                    // 遍历过滤后的列表
                    foreach (var element in elementsToReport)
                    {
                        var elementText = new TextBlock
                        {
                            Text = $"  • {element}",
                            Margin = new Thickness(10, 0, 0, 2)
                        };
                        ResultInfoPanel.Children.Add(elementText);
                    }
                }
            }
        }
        
        /// <summary>
        /// 清空结果信息
        /// </summary>
        private void ClearResultInfo()
        {
            ResultInfoPanel.Children.Clear();
            NoResultText.Visibility = Visibility.Visible;
        }
        
        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            ExecuteButton.IsEnabled = !_isProcessing && _originalImage != null && _currentProcessor != null;
            LoadImageButton.IsEnabled = !_isProcessing;
            SaveResultButton.IsEnabled = !_isProcessing && _resultImage != null;
        }
        
        /// <summary>
        /// 更新状态栏
        /// </summary>
        /// <param name="message">状态消息</param>
        private void UpdateStatus(string message)
        {
            UpdateStatus(message, false);
        }
        
        /// <summary>
        /// 更新状态栏
        /// </summary>
        /// <param name="message">状态消息</param>
        /// <param name="isError">是否为错误消息</param>
        private void UpdateStatus(string message, bool isError)
        {
            StatusText.Text = message;
            
            // 根据是否为错误设置颜色
            StatusText.Foreground = isError ? 
                System.Windows.Media.Brushes.Red : 
                System.Windows.Media.Brushes.Green;
            
            // 3秒后恢复默认状态
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                if (StatusText.Text == message) // 只有当前消息没有被其他操作覆盖时才恢复
                {
                    StatusText.Text = "就绪";
                    StatusText.Foreground = System.Windows.Media.Brushes.Black;
                }
                timer.Stop();
            };
            timer.Start();
        }
        
        /// <summary>
        /// 更新图像信息
        /// </summary>
        private void UpdateImageInfo()
        {
            if (_originalImage != null)
            {
                ImageInfoText.Text = $"图像: {_originalImage.Width}×{_originalImage.Height}×{_originalImage.Channels}";
            }
            else
            {
                ImageInfoText.Text = "";
            }
        }
        
        /// <summary>
        /// 参数面板参数变化事件处理
        /// </summary>
        private void ParametersPanel_ParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            try
            {
                // 参数实时变化时可以在这里处理，比如实时预览
                // 暂时只记录状态
                UpdateStatus($"参数 {e.ParameterName} 已更改为 {e.NewValue}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"参数更改失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 参数面板应用按钮点击事件处理
        /// </summary>
        private void ParametersPanel_ParametersApplied(object sender, EventArgs e)
        {
            try
            {
                UpdateStatus("参数已应用");
                // 如果需要，可以在这里触发算法重新执行
            }
            catch (Exception ex)
            {
                UpdateStatus($"应用参数失败: {ex.Message}");
            }
        }
        
        
        /// <summary>
        /// 适应窗口按钮点击事件
        /// </summary>
        private void FitImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hImage != null)
                {
                    HalconDisplay.HalconWindow.SetPart(0, 0, -1, -1);
                    UpdateStatus("图像已适应窗口大小");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"适应窗口失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 显示模式切换事件处理
        /// </summary>
        private void DisplayMode_Changed(object sender, RoutedEventArgs e)
        {

            // 如果 HalconDisplay 控件或其内部的 HalconWindow 尚未初始化，则不执行任何操作。
            if (HalconDisplay == null || !HalconDisplay.IsLoaded || HalconDisplay.HalconWindow == null)
            {
                return;
            }
            try
            {
                // 根据显示模式显示相应的图像
                if (OriginalImageMode.IsChecked == true && _hImage != null)
                {
                    HalconDisplay.HalconWindow.ClearWindow();
                    HalconDisplay.HalconWindow.DispObj(_hImage);
                    // 保持用户当前的显示区域
                }
                else if (ResultImageMode.IsChecked == true && _hResultImage != null)
                {
                    HalconDisplay.HalconWindow.ClearWindow();
                    HalconDisplay.HalconWindow.DispObj(_hResultImage);
                    // 保持用户当前的显示区域
                    
                    // 重新显示轮廓（如果有最近的处理结果）
                    if (_lastProcessResult != null && _lastProcessResult.Success)
                    {
                        DisplayHalconContours(_lastProcessResult);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"切换显示模式失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 执行实时算法预览（轻量级版本，不阻塞手动执行）
        /// </summary>
        private async Task ExecuteRealtimeAlgorithm(int timerId = 0)
        {
            if (_currentProcessor == null || _originalImage == null)
                return;
                
            try
            {
                var algorithmStartTime = DateTime.Now;
                
                // 应用参数面板的设置到处理器
                var applyStartTime = DateTime.Now;
                AlgorithmParameterPanel.ApplyParametersToProcessor();
                var applyTime = (DateTime.Now - applyStartTime).TotalMilliseconds;
                
                // 对于圆形查找算法，从HDrawingObject获取参数
                if (_currentProcessor is CircleCaliperProcessor circleProcessor && _interactiveCaliper != null)
                {
                    try
                    {
                        var roiParamStartTime = DateTime.Now;
                        // 从HDrawingObject获取参数
                        HTuple param = _interactiveCaliper.GetDrawingObjectParams("row");
                        double row = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column");
                        double col = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("radius");
                        double radius = param.D;
                        
                        // 设置CircleCaliperProcessor的参数
                        circleProcessor.SetParameter("CenterRow", row);
                        circleProcessor.SetParameter("CenterCol", col);
                        circleProcessor.SetParameter("ExpectedRadius", radius);
                        
                        var roiParamTime = (DateTime.Now - roiParamStartTime).TotalMilliseconds;
                    }
                    catch (Exception)
                    {
                    }
                }
                
                // 对于直线查找算法，从HDrawingObject获取参数
                if (_currentProcessor is LineCaliperProcessor lineProcessor && _interactiveCaliper != null)
                {
                    try
                    {
                        // 从HDrawingObject获取直线参数
                        HTuple param = _interactiveCaliper.GetDrawingObjectParams("row1");
                        double row1 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column1");
                        double col1 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("row2");
                        double row2 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column2");
                        double col2 = param.D;
                        
                        // 设置LineCaliperProcessor的参数
                        lineProcessor.SetParameter("StartRow", row1);
                        lineProcessor.SetParameter("StartCol", col1);
                        lineProcessor.SetParameter("EndRow", row2);
                        lineProcessor.SetParameter("EndCol", col2);
                    }
                    catch (Exception)
                    {
                        // 忽略参数获取异常
                    }
                }
                
                // 执行算法
                var processStartTime = DateTime.Now;
                ProcessResult result = await _currentProcessor.ProcessAsync(_originalImage);
                var processTime = (DateTime.Now - processStartTime).TotalMilliseconds;
                
                if (result.Success)
                {
                    var displayStartTime = DateTime.Now;
                    
                    // 保存处理结果用于重新显示轮廓
                    _lastProcessResult = result;
                    
                    // 释放之前的结果图像
                    _resultImage?.Dispose();
                    _hResultImage?.Dispose();
                    _resultImage = result.OutputImage;
                    _hResultImage = _resultImage.HImage.Clone();
                    
                    var imageSetupTime = (DateTime.Now - displayStartTime).TotalMilliseconds;
                    
                    // 无论什么模式，都要先清除之前的显示内容，防止重叠
                    var clearStartTime = DateTime.Now;
                    HalconDisplay.HalconWindow.ClearWindow();
                    var clearTime = (DateTime.Now - clearStartTime).TotalMilliseconds;
                    
                    // 显示结果图像
                    var dispStartTime = DateTime.Now;
                    if (ResultImageMode.IsChecked == true)
                    {
                        // 显示处理后的图像
                        HalconDisplay.HalconWindow.DispObj(_hResultImage);
                    }
                    else
                    {
                        // 显示原始图像
                        HalconDisplay.HalconWindow.DispObj(_hImage);
                    }
                    var dispTime = (DateTime.Now - dispStartTime).TotalMilliseconds;
                    
                    // 显示Halcon原生轮廓（如果是圆形卡尺结果）
                    var contourStartTime = DateTime.Now;
                    DisplayHalconContours(result);
                    var contourTime = (DateTime.Now - contourStartTime).TotalMilliseconds;
                    
                    // 绘制几何元素（用于其他算法的兼容）
                    DrawGeometryElements(result.GeometryElements);
                    
                    
                    // 显示结果信息
                    DisplayResultInfo(result);
                    
                    // 切换到结果显示模式
                    ResultImageMode.IsChecked = true;
                    
                    var totalAlgorithmTime = (DateTime.Now - algorithmStartTime).TotalMilliseconds;
                }
                else
                {
                }
            }
            catch (Exception)
            {
            }
        }
        
        /// <summary>
        /// 重置到初始状态（算法切换时调用）
        /// </summary>
        private void ResetToInitialState()
        {
            try
            {
                // 1. 清除处理结果
                _resultImage?.Dispose();
                _resultImage = null;
                _hResultImage?.Dispose();
                _hResultImage = null;
                _lastProcessResult = null;
                
                // 2. 清除交互式ROI
                if (_interactiveCaliper != null)
                {
                    try
                    {
                        HalconDisplay.HalconWindow.DetachDrawingObjectFromWindow(_interactiveCaliper);
                        _interactiveCaliper.Dispose();
                    }
                    catch (Exception)
                    {
                        // ROI清理失败不是致命错误
                    }
                    _interactiveCaliper = null;
                }

                // 3. 清除图像匹配ROI管理器
                if (_roiManager != null)
                {
                    try
                    {
                        _roiManager.ClearAllROI();
                        System.Diagnostics.Debug.WriteLine("已清理图像匹配ROI管理器");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清理ROI管理器失败: {ex.Message}");
                    }
                }
                
                // 4. 切换到原始图像模式
                OriginalImageMode.IsChecked = true;
                if (_hImage != null)
                {
                    HalconDisplay.HalconWindow.DispObj(_hImage);
                }

                // 5. 清除结果信息面板
                ClearResultInfo();

                // 6. 重置UI状态
                SaveResultButton.IsEnabled = false;
                ProcessingTimeText.Text = "";

                // 7. 重置当前算法记录
                _currentAlgorithmKey = null;
                _isImageMatchingMode = false;

                // 8. 停止并清理统一防抖定时器
                if (_unifiedDebounceTimer != null)
                {
                    _unifiedDebounceTimer.Stop();
                }
                _isRealtimeProcessing = false;

                UpdateStatus("已切换算法，状态已重置");
            }
            catch (Exception ex)
            {
                UpdateStatus($"重置状态失败: {ex.Message}", true);
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 获取当前算法键
        /// </summary>
        /// <returns>算法键</returns>
        private string GetCurrentAlgorithmKey()
        {
            if (_currentProcessor == null) return null;
            
            // 通过处理器名称反推算法键
            foreach (var kvp in _algorithmProcessors)
            {
                if (kvp.Value == _currentProcessor)
                {
                    return kvp.Key;
                }
            }
            return null;
        }
        
        #region HDrawingObject交互系统
        

        /// <summary>
        /// 初始化交互式圆形卡尺
        /// </summary>
        private void InitializeInteractiveCircleCaliper()
        {
            if (_hImage == null) return;

            // 清理之前的交互对象
            CleanupInteractiveCaliper();

            try
            {
                // 获取图像尺寸设定初始参数
                HOperatorSet.GetImageSize(_hImage, out HTuple imageWidth, out HTuple imageHeight);
                double centerRow = imageHeight[0].D / 2.0;
                double centerCol = imageWidth[0].D / 2.0;
                double initialRadius = Math.Min(imageHeight[0].D, imageWidth[0].D) * 0.1;

                // 同步动态计算的参数到处理器
                if (_currentProcessor is CircleCaliperProcessor processor)
                {
                    processor.CenterRow = centerRow;
                    processor.CenterCol = centerCol;
                    processor.ExpectedRadius = initialRadius;
                }

                // 同步动态计算的参数到参数面板(静默更新，不触发事件)
                AlgorithmParameterPanel.UpdateParameterValueSilently("CenterRow", centerRow);
                AlgorithmParameterPanel.UpdateParameterValueSilently("CenterCol", centerCol);
                AlgorithmParameterPanel.UpdateParameterValueSilently("ExpectedRadius", initialRadius);

                // 创建圆形HDrawingObject
                _interactiveCaliper = HDrawingObject.CreateDrawingObject(
                    HDrawingObject.HDrawingObjectType.CIRCLE,
                    centerRow, centerCol, initialRadius);

                // 设置样式 - 红色圆圈，便于识别
                _interactiveCaliper.SetDrawingObjectParams("color", "red");
                _interactiveCaliper.SetDrawingObjectParams("line_width", 2);
                _interactiveCaliper.SetDrawingObjectParams("marker_size", 15);

                // 附加到窗口
                HalconDisplay.HalconWindow.AttachDrawingObjectToWindow(_interactiveCaliper);

                // 订阅HDrawingObject事件
                _interactiveCaliper.OnDrag(OnCircleCaliperDragging);      // 拖动中：只更新参数
                _interactiveCaliper.OnResize(OnCircleCaliperDragging);    // 大小调整中：只更新参数

                UpdateStatus($"交互式圆形卡尺已激活: 中心({centerRow:F1},{centerCol:F1}) 半径:{initialRadius:F1}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化交互式圆形卡尺失败: {ex.Message}", true);
            }
        }

        /// <summary>
        /// 初始化交互式直线卡尺
        /// </summary>
        private void InitializeInteractiveLineCaliper()
        {
            if (_hImage == null) return;

            // 清理之前的交互对象
            CleanupInteractiveCaliper();

            try
            {
                // 获取图像尺寸设定初始参数
                HOperatorSet.GetImageSize(_hImage, out HTuple imageWidth, out HTuple imageHeight);
                double centerRow = imageHeight[0].D / 2.0;
                double centerCol = imageWidth[0].D / 2.0;
                double lineLength = Math.Min(imageHeight[0].D, imageWidth[0].D) * 0.3;

                // 计算默认直线的起点和终点（水平方向）
                double startRow = centerRow;
                double startCol = centerCol - lineLength / 2;
                double endRow = centerRow;
                double endCol = centerCol + lineLength / 2;

                // 同步动态计算的参数到处理器
                if (_currentProcessor is LineCaliperProcessor processor)
                {
                    processor.StartRow = startRow;
                    processor.StartCol = startCol;
                    processor.EndRow = endRow;
                    processor.EndCol = endCol;
                }

                // 同步动态计算的参数到参数面板(静默更新，不触发事件)
                AlgorithmParameterPanel.UpdateParameterValueSilently("起点Row", startRow);
                AlgorithmParameterPanel.UpdateParameterValueSilently("起点Col", startCol);
                AlgorithmParameterPanel.UpdateParameterValueSilently("终点Row", endRow);
                AlgorithmParameterPanel.UpdateParameterValueSilently("终点Col", endCol);

                // 创建直线HDrawingObject
                _interactiveCaliper = HDrawingObject.CreateDrawingObject(
                    HDrawingObject.HDrawingObjectType.LINE,
                    startRow, startCol, endRow, endCol);

                // 设置样式 - 红色直线，便于识别
                _interactiveCaliper.SetDrawingObjectParams("color", "red");
                _interactiveCaliper.SetDrawingObjectParams("line_width", 2);
                _interactiveCaliper.SetDrawingObjectParams("marker_size", 15);

                // 附加到窗口
                HalconDisplay.HalconWindow.AttachDrawingObjectToWindow(_interactiveCaliper);

                // 订阅HDrawingObject事件
                _interactiveCaliper.OnDrag(OnLineCaliperDragging);      // 拖动中：只更新参数
                _interactiveCaliper.OnResize(OnLineCaliperDragging);    // 大小调整中：只更新参数
                _interactiveCaliper.OnSelect(OnLineCaliperUpdate);      // 拖动结束：执行算法

                // 初始显示卡尺预览
                ShowRealtimeCaliperPreview();

                UpdateStatus($"交互式直线卡尺已激活: 起点({startRow:F1},{startCol:F1}) 终点({endRow:F1},{endCol:F1})");
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化交互式直线卡尺失败: {ex.Message}", true);
            }
        }

        /// <summary>
        /// 卡尺更新回调
        /// </summary>
        private void OnCaliperUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 获取当前卡尺参数
                    HTuple row = _interactiveCaliper.GetDrawingObjectParams("row");
                    HTuple col = _interactiveCaliper.GetDrawingObjectParams("column");
                    HTuple phi = _interactiveCaliper.GetDrawingObjectParams("phi");
                    HTuple len1 = _interactiveCaliper.GetDrawingObjectParams("length1");
                    HTuple len2 = _interactiveCaliper.GetDrawingObjectParams("length2");

                    // 实时预览已移除边缘检测功能
                }
                catch (Exception ex)
                {
                    UpdateStatus($"更新卡尺失败: {ex.Message}", true);
                }
            });
        }

        // 调试信息：ROI更新计数器
        private int _roiUpdateCount = 0;
        private DateTime _lastRoiUpdateTime = DateTime.MinValue;
        
        
        

        // ROI防抖机制已统一到_unifiedDebounceTimer
        // private System.Windows.Threading.DispatcherTimer _roiDebounceTimer;  // 已删除，使用统一防抖
        // private const int DEBOUNCE_DELAY_MS = 50;  // 已合并到UNIFIED_DEBOUNCE_DELAY_MS
        
        /// <summary>
        /// 圆形卡尺拖动回调（标准防抖机制）
        /// </summary>
        private void OnCircleCaliperDragging(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 防止递归更新
            if (_isUpdatingFromROI) return;
            
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 立即更新ROI参数
                    UpdateCircleCaliperParameters(dobj);
                    
                    // 立即显示卡尺工具的实时预览（只显示卡尺位置，不执行算法）
                    ShowRealtimeCaliperPreview();
                    
                    // 使用统一防抖机制触发算法执行
                    TriggerUnifiedDebounce("ROI_Dragging");
                    
                }
                catch (Exception)
                {
                }
            });
        }
        
        /// <summary>
        /// 更新圆形卡尺参数
        /// </summary>
        private void UpdateCircleCaliperParameters(HDrawingObject dobj)
        {
            // 获取当前圆形参数
            HTuple row = dobj.GetDrawingObjectParams("row");
            HTuple col = dobj.GetDrawingObjectParams("column");
            HTuple radius = dobj.GetDrawingObjectParams("radius");

            // 更新处理器参数
            if (_currentProcessor is CircleCaliperProcessor processor)
            {
                processor.CenterRow = row.D;
                processor.CenterCol = col.D;
                processor.ExpectedRadius = radius.D;
            }

            // 同步更新参数面板显示（不触发事件）
            AlgorithmParameterPanel.UpdateParameterValueSilently("CenterRow", row.D);
            AlgorithmParameterPanel.UpdateParameterValueSilently("CenterCol", col.D);
            AlgorithmParameterPanel.UpdateParameterValueSilently("ExpectedRadius", radius.D);
        }
        
        /// <summary>
        /// 显示实时卡尺工具预览（不执行算法，只显示卡尺位置）
        /// </summary>
        /// <summary>
        /// 统一的实时卡尺预览方法，支持圆查找和直线查找
        /// </summary>
        private void ShowRealtimeCaliperPreview()
        {
            if (_hImage == null || _currentProcessor == null) return;
            
            try
            {
                // 检查图像对象状态
                if (_hImage.IsInitialized() == false)
                {
                    if (_originalImage != null && _originalImage.HImage != null)
                    {
                        _hImage?.Dispose();
                        _hImage = _originalImage.HImage.Clone();
                    }
                    else
                    {
                        return;
                    }
                }

                // 清除旧内容并显示背景图像
                HalconDisplay.HalconWindow.ClearWindow();
                HalconDisplay.HalconWindow.DispObj(_hImage);

                // 根据处理器类型选择不同的处理逻辑
                if (_currentProcessor is CircleCaliperProcessor circleProcessor)
                {
                    ShowCircleCaliperPreview(circleProcessor);
                }
                else if (_currentProcessor is LineCaliperProcessor lineProcessor)
                {
                    ShowLineCaliperPreview(lineProcessor);
                }
            }
            catch (Exception)
            {
                // 忽略预览显示异常
            }
        }

        /// <summary>
        /// 显示圆卡尺预览
        /// </summary>
        private void ShowCircleCaliperPreview(CircleCaliperProcessor processor)
        {
            // 获取当前ROI参数
            var centerRow = processor.CenterRow;
            var centerCol = processor.CenterCol;
            var radius = processor.ExpectedRadius;

            // 创建ROI几何对象
            var roiGeometry = new VisionLite.Vision.Core.Models.CaliperData.ROIGeometry
            {
                RoiType = "circle",
                Parameters = new Dictionary<string, double>
                {
                    ["row"] = centerRow,
                    ["column"] = centerCol,
                    ["radius"] = radius
                }
            };

            // 1. 计算理想的原始卡尺位置
            var originalCalipers = processor.CalculateCaliperPositions(_originalImage, roiGeometry);

            // 2. 调用公开的裁剪方法，获取处理结果
            var processingResult = processor.ProcessCalipersWithBoundaryClipping(originalCalipers, _originalImage.Width, _originalImage.Height);

            // 3. 只使用裁剪后的有效卡尺进行绘制
            var calipersToDraw = processingResult.ValidCalipers;

            // 显示卡尺工具（蓝色矩形）
            if (calipersToDraw != null && calipersToDraw.Count > 0)
            {
                HalconDisplay.HalconWindow.SetColor("blue");
                HalconDisplay.HalconWindow.SetLineWidth(1);

                foreach (var caliper in calipersToDraw)
                {
                    HOperatorSet.GenRectangle2ContourXld(out HObject caliperContour,
                        caliper.CenterRow,
                        caliper.CenterCol,
                        caliper.Phi,
                        caliper.Length / 2.0,
                        caliper.Width / 2.0);

                    HalconDisplay.HalconWindow.DispObj(caliperContour);
                    caliperContour.Dispose();
                }
            }
        }

        /// <summary>
        /// 显示直线卡尺预览
        /// </summary>
        private void ShowLineCaliperPreview(LineCaliperProcessor processor)
        {
            // 获取当前直线参数
            var startRow = processor.StartRow;
            var startCol = processor.StartCol;
            var endRow = processor.EndRow;
            var endCol = processor.EndCol;

            // 创建ROI几何对象
            var roiGeometry = new VisionLite.Vision.Core.Models.CaliperData.ROIGeometry
            {
                RoiType = "line",
                Parameters = new Dictionary<string, double>
                {
                    ["row1"] = startRow,
                    ["column1"] = startCol,
                    ["row2"] = endRow,
                    ["column2"] = endCol
                }
            };

            // 1. 计算理想的原始卡尺位置
            var originalCalipers = processor.CalculateCaliperPositions(_originalImage, roiGeometry);

            // 2. 调用公开的裁剪方法，获取处理结果
            var processingResult = processor.ProcessCalipersWithBoundaryClipping(originalCalipers, _originalImage.Width, _originalImage.Height);

            // 3. 只使用裁剪后的有效卡尺进行绘制
            var calipersToDraw = processingResult.ValidCalipers;

            // 显示卡尺工具（蓝色矩形）
            if (calipersToDraw != null && calipersToDraw.Count > 0)
            {
                HalconDisplay.HalconWindow.SetColor("blue");
                HalconDisplay.HalconWindow.SetLineWidth(1);

                foreach (var caliper in calipersToDraw)
                {
                    HOperatorSet.GenRectangle2ContourXld(out HObject caliperContour,
                        caliper.CenterRow,
                        caliper.CenterCol,
                        caliper.Phi,
                        caliper.Length / 2.0,
                        caliper.Width / 2.0);

                    HalconDisplay.HalconWindow.DispObj(caliperContour);
                    caliperContour.Dispose();
                }
            }
        }
        
        
        
        
        /// <summary>
        /// 直线卡尺拖拽回调（实时更新参数和预览）
        /// </summary>
        private void OnLineCaliperDragging(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 防止递归更新
            if (_isUpdatingFromROI) return;
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 立即更新ROI参数
                    UpdateLineCaliperParameters(dobj);
                    
                    // 立即显示卡尺工具的实时预览（只显示卡尺位置，不执行算法）
                    ShowRealtimeCaliperPreview();
                    
                    // 使用统一防抖机制触发算法执行
                    TriggerUnifiedDebounce("ROI_Dragging");
                }
                catch (Exception)
                {
                    // 忽略拖拽过程中的异常
                }
            });
        }
        
        /// <summary>
        /// 直线卡尺更新回调（拖拽完成后）
        /// </summary>
        private void OnLineCaliperUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 防止递归更新
            if (_isUpdatingFromROI) return;
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 更新直线参数
                    UpdateLineCaliperParameters(dobj);
                    
                    // 立即显示卡尺预览
                    ShowRealtimeCaliperPreview();
                    
                    // 使用统一防抖机制触发算法执行
                    TriggerUnifiedDebounce("ROI_Update");
                }
                catch (Exception)
                {
                    // 忽略更新过程中的异常
                }
            });
        }
        
        /// <summary>
        /// 更新直线卡尺参数
        /// </summary>
        private void UpdateLineCaliperParameters(HDrawingObject dobj)
        {
            try
            {
                // 获取当前直线参数
                HTuple row1 = dobj.GetDrawingObjectParams("row1");
                HTuple col1 = dobj.GetDrawingObjectParams("column1");
                HTuple row2 = dobj.GetDrawingObjectParams("row2");
                HTuple col2 = dobj.GetDrawingObjectParams("column2");

                // 更新处理器参数
                if (_currentProcessor is LineCaliperProcessor processor)
                {
                    processor.StartRow = row1.D;
                    processor.StartCol = col1.D;
                    processor.EndRow = row2.D;
                    processor.EndCol = col2.D;
                }

                // 同步更新参数面板显示（不触发事件）
                AlgorithmParameterPanel.UpdateParameterValueSilently("StartRow", row1.D);
                AlgorithmParameterPanel.UpdateParameterValueSilently("StartCol", col1.D);
                AlgorithmParameterPanel.UpdateParameterValueSilently("EndRow", row2.D);
                AlgorithmParameterPanel.UpdateParameterValueSilently("EndCol", col2.D);
            }
            catch (Exception)
            {
                // 忽略参数更新异常
            }
        }
        
        
        // ResetDebounceTimer 方法已删除，使用统一防抖机制 TriggerUnifiedDebounce
        
        // OnDebounceTimeout 方法已删除，逻辑合并到 ExecuteUnifiedDebounceCallback


        /// <summary>
        /// 圆形卡尺更新回调（旧版本，保留用于兼容）
        /// </summary>
        private void OnCircleCaliperUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            var updateStartTime = DateTime.Now;
            var currentUpdateId = ++_roiUpdateCount;
            
            // 调试信息
            var timeSinceLastUpdate = _lastRoiUpdateTime == DateTime.MinValue ? 0 : (updateStartTime - _lastRoiUpdateTime).TotalMilliseconds;
            _lastRoiUpdateTime = updateStartTime;
            
            // 防止递归更新
            if (_isUpdatingFromROI) 
            {
                return;
            }
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var dispatcherStartTime = DateTime.Now;
                    var dispatcherDelay = (dispatcherStartTime - updateStartTime).TotalMilliseconds;
                    
                    // 立即清除旧的算法结果显示，只保留原始图像和ROI
                    if (_hImage != null)
                    {
                        var clearStartTime = DateTime.Now;
                        HalconDisplay.HalconWindow.DispObj(_hImage);
                        var clearTime = (DateTime.Now - clearStartTime).TotalMilliseconds;
                    }

                    // 获取当前圆形参数
                    var paramStartTime = DateTime.Now;
                    HTuple row = dobj.GetDrawingObjectParams("row");
                    HTuple col = dobj.GetDrawingObjectParams("column");
                    HTuple radius = dobj.GetDrawingObjectParams("radius");
                    var paramTime = (DateTime.Now - paramStartTime).TotalMilliseconds;

                    // 直接更新处理器参数（避免触发事件循环）
                    if (_currentProcessor is CircleCaliperProcessor processor)
                    {
                        processor.CenterRow = row.D;
                        processor.CenterCol = col.D;
                        processor.ExpectedRadius = radius.D;
                    }

                    // 同步更新参数面板显示（不触发事件）
                    var panelStartTime = DateTime.Now;
                    AlgorithmParameterPanel.UpdateParameterValueSilently("CenterRow", row.D);
                    AlgorithmParameterPanel.UpdateParameterValueSilently("CenterCol", col.D);
                    AlgorithmParameterPanel.UpdateParameterValueSilently("ExpectedRadius", radius.D);
                    var panelTime = (DateTime.Now - panelStartTime).TotalMilliseconds;

                    var totalCallbackTime = (DateTime.Now - updateStartTime).TotalMilliseconds;
                    
                    // 使用统一防抖机制触发实时预览
                    TriggerUnifiedDebounce("CircleUpdate");
                }
                catch (Exception)
                {
                }
            });
        }

        /// <summary>
        /// 清理交互式卡尺
        /// </summary>
        private void CleanupInteractiveCaliper()
        {
            if (_interactiveCaliper != null)
            {
                try
                {
                    HalconDisplay.HalconWindow.DetachDrawingObjectFromWindow(_interactiveCaliper);
                }
                catch { }
                
                _interactiveCaliper.Dispose();
                _interactiveCaliper = null;
            }
        }
        
        #endregion
        
        #region Halcon原生轮廓显示
        
        /// <summary>
        /// 显示Halcon原生轮廓
        /// </summary>
        /// <param name="result">处理结果</param>
        private void DisplayHalconContours(ProcessResult result)
        {
            try
            {
                // 检查是否包含Halcon轮廓数据
                if (result.Metadata != null && 
                    result.Metadata.TryGetValue("HalconDisplayContours", out var contoursObj) &&
                    contoursObj is HalconDisplayContours displayContours)
                {
                    
                    // 先显示测量轮廓（蓝色卡尺线）
                    if (displayContours.MeasureContours != null)
                    {
                        try
                        {
                            // 检查HALCON对象是否有效
                            if (displayContours.MeasureContours.IsInitialized() && displayContours.MeasureContours.CountObj() > 0)
                            {
                                HalconDisplay.HalconWindow.SetColor("blue");
                                HalconDisplay.HalconWindow.SetLineWidth(1);
                                HalconDisplay.HalconWindow.DispObj(displayContours.MeasureContours);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                    }

                    // 后显示模型轮廓（绿色匹配框，醒目显示）
                    if (displayContours.ModelContour != null)
                    {
                        try
                        {
                            // 检查HALCON对象是否有效
                            if (displayContours.ModelContour.IsInitialized() && displayContours.ModelContour.CountObj() > 0)
                            {
                                HalconDisplay.HalconWindow.SetColor("green");
                                HalconDisplay.HalconWindow.SetLineWidth(3);
                                HalconDisplay.HalconWindow.DispObj(displayContours.ModelContour);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                    }

                    // 恢复默认设置
                    HalconDisplay.HalconWindow.SetColor("red");
                    HalconDisplay.HalconWindow.SetLineWidth(1);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"显示轮廓失败: {ex.Message}", true);
            }
        }
        
        #endregion
        
        #region 几何元素绘制
        
        /// <summary>
        /// 绘制几何元素到Halcon窗口
        /// </summary>
        /// <param name="geometryElements">几何元素列表</param>
        private void DrawGeometryElements(List<GeometryElement> geometryElements)
        {
            if (geometryElements == null || geometryElements.Count == 0)
                return;
                
            try
            {
                foreach (var element in geometryElements)
                {
                    if (!element.IsVisible) continue;
                    
                    // 设置颜色
                    var color = ColorToHalconColor(element.Color);
                    HalconDisplay.HalconWindow.SetColor(color);
                    HalconDisplay.HalconWindow.SetLineWidth((int)element.LineWidth);
                    
                    switch (element.ElementType)
                    {
                        case GeometryElementType.Circle:
                            DrawCircleElement((CircleElement)element);
                            break;
                            
                        case GeometryElementType.Point:
                            DrawPointElement((PointElement)element);
                            break;
                            
                        case GeometryElementType.Line:
                            DrawLineElement((LineElement)element);
                            break;
                            
                        case GeometryElementType.Rectangle:
                            DrawRectangleElement((RectangleElement)element);
                            break;
                    }
                }
                
                // 恢复默认设置
                HalconDisplay.HalconWindow.SetColor("red");
                HalconDisplay.HalconWindow.SetLineWidth(1);
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制几何元素失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 绘制圆形元素
        /// </summary>
        private void DrawCircleElement(CircleElement circle)
        {
            try
            {
                HOperatorSet.GenCircleContourXld(out HObject circleContour,
                    circle.CenterRow, circle.CenterColumn, circle.Radius,
                    0, Math.PI * 2, "positive", 1.0);
                    
                HalconDisplay.HalconWindow.DispObj(circleContour);
                circleContour?.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制圆形失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 绘制点元素
        /// </summary>
        private void DrawPointElement(PointElement point)
        {
            try
            {
                HOperatorSet.GenCrossContourXld(out HObject crossContour,
                    point.Row, point.Column, point.Size, Math.PI / 4);
                    
                HalconDisplay.HalconWindow.DispObj(crossContour);
                crossContour?.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制点失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 绘制直线元素
        /// </summary>
        private void DrawLineElement(LineElement line)
        {
            try
            {
                HOperatorSet.GenContourPolygonXld(out HObject lineContour,
                    new HTuple(new double[] { line.Row1, line.Row2 }),
                    new HTuple(new double[] { line.Column1, line.Column2 }));
                    
                HalconDisplay.HalconWindow.DispObj(lineContour);
                lineContour?.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制直线失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 绘制矩形元素
        /// </summary>
        private void DrawRectangleElement(RectangleElement rect)
        {
            try
            {
                HOperatorSet.GenRectangle2ContourXld(out HObject rectContour,
                    (rect.Row1 + rect.Row2) / 2, (rect.Column1 + rect.Column2) / 2,
                    0, Math.Abs(rect.Row2 - rect.Row1) / 2, Math.Abs(rect.Column2 - rect.Column1) / 2);
                    
                HalconDisplay.HalconWindow.DispObj(rectContour);
                rectContour?.Dispose();
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制矩形失败: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 将WPF颜色转换为Halcon颜色字符串
        /// </summary>
        private string ColorToHalconColor(Color color)
        {
            // 根据颜色返回Halcon支持的颜色名称
            if (color == Colors.Red) return "red";
            if (color == Colors.Green) return "green";
            if (color == Colors.Blue) return "blue";
            if (color == Colors.Yellow) return "yellow";
            if (color == Colors.White) return "white";
            if (color == Colors.Black) return "black";
            if (color == Colors.Orange) return "orange";
            if (color == Colors.Purple) return "magenta";
            if (color == Colors.Cyan) return "cyan";

            // 默认返回红色
            return "red";
        }

        #endregion

        #region ROI交互方法

        /// <summary>
        /// 判断是否为图像匹配算法
        /// </summary>
        private bool IsImageMatchingAlgorithm(string algorithmKey)
        {
            return algorithmKey == "GrayValueMatching" ||
                   algorithmKey == "ShapeMatching";
        }

        /// <summary>
        /// 开始图像匹配ROI工作流程
        /// </summary>
        private void StartImageMatchingROIWorkflow(string algorithmKey)
        {
            if (_originalImage == null)
            {
                MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectAlgorithm(algorithmKey);
            _isImageMatchingMode = true;
            _roiManager.SetCurrentImage(_originalImage);
            _roiManager.SetROIMode(ROIInteractionMode.TemplateROI);

            // 显示ROI控制面板
            AlgorithmParameterPanel.ShowROIControls = true;

            UpdateStatus("请在参数面板选择ROI类型，然后在图像上绘制ROI区域", false);
        }

        /// <summary>
        /// 参数面板ROI模式变化事件处理
        /// </summary>
        private void OnParameterPanelROIModeChanged(ROIInteractionMode mode)
        {
            if (_roiManager == null) return;

            // 统一使用SetROIMode方法，它会自动处理模式切换逻辑
            _roiManager.SetROIMode(mode);
            RefreshROIDisplay();
        }

        /// <summary>
        /// 模板ROI完成事件处理
        /// </summary>
        private void OnTemplateROICompleted(CaliperData.ROIGeometry templateROI)
        {
            if (_currentProcessor is ImageMatchingProcessorBase processor)
            {
                processor.TemplateROI = templateROI;
                // 强制重新创建模板
                processor.NeedUpdateTemplate = true;
                
            }

            RefreshROIDisplay();
            UpdateStatus("模板ROI已确认，可以通过参数面板切换到搜索ROI模式", false);
        }

        /// <summary>
        /// 搜索ROI完成事件处理
        /// </summary>
        private void OnSearchROICompleted(CaliperData.ROIGeometry searchROI)
        {
            if (_currentProcessor is ImageMatchingProcessorBase processor)
            {
                processor.SearchROI = searchROI;
            }

            RefreshROIDisplay();
            UpdateStatus("搜索ROI设置完成，可以执行图像匹配算法", false);
        }

        /// <summary>
        /// ROI更新事件处理
        /// </summary>
        private void OnROIUpdated()
        {
            // 当ROI被拖拽或编辑时，需要重新创建模板
            if (_currentProcessor is ImageMatchingProcessorBase processor)
            {
                processor.NeedUpdateTemplate = true;
                
            }

            RefreshROIDisplay();
        }

        /// <summary>
        /// 刷新ROI显示（HDrawingObject自动管理显示）
        /// </summary>
        private void RefreshROIDisplay()
        {
            // HDrawingObject会自动管理所有的ROI显示
            // 不需要手动重绘，只需要确保背景图像正确显示
            try
            {
                var window = HalconDisplay.HalconWindow;
                if (_hImage != null)
                {
                    window.ClearWindow();
                    window.DispObj(_hImage);
                    // HDrawingObject会自动重新显示
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新ROI显示失败: {ex.Message}");
            }
        }


        /// <summary>
        /// Halcon控件鼠标按下事件（仅处理ROI确认）
        /// </summary>
        private void HalconDisplay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // HDrawingObject会自动处理所有的ROI交互，这里只需要处理右键确认
        }

        /// <summary>
        /// Halcon控件鼠标移动事件（HDrawingObject自动处理）
        /// </summary>
        private void HalconDisplay_MouseMove(object sender, MouseEventArgs e)
        {
            // HDrawingObject会自动处理所有的ROI交互
        }

        /// <summary>
        /// Halcon控件鼠标抬起事件（已移除右键确认逻辑）
        /// </summary>
        private void HalconDisplay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // HDrawingObject会自动处理所有的ROI交互
            // 新的流程通过参数面板的单选钮切换ROI模式
        }

        /// <summary>
        /// 将屏幕坐标转换为图像坐标
        /// </summary>
        private (double Row, double Col) ConvertScreenToImageCoordinates(Point screenPoint)
        {
            try
            {
                var window = HalconDisplay.HalconWindow;
                window.ConvertCoordinatesWindowToImage(screenPoint.X, screenPoint.Y, out double row, out double col);
                return (row, col);
            }
            catch
            {
                return (screenPoint.Y, screenPoint.X);
            }
        }

        #endregion


        #endregion
    }
}