// ROIToolWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;
using System.Windows.Threading;

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
                MessageBox.Show("ROI工具窗口中没有背景图像。请先在主窗口采集或加载图像。", "错误");
                RoiTypeComboBox.SelectedIndex = -1; // 重置选择
                return;
            }
            // 清理旧的绘图对象和UI
            DetachAndDisposeDrawingObject();
            ParametersPanel.Children.Clear();
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
                UpdateRoiDisplay();
                UpdateParametersUI();
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

            // 根据ROI类型显示不同的参数
            if (type == "rectangle1")
            {
                HTuple row1 = _drawingObject.GetDrawingObjectParams("row1");
                HTuple col1 = _drawingObject.GetDrawingObjectParams("column1");
                HTuple row2 = _drawingObject.GetDrawingObjectParams("row2");
                HTuple col2 = _drawingObject.GetDrawingObjectParams("column2");
                ParametersPanel.Children.Add(CreateParamLabel($"Row1: {row1.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Col1: {col1.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Row2: {row2.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Col2: {col2.D:F2}"));
            }
            else if (type == "rectangle2")
            {
                HTuple row = _drawingObject.GetDrawingObjectParams("row");
                HTuple col = _drawingObject.GetDrawingObjectParams("column");
                HTuple phi = _drawingObject.GetDrawingObjectParams("phi");
                HTuple len1 = _drawingObject.GetDrawingObjectParams("length1");
                HTuple len2 = _drawingObject.GetDrawingObjectParams("length2");
                ParametersPanel.Children.Add(CreateParamLabel($"Center Row: {row.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Center Col: {col.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Angle (deg): {(phi.D * 180 / Math.PI):F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Length1 (Radius1): {len1.D:F2}"));
                ParametersPanel.Children.Add(CreateParamLabel($"Length2 (Radius2): {len2.D:F2}"));
            }
            // 可以为 Circle, Ellipse, Line 添加更多 else if 分支
        }

        private TextBlock CreateParamLabel(string content)
        {
            return new TextBlock { Text = content, Margin = new Thickness(5) };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_drawingObject == null || _drawingObject.ID == -1)
            {
                MessageBox.Show("您还没有创建有效的ROI！", "提示");
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