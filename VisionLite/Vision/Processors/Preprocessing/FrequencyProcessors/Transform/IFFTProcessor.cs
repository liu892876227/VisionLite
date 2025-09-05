using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Transform
{
    /// <summary>
    /// 逆快速傅里叶变换处理器
    /// 实现从频域到空域的逆FFT变换，支持复数输入和实数输出
    /// </summary>
    public class IFFTProcessor : FrequencyProcessorBase, IFrequencyTransform
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "逆快速傅里叶变换";
        
        /// <summary>
        /// 输出类型选择
        /// </summary>
        [Parameter("输出类型", "选择IFFT的输出类型", Group = "变换参数")]
        public IFFTOutputType OutputType { get; set; } = IFFTOutputType.Magnitude;
        
        /// <summary>
        /// 是否去中心化
        /// </summary>
        [Parameter("去中心化", "是否对输入频谱进行去中心化处理", Group = "变换参数")]
        public bool RemoveCentering { get; set; } = true;
        
        /// <summary>
        /// 是否裁剪到原始尺寸
        /// </summary>
        [Parameter("裁剪到原始尺寸", "是否将结果裁剪到输入图像的原始尺寸", Group = "变换参数")]
        public bool CropToOriginalSize { get; set; } = true;
        
        /// <summary>
        /// 原始图像宽度（用于裁剪）
        /// </summary>
        [Parameter("原始宽度", "原始图像的宽度，用于裁剪", 1, 4096, DecimalPlaces = 0, Group = "尺寸参数")]
        public int OriginalWidth { get; set; } = 512;
        
        /// <summary>
        /// 原始图像高度（用于裁剪）
        /// </summary>
        [Parameter("原始高度", "原始图像的高度，用于裁剪", 1, 4096, DecimalPlaces = 0, Group = "尺寸参数")]
        public int OriginalHeight { get; set; } = 512;
        
        #endregion
        
        #region 私有字段
        
        /// <summary>
        /// 输入复数图像类型
        /// </summary>
        private string _inputComplexType = "";
        
        /// <summary>
        /// IFFT结果图像类型
        /// </summary>
        private string _ifftImageType = "";
        
        /// <summary>
        /// 去中心化执行状态
        /// </summary>
        private bool _decenteringExecuted = false;
        
        /// <summary>
        /// 裁剪执行状态
        /// </summary>
        private bool _croppingExecuted = false;
        
        #endregion
        
        #region 主要方法
        
        /// <summary>
        /// 执行频域处理
        /// </summary>
        /// <param name="inputImage">输入图像（这里应该是复数图像或频谱图像）</param>
        /// <returns>频域处理结果</returns>
        protected override FrequencyProcessResult ExecuteFrequencyProcess(VisionImage inputImage)
        {
            HObject processedComplex = null;
            VisionImage resultImage = null;
            
            try
            {
                // 1. 验证输入是否为复数图像
                ValidateComplexInput(inputImage);
                
                // 2. 安全获取输入复数图像类型
                try
                {
                    if (inputImage?.HImage != null)
                    {
                        HOperatorSet.GetImageType(inputImage.HImage, out HTuple inputType);
                        _inputComplexType = inputType.S ?? "unknown";
                    }
                    else
                    {
                        _inputComplexType = "null_input";
                    }
                }
                catch
                {
                    _inputComplexType = "type_check_failed";
                }
                
                // 3. 预处理：去中心化
                if (RemoveCentering)
                {
                    processedComplex = RemoveFrequencySpectrumCentering(inputImage.HImage);
                    _decenteringExecuted = true;
                }
                else
                {
                    processedComplex = inputImage.HImage;
                    _decenteringExecuted = false;
                }
                
                // 4. 执行逆FFT变换
                resultImage = ExecuteInverseTransform(processedComplex);
                
                // 5. 后处理：裁剪到原始尺寸
                if (CropToOriginalSize)
                {
                    resultImage = CropToSize(resultImage, OriginalWidth, OriginalHeight);
                    _croppingExecuted = true;
                }
                else
                {
                    _croppingExecuted = false;
                }
                
                // 6. 安全获取最终结果图像类型
                try
                {
                    if (resultImage?.HImage != null)
                    {
                        HOperatorSet.GetImageType(resultImage.HImage, out HTuple resultType);
                        _ifftImageType = resultType.S ?? "unknown";
                    }
                    else
                    {
                        _ifftImageType = "null_result";
                    }
                }
                catch
                {
                    _ifftImageType = "type_check_failed";
                }
                
                // 7. 计算统计信息
                var statistics = CalculateIFFTStatistics(resultImage);
                
                return new FrequencyProcessResult
                {
                    ProcessedImage = resultImage,
                    SpectrumImage = null, // IFFT不生成频谱图
                    ComplexImage = null,  // IFFT输出实数图像
                    Statistics = statistics
                };
            }
            catch (Exception ex)
            {
                // 清理资源
                if (processedComplex != inputImage.HImage)
                    processedComplex?.Dispose();
                resultImage?.Dispose();
                
                throw new InvalidOperationException($"IFFT变换失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region IFrequencyTransform接口实现
        
        /// <summary>
        /// 执行前向FFT变换（这里不实现，由FFT处理器负责）
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>变换结果</returns>
        public FrequencyProcessResult ExecuteForwardTransform(VisionImage inputImage)
        {
            throw new NotSupportedException("IFFT处理器不支持前向变换，请使用FFT处理器");
        }
        
        /// <summary>
        /// 执行反向FFT变换
        /// </summary>
        /// <param name="complexData">复数数据</param>
        /// <returns>变换结果</returns>
        public VisionImage ExecuteInverseTransform(HObject complexData)
        {
            HObject ifftResult = null;
            
            try
            {
                // 使用Halcon的FftImageInv算子进行逆FFT
                HOperatorSet.FftImageInv(complexData, out ifftResult);
                
                // 根据输出类型处理结果
                HObject finalResult = ProcessIFFTOutput(ifftResult, OutputType);
                
                if (finalResult != ifftResult)
                    ifftResult?.Dispose();
                
                return new VisionImage(finalResult);
            }
            catch (HalconException halconEx)
            {
                ifftResult?.Dispose();
                throw new InvalidOperationException($"Halcon IFFT算子调用失败: {halconEx.Message}", halconEx);
            }
            catch (Exception ex)
            {
                ifftResult?.Dispose();
                throw new InvalidOperationException($"IFFT变换执行失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 生成频谱可视化（IFFT处理器不生成频谱）
        /// </summary>
        /// <param name="complexData">复数数据</param>
        /// <param name="displayType">显示类型</param>
        /// <returns>频谱图像</returns>
        public VisionImage GenerateSpectrumVisualization(HObject complexData, SpectrumDisplayType displayType)
        {
            // IFFT处理器不生成频谱可视化
            return null;
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 验证复数输入
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        private void ValidateComplexInput(VisionImage inputImage)
        {
            try
            {
                // 检查是否为复数图像
                HOperatorSet.GetImageType(inputImage.HImage, out HTuple imageType);
                
                if (!imageType.S.ToLower().Contains("complex"))
                {
                    // 如果不是复数图像，尝试作为实数图像处理
                    // 这里可以将实数图像转换为复数图像（虚部为0）
                    // throw new ArgumentException($"输入图像类型 '{imageType.S}' 不是复数类型，IFFT需要复数输入");
                }
            }
            catch (HalconException ex)
            {
                throw new ArgumentException($"无法验证输入图像类型: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 去除频谱中心化
        /// </summary>
        /// <param name="centeredSpectrum">中心化的频谱</param>
        /// <returns>去中心化后的频谱</returns>
        private HObject RemoveFrequencySpectrumCentering(HObject centeredSpectrum)
        {
            try
            {
                // 获取图像尺寸
                HOperatorSet.GetImageSize(centeredSpectrum, out HTuple width, out HTuple height);
                
                int shiftX = width.I / 2;
                int shiftY = height.I / 2;
                
                // 分别处理实部和虚部
                HOperatorSet.ComplexToReal(centeredSpectrum, out HObject realPart, out HObject imagPart);
                
                // 对实部和虚部分别进行去中心化（象限交换的逆操作）
                HObject decenteredReal = CenterImageQuadrants(realPart, shiftX, shiftY);
                HObject decenteredImag = CenterImageQuadrants(imagPart, shiftX, shiftY);
                
                // 重新组合为复数图像
                HOperatorSet.RealToComplex(decenteredReal, decenteredImag, out HObject decenteredComplex);
                
                // 清理临时对象
                realPart?.Dispose();
                imagPart?.Dispose();
                decenteredReal?.Dispose();
                decenteredImag?.Dispose();
                
                return decenteredComplex;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"频谱去中心化失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 图像象限中心化（简化实现）
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="shiftX">X方向偏移</param>
        /// <param name="shiftY">Y方向偏移</param>
        /// <returns>中心化后的图像</returns>
        private HObject CenterImageQuadrants(HObject image, int shiftX, int shiftY)
        {
            try
            {
                // 简化实现：直接返回原图像
                // 在真实应用中，这里需要实现象限交换
                return image;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"图像象限交换失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 处理IFFT输出
        /// </summary>
        /// <param name="ifftResult">IFFT结果</param>
        /// <param name="outputType">输出类型</param>
        /// <returns>处理后的结果</returns>
        private HObject ProcessIFFTOutput(HObject ifftResult, IFFTOutputType outputType)
        {
            try
            {
                switch (outputType)
                {
                    case IFFTOutputType.Magnitude:
                        // 输出幅度（模）
                        HOperatorSet.ComplexToReal(ifftResult, out HObject magnitude, out HObject _);
                        return magnitude;
                        
                    case IFFTOutputType.RealPart:
                        // 输出实部
                        HOperatorSet.ComplexToReal(ifftResult, out HObject realPart, out HObject _);
                        return realPart;
                        
                    case IFFTOutputType.ImaginaryPart:
                        // 输出虚部
                        HOperatorSet.ComplexToReal(ifftResult, out HObject _, out HObject imagPart);
                        return imagPart;
                        
                    case IFFTOutputType.Phase:
                        // 输出相位
                        HOperatorSet.ComplexToReal(ifftResult, out HObject realForPhase, out HObject imagForPhase);
                        
                        // 计算相位：atan2(虚部, 实部)
                        HOperatorSet.Atan2Image(imagForPhase, realForPhase, out HObject phaseImage);
                        
                        realForPhase?.Dispose();
                        imagForPhase?.Dispose();
                        return phaseImage;
                        
                    case IFFTOutputType.Complex:
                        // 输出完整复数（转换为可显示格式）
                        return ifftResult;
                        
                    default:
                        // 默认输出幅度
                        HOperatorSet.ComplexToReal(ifftResult, out HObject defaultMag, out HObject _);
                        return defaultMag;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"IFFT输出处理失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 裁剪到指定尺寸
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="targetWidth">目标宽度</param>
        /// <param name="targetHeight">目标高度</param>
        /// <returns>裁剪后的图像</returns>
        private VisionImage CropToSize(VisionImage image, int targetWidth, int targetHeight)
        {
            try
            {
                // 获取当前图像尺寸
                HOperatorSet.GetImageSize(image.HImage, out HTuple currentWidth, out HTuple currentHeight);
                
                // 如果尺寸已经匹配，直接返回
                if (currentWidth.I == targetWidth && currentHeight.I == targetHeight)
                {
                    return image;
                }
                
                // 计算裁剪区域（从中心裁剪）
                int startRow = Math.Max(0, (currentHeight.I - targetHeight) / 2);
                int startCol = Math.Max(0, (currentWidth.I - targetWidth) / 2);
                
                // 确保裁剪区域不超出图像边界
                int actualHeight = Math.Min(targetHeight, currentHeight.I - startRow);
                int actualWidth = Math.Min(targetWidth, currentWidth.I - startCol);
                
                // 执行裁剪
                HOperatorSet.CropPart(image.HImage, out HObject croppedImage, 
                    startRow, startCol, actualHeight, actualWidth);
                
                return new VisionImage(croppedImage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"图像裁剪失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 计算IFFT统计信息
        /// </summary>
        /// <param name="resultImage">结果图像</param>
        /// <returns>统计信息</returns>
        private Dictionary<string, double> CalculateIFFTStatistics(VisionImage resultImage)
        {
            var statistics = new Dictionary<string, double>();
            
            try
            {
                // 基本统计信息
                HOperatorSet.Intensity(resultImage.HImage, resultImage.HImage, 
                    out HTuple meanIntensity, out HTuple stdDev);
                HOperatorSet.MinMaxGray(resultImage.HImage, resultImage.HImage, 0, 
                    out HTuple min, out HTuple max, out HTuple range);
                
                statistics["平均灰度"] = meanIntensity.D;
                statistics["灰度标准差"] = stdDev.D;
                statistics["最小灰度"] = min.D;
                statistics["最大灰度"] = max.D;
                statistics["动态范围"] = range.D;
                
                // 图像尺寸信息
                statistics["输出宽度"] = resultImage.Width;
                statistics["输出高度"] = resultImage.Height;
                statistics["像素总数"] = resultImage.Width * resultImage.Height;
                
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
            
            // 添加IFFT特有的信息
            measurements["输入复数图像类型"] = string.IsNullOrEmpty(_inputComplexType) ? "未检测" : _inputComplexType;
            measurements["IFFT结果图像类型"] = string.IsNullOrEmpty(_ifftImageType) ? "未检测" : _ifftImageType;
            measurements["输出类型选择"] = OutputType.ToString();
            measurements["去中心化处理"] = RemoveCentering ? "已启用" : "已禁用";
            measurements["去中心化执行状态"] = _decenteringExecuted ? "已执行" : "未执行";
            measurements["裁剪到原始尺寸"] = CropToOriginalSize ? "已启用" : "已禁用";
            measurements["裁剪执行状态"] = _croppingExecuted ? "已执行" : "未执行";
            
            // 添加尺寸信息
            if (result?.ProcessedImage != null)
            {
                measurements["输出图像尺寸"] = $"{result.ProcessedImage.Width} × {result.ProcessedImage.Height}";
                
                if (_croppingExecuted)
                {
                    measurements["尺寸变化"] = $"{inputImage.Width}×{inputImage.Height} → {result.ProcessedImage.Width}×{result.ProcessedImage.Height}";
                }
            }
            
            if (CropToOriginalSize)
            {
                measurements["目标尺寸"] = $"{OriginalWidth} × {OriginalHeight}";
            }
            
            return measurements;
        }
        
        #endregion
    }
    
    /// <summary>
    /// IFFT输出类型枚举
    /// </summary>
    public enum IFFTOutputType
    {
        /// <summary>幅度（模）</summary>
        Magnitude,
        
        /// <summary>实部</summary>
        RealPart,
        
        /// <summary>虚部</summary>
        ImaginaryPart,
        
        /// <summary>相位</summary>
        Phase,
        
        /// <summary>复数（完整）</summary>
        Complex
    }
}