using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;
using HalconDotNet;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 九点标定处理器
    /// </summary>
    public class NinePointCalibrationProcessor : VisionProcessorBase
    {
        public override string ProcessorName => "九点标定";
        public override string Category => "标定工具";

        #region 参数配置

        [Parameter("标定名称", "标定配置的名称", Order = 1, Group = "基础设置")]
        public string CalibrationName { get; set; } = "九点标定";

        [Parameter("物理单位", "物理坐标的单位", Order = 2, Group = "基础设置")]
        public PhysicalUnit Unit { get; set; } = PhysicalUnit.Millimeter;

        // 固定使用仿射变换，无需用户选择

        [Parameter("最小标定点数", "执行标定所需的最少点数", Order = 4, Group = "算法设置", MinValue = 4, MaxValue = 9)]
        public int MinimumPoints { get; set; } = 6;

        [Parameter("误差阈值(mm)", "可接受的最大标定误差", Order = 5, Group = "质量控制", MinValue = 0.01, MaxValue = 10.0)]
        public double ErrorThreshold { get; set; } = 1.0;

        #endregion

        /// <summary>当前标定数据</summary>
        public NinePointCalibrationData CurrentCalibration { get; set; } = new NinePointCalibrationData();

        /// <summary>
        /// 执行标定处理
        /// </summary>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            return await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    // 验证输入图像
                    if (inputImage == null)
                    {
                        System.Diagnostics.Debug.WriteLine("输入图像为null，使用虚拟图像进行标定");
                        // 创建一个虚拟图像用于标定计算（九点标定不依赖实际图像内容）
                        inputImage = CreateDummyImage();
                    }
                    // 执行标定计算
                    var calibrationResult = PerformCalibration();
                    if (!calibrationResult.Success)
                    {
                        return CreateFailureResult(calibrationResult.ErrorMessage);
                    }

                    // 创建测量数据
                    var measurements = CreateMeasurements(calibrationResult);

                    stopwatch.Stop();
                    var processResult = CreateSuccessResult(inputImage.Clone(), stopwatch.Elapsed, measurements);
                    processResult.AddMetadata("CalibrationData", CurrentCalibration);
                    
                    return processResult;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    return CreateFailureResult($"标定失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 添加标定点对
        /// </summary>
        public bool AddCalibrationPoint(string pointName, Point2D imagePoint, Point2D worldPoint)
        {
            var existingPoint = CurrentCalibration.PointPairs.FirstOrDefault(p => p.PointName == pointName);
            if (existingPoint != null)
            {
                existingPoint.ImagePoint = imagePoint;
                existingPoint.WorldPoint = worldPoint;
                existingPoint.IsSet = true;
                existingPoint.CreatedTime = DateTime.Now;
            }
            else
            {
                CurrentCalibration.PointPairs.Add(new CalibrationPointPair(pointName)
                {
                    ImagePoint = imagePoint,
                    WorldPoint = worldPoint,
                    IsSet = true
                });
            }
            
            return CurrentCalibration.IsDataComplete(MinimumPoints);
        }

        /// <summary>
        /// 清除所有标定点
        /// </summary>
        public void ClearCalibrationPoints()
        {
            CurrentCalibration.PointPairs.Clear();
            CurrentCalibration.IsValid = false;
        }

        /// <summary>
        /// 执行标定计算的核心方法
        /// </summary>
        private CalibrationResult PerformCalibration()
        {
            var result = new CalibrationResult();
            
            // 检查标定点数量
            if (!CurrentCalibration.IsDataComplete(MinimumPoints))
            {
                result.ErrorMessage = $"标定点数量不足，需要至少{MinimumPoints}个点，当前只有{CurrentCalibration.ValidPointCount}个";
                return result;
            }

            try
            {
                // 计算仿射变换矩阵（Halcon VectorToHomMat2d实际返回仿射变换）
                bool success = TransformCalculator.ComputeHomographyMatrix(
                    CurrentCalibration.PointPairs,
                    out double[,] transformMatrix,
                    out double[,] inverseMatrix);
                
                if (success)
                {
                    CurrentCalibration.TransformMatrix = transformMatrix;
                    CurrentCalibration.InverseTransformMatrix = inverseMatrix;
                }

                if (!success)
                {
                    result.ErrorMessage = GetDetailedErrorMessage(CurrentCalibration.PointPairs);
                    return result;
                }

                // 计算标定误差
                CurrentCalibration.CalibrationError = TransformCalculator.CalculateCalibrationError(
                    CurrentCalibration.PointPairs, CurrentCalibration.TransformMatrix);
                
                // 计算最大单点误差
                CurrentCalibration.MaxPointError = CurrentCalibration.PointPairs
                    .Where(p => p.IsSet)
                    .Max(p => p.FitError);

                // 计算像素比例因子
                CalculatePixelScale();

                // 评估标定质量
                CurrentCalibration.Quality = TransformCalculator.EvaluateQuality(
                    CurrentCalibration.CalibrationError, CurrentCalibration.MaxPointError);

                // 更新标定信息
                CurrentCalibration.IsValid = CurrentCalibration.CalibrationError < ErrorThreshold;
                CurrentCalibration.CalibrationTime = DateTime.Now;
                CurrentCalibration.Name = CalibrationName;
                CurrentCalibration.TransformType = "仿射变换";
                CurrentCalibration.Unit = Unit;

                result.Success = CurrentCalibration.IsValid;
                if (!result.Success)
                {
                    result.ErrorMessage = $"标定误差({CurrentCalibration.CalibrationError:F3}mm)超过阈值({ErrorThreshold}mm)";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"标定计算异常: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 计算像素比例因子
        /// </summary>
        private void CalculatePixelScale()
        {
            var validPairs = CurrentCalibration.PointPairs.Where(p => p.IsSet).ToList();
            if (validPairs.Count < 2)
            {
                CurrentCalibration.PixelScale = 1.0;
                return;
            }

            var scales = new List<double>();
            
            // 计算各对点之间的像素比例
            for (int i = 0; i < validPairs.Count - 1; i++)
            {
                for (int j = i + 1; j < validPairs.Count; j++)
                {
                    var pair1 = validPairs[i];
                    var pair2 = validPairs[j];
                    
                    var imageDistance = pair1.ImagePoint.DistanceTo(pair2.ImagePoint);
                    var worldDistance = pair1.WorldPoint.DistanceTo(pair2.WorldPoint);
                    
                    if (worldDistance > 0.01) // 避免除零
                    {
                        scales.Add(imageDistance / worldDistance);
                    }
                }
            }
            
            CurrentCalibration.PixelScale = scales.Any() ? scales.Average() : 1.0;
        }

        /// <summary>
        /// 创建测量数据
        /// </summary>
        private Dictionary<string, object> CreateMeasurements(CalibrationResult calibrationResult)
        {
            var measurements = new Dictionary<string, object>();
            
            // 基础信息
            measurements["标定名称"] = CalibrationName;
            measurements["标定时间"] = CurrentCalibration.CalibrationTime.ToString("yyyy-MM-dd HH:mm:ss");
            measurements["变换类型"] = GetTransformTypeDescription();
            measurements["物理单位"] = GetUnitDescription();
            
            // 标定点信息
            measurements["标定点数量"] = CurrentCalibration.ValidPointCount;
            measurements["最小点数要求"] = MinimumPoints;
            
            // 标定结果
            measurements["标定成功"] = calibrationResult.Success ? "是" : "否";
            if (calibrationResult.Success)
            {
                measurements["标定误差(mm)"] = Math.Round(CurrentCalibration.CalibrationError, 3);
                measurements["最大单点误差(mm)"] = Math.Round(CurrentCalibration.MaxPointError, 3);
                measurements["像素比例(像素/mm)"] = Math.Round(CurrentCalibration.PixelScale, 3);
                measurements["标定质量"] = GetQualityDescription();
                measurements["变换矩阵"] = CurrentCalibration.GetTransformMatrixString();
            }
            else
            {
                measurements["错误信息"] = calibrationResult.ErrorMessage;
            }
            
            return measurements;
        }

        #region 辅助方法

        private string GetTransformTypeDescription()
        {
            return "仿射变换";
        }

        private string GetUnitDescription()
        {
            return Unit switch
            {
                PhysicalUnit.Millimeter => "毫米",
                PhysicalUnit.Micrometer => "微米",
                PhysicalUnit.Inch => "英寸",
                PhysicalUnit.Pixel => "像素",
                _ => "未知"
            };
        }

        private string GetQualityDescription()
        {
            return CurrentCalibration.Quality switch
            {
                CalibrationQuality.Excellent => "优秀",
                CalibrationQuality.Good => "良好",
                CalibrationQuality.Acceptable => "可接受",
                CalibrationQuality.Poor => "较差",
                CalibrationQuality.Unusable => "不可用",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取详细的错误信息
        /// </summary>
        private string GetDetailedErrorMessage(List<CalibrationPointPair> pointPairs)
        {
            var validPairs = pointPairs.Where(p => p.IsSet).ToList();
            
            if (validPairs.Count < 4)
            {
                return $"标定点数量不足，至少需要4个点，当前只有{validPairs.Count}个";
            }

            // 检查重复点
            for (int i = 0; i < validPairs.Count - 1; i++)
            {
                for (int j = i + 1; j < validPairs.Count; j++)
                {
                    var imageDist = validPairs[i].ImagePoint.DistanceTo(validPairs[j].ImagePoint);
                    var worldDist = validPairs[i].WorldPoint.DistanceTo(validPairs[j].WorldPoint);
                    
                    if (imageDist < 1.0)
                    {
                        return $"点{validPairs[i].PointName}和{validPairs[j].PointName}的图像坐标过于接近 (距离: {imageDist:F2}像素)";
                    }
                    
                    if (worldDist < 0.001)
                    {
                        return $"点{validPairs[i].PointName}和{validPairs[j].PointName}的世界坐标过于接近 (距离: {worldDist:F3}mm)";
                    }
                }
            }

            // 检查共线性
            if (validPairs.Count >= 3)
            {
                bool allCollinear = true;
                for (int i = 0; i < validPairs.Count - 2 && allCollinear; i++)
                {
                    for (int j = i + 1; j < validPairs.Count - 1 && allCollinear; j++)
                    {
                        for (int k = j + 1; k < validPairs.Count && allCollinear; k++)
                        {
                            var p1 = validPairs[i].ImagePoint;
                            var p2 = validPairs[j].ImagePoint;
                            var p3 = validPairs[k].ImagePoint;
                            
                            double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
                            if (Math.Abs(cross) > 1e-6)
                            {
                                allCollinear = false;
                            }
                        }
                    }
                }
                
                if (allCollinear)
                {
                    return "所有标定点都在一条直线上，请重新选择分布更均匀的点位";
                }
            }

            return $"变换矩阵计算失败，请检查点位分布是否合理。当前数据适合仿射变换（平移、旋转、缩放、剪切），点位要分布均匀且不能共线";
        }

        /// <summary>
        /// 创建虚拟图像用于标定计算（九点标定不依赖实际图像内容）
        /// </summary>
        private VisionImage CreateDummyImage()
        {
            try
            {
                // 九点标定只需要坐标信息，不依赖实际图像内容
                // 使用Halcon创建一个简单的虚拟图像作为载体
                HOperatorSet.GenImageConst(out HObject dummyImage, "byte", 800, 600);
                return new VisionImage(dummyImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建虚拟图像失败: {ex.Message}");
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 标定结果
    /// </summary>
    public class CalibrationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}