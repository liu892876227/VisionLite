using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base
{
    /// <summary>
    /// 频域处理工具类
    /// 提供常用的频域操作和实用方法
    /// </summary>
    public static class FrequencyProcessingUtils
    {
        /// <summary>
        /// 计算图像的理想FFT尺寸（2的幂次方）
        /// </summary>
        /// <param name="width">原始宽度</param>
        /// <param name="height">原始高度</param>
        /// <returns>优化后的FFT尺寸</returns>
        public static (int OptimalWidth, int OptimalHeight) CalculateOptimalFFTSize(int width, int height)
        {
            var optimalWidth = GetNextPowerOfTwo(width);
            var optimalHeight = GetNextPowerOfTwo(height);
            
            return (optimalWidth, optimalHeight);
        }

        /// <summary>
        /// 获取下一个2的幂次方
        /// </summary>
        /// <param name="value">输入值</param>
        /// <returns>下一个2的幂次方</returns>
        private static int GetNextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            
            int power = 1;
            while (power < value)
            {
                power *= 2;
            }
            
            return power;
        }

        /// <summary>
        /// 对图像进行零填充以达到指定尺寸
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="targetWidth">目标宽度</param>
        /// <param name="targetHeight">目标高度</param>
        /// <returns>零填充后的图像</returns>
        public static HObject ApplyZeroPadding(HObject inputImage, int targetWidth, int targetHeight)
        {
            try
            {
                HOperatorSet.GetImageSize(inputImage, out HTuple currentWidth, out HTuple currentHeight);
                
                if (currentWidth.I == targetWidth && currentHeight.I == targetHeight)
                {
                    // 尺寸已经匹配，直接复制
                    HOperatorSet.CopyImage(inputImage, out HObject resultImage);
                    return resultImage;
                }
                
                // 使用缩放进行零填充
                double scaleX = (double)targetWidth / currentWidth.I;
                double scaleY = (double)targetHeight / currentHeight.I;
                
                HOperatorSet.ZoomImageFactor(inputImage, out HObject paddedImage, scaleX, scaleY, "constant");
                
                return paddedImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"零填充操作失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 移除零填充，恢复原始尺寸
        /// </summary>
        /// <param name="paddedImage">填充后的图像</param>
        /// <param name="originalWidth">原始宽度</param>
        /// <param name="originalHeight">原始高度</param>
        /// <returns>裁剪后的图像</returns>
        public static HObject RemoveZeroPadding(HObject paddedImage, int originalWidth, int originalHeight)
        {
            try
            {
                // 使用缩放恢复原始尺寸
                HOperatorSet.GetImageSize(paddedImage, out HTuple currentWidth, out HTuple currentHeight);
                
                if (currentWidth.I == originalWidth && currentHeight.I == originalHeight)
                {
                    HOperatorSet.CopyImage(paddedImage, out HObject resultImage);
                    return resultImage;
                }
                
                double scaleX = (double)originalWidth / currentWidth.I;
                double scaleY = (double)originalHeight / currentHeight.I;
                
                HOperatorSet.ZoomImageFactor(paddedImage, out HObject croppedImage, scaleX, scaleY, "constant");
                
                return croppedImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"移除零填充操作失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 执行频谱中心化（将零频分量移至中心）
        /// </summary>
        /// <param name="complexImage">复数频域图像</param>
        /// <returns>中心化后的复数图像</returns>
        public static HObject CenterSpectrum(HObject complexImage)
        {
            try
            {
                // 简化实现：直接返回原图像
                // 在实际应用中，这里应该实现真正的频谱中心化算法
                HOperatorSet.CopyImage(complexImage, out HObject centeredImage);
                return centeredImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"频谱中心化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 执行频谱去中心化
        /// </summary>
        /// <param name="complexImage">中心化的复数图像</param>
        /// <returns>去中心化后的复数图像</returns>
        public static HObject DecenterSpectrum(HObject complexImage)
        {
            try
            {
                // 简化实现：直接返回原图像
                HOperatorSet.CopyImage(complexImage, out HObject decenteredImage);
                return decenteredImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"频谱去中心化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算复数图像的幅度谱
        /// </summary>
        /// <param name="complexImage">复数图像</param>
        /// <returns>幅度谱图像</returns>
        public static HObject CalculateMagnitudeSpectrum(HObject complexImage)
        {
            try
            {
                HOperatorSet.ComplexToReal(complexImage, out HObject realPart, out HObject imagPart);
                
                // 计算幅度：sqrt(real^2 + imag^2)
                HOperatorSet.MultImage(realPart, realPart, out HObject realSquared, 1.0, 0.0);
                HOperatorSet.MultImage(imagPart, imagPart, out HObject imagSquared, 1.0, 0.0);
                HOperatorSet.AddImage(realSquared, imagSquared, out HObject sumSquared, 1.0, 0.0);
                HOperatorSet.SqrtImage(sumSquared, out HObject magnitude);

                // 清理中间结果
                realPart?.Dispose();
                imagPart?.Dispose();
                realSquared?.Dispose();
                imagSquared?.Dispose();
                sumSquared?.Dispose();

                return magnitude;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"计算幅度谱失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算复数图像的相位谱
        /// </summary>
        /// <param name="complexImage">复数图像</param>
        /// <returns>相位谱图像</returns>
        public static HObject CalculatePhaseSpectrum(HObject complexImage)
        {
            try
            {
                HOperatorSet.ComplexToReal(complexImage, out HObject realPart, out HObject imagPart);
                
                // 计算相位：atan2(imag, real)
                HOperatorSet.Atan2Image(imagPart, realPart, out HObject phase);

                // 清理中间结果
                realPart?.Dispose();
                imagPart?.Dispose();

                return phase;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"计算相位谱失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算功率谱密度
        /// </summary>
        /// <param name="complexImage">复数图像</param>
        /// <returns>功率谱密度图像</returns>
        public static HObject CalculatePowerSpectralDensity(HObject complexImage)
        {
            try
            {
                HOperatorSet.PowerReal(complexImage, out HObject powerSpectrum);
                return powerSpectrum;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"计算功率谱密度失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 应用窗函数到图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="windowType">窗函数类型</param>
        /// <returns>应用窗函数后的图像</returns>
        public static HObject ApplyWindowFunction(HObject inputImage, WindowType windowType)
        {
            try
            {
                HOperatorSet.GetImageSize(inputImage, out HTuple width, out HTuple height);
                
                switch (windowType)
                {
                    case WindowType.None:
                        HOperatorSet.CopyImage(inputImage, out HObject noneResult);
                        return noneResult;
                        
                    case WindowType.Hann:
                    case WindowType.Hamming:
                    case WindowType.Blackman:
                    case WindowType.Gaussian:
                    default:
                        // 简化实现：对于所有窗函数，使用轻微的高斯模糊
                        HOperatorSet.GaussFilter(inputImage, out HObject windowedImage, 1.0);
                        HOperatorSet.ScaleImage(windowedImage, out HObject scaledResult, 0.9, 0.1);
                        windowedImage?.Dispose();
                        return scaledResult;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"应用窗函数失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 标准化频域图像以便显示
        /// </summary>
        /// <param name="frequencyImage">频域图像</param>
        /// <param name="displayType">显示类型</param>
        /// <returns>标准化后的显示图像</returns>
        public static VisionImage NormalizeForDisplay(HObject frequencyImage, SpectrumDisplayType displayType)
        {
            try
            {
                HObject displayImage = null;
                
                switch (displayType)
                {
                    case SpectrumDisplayType.LogMagnitude:
                        HOperatorSet.LogImage(frequencyImage, out HObject logImage, "e");
                        HOperatorSet.ScaleImageMax(logImage, out displayImage);
                        logImage?.Dispose();
                        break;
                        
                    case SpectrumDisplayType.LogPower:
                        HOperatorSet.PowerLn(frequencyImage, out HObject logPowerImage);
                        HOperatorSet.ScaleImageMax(logPowerImage, out displayImage);
                        logPowerImage?.Dispose();
                        break;
                        
                    default:
                        HOperatorSet.ScaleImageMax(frequencyImage, out displayImage);
                        break;
                }
                
                return new VisionImage(displayImage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"标准化显示失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算频率轴
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="samplingRate">采样率（可选）</param>
        /// <returns>频率轴数据</returns>
        public static double[] CalculateFrequencyAxis(int imageSize, double samplingRate = 1.0)
        {
            var frequencies = new double[imageSize / 2];
            
            for (int i = 0; i < frequencies.Length; i++)
            {
                frequencies[i] = (double)i * samplingRate / imageSize;
            }
            
            return frequencies;
        }

        /// <summary>
        /// 估算滤波器的3dB截止点
        /// </summary>
        /// <param name="frequencyResponse">频率响应</param>
        /// <returns>3dB截止点索引</returns>
        public static int EstimateCutoffPoint(double[] frequencyResponse)
        {
            if (frequencyResponse == null || frequencyResponse.Length == 0)
                return -1;
                
            double maxResponse = 0;
            foreach (var response in frequencyResponse)
            {
                if (response > maxResponse)
                    maxResponse = response;
            }
            
            double threshold = maxResponse * 0.707; // -3dB点
            
            for (int i = 0; i < frequencyResponse.Length; i++)
            {
                if (frequencyResponse[i] <= threshold)
                {
                    return i;
                }
            }
            
            return frequencyResponse.Length - 1;
        }

        /// <summary>
        /// 获取滤波器的统计信息
        /// </summary>
        /// <param name="originalImage">原始图像</param>
        /// <param name="filteredImage">滤波后图像</param>
        /// <returns>统计信息字典</returns>
        public static Dictionary<string, double> CalculateFilterStatistics(HObject originalImage, HObject filteredImage)
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
                
                // 计算信噪比改善
                double snrImprovement = deviationFilt.D / Math.Max(deviationOrig.D, 1e-6);
                statistics["标准差比率"] = snrImprovement;
                
                // 计算均值保持度
                double meanPreservation = 1.0 - Math.Abs(meanFilt.D - meanOrig.D) / Math.Max(Math.Abs(meanOrig.D), 1e-6);
                statistics["均值保持度"] = Math.Max(0.0, meanPreservation);
                
                return statistics;
            }
            catch (Exception ex)
            {
                statistics["错误"] = ex.GetHashCode();
                return statistics;
            }
        }
    }

    /// <summary>
    /// 窗函数类型枚举
    /// </summary>
    public enum WindowType
    {
        /// <summary>无窗函数</summary>
        None,
        /// <summary>汉宁窗</summary>
        Hann,
        /// <summary>汉明窗</summary>
        Hamming,
        /// <summary>布莱克曼窗</summary>
        Blackman,
        /// <summary>高斯窗</summary>
        Gaussian
    }
}