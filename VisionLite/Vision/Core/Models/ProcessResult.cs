using System;
using System.Collections.Generic;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// 处理结果类
    /// 封装算法处理的所有结果信息
    /// </summary>
    public class ProcessResult
    {
        /// <summary>
        /// 处理是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 错误消息（当Success为false时）
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 输出图像
        /// </summary>
        public VisionImage OutputImage { get; set; }
        
        /// <summary>
        /// 测量结果字典
        /// 存储各种数值测量结果，如长度、角度、面积等
        /// </summary>
        public Dictionary<string, object> Measurements { get; set; }
        
        /// <summary>
        /// 几何元素列表
        /// 存储检测到的几何图形，如圆、直线、点等
        /// </summary>
        public List<GeometryElement> GeometryElements { get; set; }
        
        /// <summary>
        /// 处理耗时
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
        
        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception Exception { get; set; }
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public string ProcessorName { get; set; }
        
        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessTime { get; set; }
        
        /// <summary>
        /// 额外的元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ProcessResult()
        {
            Measurements = new Dictionary<string, object>();
            GeometryElements = new List<GeometryElement>();
            Metadata = new Dictionary<string, object>();
            ProcessTime = DateTime.Now;
        }
        
        /// <summary>
        /// 创建成功的结果
        /// </summary>
        /// <param name="outputImage">输出图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>成功的处理结果</returns>
        public static ProcessResult CreateSuccess(VisionImage outputImage, TimeSpan processingTime)
        {
            return new ProcessResult
            {
                Success = true,
                OutputImage = outputImage,
                ProcessingTime = processingTime
            };
        }
        
        /// <summary>
        /// 创建失败的结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常对象</param>
        /// <returns>失败的处理结果</returns>
        public static ProcessResult CreateFailure(string errorMessage, Exception exception = null)
        {
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
        
        /// <summary>
        /// 添加测量结果
        /// </summary>
        /// <param name="name">测量名称</param>
        /// <param name="value">测量值</param>
        public void AddMeasurement(string name, object value)
        {
            Measurements[name] = value;
        }
        
        /// <summary>
        /// 添加几何元素
        /// </summary>
        /// <param name="element">几何元素</param>
        public void AddGeometryElement(GeometryElement element)
        {
            GeometryElements.Add(element);
        }
        
        /// <summary>
        /// 添加元数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddMetadata(string key, object value)
        {
            Metadata[key] = value;
        }
        
        /// <summary>
        /// 获取结果摘要
        /// </summary>
        /// <returns>结果摘要字符串</returns>
        public override string ToString()
        {
            if (Success)
            {
                return $"处理成功 - 耗时: {ProcessingTime.TotalMilliseconds:F2}ms, 测量项: {Measurements.Count}, 几何元素: {GeometryElements.Count}";
            }
            else
            {
                return $"处理失败 - {ErrorMessage}";
            }
        }
    }
}