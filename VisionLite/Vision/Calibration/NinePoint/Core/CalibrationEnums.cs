using System.ComponentModel;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    // 注意：九点标定系统固定使用仿射变换，无需枚举选择
    // 仿射变换支持平移、旋转、缩放和剪切，适用于大多数工业视觉应用

    /// <summary>
    /// 物理单位枚举
    /// </summary>
    public enum PhysicalUnit
    {
        [Description("毫米")]
        Millimeter
    }

    /// <summary>
    /// 标定质量等级
    /// </summary>
    public enum CalibrationQuality
    {
        [Description("优秀")]
        Excellent,      // <0.1mm误差
        
        [Description("良好")]
        Good,           // <0.5mm误差
        
        [Description("可接受")]
        Acceptable,     // <1.0mm误差
        
        [Description("较差")]
        Poor,           // <2.0mm误差
        
        [Description("不可用")]
        Unusable        // >2.0mm误差
    }

    /// <summary>
    /// 验证状态枚举
    /// </summary>
    public enum ValidationStatus
    {
        [Description("优秀")]
        Excellent,
        
        [Description("良好")]
        Good,
        
        [Description("可接受")]
        Acceptable,
        
        [Description("警告")]
        Warning,
        
        [Description("失败")]
        Failed,
        
        [Description("错误")]
        Error
    }

    /// <summary>
    /// 验证类型枚举
    /// </summary>
    public enum ValidationType
    {
        [Description("距离验证")]
        Distance,
        
        [Description("角度验证")]
        Angle,
        
        [Description("面积验证")]
        Area,
        
        [Description("重复性验证")]
        Repeatability,
        
        [Description("交叉验证")]
        CrossValidation
    }

    /// <summary>
    /// 标定状态枚举
    /// </summary>
    public enum CalibrationStatus
    {
        [Description("未标定")]
        NotCalibrated,
        
        [Description("标定中")]
        Calibrating,
        
        [Description("已标定")]
        Calibrated,
        
        [Description("标定无效")]
        Invalid,
        
        [Description("标定过期")]
        Expired
    }
}