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
    /// 基于局部统计的自适应阈值二值化处理器
    /// 使用Halcon DynThreshold算子，根据局部统计特征进行动态阈值处理
    /// </summary>
    public class DynThresholdProcessor : VisionProcessorBase
    {
        #region 图像类型跟踪字段
        
        /// <summary>
        /// 原始图像类型
        /// </summary>
        private string _originalImageType = "";
        
        /// <summary>
        /// 处理后图像类型
        /// </summary>
        private string _processedImageType = "";
        
        /// <summary>
        /// 最终图像类型
        /// </summary>
        private string _finalImageType = "";
        
        /// <summary>
        /// 类型转换是否已执行
        /// </summary>
        private bool _typeConversionExecuted = false;
        
        #endregion
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "基于局部统计的自适应阈值二值化";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 窗口大小
        /// </summary>
        [Parameter("窗口大小", "局部统计计算的窗口大小，影响统计区域范围。较小窗口适应局部变化，较大窗口更稳定", 3, 99, DecimalPlaces = 0, Group = "参数设置")]
        public int WindowSize { get; set; } = 15;
        
        /// <summary>
        /// 统计方法
        /// </summary>
        [Parameter("统计方法", "局部统计的计算方法，影响阈值的计算基础", Group = "参数设置")]
        public StatisticalMethod Method { get; set; } = StatisticalMethod.Mean;
        
        /// <summary>
        /// 阈值偏移
        /// </summary>
        [Parameter("阈值偏移", "在局部统计值基础上的偏移量。正值提高阈值，负值降低阈值", -100, 100, DecimalPlaces = 0, Group = "参数设置")]
        public int ThresholdOffset { get; set; } = 5;
        
        /// <summary>
        /// 提取模式
        /// </summary>
        [Parameter("提取模式", "选择提取亮区域还是暗区域", Group = "参数设置")]
        public ExtractionMode LightDark { get; set; } = ExtractionMode.Light;
        
        /// <summary>
        /// 边界处理
        /// </summary>
        [Parameter("边界处理", "图像边界区域的处理方式", Group = "参数设置")]
        public BorderMode BorderHandling { get; set; } = BorderMode.Mirror;
        
        /// <summary>
        /// 反转输出
        /// </summary>
        [Parameter("反转输出", "是否反转二值化结果", Group = "其他设置")]
        public bool InvertOutput { get; set; } = false;
        
        /// <summary>
        /// 保持图像类型
        /// </summary>
        [Parameter("保持图像类型", "是否保持与原图像相同的数据类型", Group = "其他设置")]
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
                
                // 执行基于局部统计的自适应阈值二值化
                var outputImage = await Task.Run(() => ExecuteDynThreshold(inputImage));
                
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
                return CreateFailureResult($"基于局部统计的自适应阈值二值化处理失败: {ex.Message}", ex);
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
                
            // 参数验证和自动调整
            ValidateAndAdjustParameters();
        }
        
        /// <summary>
        /// 参数验证和自动调整
        /// </summary>
        private void ValidateAndAdjustParameters()
        {
            // 窗口大小必须为奇数且大于等于3
            if (WindowSize % 2 == 0) WindowSize++;
            WindowSize = Math.Max(3, Math.Min(99, WindowSize));
            
            // 阈值偏移范围限制
            ThresholdOffset = Math.Max(-100, Math.Min(100, ThresholdOffset));
        }
        
        /// <summary>
        /// 执行基于局部统计的自适应阈值二值化算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>二值化后的图像</returns>
        private VisionImage ExecuteDynThreshold(VisionImage inputImage)
        {
            HObject outputImage = null;
            HObject regions = null;
            HObject statisticalImage = null;
            
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
                
                // 验证图像尺寸是否足够大
                if (width.I < WindowSize || height.I < WindowSize)
                    throw new ArgumentException($"图像尺寸({width.I}×{height.I})小于窗口尺寸({WindowSize}×{WindowSize})");
                
                // 检查图像是否为灰度图像
                HOperatorSet.CountChannels(inputImage.HImage, out HTuple channels);
                if (channels.I != 1)
                    throw new ArgumentException($"DynThreshold算子仅支持灰度图像，当前图像通道数: {channels.I}");
                
                // 直接使用原始图像进行统计处理
                var processedImage = inputImage.HImage;
                
                // 生成统计图像（阈值图像）
                statisticalImage = GenerateStatisticalImage(processedImage);
                
                // 执行DynThreshold算子，基于局部统计特征进行动态阈值
                try
                {
                    HOperatorSet.DynThreshold(processedImage, statisticalImage, out regions,
                        ThresholdOffset, LightDark.GetHalconString());
                }
                catch (HalconException halconEx)
                {
                    throw new InvalidOperationException($"DynThreshold算子调用失败。参数: 窗口{WindowSize}, 统计方法:{Method.GetDisplayName()}, 偏移:{ThresholdOffset}, 模式:{LightDark.GetHalconString()}。Halcon错误: {halconEx.Message}", halconEx);
                }
                
                // 检查是否产生了有效区域
                if (regions == null)
                    throw new InvalidOperationException("DynThreshold算子未产生有效区域");
                
                // 将区域转换为二值图像
                HOperatorSet.RegionToBin(regions, out outputImage, 
                    255, 0, width.I, height.I);
                
                // 获取处理后的图像类型
                HOperatorSet.GetImageType(outputImage, out HTuple processedType);
                _processedImageType = processedType.S;
                
                // 清理临时资源
                regions?.Dispose();
                regions = null;
                
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
                
                // 获取最终输出图像类型
                HOperatorSet.GetImageType(outputImage, out HTuple finalType);
                _finalImageType = finalType.S;
                
                // 记录是否执行了类型转换
                _typeConversionExecuted = _processedImageType != _finalImageType;
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("基于局部统计的自适应阈值二值化处理失败，输出图像为空");
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                regions?.Dispose();
                statisticalImage?.Dispose();
                throw new InvalidOperationException($"Halcon DynThreshold操作失败: {ex.Message}", ex);
            }
            finally
            {
                statisticalImage?.Dispose();
            }
        }
        
        /// <summary>
        /// 生成统计图像（阈值图像）
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>统计图像</returns>
        private HObject GenerateStatisticalImage(HObject inputImage)
        {
            try
            {
                switch (Method)
                {
                    case StatisticalMethod.Mean:
                        HOperatorSet.MeanImage(inputImage, out HObject meanImg, WindowSize, WindowSize);
                        return meanImg;
                        
                    case StatisticalMethod.Median:
                        HOperatorSet.MedianImage(inputImage, out HObject medianImg, 
                            "circle", WindowSize, BorderHandling.GetHalconString());
                        return medianImg;
                        
                    case StatisticalMethod.Gauss:
                        // GaussFilter需要sigma参数，不是窗口大小
                        // 将窗口大小转换为合适的sigma值：sigma = windowSize / 6.0
                        // 这个公式确保高斯核在3sigma范围内覆盖窗口区域
                        double sigma = Math.Max(0.5, WindowSize / 6.0);
                        HOperatorSet.GaussFilter(inputImage, out HObject gaussImg, sigma);
                        return gaussImg;
                        
                    default:
                        HOperatorSet.MeanImage(inputImage, out HObject defaultImg, WindowSize, WindowSize);
                        return defaultImg;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"生成统计图像失败: {ex.Message}", ex);
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
                    ["窗口大小"] = WindowSize,
                    ["统计方法"] = Method.GetDisplayName(),
                    ["统计参数"] = GetStatisticalParameter(),
                    ["阈值偏移"] = ThresholdOffset,
                    ["提取模式"] = LightDark.GetDisplayName(),
                    ["边界处理"] = BorderHandling.GetDisplayName(),
                    ["反转输出"] = InvertOutput ? "是" : "否",
                    ["保持图像类型"] = PreserveImageType ? "是" : "否",
                    ["原始图像类型"] = _originalImageType,
                    ["DynThreshold处理后类型"] = _processedImageType,
                    ["最终输出类型"] = _finalImageType,
                    ["类型转换执行状态"] = _typeConversionExecuted ? "已执行" : "未执行",
                    ["类型转换详情"] = GetTypeConversionDetails(),
                    ["检测到的区域数"] = regionCount,
                    ["总面积(像素)"] = totalArea,
                    ["面积占比(%)"] = Math.Round(areaRatio, 2),
                    ["算法特点"] = "基于局部统计的动态阈值",
                    ["适用场景"] = "光照渐变、复杂背景图像",
                    ["与VarThreshold差异"] = "基于统计值比较，参数更直观",
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "基于局部统计的自适应阈值二值化"
                };
                
                return measurements;
            }
            catch (Exception ex)
            {
                // 如果统计失败，返回基础信息
                return new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["窗口大小"] = WindowSize,
                    ["统计方法"] = Method.GetDisplayName(),
                    ["阈值偏移"] = ThresholdOffset,
                    ["提取模式"] = LightDark.GetDisplayName(),
                    ["保持图像类型"] = PreserveImageType ? "是" : "否",
                    ["原始图像类型"] = _originalImageType,
                    ["DynThreshold处理后类型"] = _processedImageType,
                    ["最终输出类型"] = _finalImageType,
                    ["类型转换执行状态"] = _typeConversionExecuted ? "已执行" : "未执行",
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["算法类型"] = "基于局部统计的自适应阈值二值化",
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
        
        /// <summary>
        /// 获取当前统计方法的具体参数
        /// </summary>
        /// <returns>参数描述字符串</returns>
        private string GetStatisticalParameter()
        {
            return Method switch
            {
                StatisticalMethod.Mean => $"窗口 {WindowSize}×{WindowSize}",
                StatisticalMethod.Median => $"圆形窗口 r={WindowSize/2}",
                StatisticalMethod.Gauss => $"sigma={Math.Round(Math.Max(0.5, WindowSize / 6.0), 2)}",
                _ => $"窗口 {WindowSize}×{WindowSize}"
            };
        }
        
        /// <summary>
        /// 获取类型转换详细信息
        /// </summary>
        /// <returns>类型转换详情字符串</returns>
        private string GetTypeConversionDetails()
        {
            if (!_typeConversionExecuted)
            {
                return $"未转换，保持{_processedImageType}类型";
            }
            
            if (_originalImageType == _finalImageType)
            {
                return $"成功转换，从{_processedImageType}恢复为{_originalImageType}";
            }
            else
            {
                return $"转换异常，期望{_originalImageType}实际{_finalImageType}";
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 统计方法枚举
    /// </summary>
    public enum StatisticalMethod
    {
        /// <summary>局部均值</summary>
        Mean,
        /// <summary>局部中值</summary>
        Median,
        /// <summary>高斯滤波</summary>
        Gauss
    }
    
    
    /// <summary>
    /// 边界处理模式枚举
    /// </summary>
    public enum BorderMode
    {
        /// <summary>镜像填充</summary>
        Mirror,
        /// <summary>常数填充</summary>
        Constant,
        /// <summary>边界延续</summary>
        Continued
    }
    
    /// <summary>
    /// 统计方法扩展方法
    /// </summary>
    public static class StatisticalMethodExtensions
    {
        /// <summary>
        /// 获取统计方法的中文显示名称
        /// </summary>
        public static string GetDisplayName(this StatisticalMethod method)
        {
            return method switch
            {
                StatisticalMethod.Mean => "局部均值",
                StatisticalMethod.Median => "局部中值",
                StatisticalMethod.Gauss => "高斯滤波",
                _ => method.ToString()
            };
        }
    }
    
    
    /// <summary>
    /// 边界处理扩展方法
    /// </summary>
    public static class BorderModeExtensions
    {
        /// <summary>
        /// 获取边界处理的中文显示名称
        /// </summary>
        public static string GetDisplayName(this BorderMode mode)
        {
            return mode switch
            {
                BorderMode.Mirror => "镜像填充",
                BorderMode.Constant => "常数填充",
                BorderMode.Continued => "边界延续",
                _ => mode.ToString()
            };
        }
        
        /// <summary>
        /// 获取Halcon参数字符串
        /// </summary>
        public static string GetHalconString(this BorderMode mode)
        {
            return mode switch
            {
                BorderMode.Mirror => "mirrored",
                BorderMode.Constant => "constant",
                BorderMode.Continued => "continued",
                _ => "mirrored"
            };
        }
    }
}