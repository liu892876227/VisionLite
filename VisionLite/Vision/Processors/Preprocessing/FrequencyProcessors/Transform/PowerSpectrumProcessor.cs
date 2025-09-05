using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Transform
{
    /// <summary>
    /// 功率谱分析处理器
    /// 实现幅度谱、功率谱和相位谱的计算与分析
    /// </summary>
    public class PowerSpectrumProcessor : FrequencyProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "功率谱分析";
        
        /// <summary>
        /// 分析类型
        /// </summary>
        [Parameter("分析类型", "选择要计算的频谱类型", Group = "分析参数")]
        public SpectrumAnalysisType AnalysisType { get; set; } = SpectrumAnalysisType.PowerSpectrum;
        
        /// <summary>
        /// 是否应用FFT
        /// </summary>
        [Parameter("自动FFT", "如果输入不是复数图像，是否自动执行FFT", Group = "分析参数")]
        public bool AutoFFT { get; set; } = true;
        
        /// <summary>
        /// 是否归一化
        /// </summary>
        [Parameter("归一化", "是否对频谱进行归一化处理", Group = "分析参数")]
        public bool NormalizeSpectrum { get; set; } = true;
        
        /// <summary>
        /// 对数变换
        /// </summary>
        [Parameter("对数变换", "是否应用对数变换以增强显示效果", Group = "显示参数")]
        public bool LogTransform { get; set; } = true;
        
        /// <summary>
        /// 对数底数
        /// </summary>
        [Parameter("对数底数", "对数变换的底数（e=自然对数，10=常用对数）", Group = "显示参数")]
        public LogBase LogBase { get; set; } = LogBase.Natural;
        
        /// <summary>
        /// 动态范围压缩
        /// </summary>
        [Parameter("动态范围压缩", "是否压缩动态范围以改善对比度", Group = "显示参数")]
        public bool CompressDynamicRange { get; set; } = true;
        
        /// <summary>
        /// 压缩因子
        /// </summary>
        [Parameter("压缩因子", "动态范围压缩的强度（0.1-1.0）", 0.1, 1.0, Step = 0.1, Group = "显示参数")]
        public double CompressionFactor { get; set; } = 0.5;
        
        #endregion
        
        #region 私有字段
        
        /// <summary>
        /// 是否执行了自动FFT
        /// </summary>
        private bool _autoFFTExecuted = false;
        
        /// <summary>
        /// 输入图像是否为复数类型
        /// </summary>
        private bool _inputIsComplex = false;
        
        /// <summary>
        /// 频谱分析结果类型
        /// </summary>
        private string _spectrumResultType = "";
        
        /// <summary>
        /// 归一化执行状态
        /// </summary>
        private bool _normalizationExecuted = false;
        
        #endregion
        
        #region 主要方法
        
        /// <summary>
        /// 执行频域处理
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>频域处理结果</returns>
        protected override FrequencyProcessResult ExecuteFrequencyProcess(VisionImage inputImage)
        {
            HObject complexImage = null;
            VisionImage spectrumResult = null;
            
            try
            {
                // 1. 检查输入图像类型
                _inputIsComplex = CheckIfComplexImage(inputImage);
                
                // 2. 获取或生成复数频谱
                complexImage = GetComplexSpectrum(inputImage);
                
                // 3. 根据分析类型计算频谱
                spectrumResult = CalculateSpectrum(complexImage, AnalysisType);
                
                // 4. 应用后处理
                spectrumResult = ApplyPostProcessing(spectrumResult);
                
                // 5. 安全获取结果图像类型
                try
                {
                    if (spectrumResult?.HImage != null)
                    {
                        HOperatorSet.GetImageType(spectrumResult.HImage, out HTuple resultType);
                        _spectrumResultType = resultType.S ?? "unknown";
                    }
                    else
                    {
                        _spectrumResultType = "null_result";
                    }
                }
                catch
                {
                    _spectrumResultType = "type_check_failed";
                }
                
                // 6. 计算频谱统计信息
                var statistics = CalculateSpectrumStatistics(spectrumResult, complexImage);
                
                return new FrequencyProcessResult
                {
                    ProcessedImage = spectrumResult,
                    SpectrumImage = spectrumResult, // 功率谱分析的结果就是频谱图像
                    ComplexImage = complexImage,
                    Statistics = statistics
                };
            }
            catch (Exception ex)
            {
                // 清理资源
                if (!_inputIsComplex && complexImage != inputImage.HImage)
                    complexImage?.Dispose();
                spectrumResult?.Dispose();
                
                throw new InvalidOperationException($"功率谱分析失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 检查图像是否为复数类型
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>是否为复数类型</returns>
        private bool CheckIfComplexImage(VisionImage image)
        {
            try
            {
                HOperatorSet.GetImageType(image.HImage, out HTuple imageType);
                return imageType.S.ToLower().Contains("complex");
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取复数频谱
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>复数频谱</returns>
        private HObject GetComplexSpectrum(VisionImage inputImage)
        {
            try
            {
                if (_inputIsComplex)
                {
                    // 输入已经是复数图像，直接使用
                    _autoFFTExecuted = false;
                    return inputImage.HImage;
                }
                else if (AutoFFT)
                {
                    // 对实数图像执行FFT
                    HOperatorSet.FftImage(inputImage.HImage, out HObject complexSpectrum);
                    _autoFFTExecuted = true;
                    return complexSpectrum;
                }
                else
                {
                    // 将实数图像转换为复数图像（虚部为0）
                    HOperatorSet.GenImageConst(out HObject zeroImage, "real", inputImage.Width, inputImage.Height);
                    HOperatorSet.RealToComplex(inputImage.HImage, zeroImage, out HObject pseudoComplex);
                    zeroImage?.Dispose();
                    _autoFFTExecuted = false;
                    return pseudoComplex;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取复数频谱失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 计算频谱
        /// </summary>
        /// <param name="complexImage">复数图像</param>
        /// <param name="analysisType">分析类型</param>
        /// <returns>频谱结果</returns>
        private VisionImage CalculateSpectrum(HObject complexImage, SpectrumAnalysisType analysisType)
        {
            HObject spectrumImage = null;
            
            try
            {
                switch (analysisType)
                {
                    case SpectrumAnalysisType.MagnitudeSpectrum:
                        // 幅度谱：sqrt(real^2 + imag^2)
                        HOperatorSet.ComplexToReal(complexImage, out HObject magReal, out HObject magImag);
                        HOperatorSet.MultImage(magReal, magReal, out HObject realSquared, 1.0, 0.0);
                        HOperatorSet.MultImage(magImag, magImag, out HObject imagSquared, 1.0, 0.0);
                        HOperatorSet.AddImage(realSquared, imagSquared, out HObject sumSquares, 1.0, 0.0);
                        HOperatorSet.SqrtImage(sumSquares, out spectrumImage);
                        
                        // 清理临时对象
                        magReal?.Dispose();
                        magImag?.Dispose();
                        realSquared?.Dispose();
                        imagSquared?.Dispose();
                        sumSquares?.Dispose();
                        break;
                        
                    case SpectrumAnalysisType.PowerSpectrum:
                        // 功率谱：real^2 + imag^2
                        HOperatorSet.PowerReal(complexImage, out spectrumImage);
                        break;
                        
                    case SpectrumAnalysisType.PhaseSpectrum:
                        // 相位谱：atan2(imag, real)
                        HOperatorSet.ComplexToReal(complexImage, out HObject phaseReal, out HObject phaseImag);
                        HOperatorSet.Atan2Image(phaseImag, phaseReal, out spectrumImage);
                        phaseReal?.Dispose();
                        phaseImag?.Dispose();
                        break;
                        
                    case SpectrumAnalysisType.LogMagnitudeSpectrum:
                        // 对数幅度谱
                        HOperatorSet.ComplexToReal(complexImage, out HObject logMagReal, out HObject logMagImag);
                        HOperatorSet.MultImage(logMagReal, logMagReal, out HObject logRealSquared, 1.0, 0.0);
                        HOperatorSet.MultImage(logMagImag, logMagImag, out HObject logImagSquared, 1.0, 0.0);
                        HOperatorSet.AddImage(logRealSquared, logImagSquared, out HObject logSumSquares, 1.0, 0.0);
                        HOperatorSet.SqrtImage(logSumSquares, out HObject magnitude);
                        
                        // 应用对数变换
                        if (LogBase == LogBase.Natural)
                            HOperatorSet.LogImage(magnitude, out spectrumImage, "e");
                        else
                            HOperatorSet.LogImage(magnitude, out spectrumImage, "10");
                        
                        // 清理临时对象
                        logMagReal?.Dispose();
                        logMagImag?.Dispose();
                        logRealSquared?.Dispose();
                        logImagSquared?.Dispose();
                        logSumSquares?.Dispose();
                        magnitude?.Dispose();
                        break;
                        
                    case SpectrumAnalysisType.LogPowerSpectrum:
                        // 对数功率谱
                        HOperatorSet.PowerReal(complexImage, out HObject powerImage);
                        
                        if (LogBase == LogBase.Natural)
                            HOperatorSet.PowerLn(complexImage, out spectrumImage);
                        else
                            HOperatorSet.LogImage(powerImage, out spectrumImage, "10");
                        
                        powerImage?.Dispose();
                        break;
                        
                    default:
                        // 默认使用功率谱
                        HOperatorSet.PowerReal(complexImage, out spectrumImage);
                        break;
                }
                
                return new VisionImage(spectrumImage);
            }
            catch (Exception ex)
            {
                spectrumImage?.Dispose();
                throw new InvalidOperationException($"频谱计算失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 应用后处理
        /// </summary>
        /// <param name="spectrumImage">频谱图像</param>
        /// <returns>后处理后的图像</returns>
        private VisionImage ApplyPostProcessing(VisionImage spectrumImage)
        {
            HObject processedImage = spectrumImage.HImage;
            
            try
            {
                // 1. 归一化
                if (NormalizeSpectrum)
                {
                    processedImage = NormalizeImage(processedImage);
                    _normalizationExecuted = true;
                }
                else
                {
                    _normalizationExecuted = false;
                }
                
                // 2. 动态范围压缩
                if (CompressDynamicRange)
                {
                    processedImage = CompressImageDynamicRange(processedImage, CompressionFactor);
                }
                
                // 3. 对数变换（如果分析类型不是对数类型且需要对数变换）
                if (LogTransform && 
                    AnalysisType != SpectrumAnalysisType.LogMagnitudeSpectrum && 
                    AnalysisType != SpectrumAnalysisType.LogPowerSpectrum)
                {
                    HObject logImage;
                    if (LogBase == LogBase.Natural)
                        HOperatorSet.LogImage(processedImage, out logImage, "e");
                    else
                        HOperatorSet.LogImage(processedImage, out logImage, "10");
                        
                    if (processedImage != spectrumImage.HImage)
                        processedImage?.Dispose();
                    processedImage = logImage;
                }
                
                // 如果处理后的图像与原图像相同，直接返回
                if (processedImage == spectrumImage.HImage)
                {
                    return spectrumImage;
                }
                else
                {
                    return new VisionImage(processedImage);
                }
            }
            catch (Exception ex)
            {
                if (processedImage != spectrumImage.HImage)
                    processedImage?.Dispose();
                throw new InvalidOperationException($"频谱后处理失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 归一化图像
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>归一化后的图像</returns>
        private HObject NormalizeImage(HObject image)
        {
            try
            {
                // 获取图像的最值
                HOperatorSet.MinMaxGray(image, image, 0, out HTuple min, out HTuple max, out HTuple range);
                
                if (range.D <= 0)
                {
                    // 如果范围为0，返回原图像
                    return image;
                }
                
                // 归一化到0-255范围
                double scale = 255.0 / range.D;
                double offset = -min.D * scale;
                
                HOperatorSet.ScaleImage(image, out HObject normalizedImage, scale, offset);
                return normalizedImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"图像归一化失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 压缩图像动态范围
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="compressionFactor">压缩因子</param>
        /// <returns>压缩后的图像</returns>
        private HObject CompressImageDynamicRange(HObject image, double compressionFactor)
        {
            try
            {
                // 使用伽马校正进行动态范围压缩
                double gamma = compressionFactor;
                HOperatorSet.GammaImage(image, out HObject compressedImage, gamma, 0, 255, 0, 255);
                return compressedImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"动态范围压缩失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 计算频谱统计信息
        /// </summary>
        /// <param name="spectrumImage">频谱图像</param>
        /// <param name="complexImage">复数图像</param>
        /// <returns>统计信息</returns>
        private Dictionary<string, double> CalculateSpectrumStatistics(VisionImage spectrumImage, HObject complexImage)
        {
            var statistics = new Dictionary<string, double>();
            
            try
            {
                // 频谱图像的基本统计
                HOperatorSet.Intensity(spectrumImage.HImage, spectrumImage.HImage, 
                    out HTuple meanValue, out HTuple stdDev);
                HOperatorSet.MinMaxGray(spectrumImage.HImage, spectrumImage.HImage, 0,
                    out HTuple min, out HTuple max, out HTuple range);
                
                statistics["频谱平均值"] = meanValue.D;
                statistics["频谱标准差"] = stdDev.D;
                statistics["频谱最小值"] = min.D;
                statistics["频谱最大值"] = max.D;
                statistics["频谱动态范围"] = range.D;
                
                // 复数图像的统计（如果可用）
                if (complexImage != null)
                {
                    // 计算总能量
                    HOperatorSet.PowerReal(complexImage, out HObject powerSpectrum);
                    HOperatorSet.Intensity(powerSpectrum, powerSpectrum, 
                        out HTuple totalEnergy, out HTuple _);
                    statistics["总频域能量"] = totalEnergy.D;
                    powerSpectrum?.Dispose();
                    
                    // 计算零频率分量（直流分量）
                    HOperatorSet.GetImageSize(complexImage, out HTuple width, out HTuple height);
                    int centerX = width.I / 2;
                    int centerY = height.I / 2;
                    
                    HOperatorSet.GetGrayval(complexImage, centerY, centerX, out HTuple dcComponent);
                    statistics["零频率分量"] = dcComponent.D;
                }
                
                // 分析类型特定的统计
                switch (AnalysisType)
                {
                    case SpectrumAnalysisType.PhaseSpectrum:
                        statistics["相位范围(弧度)"] = range.D;
                        statistics["相位范围(度)"] = range.D * 180.0 / Math.PI;
                        break;
                        
                    case SpectrumAnalysisType.PowerSpectrum:
                    case SpectrumAnalysisType.LogPowerSpectrum:
                        if (max.D > 0 && min.D > 0)
                        {
                            statistics["功率动态范围(dB)"] = 10 * Math.Log10(max.D / min.D);
                        }
                        break;
                        
                    case SpectrumAnalysisType.MagnitudeSpectrum:
                    case SpectrumAnalysisType.LogMagnitudeSpectrum:
                        if (max.D > 0 && min.D > 0)
                        {
                            statistics["幅度动态范围(dB)"] = 20 * Math.Log10(max.D / min.D);
                        }
                        break;
                }
                
                return statistics;
            }
            catch (Exception ex)
            {
                // 如果统计计算失败，返回基础信息
                statistics["统计计算状态"] = -1;
                statistics["错误信息"] = ex.Message.GetHashCode();
                return statistics;
            }
        }
        
        /// <summary>
        /// 创建频域测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="result">频域处理结果</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        protected override Dictionary<string, object> CreateFrequencyMeasurements(
            VisionImage inputImage, FrequencyProcessResult result, TimeSpan processingTime)
        {
            // 获取基础测量结果
            var measurements = base.CreateFrequencyMeasurements(inputImage, result, processingTime);
            
            // 添加功率谱分析特有的信息
            measurements["分析类型"] = AnalysisType.ToString();
            measurements["输入图像类型"] = _inputIsComplex ? "复数图像" : "实数图像";
            measurements["自动FFT执行"] = _autoFFTExecuted ? "已执行" : "未执行";
            measurements["频谱结果类型"] = string.IsNullOrEmpty(_spectrumResultType) ? "未检测" : _spectrumResultType;
            measurements["归一化处理"] = NormalizeSpectrum ? "已启用" : "已禁用";
            measurements["归一化执行状态"] = _normalizationExecuted ? "已执行" : "未执行";
            measurements["对数变换"] = LogTransform ? "已启用" : "已禁用";
            measurements["对数底数"] = LogBase.ToString();
            measurements["动态范围压缩"] = CompressDynamicRange ? "已启用" : "已禁用";
            
            if (CompressDynamicRange)
            {
                measurements["压缩因子"] = CompressionFactor;
            }
            
            return measurements;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 频谱分析类型枚举
    /// </summary>
    public enum SpectrumAnalysisType
    {
        /// <summary>幅度谱</summary>
        MagnitudeSpectrum,
        
        /// <summary>功率谱</summary>
        PowerSpectrum,
        
        /// <summary>相位谱</summary>
        PhaseSpectrum,
        
        /// <summary>对数幅度谱</summary>
        LogMagnitudeSpectrum,
        
        /// <summary>对数功率谱</summary>
        LogPowerSpectrum
    }
    
    /// <summary>
    /// 对数底数枚举
    /// </summary>
    public enum LogBase
    {
        /// <summary>自然对数（e）</summary>
        Natural,
        
        /// <summary>常用对数（10）</summary>
        Common
    }
}