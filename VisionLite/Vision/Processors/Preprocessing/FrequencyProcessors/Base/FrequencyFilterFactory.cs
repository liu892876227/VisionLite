using System;
using System.Collections.Generic;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Filters;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base
{
    /// <summary>
    /// 频域滤波器工厂类
    /// 提供统一的滤波器创建和管理功能
    /// </summary>
    public static class FrequencyFilterFactory
    {
        /// <summary>
        /// 滤波器类型信息
        /// </summary>
        public class FilterInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public Type ProcessorType { get; set; }
            public bool IsTransform { get; set; }
        }

        /// <summary>
        /// 支持的滤波器类型字典
        /// </summary>
        private static readonly Dictionary<string, FilterInfo> _supportedFilters = new Dictionary<string, FilterInfo>
        {
            ["LowPass"] = new FilterInfo
            {
                Name = "低通滤波",
                Description = "允许低频分量通过，抑制高频噪声",
                ProcessorType = typeof(LowPassFilterProcessor),
                IsTransform = false
            },
            ["HighPass"] = new FilterInfo
            {
                Name = "高通滤波",
                Description = "允许高频分量通过，增强边缘和细节",
                ProcessorType = typeof(HighPassFilterProcessor),
                IsTransform = false
            }
        };

        /// <summary>
        /// 获取所有支持的滤波器类型
        /// </summary>
        /// <returns>滤波器信息列表</returns>
        public static Dictionary<string, FilterInfo> GetSupportedFilters()
        {
            return new Dictionary<string, FilterInfo>(_supportedFilters);
        }

        /// <summary>
        /// 创建指定类型的滤波器处理器
        /// </summary>
        /// <param name="filterType">滤波器类型</param>
        /// <returns>滤波器处理器实例</returns>
        public static FrequencyProcessorBase CreateFilter(string filterType)
        {
            if (string.IsNullOrEmpty(filterType))
                throw new ArgumentException("滤波器类型不能为空", nameof(filterType));

            if (!_supportedFilters.ContainsKey(filterType))
                throw new NotSupportedException($"不支持的滤波器类型: {filterType}");

            var filterInfo = _supportedFilters[filterType];
            
            try
            {
                return (FrequencyProcessorBase)Activator.CreateInstance(filterInfo.ProcessorType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建滤波器失败: {filterType}", ex);
            }
        }

        /// <summary>
        /// 创建低通滤波器
        /// </summary>
        /// <param name="cutoffFrequency">截止频率</param>
        /// <param name="filterType">滤波器类型</param>
        /// <param name="filterOrder">滤波器阶数</param>
        /// <returns>配置好的低通滤波器</returns>
        public static LowPassFilterProcessor CreateLowPassFilter(
            double cutoffFrequency = 0.3, 
            FilterType filterType = FilterType.Butterworth, 
            int filterOrder = 2)
        {
            var filter = new LowPassFilterProcessor
            {
                CutoffFrequency = cutoffFrequency,
                FilterType = filterType,
                FilterOrder = filterOrder
            };

            return filter;
        }

        /// <summary>
        /// 创建高通滤波器
        /// </summary>
        /// <param name="cutoffFrequency">截止频率</param>
        /// <param name="filterType">滤波器类型</param>
        /// <param name="filterOrder">滤波器阶数</param>
        /// <param name="applyZeroFrequencyBoost">是否应用零频增强</param>
        /// <param name="zeroFrequencyBoost">零频增强系数</param>
        /// <returns>配置好的高通滤波器</returns>
        public static HighPassFilterProcessor CreateHighPassFilter(
            double cutoffFrequency = 0.1, 
            FilterType filterType = FilterType.Butterworth, 
            int filterOrder = 2,
            bool applyZeroFrequencyBoost = true,
            double zeroFrequencyBoost = 0.1)
        {
            var filter = new HighPassFilterProcessor
            {
                CutoffFrequency = cutoffFrequency,
                FilterType = filterType,
                FilterOrder = filterOrder,
                ApplyZeroFrequencyBoost = applyZeroFrequencyBoost,
                ZeroFrequencyBoost = zeroFrequencyBoost
            };

            return filter;
        }

        /// <summary>
        /// 验证滤波器参数是否有效
        /// </summary>
        /// <param name="cutoffFrequency">截止频率</param>
        /// <param name="filterOrder">滤波器阶数</param>
        /// <param name="filterType">滤波器类型</param>
        /// <returns>参数验证结果</returns>
        public static (bool IsValid, string ErrorMessage) ValidateFilterParameters(
            double cutoffFrequency, 
            int filterOrder, 
            FilterType filterType)
        {
            // 截止频率验证
            if (cutoffFrequency <= 0 || cutoffFrequency >= 1.0)
            {
                return (false, "截止频率必须在0到1之间");
            }

            // 滤波器阶数验证
            if (filterOrder < 1 || filterOrder > 10)
            {
                return (false, "滤波器阶数必须在1到10之间");
            }

            // 滤波器类型验证
            if (!Enum.IsDefined(typeof(FilterType), filterType))
            {
                return (false, "无效的滤波器类型");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// 获取滤波器的推荐参数
        /// </summary>
        /// <param name="filterType">滤波器类型</param>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="noiseLevel">噪声水平 (0-1)</param>
        /// <returns>推荐的滤波器参数</returns>
        public static (double CutoffFrequency, int FilterOrder) GetRecommendedParameters(
            string filterType, 
            (int Width, int Height) imageSize, 
            double noiseLevel = 0.1)
        {
            var minSize = Math.Min(imageSize.Width, imageSize.Height);
            
            switch (filterType.ToUpper())
            {
                case "LOWPASS":
                    // 低通滤波：根据噪声水平调整截止频率
                    var lowPassCutoff = Math.Max(0.1, Math.Min(0.8, 0.5 - noiseLevel * 0.3));
                    var lowPassOrder = noiseLevel > 0.3 ? 4 : 2;
                    return (lowPassCutoff, lowPassOrder);

                case "HIGHPASS":
                    // 高通滤波：根据图像尺寸调整截止频率
                    var highPassCutoff = minSize > 512 ? 0.05 : 0.1;
                    var highPassOrder = minSize > 1024 ? 3 : 2;
                    return (highPassCutoff, highPassOrder);

                default:
                    return (0.3, 2);
            }
        }

        /// <summary>
        /// 获取滤波器性能信息
        /// </summary>
        /// <param name="filterType">滤波器类型</param>
        /// <returns>性能信息</returns>
        public static Dictionary<string, object> GetFilterPerformanceInfo(string filterType)
        {
            var info = new Dictionary<string, object>();

            if (_supportedFilters.ContainsKey(filterType))
            {
                var filterInfo = _supportedFilters[filterType];
                info["Name"] = filterInfo.Name;
                info["Description"] = filterInfo.Description;
                info["IsTransform"] = filterInfo.IsTransform;
                info["SupportedImageTypes"] = new[] { "byte", "int2", "uint2", "int4", "real", "complex" };
                info["MaxImageSize"] = new { Width = 8192, Height = 8192 };
                info["ProcessingComplexity"] = filterInfo.IsTransform ? "O(N*log(N))" : "O(N^2*log(N))";
                info["MemoryRequirement"] = "约为输入图像的2-4倍";
            }

            return info;
        }
    }
}