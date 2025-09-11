using System.ComponentModel;

namespace VisionLite.Vision.Core.Enums
{
    /// <summary>
    /// 边缘极性枚举
    /// 定义边缘检测的方向和类型
    /// </summary>
    public enum EdgeTransition
    {
        /// <summary>从暗到亮边缘（检测白色/亮色目标）</summary>
        [Description("从暗到亮")]
        Positive,
        
        /// <summary>从亮到暗边缘（检测黑色/暗色目标）</summary>
        [Description("从亮到暗")]
        Negative,
        
        /// <summary>所有边缘（通用检测）</summary>
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
        Strongest
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
    /// 适用于圆形拟合和直线拟合（向后兼容）
    /// </summary>
    public enum FittingAlgorithm
    {
        /// <summary>代数拟合（速度快）</summary>
        [Description("代数拟合")]
        Algebraic,
        
        /// <summary>几何拟合（精度高）</summary>
        [Description("几何拟合")]
        Geometric,
        
        /// <summary>自适应Huber拟合（抗噪声）</summary>
        [Description("自适应Huber")]
        AHuber,
        
        /// <summary>自适应Tukey拟合（抗异常点）</summary>
        [Description("自适应Tukey")]
        ATukey,
        
        /// <summary>几何Huber拟合（精度+抗噪声）</summary>
        [Description("几何Huber")]
        GeoHuber,
        
        /// <summary>几何Tukey拟合（精度+抗异常点）</summary>
        [Description("几何Tukey")]
        GeoTukey
    }
    
    /// <summary>
    /// 直线拟合算法枚举
    /// 专用于FitLineContourXld算子
    /// </summary>
    public enum LineFittingAlgorithm
    {
        /// <summary>标准回归（最小二乘法）</summary>
        [Description("标准回归")]
        Regression,
        
        /// <summary>高斯加权拟合</summary>
        [Description("高斯拟合")]
        Gauss,
        
        /// <summary>Huber加权拟合（抗噪声）</summary>
        [Description("Huber拟合")]
        Huber,
        
        /// <summary>Tukey加权拟合（抗异常点）</summary>
        [Description("Tukey拟合")]
        Tukey,
        
        /// <summary>忽略离群点的拟合</summary>
        [Description("抗离群点")]
        Drop
    }
    
    /// <summary>
    /// 圆拟合算法枚举
    /// 专用于FitCircleContourXld算子
    /// </summary>
    public enum CircleFittingAlgorithm
    {
        /// <summary>代数拟合（速度快）</summary>
        [Description("代数拟合")]
        Algebraic,
        
        /// <summary>几何拟合（精度高）</summary>
        [Description("几何拟合")]
        Geometric,
        
        /// <summary>自适应Huber拟合（抗噪声）</summary>
        [Description("自适应Huber")]
        AHuber,
        
        /// <summary>自适应Tukey拟合（抗异常点）</summary>
        [Description("自适应Tukey")]
        ATukey,
        
        /// <summary>几何Huber拟合（精度+抗噪声）</summary>
        [Description("几何Huber")]
        GeoHuber,
        
        /// <summary>几何Tukey拟合（精度+抗异常点）</summary>
        [Description("几何Tukey")]
        GeoTukey
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