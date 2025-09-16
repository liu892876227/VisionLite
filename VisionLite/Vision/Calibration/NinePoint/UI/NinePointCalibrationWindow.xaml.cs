using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using HalconDotNet;
using VisionLite.Vision.Calibration.NinePoint.Core;
using VisionLite.Vision.Calibration.NinePoint.Storage;

namespace VisionLite.Vision.Calibration.NinePoint.UI
{
    /// <summary>
    /// 九点标定窗口
    /// </summary>
    public partial class NinePointCalibrationWindow : Window
    {
        private NinePointCalibrationProcessor _processor;
        private GlobalCalibrationManager _manager;
        private GlobalCalibrationService _globalService;
        private int _currentPointIndex = 1;
        private bool _isUpdatingUI = false;
        
        /// <summary>当前选中的标定点按钮</summary>
        private Button[] _pointButtons;
        
        public NinePointCalibrationWindow()
        {
            InitializeComponent();
            InitializeComponents();
            LoadInitialData();
        }
        
        #region 初始化
        
        private void InitializeComponents()
        {
            _processor = new NinePointCalibrationProcessor();
            _manager = GlobalCalibrationManager.Instance;
            _globalService = GlobalCalibrationService.Instance;
            
            // 初始化点按钮数组
            _pointButtons = new Button[]
            {
                btnPoint1, btnPoint2, btnPoint3,
                btnPoint4, btnPoint5, btnPoint6,
                btnPoint7, btnPoint8, btnPoint9
            };
            
            // 初始化组合框
            InitializeComboBoxes();
            
            // 订阅事件
            _manager.StatusChanged += Manager_StatusChanged;
            _manager.CalibrationUpdated += Manager_CalibrationUpdated;
            _manager.CalibrationListChanged += Manager_CalibrationListChanged;
            _globalService.CalibrationChanged += GlobalService_CalibrationChanged;
            
            // 设置数据绑定
            DataContext = _processor;
            
            // 初始化界面状态
            UpdateUI();
            UpdateGlobalCalibrationStatus();
        }
        
        private void InitializeComboBoxes()
        {
            // 物理单位 - 只支持毫米，禁用下拉框
            cmbPhysicalUnit.ItemsSource = new[] { "毫米" };
            cmbPhysicalUnit.SelectedIndex = 0;
            cmbPhysicalUnit.IsEnabled = false;
        }
        
        private void LoadInitialData()
        {
            // 加载已保存的标定配置
            RefreshCalibrationList();
            
            // 如果没有任何标定配置，创建默认标定
            if (cmbCalibrations.Items.Count == 0)
            {
                CreateNewCalibration("默认标定");
            }
            else
            {
                // 如果有标定配置但没有当前活动标定，自动选择第一个
                if (_manager.CurrentCalibration == null && cmbCalibrations.Items.Count > 0)
                {
                    var firstCalibrationName = cmbCalibrations.Items[0] as string;
                    if (!string.IsNullOrEmpty(firstCalibrationName))
                    {
                        cmbCalibrations.SelectedItem = firstCalibrationName;
                        _manager.SetCurrentCalibration(firstCalibrationName);
                        LoadCalibrationToProcessor();
                    }
                }
                else if (_manager.CurrentCalibration != null)
                {
                    // 如果管理器有当前标定，确保下拉框也选中它
                    cmbCalibrations.SelectedItem = _manager.CurrentCalibration.Name;
                    LoadCalibrationToProcessor();
                }
            }
        }
        
        
        #endregion
        
        #region 工具栏事件
        
        private void BtnNewCalibration_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CalibrationNameDialog();
            if (dialog.ShowDialog() == true)
            {
                CreateNewCalibration(dialog.CalibrationName, dialog.Description);
            }
        }
        
        private void BtnLoadCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCalibrations.SelectedItem is string selectedName)
            {
                _manager.SetCurrentCalibration(selectedName);
                LoadCalibrationToProcessor();
                UpdateUI();
                UpdateStatusBar($"已加载标定: {selectedName}");
            }
            else
            {
                // 如果没有选中项，但有标定配置，提示用户选择
                if (cmbCalibrations.Items.Count > 0)
                {
                    UpdateStatusBar("请先在下拉框中选择要加载的标定配置");
                    MessageBox.Show("请先在\"标定配置\"下拉框中选择要加载的标定配置。", "提示", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UpdateStatusBar("没有可用的标定配置");
                    MessageBox.Show("没有找到可用的标定配置，请先创建一个新的标定配置。", "提示", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        
        private async void BtnSaveCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (_manager.CurrentCalibration != null)
            {
                var currentName = _manager.CurrentCalibration.Name;
                var success = await _manager.SaveCalibrationAsync(_manager.CurrentCalibration);
                
                if (success)
                {
                    // 刷新配置列表并保持选中状态
                    RefreshCalibrationList();
                    cmbCalibrations.SelectedItem = currentName;
                    UpdateStatusBar("标定已保存");
                }
                else
                {
                    UpdateStatusBar("保存失败");
                }
            }
        }
        
        private async void BtnImportCalibration_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "标定文件 (*.vl9pc)|*.vl9pc|JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入标定文件"
            };
            
            if (openDialog.ShowDialog() == true)
            {
                var calibration = await CalibrationFileManager.ImportCalibrationAsync(openDialog.FileName);
                if (calibration != null)
                {
                    _manager.SavedCalibrations[calibration.Name] = calibration;
                    RefreshCalibrationList();
                    _manager.SetCurrentCalibration(calibration.Name);
                    LoadCalibrationToProcessor();
                    UpdateUI();
                    UpdateStatusBar("导入成功");
                }
                else
                {
                    MessageBox.Show("导入失败，请检查文件格式", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private async void BtnExportCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (_manager.CurrentCalibration == null)
            {
                MessageBox.Show("没有可导出的标定数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var saveDialog = new SaveFileDialog
            {
                Filter = "标定文件 (*.vl9pc)|*.vl9pc|JSON文件 (*.json)|*.json",
                Title = "导出标定文件",
                FileName = _manager.CurrentCalibration.Name
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                var success = await CalibrationFileManager.ExportCalibrationAsync(_manager.CurrentCalibration, saveDialog.FileName);
                UpdateStatusBar(success ? "导出成功" : "导出失败");
            }
        }
        
        /// <summary>当前加载的图像对象</summary>
        private HObject _currentImage = null;
        private HObject _calibrationMarkers = null;
        
        private void BtnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "图像文件 (*.jpg;*.jpeg;*.png;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件 (*.*)|*.*",
                Title = "加载标定图像"
            };
            
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // 使用Halcon加载图像
                    HOperatorSet.ReadImage(out _currentImage, openDialog.FileName);
                    
                    // 显示图像到Halcon控件（包含标定点标记）
                    halconWindow.HalconWindow.SetPart(0, 0, -1, -1); // 显示完整图像
                    RefreshImageDisplay();
                    
                    // 隐藏占位符文字
                    txtImagePlaceholder.Visibility = Visibility.Collapsed;
                    
                    // 获取图像尺寸
                    HOperatorSet.GetImageSize(_currentImage, out HTuple width, out HTuple height);
                    
                    var fileName = System.IO.Path.GetFileName(openDialog.FileName);
                    UpdateStatusBar($"已加载图像: {fileName} (尺寸: {width}×{height})");
                    
                    // 添加Halcon窗口的鼠标事件
                    halconWindow.HMouseDown += HalconWindow_MouseDown;
                    halconWindow.HMouseMove += HalconWindow_MouseMove;
                    
                    // 设置Halcon窗口属性
                    SetupHalconWindow();
                    
                    System.Diagnostics.Debug.WriteLine($"图像加载成功: {fileName}, 尺寸: {width}×{height}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatusBar("图像加载失败");
                    System.Diagnostics.Debug.WriteLine($"图像加载失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 设置Halcon窗口属性
        /// </summary>
        private void SetupHalconWindow()
        {
            try
            {
                // 设置窗口属性
                halconWindow.HalconWindow.SetWindowParam("background_color", "black");
                halconWindow.HalconWindow.SetDraw("margin");
                halconWindow.HalconWindow.SetLineWidth(2);
                
                // 启用鼠标和键盘事件
                halconWindow.HalconWindow.SetWindowParam("mouse_move_events", "true");
                halconWindow.HalconWindow.SetWindowParam("mouse_click_events", "true");
                
                System.Diagnostics.Debug.WriteLine("Halcon窗口设置完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Halcon窗口设置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Halcon窗口鼠标点击事件
        /// </summary>
        private void HalconWindow_MouseDown(object sender, HalconDotNet.HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (_currentImage != null)
            {
                try
                {
                    var row = e.Row;    // Y坐标
                    var column = e.Column; // X坐标
                    
                    // 检查坐标是否在图像范围内
                    HOperatorSet.GetImageSize(_currentImage, out HTuple width, out HTuple height);
                    
                    if (column >= 0 && column < width && row >= 0 && row < height)
                    {
                        // 更新当前点的图像坐标
                        _isUpdatingUI = true;
                        nudImageX.Value = (double)column;
                        nudImageY.Value = (double)row;
                        _isUpdatingUI = false;
                        
                        UpdateStatusBar($"图像坐标: ({(double)column:F1}, {(double)row:F1})");
                        System.Diagnostics.Debug.WriteLine($"点击坐标: ({(double)column:F2}, {(double)row:F2})");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标点击事件处理失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Halcon窗口鼠标移动事件
        /// </summary>
        private void HalconWindow_MouseMove(object sender, HalconDotNet.HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (_currentImage != null)
            {
                try
                {
                    var row = e.Row;    // Y坐标
                    var column = e.Column; // X坐标
                    
                    // 检查坐标是否在图像范围内
                    HOperatorSet.GetImageSize(_currentImage, out HTuple width, out HTuple height);
                    
                    if (column >= 0 && column < width && row >= 0 && row < height)
                    {
                        // 在状态栏显示实时坐标
                        UpdateStatusBar($"鼠标位置: ({(double)column:F1}, {(double)row:F1})");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标移动事件处理失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 绘制标定点标记到Halcon显示窗口
        /// </summary>
        private void DrawCalibrationMarkers()
        {
            if (_currentImage == null || halconWindow?.HalconWindow == null) return;
            
            try
            {
                // 清除之前的标记
                if (_calibrationMarkers != null)
                {
                    _calibrationMarkers.Dispose();
                    _calibrationMarkers = null;
                }
                
                // 创建标记容器
                HOperatorSet.GenEmptyObj(out _calibrationMarkers);
                
                // 获取已设置的点
                var setPoints = _processor.CurrentCalibration.PointPairs.Where(p => p.IsSet).ToList();
                
                if (setPoints.Any())
                {
                    // 为每个已设置的点创建绿色十字标记
                    foreach (var point in setPoints)
                    {
                        var x = point.ImagePoint.X;
                        var y = point.ImagePoint.Y;
                        
                        // 创建十字标记
                        CreateCrossMarker(x, y, out HObject crossMarker);
                        HOperatorSet.ConcatObj(_calibrationMarkers, crossMarker, out HObject tempMarkers);
                        
                        _calibrationMarkers.Dispose();
                        _calibrationMarkers = tempMarkers;
                        crossMarker.Dispose();
                    }
                    
                    // 设置显示颜色为绿色
                    halconWindow.HalconWindow.SetColor("green");
                    halconWindow.HalconWindow.SetLineWidth(2);
                    
                    // 显示标记
                    halconWindow.HalconWindow.DispObj(_calibrationMarkers);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"绘制标定点标记失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建十字标记
        /// </summary>
        private void CreateCrossMarker(double x, double y, out HObject crossMarker)
        {
            var size = 10; // 十字大小
            
            // 创建水平线
            HOperatorSet.GenRegionLine(out HObject hLine, y, x - size, y, x + size);
            
            // 创建垂直线
            HOperatorSet.GenRegionLine(out HObject vLine, y - size, x, y + size, x);
            
            // 合并十字线
            HOperatorSet.Union2(hLine, vLine, out crossMarker);
            
            hLine.Dispose();
            vLine.Dispose();
        }
        
        
        /// <summary>
        /// 刷新图像显示（包含标定点标记）
        /// </summary>
        private void RefreshImageDisplay()
        {
            if (_currentImage != null && halconWindow?.HalconWindow != null)
            {
                // 显示图像
                halconWindow.HalconWindow.DispObj(_currentImage);
                
                // 绘制标定点标记
                DrawCalibrationMarkers();
            }
        }
        
        private async void BtnStartCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (_processor.CurrentCalibration.IsDataComplete(_processor.MinimumPoints))
            {
                // 执行标定计算
                var result = await _processor.ProcessAsync(null);
                
                if (result.Success)
                {
                    UpdateStatusBar("标定计算成功");
                    UpdateCalibrationResults();
                    
                    // 保存结果到管理器
                    if (_manager.CurrentCalibration != null)
                    {
                        var currentName = _manager.CurrentCalibration.Name;
                        
                        _manager.CurrentCalibration.TransformMatrix = _processor.CurrentCalibration.TransformMatrix;
                        _manager.CurrentCalibration.InverseTransformMatrix = _processor.CurrentCalibration.InverseTransformMatrix;
                        _manager.CurrentCalibration.CalibrationError = _processor.CurrentCalibration.CalibrationError;
                        _manager.CurrentCalibration.MaxPointError = _processor.CurrentCalibration.MaxPointError;
                        _manager.CurrentCalibration.PixelScale = _processor.CurrentCalibration.PixelScale;
                        _manager.CurrentCalibration.Quality = _processor.CurrentCalibration.Quality;
                        _manager.CurrentCalibration.IsValid = _processor.CurrentCalibration.IsValid;
                        
                        var saveSuccess = await _manager.SaveCalibrationAsync(_manager.CurrentCalibration);
                        
                        if (saveSuccess)
                        {
                            // 刷新配置列表并保持选中状态
                            RefreshCalibrationList();
                            cmbCalibrations.SelectedItem = currentName;
                        }
                    }
                }
                else
                {
                    UpdateStatusBar($"标定失败: {result.ErrorMessage}");
                    MessageBox.Show($"标定失败: {result.ErrorMessage}", "标定错误", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show($"标定点数量不足，需要至少{_processor.MinimumPoints}个点", "提示", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void BtnApplyCalibration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查当前是否有有效的标定
                if (_manager.CurrentCalibration == null)
                {
                    MessageBox.Show("没有可用的标定配置，请先选择一个标定配置。", "提示", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (!_manager.CurrentCalibration.IsValid)
                {
                    MessageBox.Show($"标定配置 '{_manager.CurrentCalibration.Name}' 无效，请先完成标定计算。", "提示", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 应用标定到全局服务
                var success = _globalService.ApplyCalibration(_manager.CurrentCalibration);
                
                if (success)
                {
                    UpdateStatusBar($"标定 '{_manager.CurrentCalibration.Name}' 已成功应用");
                }
                else
                {
                    UpdateStatusBar("标定应用失败");
                    MessageBox.Show("标定应用失败，请检查标定数据的有效性。", "错误", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusBar($"应用标定时发生错误: {ex.Message}");
                MessageBox.Show($"应用标定时发生错误: {ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"应用标定异常: {ex}");
            }
        }
        
        private void BtnResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要重置当前标定吗？", "确认", 
                                       MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _processor.ClearCalibrationPoints();
                UpdateUI();
                UpdateStatusBar("标定已重置");
            }
        }
        
        #endregion
        
        #region 标定点事件
        
        private void BtnPoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int pointIndex))
            {
                _currentPointIndex = pointIndex;
                UpdateCurrentPointSelection();
                UpdatePointDetails();
                
                UpdateStatusBar($"已选择点{pointIndex}");
            }
        }
        
        
        private void BtnSetCurrentPoint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var imageX = nudImageX.Value ?? 0;
                var imageY = nudImageY.Value ?? 0;
                var worldX = nudWorldX.Value ?? 0;
                var worldY = nudWorldY.Value ?? 0;
                
                var imagePoint = new Point2D(imageX, imageY);
                var worldPoint = new Point2D(worldX, worldY);
                var pointName = $"点{_currentPointIndex}";
                
                _processor.AddCalibrationPoint(pointName, imagePoint, worldPoint);
                
                UpdateUI();
                UpdateStatusBar($"已设置{pointName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置点坐标失败: {ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnClearCurrentPoint_Click(object sender, RoutedEventArgs e)
        {
            var pointName = $"点{_currentPointIndex}";
            var existingPoint = _processor.CurrentCalibration.PointPairs.FirstOrDefault(p => p.PointName == pointName);
            if (existingPoint != null)
            {
                existingPoint.IsSet = false;
                existingPoint.ImagePoint = new Point2D();
                existingPoint.WorldPoint = new Point2D();
                existingPoint.FitError = 0;
                
                UpdateUI();
                UpdateStatusBar($"已清除{pointName}");
            }
        }
        
        #endregion
        
        #region 界面更新
        
        private void UpdateUI()
        {
            if (_isUpdatingUI) return;
            _isUpdatingUI = true;
            
            try
            {
                UpdatePointButtons();
                UpdatePointDetails();
                UpdateCalibrationResults();
                UpdatePointDataGrid();
                UpdateCurrentPointSelection();
                UpdateGlobalCalibrationStatus();
                
                // 刷新图像显示（包含标定点标记）
                RefreshImageDisplay();
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }
        
        private void UpdatePointButtons()
        {
            for (int i = 0; i < _pointButtons.Length; i++)
            {
                var pointName = $"点{i + 1}";
                var point = _processor.CurrentCalibration.PointPairs.FirstOrDefault(p => p.PointName == pointName);
                var button = _pointButtons[i];
                
                if (point?.IsSet == true)
                {
                    button.Background = Brushes.LightGreen;
                    button.Content = $"{pointName}✓";
                }
                else
                {
                    button.Background = Brushes.LightGray;
                    button.Content = pointName;
                }
            }
        }
        
        private void UpdateCurrentPointSelection()
        {
            for (int i = 0; i < _pointButtons.Length; i++)
            {
                if (i + 1 == _currentPointIndex)
                {
                    _pointButtons[i].BorderBrush = Brushes.Blue;
                    _pointButtons[i].BorderThickness = new Thickness(3);
                }
                else
                {
                    _pointButtons[i].BorderBrush = Brushes.Gray;
                    _pointButtons[i].BorderThickness = new Thickness(1);
                }
            }
        }
        
        private void UpdatePointDetails()
        {
            var pointName = $"点{_currentPointIndex}";
            var point = _processor.CurrentCalibration.PointPairs.FirstOrDefault(p => p.PointName == pointName);
            
            txtCurrentPointName.Text = pointName;
            
            if (point?.IsSet == true)
            {
                nudImageX.Value = point.ImagePoint.X;
                nudImageY.Value = point.ImagePoint.Y;
                nudWorldX.Value = point.WorldPoint.X;
                nudWorldY.Value = point.WorldPoint.Y;
            }
            else
            {
                nudImageX.Value = 0;
                nudImageY.Value = 0;
                nudWorldX.Value = 0;
                nudWorldY.Value = 0;
            }
        }
        
        private void UpdateCalibrationResults()
        {
            var calibration = _processor.CurrentCalibration;
            
            txtCalibrationStatus.Text = calibration.IsValid ? "有效" : "无效";
            txtCalibrationStatus.Foreground = calibration.IsValid ? Brushes.Green : Brushes.Red;
            
            txtValidPointCount.Text = $"{calibration.ValidPointCount}/{_processor.MinimumPoints}";
            
            if (calibration.IsValid)
            {
                txtCalibrationError.Text = $"{calibration.CalibrationError:F3} mm";
                txtCalibrationQuality.Text = GetQualityDescription(calibration.Quality);
                txtPixelScale.Text = $"{calibration.PixelScale:F3} 像素/mm";
            }
            else
            {
                txtCalibrationError.Text = "N/A";
                txtCalibrationQuality.Text = "N/A";
                txtPixelScale.Text = "N/A";
            }
        }
        
        private void UpdatePointDataGrid()
        {
            dgPointDetails.ItemsSource = null;
            dgPointDetails.ItemsSource = _processor.CurrentCalibration.PointPairs;
        }
        
        private void UpdateStatusBar(string message = "就绪")
        {
            txtStatusBar.Text = message;
            txtCurrentCalibrationName.Text = _manager.CurrentCalibration?.Name ?? "无";
        }
        
        #endregion
        
        #region 辅助方法
        
        private void CreateNewCalibration(string name, string description = "")
        {
            _manager.CreateNewCalibration(name, description);
            _processor.CalibrationName = name;
            _processor.ClearCalibrationPoints();

            RefreshCalibrationList();
            cmbCalibrations.SelectedItem = name;
            // 手动更新标定名称文本框
            txtCalibrationName.Text = name;
            UpdateUI();
            UpdateStatusBar($"创建新标定: {name}");
        }
        
        private void LoadCalibrationToProcessor()
        {
            if (_manager.CurrentCalibration != null)
            {
                _processor.CurrentCalibration = _manager.CurrentCalibration;
                _processor.CalibrationName = _manager.CurrentCalibration.Name;
                _processor.Unit = _manager.CurrentCalibration.Unit;
                // 手动更新标定名称文本框
                txtCalibrationName.Text = _manager.CurrentCalibration.Name;
                // 物理单位固定为毫米，无需设置
            }
        }
        
        private void RefreshCalibrationList()
        {
            cmbCalibrations.ItemsSource = null;
            cmbCalibrations.ItemsSource = _manager.GetCalibrationNames();
        }
        
        private string GetQualityDescription(CalibrationQuality quality)
        {
            return quality switch
            {
                CalibrationQuality.Excellent => "优秀",
                CalibrationQuality.Good => "良好",
                CalibrationQuality.Acceptable => "可接受",
                CalibrationQuality.Poor => "较差",
                CalibrationQuality.Unusable => "不可用",
                _ => "未知"
            };
        }
        
        #endregion
        
        #region 事件处理
        
        private void Manager_StatusChanged(object sender, CalibrationStatusChangedEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateUI());
        }
        
        private void Manager_CalibrationUpdated(object sender, CalibrationDataUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateUI());
        }
        
        private void Manager_CalibrationListChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() => 
            {
                var currentSelection = cmbCalibrations.SelectedItem as string;
                RefreshCalibrationList();
                
                // 如果之前有选中的项且还存在，则保持选中
                if (!string.IsNullOrEmpty(currentSelection) && 
                    _manager.GetCalibrationNames().Contains(currentSelection))
                {
                    cmbCalibrations.SelectedItem = currentSelection;
                }
            });
        }
        
        private void CmbCalibrations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingUI && cmbCalibrations.SelectedItem is string selectedName)
            {
                _manager.SetCurrentCalibration(selectedName);
                LoadCalibrationToProcessor();
                UpdateUI();
            }
        }
        
        private void BtnDeleteCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCalibrations.SelectedItem is string selectedName)
            {
                var result = MessageBox.Show($"确定要删除标定 '{selectedName}' 吗？", "确认删除", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _manager.DeleteCalibration(selectedName);
                    RefreshCalibrationList();
                    UpdateUI();
                }
            }
        }
        
        private void BtnDuplicateCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (_manager.CurrentCalibration != null)
            {
                var dialog = new CalibrationNameDialog();
                dialog.CalibrationName = _manager.CurrentCalibration.Name + "_副本";
                
                if (dialog.ShowDialog() == true)
                {
                    var newCalibration = new NinePointCalibrationData
                    {
                        Name = dialog.CalibrationName,
                        Description = dialog.Description,
                        PointPairs = _manager.CurrentCalibration.PointPairs.Select(p => new CalibrationPointPair(p.PointName)
                        {
                            ImagePoint = new Point2D(p.ImagePoint.X, p.ImagePoint.Y),
                            WorldPoint = new Point2D(p.WorldPoint.X, p.WorldPoint.Y),
                            IsSet = p.IsSet,
                            FitError = p.FitError,
                            Confidence = p.Confidence
                        }).ToList(),
                        TransformType = _manager.CurrentCalibration.TransformType,
                        Unit = _manager.CurrentCalibration.Unit
                    };
                    
                    _manager.SavedCalibrations[newCalibration.Name] = newCalibration;
                    RefreshCalibrationList();
                    _manager.SetCurrentCalibration(newCalibration.Name);
                    LoadCalibrationToProcessor();
                    UpdateUI();
                }
            }
        }
        
        private void DgPointDetails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgPointDetails.SelectedItem is CalibrationPointPair selectedPoint)
            {
                var pointNumber = selectedPoint.PointName.Replace("点", "");
                if (int.TryParse(pointNumber, out int index))
                {
                    _currentPointIndex = index;
                    UpdateCurrentPointSelection();
                    UpdatePointDetails();
                }
            }
        }
        
        #endregion
        
        // 注意：此方法已被HalconWindow_MouseDown替代，保留用于向后兼容
        private void ImgDisplay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 方法已移除，使用Halcon控件的HalconWindow_MouseDown
        }
        
        // 注意：此方法已被HalconWindow_MouseMove替代，保留用于向后兼容
        private void ImgDisplay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 方法已移除，使用Halcon控件的HalconWindow_MouseMove
        }
        
        /// <summary>
        /// 全局标定服务状态变更事件处理
        /// </summary>
        private void GlobalService_CalibrationChanged(object sender, CalibrationChangedEventArgs e)
        {
            // 在UI线程中更新状态显示
            if (Dispatcher.CheckAccess())
            {
                UpdateGlobalCalibrationStatus();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(UpdateGlobalCalibrationStatus));
            }
        }
        
        /// <summary>
        /// 更新全局标定状态显示
        /// </summary>
        private void UpdateGlobalCalibrationStatus()
        {
            try
            {
                if (txtGlobalCalibrationStatus != null)
                {
                    txtGlobalCalibrationStatus.Text = _globalService.CalibrationStatus;
                    
                    // 根据状态设置颜色
                    if (_globalService.IsCalibrationActive)
                    {
                        txtGlobalCalibrationStatus.Foreground = new SolidColorBrush(Colors.DarkGreen);
                    }
                    else
                    {
                        txtGlobalCalibrationStatus.Foreground = new SolidColorBrush(Colors.DarkRed);
                    }
                }
                
                // 更新应用标定按钮的可用性
                if (btnApplyCalibration != null)
                {
                    btnApplyCalibration.IsEnabled = _manager.CurrentCalibration?.IsValid == true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新全局标定状态失败: {ex.Message}");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // 取消事件订阅
            _manager.StatusChanged -= Manager_StatusChanged;
            _manager.CalibrationUpdated -= Manager_CalibrationUpdated;
            _manager.CalibrationListChanged -= Manager_CalibrationListChanged;
            _globalService.CalibrationChanged -= GlobalService_CalibrationChanged;
            
            // 释放Halcon资源
            if (_currentImage != null)
            {
                _currentImage.Dispose();
                _currentImage = null;
            }
            
            if (_calibrationMarkers != null)
            {
                _calibrationMarkers.Dispose();
                _calibrationMarkers = null;
            }
        }
    }
}