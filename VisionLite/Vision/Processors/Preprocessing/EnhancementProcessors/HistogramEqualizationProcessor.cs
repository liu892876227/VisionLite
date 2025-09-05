using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.EnhancementProcessors
{
    /// <summary>
    /// 直方图均衡处理器
    /// 使用Halcon EquHistoImage算子实现全局直方图均衡，用于图像对比度增强
    /// </summary>
    public class HistogramEqualizationProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "直方图均衡";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 是否保持图像类型
        /// </summary>
        [Parameter("保持图像类型", "是否保持与输入图像相同的数据类型", Group = "其他设置")]
        public bool PreserveImageType { get; set; } = true;
        
        #endregion
        
        #region 私有字段
        
        /// <summary>
        /// 原始图像类型（用于测量结果显示）
        /// </summary>
        private string _originalImageType = "";
        
        /// <summary>
        /// EquHistoImage处理后的实际图像类型
        /// </summary>
        private string _processedImageType = "";
        
        /// <summary>
        /// 最终输出图像类型
        /// </summary>
        private string _finalImageType = "";
        
        /// <summary>
        /// 是否执行了类型转换
        /// </summary>
        private bool _typeConversionExecuted = false;
        
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
                
                // 执行全局直方图均衡
                var outputImage = await Task.Run(() => ExecuteGlobalEqualization(inputImage));
                
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
                return CreateFailureResult($"直方图均衡处理失败: {ex.Message}", ex);
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
                
            // 检查是否为灰度图像
            if (inputImage.Channels != 1)
                throw new ArgumentException($"直方图均衡仅支持灰度图像，当前图像通道数: {inputImage.Channels}");
        }
        
        /// <summary>
        /// 执行全局直方图均衡
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>均衡后的图像</returns>
        private VisionImage ExecuteGlobalEqualization(VisionImage inputImage)
        {
            HObject outputImage = null;
            
            try
            {
                // 检查输入图像是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入图像对象为空");
                
                // 获取原始图像类型
                HOperatorSet.GetImageType(inputImage.HImage, out HTuple originalType);
                _originalImageType = originalType.S;
                
                // 使用Halcon官方EquHistoImage算子进行全局直方图均衡
                HOperatorSet.EquHistoImage(inputImage.HImage, out outputImage);
                
                // 获取EquHistoImage处理后的实际图像类型
                HOperatorSet.GetImageType(outputImage, out HTuple processedType);
                _processedImageType = processedType.S;
                
                // 如果需要保持图像类型，进行类型转换
                if (PreserveImageType)
                {
                    outputImage = PreserveOriginalImageType(inputImage.HImage, outputImage);
                }
                
                // 获取最终输出图像类型
                HOperatorSet.GetImageType(outputImage, out HTuple finalType);
                _finalImageType = finalType.S;
                
                // 记录是否执行了类型转换
                _typeConversionExecuted = _processedImageType != _finalImageType;
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("全局直方图均衡处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException($"Halcon直方图均衡操作失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 保持原始图像类型
        /// </summary>
        /// <param name="originalImage">原始图像</param>
        /// <param name="processedImage">处理后图像</param>
        /// <returns>类型转换后的图像</returns>
        private HObject PreserveOriginalImageType(HObject originalImage, HObject processedImage)
        {
            try
            {
                // 获取原始图像类型
                HOperatorSet.GetImageType(originalImage, out HTuple imageType);
                
                // 如果类型不同，进行转换
                HOperatorSet.GetImageType(processedImage, out HTuple processedType);
                if (imageType.S != processedType.S)
                {
                    // 记录类型转换操作
                    System.Diagnostics.Debug.WriteLine($"[直方图均衡] 图像类型转换: {processedType.S} -> {imageType.S}");
                    
                    HOperatorSet.ConvertImageType(processedImage, out HObject convertedImage, imageType.S);
                    return convertedImage;
                }
                else
                {
                    // 记录无需转换
                    System.Diagnostics.Debug.WriteLine($"[直方图均衡] 图像类型保持不变: {imageType.S}");
                }
                
                return processedImage;
            }
            catch (Exception ex)
            {
                // 如果转换失败，记录错误并返回原图像
                System.Diagnostics.Debug.WriteLine($"[直方图均衡] 类型转换失败: {ex.Message}");
                return processedImage;
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
                ["处理算法"] = "EquHistoImage 全局直方图均衡",
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = "直方图均衡",
                ["算法特点"] = "全局直方图线性化，增强整体对比度",
                ["适用场景"] = "整体亮度分布不均的灰度图像"
            };
            
            // 添加真实的图像类型处理信息
            measurements["原始图像类型"] = string.IsNullOrEmpty(_originalImageType) ? "未检测" : _originalImageType;
            measurements["EquHistoImage实际输出"] = string.IsNullOrEmpty(_processedImageType) ? "未检测" : _processedImageType;
            measurements["最终输出类型"] = string.IsNullOrEmpty(_finalImageType) ? "未检测" : _finalImageType;
            measurements["保持图像类型"] = PreserveImageType ? "已启用" : "已禁用";
            measurements["类型转换执行"] = _typeConversionExecuted ? "已执行" : "未执行";
            
            // 添加转换状态详细信息
            if (_typeConversionExecuted && PreserveImageType)
            {
                measurements["转换状态"] = $"{_processedImageType} → {_finalImageType} 转换成功";
            }
            else if (PreserveImageType && !_typeConversionExecuted)
            {
                measurements["转换状态"] = "无需转换（类型相同）";
            }
            else if (!PreserveImageType)
            {
                measurements["转换状态"] = $"保持EquHistoImage输出类型：{_processedImageType}";
            }
            else
            {
                measurements["转换状态"] = "转换状态未知";
            }
            
            return measurements;
        }
        
        #endregion
    }
}