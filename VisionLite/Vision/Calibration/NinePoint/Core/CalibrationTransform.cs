using System;
using System.Collections.Generic;
using System.Linq;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 标定坐标变换工具类
    /// 提供图像坐标与物理坐标之间的变换功能
    /// </summary>
    public static class CalibrationTransform
    {
        /// <summary>
        /// 将图像坐标转换为物理坐标
        /// </summary>
        /// <param name="imagePoint">图像坐标点</param>
        /// <param name="transformMatrix">变换矩阵(3x3)</param>
        /// <returns>物理坐标点</returns>
        public static Point2D ImageToWorld(Point2D imagePoint, double[,] transformMatrix)
        {
            if (transformMatrix == null || transformMatrix.GetLength(0) != 3 || transformMatrix.GetLength(1) != 3)
                throw new ArgumentException("变换矩阵必须为3x3矩阵");
            
            // 齐次坐标变换: [x' y' 1] = [x y 1] * T
            var x = imagePoint.X;
            var y = imagePoint.Y;
            
            var worldX = transformMatrix[0, 0] * x + transformMatrix[0, 1] * y + transformMatrix[0, 2];
            var worldY = transformMatrix[1, 0] * x + transformMatrix[1, 1] * y + transformMatrix[1, 2];
            var w = transformMatrix[2, 0] * x + transformMatrix[2, 1] * y + transformMatrix[2, 2];
            
            // 齐次坐标归一化
            if (Math.Abs(w) < 1e-10)
                throw new InvalidOperationException("变换矩阵异常: 齐次坐标归一化因子接近零");
            
            return new Point2D(worldX / w, worldY / w);
        }
        
        /// <summary>
        /// 将物理坐标转换为图像坐标
        /// </summary>
        /// <param name="worldPoint">物理坐标点</param>
        /// <param name="inverseMatrix">逆变换矩阵(3x3)</param>
        /// <returns>图像坐标点</returns>
        public static Point2D WorldToImage(Point2D worldPoint, double[,] inverseMatrix)
        {
            if (inverseMatrix == null || inverseMatrix.GetLength(0) != 3 || inverseMatrix.GetLength(1) != 3)
                throw new ArgumentException("逆变换矩阵必须为3x3矩阵");
            
            // 齐次坐标变换: [x' y' 1] = [x y 1] * T^-1
            var x = worldPoint.X;
            var y = worldPoint.Y;
            
            var imageX = inverseMatrix[0, 0] * x + inverseMatrix[0, 1] * y + inverseMatrix[0, 2];
            var imageY = inverseMatrix[1, 0] * x + inverseMatrix[1, 1] * y + inverseMatrix[1, 2];
            var w = inverseMatrix[2, 0] * x + inverseMatrix[2, 1] * y + inverseMatrix[2, 2];
            
            // 齐次坐标归一化
            if (Math.Abs(w) < 1e-10)
                throw new InvalidOperationException("逆变换矩阵异常: 齐次坐标归一化因子接近零");
            
            return new Point2D(imageX / w, imageY / w);
        }
        
        /// <summary>
        /// 批量转换坐标点
        /// </summary>
        /// <param name="points">输入坐标点列表</param>
        /// <param name="transformMatrix">变换矩阵(3x3)</param>
        /// <returns>变换后的坐标点列表</returns>
        public static List<Point2D> TransformPoints(List<Point2D> points, double[,] transformMatrix)
        {
            if (points == null || points.Count == 0)
                return new List<Point2D>();
            
            if (transformMatrix == null || transformMatrix.GetLength(0) != 3 || transformMatrix.GetLength(1) != 3)
                throw new ArgumentException("变换矩阵必须为3x3矩阵");
            
            var transformedPoints = new List<Point2D>();
            
            foreach (var point in points)
            {
                try
                {
                    var transformedPoint = ImageToWorld(point, transformMatrix);
                    transformedPoints.Add(transformedPoint);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"坐标点变换失败: {point} - {ex.Message}");
                    // 变换失败时添加原坐标作为降级处理
                    transformedPoints.Add(new Point2D(point.X, point.Y));
                }
            }
            
            return transformedPoints;
        }
        
        /// <summary>
        /// 计算两点间的距离（物理单位）
        /// </summary>
        /// <param name="point1">第一个点</param>
        /// <param name="point2">第二个点</param>
        /// <returns>距离值</returns>
        public static double CalculateDistance(Point2D point1, Point2D point2)
        {
            return Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
        }
        
        /// <summary>
        /// 检查坐标点是否在标定范围内
        /// </summary>
        /// <param name="imagePoint">图像坐标点</param>
        /// <param name="calibrationData">标定数据</param>
        /// <param name="toleranceRatio">容差比例(0.1表示10%扩展)</param>
        /// <returns>是否在范围内</returns>
        public static bool IsPointInCalibrationRange(Point2D imagePoint, NinePointCalibrationData calibrationData, double toleranceRatio = 0.1)
        {
            if (calibrationData?.PointPairs == null || !calibrationData.IsValid)
                return false;
            
            var setPoints = calibrationData.PointPairs.Where(p => p.IsSet).Select(p => p.ImagePoint).ToList();
            if (setPoints.Count < 3)
                return false;
            
            // 计算标定点的边界范围
            var minX = setPoints.Min(p => p.X);
            var maxX = setPoints.Max(p => p.X);
            var minY = setPoints.Min(p => p.Y);
            var maxY = setPoints.Max(p => p.Y);
            
            // 应用容差扩展
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            var toleranceX = rangeX * toleranceRatio;
            var toleranceY = rangeY * toleranceRatio;
            
            minX -= toleranceX;
            maxX += toleranceX;
            minY -= toleranceY;
            maxY += toleranceY;
            
            // 检查点是否在扩展范围内
            return imagePoint.X >= minX && imagePoint.X <= maxX && 
                   imagePoint.Y >= minY && imagePoint.Y <= maxY;
        }
        
        /// <summary>
        /// 估算变换精度
        /// </summary>
        /// <param name="calibrationData">标定数据</param>
        /// <returns>精度评估信息</returns>
        public static string EstimateTransformAccuracy(NinePointCalibrationData calibrationData)
        {
            if (calibrationData?.IsValid != true)
                return "无法评估: 标定数据无效";
            
            var pointCount = calibrationData.ValidPointCount;
            var error = calibrationData.CalibrationError;
            var maxError = calibrationData.MaxPointError;
            
            string quality;
            if (error < 0.1) quality = "优秀";
            else if (error < 0.5) quality = "良好";
            else if (error < 1.0) quality = "一般";
            else quality = "较差";
            
            return $"精度评估: {quality}\n" +
                   $"平均误差: {error:F3} {calibrationData.Unit}\n" +
                   $"最大误差: {maxError:F3} {calibrationData.Unit}\n" +
                   $"标定点数: {pointCount}";
        }
    }
}