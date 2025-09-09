using System;
using HalconDotNet;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// Halcon显示轮廓数据结构
    /// 用于存储Metrology模型的显示轮廓
    /// </summary>
    public class HalconDisplayContours : IDisposable
    {
        /// <summary>
        /// 模型轮廓（如拟合的圆、直线等）
        /// </summary>
        public HObject ModelContour { get; set; }
        
        /// <summary>
        /// 测量轮廓（卡尺测量线）
        /// </summary>
        public HObject MeasureContours { get; set; }

        /// <summary>
        /// 释放Halcon对象资源
        /// </summary>
        public void Dispose()
        {
            ModelContour?.Dispose();
            MeasureContours?.Dispose();
        }
    }
}