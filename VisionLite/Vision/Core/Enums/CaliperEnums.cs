using System.ComponentModel;

namespace VisionLite.Vision.Core.Enums
{
    /// <summary>
    /// 边缘极性枚举
    /// 定义边缘检测的方向和类型
    /// </summary>
    public enum EdgeTransition
    {
        /// <summary>正向边缘（从暗到亮）</summary>
        [Description("正向边缘")]
        Positive,
        
        /// <summary>从正值到负值的边缘</summary>
        [Description("正值到负值")]
        PositiveToNegative,
        
        /// <summary>从负值到正值的边缘</summary>
        [Description("负值到正值")]
        NegativeToPositive,
        
        /// <summary>负向边缘（从亮到暗）</summary>
        [Description("负向边缘")]
        Negative,
        
        /// <summary>所有边缘（正向和负向）</summary>
        [Description("所有边缘")]
        All
    }
    
    /// <summary>
    /// 边缘选择策略枚举
    /// </summary>
    public enum EdgeSelection
    {
        /// <summary>选择所有检测到的边缘</summary>
        [Description("所有边缘")]
        All,
        
        /// <summary>选择第一个边缘</summary>
        [Description("第一个边缘")]
        First,
        
        /// <summary>选择最后一个边缘</summary>
        [Description("最后一个边缘")]
        Last,
        
        /// <summary>选择最强的边缘</summary>
        [Description("最强边缘")]
        Strongest,
        
        /// <summary>选择最大的边缘</summary>
        [Description("最大边缘")]
        Largest
    }
    
    /// <summary>
    /// 测量模式枚举
    /// </summary>
    public enum MeasureMode
    {
        /// <summary>单边缘测量</summary>
        [Description("单边缘测量")]
        SingleEdge,
        
        /// <summary>边缘对测量</summary>
        [Description("边缘对测量")]
        EdgePairs
    }
    
    /// <summary>
    /// 卡尺工具类型枚举
    /// </summary>
    public enum CaliperType
    {
        /// <summary>边缘检测卡尺</summary>
        [Description("边缘检测")]
        EdgeDetection,
        
        /// <summary>直线拟合卡尺</summary>
        [Description("直线拟合")]
        LineFitting,
        
        /// <summary>圆形拟合卡尺</summary>
        [Description("圆形拟合")]
        CircleFitting
    }
    
    /// <summary>
    /// 圆测量方向枚举
    /// </summary>
    public enum CircleMeasureDirection
    {
        /// <summary>径向测量</summary>
        [Description("径向")]
        Radial,
        
        /// <summary>从外向内测量</summary>
        [Description("从外向内")]
        OutsideIn,
        
        /// <summary>从内向外测量</summary>
        [Description("从内向外")]
        InsideOut,
        
        /// <summary>切向测量</summary>
        [Description("切向")]
        Tangential
    }
    
    /// <summary>
    /// 拟合算法类型枚举
    /// </summary>
    public enum FittingAlgorithm
    {
        /// <summary>代数拟合（速度快）</summary>
        [Description("代数拟合")]
        Algebraic,
        
        /// <summary>最小二乘法</summary>
        [Description("最小二乘法")]
        LeastSquares,
        
        /// <summary>回归拟合</summary>
        [Description("回归拟合")]
        Regression,
        
        /// <summary>几何拟合（精度高）</summary>
        [Description("几何拟合")]
        Geometric,
        
        /// <summary>鲁棒拟合（抗干扰强）</summary>
        [Description("鲁棒拟合")]
        Tukey
    }
    
    /// <summary>
    /// ROI几何类型枚举
    /// </summary>
    public enum ROIGeometryType
    {
        /// <summary>直线</summary>
        [Description("直线")]
        Line,
        
        /// <summary>矩形</summary>
        [Description("矩形")]
        Rectangle,
        
        /// <summary>带角度的矩形</summary>
        [Description("带角度矩形")]
        Rectangle2,
        
        /// <summary>圆形</summary>
        [Description("圆形")]
        Circle,
        
        /// <summary>椭圆</summary>
        [Description("椭圆")]
        Ellipse
    }
}