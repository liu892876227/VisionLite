// Communication/Models/ParameterDefinition.cs
// 通讯协议参数定义模型 - 定义协议参数的元数据信息
using System;

namespace VisionLite.Communication.Models
{
    /// <summary>
    /// 参数类型枚举
    /// 定义支持的参数输入类型，用于动态生成对应的UI控件
    /// </summary>
    public enum ParameterType
    {
        String,      // 字符串类型 - 普通文本框
        Integer,     // 整数类型 - 数字输入框，只允许整数
        Double,      // 浮点数类型 - 数字输入框，允许小数
        Boolean,     // 布尔类型 - 复选框
        IPAddress,   // IP地址类型 - 带格式验证的文本框
        Port,        // 端口号类型 - 范围为1-65535的整数输入框
        ComboBox,    // 下拉选择类型 - 从预定义选项中选择
        FilePath,    // 文件路径类型 - 带浏览按钮的文本框
        Password     // 密码类型 - 密码输入框
    }

    /// <summary>
    /// 协议参数定义类
    /// 描述一个协议参数的所有元数据信息，用于动态生成配置界面
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// 参数的唯一键名，用于在代码中识别和存储参数值
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 参数的显示名称，在UI界面中显示给用户的友好名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 参数的数据类型，决定生成什么样的输入控件
        /// </summary>
        public ParameterType Type { get; set; }

        /// <summary>
        /// 参数的默认值，创建新配置时的初始值
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// 是否为必需参数，必需参数在验证时不能为空
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// 参数的描述信息，用作工具提示或帮助文本
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 有效值列表，用于ComboBox类型参数的选项列表
        /// 对于其他类型可以作为验证约束使用
        /// </summary>
        public object[] ValidValues { get; set; }

        /// <summary>
        /// 参数的最小值约束（用于数值类型）
        /// </summary>
        public object MinValue { get; set; }

        /// <summary>
        /// 参数的最大值约束（用于数值类型）
        /// </summary>
        public object MaxValue { get; set; }

        /// <summary>
        /// 参数的正则表达式验证模式（用于字符串类型）
        /// </summary>
        public string ValidationPattern { get; set; }

        /// <summary>
        /// 自定义验证错误消息
        /// </summary>
        public string ValidationErrorMessage { get; set; }

        /// <summary>
        /// 参数是否在UI中可见（某些内部参数可能不需要用户配置）
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 参数是否可编辑（某些参数可能只读）
        /// </summary>
        public bool IsEditable { get; set; } = true;

        /// <summary>
        /// UI控件的宽度建议（像素）
        /// </summary>
        public double? ControlWidth { get; set; }

        /// <summary>
        /// 验证参数值是否有效
        /// </summary>
        /// <param name="value">要验证的值</param>
        /// <returns>验证结果，包含是否有效和错误消息</returns>
        public ValidationResult ValidateValue(object value)
        {
            // 必需参数检查
            if (IsRequired && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
            {
                return new ValidationResult(false, $"{DisplayName}是必需参数，不能为空");
            }

            // 如果值为空且不是必需参数，则认为有效
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult(true);
            }

            try
            {
                switch (Type)
                {
                    case ParameterType.Integer:
                        return ValidateInteger(value);
                    
                    case ParameterType.Double:
                        return ValidateDouble(value);
                    
                    case ParameterType.Port:
                        return ValidatePort(value);
                    
                    case ParameterType.IPAddress:
                        return ValidateIPAddress(value);
                    
                    case ParameterType.ComboBox:
                        return ValidateComboBoxValue(value);
                    
                    case ParameterType.String:
                    case ParameterType.Password:
                    case ParameterType.FilePath:
                        return ValidateString(value);
                    
                    case ParameterType.Boolean:
                        return new ValidationResult(true); // 布尔值总是有效
                    
                    default:
                        return new ValidationResult(true);
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"参数验证异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证整数类型参数
        /// </summary>
        private ValidationResult ValidateInteger(object value)
        {
            if (!int.TryParse(value.ToString(), out int intValue))
            {
                return new ValidationResult(false, $"{DisplayName}必须是整数");
            }

            if (MinValue != null && intValue < Convert.ToInt32(MinValue))
            {
                return new ValidationResult(false, $"{DisplayName}不能小于{MinValue}");
            }

            if (MaxValue != null && intValue > Convert.ToInt32(MaxValue))
            {
                return new ValidationResult(false, $"{DisplayName}不能大于{MaxValue}");
            }

            return new ValidationResult(true);
        }

        /// <summary>
        /// 验证浮点数类型参数
        /// </summary>
        private ValidationResult ValidateDouble(object value)
        {
            if (!double.TryParse(value.ToString(), out double doubleValue))
            {
                return new ValidationResult(false, $"{DisplayName}必须是数字");
            }

            if (MinValue != null && doubleValue < Convert.ToDouble(MinValue))
            {
                return new ValidationResult(false, $"{DisplayName}不能小于{MinValue}");
            }

            if (MaxValue != null && doubleValue > Convert.ToDouble(MaxValue))
            {
                return new ValidationResult(false, $"{DisplayName}不能大于{MaxValue}");
            }

            return new ValidationResult(true);
        }

        /// <summary>
        /// 验证端口号类型参数
        /// </summary>
        private ValidationResult ValidatePort(object value)
        {
            if (!int.TryParse(value.ToString(), out int port))
            {
                return new ValidationResult(false, $"{DisplayName}必须是整数");
            }

            if (port < 1 || port > 65535)
            {
                return new ValidationResult(false, $"{DisplayName}必须在1-65535范围内");
            }

            return new ValidationResult(true);
        }

        /// <summary>
        /// 验证IP地址类型参数
        /// </summary>
        private ValidationResult ValidateIPAddress(object value)
        {
            if (!System.Net.IPAddress.TryParse(value.ToString(), out _))
            {
                return new ValidationResult(false, $"{DisplayName}必须是有效的IP地址格式");
            }

            return new ValidationResult(true);
        }

        /// <summary>
        /// 验证下拉框选择值
        /// </summary>
        private ValidationResult ValidateComboBoxValue(object value)
        {
            if (ValidValues != null && ValidValues.Length > 0)
            {
                string stringValue = value.ToString();
                foreach (var validValue in ValidValues)
                {
                    if (validValue.ToString().Equals(stringValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ValidationResult(true);
                    }
                }
                return new ValidationResult(false, $"{DisplayName}的值必须从预定义选项中选择");
            }

            return new ValidationResult(true);
        }

        /// <summary>
        /// 验证字符串类型参数
        /// </summary>
        private ValidationResult ValidateString(object value)
        {
            string stringValue = value.ToString();

            // 正则表达式验证
            if (!string.IsNullOrEmpty(ValidationPattern))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, ValidationPattern))
                {
                    string errorMessage = !string.IsNullOrEmpty(ValidationErrorMessage) 
                        ? ValidationErrorMessage 
                        : $"{DisplayName}格式不正确";
                    return new ValidationResult(false, errorMessage);
                }
            }

            return new ValidationResult(true);
        }
    }

    /// <summary>
    /// 参数验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 验证是否通过
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 验证失败时的错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isValid">是否有效</param>
        /// <param name="errorMessage">错误消息（可选）</param>
        public ValidationResult(bool isValid, string errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}