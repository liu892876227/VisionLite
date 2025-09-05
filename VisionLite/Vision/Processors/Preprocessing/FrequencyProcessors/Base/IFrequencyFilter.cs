using HalconDotNet;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base
{
    /// <summary>
    /// 频域滤波器接口
    /// 定义频域滤波器的基本操作
    /// </summary>
    public interface IFrequencyFilter
    {
        /// <summary>
        /// 滤波器类型
        /// </summary>
        FilterType FilterType { get; }
        
        /// <summary>
        /// 生成滤波器掩码
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>滤波器掩码</returns>
        HObject GenerateFilterMask(int width, int height);
        
        /// <summary>
        /// 应用滤波器到频谱
        /// </summary>
        /// <param name="complexSpectrum">复数频谱</param>
        /// <param name="filterMask">滤波器掩码</param>
        /// <returns>滤波后的频谱</returns>
        HObject ApplyFilter(HObject complexSpectrum, HObject filterMask);
        
        /// <summary>
        /// 计算滤波器的频率响应
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>频率响应数组</returns>
        double[] CalculateFrequencyResponse(int width, int height);
        
        /// <summary>
        /// 验证滤波器参数是否有效
        /// </summary>
        /// <returns>参数是否有效</returns>
        bool ValidateParameters();
        
        /// <summary>
        /// 获取滤波器的显示名称
        /// </summary>
        /// <returns>显示名称</returns>
        string GetDisplayName();
    }
    
    /// <summary>
    /// 频域变换接口
    /// 定义频域变换的基本操作
    /// </summary>
    public interface IFrequencyTransform
    {
        /// <summary>
        /// 执行前向变换
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>变换结果</returns>
        FrequencyProcessResult ExecuteForwardTransform(VisionImage inputImage);
        
        /// <summary>
        /// 执行反向变换
        /// </summary>
        /// <param name="complexData">复数数据</param>
        /// <returns>变换结果</returns>
        VisionImage ExecuteInverseTransform(HObject complexData);
        
        /// <summary>
        /// 生成频谱可视化
        /// </summary>
        /// <param name="complexData">复数数据</param>
        /// <param name="displayType">显示类型</param>
        /// <returns>频谱图像</returns>
        VisionImage GenerateSpectrumVisualization(HObject complexData, SpectrumDisplayType displayType);
    }
}