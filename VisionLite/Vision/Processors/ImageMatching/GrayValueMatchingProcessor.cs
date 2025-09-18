using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.ImageMatching.Models;
using VisionLite.Vision.Calibration.NinePoint.Core;

namespace VisionLite.Vision.Processors.ImageMatching
{
    /// <summary>
    /// 灰度匹配处理器
    /// 使用归一化互相关(NCC)算法进行灰度模板匹配
    /// </summary>
    public class GrayValueMatchingProcessor : ImageMatchingProcessorBase
    {
        public override string ProcessorName => "灰度匹配";

        #region 参数定义

        [Parameter("角度范围最小值", "搜索的最小旋转角度(度)", Order = 10, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MinAngle { get; set; } = -10;

        [Parameter("角度范围最大值", "搜索的最大旋转角度(度)", Order = 11, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MaxAngle { get; set; } = 10;

        [Parameter("最小匹配得分", "接受匹配结果的最小分数(0-1)", Order = 12, Group = "搜索参数", MinValue = 0.1, MaxValue = 1.0)]
        public double MinScore { get; set; } = 0.5;

        [Parameter("子像素精度", "是否启用子像素精度定位", Order = 20, Group = "精度设置")]
        public bool SubpixelAccuracy { get; set; } = true;


        #endregion

        #region Halcon算法流程

        protected override bool CreateTemplate(VisionImage inputImage)
        {
            try
            {
                ReleaseModel();

                if (TemplateROI == null)
                {
                    return false;
                }

                HObject templateRegion, templateImage;
                
                var success = ExtractTemplateFromROI(inputImage.HImage, TemplateROI,
                    out templateRegion, out templateImage);

                if (!success)
                {
                    return false;
                }

                // 计算正确的角度参数：angleStart是起始角度，angleExtent是角度范围跨度
                double angleStartRad = MinAngle * Math.PI / 180;
                double angleExtentRad = (MaxAngle - MinAngle) * Math.PI / 180;

                HTuple modelHandle;
                HOperatorSet.CreateNccModel(templateImage, "auto",
                    angleStartRad, angleExtentRad,
                    "auto", "ignore_global_polarity", out modelHandle);

                System.Diagnostics.Debug.WriteLine($"创建NCC模型 - 角度范围: {MinAngle}°到{MaxAngle}° (跨度{MaxAngle - MinAngle}°)");
                ModelHandle = modelHandle;

                templateRegion?.Dispose();
                templateImage?.Dispose();

                NeedUpdateTemplate = false;
                return ModelHandle != null && ModelHandle.Length > 0;
            }
            catch (Exception ex)
            {
                // 检查是否是彩色图像导致的错误
                if (ex.Message.Contains("3359") || ex.Message.Contains("Wrong number of image channels"))
                {
                    System.Diagnostics.Debug.WriteLine($"创建NCC模板失败: 输入图像为彩色图像，灰度匹配只支持灰度图像。请使用灰度图像或先转换为灰度图像。错误详情: {ex.Message}");
                    throw new InvalidOperationException("灰度匹配算法只支持灰度图像。当前输入的是彩色图像，请先将图像转换为灰度图像后再使用本算法，或选择其他支持彩色图像的匹配算法。");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"创建NCC模板失败: {ex.Message}");
                    throw new InvalidOperationException($"模板创建失败: {ex.Message}");
                }
            }
        }

        protected override List<MatchInstance> ExecuteMatching(VisionImage inputImage)
        {
            var results = new List<MatchInstance>();

            try
            {
                if (ModelHandle == null || ModelHandle.Length == 0)
                {
                    return results;
                }

                HObject searchImage = inputImage.HImage;
                if (SearchROI != null)
                {
                    HOperatorSet.ReduceDomain(inputImage.HImage,
                        CreateHalconRegionFromROI(SearchROI), out searchImage);
                }

                // 计算正确的角度参数：与CreateNccModel保持一致
                double angleStartRad = MinAngle * Math.PI / 180;
                double angleExtentRad = (MaxAngle - MinAngle) * Math.PI / 180;

                HOperatorSet.FindNccModel(searchImage, ModelHandle,
                    angleStartRad, angleExtentRad,
                    MinScore, MaxMatches, MaxOverlap, SubpixelAccuracy ? "true" : "false", 0,
                    out HTuple rows, out HTuple columns, out HTuple angles, out HTuple scores);

                System.Diagnostics.Debug.WriteLine($"NCC搜索 - 角度范围: {MinAngle}°到{MaxAngle}°，找到{rows.Length}个匹配");

                for (int i = 0; i < rows.Length; i++)
                {
                    var instance = new MatchInstance
                    {
                        Row = rows[i].D,
                        Column = columns[i].D,
                        Angle = angles[i].D * 180 / Math.PI,
                        Scale = 1.0,
                        Score = scores[i].D
                    };

                    if (ShowMatchContours)
                    {
                        instance.ContourPoints = GetNccModelContour(instance);
                    }

                    results.Add(instance);
                }

                if (SearchROI != null)
                {
                    searchImage?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NCC匹配执行失败: {ex.Message}");
            }

            return results;
        }

        protected override void DisposeModel()
        {
            if (ModelHandle != null && ModelHandle.Length > 0)
            {
                HOperatorSet.ClearNccModel(ModelHandle);
            }
        }

        #endregion

        #region 辅助方法

        private Point2D[] GetNccModelContour(MatchInstance instance)
        {
            if (TemplateROI == null) return new Point2D[0];

            var rect = GetBoundingRectangleFromROI(TemplateROI);

            // 计算模板ROI的中心（NCC模型的参考点）
            var templateCenterRow = (rect.Row1 + rect.Row2) / 2.0;
            var templateCenterCol = (rect.Col1 + rect.Col2) / 2.0;

            // 匹配结果的位置和角度
            var matchRow = instance.Row;
            var matchCol = instance.Column;
            var angle = instance.Angle * Math.PI / 180;

            var points = new Point2D[5];
            // 计算模板矩形各角点相对于模板中心的偏移
            var corners = new[]
            {
                new Point2D(rect.Col1 - templateCenterCol, rect.Row1 - templateCenterRow),
                new Point2D(rect.Col2 - templateCenterCol, rect.Row1 - templateCenterRow),
                new Point2D(rect.Col2 - templateCenterCol, rect.Row2 - templateCenterRow),
                new Point2D(rect.Col1 - templateCenterCol, rect.Row2 - templateCenterRow),
                new Point2D(rect.Col1 - templateCenterCol, rect.Row1 - templateCenterRow)  // 闭合矩形
            };

            for (int i = 0; i < corners.Length; i++)
            {
                // 应用旋转变换：corners[i].X是列偏移，corners[i].Y是行偏移
                var rotatedCol = corners[i].X * Math.Cos(angle) - corners[i].Y * Math.Sin(angle);
                var rotatedRow = corners[i].X * Math.Sin(angle) + corners[i].Y * Math.Cos(angle);

                // 平移到匹配结果位置：Point2D(X, Y) = Point2D(Column, Row)
                points[i] = new Point2D(matchCol + rotatedCol, matchRow + rotatedRow);
            }

            System.Diagnostics.Debug.WriteLine($"生成NCC轮廓: 模板中心({templateCenterCol:F1},{templateCenterRow:F1}) -> 匹配位置({matchCol:F1},{matchRow:F1}), 角度{instance.Angle:F1}°");
            return points;
        }

        private BoundingRectangle GetBoundingRectangleFromROI(CaliperData.ROIGeometry roi)
        {
            if (roi?.Parameters == null)
                throw new ArgumentException("ROI参数无效");

            switch (roi.RoiType)
            {
                case "rectangle1":
                    return new BoundingRectangle
                    {
                        Row1 = roi.Parameters["Row1"],
                        Col1 = roi.Parameters["Col1"],
                        Row2 = roi.Parameters["Row2"],
                        Col2 = roi.Parameters["Col2"]
                    };
                case "rectangle2":
                    var row = roi.Parameters["Row"];
                    var col = roi.Parameters["Column"];
                    var length1 = roi.Parameters["Length1"];
                    var length2 = roi.Parameters["Length2"];
                    return new BoundingRectangle
                    {
                        Row1 = row - length2,
                        Col1 = col - length1,
                        Row2 = row + length2,
                        Col2 = col + length1
                    };
                default:
                    throw new NotSupportedException($"不支持的ROI类型: {roi.RoiType}");
            }
        }

        #endregion

        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (NeedUpdateTemplate || ModelHandle == null || ModelHandle.Length == 0)
                {
                    try
                    {
                        if (!CreateTemplate(inputImage))
                        {
                            return CreateFailureResult("模板创建失败，请检查模板ROI设置");
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 如果是彩色图像错误，直接传递详细的错误信息
                        return CreateFailureResult(ex.Message);
                    }
                }

                var matchResults = ExecuteMatching(inputImage);
                var measurements = CreateMeasurements(matchResults);

                stopwatch.Stop();
                var result = CreateSuccessResult(inputImage.Clone(), stopwatch.Elapsed, measurements);

                if (ShowMatchContours && matchResults.Count > 0)
                {
                    var displayContours = CreateDisplayContours(matchResults);
                    result.AddMetadata("HalconDisplayContours", displayContours);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateFailureResult($"灰度匹配失败: {ex.Message}");
            }
        }
    }
}