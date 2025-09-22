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
    /// 形状匹配处理器
    /// 支持常规、缩放、各向异性三种形状匹配模式
    /// </summary>
    public class ShapeMatchingProcessor : ImageMatchingProcessorBase
    {
        public override string ProcessorName => "形状匹配";

        #region 参数定义

        [Parameter("匹配类型", "选择形状匹配的算法类型", Order = 5, Group = "算法选择")]
        public ShapeMatchingType MatchingType { get; set; } = ShapeMatchingType.Standard;

        [Parameter("角度范围最小值", "搜索角度下限(度)", Order = 10, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MinAngle { get; set; } = -10;

        [Parameter("角度范围最大值", "搜索角度上限(度)", Order = 11, Group = "搜索参数", MinValue = -180, MaxValue = 180)]
        public double MaxAngle { get; set; } = 10;

        [Parameter("最小匹配得分", "接受结果的最低分数", Order = 12, Group = "搜索参数", MinValue = 0.1, MaxValue = 1.0)]
        public double MinScore { get; set; } = 0.6;

        // 缩放匹配参数组
        [Parameter("缩放范围最小值", "最小缩放比例", Order = 20, Group = "缩放参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MinScale { get; set; } = 0.9;

        [Parameter("缩放范围最大值", "最大缩放比例", Order = 21, Group = "缩放参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MaxScale { get; set; } = 1.1;

        // 各向异性参数组
        [Parameter("行缩放最小值", "行方向最小缩放比例", Order = 30, Group = "各向异性参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MinScaleRow { get; set; } = 0.9;

        [Parameter("行缩放最大值", "行方向最大缩放比例", Order = 31, Group = "各向异性参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MaxScaleRow { get; set; } = 1.1;

        [Parameter("列缩放最小值", "列方向最小缩放比例", Order = 32, Group = "各向异性参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MinScaleCol { get; set; } = 0.9;

        [Parameter("列缩放最大值", "列方向最大缩放比例", Order = 33, Group = "各向异性参数", MinValue = 0.5, MaxValue = 2.0)]
        public double MaxScaleCol { get; set; } = 1.1;

        // 高级参数组
        [Parameter("对比度阈值", "边缘检测对比度阈值", Order = 40, Group = "高级参数", MinValue = 10, MaxValue = 100)]
        public int ContrastThreshold { get; set; } = 30;

        [Parameter("最小对比度", "边缘点最小对比度", Order = 41, Group = "高级参数", MinValue = 5, MaxValue = 50)]
        public int MinContrast { get; set; } = 10;


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


                HTuple modelHandle;
                // 计算正确的角度参数：angleStart是起始角度，angleExtent是角度范围跨度
                double angleStartRad = MinAngle * Math.PI / 180;
                double angleExtentRad = (MaxAngle - MinAngle) * Math.PI / 180;

                try
                {
                    switch (MatchingType)
                    {
                        case ShapeMatchingType.Standard:
                            HOperatorSet.CreateShapeModel(templateImage, "auto",
                                angleStartRad, angleExtentRad,
                                "auto", "auto", "use_polarity",
                                ContrastThreshold, MinContrast, out modelHandle);
                            System.Diagnostics.Debug.WriteLine($"创建标准形状模型成功，角度范围: {MinAngle}°到{MaxAngle}°，句柄: {modelHandle}");
                            break;

                        case ShapeMatchingType.Scaled:
                            HOperatorSet.CreateScaledShapeModel(templateImage, "auto",
                                angleStartRad, angleExtentRad, "auto",
                                MinScale, MaxScale, "auto", "auto", "use_polarity",
                                ContrastThreshold, MinContrast, out modelHandle);
                            System.Diagnostics.Debug.WriteLine($"创建缩放形状模型成功，角度范围: {MinAngle}°到{MaxAngle}°，句柄: {modelHandle}");
                            break;

                        case ShapeMatchingType.Anisotropic:
                            HOperatorSet.CreateAnisoShapeModel(templateImage, "auto",
                                angleStartRad, angleExtentRad, "auto",
                                MinScaleRow, MaxScaleRow, "auto",
                                MinScaleCol, MaxScaleCol, "auto",
                                "auto", "use_polarity",
                                ContrastThreshold, MinContrast, out modelHandle);
                            System.Diagnostics.Debug.WriteLine($"创建各向异性形状模型成功，角度范围: {MinAngle}°到{MaxAngle}°，句柄: {modelHandle}");
                            break;
                        default:
                            modelHandle = new HTuple();
                            System.Diagnostics.Debug.WriteLine("未知的形状匹配类型");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建形状模型失败: {ex.Message}");
                    modelHandle = new HTuple();
                }

                ModelHandle = modelHandle;

                templateRegion?.Dispose();
                templateImage?.Dispose();

                NeedUpdateTemplate = false;
                return ModelHandle != null && ModelHandle.Length > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建形状模板失败: {ex.Message}");
                return false;
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

                // 计算正确的角度参数：与CreateShapeModel保持一致
                double angleStartRad = MinAngle * Math.PI / 180;
                double angleExtentRad = (MaxAngle - MinAngle) * Math.PI / 180;

                HTuple rows = new HTuple(), columns = new HTuple(), angles = new HTuple(), scores = new HTuple();
                HTuple scales = new HTuple(), scaleRows = new HTuple(), scaleCols = new HTuple();

                switch (MatchingType)
                {
                    case ShapeMatchingType.Standard:
                        HOperatorSet.FindShapeModel(searchImage, ModelHandle,
                            angleStartRad, angleExtentRad,
                            MinScore, MaxMatches, MaxOverlap, "least_squares", 0, 0.9,
                            out rows, out columns, out angles, out scores);
                        break;

                    case ShapeMatchingType.Scaled:
                        HOperatorSet.FindScaledShapeModel(searchImage, ModelHandle,
                            angleStartRad, angleExtentRad,
                            MinScale, MaxScale, MinScore, MaxMatches, MaxOverlap,
                            "least_squares", 0, 0.9,
                            out rows, out columns, out angles, out scales, out scores);
                        break;

                    case ShapeMatchingType.Anisotropic:
                        HOperatorSet.FindAnisoShapeModel(searchImage, ModelHandle,
                            angleStartRad, angleExtentRad,
                            MinScaleRow, MaxScaleRow, MinScaleCol, MaxScaleCol,
                            MinScore, MaxMatches, MaxOverlap, "least_squares", 0, 0.9,
                            out rows, out columns, out angles, out scaleRows, out scaleCols, out scores);
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"形状匹配搜索 - 角度范围: {MinAngle}°到{MaxAngle}°，找到{rows.Length}个匹配");

                for (int i = 0; i < rows.Length; i++)
                {
                    var instance = new MatchInstance
                    {
                        Row = rows[i].D,
                        Column = columns[i].D,
                        Angle = angles[i].D * 180 / Math.PI,
                        Score = scores[i].D
                    };

                    switch (MatchingType)
                    {
                        case ShapeMatchingType.Standard:
                            instance.Scale = 1.0;
                            instance.ScaleRow = 1.0;
                            instance.ScaleCol = 1.0;
                            break;
                        case ShapeMatchingType.Scaled:
                            instance.Scale = scales[i].D;
                            instance.ScaleRow = scales[i].D;
                            instance.ScaleCol = scales[i].D;
                            break;
                        case ShapeMatchingType.Anisotropic:
                            instance.ScaleRow = scaleRows[i].D;
                            instance.ScaleCol = scaleCols[i].D;
                            instance.Scale = Math.Sqrt(instance.ScaleRow * instance.ScaleCol);
                            break;
                    }

                    if (ShowMatchContours)
                    {
                        instance.ContourPoints = GetShapeModelContour(instance);
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
                System.Diagnostics.Debug.WriteLine($"形状匹配执行失败: {ex.Message}");
            }

            return results;
        }

        protected override void DisposeModel()
        {
            if (ModelHandle != null && ModelHandle.Length > 0)
            {
                HOperatorSet.ClearShapeModel(ModelHandle);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取形状模型轮廓的变换后XLD对象（供CreateDisplayContours使用）
        /// </summary>
        private HObject GetTransformedShapeModelContours(MatchInstance instance)
        {
            try
            {
                // 检查模型句柄是否有效
                if (ModelHandle == null || ModelHandle.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("形状模型句柄无效，无法获取轮廓");
                    return null;
                }

                HObject contours = null;
                HObject transformedContour = null;

                try
                {
                    // 获取形状模型轮廓
                    HOperatorSet.GetShapeModelContours(out contours, ModelHandle, 1);

                    // 验证轮廓对象是否有效
                    if (contours == null)
                    {
                        System.Diagnostics.Debug.WriteLine("获取的形状模型轮廓对象为null");
                        return null;
                    }

                    // 检查轮廓数量
                    HTuple contourCount;
                    HOperatorSet.CountObj(contours, out contourCount);
                    if (contourCount.I == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("形状模型轮廓数量为0");
                        return null;
                    }

                   

                    // 创建仿射变换矩阵
                    HTuple homMat2D;
                    HOperatorSet.VectorAngleToRigid(0, 0, 0,
                        instance.Row, instance.Column, instance.Angle * Math.PI / 180,
                        out homMat2D);

                    // 如果需要缩放变换
                    if (MatchingType != ShapeMatchingType.Standard)
                    {
                        HTuple scaleMat;
                        if (MatchingType == ShapeMatchingType.Anisotropic)
                        {
                            HOperatorSet.HomMat2dScale(homMat2D, instance.ScaleRow, instance.ScaleCol,
                                instance.Row, instance.Column, out scaleMat);
                        }
                        else
                        {
                            HOperatorSet.HomMat2dScale(homMat2D, instance.Scale, instance.Scale,
                                instance.Row, instance.Column, out scaleMat);
                        }
                        homMat2D = scaleMat;
                    }

                    // 应用仿射变换
                    HOperatorSet.AffineTransContourXld(contours, out transformedContour, homMat2D);

                    if (transformedContour == null)
                    {
                        System.Diagnostics.Debug.WriteLine("变换后的轮廓对象为null");
                        return null;
                    }

                    // 检查变换后轮廓数量
                    HTuple transformedCount;
                    HOperatorSet.CountObj(transformedContour, out transformedCount);
                    if (transformedCount.I == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("变换后轮廓数量为0");
                        return null;
                    }

                    // 返回变换后的轮廓对象（不释放）
                    contours?.Dispose();
                    return transformedContour;
                }
                catch 
                {
                    // 发生异常时释放资源
                    contours?.Dispose();
                    transformedContour?.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取形状轮廓失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
            }

            return null;
        }

        private Point2D[] GetShapeModelContour(MatchInstance instance)
        {
            // 兼容性方法：为ContourPoints提供一个代表性轮廓
            // 实际的多轮廓显示由CreateDisplayContours处理
            return new Point2D[0];
        }

        #endregion

        /// <summary>
        /// 创建形状匹配的显示轮廓（重写基类方法以支持多轮廓独立显示）
        /// </summary>
        protected new HalconDisplayContours CreateDisplayContours(List<MatchInstance> results)
        {
            var displayContours = new HalconDisplayContours();

            if (results != null && results.Count > 0)
            {
                try
                {
                    // 创建匹配结果轮廓的复合对象，但保持每个子轮廓的独立性
                    HObject allContours = null;

                    foreach (var result in results)
                    {
                        // 获取每个匹配实例的变换后轮廓（已经是多个独立的XLD轮廓）
                        var transformedContours = GetTransformedShapeModelContours(result);
                        if (transformedContours != null)
                        {
                            if (allContours == null)
                            {
                                allContours = transformedContours;
                            }
                            else
                            {
                                // 使用ConcatObj连接轮廓对象，保持各个子轮廓的独立性
                                HOperatorSet.ConcatObj(allContours, transformedContours, out HObject combined);
                                allContours?.Dispose();
                                allContours = combined;
                                transformedContours?.Dispose();
                            }
                        }
                    }

                    // 将匹配轮廓作为模型轮廓显示（红色）
                    displayContours.ModelContour = allContours;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建形状匹配显示轮廓失败: {ex.Message}");
                }
            }

            return displayContours;
        }

        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (NeedUpdateTemplate || ModelHandle == null || ModelHandle.Length == 0)
                {
                    if (!CreateTemplate(inputImage))
                    {
                        return CreateFailureResult("形状模板创建失败，请检查模板ROI设置");
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
                return CreateFailureResult($"形状匹配失败: {ex.Message}");
            }
        }
    }
}