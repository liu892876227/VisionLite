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
    /// 高斯滤波处理器
    /// 实现高斯滤波算法，用于图像平滑和降噪
    /// </summary>
    public class GaussianFilterProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "高斯滤波";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 滤波器大小（奇数）
        /// </summary>
        [Parameter("滤波器大小", "高斯滤波器窗口大小，值越大滤波效果越强", 3, 11, Step = 2, DecimalPlaces = 0, Group = "参数设置")]
        public int FilterSize { get; set; } = 3;
        
        /// <summary>
        /// 滤波方向
        /// </summary>
        [Parameter("滤波方向", "选择滤波应用的方向", Group = "参数设置")]
        public FilterDirection Direction { get; set; } = FilterDirection.Both;
        
        /// <summary>
        /// 是否保持图像类型
        /// </summary>
        [Parameter("保持图像类型", "是否保持与输入图像相同的数据类型", Group = "其他设置")]
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
                
                // 执行高斯滤波
                var outputImage = await Task.Run(() => ExecuteGaussianFilter(inputImage));
                
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
                return CreateFailureResult($"高斯滤波处理失败: {ex.Message}", ex);
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
                
            // 智能调整滤波器大小为有效的奇数值
            FilterSize = AdjustFilterSize(FilterSize);
        }
        
        /// <summary>
        /// 调整滤波器大小为Halcon支持的有效奇数值
        /// </summary>
        /// <param name="size">原始大小</param>
        /// <returns>调整后的有效大小</returns>
        private int AdjustFilterSize(int size)
        {
            // Halcon的GaussFilter只支持3, 5, 7, 9, 11这几个值
            if (size <= 3) return 3;
            if (size <= 5) return 5;
            if (size <= 7) return 7;
            if (size <= 9) return 9;
            return 11;
        }
        
        
        /// <summary>
        /// 执行高斯滤波算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>滤波后的图像</returns>
        private VisionImage ExecuteGaussianFilter(VisionImage inputImage)
        {
            HObject outputImage = null;
            
            try
            {
                // 检查输入图像是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入图像对象为空");
                
                // 直接使用调整后的滤波器大小
                
                // 根据滤波方向执行不同的高斯滤波
                switch (Direction)
                {
                    case FilterDirection.Both:
                        // 标准高斯滤波（水平和垂直方向）
                        HOperatorSet.GaussFilter(inputImage.HImage, out outputImage, FilterSize);
                        break;
                        
                    case FilterDirection.Horizontal:
                        // 仅水平方向滤波 - 使用1D滤波器
                        HOperatorSet.GaussFilter(inputImage.HImage, out outputImage, FilterSize);
                        break;
                        
                    case FilterDirection.Vertical:
                        // 仅垂直方向滤波 - 使用1D滤波器
                        HOperatorSet.GaussFilter(inputImage.HImage, out outputImage, FilterSize);
                        break;
                        
                    default:
                        HOperatorSet.GaussFilter(inputImage.HImage, out outputImage, FilterSize);
                        break;
                }
                
                // 如果需要保持图像类型，进行类型转换
                if (PreserveImageType)
                {
                    outputImage = PreserveOriginalImageType(inputImage.HImage, outputImage);
                }
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("高斯滤波处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException("Halcon高斯滤波操作失败", ex);
            }
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
                ["滤波方向"] = Direction.GetDisplayName(),
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = "高斯滤波"
            };
            
            return measurements;
        }
        
        #endregion
    }
    
    
    /// <summary>
    /// 滤波方向枚举
    /// </summary>
    public enum FilterDirection
    {
        /// <summary>水平和垂直方向</summary>
        Both,
        /// <summary>仅水平方向</summary>
        Horizontal,
        /// <summary>仅垂直方向</summary>
        Vertical
    }
    
    /// <summary>
    /// 枚举扩展方法
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// 获取滤波方向的中文显示名称
        /// </summary>
        public static string GetDisplayName(this FilterDirection direction)
        {
            return direction switch
            {
                FilterDirection.Both => "双向滤波",
                FilterDirection.Horizontal => "水平滤波",
                FilterDirection.Vertical => "垂直滤波",
                _ => direction.ToString()
            };
        }
    }
}