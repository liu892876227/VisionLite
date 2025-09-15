using System;
using System.Collections.Generic;
using System.Linq;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 2D点结构
    /// </summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        
        public Point2D(double x = 0, double y = 0)
        {
            X = x;
            Y = y;
        }
        
        /// <summary>计算到另一点的距离</summary>
        public double DistanceTo(Point2D other)
        {
            return Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
        }
        
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// 尺寸结构
    /// </summary>
    public class Size
    {
        public int Width { get; set; }
        public int Height { get; set; }
        
        public Size(int width = 0, int height = 0)
        {
            Width = width;
            Height = height;
        }
        
        public override string ToString() => $"{Width}×{Height}";
    }

    /// <summary>
    /// 标定点对
    /// </summary>
    public class CalibrationPointPair
    {
        /// <summary>点的唯一标识</summary>
        public string PointId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>点名称</summary>
        public string PointName { get; set; }
        
        /// <summary>图像坐标点</summary>
        public Point2D ImagePoint { get; set; } = new Point2D();
        
        /// <summary>物理坐标点</summary>
        public Point2D WorldPoint { get; set; } = new Point2D();
        
        /// <summary>是否已设置</summary>
        public bool IsSet { get; set; }
        
        /// <summary>点的拟合误差(mm)</summary>
        public double FitError { get; set; }
        
        /// <summary>点的置信度</summary>
        public double Confidence { get; set; } = 1.0;
        
        /// <summary>创建时间</summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        
        /// <summary>备注</summary>
        public string Notes { get; set; } = "";
        
        public CalibrationPointPair(string name = "")
        {
            PointName = string.IsNullOrEmpty(name) ? $"Point_{DateTime.Now:HHmmss}" : name;
        }
    }

    /// <summary>
    /// 九点标定数据模型
    /// </summary>
    public class NinePointCalibrationData
    {
        /// <summary>标定唯一ID</summary>
        public string CalibrationId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>标定名称</summary>
        public string Name { get; set; } = "九点标定";
        
        /// <summary>标定描述</summary>
        public string Description { get; set; } = "";
        
        /// <summary>标定点对列表（最多9对）</summary>
        public List<CalibrationPointPair> PointPairs { get; set; } = new List<CalibrationPointPair>();
        
        /// <summary>变换矩阵(3x3)</summary>
        public double[,] TransformMatrix { get; set; } = new double[3, 3];
        
        /// <summary>逆变换矩阵(3x3)</summary>
        public double[,] InverseTransformMatrix { get; set; } = new double[3, 3];
        
        /// <summary>标定误差(mm)</summary>
        public double CalibrationError { get; set; }
        
        /// <summary>最大单点误差(mm)</summary>
        public double MaxPointError { get; set; }
        
        /// <summary>像素比例因子(像素/mm)</summary>
        public double PixelScale { get; set; }
        
        /// <summary>是否有效</summary>
        public bool IsValid { get; set; }
        
        /// <summary>标定时间</summary>
        public DateTime CalibrationTime { get; set; } = DateTime.Now;
        
        /// <summary>变换类型（固定为仿射变换）</summary>
        public string TransformType { get; set; } = "仿射变换";
        
        /// <summary>物理单位</summary>
        public PhysicalUnit Unit { get; set; } = PhysicalUnit.Millimeter;
        
        /// <summary>标定图像路径</summary>
        public string ImagePath { get; set; }
        
        /// <summary>标定图像尺寸</summary>
        public Size ImageSize { get; set; }
        
        /// <summary>标定质量等级</summary>
        public CalibrationQuality Quality { get; set; }
        
        /// <summary>
        /// 获取有效标定点数量
        /// </summary>
        public int ValidPointCount => PointPairs.Count(p => p.IsSet);
        
        /// <summary>
        /// 检查标定数据完整性
        /// </summary>
        public bool IsDataComplete(int minPoints = 4) => ValidPointCount >= minPoints;

        /// <summary>
        /// 获取标定矩阵的字符串表示
        /// </summary>
        public string GetTransformMatrixString()
        {
            if (TransformMatrix == null) return "未计算";
            
            var lines = new string[3];
            for (int i = 0; i < 3; i++)
            {
                lines[i] = $"[{TransformMatrix[i, 0]:F6} {TransformMatrix[i, 1]:F6} {TransformMatrix[i, 2]:F6}]";
            }
            return string.Join("\n", lines);
        }
    }
}