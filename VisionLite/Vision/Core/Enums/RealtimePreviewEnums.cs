using System.ComponentModel;

namespace VisionLite.Vision.Core.Enums
{
    /// <summary>
    /// 实时预览级别枚举
    /// 控制参数变化时的实时预览行为
    /// </summary>
    public enum RealtimePreviewLevel
    {
        /// <summary>禁用实时预览，需要手动执行算法</summary>
        [Description("禁用")]
        Disabled,
        
        /// <summary>仅关键参数支持实时预览</summary>
        [Description("仅关键参数")]
        Essential,
        
        /// <summary>所有参数都支持实时预览（推荐）</summary>
        [Description("所有参数")]
        All
    }
}