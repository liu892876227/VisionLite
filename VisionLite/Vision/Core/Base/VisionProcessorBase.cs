using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Interfaces;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Core.Base
{
    /// <summary>
    /// 视觉处理器基类
    /// 提供参数管理、反射等通用功能
    /// </summary>
    public abstract class VisionProcessorBase : IVisionProcessor
    {
        /// <summary>
        /// 处理器名称
        /// </summary>
        public abstract string ProcessorName { get; }
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public abstract string Category { get; }
        
        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public abstract Task<ProcessResult> ProcessAsync(VisionImage inputImage);
        
        /// <summary>
        /// 获取所有参数信息
        /// 通过反射自动获取标记了ParameterAttribute的属性
        /// </summary>
        /// <returns>参数信息列表</returns>
        public virtual List<Models.ParameterInfo> GetParameters()
        {
            var parameters = new List<Models.ParameterInfo>();
            
            // 获取所有标记了ParameterAttribute的属性
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.HasCustomAttribute<ParameterAttribute>())
                .OrderBy(p => p.GetCustomAttribute<ParameterAttribute>().Order);
            
            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<ParameterAttribute>();
                var parameterInfo = CreateParameterInfo(property, attr);
                parameters.Add(parameterInfo);
            }
            
            return parameters;
        }
        
        /// <summary>
        /// 设置参数值
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <param name="value">参数值</param>
        public virtual void SetParameter(string name, object value)
        {
            var property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                try
                {
                    // 类型转换
                    var convertedValue = ConvertValue(value, property.PropertyType);
                    property.SetValue(this, convertedValue);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"无法设置参数 {name}，值: {value}", ex);
                }
            }
            else
            {
                throw new ArgumentException($"参数 {name} 不存在或不可写");
            }
        }
        
        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <returns>参数值</returns>
        public virtual object GetParameter(string name)
        {
            var property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                return property.GetValue(this);
            }
            
            throw new ArgumentException($"参数 {name} 不存在或不可读");
        }
        
        /// <summary>
        /// 验证所有参数
        /// </summary>
        /// <returns>验证结果</returns>
        protected virtual bool ValidateParameters()
        {
            try
            {
                var parameters = GetParameters();
                return parameters.All(p => p.ValidateValue());
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 创建参数信息对象
        /// </summary>
        /// <param name="property">属性信息</param>
        /// <param name="attr">参数特性</param>
        /// <returns>参数信息</returns>
        private Models.ParameterInfo CreateParameterInfo(PropertyInfo property, ParameterAttribute attr)
        {
            var parameterInfo = new Models.ParameterInfo
            {
                Name = property.Name,
                DisplayName = attr.DisplayName ?? property.Name,
                Description = attr.Description,
                Value = property.GetValue(this),
                MinValue = attr.MinValue,
                MaxValue = attr.MaxValue,
                Step = attr.Step,
                DecimalPlaces = attr.DecimalPlaces,
                IsAdvanced = attr.IsAdvanced,
                Group = attr.Group,
                Order = attr.Order,
                ParameterType = GetParameterType(property.PropertyType)
            };
            
            // 处理枚举类型
            if (property.PropertyType.IsEnum)
            {
                var enumValues = Enum.GetValues(property.PropertyType);
                foreach (var enumValue in enumValues)
                {
                    parameterInfo.EnumValues.Add(enumValue);
                    parameterInfo.EnumDisplayNames.Add(enumValue.ToString());
                }
            }
            
            return parameterInfo;
        }
        
        /// <summary>
        /// 获取参数类型
        /// </summary>
        /// <param name="propertyType">属性类型</param>
        /// <returns>参数类型</returns>
        private ParameterType GetParameterType(Type propertyType)
        {
            if (propertyType == typeof(double) || propertyType == typeof(float))
                return ParameterType.Double;
            
            if (propertyType == typeof(int) || propertyType == typeof(long) || propertyType == typeof(short))
                return ParameterType.Integer;
            
            if (propertyType == typeof(bool))
                return ParameterType.Boolean;
            
            if (propertyType == typeof(string))
                return ParameterType.String;
            
            if (propertyType.IsEnum)
                return ParameterType.Enum;
            
            if (propertyType == typeof(System.Windows.Media.Color))
                return ParameterType.Color;
            
            // 默认返回字符串类型
            return ParameterType.String;
        }
        
        /// <summary>
        /// 值类型转换
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换后的值</returns>
        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;
            
            if (targetType.IsEnum && value is string stringValue)
                return Enum.Parse(targetType, stringValue);
            
            return Convert.ChangeType(value, targetType);
        }
        
        /// <summary>
        /// 创建成功的处理结果
        /// </summary>
        /// <param name="outputImage">输出图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <param name="measurements">测量结果</param>
        /// <returns>处理结果</returns>
        protected ProcessResult CreateSuccessResult(VisionImage outputImage, TimeSpan processingTime, 
            Dictionary<string, object> measurements = null)
        {
            var result = ProcessResult.CreateSuccess(outputImage, processingTime);
            result.ProcessorName = ProcessorName;
            
            if (measurements != null)
            {
                foreach (var measurement in measurements)
                {
                    result.AddMeasurement(measurement.Key, measurement.Value);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 创建失败的处理结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常对象</param>
        /// <returns>处理结果</returns>
        protected ProcessResult CreateFailureResult(string errorMessage, Exception exception = null)
        {
            var result = ProcessResult.CreateFailure(errorMessage, exception);
            result.ProcessorName = ProcessorName;
            return result;
        }
    }
}

/// <summary>
/// 扩展方法类
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// 检查属性是否有指定的特性
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    /// <param name="property">属性信息</param>
    /// <returns>是否有特性</returns>
    public static bool HasCustomAttribute<T>(this PropertyInfo property) where T : Attribute
    {
        return property.GetCustomAttribute<T>() != null;
    }
}