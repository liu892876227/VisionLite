using System;

namespace VisionLite.Vision.Core.Attributes
{
    /// <summary>
    /// 参数特性
    /// 用于标记算法处理器中的可配置参数，支持UI自动生成
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ParameterAttribute : Attribute
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 最小值（用于数值类型）
        /// </summary>
        public double MinValue { get; set; } = double.MinValue;
        
        /// <summary>
        /// 最大值（用于数值类型）
        /// </summary>
        public double MaxValue { get; set; } = double.MaxValue;
        
        /// <summary>
        /// 步长（用于滑块控件）
        /// </summary>
        public double Step { get; set; } = 0.1;
        
        /// <summary>
        /// 小数位数
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;
        
        /// <summary>
        /// 是否为高级参数（默认不显示）
        /// </summary>
        public bool IsAdvanced { get; set; } = false;
        
        /// <summary>
        /// 参数分组
        /// </summary>
        public string Group { get; set; }
        
        /// <summary>
        /// 显示顺序
        /// </summary>
        public int Order { get; set; } = 0;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="displayName">显示名称</param>
        public ParameterAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
        
        /// <summary>
        /// 构造函数，带描述
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="description">参数描述</param>
        public ParameterAttribute(string displayName, string description) : this(displayName)
        {
            Description = description;
        }
        
        /// <summary>
        /// 构造函数，带描述和范围限制
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="description">参数描述</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        public ParameterAttribute(string displayName, string description, double minValue, double maxValue) : this(displayName)
        {
            Description = description;
            MinValue = minValue;
            MaxValue = maxValue;
        }
        
        /// <summary>
        /// 构造函数，带范围限制
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        public ParameterAttribute(string displayName, double minValue, double maxValue) : this(displayName)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }
}