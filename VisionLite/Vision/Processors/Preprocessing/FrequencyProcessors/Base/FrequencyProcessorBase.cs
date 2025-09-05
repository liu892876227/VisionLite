using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.FrequencyProcessors.Base
{
    /// <summary>
    /// 频域处理器基类
    /// 为所有频域处理算法提供统一的基础功能
    /// </summary>
    public abstract class FrequencyProcessorBase : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器分类 - 所有频域处理器都属于"频域处理"分类
        /// </summary>
        public override string Category => "频域处理";
        
        /// <summary>
        /// 是否显示频谱图
        /// </summary>
        [Parameter("显示频谱图", "是否在结果中包含频谱可视化", Group = "显示设置")]
        public bool ShowSpectrum { get; set; } = true;
        
        /// <summary>
        /// 频谱显示类型
        /// </summary>
        [Parameter("频谱显示类型", "选择频谱的显示方式", Group = "显示设置")]
        public SpectrumDisplayType DisplayType { get; set; } = SpectrumDisplayType.LogMagnitude;
        
        /// <summary>
        /// 是否保留原图像尺寸
        /// </summary>
        [Parameter("保留原图像尺寸", "处理后是否保持与输入图像相同的尺寸", Group = "处理设置")]
        public bool PreserveImageSize { get; set; } = true;
        
        
        #endregion
        
        #region 私有字段
        
        
        /// <summary>
        /// 频域处理结果
        /// </summary>
        protected FrequencyProcessResult _frequencyResult;
        
        #endregion
        
        #region 主要方法
        
        /// <summary>
        /// 异步处理图像
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>处理结果</returns>
        public override async Task<ProcessResult> ProcessAsync(VisionImage inputImage)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // 参数验证
                ValidateInputs(inputImage);
                
                // 让出线程控制权，但继续在相同线程执行以保持Halcon对象线程安全
                await Task.Yield();
                
                // 执行频域处理
                _frequencyResult = ExecuteFrequencyProcess(inputImage);
                
                
                // 计算处理时间
                var processingTime = DateTime.Now - startTime;
                
                // 创建测量结果
                var measurements = CreateFrequencyMeasurements(inputImage, _frequencyResult, processingTime);
                
                // 返回成功结果
                return CreateSuccessResult(_frequencyResult.ProcessedImage, processingTime, measurements);
            }
            catch (Exception ex)
            {
                // 清理资源
                _frequencyResult?.Dispose();
                return CreateFailureResult($"{ProcessorName}处理失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region 抽象方法
        
        /// <summary>
        /// 执行频域处理的抽象方法
        /// 子类必须实现具体的频域处理算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>频域处理结果</returns>
        protected abstract FrequencyProcessResult ExecuteFrequencyProcess(VisionImage inputImage);
        
        #endregion
        
        #region 保护方法
        
        /// <summary>
        /// 验证输入参数
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        protected virtual void ValidateInputs(VisionImage inputImage)
        {
            if (inputImage == null)
                throw new ArgumentNullException(nameof(inputImage), "输入图像不能为空");
                
            if (inputImage.HImage == null)
                throw new ArgumentException("输入图像数据无效");
        }
        
        /// <summary>
        /// 生成频谱可视化
        /// </summary>
        /// <param name="complexImage">复数图像</param>
        /// <returns>频谱图像</returns>
        protected virtual VisionImage GenerateSpectrumVisualization(HObject complexImage)
        {
            if (complexImage == null || !ShowSpectrum)
                return null;
                
            try
            {
                HObject visualImage = null;
                
                switch (DisplayType)
                {
                    case SpectrumDisplayType.Magnitude:
                        HOperatorSet.ComplexToReal(complexImage, out visualImage, out HObject _);
                        break;
                        
                    case SpectrumDisplayType.LogMagnitude:
                        HOperatorSet.ComplexToReal(complexImage, out HObject magImage, out HObject _);
                        HOperatorSet.LogImage(magImage, out visualImage, "e");
                        magImage?.Dispose();
                        break;
                        
                    case SpectrumDisplayType.Phase:
                        HOperatorSet.ComplexToReal(complexImage, out HObject _, out visualImage);
                        break;
                        
                    case SpectrumDisplayType.Power:
                        HOperatorSet.PowerReal(complexImage, out visualImage);
                        break;
                        
                    case SpectrumDisplayType.LogPower:
                        HOperatorSet.PowerLn(complexImage, out visualImage);
                        break;
                        
                    default:
                        HOperatorSet.ComplexToReal(complexImage, out visualImage, out HObject _);
                        break;
                }
                
                return new VisionImage(visualImage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"频谱可视化生成失败: {ex.Message}", ex);
            }
        }
        
        
        /// <summary>
        /// 创建频域测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="result">频域处理结果</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        protected virtual Dictionary<string, object> CreateFrequencyMeasurements(
            VisionImage inputImage, FrequencyProcessResult result, TimeSpan processingTime)
        {
            var measurements = new Dictionary<string, object>
            {
                ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                ["图像通道数"] = inputImage.Channels,
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = ProcessorName,
                ["频谱显示类型"] = DisplayType.ToString()
            };
            
            
            // 添加频域特有的统计信息
            if (result?.Statistics != null)
            {
                foreach (var stat in result.Statistics)
                {
                    measurements[stat.Key] = stat.Value;
                }
            }
            
            // 添加频谱信息
            measurements["包含频谱数据"] = result?.HasSpectrumData == true ? "是" : "否";
            measurements["包含复数数据"] = result?.HasComplexData == true ? "是" : "否";
            measurements["包含频率响应"] = result?.HasFrequencyResponse == true ? "是" : "否";
            
            return measurements;
        }
        
        #endregion
    }
}