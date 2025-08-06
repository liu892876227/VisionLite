// ROIToolWindow.xaml.cs
using System;
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
            // 确保我们有可以操作的背景图像
            if (_sourceImage == null || !_sourceImage.IsInitialized())
            {
                System.Windows.MessageBox.Show("ROI工具窗口中没有背景图像。请先在主窗口采集或加载图像。", "错误");
                RoiTypeComboBox.SelectedIndex = -1; // 重置选择
                return;
            }
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
                CreateDoubleUpDown("Row1:", "row1", 0, height.D - 1);
                CreateDoubleUpDown("Col1:", "column1", 0, width.D - 1);
                CreateDoubleUpDown("Row2:", "row2", 0, height.D - 1);
                CreateDoubleUpDown("Col2:", "column2", 0, width.D - 1);
            }
            else if (type == "rectangle2")
            {
                
                
            }
            // 可以为 Circle, Ellipse, Line 添加更多 else if 分支
        }

        /// <summary>
        /// 创建一个 DoubleUpDown 控件并添加到UI
        /// </summary>
        private void CreateDoubleUpDown(string label, string paramName, double min, double max)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new Label { Content = label, Width = 80 });

            // 使用 Xceed Toolkit 的 DoubleUpDown 控件
            var doubleUpDown = new DoubleUpDown
            {
                Value = _drawingObject.GetDrawingObjectParams(paramName),
                Minimum = min,
                Maximum = max,
                FormatString = "F2", // 显示两位小数
                Increment = 1.0,     // 对应我们之前的 SmallChange
                MinWidth = 120
            };

            // 订阅值改变事件
            doubleUpDown.ValueChanged += (sender, args) =>
            {
                if (_isUpdatingFromRoi) return;
                double? newValue = (double?)args.NewValue;

                // args.NewValue 是 nullable double (double?)，需要检查
                if (newValue.HasValue)
                {
                    try
                    {
                        _drawingObject.SetDrawingObjectParams(paramName, newValue.Value);
                        // 手动触发图像更新，形成闭环
                        UpdateRoiDisplay();
                    }
                    catch (HalconException) { /* 忽略设置失败 */ }
                }
            };

            sp.Children.Add(doubleUpDown);
            ParametersPanel.Children.Add(sp);
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
            this.Close();
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
    }
}