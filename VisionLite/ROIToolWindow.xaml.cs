// ROIToolWindow.xaml.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit;
using System.Diagnostics;
using System.Linq;

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

                //// 设置本窗口(HSmartROI)的坐标系，让它和源图像完全一样大。
                //// 这样，后续所有显示操作都会基于这个完整的坐标系。
                //if (_sourceImage != null && _sourceImage.IsInitialized())
                //{
                //    HOperatorSet.GetImageSize(_sourceImage, out HTuple width, out HTuple height);
                //    HSmartROI.HalconWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                //}

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
            if (RoiTypeComboBox.SelectedItem == null)
            {

                return;
            }

            //调试信息
            Console.WriteLine("--- RoiTypeComboBox_SelectionChanged START ---");

            if (_sourceImage == null || !_sourceImage.IsInitialized())
            {
                Console.WriteLine("!!! ERROR: _sourceImage is null or not initialized at the beginning of SelectionChanged.");
                System.Windows.MessageBox.Show("ROI工具窗口中没有有效的背景图像。请在主窗口重新采集或加载图像。", "错误");
                // 重置下拉框，防止用户再次点击
                RoiTypeComboBox.SelectedIndex = -1;
                return;
            }


            // 清理旧的绘图对象和UI
            DetachAndDisposeDrawingObject();

            // 获取图像尺寸，用于计算初始ROI的位置和大小
            HOperatorSet.GetImageSize(_sourceImage, out HTuple width, out HTuple height);

            var selectedItem = (RoiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            //调试信息
            Console.WriteLine($"Selected ROI Type: {selectedItem}");

            // 根据选择创建不同类型的HDrawingObject
            try
            {
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

                    case "自由轮廓 (Contour)":
                        HTuple initialRows = new HTuple(
                       height.D / 4,    // 左上 Row
                       height.D / 4,    // 右上 Row
                       height.D * 0.75, // 右下 Row
                       height.D * 0.75 // 左下 Row
                       //height.D / 4     // 回到左上 Row (闭合)
                       );
                        HTuple initialCols = new HTuple(
                            width.D / 4,     // 左上 Col
                            width.D * 0.75,  // 右上 Col
                            width.D * 0.75,  // 右下 Col
                            width.D / 4    // 左下 Col
                            //width.D / 4      // 回到左上 Col (闭合)
                        );
                        _drawingObject = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.XLD_CONTOUR, initialRows, initialCols);

                        break;

                    default:
                        //调试信息
                        Console.WriteLine("No matching ROI type found. Exiting.");
                        return; // 如果没有匹配项，则退出
                }


                //调试信息
                Console.WriteLine("Drawing object created successfully.");


                // 将新创建的绘图对象附加到主窗口的HSmart1上
                if (Owner is MainWindow mainWindow)
                {
                    //调试信息
                    Console.WriteLine("Owner is MainWindow. Preparing to attach...");

                    HWindow window = mainWindow.HSmart1.HalconWindow;

                    // 先刷新主窗口的显示，清除可能残留的旧ROI
                    HObject mainImage = mainWindow.HSmart1.Tag as HObject;
                    if (mainImage != null && mainImage.IsInitialized())
                    {
                        HOperatorSet.AttachBackgroundToWindow(mainImage, window);

                        //调试信息
                        Console.WriteLine("Main window image redisplayed.");

                    }
                    // 订阅所有交互事件
                    _drawingObject.OnDrag(OnRoiUpdate);
                    _drawingObject.OnResize(OnRoiUpdate);
                    _drawingObject.OnSelect(OnRoiUpdate);

                    //调试信息
                    _drawingObject.OnAttach((dobj, hwin, type) =>
                    {
                        Console.WriteLine($"*** EVENT: OnAttach triggered! Type: {type} ***");
                        OnRoiUpdate(dobj, hwin, type);
                    });
                    Console.WriteLine("All events subscribed.");


                    // 最后再将绘图对象附加到窗口，这会触发 on_attach 事件
                    window.AttachDrawingObjectToWindow(_drawingObject);

                    //调试信息
                    Console.WriteLine("AttachDrawingObjectToWindow called.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! EXCEPTION CAUGHT in SelectionChanged: {ex.Message}");
                System.Windows.MessageBox.Show($"创建ROI时发生错误: {ex.Message}");
            }

            Console.WriteLine("--- RoiTypeComboBox_SelectionChanged END ---");
        }

        /// <summary>
        /// 当ROI被拖动、缩放或选中时触发的回调函数
        /// </summary>
        private void OnRoiUpdate(HDrawingObject dobj, HWindow hwin, string type)
        {

            Console.WriteLine($"--- OnRoiUpdate triggered by '{type}' ---");

            // 使用Dispatcher确保在UI线程上操作
            Dispatcher.Invoke(() =>
            {
                _isUpdatingFromRoi = true; // 设置标志位，表示本次更新来自ROI拖动

                Console.WriteLine("Updating Parameters UI...");
                UpdateParametersUI();
                Console.WriteLine("Updating ROI Display...");
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

            Console.WriteLine("--- OnRoiUpdate finished ---");

        }

        /// <summary>
        /// 核心功能：更新本窗口(ROI工具)的图像显示，使其只显示ROI区域的内容
        /// </summary>
        private void UpdateRoiDisplay()
        {
            if (_sourceImage == null || !_sourceImage.IsInitialized() || _drawingObject == null || _drawingObject.ID == -1)
                return;

            

            HObject iconicObject = _drawingObject.GetDrawingObjectIconic();
            string roiType = _drawingObject.GetDrawingObjectParams("type");

            // 准备一个最终用于裁剪的 Region 对象
            HObject regionForClipping = null;

            try
            {
                if (roiType == "xld")
                {
                    HOperatorSet.GenRegionContourXld(iconicObject, out HObject regionMargin, "margin");
                    // 检查是否成功生成了边缘区域
                    if (regionMargin != null && regionMargin.CountObj() > 0)
                    {
                        // 然后对这个边缘区域求凸包，得到最终用于裁剪的填充区域
                        HOperatorSet.ShapeTrans(regionMargin, out regionForClipping, "convex");

                        // 释放临时的边缘区域对象
                        regionMargin.Dispose();

                        //// ======================= Console START (调试代码) =======================
                        //if (regionForClipping != null && regionForClipping.IsInitialized() && regionForClipping.CountObj() > 0)
                        //{
                        //    if (Owner is MainWindow mainWindow)
                        //    {
                        //        HWindow mainWin = mainWindow.HSmart1.HalconWindow;
                        //        HObject mainImage = mainWindow.HSmart1.Tag as HObject;

                        //        if (mainImage != null && mainImage.IsInitialized())
                        //        {


                        //            // 在这个完整的视图上，重新显示背景图
                        //            mainWin.DispObj(mainImage);

                        //            // 现在，在这个与ROI坐标系完全匹配的视图上绘制红色矩形
                        //            mainWin.SetColor("red");
                        //            mainWin.SetDraw("fill");
                        //            mainWin.DispObj(regionForClipping);
                        //        }
                        //    }
                        //}
                        //// ======================== Console END ========================

                    }
                }

                else if (roiType != "line")
                {
                    regionForClipping = iconicObject.CopyObj(1, -1);
                }
            }

            finally
            {
                // 释放从 GetDrawingObjectIconic() 获取的临时对象
                iconicObject.Dispose();
            }

            // --- 统一的裁剪和显示逻辑 ---
            if (regionForClipping != null && regionForClipping.IsInitialized() && regionForClipping.CountObj() > 0)
            {
                HOperatorSet.ReduceDomain(_sourceImage, regionForClipping, out HObject imageReduced);
                HOperatorSet.CropDomain(imageReduced, out HObject imageCropped);
                HWindow roiWindow = HSmartROI.HalconWindow;
                roiWindow.ClearWindow();
                HOperatorSet.GetImageSize(imageCropped, out HTuple width, out HTuple height);
                if (height > 0 && width > 0)
                {
                    roiWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                }
                roiWindow.DispObj(imageCropped);

                imageCropped.Dispose();
                imageReduced.Dispose();
            }
            else
            {
                HSmartROI.HalconWindow.ClearWindow();
            }

            regionForClipping?.Dispose();

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

            else if (type == "xld")
            {
                // 获取所有顶点的坐标
                HObject contour = _drawingObject.GetDrawingObjectIconic();
                HOperatorSet.GetContourXld(contour, out HTuple rows, out HTuple cols);
                contour.Dispose();

                var listView = new ListView { MaxHeight = 150 };
                var gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn { Header = "#", DisplayMemberBinding = new System.Windows.Data.Binding("Index"), Width = 30 });
                gridView.Columns.Add(new GridViewColumn { Header = "行坐标 (Row)", DisplayMemberBinding = new System.Windows.Data.Binding("Row"), Width = 120 });
                gridView.Columns.Add(new GridViewColumn { Header = "列坐标 (Col)", DisplayMemberBinding = new System.Windows.Data.Binding("Col"), Width = 120 });
                listView.View = gridView;

                for (int i = 0; i < rows.Length; i++)
                {
                    listView.Items.Add(new
                    {
                        Index = i + 1,
                        Row = rows[i].D.ToString("F2"),
                        Col = cols[i].D.ToString("F2")
                    });
                }

                ParametersPanel.Children.Add(new Label { Content = "顶点列表:" });
                ParametersPanel.Children.Add(listView);
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
                HObject iconicObject = _drawingObject.GetDrawingObjectIconic();
                string roiType = _drawingObject.GetDrawingObjectParams("type");


                if (roiType == "line")
                {
                    UpdateStatus("错误: 无法保存直线ROI的图像。");
                    iconicObject.Dispose();
                    return;
                }

                // 如果是xld，则转换为region
                if (roiType == "xld")
                {
                    HOperatorSet.TestClosedXld(iconicObject, out HTuple isClosed);
                    if (isClosed.I != 1)
                    {
                        UpdateStatus("错误: 轮廓未闭合，无法保存。");
                        iconicObject.Dispose();
                        return;
                    }
                    HOperatorSet.GenRegionContourXld(iconicObject, out HObject regionFromContour, "filled");
                    iconicObject.Dispose();
                    iconicObject = regionFromContour;
                }

                // --- 从这里开始，iconicObject 保证是一个有效的 Region ---

                if (iconicObject.CountObj() == 0)
                {
                    UpdateStatus("错误：无法从此ROI生成有效区域。");
                    iconicObject.Dispose();
                    return;
                }

                HOperatorSet.ReduceDomain(_sourceImage, iconicObject, out HObject imageReduced);
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
                iconicObject.Dispose(); // 只需要释放这一个
                imageReduced.Dispose();
                imageToSave.Dispose();

                UpdateStatus($"图像已成功保存到：{fullPath}");
            }
            catch (Exception ex)
            {
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


        //private void DetachAndDisposeDrawingObject()
        //{
        //    if (_drawingObject != null && _drawingObject.ID != -1)
        //    {
        //        if (Owner is MainWindow mainWindow)
        //        {
        //            try { mainWindow.HSmart1.HalconWindow.DetachDrawingObjectFromWindow(_drawingObject); } catch (HalconException) { }
        //        }
        //        _drawingObject.Dispose();
        //        _drawingObject = null;
        //    }
        //}

        /// <summary>
        /// 辅助方法：安全地从主窗口分离并释放当前的绘图对象
        /// </summary>
        private void DetachAndDisposeDrawingObject()
        {
            if (Owner is MainWindow mainWindow)
            {
                HWindow window = mainWindow.HSmart1.HalconWindow;
                // 分离背景图
                try { HOperatorSet.DetachBackgroundFromWindow(window); } catch (HalconException) { }

                // 分离绘图对象
                if (_drawingObject != null && _drawingObject.ID != -1)
                {
                    try { window.DetachDrawingObjectFromWindow(_drawingObject); } catch (HalconException) { }
                    _drawingObject.Dispose();
                    _drawingObject = null;
                }

                // 分离后，重新显示原始图像，确保窗口回到干净状态
                HObject mainImage = mainWindow.HSmart1.Tag as HObject;
                if (mainImage != null && mainImage.IsInitialized())
                {
                    window.DispObj(mainImage);
                }
            }
            else if (_drawingObject != null)
            {
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