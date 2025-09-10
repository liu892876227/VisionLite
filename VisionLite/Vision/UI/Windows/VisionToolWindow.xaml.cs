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
using VisionLite.Vision.Processors.Preprocessing.FilterProcessors;
using VisionLite.Vision.Processors.Preprocessing.ThresholdProcessors;
using VisionLite.Vision.Processors.Preprocessing.MorphologyProcessors;
using VisionLite.Vision.Processors.Preprocessing.EnhancementProcessors;
using VisionLite.Vision.Processors.Measurement.CaliperProcessors;
using VisionLite.Vision.UI.Controls;
using VisionLite.Vision.Core.Utils;

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
                
                // 注册边缘卡尺算法
                var edgeCaliperProcessor = new EdgeCaliperProcessor();
                var edgeCaliperParams = edgeCaliperProcessor.GetParameters();
                if (edgeCaliperParams != null)
                {
                    _algorithmProcessors["EdgeCaliper"] = edgeCaliperProcessor;
                }
                else
                {
                    throw new InvalidOperationException("边缘卡尺处理器参数获取失败");
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
                
                
                // 后续可以通过反射自动加载所有算法
                // LoadAllProcessorsByReflection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化算法失败: {ex.Message}\n\n详细信息: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                SelectAlgorithm(algorithmKey);
                
                // 对于边缘查找算法，初始化交互式卡尺
                if (algorithmKey == "EdgeCaliper")
                {
                    InitializeInteractiveEdgeCaliper();
                }
                // 对于圆查找算法，初始化交互式圆形卡尺
                else if (algorithmKey == "CircleCaliper")
                {
                    InitializeInteractiveCircleCaliper();
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
                
                // 如果是圆查找算法，处理参数更新
                if (_currentProcessor is CircleCaliperProcessor)
                {
                    // ROI参数需要同步更新HDrawingObject
                    if (_interactiveCaliper != null && IsROIParameter(e.ParameterName))
                    {
                        UpdateInteractiveCaliper();
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
        
        // 防抖定时器，防止参数快速变化时频繁执行算法
        private System.Windows.Threading.DispatcherTimer _realtimePreviewTimer;
        
        // 实时预览处理标志，独立于手动执行的处理标志
        private bool _isRealtimeProcessing = false;
        
        // 实时预览级别控制
        private RealtimePreviewLevel _realtimePreviewLevel = RealtimePreviewLevel.All;
        
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
            catch (Exception ex)
            {
                // 记录错误但不中断流程，返回空数组作为降级方案
                System.Diagnostics.Debug.WriteLine($"获取实时参数失败: {ex.Message}");
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
                   parameterName == "ExpectedRadius";
        }
        
        /// <summary>
        /// 触发实时预览（使用智能防抖机制）
        /// </summary>
        /// <param name="changedParameter">发生变化的参数名称</param>
        private void TriggerRealtimePreview(string changedParameter = null)
        {
            // 根据处理器类型计算最优防抖延迟
            var debounceDelay = CalculateOptimalDebounceDelay();
            
            // 如果定时器已存在，停止并更新延迟
            if (_realtimePreviewTimer != null)
            {
                _realtimePreviewTimer.Stop();
                _realtimePreviewTimer.Interval = TimeSpan.FromMilliseconds(debounceDelay);
            }
            else
            {
                // 初始化智能防抖定时器
                _realtimePreviewTimer = new System.Windows.Threading.DispatcherTimer();
                _realtimePreviewTimer.Interval = TimeSpan.FromMilliseconds(debounceDelay);
                _realtimePreviewTimer.Tick += async (s, args) =>
                {
                    _realtimePreviewTimer.Stop();
                    try
                    {
                        // 使用轻量级预览模式（如果适用）
                        await ExecuteRealtimePreview(changedParameter);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"实时预览失败: {ex.Message}", true);
                    }
                };
            }
            
            // 启动防抖定时器
            _realtimePreviewTimer.Start();
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
                "ShowCalipers", "ShowEdgePoints", "ShowFittedCircle",
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
        
        /// <summary>
        /// 根据处理器类型计算最优防抖延迟
        /// </summary>
        /// <returns>防抖延迟时间(毫秒)</returns>
        private int CalculateOptimalDebounceDelay()
        {
            if (_currentProcessor == null)
                return 100; // 默认延迟
                
            // 根据处理器复杂度调整防抖延迟
            switch (_currentProcessor)
            {
                case CircleCaliperProcessor _:
                    return 50;   // 圆卡尺：快速预览，用户交互频繁
                    
                // 其他处理器可以根据计算复杂度调整
                // case ComplexProcessor _:
                //     return 150;  // 复杂算法：较长延迟
                    
                default:
                    return 75;   // 适中的默认延迟
            }
        }
        
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
                "ShowCalipers", "ShowEdgePoints", "ShowFittedCircle",
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
                System.Diagnostics.Debug.WriteLine($"实时参数信息: {cacheInfo}, {levelInfo}");
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
                if (_currentProcessor is CircleCaliperProcessor processor && _interactiveCaliper != null)
                {
                    // 设置标志位，防止递归更新
                    _isUpdatingFromROI = true;
                    
                    // 直接从处理器属性获取最新参数值
                    var centerRow = processor.CenterRow;
                    var centerCol = processor.CenterCol;
                    var radius = processor.ExpectedRadius;
                    
                    // 清理之前的显示，避免出现重叠的ROI
                    if (_hImage != null)
                    {
                        HalconDisplay.HalconWindow.ClearWindow();
                        HalconDisplay.HalconWindow.DispObj(_hImage);
                    }
                    
                    // 更新HDrawingObject参数
                    _interactiveCaliper.SetDrawingObjectParams("row", centerRow);
                    _interactiveCaliper.SetDrawingObjectParams("column", centerCol);
                    _interactiveCaliper.SetDrawingObjectParams("radius", radius);
                    
                    // 验证更新是否成功
                    HTuple newRow = _interactiveCaliper.GetDrawingObjectParams("row");
                    HTuple newCol = _interactiveCaliper.GetDrawingObjectParams("column");
                    HTuple newRadius = _interactiveCaliper.GetDrawingObjectParams("radius");
                    
                    UpdateStatus($"ROI已更新: 圆心({newRow.D:F1}, {newCol.D:F1}) 半径:{newRadius.D:F1}");
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
        /// 参数应用事件
        /// </summary>
        private async void OnParametersApplied(object sender, EventArgs e)
        {
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
                // 清理防抖定时器
                if (_realtimePreviewTimer != null)
                {
                    _realtimePreviewTimer.Stop();
                    _realtimePreviewTimer = null;
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
                
                // 对于边缘查找算法，从HDrawingObject获取参数
                if (_currentProcessor is EdgeCaliperProcessor edgeProcessor && _interactiveCaliper != null)
                {
                    try
                    {
                        // 从HDrawingObject获取参数
                        HTuple param = _interactiveCaliper.GetDrawingObjectParams("row");
                        double row = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("column");
                        double col = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("phi");
                        double phi = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("length1");
                        double len1 = param.D;
                        
                        param = _interactiveCaliper.GetDrawingObjectParams("length2");
                        double len2 = param.D;
                        
                        // 设置EdgeCaliperProcessor的参数
                        edgeProcessor.SetParameter("CenterRow", row);
                        edgeProcessor.SetParameter("CenterCol", col);
                        edgeProcessor.SetParameter("Phi", phi * 180.0 / Math.PI); // 转换为度数
                        edgeProcessor.SetParameter("Length1", len1);
                        edgeProcessor.SetParameter("Length2", len2);
                        
                        UpdateStatus($"卡尺参数: 中心({row:F1},{col:F1}) 角度:{phi * 180.0 / Math.PI:F1}° 长度:{len1:F1}x{len2:F1}");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"获取卡尺参数失败: {ex.Message}");
                    }
                }
                // 对于圆形查找算法，从HDrawingObject获取参数
                else if (_currentProcessor is CircleCaliperProcessor circleProcessor && _interactiveCaliper != null)
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
            
            ClearResultInfo();
            
            // 切换到原始图像模式
            OriginalImageMode.IsChecked = true;
            if (_hImage != null)
            {
                HalconDisplay.HalconWindow.DispObj(_hImage);
                HalconDisplay.HalconWindow.SetPart(0, 0, -1, -1);
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
                var geometryHeader = new TextBlock
                {
                    Text = $"检测到的几何元素 ({result.GeometryElements.Count}个):",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                ResultInfoPanel.Children.Add(geometryHeader);
                
                foreach (var element in result.GeometryElements)
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
                    HalconDisplay.HalconWindow.SetPart(0, 0, -1, -1);
                }
                else if (ResultImageMode.IsChecked == true && _hResultImage != null)
                {
                    HalconDisplay.HalconWindow.ClearWindow();
                    HalconDisplay.HalconWindow.DispObj(_hResultImage);
                    HalconDisplay.HalconWindow.SetPart(0, 0, -1, -1);
                    
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
        private async Task ExecuteRealtimeAlgorithm()
        {
            if (_currentProcessor == null || _originalImage == null)
                return;
                
            try
            {
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
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"获取圆形参数失败: {ex.Message}");
                    }
                }
                
                // 执行算法
                ProcessResult result = await _currentProcessor.ProcessAsync(_originalImage);
                
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
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"实时预览失败: {ex.Message}", true);
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
                
                // 3. 清除显示内容并切换到原始图像模式
                HalconDisplay.HalconWindow.ClearWindow();
                OriginalImageMode.IsChecked = true;
                if (_hImage != null)
                {
                    HalconDisplay.HalconWindow.DispObj(_hImage);
                }
                
                // 4. 清除结果信息面板
                ClearResultInfo();
                
                // 5. 重置UI状态
                SaveResultButton.IsEnabled = false;
                ProcessingTimeText.Text = "";
                
                // 6. 停止并清理实时预览定时器
                if (_realtimePreviewTimer != null)
                {
                    _realtimePreviewTimer.Stop();
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
        /// 初始化交互式边缘卡尺工具
        /// </summary>
        private void InitializeInteractiveEdgeCaliper()
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
                double initialLength1 = Math.Min(imageHeight[0].D, imageWidth[0].D) * 0.1;
                double initialLength2 = Math.Min(imageHeight[0].D, imageWidth[0].D) * 0.05;

                // 创建HDrawingObject
                _interactiveCaliper = HDrawingObject.CreateDrawingObject(
                    HDrawingObject.HDrawingObjectType.RECTANGLE2,
                    centerRow, centerCol, 0, initialLength1, initialLength2);

                // 设置样式 - 设为很细的白色线条，尽量不干扰显示
                _interactiveCaliper.SetDrawingObjectParams("color", "white");
                _interactiveCaliper.SetDrawingObjectParams("line_width", 1);

                // 附加到窗口
                HalconDisplay.HalconWindow.AttachDrawingObjectToWindow(_interactiveCaliper);

                // 订阅事件
                _interactiveCaliper.OnDrag(OnCaliperUpdate);
                _interactiveCaliper.OnResize(OnCaliperUpdate);
                _interactiveCaliper.OnSelect(OnCaliperUpdate);

                UpdateStatus("交互式卡尺已激活，请拖动调整卡尺位置和大小");
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化交互式卡尺失败: {ex.Message}", true);
            }
        }

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

                // 设置样式 - 绿色圆圈，便于识别
                _interactiveCaliper.SetDrawingObjectParams("color", "green");
                _interactiveCaliper.SetDrawingObjectParams("line_width", 2);

                // 附加到窗口
                HalconDisplay.HalconWindow.AttachDrawingObjectToWindow(_interactiveCaliper);

                // 订阅事件
                _interactiveCaliper.OnDrag(OnCircleCaliperUpdate);
                _interactiveCaliper.OnResize(OnCircleCaliperUpdate);
                _interactiveCaliper.OnSelect(OnCircleCaliperUpdate);

                UpdateStatus($"交互式圆形卡尺已激活: 中心({centerRow:F1},{centerCol:F1}) 半径:{initialRadius:F1}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化交互式圆形卡尺失败: {ex.Message}", true);
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

                    // 实时预览
                    RunRealtimeEdgePreview(row.D, col.D, phi.D, len1.D, len2.D);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"更新卡尺失败: {ex.Message}", true);
                }
            });
        }

        /// <summary>
        /// 圆形卡尺更新回调
        /// </summary>
        private void OnCircleCaliperUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 防止递归更新
            if (_isUpdatingFromROI) return;
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 立即清除旧的算法结果显示，只保留原始图像和ROI
                    if (_hImage != null)
                    {
                        HalconDisplay.HalconWindow.ClearWindow();
                        HalconDisplay.HalconWindow.DispObj(_hImage);
                        // 不调用SetPart，保持用户设置的显示区域
                    }

                    // 获取当前圆形参数
                    HTuple row = dobj.GetDrawingObjectParams("row");
                    HTuple col = dobj.GetDrawingObjectParams("column");
                    HTuple radius = dobj.GetDrawingObjectParams("radius");

                    // 直接更新处理器参数（避免触发事件循环）
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

                    // 实时预览圆检测（50ms后显示新的卡尺和拟合圆）
                    RunRealtimeCirclePreview(row.D, col.D, radius.D);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"更新圆形卡尺失败: {ex.Message}", true);
                }
            });
        }

        /// <summary>
        /// 实时圆检测预览
        /// </summary>
        private void RunRealtimeCirclePreview(double centerRow, double centerCol, double radius)
        {
            if (_originalImage == null) return;

            try
            {
                // 更新处理器参数
                var processor = _currentProcessor as CircleCaliperProcessor;
                if (processor != null)
                {
                    processor.CenterRow = centerRow;
                    processor.CenterCol = centerCol;
                    processor.ExpectedRadius = radius;
                }

                // 使用与参数面板相同的防抖机制执行完整算法
                // 如果定时器已存在，停止并重置
                if (_realtimePreviewTimer != null)
                {
                    _realtimePreviewTimer.Stop();
                }
                else
                {
                    // 初始化防抖定时器
                    _realtimePreviewTimer = new System.Windows.Threading.DispatcherTimer();
                    _realtimePreviewTimer.Interval = TimeSpan.FromMilliseconds(50); // 50ms延迟
                    _realtimePreviewTimer.Tick += async (s, args) =>
                    {
                        _realtimePreviewTimer.Stop();
                        
                        // 如果正在实时处理，跳过本次执行
                        if (_isRealtimeProcessing)
                        {
                            return;
                        }
                        
                        var debounceEnd = DateTime.Now;
                        try
                        {
                            _isRealtimeProcessing = true;
                            await ExecuteRealtimeAlgorithm();
                            var algorithmEnd = DateTime.Now;
                            var totalDelay = (algorithmEnd - debounceEnd).TotalMilliseconds;
                            if (totalDelay > 100) // 只在延迟超过100ms时提示
                            {
                                UpdateStatus($"算法执行耗时: {totalDelay:F0}ms", false);
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
                    };
                }
                
                // 启动防抖定时器
                _realtimePreviewTimer.Start();
            }
            catch (Exception ex)
            {
                UpdateStatus($"圆检测预览失败: {ex.Message}", true);
            }
        }

        /// <summary>
        /// 实时边缘预览
        /// </summary>
        private void RunRealtimeEdgePreview(double row, double col, double phi, double len1, double len2)
        {
            if (_hImage == null) return;

            HTuple measureHandle = null;
            HObject foundEdges = null;

            try
            {
                // 获取图像尺寸
                HOperatorSet.GetImageSize(_hImage, out HTuple width, out HTuple height);

                // 创建测量句柄
                HOperatorSet.GenMeasureRectangle2(row, col, phi, len1, len2,
                    width, height, "nearest_neighbor", out measureHandle);

                // 获取处理器参数
                var processor = _currentProcessor as EdgeCaliperProcessor;
                double sigma = processor?.Sigma ?? 1.0;
                double threshold = processor?.EdgeThreshold ?? 20;

                // 执行测量
                HOperatorSet.MeasurePos(_hImage, measureHandle, sigma, threshold, "all", "all",
                    out HTuple edgeRows, out HTuple edgeCols, out HTuple amplitudes, out HTuple distances);

                // 显示结果
                HalconDisplay.HalconWindow.ClearWindow();
                HalconDisplay.HalconWindow.DispObj(_hImage);

                // 绘制蓝色检测线（卡尺内部的测量线条）
                DrawMeasureLines(row, col, phi, len1, len2);

                // 显示找到的边缘点和拟合直线
                if (edgeRows.Length > 0)
                {
                    // 显示红色边缘点（可选，或者不显示）
                    // HOperatorSet.GenCrossContourXld(out foundEdges, edgeRows, edgeCols, 6, 0.785398);
                    // HalconDisplay.HalconWindow.SetColor("red");
                    // HalconDisplay.HalconWindow.DispObj(foundEdges);
                    
                    // 如果有足够的边缘点，拟合直线
                    if (edgeRows.Length >= 2)
                    {
                        try
                        {
                            // 拟合直线
                            HOperatorSet.FitLineContourXld(foundEdges, "tukey", -1, 0, 5, 2, 
                                out HTuple rowBegin, out HTuple colBegin, out HTuple rowEnd, out HTuple colEnd,
                                out HTuple nr, out HTuple nc, out HTuple dist);
                            
                            if (rowBegin.Length > 0)
                            {
                                // 生成直线轮廓
                                HOperatorSet.GenContourPolygonXld(out HObject lineContour, 
                                    new HTuple(new double[] { rowBegin.D, rowEnd.D }),
                                    new HTuple(new double[] { colBegin.D, colEnd.D }));
                                
                                // 用绿色绘制拟合的直线
                                HalconDisplay.HalconWindow.SetColor("green");
                                HalconDisplay.HalconWindow.SetLineWidth(3);
                                HalconDisplay.HalconWindow.DispObj(lineContour);
                                HalconDisplay.HalconWindow.SetLineWidth(1); // 恢复默认线宽
                                
                                lineContour?.Dispose();
                            }
                        }
                        catch (Exception)
                        {
                            // 如果拟合失败，使用简单的连线方式
                            if (edgeRows.Length >= 2)
                            {
                                HOperatorSet.GenContourPolygonXld(out HObject simpleLineContour,
                                    new HTuple(new double[] { edgeRows[0].D, edgeRows[edgeRows.Length - 1].D }),
                                    new HTuple(new double[] { edgeCols[0].D, edgeCols[edgeCols.Length - 1].D }));
                                
                                HalconDisplay.HalconWindow.SetColor("green");
                                HalconDisplay.HalconWindow.SetLineWidth(2);
                                HalconDisplay.HalconWindow.DispObj(simpleLineContour);
                                HalconDisplay.HalconWindow.SetLineWidth(1);
                                
                                simpleLineContour?.Dispose();
                            }
                            UpdateStatus("直线拟合失败，使用简单连线", true);
                        }
                    }
                }

                // 更新状态
                UpdateStatus($"找到 {edgeRows.Length} 个边缘点");
            }
            catch (Exception ex)
            {
                UpdateStatus($"实时预览失败: {ex.Message}", true);
            }
            finally
            {
                measureHandle?.Dispose();
                foundEdges?.Dispose();
            }
        }

        /// <summary>
        /// 绘制蓝色检测线（卡尺内部的测量线条）
        /// </summary>
        private void DrawMeasureLines(double row, double col, double phi, double len1, double len2)
        {
            try
            {
                // 计算矩形的四个角点
                double cosA = Math.Cos(phi);
                double sinA = Math.Sin(phi);
                
                // 矩形的四个角点
                double r1 = row - len1 * cosA - len2 * sinA;
                double c1 = col - len1 * sinA + len2 * cosA;
                double r2 = row + len1 * cosA - len2 * sinA;
                double c2 = col + len1 * sinA + len2 * cosA;
                double r3 = row + len1 * cosA + len2 * sinA;
                double c3 = col + len1 * sinA - len2 * cosA;
                double r4 = row - len1 * cosA + len2 * sinA;
                double c4 = col - len1 * sinA - len2 * cosA;

                // 绘制矩形框（蓝色）
                HalconDisplay.HalconWindow.SetColor("blue");
                HalconDisplay.HalconWindow.SetLineWidth(2);
                
                // 绘制四条边
                HOperatorSet.GenContourPolygonXld(out HObject rectContour,
                    new HTuple(new double[] { r1, r2, r3, r4, r1 }),
                    new HTuple(new double[] { c1, c2, c3, c4, c1 }));
                
                HalconDisplay.HalconWindow.DispObj(rectContour);

                // 绘制内部检测线条（从图片看是多条平行线）
                int numLines = 20; // 检测线数量
                for (int i = 0; i < numLines; i++)
                {
                    double t = -1.0 + 2.0 * i / (numLines - 1); // -1 到 1
                    double lineRow = row + t * len1 * cosA;
                    double lineCol = col + t * len1 * sinA;
                    
                    // 每条检测线的起点和终点
                    double startRow = lineRow - len2 * sinA;
                    double startCol = lineCol + len2 * cosA;
                    double endRow = lineRow + len2 * sinA;
                    double endCol = lineCol - len2 * cosA;
                    
                    HOperatorSet.GenContourPolygonXld(out HObject lineContour,
                        new HTuple(new double[] { startRow, endRow }),
                        new HTuple(new double[] { startCol, endCol }));
                    
                    HalconDisplay.HalconWindow.DispObj(lineContour);
                    lineContour?.Dispose();
                }
                
                rectContour?.Dispose();
                HalconDisplay.HalconWindow.SetLineWidth(1); // 恢复默认线宽
            }
            catch (Exception ex)
            {
                UpdateStatus($"绘制检测线失败: {ex.Message}", true);
            }
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
                        HalconDisplay.HalconWindow.SetColor("blue");
                        HalconDisplay.HalconWindow.SetLineWidth(1);
                        HalconDisplay.HalconWindow.DispObj(displayContours.MeasureContours);
                    }
                    else
                    {
                    }

                    // 后显示模型轮廓（红色圆圈，更醒目）
                    if (displayContours.ModelContour != null)
                    {
                        HalconDisplay.HalconWindow.SetColor("red");
                        HalconDisplay.HalconWindow.SetLineWidth(3);
                        HalconDisplay.HalconWindow.DispObj(displayContours.ModelContour);
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
        
        
        #endregion
    }
}