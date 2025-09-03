using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Core.Interfaces
{
    /// <summary>
    /// 视觉处理器接口
    /// 所有视觉算法处理器都需要实现此接口
    /// </summary>
    public interface IVisionProcessor
    {
        /// <summary>
        /// 处理器名称，用于UI显示
        /// </summary>
        string ProcessorName { get; }
        
        /// <summary>
        /// 处理器分类，用于算法分组
        /// </summary>
        string Category { get; }
        
        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        Task<ProcessResult> ProcessAsync(VisionImage inputImage);
        
        /// <summary>
        /// 获取所有参数信息
        /// </summary>
        /// <returns>参数信息列表</returns>
        List<Models.ParameterInfo> GetParameters();
        
        /// <summary>
        /// 设置参数值
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <param name="value">参数值</param>
        void SetParameter(string name, object value);
    }
}