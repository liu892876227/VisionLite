using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.FilterProcessors
{
    /// <summary>
    /// 中值滤波处理器
    /// 实现中值滤波算法，用于去除椒盐噪声
    /// </summary>
    public class MedianFilterProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "中值滤波";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 滤波器大小（奇数）
        /// </summary>
        [Parameter("滤波器大小", "滤波器窗口大小，自动调整为奇数，值越大滤波效果越强", 3, 15, Step = 1, DecimalPlaces = 0)]
        public int FilterSize { get; set; } = 3;
        
        /// <summary>
        /// 滤波形状
        /// </summary>
        [Parameter("滤波形状", Group = "高级设置", IsAdvanced = true)]
        public FilterShape Shape { get; set; } = FilterShape.Rectangle;
        
        /// <summary>
        /// 是否保持图像类型
        /// </summary>
        [Parameter("保持图像类型", Group = "高级设置", IsAdvanced = true)]
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
                
                // 执行中值滤波
                var outputImage = await Task.Run(() => ExecuteMedianFilter(inputImage));
                
                // 计算处理时间
                var processingTime = DateTime.Now - startTime;
                
                // 创建测量结果
                var measurements = CreateMeasurements(inputImage, processingTime);
                
                // 返回成功结果
                return CreateSuccessResult(outputImage, processingTime, measurements);
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return CreateFailureResult($"中值滤波处理失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region 私有方法
        
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
                
            // 智能调整滤波器大小
            FilterSize = AdjustFilterSize(FilterSize);
        }
        
        /// <summary>
        /// 调整滤波器大小为合适的奇数值
        /// </summary>
        /// <param name="size">原始大小</param>
        /// <returns>调整后的奇数大小</returns>
        private int AdjustFilterSize(int size)
        {
            // 确保在有效范围内
            size = Math.Max(3, Math.Min(15, size));
            
            // 如果是偶数，向上调整为奇数
            if (size % 2 == 0)
                size++;
                
            return size;
        }
        
        /// <summary>
        /// 执行中值滤波算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>滤波后的图像</returns>
        private VisionImage ExecuteMedianFilter(VisionImage inputImage)
        {
            HObject outputImage = null;
            
            try
            {
                // 检查输入图像是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入图像对象为空");
                
                // 根据滤波形状执行中值滤波
                string shapeType = GetShapeTypeName();
                
                // 使用Halcon的MedianImage算子进行中值滤波
                // MedianImage需要mask参数和margins参数
                HOperatorSet.MedianImage(inputImage.HImage, out outputImage, shapeType, FilterSize, "mirrored");
                
                // 如果需要保持图像类型，进行类型转换
                if (PreserveImageType)
                {
                    outputImage = PreserveOriginalImageType(inputImage.HImage, outputImage);
                }
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("中值滤波处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException("Halcon中值滤波操作失败", ex);
            }
        }
        
        /// <summary>
        /// 获取滤波形状类型名称（Halcon格式）
        /// </summary>
        /// <returns>形状类型名称</returns>
        private string GetShapeTypeName()
        {
            return Shape switch
            {
                FilterShape.Rectangle => "rectangle",
                FilterShape.Circle => "circle",
                _ => "rectangle"
            };
        }
        
        /// <summary>
        /// 保持原始图像类型
        /// </summary>
        /// <param name="originalImage">原始图像</param>
        /// <param name="filteredImage">滤波后图像</param>
        /// <returns>类型转换后的图像</returns>
        private HObject PreserveOriginalImageType(HObject originalImage, HObject filteredImage)
        {
            try
            {
                // 获取原始图像类型
                HOperatorSet.GetImageType(originalImage, out HTuple imageType);
                
                // 如果类型不同，进行转换
                HOperatorSet.GetImageType(filteredImage, out HTuple filteredType);
                if (imageType.S != filteredType.S)
                {
                    HOperatorSet.ConvertImageType(filteredImage, out HObject convertedImage, imageType.S);
                    return convertedImage;
                }
                
                return filteredImage;
            }
            catch
            {
                // 如果转换失败，返回原图像
                return filteredImage;
            }
        }
        
        /// <summary>
        /// 创建测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        private Dictionary<string, object> CreateMeasurements(VisionImage inputImage, TimeSpan processingTime)
        {
            var measurements = new Dictionary<string, object>
            {
                ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                ["图像通道数"] = inputImage.Channels,
                ["滤波器大小"] = FilterSize,
                ["滤波形状"] = Shape.GetDisplayName(),
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = "中值滤波",
                ["滤波窗口像素数"] = CalculateWindowPixelCount()
            };
            
            return measurements;
        }
        
        /// <summary>
        /// 计算滤波窗口的像素数量
        /// </summary>
        /// <returns>像素数量</returns>
        private int CalculateWindowPixelCount()
        {
            return Shape switch
            {
                FilterShape.Rectangle => FilterSize * FilterSize,
                FilterShape.Circle => (int)(Math.PI * Math.Pow(FilterSize / 2.0, 2)),
                _ => FilterSize * FilterSize
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// 滤波形状枚举
    /// </summary>
    public enum FilterShape
    {
        /// <summary>矩形滤波器</summary>
        Rectangle,
        /// <summary>圆形滤波器</summary>
        Circle
    }
    
    /// <summary>
    /// 滤波形状扩展方法
    /// </summary>
    public static class FilterShapeExtensions
    {
        /// <summary>
        /// 获取滤波形状的中文显示名称
        /// </summary>
        public static string GetDisplayName(this FilterShape shape)
        {
            return shape switch
            {
                FilterShape.Rectangle => "矩形窗口",
                FilterShape.Circle => "圆形窗口",
                _ => shape.ToString()
            };
        }
    }
}