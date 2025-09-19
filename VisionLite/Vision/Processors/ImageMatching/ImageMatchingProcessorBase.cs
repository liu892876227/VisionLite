using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.ImageMatching.Models;
using VisionLite.Vision.Calibration.NinePoint.Core;

namespace VisionLite.Vision.Processors.ImageMatching
{
    /// <summary>
    /// 图像匹配处理器基类
    /// </summary>
    public abstract class ImageMatchingProcessorBase : VisionProcessorBase
    {
        public override string Category => "图像匹配";

        /// <summary>模板ROI区域</summary>
        public CaliperData.ROIGeometry TemplateROI { get; set; }

        /// <summary>搜索区域</summary>
        public CaliperData.ROIGeometry SearchROI { get; set; }

        /// <summary>最大匹配数量</summary>
        [Parameter("最大匹配数量", "返回的最大匹配结果数", Order = 50, Group = "搜索参数", MinValue = 1, MaxValue = 100)]
        public int MaxMatches { get; set; } = 10;

        /// <summary>重叠阈值</summary>
        [Parameter("重叠阈值", "结果去重的重叠阈值(0-1)", Order = 51, Group = "搜索参数", MinValue = 0, MaxValue = 1.0)]
        public double MaxOverlap { get; set; } = 0.1;

        /// <summary>显示匹配结果</summary>
        [Parameter("显示匹配轮廓", "是否显示匹配结果的轮廓", Order = 60, Group = "显示设置")]
        public bool ShowMatchContours { get; set; } = true;

        /// <summary>Halcon模型句柄</summary>
        protected HTuple ModelHandle { get; set; } = new HTuple();

        /// <summary>是否需要重新创建模板</summary>
        public bool NeedUpdateTemplate { get; set; } = true;

        /// <summary>创建匹配模板的抽象方法</summary>
        protected abstract bool CreateTemplate(VisionImage inputImage);

        /// <summary>执行匹配的抽象方法</summary>
        protected abstract List<MatchInstance> ExecuteMatching(VisionImage inputImage);

        /// <summary>释放模型资源</summary>
        protected virtual void ReleaseModel()
        {
            if (ModelHandle != null && ModelHandle.Length > 0)
            {
                try
                {
                    DisposeModel();
                    ModelHandle = new HTuple();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放模型失败: {ex.Message}");
                }
            }
        }

        protected abstract void DisposeModel();

        /// <summary>从ROI区域提取模板</summary>
        protected bool ExtractTemplateFromROI(HObject inputImage, CaliperData.ROIGeometry roi,
            out HObject templateRegion, out HObject templateImage)
        {
            templateRegion = null;
            templateImage = null;

            try
            {
                templateRegion = CreateHalconRegionFromROI(roi);
                HOperatorSet.ReduceDomain(inputImage, templateRegion, out templateImage);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"提取模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>从ROI几何信息创建Halcon区域</summary>
        protected HObject CreateHalconRegionFromROI(CaliperData.ROIGeometry roi)
        {
            var rect = GetBoundingRectangleFromROI(roi);
            HOperatorSet.GenRectangle1(out HObject region,
                rect.Row1, rect.Col1, rect.Row2, rect.Col2);
            return region;
        }

        /// <summary>从ROI获取边界矩形</summary>
        private BoundingRectangle GetBoundingRectangleFromROI(CaliperData.ROIGeometry roi)
        {
            if (roi?.Parameters == null)
                throw new ArgumentException("ROI参数无效");

            switch (roi.RoiType)
            {
                case "rectangle1":
                    // Rectangle1: Row1, Col1, Row2, Col2
                    return new BoundingRectangle
                    {
                        Row1 = roi.Parameters["Row1"],
                        Col1 = roi.Parameters["Col1"],
                        Row2 = roi.Parameters["Row2"],
                        Col2 = roi.Parameters["Col2"]
                    };
                case "rectangle2":
                    // Rectangle2: Row, Column, Phi, Length1, Length2
                    // 转换为Rectangle1格式
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

        /// <summary>创建测量数据</summary>
        protected Dictionary<string, object> CreateMeasurements(List<MatchInstance> results)
        {
            var measurements = new Dictionary<string, object>();

            measurements["匹配成功"] = results.Count > 0 ? "是" : "否";
            measurements["匹配数量"] = results.Count;

            if (results.Count > 0)
            {
                var best = results[0];
                measurements["最佳匹配位置Row"] = Math.Round(best.Row, 3);
                measurements["最佳匹配位置Col"] = Math.Round(best.Column, 3);
                measurements["最佳匹配角度"] = Math.Round(best.Angle, 2);
                measurements["最佳匹配分数"] = Math.Round(best.Score, 6);

                if (best.Scale != 1.0)
                {
                    measurements["最佳匹配缩放"] = Math.Round(best.Scale, 6);
                }
            }

            return measurements;
        }

        /// <summary>创建显示轮廓</summary>
        protected HalconDisplayContours CreateDisplayContours(List<MatchInstance> results)
        {
            var displayContours = new HalconDisplayContours();

            if (results != null && results.Count > 0)
            {
                try
                {
                    // 创建匹配结果轮廓的复合对象
                    HObject allContours = null;

                    foreach (var result in results)
                    {
                        if (result.ContourPoints != null && result.ContourPoints.Length > 0)
                        {
                            // 修复坐标转换：Row对应Y，Col对应X
                            var rows = new HTuple(result.ContourPoints.Select(p => p.Y).ToArray());
                            var cols = new HTuple(result.ContourPoints.Select(p => p.X).ToArray());

                            HOperatorSet.GenContourPolygonXld(out HObject contour, rows, cols);

                            if (allContours == null)
                            {
                                allContours = contour;
                            }
                            else
                            {
                                HOperatorSet.ConcatObj(allContours, contour, out HObject combined);
                                allContours?.Dispose();
                                allContours = combined;
                                contour?.Dispose();
                            }
                        }
                    }

                    // 将匹配轮廓作为模型轮廓显示（红色）
                    displayContours.ModelContour = allContours;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建显示轮廓失败: {ex.Message}");
                }
            }

            return displayContours;
        }

        protected new ProcessResult CreateSuccessResult(VisionImage outputImage, TimeSpan processingTime, Dictionary<string, object> measurements = null)
        {
            NeedUpdateTemplate = false;
            return base.CreateSuccessResult(outputImage, processingTime, measurements);
        }
    }

    /// <summary>
    /// 边界矩形结构
    /// </summary>
    public class BoundingRectangle
    {
        public double Row1 { get; set; }
        public double Col1 { get; set; }
        public double Row2 { get; set; }
        public double Col2 { get; set; }
    }
}