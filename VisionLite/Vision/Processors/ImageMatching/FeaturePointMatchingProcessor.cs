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
    /// 特征点匹配处理器
    /// 使用局部可变形模型进行特征点描述符匹配
    /// </summary>
    public class FeaturePointMatchingProcessor : ImageMatchingProcessorBase
    {
        public override string ProcessorName => "特征点匹配";

        #region 参数定义

        [Parameter("角度范围最小值", "搜索角度下限(度)", Order = 10, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MinAngle { get; set; } = -20;

        [Parameter("角度范围最大值", "搜索角度上限(度)", Order = 11, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MaxAngle { get; set; } = 20;

        [Parameter("最小匹配分数", "接受匹配的最低分数", Order = 12, Group = "搜索参数", MinValue = 0.1, MaxValue = 1.0)]
        public double MinScore { get; set; } = 0.6;

        [Parameter("最大变形程度", "允许的最大局部变形", Order = 20, Group = "变形参数", MinValue = 0.01, MaxValue = 0.5)]
        public double MaxDeformation { get; set; } = 0.1;

        [Parameter("变形步长", "局部变形的搜索步长", Order = 21, Group = "变形参数", MinValue = 0.005, MaxValue = 0.1)]
        public double DeformationStep { get; set; } = 0.02;

        [Parameter("金字塔层数", "图像金字塔的层数", Order = 30, Group = "高级参数")]
        public PyramidLevels PyramidLevels { get; set; } = PyramidLevels.Auto;

        [Parameter("点约简策略", "特征点约简策略", Order = 31, Group = "高级参数")]
        public PointReduction PointReduction { get; set; } = PointReduction.High;

        [Parameter("显示特征点", "是否显示检测到的特征点", Order = 40, Group = "显示设置")]
        public bool ShowFeaturePoints { get; set; } = true;

        [Parameter("显示变形网格", "是否显示局部变形网格", Order = 41, Group = "显示设置")]
        public bool ShowDeformationGrid { get; set; } = false;

        #endregion

        #region Halcon算法流程

        protected override bool CreateTemplate(VisionImage inputImage)
        {
            // 暂时禁用特征点匹配，待Halcon API调用修复后启用
            System.Diagnostics.Debug.WriteLine("特征点匹配暂未实现");
            return false;
        }

        protected override List<MatchInstance> ExecuteMatching(VisionImage inputImage)
        {
            // 暂时禁用特征点匹配，待Halcon API调用修复后启用
            return new List<MatchInstance>();
        }

        protected override void DisposeModel()
        {
            if (ModelHandle != null && ModelHandle.Length > 0)
            {
                HOperatorSet.ClearDeformableModel(ModelHandle);
            }
        }

        #endregion

        #region 辅助方法

        private Point2D[] GetContourFromHObject(HObject contours, int index)
        {
            try
            {
                if (contours != null)
                {
                    HOperatorSet.GetContourXld(contours, out HTuple rows, out HTuple cols);

                    var points = new Point2D[Math.Min(rows.Length, 100)];
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = new Point2D(rows[i].D, cols[i].D);
                    }

                    return points;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取轮廓失败: {ex.Message}");
            }

            return new Point2D[0];
        }

        #endregion

        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (NeedUpdateTemplate || ModelHandle == null || ModelHandle.Length == 0)
                {
                    if (!CreateTemplate(inputImage))
                    {
                        return CreateFailureResult("特征点模板创建失败，请检查模板ROI设置");
                    }
                }

                var matchResults = ExecuteMatching(inputImage);
                var measurements = CreateMeasurements(matchResults);

                stopwatch.Stop();
                var result = CreateSuccessResult(inputImage.Clone(), stopwatch.Elapsed, measurements);

                if (ShowMatchContours && matchResults.Count > 0)
                {
                    var contours = CreateDisplayContours(matchResults);
                    result.AddMetadata("HalconDisplayContours", contours);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateFailureResult($"特征点匹配失败: {ex.Message}");
            }
        }
    }
}