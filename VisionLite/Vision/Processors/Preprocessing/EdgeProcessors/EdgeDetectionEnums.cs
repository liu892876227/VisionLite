using System.ComponentModel;

namespace VisionLite.Vision.Processors.Preprocessing.EdgeProcessors
{
    /// <summary>
    /// 边缘极性类型
    /// </summary>
    public enum EdgePolarity
    {
        [Description("正边缘")]
        Positive,

        [Description("负边缘")]
        Negative,

        [Description("所有边缘")]
        All
    }

    /// <summary>
    /// 边缘连接模式
    /// </summary>
    public enum EdgeConnection
    {
        [Description("4连通")]
        Connected4,

        [Description("8连通")]
        Connected8
    }

    /// <summary>
    /// Sobel算子方向
    /// </summary>
    public enum SobelDirection
    {
        [Description("X方向梯度")]
        X,

        [Description("Y方向梯度")]
        Y,

        [Description("梯度幅值")]
        Amplitude
    }

    /// <summary>
    /// Laplacian算子类型
    /// </summary>
    public enum LaplacianType
    {
        [Description("标准拉普拉斯")]
        Standard,

        [Description("高斯-拉普拉斯")]
        OfGauss
    }

    /// <summary>
    /// 边缘检测结果类型
    /// </summary>
    public enum EdgeResultType
    {
        [Description("二值边缘图")]
        BinaryEdges,

        [Description("边缘轮廓")]
        EdgeContours,

        [Description("梯度幅值图")]
        GradientMagnitude
    }
}