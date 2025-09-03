using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VisionLite.Vision.Core.Interfaces;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FilterProcessors;
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
                
                // 图像显示控件事件
                if (OriginalImageDisplay != null)
                {
                    OriginalImageDisplay.ImageClicked += OnImageClicked;
                }
                
                if (ResultImageDisplay != null)
                {
                    ResultImageDisplay.ImageClicked += OnImageClicked;
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
            }
        }
        
        /// <summary>
        /// 参数变化事件
        /// </summary>
        private void OnParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            // 参数变化时可以实时预览（如果启用）
            // 这里暂时不实现实时预览，避免频繁计算
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
                // 释放图像资源
                _originalImage?.Dispose();
                _resultImage?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放资源失败: {ex.Message}");
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
                
                // 加载新图像
                _originalImage = VisionImage.FromFile(imagePath);
                
                // 显示原始图像
                OriginalImageDisplay.DisplayImage(_originalImage);
                
                // 清空结果显示
                ResultImageDisplay.ClearDisplay();
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
                
                // 执行算法
                var result = await _currentProcessor.ProcessAsync(_originalImage);
                
                var endTime = DateTime.Now;
                var processingTime = endTime - startTime;
                
                if (result.Success)
                {
                    // 释放之前的结果图像
                    _resultImage?.Dispose();
                    _resultImage = result.OutputImage;
                    
                    // 显示结果图像
                    ResultImageDisplay.DisplayImage(_resultImage);
                    
                    // 显示结果信息
                    DisplayResultInfo(result);
                    
                    // 切换到结果标签页
                    ImageTabControl.SelectedIndex = 1;
                    
                    UpdateStatus($"算法执行成功 - 耗时: {processingTime.TotalMilliseconds:F2}ms");
                    ProcessingTimeText.Text = $"处理时间: {processingTime.TotalMilliseconds:F2}ms";
                    
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
            // 暂时注释掉结果清理
            // ResultImageDisplay.ClearDisplay();
            _resultImage?.Dispose();
            _resultImage = null;
            
            ClearResultInfo();
            ImageTabControl.SelectedIndex = 0;
            
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
                Text = $"算法: {result.ProcessorName}\n处理时间: {result.ProcessingTime.TotalMilliseconds:F2}ms",
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
        
        #endregion
    }
}