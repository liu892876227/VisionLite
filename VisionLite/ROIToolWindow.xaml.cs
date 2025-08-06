// ROIToolWindow.xaml.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit;

namespace VisionLite
{
    // 定义一个委托，用于 ROI 确认事件
    public delegate void ROIAcceptedEventHandler(HObject roi);

    public partial class ROIToolWindow : Window
    {
        public event ROIAcceptedEventHandler ROIAccepted;

        // --- 私有成员 ---
        private HObject _sourceImage;       // 从主窗口接收的原始图像
        private HDrawingObject _drawingObject; // 当前活动的ROI绘图对象
        public HObject CreatedROI { get; private set; } // 创建的最终ROI区域

        private bool _isUpdatingFromRoi = false; // 标志位，防止UI更新触发ROI更新的死循环
        public ROIToolWindow()
        {
            InitializeComponent();
            
        }

        /// <summary>
        /// 公共方法：由主窗口调用，用于更新或设置背景图像
        /// </summary>
        public void UpdateImage(HObject newImage)
        {
            // 使用Dispatcher确保在UI线程上操作
            Dispatcher.Invoke(() =>
            {
                // 先释放旧的图像资源
                _sourceImage?.Dispose();
                // 复制一份新的图像，防止多线程问题和意外释放
                _sourceImage = newImage?.CopyObj(1, -1);

                // 如果已经有ROI在绘制，则在显示新背景图后，需要更新ROI窗口的显示
                if (_drawingObject != null && _drawingObject.ID != -1)
                {
                    UpdateRoiDisplay();
                }
                else // 否则，只清空ROI窗口
                {
                    HSmartROI.HalconWindow.ClearWindow();
                }
            });
        }

        /// <summary>
        /// 当在下拉框中选择一个新的ROI形状时触发
        /// </summary>
        private void RoiTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoiTypeComboBox.SelectedItem == null) return;
            
            // 清理旧的绘图对象和UI
            DetachAndDisposeDrawingObject();
            
            // 获取图像尺寸，用于计算初始ROI的位置和大小
            HOperatorSet.GetImageSize(_sourceImage, out HTuple width, out HTuple height);

            var selectedItem = (RoiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();


            // 根据选择创建不同类型的HDrawingObject
            switch (selectedItem)
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
                default:
                    return; // 如果没有匹配项，则退出
            }

            // 将新创建的绘图对象附加到主窗口的HSmart1上
            if (Owner is MainWindow mainWindow)
            {
                // 先刷新主窗口的显示，清除可能残留的旧ROI
                HObject mainImage = mainWindow.HSmart1.Tag as HObject;
                if (mainImage != null && mainImage.IsInitialized())
                {
                    mainWindow.HSmart1.HalconWindow.DispObj(mainImage);
                }

                // 再附加新的绘图对象
                mainWindow.HSmart1.HalconWindow.AttachDrawingObjectToWindow(_drawingObject);
            }

            // 订阅绘图对象的回调事件，以实现实时更新
            _drawingObject.OnDrag(OnRoiUpdate);
            _drawingObject.OnResize(OnRoiUpdate);
            _drawingObject.OnSelect(OnRoiUpdate);

            // 首次手动调用一次，以显示初始ROI的内容和参数
            OnRoiUpdate(_drawingObject, null, null);
        }

        /// <summary>
        /// 当ROI被拖动、缩放或选中时触发的回调函数
        /// </summary>
        private void OnRoiUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {
            // 使用Dispatcher确保在UI线程上操作
            Dispatcher.Invoke(() =>
            {
                _isUpdatingFromRoi = true; // 设置标志位，表示本次更新来自ROI拖动
                
                UpdateParametersUI();
                UpdateRoiDisplay();

                // --- 启用按钮并更新Tooltip ---
                if (_drawingObject != null && _drawingObject.ID != -1)
                {
                    SaveROIImageButton.IsEnabled = true;
                    string savePath = Path.Combine("D:\\", "VisionLite图像保存");
                    string fileName = $"ROI_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                    SaveROIImageButton.ToolTip = $"保存ROI图像到: \n{savePath}";
                }
                _isUpdatingFromRoi = false; // 恢复标志位
            });
        }

        /// <summary>
        /// 核心功能：更新本窗口(ROI工具)的图像显示，使其只显示ROI区域的内容
        /// </summary>
        private void UpdateRoiDisplay()
        {
            if (_sourceImage == null || !_sourceImage.IsInitialized() || _drawingObject == null || _drawingObject.ID == -1)
                return;

            HObject roiRegion = _drawingObject.GetDrawingObjectIconic();
            string roiType = _drawingObject.GetDrawingObjectParams("type");

            if (!roiType.Contains("line"))
            {
                if (roiRegion.CountObj() > 0)
                {
                    HOperatorSet.ReduceDomain(_sourceImage, roiRegion, out HObject imageReduced);
                    HOperatorSet.CropDomain(imageReduced, out HObject imageCropped);

                    HWindow roiWindow = HSmartROI.HalconWindow;
                    roiWindow.ClearWindow();
                    HOperatorSet.GetImageSize(imageCropped, out HTuple width, out HTuple height);
                    // 适配显示，防止图像过小或过大
                    if (height.D > 0 && width.D > 0)
                    {
                        roiWindow.SetPart(0, 0, height.D - 1, width.D - 1);
                    }
                    roiWindow.DispObj(imageCropped);

                    imageCropped.Dispose();
                    imageReduced.Dispose();
                }
            }
            else
            {
                // 对于直线，我们不在ROI窗口中显示任何内容，只清空它
                HSmartROI.HalconWindow.ClearWindow();
            }
                
            roiRegion.Dispose();
        }

        /// <summary>
        /// 更新UI上的参数信息
        /// </summary>
        private void UpdateParametersUI()
        {
            if (_drawingObject == null || _drawingObject.ID == -1) return;

            ParametersPanel.Children.Clear();
            string type = _drawingObject.GetDrawingObjectParams("type");
            HOperatorSet.GetImageSize(_sourceImage, out HTuple width, out HTuple height);

            // 根据ROI类型显示不同的参数
            if (type == "rectangle1")
            {
                // 使用 ParameterTranslator.Translate() 获取标签 ***
                CreateDoubleUpDown(ParameterTranslator.Translate("row1"), "row1", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column1"), "column1", 0, width.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("row2"), "row2", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column2"), "column2", 0, width.D - 1);
            }
            else if (type == "rectangle2")
            {
                CreateDoubleUpDown(ParameterTranslator.Translate("row"), "row", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column"), "column", 0, width.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("phi"), "phi", -180, 180, true); // 角度范围用度更直观
                CreateDoubleUpDown(ParameterTranslator.Translate("length1"), "length1", 0, Math.Max(width.D, height.D));
                CreateDoubleUpDown(ParameterTranslator.Translate("length2"), "length2", 0, Math.Max(width.D, height.D));
            }
            else if (type == "circle")
            {
                CreateDoubleUpDown(ParameterTranslator.Translate("row"), "row", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column"), "column", 0, width.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("radius"), "radius", 0, Math.Max(width.D, height.D));
            }
            else if (type == "ellipse")
            {
                CreateDoubleUpDown(ParameterTranslator.Translate("row"), "row", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column"), "column", 0, width.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("phi"), "phi", -180, 180, true); // 角度范围用度更直观
                CreateDoubleUpDown(ParameterTranslator.Translate("radius1"), "radius1", 0, Math.Max(width.D, height.D));
                CreateDoubleUpDown(ParameterTranslator.Translate("radius2"), "radius2", 0, Math.Max(width.D, height.D));
            }
            else if (type == "line")
            {
                CreateDoubleUpDown(ParameterTranslator.Translate("row1"), "row1", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column1"), "column1", 0, width.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("row2"), "row2", 0, height.D - 1);
                CreateDoubleUpDown(ParameterTranslator.Translate("column2"), "column2", 0, width.D - 1);
            }
            
        }

        /// <summary>
        /// 创建一个 DoubleUpDown 控件并添加到UI
        /// </summary>
        private void CreateDoubleUpDown(string label, string paramName, double min, double max)
        {
            CreateDoubleUpDown(label, paramName, min, max, false); // 调用新的重载方法
        }


        /// <summary>
        /// 辅助方法：创建一个 DoubleUpDown 控件并添加到UI (带角度处理的重载版本)
        /// </summary>
        private void CreateDoubleUpDown(string label, string paramName, double min, double max, bool isAngle)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new Label { Content = label, Width = 120 });

            double initialValue = _drawingObject.GetDrawingObjectParams(paramName);
            // 如果是角度，从弧度转换为度
            if (isAngle)
            {
                initialValue = initialValue * 180 / Math.PI;
                min = min * 180 / Math.PI;
                max = max * 180 / Math.PI;
            }

            var doubleUpDown = new DoubleUpDown
            {
                Value = initialValue,
                Minimum = min,
                Maximum = max,
                FormatString = "F2",
                Increment = 1.0,
                MinWidth = 120
            };

            doubleUpDown.ValueChanged += (sender, args) =>
            {
                if (_isUpdatingFromRoi) return;
                double? newValue = (double?)args.NewValue;

                if (newValue.HasValue)
                {
                    double valueToSet = newValue.Value;
                    // 如果是角度，从度转换回弧度再设置
                    if (isAngle)
                    {
                        valueToSet = valueToSet * Math.PI / 180;
                    }

                    try
                    {
                        _drawingObject.SetDrawingObjectParams(paramName, valueToSet);
                        UpdateRoiDisplay();
                    }
                    catch (HalconException) { /* 忽略设置失败 */ }
                }
            };

            sp.Children.Add(doubleUpDown);
            ParametersPanel.Children.Add(sp);
        }

        private void SaveROIImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 再次确认ROI是否有效
            if (_sourceImage == null || !_sourceImage.IsInitialized() || _drawingObject == null || _drawingObject.ID == -1)
            {
                UpdateStatus("错误: 没有有效的ROI可以保存。");
                return;
            }

            try
            {
                // 获取ROI区域内的图像
                HObject roiRegion = _drawingObject.GetDrawingObjectIconic();
                HOperatorSet.ReduceDomain(_sourceImage, roiRegion, out HObject imageReduced);
                HOperatorSet.CropDomain(imageReduced, out HObject imageToSave);

                // 定义并创建保存路径
                string savePath = Path.Combine("D:\\", "VisionLite图像保存");
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                // 生成唯一文件名
                string fileName = $"ROI_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                string fullPath = Path.Combine(savePath, fileName);

                // 保存图像
                HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);

                // 释放临时对象
                roiRegion.Dispose();
                imageReduced.Dispose();
                imageToSave.Dispose();

                // 给出成功反馈
                UpdateStatus($"图像已成功保存到：{fullPath}");
            }
            catch (Exception ex)
            {
                // 8. 处理所有可能的异常
                UpdateStatus($"保存失败: {ex.Message}");
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_drawingObject == null || _drawingObject.ID == -1)
            {
                System.Windows.MessageBox.Show("您还没有创建有效的ROI！", "提示");
                return;
            }

            CreatedROI = _drawingObject.GetDrawingObjectIconic();
            ROIAccepted?.Invoke(CreatedROI.CopyObj(1, -1));
            
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 在窗口关闭时，确保从主窗口分离并释放绘图对象
            DetachAndDisposeDrawingObject();
            _sourceImage?.Dispose();
            CreatedROI?.Dispose();
        }

        /// <summary>
        /// 辅助方法：安全地从主窗口分离并释放当前的绘图对象
        /// </summary>
        private void DetachAndDisposeDrawingObject()
        {
            if (_drawingObject != null && _drawingObject.ID != -1)
            {
                if (Owner is MainWindow mainWindow)
                {
                    try { mainWindow.HSmart1.HalconWindow.DetachDrawingObjectFromWindow(_drawingObject); } catch (HalconException) { }
                }
                _drawingObject.Dispose();
                _drawingObject = null;
            }
        }

        /// <summary>
        /// 辅助方法：更新状态栏文本，并在一段时间后自动清除
        /// </summary>
        private async void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
            // 等待5秒
            await System.Threading.Tasks.Task.Delay(5000);
            // 如果5秒后状态栏文本还是这个消息，就把它清除
            if (StatusTextBlock.Text == message)
            {
                StatusTextBlock.Text = "准备就绪";
            }
        }
    }
}