using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// 参数类型枚举
    /// </summary>
    public enum ParameterType
    {
        /// <summary>浮点数</summary>
        Double,
        /// <summary>整数</summary>
        Integer,
        /// <summary>布尔值</summary>
        Boolean,
        /// <summary>字符串</summary>
        String,
        /// <summary>枚举</summary>
        Enum,
        /// <summary>颜色</summary>
        Color,
        /// <summary>点</summary>
        Point,
        /// <summary>矩形</summary>
        Rectangle
    }
    
    /// <summary>
    /// 参数信息类
    /// 用于描述算法参数的各种属性，支持UI自动生成
    /// </summary>
    public class ParameterInfo : INotifyPropertyChanged
    {
        private object _value;
        
        /// <summary>
        /// 参数名称（用于代码）
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 显示名称（用于UI）
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 参数类型
        /// </summary>
        public ParameterType ParameterType { get; set; }
        
        /// <summary>
        /// 参数值
        /// </summary>
        public object Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
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
        /// 小数位数（用于显示）
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;
        
        /// <summary>
        /// 枚举值列表（用于枚举类型）
        /// </summary>
        public List<object> EnumValues { get; set; }
        
        /// <summary>
        /// 枚举显示名称列表
        /// </summary>
        public List<string> EnumDisplayNames { get; set; }
        
        /// <summary>
        /// 是否有数值范围限制
        /// </summary>
        public bool HasRange => MinValue != double.MinValue && MaxValue != double.MaxValue;
        
        /// <summary>
        /// 是否为只读参数
        /// </summary>
        public bool IsReadOnly { get; set; }
        
        /// <summary>
        /// 是否为高级参数（默认不显示）
        /// </summary>
        public bool IsAdvanced { get; set; }
        
        /// <summary>
        /// 参数分组名称
        /// </summary>
        public string Group { get; set; }
        
        /// <summary>
        /// 显示顺序
        /// </summary>
        public int Order { get; set; }
        
        /// <summary>
        /// 参数值变化事件
        /// </summary>
        public event EventHandler ValueChanged;
        
        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ParameterInfo()
        {
            EnumValues = new List<object>();
            EnumDisplayNames = new List<string>();
        }
        
        /// <summary>
        /// 验证参数值是否有效
        /// </summary>
        /// <returns>验证结果</returns>
        public bool ValidateValue()
        {
            try
            {
                switch (ParameterType)
                {
                    case ParameterType.Double:
                        if (Value is double doubleVal)
                        {
                            return doubleVal >= MinValue && doubleVal <= MaxValue;
                        }
                        break;
                        
                    case ParameterType.Integer:
                        if (Value is int intVal)
                        {
                            return intVal >= MinValue && intVal <= MaxValue;
                        }
                        break;
                        
                    case ParameterType.String:
                        return Value is string;
                        
                    case ParameterType.Boolean:
                        return Value is bool;
                        
                    case ParameterType.Enum:
                        return EnumValues.Contains(Value);
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            Value = GetDefaultValue();
        }
        
        /// <summary>
        /// 获取默认值
        /// </summary>
        /// <returns>默认值</returns>
        public object GetDefaultValue()
        {
            return ParameterType switch
            {
                ParameterType.Double => HasRange ? (MinValue + MaxValue) / 2 : 0.0,
                ParameterType.Integer => HasRange ? (int)((MinValue + MaxValue) / 2) : 0,
                ParameterType.Boolean => false,
                ParameterType.String => string.Empty,
                ParameterType.Enum => EnumValues.Count > 0 ? EnumValues[0] : null,
                _ => null
            };
        }
        
        /// <summary>
        /// 克隆参数信息
        /// </summary>
        /// <returns>克隆的参数信息</returns>
        public ParameterInfo Clone()
        {
            return new ParameterInfo
            {
                Name = this.Name,
                DisplayName = this.DisplayName,
                Description = this.Description,
                ParameterType = this.ParameterType,
                Value = this.Value,
                MinValue = this.MinValue,
                MaxValue = this.MaxValue,
                Step = this.Step,
                DecimalPlaces = this.DecimalPlaces,
                EnumValues = new List<object>(this.EnumValues),
                EnumDisplayNames = new List<string>(this.EnumDisplayNames),
                IsReadOnly = this.IsReadOnly,
                IsAdvanced = this.IsAdvanced,
                Group = this.Group,
                Order = this.Order
            };
        }
        
        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// 获取参数信息的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"{DisplayName ?? Name}: {Value} ({ParameterType})";
        }
    }
}