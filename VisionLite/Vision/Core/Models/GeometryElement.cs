using System.Windows.Media;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// 几何元素类型枚举
    /// </summary>
    public enum GeometryElementType
    {
        /// <summary>点</summary>
        Point,
        /// <summary>直线</summary>
        Line,
        /// <summary>圆</summary>
        Circle,
        /// <summary>椭圆</summary>
        Ellipse,
        /// <summary>矩形</summary>
        Rectangle,
        /// <summary>多边形</summary>
        Polygon,
        /// <summary>文本</summary>
        Text
    }
    
    /// <summary>
    /// 几何元素基类
    /// 用于在图像上显示各种几何图形，如测量结果、检测区域等
    /// </summary>
    public abstract class GeometryElement
    {
        /// <summary>
        /// 元素类型
        /// </summary>
        public abstract GeometryElementType ElementType { get; }
        
        /// <summary>
        /// 显示颜色
        /// </summary>
        public Color Color { get; set; } = Colors.Red;
        
        /// <summary>
        /// 线宽
        /// </summary>
        public double LineWidth { get; set; } = 2.0;
        
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// 元素名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }
    }
    
    /// <summary>
    /// 点元素
    /// </summary>
    public class PointElement : GeometryElement
    {
        public override GeometryElementType ElementType => GeometryElementType.Point;
        
        /// <summary>行坐标（Y）</summary>
        public double Row { get; set; }
        
        /// <summary>列坐标（X）</summary>
        public double Column { get; set; }
        
        /// <summary>点的大小（半径）</summary>
        public double Size { get; set; } = 3.0;
        
        public PointElement(double row, double column)
        {
            Row = row;
            Column = column;
        }
        
        public override string ToString()
        {
            return $"点 ({Column:F2}, {Row:F2})";
        }
    }
    
    /// <summary>
    /// 直线元素
    /// </summary>
    public class LineElement : GeometryElement
    {
        public override GeometryElementType ElementType => GeometryElementType.Line;
        
        /// <summary>起点行坐标</summary>
        public double Row1 { get; set; }
        
        /// <summary>起点列坐标</summary>
        public double Column1 { get; set; }
        
        /// <summary>终点行坐标</summary>
        public double Row2 { get; set; }
        
        /// <summary>终点列坐标</summary>
        public double Column2 { get; set; }
        
        public LineElement(double row1, double column1, double row2, double column2)
        {
            Row1 = row1;
            Column1 = column1;
            Row2 = row2;
            Column2 = column2;
        }
        
        /// <summary>
        /// 计算直线长度
        /// </summary>
        public double Length => System.Math.Sqrt(System.Math.Pow(Row2 - Row1, 2) + System.Math.Pow(Column2 - Column1, 2));
        
        public override string ToString()
        {
            return $"直线 ({Column1:F2},{Row1:F2}) - ({Column2:F2},{Row2:F2}) 长度:{Length:F2}";
        }
    }
    
    /// <summary>
    /// 圆形元素
    /// </summary>
    public class CircleElement : GeometryElement
    {
        public override GeometryElementType ElementType => GeometryElementType.Circle;
        
        /// <summary>圆心行坐标</summary>
        public double CenterRow { get; set; }
        
        /// <summary>圆心列坐标</summary>
        public double CenterColumn { get; set; }
        
        /// <summary>半径</summary>
        public double Radius { get; set; }
        
        public CircleElement(double centerRow, double centerColumn, double radius)
        {
            CenterRow = centerRow;
            CenterColumn = centerColumn;
            Radius = radius;
        }
        
        /// <summary>
        /// 计算圆的面积
        /// </summary>
        public double Area => System.Math.PI * Radius * Radius;
        
        /// <summary>
        /// 计算圆的周长
        /// </summary>
        public double Circumference => 2 * System.Math.PI * Radius;
        
        public override string ToString()
        {
            return $"圆 中心:({CenterColumn:F2},{CenterRow:F2}) 半径:{Radius:F2}";
        }
    }
    
    /// <summary>
    /// 矩形元素
    /// </summary>
    public class RectangleElement : GeometryElement
    {
        public override GeometryElementType ElementType => GeometryElementType.Rectangle;
        
        /// <summary>左上角行坐标</summary>
        public double Row1 { get; set; }
        
        /// <summary>左上角列坐标</summary>
        public double Column1 { get; set; }
        
        /// <summary>右下角行坐标</summary>
        public double Row2 { get; set; }
        
        /// <summary>右下角列坐标</summary>
        public double Column2 { get; set; }
        
        public RectangleElement(double row1, double column1, double row2, double column2)
        {
            Row1 = row1;
            Column1 = column1;
            Row2 = row2;
            Column2 = column2;
        }
        
        /// <summary>
        /// 计算矩形宽度
        /// </summary>
        public double Width => System.Math.Abs(Column2 - Column1);
        
        /// <summary>
        /// 计算矩形高度
        /// </summary>
        public double Height => System.Math.Abs(Row2 - Row1);
        
        /// <summary>
        /// 计算矩形面积
        /// </summary>
        public double Area => Width * Height;
        
        public override string ToString()
        {
            return $"矩形 ({Column1:F2},{Row1:F2}) - ({Column2:F2},{Row2:F2}) 面积:{Area:F2}";
        }
    }
    
    /// <summary>
    /// 文本元素
    /// </summary>
    public class TextElement : GeometryElement
    {
        public override GeometryElementType ElementType => GeometryElementType.Text;
        
        /// <summary>文本内容</summary>
        public string Text { get; set; }
        
        /// <summary>文本位置行坐标</summary>
        public double Row { get; set; }
        
        /// <summary>文本位置列坐标</summary>
        public double Column { get; set; }
        
        /// <summary>字体名称</summary>
        public string FontName { get; set; } = "Arial";
        
        /// <summary>字体大小</summary>
        public double FontSize { get; set; } = 12;
        
        public TextElement(string text, double row, double column)
        {
            Text = text;
            Row = row;
            Column = column;
        }
        
        public override string ToString()
        {
            return $"文本 \"{Text}\" 位置:({Column:F2},{Row:F2})";
        }
    }
}