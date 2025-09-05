using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Filters
{
    /// <summary>
    /// 频域低通滤波处理器
    /// 实现理想、巴特沃斯和高斯低通滤波器，用于去除高频噪声
    /// </summary>
    public class LowPassFilterProcessor : FrequencyProcessorBase, IFrequencyFilter
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "频域低通滤波";
        
        /// <summary>
        /// 截止频率
        /// </summary>
        [Parameter("截止频率", "低通滤波的截止频率（归一化频率：0-1）", 0.01, 1.0, Step = 0.01, Group = "滤波参数")]
        public double CutoffFrequency { get; set; } = 0.3;
        
        /// <summary>
        /// 滤波器类型
        /// </summary>
        [Parameter("滤波器类型", "选择低通滤波器的类型", Group = "滤波参数")]
        public FilterType FilterType { get; set; } = FilterType.Butterworth;
        
        /// <summary>
        /// 滤波器阶数
        /// </summary>
        [Parameter("滤波器阶数", "影响滤波器的陡峭程度（1-10）", 1, 10, DecimalPlaces = 0, Group = "滤波参数")]
        public int FilterOrder { get; set; } = 2;
        
        /// <summary>
        /// 过渡带宽
        /// </summary>
        [Parameter("过渡带宽", "滤波器过渡区域的宽度（0.01-0.5）", 0.01, 0.5, Step = 0.01, Group = "滤波参数")]
        public double TransitionWidth { get; set; } = 0.1;
        
        /// <summary>
        /// 是否自动FFT
        /// </summary>
        [Parameter("自动FFT", "如果输入不是复数图像，是否自动执行FFT", Group = "处理参数")]
        public bool AutoFFT { get; set; } = true;
        
        /// <summary>
        /// 是否自动IFFT
        /// </summary>
        [Parameter("自动IFFT", "是否自动执行IFFT返回空域图像", Group = "处理参数")]
        public bool AutoIFFT { get; set; } = true;
        
        #endregion
        
        #region 私有字段
        
        /// <summary>
        /// 是否执行了自动FFT
        /// </summary>
        private bool _autoFFTExecuted = false;
        
        /// <summary>
        /// 是否执行了自动IFFT
        /// </summary>
        private bool _autoIFFTExecuted = false;
        
        /// <summary>
        /// 输入图像是否为复数类型
        /// </summary>
        private bool _inputIsComplex = false;
        
        /// <summary>
        /// 滤波器掩码类型
        /// </summary>
        private string _filterMaskType = "";
        
        /// <summary>
        /// 频率响应数据
        /// </summary>
        private double[] _frequencyResponse;
        
        #endregion
        
        #region 主要方法
        
        /// <summary>
        /// 执行频域处理
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>频域处理结果</returns>
        protected override FrequencyProcessResult ExecuteFrequencyProcess(VisionImage inputImage)
        {
            HObject complexSpectrum = null;
            HObject filterMask = null;
            HObject filteredSpectrum = null;
            VisionImage resultImage = null;
            VisionImage spectrumImage = null;
            
            try
            {
                // 1. 验证参数
                ValidateFilterParameters();
                
                // 2. 检查输入图像类型并获取频谱
                _inputIsComplex = CheckIfComplexImage(inputImage);
                complexSpectrum = GetFrequencySpectrum(inputImage);
                
                // 3. 生成低通滤波器掩码
                filterMask = GenerateFilterMask(inputImage.Width, inputImage.Height);
                
                // 4. 应用滤波器
                filteredSpectrum = ApplyFilter(complexSpectrum, filterMask);
                
                // 5. 生成频谱可视化
                if (ShowSpectrum)
                {
                    spectrumImage = GenerateSpectrumVisualization(filteredSpectrum);
                }
                
                // 6. 根据设置决定输出类型
                if (AutoIFFT)
                {
                    // 执行IFFT，返回空域图像
                    HOperatorSet.FftImageInv(filteredSpectrum, out HObject spatialResult);
                    HOperatorSet.ComplexToReal(spatialResult, out HObject realResult, out HObject _);
                    resultImage = new VisionImage(realResult);
                    spatialResult?.Dispose();
                    _autoIFFTExecuted = true;
                }
                else
                {
                    // 返回滤波后的频谱
                    HOperatorSet.ComplexToReal(filteredSpectrum, out HObject realSpectrum, out HObject _);
                    resultImage = new VisionImage(realSpectrum);
                    _autoIFFTExecuted = false;
                }
                
                // 7. 计算频率响应
                _frequencyResponse = CalculateFilterResponse(inputImage.Width, inputImage.Height);
                
                // 8. 计算统计信息
                var statistics = CalculateLowPassStatistics(resultImage, filteredSpectrum, inputImage);
                
                return new FrequencyProcessResult
                {
                    ProcessedImage = resultImage,
                    SpectrumImage = spectrumImage,
                    ComplexImage = AutoIFFT ? null : filteredSpectrum,
                    FrequencyResponse = _frequencyResponse,
                    Statistics = statistics
                };
            }
            catch (Exception ex)
            {
                // 清理资源
                if (!_inputIsComplex && complexSpectrum != inputImage.HImage)
                    complexSpectrum?.Dispose();
                filterMask?.Dispose();
                if (filteredSpectrum != complexSpectrum)
                    filteredSpectrum?.Dispose();
                resultImage?.Dispose();
                spectrumImage?.Dispose();
                
                throw new InvalidOperationException($"低通滤波处理失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region IFrequencyFilter接口实现
        
        /// <summary>
        /// 滤波器类型
        /// </summary>
        FilterType IFrequencyFilter.FilterType => FilterType;
        
        /// <summary>
        /// 生成滤波器掩码
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>滤波器掩码</returns>
        public HObject GenerateFilterMask(int width, int height)
        {
            try
            {
                switch (FilterType)
                {
                    case FilterType.Ideal:
                        return GenerateIdealLowPassFilter(width, height, CutoffFrequency);
                        
                    case FilterType.Butterworth:
                        return GenerateButterworthLowPassFilter(width, height, CutoffFrequency, FilterOrder);
                        
                    case FilterType.Gaussian:
                        return GenerateGaussianLowPassFilter(width, height, CutoffFrequency);
                        
                    case FilterType.Chebyshev:
                        // Chebyshev滤波器的简化实现，使用Butterworth近似
                        return GenerateButterworthLowPassFilter(width, height, CutoffFrequency, FilterOrder * 2);
                        
                    default:
                        return GenerateButterworthLowPassFilter(width, height, CutoffFrequency, FilterOrder);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"低通滤波器掩码生成失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 应用滤波器到频谱
        /// </summary>
        /// <param name="complexSpectrum">复数频谱</param>
        /// <param name="filterMask">滤波器掩码</param>
        /// <returns>滤波后的频谱</returns>
        public HObject ApplyFilter(HObject complexSpectrum, HObject filterMask)
        {
            try
            {
                // 将滤波器掩码转换为复数形式
                HOperatorSet.GenImageConst(out HObject zeroImage, "real", 
                    GetImageWidth(filterMask), GetImageHeight(filterMask));
                HOperatorSet.RealToComplex(filterMask, zeroImage, out HObject complexMask);
                zeroImage?.Dispose();
                
                // 频域相乘（滤波操作）
                HOperatorSet.MultImage(complexSpectrum, complexMask, out HObject filteredSpectrum, 1.0, 0.0);
                complexMask?.Dispose();
                
                return filteredSpectrum;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"滤波器应用失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 计算滤波器的频率响应
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>频率响应数组</returns>
        public double[] CalculateFilterResponse(int width, int height)
        {
            try
            {
                var response = new List<double>();
                int centerX = width / 2;
                int centerY = height / 2;
                
                // 计算径向频率响应
                for (int i = 0; i <= Math.Max(centerX, centerY); i++)
                {
                    double normalizedFreq = (double)i / Math.Max(centerX, centerY);
                    double responseValue = CalculateResponseValue(normalizedFreq);
                    response.Add(responseValue);
                }
                
                return response.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"频率响应计算失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 验证滤波器参数是否有效
        /// </summary>
        /// <returns>参数是否有效</returns>
        public bool ValidateParameters()
        {
            return CutoffFrequency > 0 && CutoffFrequency <= 1.0 &&
                   FilterOrder >= 1 && FilterOrder <= 10 &&
                   TransitionWidth > 0 && TransitionWidth <= 0.5;
        }
        
        /// <summary>
        /// 获取滤波器的显示名称
        /// </summary>
        /// <returns>显示名称</returns>
        public string GetDisplayName()
        {
            return $"{FilterType}低通滤波器 (截止频率: {CutoffFrequency:F2})";
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 验证滤波器参数
        /// </summary>
        private void ValidateFilterParameters()
        {
            if (!ValidateParameters())
            {
                throw new ArgumentException($"滤波器参数无效: 截止频率={CutoffFrequency}, 阶数={FilterOrder}, 过渡带宽={TransitionWidth}");
            }
        }
        
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
        /// 获取频域频谱
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>复数频谱</returns>
        private HObject GetFrequencySpectrum(VisionImage inputImage)
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
                    throw new ArgumentException("输入不是复数图像，且未启用自动FFT");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取频域频谱失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 生成理想低通滤波器
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="cutoff">截止频率</param>
        /// <returns>滤波器掩码</returns>
        private HObject GenerateIdealLowPassFilter(int width, int height, double cutoff)
        {
            try
            {
                HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
                
                int centerX = width / 2;
                int centerY = height / 2;
                double cutoffRadius = cutoff * Math.Min(centerX, centerY);
                
                // 生成理想低通滤波器：在截止频率内为1，外为0
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                        double value = distance <= cutoffRadius ? 1.0 : 0.0;
                        HOperatorSet.SetGrayval(filterMask, y, x, value * 255);
                    }
                }
                
                _filterMaskType = $"理想低通(截止={cutoff:F2})";
                return filterMask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"理想低通滤波器生成失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 生成巴特沃斯低通滤波器
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="cutoff">截止频率</param>
        /// <param name="order">阶数</param>
        /// <returns>滤波器掩码</returns>
        private HObject GenerateButterworthLowPassFilter(int width, int height, double cutoff, int order)
        {
            try
            {
                HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
                
                int centerX = width / 2;
                int centerY = height / 2;
                double cutoffRadius = cutoff * Math.Min(centerX, centerY);
                
                // 生成巴特沃斯低通滤波器：H(u,v) = 1 / (1 + (D(u,v)/D0)^(2n))
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                        double ratio = distance / Math.Max(cutoffRadius, 1e-10);
                        double value = 1.0 / (1.0 + Math.Pow(ratio, 2.0 * order));
                        HOperatorSet.SetGrayval(filterMask, y, x, value * 255);
                    }
                }
                
                _filterMaskType = $"巴特沃斯低通(截止={cutoff:F2}, 阶数={order})";
                return filterMask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"巴特沃斯低通滤波器生成失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 生成高斯低通滤波器
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="cutoff">截止频率</param>
        /// <returns>滤波器掩码</returns>
        private HObject GenerateGaussianLowPassFilter(int width, int height, double cutoff)
        {
            try
            {
                HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
                
                int centerX = width / 2;
                int centerY = height / 2;
                double sigma = cutoff * Math.Min(centerX, centerY) / 3.0; // 3σ规则
                
                // 生成高斯低通滤波器：H(u,v) = exp(-D²(u,v)/(2σ²))
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                        double value = Math.Exp(-(distance * distance) / (2.0 * sigma * sigma));
                        HOperatorSet.SetGrayval(filterMask, y, x, value * 255);
                    }
                }
                
                _filterMaskType = $"高斯低通(截止={cutoff:F2}, σ={sigma:F2})";
                return filterMask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"高斯低通滤波器生成失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 计算响应值
        /// </summary>
        /// <param name="normalizedFreq">归一化频率</param>
        /// <returns>响应值</returns>
        private double CalculateResponseValue(double normalizedFreq)
        {
            switch (FilterType)
            {
                case FilterType.Ideal:
                    return normalizedFreq <= CutoffFrequency ? 1.0 : 0.0;
                    
                case FilterType.Butterworth:
                    double ratio = normalizedFreq / CutoffFrequency;
                    return 1.0 / (1.0 + Math.Pow(ratio, 2.0 * FilterOrder));
                    
                case FilterType.Gaussian:
                    double sigma = CutoffFrequency / 3.0;
                    return Math.Exp(-(normalizedFreq * normalizedFreq) / (2.0 * sigma * sigma));
                    
                default:
                    return normalizedFreq <= CutoffFrequency ? 1.0 : 0.0;
            }
        }
        
        /// <summary>
        /// 获取图像宽度
        /// </summary>
        /// <param name="image">图像</param>
        /// <returns>宽度</returns>
        private int GetImageWidth(HObject image)
        {
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple _);
            return width.I;
        }
        
        /// <summary>
        /// 获取图像高度
        /// </summary>
        /// <param name="image">图像</param>
        /// <returns>高度</returns>
        private int GetImageHeight(HObject image)
        {
            HOperatorSet.GetImageSize(image, out HTuple _, out HTuple height);
            return height.I;
        }
        
        /// <summary>
        /// 计算低通滤波统计信息
        /// </summary>
        /// <param name="resultImage">结果图像</param>
        /// <param name="filteredSpectrum">滤波后频谱</param>
        /// <param name="inputImage">输入图像</param>
        /// <returns>统计信息</returns>
        private Dictionary<string, double> CalculateLowPassStatistics(VisionImage resultImage, HObject filteredSpectrum, VisionImage inputImage)
        {
            var statistics = new Dictionary<string, double>();
            
            try
            {
                // 基本图像统计
                HOperatorSet.Intensity(resultImage.HImage, resultImage.HImage, out HTuple meanValue, out HTuple stdDev);
                HOperatorSet.MinMaxGray(resultImage.HImage, resultImage.HImage, 0, out HTuple min, out HTuple max, out HTuple range);
                
                statistics["结果图像平均值"] = meanValue.D;
                statistics["结果图像标准差"] = stdDev.D;
                statistics["结果图像最小值"] = min.D;
                statistics["结果图像最大值"] = max.D;
                statistics["结果图像动态范围"] = range.D;
                
                // 滤波器特性
                statistics["截止频率"] = CutoffFrequency;
                statistics["滤波器阶数"] = FilterOrder;
                statistics["过渡带宽"] = TransitionWidth;
                
                // 频域能量统计
                if (filteredSpectrum != null)
                {
                    HOperatorSet.PowerReal(filteredSpectrum, out HObject powerSpectrum);
                    HOperatorSet.Intensity(powerSpectrum, powerSpectrum, out HTuple totalEnergy, out HTuple _);
                    statistics["滤波后总能量"] = totalEnergy.D;
                    powerSpectrum?.Dispose();
                }
                
                // 能量保持比（如果有原始能量信息）
                if (_autoFFTExecuted && filteredSpectrum != null)
                {
                    try
                    {
                        HOperatorSet.FftImage(inputImage.HImage, out HObject originalSpectrum);
                        HOperatorSet.PowerReal(originalSpectrum, out HObject originalPower);
                        HOperatorSet.Intensity(originalPower, originalPower, out HTuple originalEnergy, out HTuple _);
                        
                        if (originalEnergy.D > 0)
                        {
                            statistics["能量保持比"] = statistics["滤波后总能量"] / originalEnergy.D;
                        }
                        
                        originalSpectrum?.Dispose();
                        originalPower?.Dispose();
                    }
                    catch
                    {
                        // 如果计算失败，跳过能量比统计
                    }
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
            
            // 添加低通滤波特有的信息
            measurements["滤波器类型"] = FilterType.ToString();
            measurements["滤波器显示名称"] = GetDisplayName();
            measurements["截止频率"] = CutoffFrequency;
            measurements["滤波器阶数"] = FilterOrder;
            measurements["过渡带宽"] = TransitionWidth;
            measurements["滤波器掩码类型"] = string.IsNullOrEmpty(_filterMaskType) ? "未生成" : _filterMaskType;
            measurements["输入图像类型"] = _inputIsComplex ? "复数图像" : "实数图像";
            measurements["自动FFT执行"] = _autoFFTExecuted ? "已执行" : "未执行";
            measurements["自动IFFT执行"] = _autoIFFTExecuted ? "已执行" : "未执行";
            measurements["输出类型"] = _autoIFFTExecuted ? "空域图像" : "频域图像";
            
            // 频率响应信息
            if (_frequencyResponse != null && _frequencyResponse.Length > 0)
            {
                measurements["频率响应采样点数"] = _frequencyResponse.Length;
                measurements["DC增益"] = _frequencyResponse.Length > 0 ? _frequencyResponse[0] : 0.0;
                
                // 找到-3dB点
                double targetGain = _frequencyResponse[0] / Math.Sqrt(2);
                for (int i = 0; i < _frequencyResponse.Length; i++)
                {
                    if (_frequencyResponse[i] <= targetGain)
                    {
                        measurements["-3dB截止点"] = (double)i / _frequencyResponse.Length;
                        break;
                    }
                }
            }
            
            return measurements;
        }
        
        /// <summary>
        /// 计算频率响应（用于分析和可视化）
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>频率响应数组</returns>
        public double[] CalculateFrequencyResponse(int width, int height)
        {
            try
            {
                int responseLength = Math.Min(width, height) / 2;
                double[] response = new double[responseLength];
                
                for (int i = 0; i < responseLength; i++)
                {
                    double normalizedFreq = (double)i / responseLength;
                    
                    switch (FilterType)
                    {
                        case FilterType.Ideal:
                            response[i] = normalizedFreq <= CutoffFrequency ? 1.0 : 0.0;
                            break;
                            
                        case FilterType.Butterworth:
                            double ratio = normalizedFreq / CutoffFrequency;
                            response[i] = 1.0 / (1.0 + Math.Pow(ratio, 2.0 * FilterOrder));
                            break;
                            
                        case FilterType.Gaussian:
                            double sigma = CutoffFrequency;
                            response[i] = Math.Exp(-Math.Pow(normalizedFreq, 2) / (2.0 * Math.Pow(sigma, 2)));
                            break;
                    }
                }
                
                return response;
            }
            catch
            {
                return new double[0];
            }
        }
        
        #endregion
    }
}