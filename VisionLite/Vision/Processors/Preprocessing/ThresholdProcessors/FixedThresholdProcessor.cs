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
    /// 固定阈值二值化处理器
    /// 使用用户指定的阈值范围进行二值化处理
    /// </summary>
    public class FixedThresholdProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "固定阈值二值化";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 最小灰度值
        /// </summary>
        [Parameter("最小灰度值", "阈值范围的最小值，低于此值的像素将被设为0", 0, 255, DecimalPlaces = 0, Group = "参数设置")]
        public int MinGrayValue { get; set; } = 128;
        
        /// <summary>
        /// 最大灰度值
        /// </summary>
        [Parameter("最大灰度值", "阈值范围的最大值，高于此值的像素将被设为0", 0, 255, DecimalPlaces = 0, Group = "参数设置")]
        public int MaxGrayValue { get; set; } = 255;
        
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
        
        #region 私有字段
        
        /// <summary>
        /// 原始图像类型（用于测量结果显示）
        /// </summary>
        private string _originalImageType = "";
        
        /// <summary>
        /// RegionToBin处理后的实际图像类型
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
                
                // 执行固定阈值二值化
                var outputImage = await Task.Run(() => ExecuteFixedThreshold(inputImage));
                
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
                return CreateFailureResult($"固定阈值二值化处理失败: {ex.Message}", ex);
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
                
            // 智能调整阈值范围
            AdjustThresholdRange();
        }
        
        /// <summary>
        /// 调整阈值范围为有效值
        /// </summary>
        private void AdjustThresholdRange()
        {
            // 确保阈值在有效范围内
            MinGrayValue = Math.Max(0, Math.Min(255, MinGrayValue));
            MaxGrayValue = Math.Max(0, Math.Min(255, MaxGrayValue));
            
            // 如果最小值大于最大值，交换它们
            if (MinGrayValue > MaxGrayValue)
            {
                (MinGrayValue, MaxGrayValue) = (MaxGrayValue, MinGrayValue);
            }
        }
        
        /// <summary>
        /// 执行固定阈值二值化算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>二值化后的图像</returns>
        private VisionImage ExecuteFixedThreshold(VisionImage inputImage)
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
                
                // 获取图像尺寸
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);
                
                // 使用Halcon的Threshold算子获取区域，然后转换为二值图像
                HOperatorSet.Threshold(inputImage.HImage, out HObject regions, MinGrayValue, MaxGrayValue);
                
                // 将区域转换为二值图像
                HOperatorSet.RegionToBin(regions, out outputImage, 255, 0, width.I, height.I);
                
                // 清理临时区域
                regions?.Dispose();
                
                // 如果需要反转输出
                if (InvertOutput)
                {
                    HOperatorSet.InvertImage(outputImage, out HObject invertedImage);
                    outputImage?.Dispose();
                    outputImage = invertedImage;
                }
                
                // 获取RegionToBin处理后的实际图像类型
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
                    throw new InvalidOperationException("固定阈值二值化处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException("Halcon固定阈值二值化操作失败", ex);
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
                    ["使用阈值范围"] = $"{MinGrayValue} - {MaxGrayValue}",
                    ["反转输出"] = InvertOutput ? "是" : "否",
                    ["检测到的区域数"] = regionCount,
                    ["总面积(像素)"] = totalArea,
                    ["面积占比(%)"] = Math.Round(areaRatio, 2),
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "固定阈值二值化"
                };
                
                // 添加真实的图像类型处理信息
                measurements["原始图像类型"] = string.IsNullOrEmpty(_originalImageType) ? "未检测" : _originalImageType;
                measurements["RegionToBin实际输出"] = string.IsNullOrEmpty(_processedImageType) ? "未检测" : _processedImageType;
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
                    measurements["转换状态"] = $"保持RegionToBin输出类型：{_processedImageType}";
                }
                else
                {
                    measurements["转换状态"] = "转换状态未知";
                }
                
                return measurements;
            }
            catch (Exception ex)
            {
                // 如果统计失败，返回基础信息
                var fallbackMeasurements = new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["使用阈值范围"] = $"{MinGrayValue} - {MaxGrayValue}",
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "固定阈值二值化",
                    ["统计信息"] = $"统计失败: {ex.Message}"
                };
                
                // 添加图像类型信息（即使统计失败也能显示）
                fallbackMeasurements["原始图像类型"] = string.IsNullOrEmpty(_originalImageType) ? "未检测" : _originalImageType;
                fallbackMeasurements["RegionToBin实际输出"] = string.IsNullOrEmpty(_processedImageType) ? "未检测" : _processedImageType;
                fallbackMeasurements["最终输出类型"] = string.IsNullOrEmpty(_finalImageType) ? "未检测" : _finalImageType;
                
                return fallbackMeasurements;
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
}