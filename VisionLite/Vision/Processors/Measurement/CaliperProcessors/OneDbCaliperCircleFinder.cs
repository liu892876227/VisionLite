using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Enums;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Measurement.CaliperProcessors
{
    /// <summary>
    /// 一维卡尺圆查找处理器
    /// 使用多个一维卡尺在圆周上进行边缘检测，然后拟合圆形
    /// </summary>
    public class OneDbCaliperCircleFinder : VisionProcessorBase
    {
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "一维卡尺圆查找";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "卡尺工具";
        
        #region 算法参数

        /// <summary>
        /// 圆心行坐标
        /// </summary>
        [Parameter("圆心Row", "圆心行坐标", Order = 1, Group = "基础参数")]
        public double CenterRow { get; set; } = 300.0;

        /// <summary>
        /// 圆心列坐标
        /// </summary>
        [Parameter("圆心Col", "圆心列坐标", Order = 2, Group = "基础参数")]
        public double CenterCol { get; set; } = 300.0;

        /// <summary>
        /// 预期半径
        /// </summary>
        [Parameter("预期半径", "圆的预期半径", Order = 3, Group = "基础参数", MinValue = 10, MaxValue = 1000)]
        public double ExpectedRadius { get; set; } = 100.0;

        /// <summary>
        /// 卡尺数量
        /// </summary>
        [Parameter("卡尺数量", "圆周上的卡尺数量", Order = 4, Group = "卡尺参数", MinValue = 3, MaxValue = 100)]
        public int CaliperCount { get; set; } = 10;

        /// <summary>
        /// 卡尺长度
        /// </summary>
        [Parameter("卡尺长度", "每个卡尺的长度", Order = 5, Group = "卡尺参数", MinValue = 5, MaxValue = 200)]
        public double CaliperLength { get; set; } = 20.0;

        /// <summary>
        /// 卡尺宽度
        /// </summary>
        [Parameter("卡尺宽度", "每个卡尺的宽度", Order = 6, Group = "卡尺参数", MinValue = 1, MaxValue = 50)]
        public double CaliperWidth { get; set; } = 5.0;

        /// <summary>
        /// 边缘阈值
        /// </summary>
        [Parameter("边缘阈值", "边缘检测阈值", Order = 7, Group = "检测参数", MinValue = 1, MaxValue = 255)]
        public double EdgeThreshold { get; set; } = 30.0;

        /// <summary>
        /// 边缘极性
        /// </summary>
        [Parameter("边缘极性", "边缘检测极性", Order = 8, Group = "检测参数")]
        public EdgeTransition EdgeTransition { get; set; } = EdgeTransition.All;

        /// <summary>
        /// 平滑参数
        /// </summary>
        [Parameter("平滑参数", "高斯平滑sigma值", Order = 9, Group = "检测参数", MinValue = 0.5, MaxValue = 10)]
        public double Sigma { get; set; } = 1.0;

        /// <summary>
        /// 边缘选择
        /// </summary>
        [Parameter("边缘选择", "边缘选择策略", Order = 10, Group = "检测参数")]
        public EdgeSelection EdgeSelection { get; set; } = EdgeSelection.First;

        /// <summary>
        /// 最小拟合点数
        /// </summary>
        [Parameter("最小拟合点数", "圆拟合所需的最小边缘点数", Order = 11, Group = "高级参数", MinValue = 3, MaxValue = 50, IsAdvanced = true)]
        public int MinFitPoints { get; set; } = 3;

        /// <summary>
        /// 显示卡尺
        /// </summary>
        [Parameter("显示卡尺", "是否显示卡尺矩形", Order = 12, Group = "显示选项")]
        public bool ShowCalipers { get; set; } = true;

        /// <summary>
        /// 显示边缘点
        /// </summary>
        [Parameter("显示边缘点", "是否显示检测到的边缘点", Order = 13, Group = "显示选项")]
        public bool ShowEdgePoints { get; set; } = true;

        /// <summary>
        /// 显示拟合圆
        /// </summary>
        [Parameter("显示拟合圆", "是否显示拟合的圆", Order = 14, Group = "显示选项")]
        public bool ShowFittedCircle { get; set; } = true;

        #endregion

        #region 构造函数

        public OneDbCaliperCircleFinder()
        {
        }

        #endregion

        #region 主处理方法

        /// <summary>
        /// 执行一维卡尺圆查找算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!ValidateParameters())
                {
                    return CreateFailureResult("参数验证失败，请检查参数设置");
                }

                var result = await Task.Run(() => ExecuteCircleDetection(inputImage));
                
                stopwatch.Stop();
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateFailureResult($"一维卡尺圆查找失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 参数验证

        /// <summary>
        /// 验证输入参数
        /// </summary>
        /// <returns>验证是否通过</returns>
        protected override bool ValidateParameters()
        {
            if (ExpectedRadius <= 0)
            {
                System.Diagnostics.Debug.WriteLine("预期半径必须大于0");
                return false;
            }

            if (CaliperCount < 3)
            {
                System.Diagnostics.Debug.WriteLine("卡尺数量不能少于3个");
                return false;
            }

            if (CaliperLength <= 0 || CaliperWidth <= 0)
            {
                System.Diagnostics.Debug.WriteLine("卡尺长度和宽度必须大于0");
                return false;
            }

            if (EdgeThreshold <= 0)
            {
                System.Diagnostics.Debug.WriteLine("边缘阈值必须大于0");
                return false;
            }

            return true;
        }

        #endregion

        #region 核心算法实现

        /// <summary>
        /// 执行圆检测算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        private ProcessResult ExecuteCircleDetection(VisionImage inputImage)
        {
            try
            {
                // 步骤1：计算卡尺位置
                var calipers = CalculateCaliperPositions(CenterRow, CenterCol, ExpectedRadius);
                
                // 步骤2：检测所有卡尺的边缘
                var allEdgePoints = new List<EdgePoint>();
                
                foreach (var caliper in calipers)
                {
                    var edgePoints = DetectEdgesInCaliper(inputImage, caliper);
                    allEdgePoints.AddRange(edgePoints);
                }

                // 步骤3：拟合圆
                var fitResult = FitCircleFromEdgePoints(allEdgePoints);

                // 步骤4：创建输出图像
                var outputImage = inputImage.Clone();

                // 步骤5：生成可视化轮廓
                var displayContours = CreateVisualizationContours(calipers, allEdgePoints, fitResult);

                // 步骤6：创建测量数据
                var measurements = CreateMeasurements(fitResult, allEdgePoints.Count);

                // 步骤7：创建成功结果并保存轮廓数据
                var result = CreateSuccessResult(outputImage, System.TimeSpan.FromMilliseconds(0), measurements);
                result.AddMetadata("HalconDisplayContours", displayContours);

                return result;
            }
            catch (Exception ex)
            {
                return CreateFailureResult($"圆检测失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 待实现方法（占位符）

        /// <summary>
        /// 计算卡尺位置
        /// 在圆周上均匀分布指定数量的卡尺
        /// </summary>
        /// <param name="centerRow">圆心行坐标</param>
        /// <param name="centerCol">圆心列坐标</param>
        /// <param name="radius">圆半径</param>
        /// <returns>卡尺信息列表</returns>
        private List<CaliperInfo> CalculateCaliperPositions(double centerRow, double centerCol, double radius)
        {
            var calipers = new List<CaliperInfo>();

            try
            {
                // 计算角度步长
                double angleStep = 2.0 * Math.PI / CaliperCount;
                
                for (int i = 0; i < CaliperCount; i++)
                {
                    // 计算当前卡尺的角度
                    double angle = i * angleStep;
                    
                    // 计算卡尺中心位置（在圆周上）
                    double caliperRow = centerRow + radius * Math.Cos(angle);
                    double caliperCol = centerCol + radius * Math.Sin(angle);
                    
                    // 卡尺方向应该垂直于半径方向（指向圆心内外）
                    double caliperPhi = angle + Math.PI / 2.0;
                    
                    // 创建卡尺信息
                    var caliper = new CaliperInfo(
                        caliperRow, 
                        caliperCol, 
                        caliperPhi, 
                        CaliperLength, 
                        CaliperWidth
                    );
                    
                    calipers.Add(caliper);
                    
                    System.Diagnostics.Debug.WriteLine($"卡尺 {i}: 位置({caliperRow:F2}, {caliperCol:F2}), 角度:{caliperPhi * 180 / Math.PI:F1}°");
                }
                
                System.Diagnostics.Debug.WriteLine($"成功计算了 {calipers.Count} 个卡尺位置");
                return calipers;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算卡尺位置失败: {ex.Message}");
                return calipers;
            }
        }

        /// <summary>
        /// 在单个卡尺中检测边缘
        /// 使用Halcon的gen_measure_rectangle2和measure_pos算子
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="caliper">卡尺信息</param>
        /// <returns>检测到的边缘点列表</returns>
        private List<EdgePoint> DetectEdgesInCaliper(VisionImage image, CaliperInfo caliper)
        {
            var edgePoints = new List<EdgePoint>();
            HTuple measureHandle = null;

            try
            {
                // 获取图像尺寸
                HOperatorSet.GetImageSize(image.HImage, out HTuple imageWidth, out HTuple imageHeight);

                // 步骤1：生成测量矩形
                HOperatorSet.GenMeasureRectangle2(
                    caliper.CenterRow,          // 矩形中心行
                    caliper.CenterCol,          // 矩形中心列
                    caliper.Phi,                // 矩形方向角
                    caliper.Length / 2.0,       // 矩形长度的一半
                    caliper.Width / 2.0,        // 矩形宽度的一半
                    imageWidth,                 // 图像宽度
                    imageHeight,                // 图像高度
                    "nearest_neighbor",         // 插值方法
                    out measureHandle);

                // 步骤2：执行边缘测量
                HOperatorSet.MeasurePos(
                    image.HImage,               // 输入图像
                    measureHandle,              // 测量句柄
                    Sigma,                      // 高斯平滑参数
                    EdgeThreshold,              // 边缘阈值
                    GetTransitionString(),      // 边缘极性
                    GetSelectionString(),       // 边缘选择策略
                    out HTuple rowEdge,         // 输出边缘点行坐标
                    out HTuple colEdge,         // 输出边缘点列坐标
                    out HTuple amplitude,       // 输出边缘强度
                    out HTuple distance);       // 输出边缘距离

                // 步骤3：转换为EdgePoint对象
                if (rowEdge.Length > 0)
                {
                    for (int i = 0; i < rowEdge.Length; i++)
                    {
                        var edgePoint = new EdgePoint(
                            rowEdge[i].D,
                            colEdge[i].D,
                            amplitude[i].D
                        );
                        edgePoints.Add(edgePoint);
                    }

                    System.Diagnostics.Debug.WriteLine($"卡尺检测到 {edgePoints.Count} 个边缘点");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("此卡尺未检测到边缘点");
                }

                return edgePoints;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"卡尺边缘检测失败: {ex.Message}");
                return edgePoints;
            }
            finally
            {
                // 清理测量句柄
                if (measureHandle != null)
                {
                    try
                    {
                        HOperatorSet.CloseMeasure(measureHandle);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"关闭测量句柄失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 从边缘点拟合圆
        /// 使用Halcon的fit_circle_contour_xld算子
        /// </summary>
        /// <param name="edgePoints">边缘点列表</param>
        /// <returns>圆拟合结果</returns>
        private CircleFitResult FitCircleFromEdgePoints(List<EdgePoint> edgePoints)
        {
            var result = new CircleFitResult();

            try
            {
                // 检查边缘点数量
                if (edgePoints.Count < MinFitPoints)
                {
                    result.ErrorMessage = $"边缘点数量不足，需要至少 {MinFitPoints} 个点，实际只有 {edgePoints.Count} 个";
                    System.Diagnostics.Debug.WriteLine(result.ErrorMessage);
                    return result;
                }

                System.Diagnostics.Debug.WriteLine($"开始用 {edgePoints.Count} 个边缘点拟合圆");

                // 步骤1：将边缘点转换为HTuple数组
                var rowArray = new double[edgePoints.Count];
                var colArray = new double[edgePoints.Count];

                for (int i = 0; i < edgePoints.Count; i++)
                {
                    rowArray[i] = edgePoints[i].Row;
                    colArray[i] = edgePoints[i].Col;
                }

                var rowTuple = new HTuple(rowArray);
                var colTuple = new HTuple(colArray);

                // 步骤2：生成轮廓
                HObject contour = null;
                HOperatorSet.GenContourPolygonXld(out contour, rowTuple, colTuple);

                // 步骤3：拟合圆
                HOperatorSet.FitCircleContourXld(
                    contour,                    // 输入轮廓
                    "algebraic",                // 拟合算法（"algebraic" 或 "geometric"）
                    -1,                         // 最大迭代次数（-1表示自动）
                    0,                          // 随机采样点数（0表示使用所有点）
                    0,                          // 随机种子
                    3,                          // 最小点数
                    2,                          // 最大异常点比例
                    out HTuple centerRow,       // 拟合圆心行坐标
                    out HTuple centerCol,       // 拟合圆心列坐标
                    out HTuple radius,          // 拟合半径
                    out HTuple startPhi,        // 起始角度
                    out HTuple endPhi,          // 结束角度
                    out HTuple pointOrder);     // 点的顺序

                // 步骤4：计算拟合误差
                double fitError = CalculateFitError(edgePoints, centerRow.D, centerCol.D, radius.D);

                // 步骤5：填充结果
                result.IsValid = true;
                result.CenterRow = centerRow.D;
                result.CenterCol = centerCol.D;
                result.Radius = radius.D;
                result.FitError = fitError;
                result.ValidEdgeCount = edgePoints.Count;

                System.Diagnostics.Debug.WriteLine($"圆拟合成功: 圆心({result.CenterRow:F2}, {result.CenterCol:F2}), 半径:{result.Radius:F2}, 误差:{result.FitError:F3}");

                // 清理临时对象
                contour?.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"圆拟合失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(result.ErrorMessage);
                return result;
            }
        }

        /// <summary>
        /// 计算拟合误差
        /// 计算所有边缘点到拟合圆的平均距离误差
        /// </summary>
        /// <param name="edgePoints">边缘点列表</param>
        /// <param name="centerRow">拟合圆心行坐标</param>
        /// <param name="centerCol">拟合圆心列坐标</param>
        /// <param name="radius">拟合半径</param>
        /// <returns>平均拟合误差</returns>
        private double CalculateFitError(List<EdgePoint> edgePoints, double centerRow, double centerCol, double radius)
        {
            if (edgePoints.Count == 0) return double.MaxValue;

            double totalError = 0.0;

            foreach (var point in edgePoints)
            {
                // 计算点到圆心的距离
                double distanceToCenter = Math.Sqrt(
                    Math.Pow(point.Row - centerRow, 2) + 
                    Math.Pow(point.Col - centerCol, 2)
                );

                // 计算距离误差（点到圆周的距离）
                double error = Math.Abs(distanceToCenter - radius);
                totalError += error;
            }

            return totalError / edgePoints.Count;
        }

        /// <summary>
        /// 创建可视化轮廓
        /// </summary>
        /// <param name="calipers">卡尺信息列表</param>
        /// <param name="edgePoints">边缘点列表</param>
        /// <param name="fitResult">拟合结果</param>
        /// <returns>Halcon显示轮廓</returns>
        private HalconDisplayContours CreateVisualizationContours(List<CaliperInfo> calipers, List<EdgePoint> edgePoints, CircleFitResult fitResult)
        {
            var displayContours = new HalconDisplayContours();

            try
            {
                var allContours = new List<HObject>();

                // 1. 创建卡尺矩形轮廓（蓝色）
                if (ShowCalipers && calipers.Count > 0)
                {
                    foreach (var caliper in calipers)
                    {
                        HOperatorSet.GenRectangle2ContourXld(
                            out HObject caliperContour,
                            caliper.CenterRow,
                            caliper.CenterCol,
                            caliper.Phi,
                            caliper.Length / 2.0,
                            caliper.Width / 2.0
                        );
                        allContours.Add(caliperContour);
                    }

                    // 将所有卡尺轮廓合并
                    if (allContours.Count > 0)
                    {
                        // 循环合并多个轮廓
                        HObject caliperContours = null;
                        for (int i = 0; i < allContours.Count; i++)
                        {
                            if (i == 0)
                            {
                                caliperContours = allContours[i].Clone();
                            }
                            else
                            {
                                HOperatorSet.ConcatObj(caliperContours, allContours[i], out HObject newContours);
                                caliperContours?.Dispose();
                                caliperContours = newContours;
                            }
                        }
                        displayContours.MeasureContours = caliperContours;

                        // 清理临时轮廓
                        foreach (var contour in allContours)
                            contour?.Dispose();
                    }
                }

                // 2. 创建边缘点轮廓（绿色十字）
                if (ShowEdgePoints && edgePoints.Count > 0)
                {
                    var edgePointContours = new List<HObject>();

                    foreach (var point in edgePoints)
                    {
                        HOperatorSet.GenCrossContourXld(
                            out HObject crossContour,
                            point.Row,
                            point.Col,
                            6.0,    // 十字大小
                            Math.PI / 4.0  // 旋转角度
                        );
                        edgePointContours.Add(crossContour);
                    }

                    if (edgePointContours.Count > 0)
                    {
                        // 循环合并多个边缘点轮廓
                        HObject edgeContours = null;
                        for (int i = 0; i < edgePointContours.Count; i++)
                        {
                            if (i == 0)
                            {
                                edgeContours = edgePointContours[i].Clone();
                            }
                            else
                            {
                                HOperatorSet.ConcatObj(edgeContours, edgePointContours[i], out HObject newContours);
                                edgeContours?.Dispose();
                                edgeContours = newContours;
                            }
                        }
                        
                        // 如果已经有测量轮廓，需要合并
                        if (displayContours.MeasureContours != null)
                        {
                            HOperatorSet.ConcatObj(displayContours.MeasureContours, edgeContours, out HObject combinedContours);
                            displayContours.MeasureContours?.Dispose();
                            displayContours.MeasureContours = combinedContours;
                            edgeContours?.Dispose();
                        }
                        else
                        {
                            displayContours.MeasureContours = edgeContours;
                        }

                        // 清理临时轮廓
                        foreach (var contour in edgePointContours)
                            contour?.Dispose();
                    }
                }

                // 3. 创建拟合圆轮廓（红色）
                if (ShowFittedCircle && fitResult.IsValid)
                {
                    HOperatorSet.GenCircleContourXld(
                        out HObject circleContour,
                        fitResult.CenterRow,
                        fitResult.CenterCol,
                        fitResult.Radius,
                        0,                  // 起始角度
                        2.0 * Math.PI,      // 结束角度
                        "positive",         // 方向
                        1.0                 // 分辨率
                    );

                    displayContours.ModelContour = circleContour;

                    System.Diagnostics.Debug.WriteLine("成功创建拟合圆可视化轮廓");
                }

                return displayContours;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建可视化轮廓失败: {ex.Message}");
                return displayContours;
            }
        }

        /// <summary>
        /// 创建测量数据
        /// </summary>
        /// <param name="fitResult">拟合结果</param>
        /// <param name="totalEdgePoints">总边缘点数</param>
        /// <returns>测量数据字典</returns>
        private Dictionary<string, object> CreateMeasurements(CircleFitResult fitResult, int totalEdgePoints)
        {
            var measurements = new Dictionary<string, object>();

            try
            {
                // 基础信息
                measurements["IsValid"] = fitResult.IsValid;
                measurements["TotalEdgePoints"] = totalEdgePoints;
                measurements["ValidEdgePoints"] = fitResult.ValidEdgeCount;
                measurements["CaliperCount"] = CaliperCount;

                if (fitResult.IsValid)
                {
                    // 圆心坐标
                    measurements["CenterRow"] = Math.Round(fitResult.CenterRow, 3);
                    measurements["CenterCol"] = Math.Round(fitResult.CenterCol, 3);

                    // 圆的几何参数
                    measurements["Radius"] = Math.Round(fitResult.Radius, 3);
                    measurements["Diameter"] = Math.Round(fitResult.Radius * 2.0, 3);
                    measurements["Circumference"] = Math.Round(2.0 * Math.PI * fitResult.Radius, 3);
                    measurements["Area"] = Math.Round(Math.PI * fitResult.Radius * fitResult.Radius, 3);

                    // 拟合质量
                    measurements["FitError"] = Math.Round(fitResult.FitError, 6);
                    measurements["FitScore"] = Math.Round(1.0 / (1.0 + fitResult.FitError), 6);

                    System.Diagnostics.Debug.WriteLine($"生成测量数据: 圆心({fitResult.CenterRow:F3}, {fitResult.CenterCol:F3}), 半径{fitResult.Radius:F3}");
                }
                else
                {
                    measurements["ErrorMessage"] = fitResult.ErrorMessage ?? "未知错误";
                    System.Diagnostics.Debug.WriteLine($"拟合失败: {fitResult.ErrorMessage}");
                }

                return measurements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建测量数据失败: {ex.Message}");
                measurements["Error"] = ex.Message;
                return measurements;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取边缘极性字符串
        /// </summary>
        /// <returns>Halcon边缘极性参数</returns>
        private string GetTransitionString()
        {
            return EdgeTransition switch
            {
                EdgeTransition.Positive => "positive",
                EdgeTransition.Negative => "negative",
                EdgeTransition.All => "all",
                _ => "all"
            };
        }

        /// <summary>
        /// 获取边缘选择字符串
        /// </summary>
        /// <returns>Halcon边缘选择参数</returns>
        private string GetSelectionString()
        {
            return EdgeSelection switch
            {
                EdgeSelection.First => "first",
                EdgeSelection.Last => "last",
                EdgeSelection.All => "all",
                _ => "first"
            };
        }

        #endregion
    }

    #region 数据结构定义

    /// <summary>
    /// 卡尺信息
    /// </summary>
    public class CaliperInfo
    {
        /// <summary>卡尺中心行坐标</summary>
        public double CenterRow { get; set; }
        
        /// <summary>卡尺中心列坐标</summary>
        public double CenterCol { get; set; }
        
        /// <summary>卡尺方向角（弧度）</summary>
        public double Phi { get; set; }
        
        /// <summary>卡尺长度</summary>
        public double Length { get; set; }
        
        /// <summary>卡尺宽度</summary>
        public double Width { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CaliperInfo(double centerRow, double centerCol, double phi, double length, double width)
        {
            CenterRow = centerRow;
            CenterCol = centerCol;
            Phi = phi;
            Length = length;
            Width = width;
        }
    }

    /// <summary>
    /// 边缘点信息
    /// </summary>
    public class EdgePoint
    {
        /// <summary>边缘点行坐标</summary>
        public double Row { get; set; }
        
        /// <summary>边缘点列坐标</summary>
        public double Col { get; set; }
        
        /// <summary>边缘强度</summary>
        public double Amplitude { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public EdgePoint(double row, double col, double amplitude)
        {
            Row = row;
            Col = col;
            Amplitude = amplitude;
        }
    }

    /// <summary>
    /// 圆拟合结果
    /// </summary>
    public class CircleFitResult
    {
        /// <summary>拟合是否有效</summary>
        public bool IsValid { get; set; } = false;
        
        /// <summary>拟合圆心行坐标</summary>
        public double CenterRow { get; set; }
        
        /// <summary>拟合圆心列坐标</summary>
        public double CenterCol { get; set; }
        
        /// <summary>拟合半径</summary>
        public double Radius { get; set; }
        
        /// <summary>拟合误差</summary>
        public double FitError { get; set; }
        
        /// <summary>参与拟合的有效边缘点数</summary>
        public int ValidEdgeCount { get; set; }
        
        /// <summary>错误消息</summary>
        public string ErrorMessage { get; set; }
    }

    #endregion
}