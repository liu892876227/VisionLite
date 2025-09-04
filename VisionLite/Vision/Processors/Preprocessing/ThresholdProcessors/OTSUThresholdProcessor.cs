using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.ThresholdProcessors
{
    /// <summary>
    /// OTSU自动阈值二值化处理器
    /// 使用OTSU算法自动计算最佳阈值进行二值化处理
    /// </summary>
    public class OTSUThresholdProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "OTSU自动阈值二值化";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 提取模式
        /// </summary>
        [Parameter("提取模式", "选择提取亮区域还是暗区域", Group = "参数设置")]
        public ExtractionMode LightDark { get; set; } = ExtractionMode.Light;
        
        /// <summary>
        /// 反转输出
        /// </summary>
        [Parameter("反转输出", "是否反转二值化结果，true时黑白颠倒", Group = "其他设置")]
        public bool InvertOutput { get; set; } = false;
        
        /// <summary>
        /// 是否保持图像类型
        /// </summary>
        [Parameter("保持图像类型", "是否保持原图像数据类型", Group = "其他设置")]
        public bool PreserveImageType { get; set; } = true;
        
        #endregion
        
        #region 主要方法
        
        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // 参数验证
                ValidateInputs(inputImage);
                
                // 执行OTSU自动阈值二值化
                var outputImage = await Task.Run(() => ExecuteOTSUThreshold(inputImage));
                
                // 计算处理时间
                var processingTime = DateTime.Now - startTime;
                
                // 创建测量结果
                var measurements = CreateMeasurements(inputImage, outputImage, processingTime);
                
                // 返回成功结果
                return CreateSuccessResult(outputImage, processingTime, measurements);
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return CreateFailureResult($"OTSU自动阈值二值化处理失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        // 存储计算出的阈值，用于结果显示
        private double _calculatedThreshold = 0;
        private double _interClassVariance = 0;
        
        /// <summary>
        /// 验证输入参数
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        private void ValidateInputs(VisionImage inputImage)
        {
            if (inputImage == null)
                throw new ArgumentNullException(nameof(inputImage), "输入图像不能为空");
                
            if (inputImage.HImage == null)
                throw new ArgumentException("输入图像数据无效");
        }
        
        /// <summary>
        /// 执行OTSU自动阈值二值化算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>二值化后的图像</returns>
        private VisionImage ExecuteOTSUThreshold(VisionImage inputImage)
        {
            HObject outputImage = null;
            
            try
            {
                // 检查输入图像是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入图像对象为空");
                
                // 获取图像尺寸
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);
                
                // 使用Halcon的BinaryThreshold算子进行OTSU自动阈值二值化
                // method="max_separability" 就是OTSU算法
                // BinaryThreshold返回的是区域对象，不是图像对象
                HOperatorSet.BinaryThreshold(inputImage.HImage, out HObject regions, 
                    "max_separability", LightDark.GetHalconString(), out HTuple usedThreshold);
                
                // 保存计算出的阈值信息
                _calculatedThreshold = usedThreshold.D;
                
                // 将区域转换为二值图像
                HOperatorSet.RegionToBin(regions, out outputImage, 255, 0, width.I, height.I);
                
                // 清理临时区域
                regions?.Dispose();
                
                // 计算类间方差（评估阈值质量的指标）
                _interClassVariance = CalculateInterClassVariance(inputImage.HImage, _calculatedThreshold);
                
                // 如果需要反转输出
                if (InvertOutput)
                {
                    HOperatorSet.InvertImage(outputImage, out HObject invertedImage);
                    outputImage?.Dispose();
                    outputImage = invertedImage;
                }
                
                // 如果需要保持图像类型，进行类型转换
                if (PreserveImageType)
                {
                    outputImage = PreserveOriginalImageType(inputImage.HImage, outputImage);
                }
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("OTSU自动阈值二值化处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException("Halcon OTSU自动阈值二值化操作失败", ex);
            }
        }
        
        /// <summary>
        /// 使用简化的OTSU算法计算最佳阈值
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>最佳阈值</returns>
        private double CalculateSimpleOTSU(HObject image)
        {
            try
            {
                // 使用图像的平均灰度值和标准差来估算阈值
                HOperatorSet.Intensity(image, image, out HTuple meanIntensity, out HTuple deviation);
                
                // 基于均值和标准差的简化OTSU估算
                double threshold = meanIntensity.D + (deviation.D * 0.5);
                
                // 限制阈值在合理范围内
                threshold = Math.Max(30, Math.Min(220, threshold));
                
                return threshold;
            }
            catch
            {
                // 如果计算失败，返回默认阈值
                return 128.0;
            }
        }
        
        /// <summary>
        /// 计算类间方差（评估阈值质量）
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="threshold">阈值</param>
        /// <returns>类间方差值</returns>
        private double CalculateInterClassVariance(HObject image, double threshold)
        {
            try
            {
                // 这里可以实现更详细的类间方差计算
                // 目前返回一个简化的质量评估值
                HOperatorSet.Threshold(image, out HObject regions, (int)threshold, 255);
                HOperatorSet.AreaCenter(regions, out HTuple area, out HTuple row, out HTuple col);
                
                // 简化的方差计算：基于区域面积的标准差
                double totalArea = 0;
                for (int i = 0; i < area.Length; i++)
                {
                    totalArea += area[i].D;
                }
                HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                double imageArea = width.D * height.D;
                double areaRatio = totalArea / imageArea;
                
                // 返回一个质量评估值（0-1之间，越接近0.5质量越好）
                regions?.Dispose();
                return Math.Abs(0.5 - areaRatio) * 100; // 转换为0-50的范围
            }
            catch
            {
                return 0.0;
            }
        }
        
        /// <summary>
        /// 保持原始图像类型
        /// </summary>
        /// <param name="originalImage">原始图像</param>
        /// <param name="thresholdImage">阈值处理后图像</param>
        /// <returns>类型转换后的图像</returns>
        private HObject PreserveOriginalImageType(HObject originalImage, HObject thresholdImage)
        {
            try
            {
                // 获取原始图像类型
                HOperatorSet.GetImageType(originalImage, out HTuple imageType);
                
                // 如果类型不同，进行转换
                HOperatorSet.GetImageType(thresholdImage, out HTuple thresholdType);
                if (imageType.S != thresholdType.S)
                {
                    HOperatorSet.ConvertImageType(thresholdImage, out HObject convertedImage, imageType.S);
                    return convertedImage;
                }
                
                return thresholdImage;
            }
            catch
            {
                // 如果转换失败，返回原图像
                return thresholdImage;
            }
        }
        
        /// <summary>
        /// 创建测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="outputImage">输出图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        private Dictionary<string, object> CreateMeasurements(VisionImage inputImage, VisionImage outputImage, TimeSpan processingTime)
        {
            try
            {
                // 统计二值化结果
                var regionCount = CountRegions(outputImage.HImage);
                var totalArea = CalculateTotalArea(outputImage.HImage);
                var imageArea = inputImage.Width * inputImage.Height;
                var areaRatio = imageArea > 0 ? (totalArea / (double)imageArea * 100) : 0;
                
                var measurements = new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["图像通道数"] = inputImage.Channels,
                    ["OTSU自动阈值"] = Math.Round(_calculatedThreshold, 1),
                    ["提取模式"] = LightDark.GetDisplayName(),
                    ["阈值质量评估"] = Math.Round(_interClassVariance, 2),
                    ["反转输出"] = InvertOutput ? "是" : "否",
                    ["检测到的区域数"] = regionCount,
                    ["总面积(像素)"] = totalArea,
                    ["面积占比(%)"] = Math.Round(areaRatio, 2),
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "OTSU自动阈值二值化"
                };
                
                return measurements;
            }
            catch (Exception ex)
            {
                // 如果统计失败，返回基础信息
                return new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["OTSU自动阈值"] = Math.Round(_calculatedThreshold, 1),
                    ["提取模式"] = LightDark.GetDisplayName(),
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "OTSU自动阈值二值化",
                    ["统计信息"] = $"统计失败: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// 统计区域数量
        /// </summary>
        /// <param name="image">二值化图像</param>
        /// <returns>区域数量</returns>
        private int CountRegions(HObject image)
        {
            try
            {
                HOperatorSet.CountObj(image, out HTuple count);
                return count.I;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 计算总面积
        /// </summary>
        /// <param name="image">二值化图像</param>
        /// <returns>总面积（像素）</returns>
        private long CalculateTotalArea(HObject image)
        {
            try
            {
                HOperatorSet.AreaCenter(image, out HTuple area, out HTuple row, out HTuple col);
                long sum = 0;
                for (int i = 0; i < area.Length; i++)
                {
                    sum += area[i].L;
                }
                return sum;
            }
            catch
            {
                return 0;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 提取模式枚举
    /// </summary>
    public enum ExtractionMode
    {
        /// <summary>提取亮区域</summary>
        Light,
        /// <summary>提取暗区域</summary>
        Dark
    }
    
    /// <summary>
    /// 提取模式扩展方法
    /// </summary>
    public static class ExtractionModeExtensions
    {
        /// <summary>
        /// 获取提取模式的中文显示名称
        /// </summary>
        public static string GetDisplayName(this ExtractionMode mode)
        {
            return mode switch
            {
                ExtractionMode.Light => "亮区域",
                ExtractionMode.Dark => "暗区域",
                _ => mode.ToString()
            };
        }
        
        /// <summary>
        /// 获取Halcon参数字符串
        /// </summary>
        public static string GetHalconString(this ExtractionMode mode)
        {
            return mode switch
            {
                ExtractionMode.Light => "light",
                ExtractionMode.Dark => "dark",
                _ => "light"
            };
        }
    }
}