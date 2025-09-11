using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using VisionLite.Vision.Core.Enums;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// 卡尺工具数据结构命名空间
    /// 包含卡尺测量相关的所有数据类型
    /// </summary>
    public static class CaliperData
    {
        /// <summary>
        /// 测量矩形参数
        /// 定义单个测量区域的几何参数和Halcon句柄
        /// </summary>
        public class MeasureRectangle : IDisposable
        {
            /// <summary>中心行坐标（Y坐标）</summary>
            public double Row { get; set; }
            
            /// <summary>中心列坐标（X坐标）</summary>
            public double Column { get; set; }
            
            /// <summary>矩形角度（弧度）</summary>
            public double Phi { get; set; }
            
            /// <summary>矩形长度的一半</summary>
            public double Length1 { get; set; }
            
            /// <summary>矩形宽度的一半</summary>
            public double Length2 { get; set; }
            
            /// <summary>Halcon测量句柄</summary>
            public HTuple MeasureHandle { get; set; }
            
            /// <summary>测量矩形索引</summary>
            public int Index { get; set; }
            
            /// <summary>是否已创建句柄</summary>
            public bool HandleCreated => MeasureHandle != null && MeasureHandle.Length > 0;
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public MeasureRectangle()
            {
                MeasureHandle = new HTuple();
            }
            
            /// <summary>
            /// 创建Halcon测量句柄
            /// </summary>
            /// <param name="imageWidth">图像宽度</param>
            /// <param name="imageHeight">图像高度</param>
            public void CreateMeasureHandle(int imageWidth, int imageHeight)
            {
                try
                {
                    HTuple tempHandle;
                    HOperatorSet.GenMeasureRectangle2(
                        Row, Column, Phi, Length1, Length2,
                        imageWidth, imageHeight, "nearest_neighbor",
                        out tempHandle);
                    MeasureHandle = tempHandle;
                }
                catch (HalconException ex)
                {
                    throw new InvalidOperationException($"创建测量句柄失败: {ex.Message}", ex);
                }
            }
            
            /// <summary>
            /// 释放测量句柄
            /// </summary>
            public void DisposeMeasureHandle()
            {
                try
                {
                    if (HandleCreated)
                    {
                        HOperatorSet.CloseMeasure(MeasureHandle);
                    }
                }
                catch (HalconException)
                {
                    // 忽略关闭句柄时的异常
                }
                finally
                {
                    MeasureHandle?.Dispose();
                    MeasureHandle = null;
                }
            }
            
            /// <summary>
            /// 释放Halcon资源
            /// </summary>
            public void Dispose()
            {
                DisposeMeasureHandle();
            }
            
            /// <summary>
            /// 获取矩形的四个角点坐标
            /// </summary>
            /// <returns>角点坐标列表</returns>
            public List<System.Windows.Point> GetCornerPoints()
            {
                var corners = new List<System.Windows.Point>();
                
                double cosAngle = Math.Cos(Phi);
                double sinAngle = Math.Sin(Phi);
                
                // 计算四个角点相对于中心的偏移
                var offsets = new[]
                {
                    (-Length1, -Length2), // 左上角
                    (Length1, -Length2),  // 右上角
                    (Length1, Length2),   // 右下角
                    (-Length1, Length2)   // 左下角
                };
                
                foreach (var (dx, dy) in offsets)
                {
                    double rotatedX = dx * cosAngle - dy * sinAngle;
                    double rotatedY = dx * sinAngle + dy * cosAngle;
                    
                    corners.Add(new System.Windows.Point(
                        Column + rotatedX, 
                        Row + rotatedY));
                }
                
                return corners;
            }
            
            /// <summary>
            /// 获取字符串表示
            /// </summary>
            public override string ToString()
            {
                return $"测量矩形[{Index}] 中心:({Column:F1},{Row:F1}) 角度:{Phi * 180 / Math.PI:F1}° 大小:{Length1 * 2:F1}×{Length2 * 2:F1}";
            }
        }
        
        /// <summary>
        /// 边缘检测结果
        /// 存储单个边缘点的详细信息
        /// </summary>
        public class EdgeResult
        {
            /// <summary>边缘点行坐标</summary>
            public double Row { get; set; }
            
            /// <summary>边缘点列坐标</summary>
            public double Column { get; set; }
            
            /// <summary>边缘强度/幅度</summary>
            public double Amplitude { get; set; }
            
            /// <summary>距离信息（边缘对的距离或到测量中心的距离）</summary>
            public double Distance { get; set; }
            
            /// <summary>边缘点是否有效</summary>
            public bool IsValid { get; set; }
            
            /// <summary>所属测量矩形的索引</summary>
            public int MeasureIndex { get; set; }
            
            /// <summary>边缘类型（正向、负向）</summary>
            public EdgeTransition EdgeType { get; set; }
            
            /// <summary>检测质量评分（0-100）</summary>
            public double Quality { get; set; }
            
            // 添加处理器需要的额外属性
            /// <summary>测量矩形</summary>
            public MeasureRectangle Rectangle { get; set; }
            
            /// <summary>边缘行坐标数组</summary>
            public double[] EdgeRows { get; set; }
            
            /// <summary>边缘列坐标数组</summary>
            public double[] EdgeCols { get; set; }
            
            /// <summary>边缘强度数组</summary>
            public double[] Amplitudes { get; set; }
            
            /// <summary>到边缘的距离数组</summary>
            public double[] Distances { get; set; }
            
            /// <summary>错误信息</summary>
            public string ErrorMessage { get; set; }
            
            /// <summary>边缘数量</summary>
            public int EdgeCount => EdgeRows?.Length ?? 0;
            
            /// <summary>平均边缘强度</summary>
            public double AverageAmplitude => Amplitudes != null && Amplitudes.Length > 0 ? 
                Amplitudes.Average() : 0.0;
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public EdgeResult()
            {
                IsValid = true;
                Quality = 100.0;
                EdgeType = EdgeTransition.All;
            }
            
            /// <summary>
            /// 计算与另一个边缘点的距离
            /// </summary>
            /// <param name="other">另一个边缘点</param>
            /// <returns>距离</returns>
            public double DistanceTo(EdgeResult other)
            {
                return Math.Sqrt(Math.Pow(Row - other.Row, 2) + Math.Pow(Column - other.Column, 2));
            }
            
            /// <summary>
            /// 转换为GeometryElement用于显示
            /// </summary>
            /// <returns>点几何元素</returns>
            public PointElement ToGeometryElement()
            {
                return new PointElement(Row, Column)
                {
                    Name = $"边缘点_{MeasureIndex}",
                    Description = $"强度: {Amplitude:F2}, 质量: {Quality:F1}%",
                    Color = IsValid ? System.Windows.Media.Colors.Red : System.Windows.Media.Colors.Gray,
                    Size = 2.0
                };
            }
            
            /// <summary>
            /// 获取字符串表示
            /// </summary>
            public override string ToString()
            {
                return $"边缘点 ({Column:F2},{Row:F2}) 强度:{Amplitude:F2} {(IsValid ? "有效" : "无效")}";
            }
        }
        
        /// <summary>
        /// 拟合结果基类
        /// 所有几何拟合结果的基础类
        /// </summary>
        public abstract class FitResult
        {
            /// <summary>拟合质量评分（0-100）</summary>
            public double Score { get; set; }
            
            /// <summary>参与拟合的边缘点数量</summary>
            public int UsedPointCount { get; set; }
            
            /// <summary>拟合算法类型</summary>
            public FittingAlgorithm Algorithm { get; set; }
            
            /// <summary>拟合误差（RMS）</summary>
            public double FitError { get; set; }
            
            /// <summary>参与拟合的边缘点</summary>
            public List<EdgeResult> UsedEdges { get; set; }
            
            /// <summary>拟合是否成功</summary>
            public bool Success { get; set; }
            
            /// <summary>拟合时间</summary>
            public DateTime FitTime { get; set; }
            
            // 添加处理器需要的兼容属性
            /// <summary>是否拟合成功（兼容属性）</summary>
            public bool IsValid { get => Success; set => Success = value; }
            
            /// <summary>错误信息</summary>
            public string ErrorMessage { get; set; }
            
            /// <summary>拟合误差（兼容属性）</summary>
            public double FittingError { get => FitError; set => FitError = value; }
            
            /// <summary>参与拟合的点数（兼容属性）</summary>
            public int PointCount { get => UsedPointCount; set => UsedPointCount = value; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            protected FitResult()
            {
                UsedEdges = new List<EdgeResult>();
                FitTime = DateTime.Now;
                Algorithm = FittingAlgorithm.Algebraic;
                Success = true;
            }
            
            /// <summary>
            /// 计算拟合质量评分
            /// </summary>
            /// <returns>质量评分</returns>
            public virtual double CalculateQualityScore()
            {
                if (UsedPointCount == 0 || FitError <= 0)
                    return 0;
                
                // 基于拟合误差和使用点数的评分算法
                double errorScore = Math.Max(0, 100 - FitError * 10);
                double countScore = Math.Min(100, UsedPointCount * 5);
                
                return (errorScore + countScore) / 2;
            }
            
            /// <summary>
            /// 抽象方法：转换为几何元素用于显示
            /// </summary>
            /// <returns>几何元素列表</returns>
            public abstract List<GeometryElement> ToGeometryElements();
        }
        
        
        /// <summary>
        /// 圆形拟合结果
        /// </summary>
        public class CircleFitResult : FitResult
        {
            /// <summary>圆心行坐标</summary>
            public double CenterRow { get; set; }
            
            /// <summary>圆心列坐标</summary>
            public double CenterColumn { get; set; }
            
            /// <summary>半径</summary>
            public double Radius { get; set; }
            
            /// <summary>起始角度（弧度）</summary>
            public double StartAngle { get; set; }
            
            /// <summary>结束角度（弧度）</summary>
            public double EndAngle { get; set; }
            
            // 添加兼容属性
            /// <summary>圆心列坐标（兼容属性）</summary>
            public double CenterCol { get => CenterColumn; set => CenterColumn = value; }
            
            /// <summary>角度范围（弧度）</summary>
            public double AngleRange
            {
                get
                {
                    double range = EndAngle - StartAngle;
                    if (range < 0) range += 2 * Math.PI;
                    return range;
                }
            }
            
            /// <summary>弧长</summary>
            public double ArcLength => Radius * AngleRange;
            
            /// <summary>圆的面积</summary>
            public double Area { get; set; }
            
            /// <summary>计算的圆面积</summary>
            public double CalculatedArea => Math.PI * Radius * Radius;
            
            /// <summary>圆的周长</summary>
            public double Circumference { get; set; }
            
            /// <summary>计算的圆周长</summary>
            public double CalculatedCircumference => 2 * Math.PI * Radius;
            
            /// <summary>
            /// 转换为几何元素
            /// </summary>
            public override List<GeometryElement> ToGeometryElements()
            {
                var elements = new List<GeometryElement>();
                
                // 添加拟合圆
                elements.Add(new CircleElement(CenterRow, CenterColumn, Radius)
                {
                    Name = "拟合圆",
                    Description = $"半径: {Radius:F2}, 弧长: {ArcLength:F2}, 质量: {Score:F1}%",
                    Color = Success ? System.Windows.Media.Colors.Green : System.Windows.Media.Colors.Orange,
                    LineWidth = 2.0
                });
                
                // 添加圆心标记
                elements.Add(new PointElement(CenterRow, CenterColumn)
                {
                    Name = "圆心",
                    Description = $"中心点 ({CenterColumn:F2},{CenterRow:F2})",
                    Color = System.Windows.Media.Colors.Blue,
                    Size = 3.0
                });
                
                // 如果有角度限制，添加起始和结束点
                if (AngleRange < 2 * Math.PI - 0.1)
                {
                    double startRow = CenterRow + Radius * Math.Sin(StartAngle);
                    double startCol = CenterColumn + Radius * Math.Cos(StartAngle);
                    elements.Add(new PointElement(startRow, startCol)
                    {
                        Name = "弧起点",
                        Color = System.Windows.Media.Colors.Purple,
                        Size = 2.5
                    });
                    
                    double endRow = CenterRow + Radius * Math.Sin(EndAngle);
                    double endCol = CenterColumn + Radius * Math.Cos(EndAngle);
                    elements.Add(new PointElement(endRow, endCol)
                    {
                        Name = "弧终点",
                        Color = System.Windows.Media.Colors.Purple,
                        Size = 2.5
                    });
                }
                
                return elements;
            }
            
            /// <summary>
            /// 计算点到圆心的距离
            /// </summary>
            /// <param name="row">点的行坐标</param>
            /// <param name="column">点的列坐标</param>
            /// <returns>距离</returns>
            public double DistanceToCenter(double row, double column)
            {
                return Math.Sqrt(Math.Pow(row - CenterRow, 2) + Math.Pow(column - CenterColumn, 2));
            }
            
            /// <summary>
            /// 计算点到圆周的距离（正值在圆外，负值在圆内）
            /// </summary>
            /// <param name="row">点的行坐标</param>
            /// <param name="column">点的列坐标</param>
            /// <returns>距离</returns>
            public double DistanceToCircle(double row, double column)
            {
                return DistanceToCenter(row, column) - Radius;
            }
            
            /// <summary>
            /// 获取字符串表示
            /// </summary>
            public override string ToString()
            {
                return $"圆形拟合 圆心:({CenterColumn:F2},{CenterRow:F2}) 半径:{Radius:F2} 弧长:{ArcLength:F2} 质量:{Score:F1}%";
            }
        }
        
        /// <summary>
        /// 直线拟合结果
        /// </summary>
        public class LineFitResult : FitResult
        {
            /// <summary>直线起点行坐标</summary>
            public double StartRow { get; set; }
            
            /// <summary>直线起点列坐标</summary>
            public double StartColumn { get; set; }
            
            /// <summary>直线终点行坐标</summary>
            public double EndRow { get; set; }
            
            /// <summary>直线终点列坐标</summary>
            public double EndColumn { get; set; }
            
            // 添加兼容属性
            /// <summary>直线起点列坐标（兼容属性）</summary>
            public double StartCol { get => StartColumn; set => StartColumn = value; }
            
            /// <summary>直线终点列坐标（兼容属性）</summary>
            public double EndCol { get => EndColumn; set => EndColumn = value; }
            
            /// <summary>直线长度</summary>
            public double Length
            {
                get
                {
                    return Math.Sqrt(Math.Pow(EndRow - StartRow, 2) + Math.Pow(EndColumn - StartColumn, 2));
                }
            }
            
            /// <summary>直线角度（弧度）</summary>
            public double Angle
            {
                get
                {
                    return Math.Atan2(EndRow - StartRow, EndColumn - StartColumn);
                }
            }
            
            /// <summary>直线角度（度）</summary>
            public double AngleDegrees
            {
                get
                {
                    return Angle * 180.0 / Math.PI;
                }
            }
            
            /// <summary>直线斜率</summary>
            public double Slope
            {
                get
                {
                    double deltaCol = EndColumn - StartColumn;
                    if (Math.Abs(deltaCol) < 1e-10)
                        return double.PositiveInfinity; // 垂直线
                    return (EndRow - StartRow) / deltaCol;
                }
            }
            
            /// <summary>
            /// 计算点到直线的距离
            /// </summary>
            /// <param name="row">点的行坐标</param>
            /// <param name="column">点的列坐标</param>
            /// <returns>距离</returns>
            public double DistanceToPoint(double row, double column)
            {
                // 使用点到直线距离公式: |ax + by + c| / sqrt(a² + b²)
                // 直线方程: (y2-y1)x - (x2-x1)y + x2*y1 - x1*y2 = 0
                double a = EndRow - StartRow;           // y2 - y1
                double b = StartColumn - EndColumn;     // x1 - x2  
                double c = EndColumn * StartRow - StartColumn * EndRow;  // x2*y1 - x1*y2
                
                return Math.Abs(a * column + b * row + c) / Math.Sqrt(a * a + b * b);
            }
            
            /// <summary>
            /// 转换为几何元素用于显示
            /// </summary>
            public override List<GeometryElement> ToGeometryElements()
            {
                var elements = new List<GeometryElement>();
                
                // 添加拟合直线
                elements.Add(new LineElement(StartRow, StartColumn, EndRow, EndColumn)
                {
                    Name = "拟合直线",
                    Description = $"长度: {Length:F2}, 角度: {AngleDegrees:F1}°, 质量: {Score:F1}%",
                    Color = Success ? System.Windows.Media.Colors.Green : System.Windows.Media.Colors.Orange,
                    LineWidth = 2.0
                });
                
                // 添加起点标记
                elements.Add(new PointElement(StartRow, StartColumn)
                {
                    Name = "起点",
                    Description = $"起点 ({StartColumn:F2},{StartRow:F2})",
                    Color = System.Windows.Media.Colors.Blue,
                    Size = 3.0
                });
                
                // 添加终点标记
                elements.Add(new PointElement(EndRow, EndColumn)
                {
                    Name = "终点", 
                    Description = $"终点 ({EndColumn:F2},{EndRow:F2})",
                    Color = System.Windows.Media.Colors.Red,
                    Size = 3.0
                });
                
                return elements;
            }
            
            /// <summary>
            /// 获取字符串表示
            /// </summary>
            public override string ToString()
            {
                return $"直线拟合 起点:({StartColumn:F2},{StartRow:F2}) 终点:({EndColumn:F2},{EndRow:F2}) 长度:{Length:F2} 角度:{AngleDegrees:F1}° 质量:{Score:F1}%";
            }
        }
        
        
        /// <summary>
        /// ROI几何信息
        /// 用于从现有ROI系统获取几何参数
        /// </summary>
        public class ROIGeometry
        {
            /// <summary>ROI类型</summary>
            public string RoiType { get; set; }
            
            /// <summary>ROI参数字典</summary>
            public Dictionary<string, double> Parameters { get; set; }
            
            /// <summary>ROI的几何类型</summary>
            public ROIGeometryType GeometryType
            {
                get
                {
                    return RoiType switch
                    {
                        "line" => ROIGeometryType.Line,
                        "rectangle1" => ROIGeometryType.Rectangle,
                        "rectangle2" => ROIGeometryType.Rectangle2,
                        "circle" => ROIGeometryType.Circle,
                        "ellipse" => ROIGeometryType.Ellipse,
                        _ => ROIGeometryType.Line
                    };
                }
            }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public ROIGeometry()
            {
                Parameters = new Dictionary<string, double>();
            }
            
            /// <summary>
            /// 检查ROI是否有效
            /// </summary>
            /// <returns>是否有效</returns>
            public bool IsValid()
            {
                return !string.IsNullOrEmpty(RoiType) && Parameters != null && Parameters.Count > 0;
            }
            
            /// <summary>
            /// 获取ROI的中心点
            /// </summary>
            /// <returns>中心点坐标</returns>
            public System.Windows.Point GetCenterPoint()
            {
                return GeometryType switch
                {
                    ROIGeometryType.Line => new System.Windows.Point(
                        (GetParameterValue("column1") + GetParameterValue("column2")) / 2,
                        (GetParameterValue("row1") + GetParameterValue("row2")) / 2),
                    
                    ROIGeometryType.Rectangle => new System.Windows.Point(
                        (GetParameterValue("column1") + GetParameterValue("column2")) / 2,
                        (GetParameterValue("row1") + GetParameterValue("row2")) / 2),
                    
                    ROIGeometryType.Rectangle2 => new System.Windows.Point(
                        GetParameterValue("column"),
                        GetParameterValue("row")),
                    
                    ROIGeometryType.Circle => new System.Windows.Point(
                        GetParameterValue("column"),
                        GetParameterValue("row")),
                    
                    ROIGeometryType.Ellipse => new System.Windows.Point(
                        GetParameterValue("column"),
                        GetParameterValue("row")),
                    
                    _ => new System.Windows.Point(0, 0)
                };
            }
            
            /// <summary>
            /// 获取参数值（带默认值）
            /// </summary>
            /// <param name="key">参数键名</param>
            /// <param name="defaultValue">默认值</param>
            /// <returns>参数值</returns>
            private double GetParameterValue(string key, double defaultValue = 0.0)
            {
                if (Parameters != null && Parameters.ContainsKey(key))
                {
                    return Parameters[key];
                }
                return defaultValue;
            }
            
            /// <summary>
            /// 获取字符串表示
            /// </summary>
            public override string ToString()
            {
                var center = GetCenterPoint();
                return $"{GeometryType} ROI 中心:({center.X:F1},{center.Y:F1})";
            }
        }
    }
}