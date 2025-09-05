using System;
using System.Collections.Generic;
using HalconDotNet;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base
{
    /// <summary>
    /// 频域处理结果数据结构
    /// 包含频域处理后的图像、频谱数据和统计信息
    /// </summary>
    public class FrequencyProcessResult : IDisposable
    {
        #region 属性
        
        /// <summary>
        /// 处理后的图像
        /// </summary>
        public VisionImage ProcessedImage { get; set; }
        
        /// <summary>
        /// 频谱图像（用于显示）
        /// </summary>
        public VisionImage SpectrumImage { get; set; }
        
        /// <summary>
        /// 复数图像数据（FFT结果）
        /// </summary>
        public HObject ComplexImage { get; set; }
        
        /// <summary>
        /// 频率响应数据
        /// </summary>
        public double[] FrequencyResponse { get; set; }
        
        /// <summary>
        /// 频域统计信息
        /// </summary>
        public Dictionary<string, double> Statistics { get; set; }
        
        /// <summary>
        /// 是否包含频谱数据
        /// </summary>
        public bool HasSpectrumData => SpectrumImage != null;
        
        /// <summary>
        /// 是否包含复数数据
        /// </summary>
        public bool HasComplexData => ComplexImage != null;
        
        /// <summary>
        /// 是否包含频率响应数据
        /// </summary>
        public bool HasFrequencyResponse => FrequencyResponse != null && FrequencyResponse.Length > 0;
        
        #endregion
        
        #region 构造函数
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public FrequencyProcessResult()
        {
            Statistics = new Dictionary<string, double>();
        }
        
        #endregion
        
        #region 资源清理
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放托管资源
                ProcessedImage?.Dispose();
                SpectrumImage?.Dispose();
                
                // 释放Halcon对象
                ComplexImage?.Dispose();
                
                // 清理集合
                Statistics?.Clear();
                FrequencyResponse = null;
            }
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~FrequencyProcessResult()
        {
            Dispose(false);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 频谱显示类型枚举
    /// </summary>
    public enum SpectrumDisplayType
    {
        /// <summary>幅度谱</summary>
        Magnitude,
        
        /// <summary>对数幅度谱</summary>
        LogMagnitude,
        
        /// <summary>相位谱</summary>
        Phase,
        
        /// <summary>功率谱</summary>
        Power,
        
        /// <summary>对数功率谱</summary>
        LogPower
    }
    
    /// <summary>
    /// 窗函数类型枚举
    /// </summary>
    public enum WindowFunctionType
    {
        /// <summary>矩形窗（无窗函数）</summary>
        Rectangle,
        
        /// <summary>汉宁窗</summary>
        Hann,
        
        /// <summary>汉明窗</summary>
        Hamming,
        
        /// <summary>布莱克曼窗</summary>
        Blackman,
        
        /// <summary>高斯窗</summary>
        Gaussian
    }
    
    /// <summary>
    /// 滤波器类型枚举
    /// </summary>
    public enum FilterType
    {
        /// <summary>理想滤波器</summary>
        Ideal,
        
        /// <summary>巴特沃斯滤波器</summary>
        Butterworth,
        
        /// <summary>高斯滤波器</summary>
        Gaussian,
        
        /// <summary>切比雪夫滤波器</summary>
        Chebyshev
    }
}