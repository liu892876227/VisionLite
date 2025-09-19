using VisionLite.Vision.Calibration.NinePoint.Core;
using HalconDotNet;

namespace VisionLite.Vision.Processors.ImageMatching.Models
{
    /// <summary>
    /// 匹配实例结果数据
    /// </summary>
    public class MatchInstance
    {
        /// <summary>匹配位置行坐标</summary>
        public double Row { get; set; }

        /// <summary>匹配位置列坐标</summary>
        public double Column { get; set; }

        /// <summary>旋转角度(度)</summary>
        public double Angle { get; set; }

        /// <summary>统一缩放比例</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>行方向缩放</summary>
        public double ScaleRow { get; set; } = 1.0;

        /// <summary>列方向缩放</summary>
        public double ScaleCol { get; set; } = 1.0;

        /// <summary>匹配分数</summary>
        public double Score { get; set; }

        /// <summary>轮廓点（用于显示）</summary>
        public Point2D[] ContourPoints { get; set; } = new Point2D[0];


        /// <summary>矫正后的图像（局部可变形模型专用）</summary>
        public HObject RectifiedImage { get; set; }

        /// <summary>变形向量场（局部可变形模型专用）</summary>
        public HObject VectorField { get; set; }

        /// <summary>变形轮廓（局部可变形模型专用）</summary>
        public HObject DeformedContours { get; set; }

        /// <summary>变形网格点（用于可视化显示）</summary>
        public Point2D[] DeformationGridPoints { get; set; } = new Point2D[0];

        /// <summary>
        /// 清理Halcon资源
        /// </summary>
        public void Dispose()
        {
            RectifiedImage?.Dispose();
            VectorField?.Dispose();
            DeformedContours?.Dispose();
        }
    }
}