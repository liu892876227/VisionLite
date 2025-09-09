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
    /// 边缘卡尺处理器
    /// 基础的边缘检测卡尺工具，支持多种ROI类型的边缘测量
    /// </summary>
    public class EdgeCaliperProcessor : VisionProcessorBase
    {
        #region 属性定义

        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "边缘卡尺";

        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "测量工具";

        /// <summary>
        /// 卡尺数量（固定为1，用于HDrawingObject交互模式）
        /// </summary>
        [Parameter("卡尺数量", "卡尺数量（交互模式固定为1）", 1, 1, Step = 1, Order = 1, Group = "基本参数")]
        public int CaliperCount { get; set; } = 1;

        /// <summary>
        /// 卡尺中心行坐标
        /// </summary>
        [Parameter("中心行", "卡尺中心的行坐标", 0, 4096, Step = 0.1, Order = 2, Group = "位置参数")]
        public double CenterRow { get; set; } = 256;

        /// <summary>
        /// 卡尺中心列坐标
        /// </summary>
        [Parameter("中心列", "卡尺中心的列坐标", 0, 4096, Step = 0.1, Order = 3, Group = "位置参数")]
        public double CenterCol { get; set; } = 256;

        /// <summary>
        /// 卡尺角度（度）
        /// </summary>
        [Parameter("角度", "卡尺的旋转角度（度）", -180, 180, Step = 0.1, Order = 4, Group = "位置参数")]
        public double Phi { get; set; } = 0;

        /// <summary>
        /// 卡尺长度1（半长）
        /// </summary>
        [Parameter("长度1", "卡尺的长度1（半长）", 5, 500, Step = 1, Order = 5, Group = "位置参数")]
        public double Length1 { get; set; } = 50;

        /// <summary>
        /// 卡尺长度2（半宽）
        /// </summary>
        [Parameter("长度2", "卡尺的长度2（半宽）", 2, 200, Step = 1, Order = 6, Group = "位置参数")]
        public double Length2 { get; set; } = 15;


        /// <summary>
        /// 边缘阈值
        /// </summary>
        [Parameter("边缘阈值", "边缘检测的阈值", 1, 100, Step = 1, Order = 4, Group = "检测参数")]
        public double EdgeThreshold { get; set; } = 20;

        /// <summary>
        /// 高斯平滑
        /// </summary>
        [Parameter("高斯平滑", "边缘检测前的高斯平滑参数", 0.5, 5.0, Step = 0.1, Order = 5, Group = "检测参数")]
        public double Sigma { get; set; } = 1.0;

        /// <summary>
        /// 边缘极性
        /// </summary>
        [Parameter("边缘极性", "边缘检测的极性", Order = 6, Group = "检测参数")]
        public EdgeTransition Transition { get; set; } = EdgeTransition.All;

        /// <summary>
        /// 选择策略
        /// </summary>
        [Parameter("选择策略", "边缘点选择策略", Order = 7, Group = "检测参数")]
        public EdgeSelection Selection { get; set; } = EdgeSelection.All;

        /// <summary>
        /// 测量模式
        /// </summary>
        [Parameter("测量模式", "单边缘或边缘对测量", Order = 8, Group = "检测参数")]
        public MeasureMode Mode { get; set; } = MeasureMode.SingleEdge;

        /// <summary>
        /// 最小边缘强度
        /// </summary>
        [Parameter("最小强度", "边缘的最小强度阈值", 0, 255, Step = 1, Order = 9, Group = "高级参数", IsAdvanced = true)]
        public double MinAmplitude { get; set; } = 10;

        /// <summary>
        /// 显示测量矩形
        /// </summary>
        [Parameter("显示测量矩形", "是否在结果中显示测量矩形", Order = 10, Group = "显示选项")]
        public bool ShowMeasureRectangles { get; set; } = true;

        /// <summary>
        /// 显示边缘点
        /// </summary>
        [Parameter("显示边缘点", "是否在结果中显示检测到的边缘点", Order = 11, Group = "显示选项")]
        public bool ShowEdgePoints { get; set; } = true;

        #endregion

        #region 核心处理方法

        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            // 使用默认ROI获取方式
            return await ProcessWithROIAsync(inputImage, null);
        }
        
        /// <summary>
        /// 使用指定ROI异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="roiGeometry">ROI几何信息</param>
        /// <returns>处理结果</returns>
        public async Task<ProcessResult> ProcessWithROIAsync(VisionImage inputImage, CaliperData.ROIGeometry roiGeometry)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 1. 参数验证
                if (!ValidateParameters())
                {
                    return CreateFailureResult("参数验证失败，请检查参数设置");
                }

                // 2. 获取ROI并生成测量矩形
                var measureRectangles = await Task.Run(() => GenerateMeasureRectangles(roiGeometry));
                if (measureRectangles.Count == 0)
                {
                    return CreateFailureResult("无法生成有效的测量矩形，请检查ROI设置");
                }

                // 3. 执行边缘测量
                var edgeResults = await Task.Run(() => ExecuteEdgeMeasurement(inputImage, measureRectangles));
                
                // 4. 生成结果图像和几何元素
                var outputImage = inputImage.Clone();
                var geometryElements = CreateResultVisualization(edgeResults, measureRectangles);

                stopwatch.Stop();

                // 5. 创建测量结果
                var measurements = CreateMeasurements(edgeResults, measureRectangles, stopwatch.Elapsed);

                // 6. 添加几何元素到结果中
                var result = CreateSuccessResult(outputImage, stopwatch.Elapsed, measurements);
                foreach (var element in geometryElements)
                {
                    result.AddGeometryElement(element);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateFailureResult($"边缘卡尺测量失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 生成单个测量矩形（HDrawingObject交互模式）
        /// </summary>
        /// <param name="customROI">自定义ROI信息（未使用，保持接口兼容性）</param>
        /// <returns>包含单个测量矩形的列表</returns>
        private List<CaliperData.MeasureRectangle> GenerateMeasureRectangles(CaliperData.ROIGeometry customROI = null)
        {
            var rectangles = new List<CaliperData.MeasureRectangle>();
            
            // 创建单个测量矩形，直接使用参数
            var rectangle = new CaliperData.MeasureRectangle
            {
                Index = 0,
                Row = CenterRow,
                Column = CenterCol,
                Phi = Phi * Math.PI / 180.0, // 转换为弧度
                Length1 = Length1,
                Length2 = Length2
            };
            
            rectangles.Add(rectangle);
            return rectangles;
        }

        /// <summary>
        /// 执行边缘测量
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="measureRectangles">测量矩形列表</param>
        /// <returns>边缘检测结果</returns>
        private List<CaliperData.EdgeResult> ExecuteEdgeMeasurement(
            VisionImage inputImage, List<CaliperData.MeasureRectangle> measureRectangles)
        {
            var edgeResults = new List<CaliperData.EdgeResult>();

            foreach (var rectangle in measureRectangles)
            {
                try
                {
                    // 创建Halcon测量句柄
                    rectangle.CreateMeasureHandle(inputImage.Width, inputImage.Height);

                    if (Mode == MeasureMode.SingleEdge)
                    {
                        // 单边缘测量
                        var singleEdges = MeasureSingleEdges(inputImage, rectangle);
                        edgeResults.AddRange(singleEdges);
                    }
                    else
                    {
                        // 边缘对测量
                        var edgePairs = MeasureEdgePairs(inputImage, rectangle);
                        edgeResults.AddRange(edgePairs);
                    }
                }
                catch (Exception ex)
                {
                    // 记录失败的测量，但继续处理其他矩形
                    System.Diagnostics.Debug.WriteLine($"测量矩形 {rectangle.Index} 失败: {ex.Message}");
                }
                finally
                {
                    // 确保释放测量句柄
                    rectangle.Dispose();
                }
            }

            // 过滤边缘结果
            return FilterEdgeResults(edgeResults);
        }

        /// <summary>
        /// 单边缘测量
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="rectangle">测量矩形</param>
        /// <returns>边缘结果列表</returns>
        private List<CaliperData.EdgeResult> MeasureSingleEdges(
            VisionImage inputImage, CaliperData.MeasureRectangle rectangle)
        {
            var edges = new List<CaliperData.EdgeResult>();

            // 调用Halcon MeasurePos算子
            HOperatorSet.MeasurePos(
                inputImage.HImage, rectangle.MeasureHandle,
                Sigma, EdgeThreshold, GetTransitionString(), GetSelectionString(),
                out HTuple rowEdge, out HTuple columnEdge,
                out HTuple amplitude, out HTuple distance);

            // 处理检测结果
            for (int i = 0; i < rowEdge.Length; i++)
            {
                var edge = new CaliperData.EdgeResult
                {
                    Row = rowEdge[i].D,
                    Column = columnEdge[i].D,
                    Amplitude = amplitude[i].D,
                    Distance = distance.Length > i ? distance[i].D : 0,
                    MeasureIndex = rectangle.Index,
                    EdgeType = Transition,
                    Quality = CalculateEdgeQuality(amplitude[i].D),
                    IsValid = amplitude[i].D >= MinAmplitude
                };

                edges.Add(edge);
            }

            return edges;
        }

        /// <summary>
        /// 边缘对测量
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="rectangle">测量矩形</param>
        /// <returns>边缘结果列表</returns>
        private List<CaliperData.EdgeResult> MeasureEdgePairs(
            VisionImage inputImage, CaliperData.MeasureRectangle rectangle)
        {
            var edges = new List<CaliperData.EdgeResult>();

            // 调用Halcon MeasurePairs算子
            HOperatorSet.MeasurePairs(
                inputImage.HImage, rectangle.MeasureHandle,
                Sigma, EdgeThreshold, GetTransitionString(), GetSelectionString(),
                out HTuple rowFirst, out HTuple columnFirst, out HTuple amplitudeFirst,
                out HTuple rowSecond, out HTuple columnSecond, out HTuple amplitudeSecond,
                out HTuple intraDistance, out HTuple interDistance);

            // 处理第一组边缘
            for (int i = 0; i < rowFirst.Length; i++)
            {
                var edge = new CaliperData.EdgeResult
                {
                    Row = rowFirst[i].D,
                    Column = columnFirst[i].D,
                    Amplitude = amplitudeFirst[i].D,
                    Distance = intraDistance.Length > i ? intraDistance[i].D : 0,
                    MeasureIndex = rectangle.Index,
                    EdgeType = EdgeTransition.Positive,
                    Quality = CalculateEdgeQuality(amplitudeFirst[i].D),
                    IsValid = amplitudeFirst[i].D >= MinAmplitude
                };
                edges.Add(edge);
            }

            // 处理第二组边缘
            for (int i = 0; i < rowSecond.Length; i++)
            {
                var edge = new CaliperData.EdgeResult
                {
                    Row = rowSecond[i].D,
                    Column = columnSecond[i].D,
                    Amplitude = amplitudeSecond[i].D,
                    Distance = intraDistance.Length > i ? intraDistance[i].D : 0,
                    MeasureIndex = rectangle.Index,
                    EdgeType = EdgeTransition.Negative,
                    Quality = CalculateEdgeQuality(amplitudeSecond[i].D),
                    IsValid = amplitudeSecond[i].D >= MinAmplitude
                };
                edges.Add(edge);
            }

            return edges;
        }

        /// <summary>
        /// 过滤边缘结果
        /// </summary>
        /// <param name="edgeResults">原始边缘结果</param>
        /// <returns>过滤后的边缘结果</returns>
        private List<CaliperData.EdgeResult> FilterEdgeResults(List<CaliperData.EdgeResult> edgeResults)
        {
            // 按强度过滤
            var filtered = edgeResults.Where(e => e.Amplitude >= MinAmplitude).ToList();

            // 根据选择策略进一步过滤
            if (Selection != EdgeSelection.All)
            {
                var groupedByMeasure = filtered.GroupBy(e => e.MeasureIndex);
                filtered = new List<CaliperData.EdgeResult>();

                foreach (var group in groupedByMeasure)
                {
                    var edges = group.ToList();
                    switch (Selection)
                    {
                        case EdgeSelection.First:
                            if (edges.Count > 0)
                                filtered.Add(edges.First());
                            break;
                        case EdgeSelection.Last:
                            if (edges.Count > 0)
                                filtered.Add(edges.Last());
                            break;
                        case EdgeSelection.Strongest:
                            var strongest = edges.OrderByDescending(e => e.Amplitude).FirstOrDefault();
                            if (strongest != null)
                                filtered.Add(strongest);
                            break;
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// 创建结果可视化
        /// </summary>
        /// <param name="edgeResults">边缘结果</param>
        /// <param name="measureRectangles">测量矩形</param>
        /// <returns>几何元素列表</returns>
        private List<GeometryElement> CreateResultVisualization(
            List<CaliperData.EdgeResult> edgeResults,
            List<CaliperData.MeasureRectangle> measureRectangles)
        {
            var elements = new List<GeometryElement>();

            // 添加测量矩形可视化
            if (ShowMeasureRectangles)
            {
                foreach (var rect in measureRectangles)
                {
                    var corners = rect.GetCornerPoints();
                    if (corners.Count == 4)
                    {
                        // 创建矩形的四条边
                        for (int i = 0; i < 4; i++)
                        {
                            var start = corners[i];
                            var end = corners[(i + 1) % 4];
                            
                            elements.Add(new LineElement(start.Y, start.X, end.Y, end.X)
                            {
                                Name = $"测量矩形_{rect.Index}",
                                Color = Colors.Blue,
                                LineWidth = 1.0
                            });
                        }
                    }
                }
            }

            // 添加边缘点可视化
            if (ShowEdgePoints)
            {
                foreach (var edge in edgeResults)
                {
                    var element = edge.ToGeometryElement();
                    element.Color = edge.IsValid ? Colors.Red : Colors.Gray;
                    element.Size = edge.IsValid ? 3.0 : 2.0;
                    elements.Add(element);
                }
            }

            return elements;
        }

        /// <summary>
        /// 创建测量结果数据
        /// </summary>
        /// <param name="edgeResults">边缘结果</param>
        /// <param name="measureRectangles">测量矩形</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量数据字典</returns>
        private Dictionary<string, object> CreateMeasurements(
            List<CaliperData.EdgeResult> edgeResults,
            List<CaliperData.MeasureRectangle> measureRectangles,
            TimeSpan processingTime)
        {
            var validEdges = edgeResults.Where(e => e.IsValid).ToList();
            
            return new Dictionary<string, object>
            {
                ["TotalEdges"] = edgeResults.Count,
                ["ValidEdges"] = validEdges.Count,
                ["InvalidEdges"] = edgeResults.Count - validEdges.Count,
                ["MeasureRectangles"] = measureRectangles.Count,
                ["AverageAmplitude"] = validEdges.Count > 0 ? validEdges.Average(e => e.Amplitude) : 0,
                ["MaxAmplitude"] = validEdges.Count > 0 ? validEdges.Max(e => e.Amplitude) : 0,
                ["MinAmplitude"] = validEdges.Count > 0 ? validEdges.Min(e => e.Amplitude) : 0,
                ["AverageQuality"] = validEdges.Count > 0 ? validEdges.Average(e => e.Quality) : 0,
                ["ProcessingTime"] = processingTime.TotalMilliseconds,
                ["ROI_Type"] = "HDrawingObject",
                ["MeasureMode"] = Mode.ToString(),
                ["EdgeTransition"] = Transition.ToString()
            };
        }

        /// <summary>
        /// 获取边缘极性字符串
        /// </summary>
        /// <returns>Halcon边缘极性参数</returns>
        private string GetTransitionString()
        {
            return Transition switch
            {
                EdgeTransition.Positive => "positive",
                EdgeTransition.Negative => "negative",
                EdgeTransition.All => "all",
                _ => "all"
            };
        }

        /// <summary>
        /// 获取选择策略字符串
        /// </summary>
        /// <returns>Halcon选择策略参数</returns>
        private string GetSelectionString()
        {
            return Selection switch
            {
                EdgeSelection.All => "all",
                EdgeSelection.First => "first",
                EdgeSelection.Last => "last",
                EdgeSelection.Strongest => "all", // Halcon中没有直接的strongest，需要后处理
                _ => "all"
            };
        }

        /// <summary>
        /// 计算边缘质量评分
        /// </summary>
        /// <param name="amplitude">边缘强度</param>
        /// <returns>质量评分（0-100）</returns>
        private double CalculateEdgeQuality(double amplitude)
        {
            // 基于边缘强度计算质量评分
            double normalizedAmplitude = Math.Min(amplitude / EdgeThreshold, 10.0);
            return Math.Min(100, normalizedAmplitude * 10);
        }

        /// <summary>
        /// 验证处理器参数
        /// </summary>
        /// <returns>验证结果</returns>
        protected override bool ValidateParameters()
        {
            if (CaliperCount != 1)
                return false;
            
            if (Length1 <= 0 || Length2 <= 0)
                return false;
                
            if (CenterRow < 0 || CenterCol < 0)
                return false;
            
            if (EdgeThreshold <= 0)
                return false;
            
            if (Sigma <= 0)
                return false;

            return true;
        }

        #endregion
    }
}