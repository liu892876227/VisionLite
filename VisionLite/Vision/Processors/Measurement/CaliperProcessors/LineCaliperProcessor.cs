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
using static VisionLite.Vision.Core.Models.CaliperData;

namespace VisionLite.Vision.Processors.Measurement.CaliperProcessors
{
    /// <summary>
    /// 直线卡尺处理器
    /// 使用多个一维卡尺在直线上进行边缘检测，然后拟合直线
    /// </summary>
    public class LineCaliperProcessor : VisionProcessorBase
    {
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "直线查找";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "卡尺工具";
        
        #region 算法参数

        /// <summary>
        /// 直线起点行坐标
        /// </summary>
        [Parameter("起点Row", "直线起点行坐标", Order = 1, Group = "基础参数", MinValue = 0, MaxValue = 2000)]
        public double StartRow { get; set; } = 200.0;

        /// <summary>
        /// 直线起点列坐标
        /// </summary>
        [Parameter("起点Col", "直线起点列坐标", Order = 2, Group = "基础参数", MinValue = 0, MaxValue = 2000)]
        public double StartCol { get; set; } = 200.0;

        /// <summary>
        /// 直线终点行坐标
        /// </summary>
        [Parameter("终点Row", "直线终点行坐标", Order = 3, Group = "基础参数", MinValue = 0, MaxValue = 2000)]
        public double EndRow { get; set; } = 400.0;

        /// <summary>
        /// 直线终点列坐标
        /// </summary>
        [Parameter("终点Col", "直线终点列坐标", Order = 4, Group = "基础参数", MinValue = 0, MaxValue = 2000)]
        public double EndCol { get; set; } = 400.0;

        /// <summary>
        /// 卡尺数量
        /// </summary>
        [Parameter("卡尺数量", "直线上的卡尺数量", Order = 5, Group = "卡尺参数", MinValue = 3, MaxValue = 100)]
        public int CaliperCount { get; set; } = 5;

        /// <summary>
        /// 卡尺长度
        /// </summary>
        [Parameter("卡尺长度", "每个卡尺的长度（垂直于直线方向）", Order = 6, Group = "卡尺参数", MinValue = 5, MaxValue = 500)]
        public double CaliperLength { get; set; } = 50.0;   // 改为50，更合理

        /// <summary>
        /// 卡尺宽度
        /// </summary>
        [Parameter("卡尺宽度", "每个卡尺的宽度（平行于直线方向）", Order = 7, Group = "卡尺参数", MinValue = 1, MaxValue = 100)]
        public double CaliperWidth { get; set; } = 10.0;    // 改为10，更合理

        /// <summary>
        /// 边缘阈值
        /// </summary>
        [Parameter("边缘阈值", "边缘检测阈值", Order = 8, Group = "检测参数", MinValue = 1, MaxValue = 255)]
        public double EdgeThreshold { get; set; } = 30.0;

        /// <summary>
        /// 边缘极性
        /// </summary>
        [Parameter("边缘极性", "边缘检测极性", Order = 9, Group = "检测参数")]
        public EdgeTransition EdgeTransition { get; set; } = EdgeTransition.All;

        /// <summary>
        /// 平滑参数
        /// </summary>
        [Parameter("平滑参数", "高斯平滑sigma值", Order = 10, Group = "检测参数", MinValue = 0.5, MaxValue = 10)]
        public double Sigma { get; set; } = 1.0;

        /// <summary>
        /// 边缘选择
        /// </summary>
        [Parameter("边缘选择", "边缘选择策略", Order = 11, Group = "检测参数")]
        public EdgeSelection EdgeSelection { get; set; } = EdgeSelection.First;

        /// <summary>
        /// 最小拟合点数
        /// </summary>
        [Parameter("最小拟合点数", "直线拟合所需的最小边缘点数", Order = 12, Group = "高级参数", MinValue = 2, MaxValue = 50, IsAdvanced = true)]
        public int MinFitPoints { get; set; } = 3;

        /// <summary>
        /// 拟合算法
        /// </summary>
        [Parameter("拟合算法", "直线拟合算法类型", Order = 13, Group = "高级参数", IsAdvanced = true)]
        public LineFittingAlgorithm FittingAlgorithm { get; set; } = LineFittingAlgorithm.Tukey;

        /// <summary>
        /// 显示卡尺工具
        /// </summary>
        [Parameter("显示卡尺", "是否显示卡尺工具", Order = 14, Group = "显示参数")]
        public bool ShowCalipers { get; set; } = true;

        /// <summary>
        /// 显示边缘点
        /// </summary>
        [Parameter("显示边缘点", "是否显示检测到的边缘点", Order = 15, Group = "显示参数")]
        public bool ShowEdgePoints { get; set; } = true;

        /// <summary>
        /// 显示拟合直线
        /// </summary>
        [Parameter("显示拟合直线", "是否显示拟合的直线", Order = 16, Group = "显示参数")]
        public bool ShowFittedLine { get; set; } = true;

        #endregion

        #region 类型

        /// <summary>
        /// 线段结构
        /// </summary>
        public class LineSegment
        {
            public PointD Start { get; set; }
            public PointD End { get; set; }

            public double Length => Math.Sqrt(Math.Pow(End.Row - Start.Row, 2) + Math.Pow(End.Col - Start.Col, 2));
            public PointD MidPoint => new PointD((Start.Row + End.Row) / 2.0, (Start.Col + End.Col) / 2.0);
        }

        /// <summary>
        /// 点结构
        /// </summary>
        public class PointD
        {
            public double Row { get; set; }
            public double Col { get; set; }

            public PointD(double row, double col)
            {
                Row = row;
                Col = col;
            }
        }

        /// <summary>
        /// 卡尺处理结果
        /// </summary>
        public class CaliperProcessingResult
        {
            public List<CaliperInfo> ValidCalipers { get; set; } = new List<CaliperInfo>();
            public int AdjustedCalipers { get; set; }
            public int SkippedCalipers { get; set; }
        }

        #endregion


        #region 核心算法方法

        /// <summary>
        /// 计算卡尺位置
        /// 根据直线ROI参数计算各个卡尺的位置和方向
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="roiGeometry">ROI几何信息</param>
        /// <returns>卡尺信息列表</returns>
        public List<CaliperInfo> CalculateCaliperPositions(VisionImage image, ROIGeometry roiGeometry)
        {
            var calipers = new List<CaliperInfo>();
            
            try
            {
                // 使用当前参数或ROI参数
                double startRow = roiGeometry?.Parameters.ContainsKey("row1") == true ? roiGeometry.Parameters["row1"] : StartRow;
                double startCol = roiGeometry?.Parameters.ContainsKey("column1") == true ? roiGeometry.Parameters["column1"] : StartCol;
                double endRow = roiGeometry?.Parameters.ContainsKey("row2") == true ? roiGeometry.Parameters["row2"] : EndRow;
                double endCol = roiGeometry?.Parameters.ContainsKey("column2") == true ? roiGeometry.Parameters["column2"] : EndCol;
                
                // 使用Halcon的 angle_lx 直接计算直线的角度
                HOperatorSet.AngleLx(startRow, startCol, endRow, endCol, out HTuple lineAngle);

                // 卡尺的角度 = 直线角度 + 90度 (PI/2)
                // Halcon的角度单位是弧度，所以可以直接加
                double caliperPhi = lineAngle.D + (Math.PI / 2.0);

                // 沿直线均匀分布卡尺
                for (int i = 0; i < CaliperCount; i++)
                {
                    // 在CaliperCount > 1时，确保t的范围是[0, 1]
                    double t = (CaliperCount > 1 && i > 0) ? (double)i / (CaliperCount - 1) : (CaliperCount == 1 ? 0.5 : 0.0);

                    // 卡尺中心位置
                    double centerRow = startRow + t * (endRow - startRow);
                    double centerCol = startCol + t * (endCol - startCol);

                    // 所有卡尺都使用这个由Halcon算子计算出的正确的垂直角度
                    calipers.Add(new CaliperInfo(centerRow, centerCol, caliperPhi, CaliperLength, CaliperWidth));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"计算卡尺位置失败: {ex.Message}", ex);
            }
            
            return calipers;
        }

        /// <summary>
        /// 使用卡尺检测边缘
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="calipers">卡尺信息列表</param>
        /// <returns>边缘检测结果</returns>
        private List<EdgeResult> DetectEdgesWithCalipers(HObject image, List<CaliperInfo> calipers)
        {
            var edgeResults = new List<EdgeResult>();
            
            try
            {
                // 获取图像尺寸
                HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                
                for (int i = 0; i < calipers.Count; i++)
                {
                    var caliper = calipers[i];
                    HTuple measureHandle = null;
                    
                    try
                    {
                        // 创建测量句柄
                        HOperatorSet.GenMeasureRectangle2(
                            caliper.CenterRow, caliper.CenterCol, caliper.Phi,
                            caliper.Length / 2, caliper.Width / 2,
                            width, height, "nearest_neighbor", out measureHandle);
                        
                        // 执行边缘检测
                        HOperatorSet.MeasurePos(image, measureHandle, Sigma, EdgeThreshold, 
                            GetTransitionString(), GetSelectionString(),
                            out HTuple edgeRows, out HTuple edgeCols, out HTuple amplitudes, out HTuple distances);
                        
                        
                        // 处理检测结果，增强HTuple安全性检查
                        // 注意：distances可能为空，不作为必要条件
                        if (edgeRows != null && edgeRows.Length > 0 && 
                            edgeCols != null && edgeCols.Length > 0 &&
                            amplitudes != null && amplitudes.Length > 0)
                        {
                            try
                            {
                                // 安全访问HTuple.DArr，防止HTupleVoid异常
                                double[] edgeRowsArray = SafeGetDoubleArray(edgeRows);
                                double[] edgeColsArray = SafeGetDoubleArray(edgeCols);
                                double[] amplitudesArray = SafeGetDoubleArray(amplitudes);
                                double[] distancesArray = SafeGetDoubleArray(distances);
                                
                                var edgeResult = new EdgeResult
                                {
                                    Rectangle = new MeasureRectangle
                                    {
                                        Row = caliper.CenterRow,
                                        Column = caliper.CenterCol,
                                        Phi = caliper.Phi,
                                        Length1 = caliper.Length / 2,
                                        Length2 = caliper.Width / 2,
                                        Index = i
                                    },
                                    MeasureIndex = i,
                                    EdgeRows = edgeRowsArray,
                                    EdgeCols = edgeColsArray,
                                    Amplitudes = amplitudesArray,
                                    Distances = distancesArray,
                                    IsValid = true,
                                    EdgeType = EdgeTransition,
                                    Quality = amplitudesArray.Length > 0 ? amplitudesArray.Average() : 0.0
                                };
                                
                                // 选择主要边缘点，增加索引边界检查
                                if (edgeRows.Length > 0 && edgeCols.Length > 0 && amplitudes.Length > 0)
                                {
                                    int selectedIndex = SelectEdgeIndex(amplitudes);
                                    
                                    if (selectedIndex >= 0 && selectedIndex < edgeRows.Length && selectedIndex < edgeCols.Length)
                                    {
                                        edgeResult.Row = edgeRows[selectedIndex].D;
                                        edgeResult.Column = edgeCols[selectedIndex].D;
                                        edgeResult.Amplitude = amplitudes[selectedIndex].D;
                                        edgeResult.Distance = (distances != null && selectedIndex < distances.Length) ? distances[selectedIndex].D : 0.0;
                                    }
                                    else
                                    {
                                        // 如果索引检查不通过，标记为无效
                                        edgeResult.IsValid = false;
                                    }
                                }
                                else
                                {
                                    // 如果没有边缘数据，标记为无效
                                    edgeResult.IsValid = false;
                                }
                                
                                edgeResults.Add(edgeResult);
                            }
                            catch (Exception)
                            {
                                
                                // HTuple访问异常，创建无效的边缘结果
                                var invalidResult = new EdgeResult
                                {
                                    MeasureIndex = i,
                                    IsValid = false,
                                    Rectangle = new MeasureRectangle
                                    {
                                        Row = caliper.CenterRow,
                                        Column = caliper.CenterCol,
                                        Phi = caliper.Phi,
                                        Length1 = caliper.Length / 2,
                                        Length2 = caliper.Width / 2,
                                        Index = i
                                    }
                                };
                                edgeResults.Add(invalidResult);
                            }
                        }
                        else
                        {
                            // 未检测到有效边缘，不创建EdgeResult
                        }
                    }
                    finally
                    {
                        measureHandle?.Dispose();
                    }
                }
                
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"边缘检测失败: {ex.Message}", ex);
            }
            
            return edgeResults;
        }

        /// <summary>
        /// 根据边缘选择策略选择边缘索引
        /// </summary>
        private int SelectEdgeIndex(HTuple amplitudes)
        {
            if (amplitudes == null || amplitudes.Length == 0)
                return 0;
                
            try
            {
                return EdgeSelection switch
                {
                    EdgeSelection.First => 0,
                    EdgeSelection.Last => Math.Max(0, amplitudes.Length - 1),
                    EdgeSelection.Strongest => amplitudes.DArr != null && amplitudes.DArr.Length > 0
                        ? amplitudes.DArr.Select((v, i) => new { Value = v, Index = i })
                                         .OrderByDescending(x => x.Value)
                                         .FirstOrDefault()?.Index ?? 0
                        : 0,
                    _ => 0
                };
            }
            catch (Exception)
            {
                return 0; // 默认返回第一个索引
            }
        }

        /// <summary>
        /// 从边缘点拟合直线
        /// </summary>
        /// <param name="edges">边缘检测结果</param>
        /// <returns>直线拟合结果</returns>
        private LineFitResult FitLineFromEdges(List<EdgeResult> edges)
        {
            var result = new LineFitResult
            {
                Algorithm = ConvertToFittingAlgorithm(FittingAlgorithm),
                FitTime = DateTime.Now,
                UsedEdges = edges
            };
            
            try
            {
                // 筛选有效边缘点
                var validEdges = edges.Where(e => e.IsValid && !double.IsNaN(e.Row) && !double.IsNaN(e.Column)).ToList();
                result.UsedPointCount = validEdges.Count;
                
                if (validEdges.Count < MinFitPoints)
                {
                    result.Success = false;
                    result.ErrorMessage = $"有效边缘点数量({validEdges.Count})少于最小要求({MinFitPoints})";
                    return result;
                }
                
                // 准备拟合数据
                var rows = validEdges.Select(e => e.Row).ToArray();
                var cols = validEdges.Select(e => e.Column).ToArray();
                
                // 执行直线拟合
                HTuple rowBegin, colBegin, rowEnd, colEnd, nr, nc, dist;
                
                // 创建轮廓用于拟合
                HOperatorSet.GenContourPolygonXld(out HObject contour, rows, cols);
                
                // 根据算法类型选择拟合方法
                string algorithm = GetFittingAlgorithmString();
                HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0, 5, 2,
                    out rowBegin, out colBegin, out rowEnd, out colEnd, out nr, out nc, out dist);
                
                // 增强HTuple访问安全性
                if (rowBegin != null && rowBegin.Length > 0 &&
                    colBegin != null && colBegin.Length > 0 &&
                    rowEnd != null && rowEnd.Length > 0 &&
                    colEnd != null && colEnd.Length > 0)
                {
                    try
                    {
                        result.StartRow = rowBegin[0].D;
                        result.StartColumn = colBegin[0].D;
                        result.EndRow = rowEnd[0].D;
                        result.EndColumn = colEnd[0].D;
                        result.FitError = dist != null && dist.Length > 0 ? dist[0].D : 0.0;
                        result.Success = true;
                        result.Score = CalculateFitQuality(validEdges, result);
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"访问拟合结果失败: {ex.Message}";
                    }
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Halcon直线拟合返回空结果";
                }
                
                contour?.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"直线拟合异常: {ex.Message}";
            }
            
            return result;
        }

        /// <summary>
        /// 计算拟合质量
        /// </summary>
        private double CalculateFitQuality(List<EdgeResult> edges, LineFitResult fitResult)
        {
            if (edges.Count == 0) return 0.0;
            
            // 基于拟合误差和边缘强度的质量评分
            double avgAmplitude = edges.Average(e => e.Amplitude);
            double errorScore = Math.Max(0, 100 - fitResult.FitError * 10);
            double amplitudeScore = Math.Min(100, avgAmplitude / 2.55); // 假设最大幅度255
            
            return (errorScore + amplitudeScore) / 2;
        }

        #endregion

        #region 主处理方法

        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage image)
        {
            return await Task.Run(() =>
            {
                // 开始计时
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    // 参数验证
                    if (image?.HImage == null)
                    {
                        return CreateFailureResult("输入图像无效");
                    }
                    
                    if (CaliperCount < 3)
                    {
                        return CreateFailureResult("卡尺数量必须至少为3个");
                    }
                    
                    // 创建ROI几何对象
                    var roiGeometry = new ROIGeometry
                    {
                        RoiType = "line",
                        Parameters = new Dictionary<string, double>
                        {
                            ["row1"] = StartRow,
                            ["column1"] = StartCol,
                            ["row2"] = EndRow,
                            ["column2"] = EndCol
                        }
                    };

                    // 生成原始卡尺位置
                    var originalCalipers = CalculateCaliperPositions(image, roiGeometry);

                    // 对原始卡尺进行边界裁剪
                    var processingResult = ProcessCalipersWithBoundaryClipping(originalCalipers, image.Width, image.Height);

                    // 只使用裁剪后的有效卡尺进行边缘检测
                    var edges = DetectEdgesWithCalipers(image.HImage, processingResult.ValidCalipers);

                    // 执行直线拟合
                    var lineFit = FitLineFromEdges(edges);

                    // 创建Halcon标准轮廓显示
                    var displayContours = CreateVisualizationContours(processingResult.ValidCalipers, edges, lineFit);

                    // 创建几何显示元素（用于边缘点显示）
                    var geometryElements = new List<GeometryElement>();

                    //创建边缘点几何元素用于显示
                    if (ShowEdgePoints && edges.Any())
                    {
                        geometryElements.AddRange(CreateEdgePointElements(edges));
                    }

                    // 创建测量数据
                    var measurements = new Dictionary<string, object>
                    {
                        ["原始卡尺数量"] = originalCalipers.Count,
                        ["有效卡尺数量"] = processingResult.ValidCalipers.Count,
                        ["被裁剪卡尺数量"] = processingResult.AdjustedCalipers,
                        ["被跳过卡尺数量"] = processingResult.SkippedCalipers,
                        ["有效边缘点数量"] = edges.Count(e => e.IsValid),
                        ["拟合质量"] = $"{lineFit.Score:F2}%",
                        ["拟合成功"] = lineFit.Success ? "是" : "否",
                        ["拟合误差信息"] = lineFit.ErrorMessage ?? "无错误",
                        ["拟合直线结果"] = $"起点({lineFit.StartRow:F2},{lineFit.StartColumn:F2}) 终点({lineFit.EndRow:F2},{lineFit.EndColumn:F2})",
                        ["起点"] = $"{{ Row = {lineFit.StartRow:F1}, Column = {lineFit.StartColumn:F1} }}",
                        ["终点"] = $"{{ Row = {lineFit.EndRow:F1}, Column = {lineFit.EndColumn:F1} }}",
                        ["长度"] = $"{lineFit.Length:F2} 像素",
                        ["角度"] = $"{lineFit.AngleDegrees:F2}°"
                    };

                    // 创建输出图像
                    var outputImage = image.Clone();

                    // 创建成功结果（时间稍后设置）
                    var result = CreateSuccessResult(outputImage, TimeSpan.Zero, measurements);
                    
                    // 添加Halcon轮廓数据（优先显示）
                    if (displayContours != null)
                    {
                        result.AddMetadata("HalconDisplayContours", displayContours);
                    }

                    // 将边缘点列表添加到 ProcessResult 的标准 GeometryElements 属性中
                    if (geometryElements.Any())
                    {
                        result.GeometryElements.AddRange(geometryElements);
                    }
                    result.AddMetadata("EdgeResults", edges);
                    
                    // 停止计时并设置执行时间
                    stopwatch.Stop();
                    result.ProcessingTime = stopwatch.Elapsed;
                    
                    return result;
                }
                catch (Exception ex)
                {
                    return CreateFailureResult($"处理失败: {ex.Message}");
                }
            });
        }

        #endregion

        #region 轮廓显示方法

        /// <summary>
        /// 创建可视化轮廓（使用Halcon标准轮廓）
        /// </summary>
        /// <param name="calipers">卡尺信息列表</param>
        /// <param name="edges">边缘检测结果</param>
        /// <param name="lineFit">直线拟合结果</param>
        /// <returns>Halcon显示轮廓</returns>
        private HalconDisplayContours CreateVisualizationContours(List<CaliperInfo> calipers, List<EdgeResult> edges, LineFitResult lineFit)
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

                // 2. 创建拟合直线轮廓（绿色）
                if (ShowFittedLine && lineFit.Success)
                {
                    HOperatorSet.GenContourPolygonXld(
                        out HObject lineContour,
                        new HTuple(new double[] { lineFit.StartRow, lineFit.EndRow }),
                        new HTuple(new double[] { lineFit.StartColumn, lineFit.EndColumn })
                    );
                    displayContours.ModelContour = lineContour;
                }
            }
            catch (Exception)
            {
                displayContours?.Dispose();
                return null;
            }

            return displayContours;
        }

        #endregion

        #region 边界裁剪方法

        /// <summary>
        /// 智能卡尺裁剪处理
        /// </summary>
        /// <param name="originalCalipers">原始卡尺列表</param>
        /// <param name="imageWidth">图像宽度</param>
        /// <param name="imageHeight">图像高度</param>
        /// <returns>处理后的有效卡尺列表</returns>
        public CaliperProcessingResult ProcessCalipersWithBoundaryClipping(List<CaliperInfo> originalCalipers, int imageWidth, int imageHeight)
        {
            var result = new CaliperProcessingResult();

            foreach (var caliper in originalCalipers)
            {
                // 1. 将卡尺的测量轴线转换为线段
                var measureLine = GetCaliperMeasureLine(caliper);

                // 2. 使用Cohen-Sutherland算法裁剪线段
                var clippedLine = ClipLineToImageBounds(measureLine, imageWidth, imageHeight);

                if (clippedLine == null)
                {
                    // 线段完全在图像外，跳过
                    result.SkippedCalipers++;
                    continue;
                }

                // 3. 根据裁剪后的线段重新创建卡尺
                var clippedCaliper = CreateClippedCaliper(caliper, clippedLine);

                // 4. 检查裁剪后的长度是否仍然有效（例如，大于原始长度的10%）
                if (IsValidCaliperLength(clippedCaliper, caliper, 0.1))
                {
                    result.ValidCalipers.Add(clippedCaliper);
                    if (Math.Abs(clippedCaliper.Length - caliper.Length) > 0.1)
                        result.AdjustedCalipers++;
                }
                else
                {
                    // 裁剪后太短，跳过
                    result.SkippedCalipers++;
                }
            }

            return result;
        }

        /// <summary>
        /// 将卡尺转换为其中心测量线段
        /// </summary>
        private LineSegment GetCaliperMeasureLine(CaliperInfo caliper)
        {
            double halfLength = caliper.Length / 2.0;

            // 计算线段的起点和终点
            var startPoint = new PointD(
                caliper.CenterRow - halfLength * Math.Sin(caliper.Phi),
                caliper.CenterCol - halfLength * Math.Cos(caliper.Phi)
            );

            var endPoint = new PointD(
                caliper.CenterRow + halfLength * Math.Sin(caliper.Phi),
                caliper.CenterCol + halfLength * Math.Cos(caliper.Phi)
            );

            return new LineSegment { Start = startPoint, End = endPoint };
        }

        /// <summary>
        /// 使用Cohen-Sutherland线段裁剪算法将线段限制在图像边界内
        /// </summary>
        private LineSegment ClipLineToImageBounds(LineSegment line, int imageWidth, int imageHeight)
        {
            const int INSIDE = 0; // 0000
            const int LEFT = 1;   // 0001
            const int RIGHT = 2;  // 0010
            const int BOTTOM = 4; // 0100
            const int TOP = 8;    // 1000

            int ComputeOutCode(PointD p)
            {
                int code = INSIDE;
                if (p.Col < 0) code |= LEFT;
                else if (p.Col >= imageWidth) code |= RIGHT;
                if (p.Row < 0) code |= TOP;
                else if (p.Row >= imageHeight) code |= BOTTOM;
                return code;
            }

            var p1 = new PointD(line.Start.Row, line.Start.Col);
            var p2 = new PointD(line.End.Row, line.End.Col);

            int outcode1 = ComputeOutCode(p1);
            int outcode2 = ComputeOutCode(p2);

            while (true)
            {
                if ((outcode1 | outcode2) == 0) // 完全在内部
                {
                    return new LineSegment { Start = p1, End = p2 };
                }
                else if ((outcode1 & outcode2) != 0) // 完全在外部
                {
                    return null;
                }

                int outcodeOut = outcode1 != 0 ? outcode1 : outcode2;
                PointD intersection;

                if ((outcodeOut & TOP) != 0)
                {
                    intersection = new PointD(0, p1.Col + (p2.Col - p1.Col) * (0 - p1.Row) / (p2.Row - p1.Row));
                }
                else if ((outcodeOut & BOTTOM) != 0)
                {
                    intersection = new PointD(imageHeight - 1, p1.Col + (p2.Col - p1.Col) * (imageHeight - 1 - p1.Row) / (p2.Row - p1.Row));
                }
                else if ((outcodeOut & RIGHT) != 0)
                {
                    intersection = new PointD(p1.Row + (p2.Row - p1.Row) * (imageWidth - 1 - p1.Col) / (p2.Col - p1.Col), imageWidth - 1);
                }
                else // LEFT
                {
                    intersection = new PointD(p1.Row + (p2.Row - p1.Row) * (0 - p1.Col) / (p2.Col - p1.Col), 0);
                }

                if (outcodeOut == outcode1)
                {
                    p1 = intersection;
                    outcode1 = ComputeOutCode(p1);
                }
                else
                {
                    p2 = intersection;
                    outcode2 = ComputeOutCode(p2);
                }
            }
        }

        /// <summary>
        /// 根据裁剪后的线段创建新的卡尺信息
        /// </summary>
        private CaliperInfo CreateClippedCaliper(CaliperInfo original, LineSegment clippedLine)
        {
            return new CaliperInfo(
                clippedLine.MidPoint.Row,
                clippedLine.MidPoint.Col,
                original.Phi,
                clippedLine.Length,
                original.Width
            );
        }

        /// <summary>
        /// 检查卡尺长度是否满足最小比例阈值
        /// </summary>
        private bool IsValidCaliperLength(CaliperInfo clipped, CaliperInfo original, double minimumRatio)
        {
            if (original.Length < 1e-6) return false;
            double lengthRatio = clipped.Length / original.Length;
            return lengthRatio >= minimumRatio;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建卡尺显示元素
        /// </summary>
        private List<GeometryElement> CreateCaliperElements(List<CaliperInfo> calipers)
        {
            var elements = new List<GeometryElement>();
            
            for (int i = 0; i < calipers.Count; i++)
            {
                var caliper = calipers[i];
                
                // 计算卡尺矩形的四个角点
                double cosA = Math.Cos(caliper.Phi);
                double sinA = Math.Sin(caliper.Phi);
                double halfLength = caliper.Length / 2;
                double halfWidth = caliper.Width / 2;
                
                // 计算矩形四条边的端点
                double r1 = caliper.CenterRow - halfLength * cosA - halfWidth * sinA;
                double c1 = caliper.CenterCol - halfLength * sinA + halfWidth * cosA;
                double r2 = caliper.CenterRow + halfLength * cosA - halfWidth * sinA;
                double c2 = caliper.CenterCol + halfLength * sinA + halfWidth * cosA;
                double r3 = caliper.CenterRow + halfLength * cosA + halfWidth * sinA;
                double c3 = caliper.CenterCol + halfLength * sinA - halfWidth * cosA;
                double r4 = caliper.CenterRow - halfLength * cosA + halfWidth * sinA;
                double c4 = caliper.CenterCol - halfLength * sinA - halfWidth * cosA;
                
                // 添加四条边线
                elements.Add(new LineElement(r1, c1, r2, c2) { Name = $"卡尺{i}_上边", Color = Colors.Blue, LineWidth = 1.0 });
                elements.Add(new LineElement(r2, c2, r3, c3) { Name = $"卡尺{i}_右边", Color = Colors.Blue, LineWidth = 1.0 });
                elements.Add(new LineElement(r3, c3, r4, c4) { Name = $"卡尺{i}_下边", Color = Colors.Blue, LineWidth = 1.0 });
                elements.Add(new LineElement(r4, c4, r1, c1) { Name = $"卡尺{i}_左边", Color = Colors.Blue, LineWidth = 1.0 });
            }
            
            return elements;
        }

        /// <summary>
        /// 创建边缘点显示元素
        /// </summary>
        private List<GeometryElement> CreateEdgePointElements(List<EdgeResult> edges)
        {
            var elements = new List<GeometryElement>();
            
            foreach (var edge in edges.Where(e => e.IsValid))
            {
                elements.Add(new PointElement(edge.Row, edge.Column)
                {
                    Name = $"边缘点{edge.MeasureIndex}",
                    Description = $"强度: {edge.Amplitude:F1}",
                    Color = Colors.Blue,
                    Size = 2.0
                });
            }
            
            return elements;
        }

        /// <summary>
        /// 获取边缘极性字符串
        /// </summary>
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
        private string GetSelectionString()
        {
            return EdgeSelection switch
            {
                EdgeSelection.First => "first",
                EdgeSelection.Last => "last",
                EdgeSelection.All => "all",
                EdgeSelection.Strongest => "all", // Halcon中先获取所有边缘，后处理选择最强的
                _ => "all"
            };
        }

        /// <summary>
        /// 获取拟合算法字符串（FitLineContourXld专用）
        /// </summary>
        private string GetFittingAlgorithmString()
        {
            return FittingAlgorithm switch
            {
                LineFittingAlgorithm.Regression => "regression",    // 标准回归（最小二乘法）
                LineFittingAlgorithm.Gauss => "gauss",              // 高斯加权拟合
                LineFittingAlgorithm.Huber => "huber",              // Huber加权拟合
                LineFittingAlgorithm.Tukey => "tukey",              // Tukey加权拟合
                LineFittingAlgorithm.Drop => "drop",                // 忽略离群点拟合
                _ => "tukey"                                         // 默认使用tukey算法
            };
        }

        /// <summary>
        /// 将LineFittingAlgorithm转换为FittingAlgorithm（向后兼容）
        /// </summary>
        private FittingAlgorithm ConvertToFittingAlgorithm(LineFittingAlgorithm lineFittingAlgorithm)
        {
            return lineFittingAlgorithm switch
            {
                LineFittingAlgorithm.Regression => Core.Enums.FittingAlgorithm.Algebraic,
                LineFittingAlgorithm.Gauss => Core.Enums.FittingAlgorithm.Geometric,
                LineFittingAlgorithm.Huber => Core.Enums.FittingAlgorithm.AHuber,
                LineFittingAlgorithm.Tukey => Core.Enums.FittingAlgorithm.ATukey,
                LineFittingAlgorithm.Drop => Core.Enums.FittingAlgorithm.GeoHuber,
                _ => Core.Enums.FittingAlgorithm.ATukey
            };
        }

        /// <summary>
        /// 安全获取HTuple的双精度数组，处理HTupleVoid异常情况
        /// </summary>
        /// <param name="tuple">HTuple对象</param>
        /// <returns>双精度数组，如果是HTupleVoid则返回空数组</returns>
        private double[] SafeGetDoubleArray(HTuple tuple)
        {
            try
            {
                // 检查HTuple是否有效且包含数据
                if (tuple == null || tuple.Length == 0)
                {
                    return new double[0];
                }
                
                // 尝试访问DArr属性
                var arrayData = tuple.DArr;
                return arrayData ?? new double[0];
            }
            catch (Exception)
            {
                // 捕获HTupleVoid异常或其他访问异常，返回空数组
                return new double[0];
            }
        }

        #endregion
    }
}