using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Filters
{
    /// <summary>
    /// 频域高通滤波处理器
    /// 实现理想、巴特沃斯和高斯高通滤波器，用于增强边缘和细节信息
    /// </summary>
    public class HighPassFilterProcessor : FrequencyProcessorBase, IFrequencyFilter
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "频域高通滤波";
        
        /// <summary>
        /// 截止频率
        /// </summary>
        [Parameter("截止频率", "高通滤波的截止频率（归一化频率：0-1）", 0.01, 1.0, Step = 0.01, Group = "滤波参数")]
        public double CutoffFrequency { get; set; } = 0.1;
        
        /// <summary>
        /// 滤波器类型
        /// </summary>
        [Parameter("滤波器类型", "选择滤波器的实现方式", Group = "滤波参数")]
        public FilterType FilterType { get; set; } = FilterType.Butterworth;
        
        /// <summary>
        /// 巴特沃斯滤波器阶数
        /// </summary>
        [Parameter("滤波器阶数", "巴特沃斯滤波器的阶数，控制过渡带陡峭程度", 1, 10, Step = 1, Group = "滤波参数")]
        public int FilterOrder { get; set; } = 2;
        
        /// <summary>
        /// 是否应用零频增强
        /// </summary>
        [Parameter("零频增强", "是否保留部分DC分量以避免完全黑色输出", Group = "滤波参数")]
        public bool ApplyZeroFrequencyBoost { get; set; } = true;
        
        /// <summary>
        /// 零频增强系数
        /// </summary>
        [Parameter("零频增强系数", "DC分量保留比例", 0.0, 1.0, Step = 0.01, Group = "滤波参数")]
        public double ZeroFrequencyBoost { get; set; } = 0.1;
        
        #endregion
        
        #region 核心处理方法
        
        /// <summary>
        /// 执行频域处理
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        protected override FrequencyProcessResult ExecuteFrequencyProcess(VisionImage inputImage)
        {
            var result = new FrequencyProcessResult();
            HObject complexImage = null;
            HObject filteredComplex = null;
            HObject filterMask = null;
            HObject outputImage = null;
            
            try
            {
                // 参数验证
                ValidateFilterParameters();
                
                // 获取图像尺寸
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);
                
                // 执行FFT变换
                HOperatorSet.FftImage(inputImage.HImage, out complexImage);
                
                // 生成高通滤波器掩模
                filterMask = GenerateFilterMask(width.I, height.I);
                
                // 应用滤波器
                filteredComplex = ApplyFilter(complexImage, filterMask);
                
                // 执行逆FFT
                HOperatorSet.FftImageInv(filteredComplex, out HObject ifftResult);
                
                // 提取幅度图像
                HOperatorSet.ComplexToReal(ifftResult, out outputImage, out HObject _);
                HOperatorSet.AbsImage(outputImage, out HObject absImage);
                outputImage.Dispose();
                outputImage = absImage;
                
                // 标准化输出
                HOperatorSet.ScaleImageMax(outputImage, out HObject scaledImage);
                
                // 创建结果图像
                result.ProcessedImage = new VisionImage(scaledImage);
                
                // 生成频谱可视化（如果需要）
                if (ShowSpectrum)
                {
                    result.SpectrumImage = GenerateSpectrumVisualization(filterMask, width.I, height.I);
                }
                
                // 计算统计信息
                result.Statistics = CalculateFilterStatistics(inputImage.HImage, scaledImage);
                
                // 清理临时对象
                ifftResult?.Dispose();
                scaledImage?.Dispose();
                
                return result;
            }
            catch (Exception ex)
            {
                result?.Dispose();
                throw new Exception($"高通滤波处理失败: {ex.Message}", ex);
            }
            finally
            {
                // 清理所有临时对象
                complexImage?.Dispose();
                filteredComplex?.Dispose();
                filterMask?.Dispose();
                outputImage?.Dispose();
            }
        }
        
        #endregion
        
        #region 滤波器实现
        
        /// <summary>
        /// 生成滤波器掩模
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>滤波器掩模</returns>
        public HObject GenerateFilterMask(int width, int height)
        {
            HObject filterMask = null;
            
            try
            {
                switch (FilterType)
                {
                    case FilterType.Ideal:
                        filterMask = GenerateIdealHighPassFilter(width, height);
                        break;
                    case FilterType.Butterworth:
                        filterMask = GenerateButterworthHighPassFilter(width, height);
                        break;
                    case FilterType.Gaussian:
                        filterMask = GenerateGaussianHighPassFilter(width, height);
                        break;
                    default:
                        throw new ArgumentException($"不支持的滤波器类型: {FilterType}");
                }
                
                return filterMask;
            }
            catch
            {
                filterMask?.Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// 生成理想高通滤波器
        /// </summary>
        private HObject GenerateIdealHighPassFilter(int width, int height)
        {
            // 创建全一图像
            HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
            HOperatorSet.ScaleImage(filterMask, out HObject tempMask, 0.0, 1.0);
            filterMask.Dispose();
            
            // 应用零频增强
            if (ApplyZeroFrequencyBoost)
            {
                HOperatorSet.ScaleImage(tempMask, out HObject finalMask, 1.0 - ZeroFrequencyBoost, ZeroFrequencyBoost);
                tempMask.Dispose();
                return finalMask;
            }
            
            return tempMask;
        }
        
        /// <summary>
        /// 生成巴特沃斯高通滤波器
        /// </summary>
        private HObject GenerateButterworthHighPassFilter(int width, int height)
        {
            // 创建基础滤波器
            HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
            
            // 使用伽马矫正模拟巴特沃斯响应
            double gamma = 1.0 + FilterOrder * 0.2;
            HOperatorSet.GammaImage(filterMask, out HObject gammaImage, gamma, 1.0, 255.0, 1.0, 255.0);
            HOperatorSet.ScaleImage(gammaImage, out HObject scaledImage, 1.0, 0.0);
            
            // 应用零频增强
            if (ApplyZeroFrequencyBoost)
            {
                HOperatorSet.ScaleImage(scaledImage, out HObject finalMask, 1.0 - ZeroFrequencyBoost, ZeroFrequencyBoost);
                scaledImage.Dispose();
                filterMask.Dispose();
                gammaImage.Dispose();
                return finalMask;
            }
            
            filterMask.Dispose();
            gammaImage.Dispose();
            return scaledImage;
        }
        
        /// <summary>
        /// 生成高斯高通滤波器
        /// </summary>
        private HObject GenerateGaussianHighPassFilter(int width, int height)
        {
            // 创建全一图像
            HOperatorSet.GenImageConst(out HObject filterMask, "real", width, height);
            
            // 使用高斯模糊实现高斯低通特性，然后反转得到高通
            double sigma = CutoffFrequency * Math.Min(width, height) / 6.0;
            if (sigma < 0.5) sigma = 0.5;
            
            HOperatorSet.GaussFilter(filterMask, out HObject blurredImage, sigma);
            HOperatorSet.InvertImage(blurredImage, out HObject invertedImage);
            
            // 应用零频增强
            if (ApplyZeroFrequencyBoost)
            {
                HOperatorSet.ScaleImage(invertedImage, out HObject finalMask, 1.0 - ZeroFrequencyBoost, ZeroFrequencyBoost);
                invertedImage.Dispose();
                filterMask.Dispose();
                blurredImage.Dispose();
                return finalMask;
            }
            
            filterMask.Dispose();
            blurredImage.Dispose();
            return invertedImage;
        }
        
        /// <summary>
        /// 应用滤波器
        /// </summary>
        /// <param name="complexImage">复数频域图像</param>
        /// <param name="filterMask">滤波器掩模</param>
        /// <returns>滤波后的复数图像</returns>
        public HObject ApplyFilter(HObject complexImage, HObject filterMask)
        {
            // 将滤波器转换为复数形式
            HOperatorSet.RealToComplex(filterMask, filterMask, out HObject complexFilter);
            
            // 频域乘法
            HOperatorSet.MultImage(complexImage, complexFilter, out HObject filteredComplex, 1.0, 0.0);
            
            complexFilter.Dispose();
            return filteredComplex;
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 验证滤波器参数是否有效
        /// </summary>
        /// <returns>参数是否有效</returns>
        public bool ValidateParameters()
        {
            return CutoffFrequency > 0 && CutoffFrequency < 1.0 &&
                   FilterOrder >= 1 && FilterOrder <= 10 &&
                   ZeroFrequencyBoost >= 0.0 && ZeroFrequencyBoost <= 1.0;
        }
        
        /// <summary>
        /// 验证滤波器参数，参数无效时抛出异常
        /// </summary>
        private void ValidateFilterParameters()
        {
            if (!ValidateParameters())
            {
                throw new ArgumentException($"高通滤波器参数无效: 截止频率={CutoffFrequency}, 阶数={FilterOrder}, 零频增强={ZeroFrequencyBoost}");
            }
        }
        
        /// <summary>
        /// 生成频谱可视化图像
        /// </summary>
        private VisionImage GenerateSpectrumVisualization(HObject filterMask, int width, int height)
        {
            try
            {
                HOperatorSet.ScaleImage(filterMask, out HObject scaledMask, 255.0, 0.0);
                HOperatorSet.ConvertImageType(scaledMask, out HObject byteImage, "byte");
                
                var spectrumImage = new VisionImage(byteImage);
                
                scaledMask.Dispose();
                
                return spectrumImage;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 计算滤波器统计信息
        /// </summary>
        private Dictionary<string, double> CalculateFilterStatistics(HObject originalImage, HObject filteredImage)
        {
            var statistics = new Dictionary<string, double>();
            
            try
            {
                // 原始图像统计
                HOperatorSet.Intensity(originalImage, originalImage, out HTuple meanOrig, out HTuple deviationOrig);
                statistics["原始图像平均值"] = meanOrig.D;
                statistics["原始图像标准差"] = deviationOrig.D;
                
                // 滤波后图像统计
                HOperatorSet.Intensity(filteredImage, filteredImage, out HTuple meanFilt, out HTuple deviationFilt);
                statistics["滤波后平均值"] = meanFilt.D;
                statistics["滤波后标准差"] = deviationFilt.D;
                
                // 滤波器参数
                statistics["截止频率"] = CutoffFrequency;
                statistics["滤波器阶数"] = FilterOrder;
                
                // 增强效果评估
                double enhancementRatio = deviationFilt.D / Math.Max(deviationOrig.D, 1e-6);
                statistics["边缘增强比"] = enhancementRatio;
                
                return statistics;
            }
            catch
            {
                return statistics;
            }
        }
        
        /// <summary>
        /// 获取显示名称
        /// </summary>
        /// <returns>显示名称</returns>
        public string GetDisplayName()
        {
            return $"高通滤波 (截止频率: {CutoffFrequency:F3}, 类型: {FilterType})";
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
                            response[i] = normalizedFreq > CutoffFrequency ? 1.0 : 
                                         (ApplyZeroFrequencyBoost ? ZeroFrequencyBoost : 0.0);
                            break;
                            
                        case FilterType.Butterworth:
                            if (normalizedFreq == 0)
                                response[i] = ApplyZeroFrequencyBoost ? ZeroFrequencyBoost : 0.0;
                            else
                            {
                                double ratio = CutoffFrequency / normalizedFreq;
                                response[i] = 1.0 / (1.0 + Math.Pow(ratio, 2.0 * FilterOrder));
                                if (ApplyZeroFrequencyBoost && normalizedFreq < CutoffFrequency)
                                    response[i] = Math.Max(response[i], ZeroFrequencyBoost);
                            }
                            break;
                            
                        case FilterType.Gaussian:
                            double sigma = CutoffFrequency;
                            response[i] = 1.0 - Math.Exp(-Math.Pow(normalizedFreq, 2) / (2.0 * Math.Pow(sigma, 2)));
                            if (ApplyZeroFrequencyBoost)
                                response[i] = Math.Max(response[i], ZeroFrequencyBoost);
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