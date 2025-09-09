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
    /// 圆形卡尺处理器
    /// 基于Halcon Metrology模型的高精度圆检测工具
    /// </summary>
    public class CircleCaliperProcessor : VisionProcessorBase
    {
        #region 属性定义

        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "圆形卡尺";

        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "测量工具";

        /// <summary>
        /// 圆心行坐标
        /// </summary>
        [Parameter("圆心Row", "预期圆心的行坐标", 0, 4096, Step = 0.1, Order = 1, Group = "位置参数")]
        public double CenterRow { get; set; } = 256;

        /// <summary>
        /// 圆心列坐标
        /// </summary>
        [Parameter("圆心Col", "预期圆心的列坐标", 0, 4096, Step = 0.1, Order = 2, Group = "位置参数")]
        public double CenterCol { get; set; } = 256;

        /// <summary>
        /// 预期半径
        /// </summary>
        [Parameter("预期半径", "圆的大致半径", 10, 1000, Step = 1, Order = 3, Group = "位置参数")]
        public double ExpectedRadius { get; set; } = 100;

        /// <summary>
        /// 半径容差
        /// </summary>
        [Parameter("半径容差", "半径搜索范围", 5, 200, Step = 1, Order = 4, Group = "位置参数")]
        public double RadiusTolerance { get; set; } = 30;

        /// <summary>
        /// 边缘阈值
        /// </summary>
        [Parameter("边缘阈值", "边缘检测的阈值", 1, 100, Step = 1, Order = 5, Group = "检测参数")]
        public double EdgeThreshold { get; set; } = 30;

        /// <summary>
        /// 测量长度
        /// </summary>
        [Parameter("测量长度", "每个测量点的长度", 10, 100, Step = 1, Order = 6, Group = "检测参数")]
        public double MeasureLength { get; set; } = 50;

        /// <summary>
        /// 测量距离
        /// </summary>
        [Parameter("测量距离", "相邻测量点间距", 1, 50, Step = 1, Order = 7, Group = "检测参数")]
        public double MeasureDistance { get; set; } = 10;

        /// <summary>
        /// 高斯平滑
        /// </summary>
        [Parameter("高斯平滑", "边缘检测前的高斯平滑参数", 0.5, 5.0, Step = 0.1, Order = 8, Group = "检测参数")]
        public double Sigma { get; set; } = 1.0;

        /// <summary>
        /// 边缘极性
        /// </summary>
        [Parameter("边缘极性", "边缘检测的极性", Order = 9, Group = "检测参数")]
        public EdgeTransition Transition { get; set; } = EdgeTransition.All;

        /// <summary>
        /// 选择策略
        /// </summary>
        [Parameter("选择策略", "边缘点选择策略", Order = 10, Group = "检测参数")]
        public EdgeSelection Selection { get; set; } = EdgeSelection.All;

        /// <summary>
        /// 最小分数
        /// </summary>
        [Parameter("最小分数", "拟合质量最小阈值", 0.1, 1.0, Step = 0.1, Order = 11, Group = "高级参数", IsAdvanced = true)]
        public double MinScore { get; set; } = 0.5;

        /// <summary>
        /// 异常点抑制
        /// </summary>
        [Parameter("异常点抑制", "是否启用异常点过滤", Order = 12, Group = "高级参数", IsAdvanced = true)]
        public bool OutlierSuppression { get; set; } = true;

        /// <summary>
        /// 显示测量点
        /// </summary>
        [Parameter("显示测量点", "是否显示测量点", Order = 13, Group = "显示选项")]
        public bool ShowMeasurePoints { get; set; } = true;

        /// <summary>
        /// 显示拟合圆
        /// </summary>
        [Parameter("显示拟合圆", "是否显示拟合的圆", Order = 14, Group = "显示选项")]
        public bool ShowFittedCircle { get; set; } = true;

        #endregion

        #region 核心处理方法

        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            HTuple metrologyHandle = null;

            try
            {
                // 1. 参数验证
                if (!ValidateParameters())
                {
                    return CreateFailureResult("参数验证失败，请检查参数设置");
                }

                // 2. 执行圆检测
                var result = await Task.Run(() => ExecuteCircleDetection(inputImage, out metrologyHandle));
                
                stopwatch.Stop();

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateFailureResult($"圆形卡尺测量失败: {ex.Message}", ex);
            }
            finally
            {
                metrologyHandle?.Dispose();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行圆检测
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="metrologyHandle">Metrology句柄</param>
        /// <returns>处理结果</returns>
        private ProcessResult ExecuteCircleDetection(VisionImage inputImage, out HTuple metrologyHandle)
        {
            metrologyHandle = null;
            
            try
            {
                // 步骤1：创建计量模型
                HOperatorSet.CreateMetrologyModel(out metrologyHandle);

                // 步骤2：添加圆形计量对象（使用generic方法）
                HOperatorSet.AddMetrologyObjectGeneric(
                    metrologyHandle,
                    new HTuple("circle"),           // 几何类型
                    new HTuple(new double[] { CenterRow, CenterCol, ExpectedRadius }), // 圆参数[row, col, radius]
                    new HTuple(RadiusTolerance),    // 半径容差
                    MeasureLength,                  // 测量长度
                    MeasureDistance,                // 测量距离
                    1.0,                           // 完整圆形（360度）
                    new HTuple("measure_transition"), new HTuple(GetTransitionString()),
                    out HTuple objectIndex);

                // 步骤3：设置详细参数
                SetMetrologyParameters(metrologyHandle, objectIndex);

                // 步骤4：执行测量
                HOperatorSet.ApplyMetrologyModel(inputImage.HImage, metrologyHandle);

                // 步骤5：获取测量结果
                var measureResult = GetMeasurementResults(metrologyHandle, objectIndex);
                
                // 步骤6：创建输出图像
                var outputImage = inputImage.Clone();

                // 步骤7：获取Halcon显示轮廓
                var displayContours = GetHalconDisplayContours(metrologyHandle, objectIndex, measureResult);

                // 步骤8：创建测量数据
                var measurements = CreateMeasurements(measureResult);

                // 步骤9：创建成功结果并保存轮廓数据
                var result = CreateSuccessResult(outputImage, System.TimeSpan.FromMilliseconds(0), measurements);
                
                // 将Halcon轮廓数据保存到结果中（通过Metadata）
                result.AddMetadata("HalconDisplayContours", displayContours);

                return result;
            }
            catch (Exception ex)
            {
                return CreateFailureResult($"圆检测执行失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置Metrology参数
        /// </summary>
        /// <param name="metrologyHandle">Metrology句柄</param>
        /// <param name="objectIndex">对象索引</param>
        private void SetMetrologyParameters(HTuple metrologyHandle, HTuple objectIndex)
        {
            try
            {
                // 边缘检测阈值
                HOperatorSet.SetMetrologyObjectParam(metrologyHandle, objectIndex,
                    "measure_threshold", new HTuple(EdgeThreshold));

                // 高斯平滑参数
                HOperatorSet.SetMetrologyObjectParam(metrologyHandle, objectIndex,
                    "measure_sigma", new HTuple(Sigma));

                // 边缘选择策略
                HOperatorSet.SetMetrologyObjectParam(metrologyHandle, objectIndex,
                    "measure_select", new HTuple(GetSelectionString()));

                // 最小分数阈值
                HOperatorSet.SetMetrologyObjectParam(metrologyHandle, objectIndex,
                    "min_score", new HTuple(MinScore));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置Metrology参数时发生错误: {ex.Message}");
                throw new Exception($"计量模型参数设置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取测量结果
        /// </summary>
        /// <param name="metrologyHandle">Metrology句柄</param>
        /// <param name="objectIndex">对象索引</param>
        /// <returns>圆测量结果</returns>
        private CircleMeasureResult GetMeasurementResults(HTuple metrologyHandle, HTuple objectIndex)
        {
            try
            {
                // 获取拟合参数
                HOperatorSet.GetMetrologyObjectResult(metrologyHandle, objectIndex,
                    "all", "result_type", "all_param",
                    out HTuple parameter);

                if (parameter.Length < 3)
                {
                    return new CircleMeasureResult { IsValid = false, ErrorMessage = "未找到足够的圆参数" };
                }

                // parameter[0] = Row, parameter[1] = Column, parameter[2] = Radius
                double centerRow = parameter[0].D;
                double centerCol = parameter[1].D;
                double radius = parameter[2].D;

                // 获取测量点数据
                HOperatorSet.GetMetrologyObjectMeasures(out HObject measureContour, metrologyHandle, objectIndex,
                    "all", out HTuple rowCoords, out HTuple colCoords);
                measureContour?.Dispose();

                // 获取拟合质量
                HOperatorSet.GetMetrologyObjectResult(metrologyHandle, objectIndex,
                    "all", "result_type", "score",
                    out HTuple scores);

                return new CircleMeasureResult
                {
                    IsValid = true,
                    CenterRow = centerRow,
                    CenterCol = centerCol,
                    Radius = radius,
                    Score = scores.Length > 0 ? scores[0].D : 0,
                    MeasurePointsRow = rowCoords.DArr,
                    MeasurePointsCol = colCoords.DArr,
                    TotalPoints = rowCoords.Length,
                    ValidPoints = rowCoords.Length
                };
            }
            catch (Exception ex)
            {
                return new CircleMeasureResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 获取Halcon原生显示轮廓
        /// </summary>
        /// <param name="metrologyHandle">Metrology句柄</param>
        /// <param name="objectIndex">对象索引</param>
        /// <param name="result">测量结果</param>
        /// <returns>显示轮廓数据</returns>
        private HalconDisplayContours GetHalconDisplayContours(HTuple metrologyHandle, HTuple objectIndex, CircleMeasureResult result)
        {
            var displayData = new HalconDisplayContours();

            if (!result.IsValid) return displayData;

            try
            {
                // 获取模型轮廓（蓝色圆圈）
                if (ShowFittedCircle)
                {
                    HOperatorSet.GetMetrologyObjectModelContour(
                        out HObject modelContour,
                        metrologyHandle,
                        objectIndex,
                        1.5); // 轮廓分辨率
                        
                    displayData.ModelContour = modelContour;
                }

                // 获取测量轮廓（卡尺线）
                if (ShowMeasurePoints)
                {
                    HOperatorSet.GetMetrologyObjectMeasures(
                        out HObject measureContours,
                        metrologyHandle,
                        objectIndex,
                        "all",
                        out HTuple row, out HTuple column);
                        
                    displayData.MeasureContours = measureContours;
                    
                    // 调试信息
                    System.Diagnostics.Debug.WriteLine($"获取测量轮廓成功，测量点数量: {row?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取Halcon轮廓失败: {ex.Message}");
            }

            return displayData;
        }


        /// <summary>
        /// 创建测量数据
        /// </summary>
        /// <param name="result">测量结果</param>
        /// <returns>测量数据字典</returns>
        private Dictionary<string, object> CreateMeasurements(CircleMeasureResult result)
        {
            return new Dictionary<string, object>
            {
                ["IsValid"] = result.IsValid,
                ["CenterRow"] = result.CenterRow,
                ["CenterCol"] = result.CenterCol,
                ["Radius"] = result.Radius,
                ["Diameter"] = result.Radius * 2.0,
                ["Score"] = result.Score,
                ["TotalPoints"] = result.TotalPoints,
                ["ValidPoints"] = result.ValidPoints,
                ["CircleType"] = "Circle",
                ["ErrorMessage"] = result.ErrorMessage ?? ""
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
        /// 验证处理器参数
        /// </summary>
        /// <returns>验证结果</returns>
        protected override bool ValidateParameters()
        {
            if (ExpectedRadius <= 0)
                return false;

            if (RadiusTolerance <= 0)
                return false;

            if (MeasureLength <= 0 || MeasureDistance <= 0)
                return false;

            if (EdgeThreshold <= 0)
                return false;

            if (Sigma <= 0)
                return false;

            if (CenterRow < 0 || CenterCol < 0)
                return false;

            return true;
        }

        #endregion
    }

    /// <summary>
    /// 圆测量结果数据结构
    /// </summary>
    public class CircleMeasureResult
    {
        public bool IsValid { get; set; }
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Radius { get; set; }
        public double Score { get; set; }
        public double[] MeasurePointsRow { get; set; }
        public double[] MeasurePointsCol { get; set; }
        public int TotalPoints { get; set; }
        public int ValidPoints { get; set; }
        public string ErrorMessage { get; set; }
    }
}