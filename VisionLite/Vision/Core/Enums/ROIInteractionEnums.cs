using System.ComponentModel;

namespace VisionLite.Vision.Core.Enums
{
    /// <summary>
    /// ROI交互模式
    /// </summary>
    public enum ROIInteractionMode
    {
        [Description("无交互")]
        None,

        [Description("模板ROI")]
        TemplateROI,

        [Description("搜索ROI")]
        SearchROI
    }
}