// ROIToolWindow.xaml.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit;
using System.Windows.Input;
using System.Linq;

namespace VisionLite
{
    /// <summary>
    /// 定义一个委托，用于在用户确认ROI选择后，将最终的ROI对象传递出去。
    /// </summary>
    /// <param name="roi">用户最终确认的ROI几何对象。</param>
    public delegate void ROIAcceptedEventHandler(HObject roi);
    /// <summary>
    /// ROI编辑工具窗口的后台逻辑。
    /// 负责创建、管理交互式的Halcon ROI (HDrawingObject)，
    /// 并通过事件与主窗口通信，实时更新ROI参数。
    /// </summary>
    public partial class ROIToolWindow : Window
    {
        
        /// <summary>
        /// 当ROI的参数（位置、大小等）发生变化时触发的事件。
        /// </summary>
        public event EventHandler<RoiUpdatedEventArgs> RoiUpdated;
        /// <summary>
        /// 当用户点击“确定”按钮，确认最终选择的ROI时触发的事件。
        /// </summary>
        public event ROIAcceptedEventHandler ROIAccepted;

        // --- 私有成员 ---
        private HObject _sourceImage;       // 从主窗口接收的原始图像
        private HDrawingObject _drawingObject; // 当前活动的ROI绘图对象
        public HObject CreatedROI { get; private set; } // 创建的最终ROI区域
        private bool _isUpdatingFromRoi = false; // 标志位，防止UI更新触发ROI更新的死循环
        
        // --- 用于涂抹式ROI的成员 ---
        private HObject _paintedRoi; // 用于存储涂抹生成的ROI区域
        private bool _isPaintingMode = false; // 标记是否处于涂抹模式
        private bool _isPaintingActive = false; // 标记鼠标左键是否按下以激活涂抹
        private HTuple _lastPaintRow = -1;// 用于记录上一次鼠标的行坐标，以实现连续绘制
        private HTuple _lastPaintCol = -1;// 用于记录上一次鼠标的列坐标
        private string _brushShape = "圆形"; // 当前画笔形状, 默认圆形
        private int _brushRadius = 20;       // 圆形画笔的半径
        private int _brushRectWidth = 40;    // 矩形画笔的宽度
        private int _brushRectHeight = 20;   // 矩形画笔的高度

        /// <summary>
        /// 对主窗口的引用，用于访问相机列表等公共资源。
        /// </summary>
        private readonly MainWindow m_pMainWindow;
        /// <summary>
        /// ROI将要绘制在其上的主窗口显示控件。
        /// </summary>
        private HSmartWindowControlWPF _targetDisplayWindow;
        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="targetWindow">ROI将要绘制在其上的主窗口显示控件。</param>
        /// <param name="mainWindow">对主窗口的引用，用于访问相机列表等公共资源。</param>
        public ROIToolWindow(HSmartWindowControlWPF targetWindow, MainWindow mainWindow)
        {
            InitializeComponent();

            // 保存对主窗口的引用 
            m_pMainWindow = mainWindow;
            // 存储对目标窗口的引用
            _targetDisplayWindow = targetWindow;

            this.Owner = mainWindow;

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
            if (RoiTypeComboBox.SelectedItem == null || !this.IsLoaded) return;
           
            if (_sourceImage == null || !_sourceImage.IsInitialized())
            {
                System.Windows.MessageBox.Show("ROI工具窗口中没有有效的背景图像。请在主窗口重新采集或加载图像。", "错误");
                // 重置下拉框，防止用户再次点击
                RoiTypeComboBox.SelectedIndex = -1;
                return;
            }

            // 获取图像尺寸，用于计算初始ROI的位置和大小
            HOperatorSet.GetImageSize(_sourceImage, out HTuple width, out HTuple height);

            var selectedItem = (RoiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // 检查是否从涂抹模式切换到了其他模式
            if (_isPaintingMode && selectedItem != "涂抹式ROI (Paint)")
            {
                // 如果正在涂抹模式，则先结束涂抹
                FinalizePainting();
            }

            // 如果之前是标准ROI模式，清理DrawingObject
            DetachAndDisposeDrawingObject();
            // 如果之前是涂抹模式，清理涂抹模式资源
            DisablePaintMode();

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

                    case "涂抹式ROI (Paint)":
                        EnablePaintMode(); // 启用涂抹模式
                        return; // 直接返回，不执行后续的DrawingObject逻辑

                    default:
                        return; // 如果没有匹配项，则退出
                }

                // 将新创建的绘图对象附加到目标窗口上 (不再是HSmart1)
                if (_targetDisplayWindow != null)
                {
                    HWindow window = _targetDisplayWindow.HalconWindow;

                    // 使用与 RefreshTargetWindowDisplay 相同的健壮逻辑来获取背景图 
                    HObject mainImage = GetTargetWindowImage();
                    if (mainImage != null && mainImage.IsInitialized())
                    {
                       
                        window.DispObj(mainImage);
                    }
                    else
                    {
                        window.ClearWindow();
                    }
                    // 订阅所有交互事件
                    _drawingObject.OnDrag(OnRoiUpdate);
                    _drawingObject.OnResize(OnRoiUpdate);
                    _drawingObject.OnSelect(OnRoiUpdate);

                    
                    _drawingObject.OnAttach((dobj, hwin, type) =>
                    {
                        OnRoiUpdate(dobj, hwin, type);
                    });

                    // 最后再将绘图对象附加到窗口，这会触发 on_attach 事件
                    window.AttachDrawingObjectToWindow(_drawingObject);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"创建ROI时发生错误: {ex.Message}");
            }
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

                // --- 触发 RoiUpdated 事件 ---
                NotifyRoiUpdate();

                // --- 启用按钮并更新Tooltip ---
                if (_drawingObject != null && _drawingObject.ID != -1)
                {
                    SaveROIImageButton.IsEnabled = true;
                    string savePath = Path.Combine("D:\\", "VisionLite图像保存");
                    //string fileName = $"ROI_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                    SaveROIImageButton.ToolTip = $"保存ROI图像到: \n{savePath}";
                }
                _isUpdatingFromRoi = false; // 恢复标志位
            });

        }


        /// <summary>
        /// 核心方法：收集当前ROI的数值参数和几何轮廓，
        /// 然后触发RoiUpdated事件，将这些信息广播出去。
        /// </summary>
        private void NotifyRoiUpdate()
        {
            if (_drawingObject == null || _drawingObject.ID == -1)
            {
                // 如果ROI不存在，可以触发一个带有空参数的事件来清空显示
                RoiUpdated?.Invoke(this, new RoiUpdatedEventArgs(new Dictionary<string, double>(), null));
                return;
            }

            var parameters = new Dictionary<string, double>();
            string roiType = _drawingObject.GetDrawingObjectParams("type");

            // 根据不同的ROI类型，收集其参数
            if (roiType == "rectangle1")
            {
                parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
            }
            else if (roiType == "rectangle2")
            {
                parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")) * 180 / Math.PI; // 转换为度
                parameters["length1"] = _drawingObject.GetDrawingObjectParams("length1");
                parameters["length2"] = _drawingObject.GetDrawingObjectParams("length2");
            }
            else if (roiType == "circle")
            {
                parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                parameters["radius"] = _drawingObject.GetDrawingObjectParams("radius");
            }
            
            else if (roiType == "ellipse")
            {
                parameters["row"] = _drawingObject.GetDrawingObjectParams("row");
                parameters["column"] = _drawingObject.GetDrawingObjectParams("column");
                parameters["phi"] = (_drawingObject.GetDrawingObjectParams("phi")) * 180 / Math.PI; // 转换为度
                parameters["radius1"] = _drawingObject.GetDrawingObjectParams("radius1"); // 半长轴
                parameters["radius2"] = _drawingObject.GetDrawingObjectParams("radius2"); // 半短轴
            }
            
            else if (roiType == "line")
            {
                parameters["row1"] = _drawingObject.GetDrawingObjectParams("row1");
                parameters["column1"] = _drawingObject.GetDrawingObjectParams("column1");
                parameters["row2"] = _drawingObject.GetDrawingObjectParams("row2");
                parameters["column2"] = _drawingObject.GetDrawingObjectParams("column2");
            }

            // --- 获取ROI的轮廓 (Contour) ---
            HObject contour = null;
            try
            {
                // GetDrawingObjectIconic() 返回的是一个可以代表ROI几何形状的对象
                // 对于 Rectangle2, 它是一个XLD Contour
                using (HObject iconic = _drawingObject.GetDrawingObjectIconic())
                {
                    // 复制一份，因为iconic需要被释放
                    contour = iconic.CopyObj(1, -1);
                }
            }
            catch (HalconException)
            {
                contour?.Dispose();
                contour = null;
            }

            // --- 将轮廓对象和参数一起传递出去 ---
            RoiUpdated?.Invoke(this, new RoiUpdatedEventArgs(parameters, contour));
        }

        /// <summary>
        /// 核心功能：更新本窗口(ROI工具)的图像显示，使其只显示ROI区域的内容
        /// </summary>
        private void UpdateRoiDisplay()
        {
            if (_sourceImage == null || !_sourceImage.IsInitialized() ) return;
            // 准备一个最终用于裁剪的 Region 对象
            HObject regionForClipping = null;

            // 标志位，用于控制后续是否需要释放regionForClipping
            bool isTempRegion = false;

            // 优先使用涂抹模式的ROI（无论是在涂抹中还是涂抹完成）
            if (_isPaintingMode && _paintedRoi != null && _paintedRoi.IsInitialized() && _paintedRoi.CountObj() > 0)
            {
                regionForClipping = _paintedRoi;
                isTempRegion = false; // 这是成员变量，不由本方法释放
            }
            // 模式二：标准ROI模式，从 _drawingObject 临时创建
            else if (!_isPaintingMode && _drawingObject != null && _drawingObject.ID != -1)
            {
                using (HObject iconicObject = _drawingObject.GetDrawingObjectIconic())
                {
                    string roiType = _drawingObject.GetDrawingObjectParams("type");
                    if (roiType == "xld")
                    {
                        HOperatorSet.GenRegionContourXld(iconicObject, out regionForClipping, "filled");
                    }
                    else if (roiType != "line")
                    {
                        // 标准形状的iconicObject本身就是Region，直接复制即可
                        regionForClipping = iconicObject.CopyObj(1, -1);
                    }
                }
                isTempRegion = true; // 这是临时创建的，需要释放
            }
            // --- 统一的裁剪和显示逻辑 ---
            if (regionForClipping != null && regionForClipping.IsInitialized() && regionForClipping.CountObj() > 0)
            {
                HObject intersection, imageReduced, imageCropped;
                HOperatorSet.Intersection(_sourceImage, regionForClipping, out intersection);

                using (intersection)
                {
                    HOperatorSet.AreaCenter(intersection, out HTuple area, out _, out _);
                    if (area.D > 0)
                    {
                        HOperatorSet.ReduceDomain(_sourceImage, intersection, out imageReduced);
                        using (imageReduced)
                        {
                            HOperatorSet.CropDomain(imageReduced, out imageCropped);
                            using (imageCropped)
                            {
                                HWindow roiWindow = HSmartROI.HalconWindow;
                                roiWindow.ClearWindow();
                                HOperatorSet.GetImageSize(imageCropped, out HTuple width, out HTuple height);
                                if (height.I > 0 && width.I > 0)
                                {
                                    roiWindow.SetPart(0, 0, height.I - 1, width.I - 1);
                                }
                                roiWindow.DispObj(imageCropped);
                            }
                        }
                        
                    }
                    else
                    {
                        HSmartROI.HalconWindow.ClearWindow();
                    }
                }
            }
            else
            {
                HSmartROI.HalconWindow.ClearWindow();
            }
            if (isTempRegion)
            {
                regionForClipping?.Dispose();
            }
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
                using (HObject contour = _drawingObject.GetDrawingObjectIconic())
                {
                    HOperatorSet.GetContourXld(contour, out HTuple rows, out HTuple cols);
                    var listView = new ListView { MaxHeight = 150 };
                    var gridView = new GridView();
                    gridView.Columns.Add(new GridViewColumn { Header = "#", DisplayMemberBinding = new System.Windows.Data.Binding("Index"), Width = 30 });
                    gridView.Columns.Add(new GridViewColumn { Header = "行坐标 (Row)", DisplayMemberBinding = new System.Windows.Data.Binding("Row"), Width = 120 });
                    gridView.Columns.Add(new GridViewColumn { Header = "列坐标 (Col)", DisplayMemberBinding = new System.Windows.Data.Binding("Col"), Width = 120 });
                    listView.View = gridView;
                    for (int i = 0; i < rows.Length; i++)
                    {
                        listView.Items.Add(new { Index = i + 1, Row = rows[i].D.ToString("F2"), Col = cols[i].D.ToString("F2") });
                    }
                    ParametersPanel.Children.Add(new Label { Content = "顶点列表:" });
                    ParametersPanel.Children.Add(listView);
                }
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

        #region --- 涂抹式ROI (Paint ROI) 核心逻辑 ---

        /// <summary>
        /// 启用涂抹绘制模式
        /// </summary>
        private void EnablePaintMode()
        {
            if (_isPaintingMode) return;
            _isPaintingMode = true;

            // 1. 初始化一个空的ROI区域
            _paintedRoi?.Dispose();
            HOperatorSet.GenEmptyRegion(out _paintedRoi);

            // 2. 在主窗口上注册鼠标事件
            if (_targetDisplayWindow != null)
            {
                _targetDisplayWindow.HMouseDown += PaintRoi_HMouseDown;
                _targetDisplayWindow.HMouseMove += PaintRoi_HMouseMove;

                RefreshTargetWindowDisplay();
            }

            // 3. 创建并显示画笔参数UI
            ParametersPanel.IsEnabled = true;
            CreatePaintToolUI();
            UpdateStatus("涂抹模式已激活：左键涂抹，右键结束。");
        }

        /// <summary>
        /// 禁用并清理涂抹绘制模式
        /// </summary>
        private void DisablePaintMode()
        {
            if (!_isPaintingMode) return;
            _isPaintingMode = false;
            // 在此处重置“涂抹激活”状态
            _isPaintingActive = false;

            // 在主窗口上注销鼠标事件
            if (_targetDisplayWindow != null)
            {
                _targetDisplayWindow.HMouseDown -= PaintRoi_HMouseDown; 
                _targetDisplayWindow.HMouseMove -= PaintRoi_HMouseMove; 
            }

            _paintedRoi?.Dispose();
            _paintedRoi = null;
            ParametersPanel.Children.Clear();
            RefreshTargetWindowDisplay();
        }

        /// <summary>
        /// 创建涂抹工具的参数设置UI
        /// </summary>
        private void CreatePaintToolUI()
        {
            ParametersPanel.Children.Clear();
            var shapePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            shapePanel.Children.Add(new Label { Content = "画笔形状:", Width = 120, VerticalAlignment = VerticalAlignment.Center });
            var radioCircle = new RadioButton { Content = "圆形", IsChecked = _brushShape == "圆形", VerticalAlignment = VerticalAlignment.Center };
            var radioRect = new RadioButton { Content = "矩形", IsChecked = _brushShape == "矩形", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            shapePanel.Children.Add(radioCircle);
            shapePanel.Children.Add(radioRect);
            ParametersPanel.Children.Add(shapePanel);
            var sizePanel = new StackPanel { Margin = new Thickness(0, 5, 0, 2) };
            ParametersPanel.Children.Add(sizePanel);
            Action updateSizeControls = () =>
            {
                sizePanel.Children.Clear();
                if (radioCircle.IsChecked == true)
                {
                    _brushShape = "圆形";
                    var ud = new IntegerUpDown { Value = _brushRadius, Minimum = 1, Maximum = 500, Increment = 1, MinWidth = 120 };
                    ud.ValueChanged += (s, a) => _brushRadius = ud.Value ?? _brushRadius;
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new Label { Content = "画笔半径:", Width = 120 });
                    sp.Children.Add(ud);
                    sizePanel.Children.Add(sp);
                }
                else
                {
                    _brushShape = "矩形";
                    var udWidth = new IntegerUpDown { Value = _brushRectWidth, Minimum = 1, Maximum = 1000, Increment = 1, MinWidth = 120 };
                    udWidth.ValueChanged += (s, a) => _brushRectWidth = udWidth.Value ?? _brushRectWidth;
                    var spWidth = new StackPanel { Orientation = Orientation.Horizontal };
                    spWidth.Children.Add(new Label { Content = "画笔宽度:", Width = 120 });
                    spWidth.Children.Add(udWidth);
                    sizePanel.Children.Add(spWidth);
                    var udHeight = new IntegerUpDown { Value = _brushRectHeight, Minimum = 1, Maximum = 1000, Increment = 1, MinWidth = 120 };
                    udHeight.ValueChanged += (s, a) => _brushRectHeight = udHeight.Value ?? _brushRectHeight;
                    var spHeight = new StackPanel { Orientation = Orientation.Horizontal };
                    spHeight.Children.Add(new Label { Content = "画笔高度:", Width = 120 });
                    spHeight.Children.Add(udHeight);
                    sizePanel.Children.Add(spHeight);
                }
            };
            radioCircle.Checked += (s, e) => updateSizeControls();
            radioRect.Checked += (s, e) => updateSizeControls();
            updateSizeControls();
        }

        private void PaintRoi_HMouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (e.Button == MouseButton.Right)
            {
                // 右键结束绘制
                FinalizePainting();
                return;
            }

            if (e.Button == MouseButton.Left)
            {
                // 左键单击行为：切换涂抹激活状态
                _isPaintingActive = !_isPaintingActive;
                if (_isPaintingActive)
                {
                    // 当开始或重新开始绘制时，记录下第一个点的位置
                    _lastPaintRow = e.Row;
                    _lastPaintCol = e.Column;
                    // 如果是刚刚激活涂抹，则在当前点画下第一笔
                    PaintAtCurrentPosition(e.Row, e.Column);
                    UpdateStatus("涂抹已激活。移动鼠标进行绘制，再次左键单击暂停，右键结束。");
                }
                else
                {
                    // 当暂停时，重置上一个点的位置，这样下次开始时不会画出一条多余的线
                    _lastPaintRow = -1;
                    _lastPaintCol = -1;
                    UpdateStatus("涂抹已暂停。可再次左键单击继续，或右键结束。");
                }
            }
        }

        private void PaintRoi_HMouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (_isPaintingActive)
            {
                // 从上一个点绘制到当前点
                PaintAtCurrentPosition(e.Row, e.Column);

                // 处理完后，更新“上一个点”为当前点
                _lastPaintRow = e.Row;
                _lastPaintCol = e.Column;
            }
        }

        private void PaintAtCurrentPosition(double row, double col)
        {
            if (_targetDisplayWindow == null) return;
            HWindow window = _targetDisplayWindow.HalconWindow;
            try
            {
                HObject brush;

                if (_lastPaintRow.D != -1 && _lastPaintCol.D != -1)
                {
                    // 声明一个 HObject 变量 lineRegion，但不要初始化它。
                    HObject lineRegion;
                    // 将 lineRegion 作为 out 参数传递给 GenRegionLine。
                    HOperatorSet.GenRegionLine(out lineRegion, _lastPaintRow, _lastPaintCol, row, col);

                    // 使用 using 块确保 lineRegion 在使用后被正确释放。
                    using (lineRegion)
                    {
                        if (_brushShape == "圆形")
                        {
                            HOperatorSet.DilationCircle(lineRegion, out brush, _brushRadius);
                        }
                        else
                        {
                            HOperatorSet.DilationRectangle1(lineRegion, out brush, _brushRectWidth, _brushRectHeight);
                        }
                    }
                }
                else
                {
                    if (_brushShape == "圆形")
                    {
                        HOperatorSet.GenCircle(out brush, row, col, _brushRadius);
                    }
                    else
                    {
                        HOperatorSet.GenRectangle1(out brush, row - _brushRectHeight / 2.0, col - _brushRectWidth / 2.0, row + _brushRectHeight / 2.0, col + _brushRectWidth / 2.0);
                    }
                }

                HObject newRoi;
                HOperatorSet.Union2(_paintedRoi, brush, out newRoi);
                _paintedRoi.Dispose();
                _paintedRoi = newRoi;

                brush.Dispose();

                window.SetDraw("fill");
                window.SetColor("green");
                window.DispObj(_paintedRoi);

                UpdateRoiDisplay();
                SaveROIImageButton.IsEnabled = true;
            }
            catch (HalconException) { /* 忽略小错误 */ }
        }

        private void FinalizePainting()
        {
            // 注销鼠标事件，停止绘制过程
            if (_targetDisplayWindow != null)
            {
                _targetDisplayWindow.HMouseDown -= PaintRoi_HMouseDown;
                _targetDisplayWindow.HMouseMove -= PaintRoi_HMouseMove;
            }
            // 在此处重置“涂抹激活”状态
            _isPaintingActive = false;

            // 在此处也重置上一个点的位置
            _lastPaintRow = -1;
            _lastPaintCol = -1;

            // 更新UI状态
            UpdateStatus("涂抹绘制已完成。点击“确定”以确认ROI。");
            // 禁用参数面板，防止用户在完成后修改画笔
            ParametersPanel.IsEnabled = false;
            // 刷新主窗口的显示，清除上面残留的绿色涂抹痕迹
            RefreshTargetWindowDisplay();
            // 在主窗口上把最终的ROI轮廓画出来，给用户清晰的反馈
            if (_targetDisplayWindow != null && _paintedRoi != null && _paintedRoi.IsInitialized())
            {
                _targetDisplayWindow.HalconWindow.SetColor("green");
                _targetDisplayWindow.HalconWindow.SetDraw("margin");
                _targetDisplayWindow.HalconWindow.DispObj(_paintedRoi);
            }
            // 最后更新一次本窗口的ROI预览
            UpdateRoiDisplay();
        }

        #endregion

        /// <summary>
        /// “保存图像”按钮的点击事件处理程序。
        /// </summary>
        private void SaveROIImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 再次确认ROI是否有效
            if (_sourceImage == null || !_sourceImage.IsInitialized())
            {
                UpdateStatus("错误: 没有有效的ROI可以保存。");
                return;
            }
            // 声明一个临时的Region对象，用于统一处理两种模式下的ROI
            HObject regionToSave = null;
            bool isTempRegion = false; // 标志位，用于判断regionToSave是否需要释放
            // 根据当前模式，获取有效的ROI区域
            // 情况一：涂抹模式，直接使用_paintedRoi
            if (_isPaintingMode)
            {
                if (_paintedRoi != null && _paintedRoi.IsInitialized() && _paintedRoi.CountObj() > 0)
                {
                    regionToSave = _paintedRoi;
                    isTempRegion = false; // _paintedRoi是成员变量，不由本方法释放
                }
            }
            
            // 情况二：标准ROI模式，从_drawingObject创建临时Region
            else
            {
                if (_drawingObject != null && _drawingObject.ID != -1)
                {
                    using (HObject iconicObject = _drawingObject.GetDrawingObjectIconic())
                    {
                        string roiType = _drawingObject.GetDrawingObjectParams("type");
                        if (roiType == "line")
                        {
                            UpdateStatus("错误: 无法保存直线ROI的图像。");
                            return;
                        }
                        if (roiType == "xld")
                        {
                            HOperatorSet.TestClosedXld(iconicObject, out HTuple isClosed);
                            if (isClosed.I != 1)
                            {
                                UpdateStatus("错误: 轮廓未闭合，无法保存。");
                                return;
                            }
                            HOperatorSet.GenRegionContourXld(iconicObject, out regionToSave, "filled");
                        }
                        else
                        {
                            regionToSave = iconicObject.CopyObj(1, -1);
                        }
                    }
                    isTempRegion = true; // 标记这是为保存操作临时创建的对象
                }
            }

            // 检查是否成功获取了有效的ROI
            if (regionToSave == null || !regionToSave.IsInitialized() || regionToSave.CountObj() == 0)
            {
                UpdateStatus("错误: 没有有效的ROI可以保存。");
                return;
            }

            // 统一执行保存逻辑
            try
            {
                HObject imageReduced, imageToSave;
                HOperatorSet.ReduceDomain(_sourceImage, regionToSave, out imageReduced);
                using (imageReduced)
                {
                    HOperatorSet.CropDomain(imageReduced, out imageToSave);
                    using (imageToSave)
                    {
                        string savePath = Path.Combine("D:\\", "VisionLite图像保存");
                        Directory.CreateDirectory(savePath);
                        string fileName = $"ROI_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
                        string fullPath = Path.Combine(savePath, fileName);
                        HOperatorSet.WriteImage(imageToSave, "bmp", 0, fullPath);
                        UpdateStatus($"图像已成功保存到：{fullPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}");
            }
            finally
            {
                // 如果创建了临时Region，则安全地释放它
                if (isTempRegion)
                {
                    regionToSave?.Dispose();
                }
            }
        }
        /// <summary>
        /// “确定”按钮的点击事件处理程序。
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 临时的HObject，用于统一返回
            HObject roiToReturn = null;

            if (_isPaintingMode)
            {
                if (_paintedRoi == null || !_paintedRoi.IsInitialized() || _paintedRoi.CountObj() == 0)
                {
                    System.Windows.MessageBox.Show("您还没有绘制有效的ROI！", "提示");
                    return;
                }
                roiToReturn = _paintedRoi.CopyObj(1, -1);
            }
            else
            {
                if (_drawingObject == null || _drawingObject.ID == -1)
                {
                    System.Windows.MessageBox.Show("您还没有创建有效的ROI！", "提示");
                    return;
                }
                roiToReturn = _drawingObject.GetDrawingObjectIconic();
            }

            ROIAccepted?.Invoke(roiToReturn);
            this.Close();
        }
        /// <summary>
        /// “取消”按钮的点击事件处理程序。
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 辅助方法：刷新主窗口的显示，确保它显示干净的背景图
        /// </summary>
        private void RefreshTargetWindowDisplay()
        {
            if (_targetDisplayWindow != null)
            {
                HWindow window = _targetDisplayWindow.HalconWindow;
                HObject mainImage = GetTargetWindowImage();
                if (mainImage != null && mainImage.IsInitialized())
                {
                    window.DispObj(mainImage);
                }
                else
                {
                    window.ClearWindow();
                }
            }
        }

        /// <summary>
        /// 健壮的辅助方法，用于获取目标窗口当前应该显示的图像。
        /// 它会智能地区分图像是来自相机还是本地文件。
        /// </summary>
        private HObject GetTargetWindowImage()
        {
            if (_targetDisplayWindow == null || m_pMainWindow == null)
            {
                return null;
            }

            // 逻辑1: 检查是否有相机连接到此窗口
            var camera = m_pMainWindow.openCameras.Values.FirstOrDefault(c => c.DisplayWindow == _targetDisplayWindow);
            if (camera != null)
            {
                return camera.GetCurrentImage(); // 从相机获取最新图像
            }

            // 逻辑2: 如果没有相机，检查Tag是否为本地图像
            if (_targetDisplayWindow.Tag is HObject localImage)
            {
                return localImage;
            }

            // 如果两种情况都不是，则返回null
            return null;
        }
        /// <summary>
        /// 窗口关闭时触发，用于安全地清理所有资源。
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 触发一个空事件，以便主窗口可以清空显示
            RoiUpdated?.Invoke(this, new RoiUpdatedEventArgs(new Dictionary<string, double>(), null));

            DetachAndDisposeDrawingObject();
            DisablePaintMode(); 
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
            if (_targetDisplayWindow != null)
            {
                HWindow window = _targetDisplayWindow.HalconWindow;
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
                HObject mainImage = _targetDisplayWindow.Tag as HObject;
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
            // 等待7秒
            await System.Threading.Tasks.Task.Delay(7000);
            // 如果5秒后状态栏文本还是这个消息，就把它清除
            if (StatusTextBlock.Text == message)
            {
                StatusTextBlock.Text = "准备就绪";
            }
        }
    }
}