using System.ComponentModel;

namespace VisionLite.Vision.Processors.ImageMatching.Models
{
    /// <summary>
    /// 形状匹配类型
    /// </summary>
    public enum ShapeMatchingType
    {
        [Description("标准匹配")]
        Standard,

        [Description("缩放匹配")]
        Scaled,

        [Description("各向异性匹配")]
        Anisotropic
    }

    /// <summary>
    /// 金字塔层数
    /// </summary>
    public enum PyramidLevels
    {
        [Description("自动")]
        Auto = 0,

        [Description("1层")]
        Level1 = 1,

        [Description("2层")]
        Level2 = 2,

        [Description("3层")]
        Level3 = 3,

        [Description("4层")]
        Level4 = 4,

        [Description("5层")]
        Level5 = 5
    }

    /// <summary>
    /// 点约简级别
    /// </summary>
    public enum PointReduction
    {
        [Description("无约简")]
        None,

        [Description("低级约简")]
        Low,

        [Description("中级约简")]
        Medium,

        [Description("高级约简")]
        High
    }
}