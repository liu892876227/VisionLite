using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.MorphologyProcessors;

namespace VisionLite.Vision.Processors.Preprocessing.MorphologyProcessors
{
    /// <summary>
    /// 形态学膨胀处理器
    /// 使用Halcon DilationCircle和DilationRectangle1算子实现形态学膨胀操作
    /// </summary>
    public class DilationProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "形态学膨胀";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 结构元素类型
        /// </summary>
        [Parameter("结构元素类型", "选择圆形或矩形结构元素，影响膨胀的形状特征")]
        public StructuringElementType ElementType { get; set; } = StructuringElementType.Circle;
        
        /// <summary>
        /// 半径
        /// </summary>
        [Parameter("半径", "圆形结构元素的半径，值越大膨胀效果越强", 0.5, 50.0, DecimalPlaces = 1)]
        public double Radius { get; set; } = 3.5;
        
        /// <summary>
        /// 宽度
        /// </summary>
        [Parameter("宽度", "矩形结构元素的宽度，必须为奇数", 1, 201, DecimalPlaces = 0)]
        public int Width { get; set; } = 11;
        
        /// <summary>
        /// 高度
        /// </summary>
        [Parameter("高度", "矩形结构元素的高度，必须为奇数", 1, 201, DecimalPlaces = 0)]
        public int Height { get; set; } = 11;
        
        /// <summary>
        /// 反转输出
        /// </summary>
        [Parameter("反转输出", "是否反转膨胀结果", Group = "高级设置", IsAdvanced = true)]
        public bool InvertOutput { get; set; } = false;
        
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
                
                // 执行形态学膨胀
                var outputImage = await Task.Run(() => ExecuteDilation(inputImage));
                
                // 计算处理时间
                var processingTime = DateTime.Now - startTime;
                
                // 创建测量结果
                var measurements = CreateMeasurements(inputImage, outputImage, processingTime);
                
                // 返回成功结果
                return CreateSuccessResult(outputImage, processingTime, measurements);
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return CreateFailureResult($"形态学膨胀处理失败: {ex.Message}", ex);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 验证输入参数
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        private void ValidateInputs(VisionImage inputImage)
        {
            if (inputImage == null)
                throw new ArgumentNullException(nameof(inputImage), "输入图像不能为空");
                
            if (inputImage.HImage == null)
                throw new ArgumentException("输入图像数据无效");
                
            // 参数验证和自动调整
            ValidateAndAdjustParameters();
        }
        
        /// <summary>
        /// 参数验证和自动调整
        /// </summary>
        private void ValidateAndAdjustParameters()
        {
            // 圆形半径范围检查
            Radius = Math.Max(0.5, Math.Min(50.0, Radius));
            
            // 矩形尺寸必须为奇数且在有效范围内
            Width = Math.Max(1, Math.Min(201, Width));
            Height = Math.Max(1, Math.Min(201, Height));
            
            // 确保为奇数
            if (Width % 2 == 0) Width++;
            if (Height % 2 == 0) Height++;
        }
        
        /// <summary>
        /// 执行形态学膨胀算法
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>膨胀后的图像</returns>
        private VisionImage ExecuteDilation(VisionImage inputImage)
        {
            HObject outputImage = null;
            
            try
            {
                // 检查输入图像是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入图像对象为空");
                
                // 获取图像尺寸验证图像有效性
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);
                if (width.I <= 0 || height.I <= 0)
                    throw new ArgumentException($"输入图像尺寸无效: {width.I}×{height.I}");
                
                // 步骤1：将图像转换为区域（假设处理二值图像，阈值128-255）
                HOperatorSet.Threshold(inputImage.HImage, out HObject regions, 128, 255);
                
                // 步骤2：根据结构元素类型对区域执行膨胀算法
                HObject dilatedRegions = null;
                switch (ElementType)
                {
                    case StructuringElementType.Circle:
                        // 使用圆形结构元素对区域进行膨胀
                        HOperatorSet.DilationCircle(regions, out dilatedRegions, Radius);
                        break;
                        
                    case StructuringElementType.Rectangle:
                        // 使用矩形结构元素对区域进行膨胀
                        HOperatorSet.DilationRectangle1(regions, out dilatedRegions, Width, Height);
                        break;
                        
                    default:
                        regions?.Dispose();
                        throw new NotSupportedException($"不支持的结构元素类型: {ElementType}");
                }
                
                // 步骤3：将膨胀后的区域转换为二值图像
                HOperatorSet.RegionToBin(dilatedRegions, out outputImage, 255, 0, width.I, height.I);
                
                // 清理中间对象
                regions?.Dispose();
                dilatedRegions?.Dispose();
                
                // 检查输出图像是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("形态学膨胀处理失败，输出图像为空");
                
                // 验证输出图像是否有效
                try
                {
                    HOperatorSet.GetImageSize(outputImage, out HTuple outWidth, out HTuple outHeight);
                    if (outWidth.I <= 0 || outHeight.I <= 0)
                        throw new InvalidOperationException($"输出图像尺寸无效: {outWidth.I}×{outHeight.I}");
                }
                catch (HalconException halconEx)
                {
                    outputImage?.Dispose();
                    throw new InvalidOperationException($"形态学膨胀算子执行异常: {halconEx.Message}", halconEx);
                }
                
                // 如果需要反转输出
                if (InvertOutput)
                {
                    HOperatorSet.InvertImage(outputImage, out HObject invertedImage);
                    outputImage?.Dispose();
                    outputImage = invertedImage;
                }
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException($"Halcon形态学膨胀操作失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 创建测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="outputImage">输出图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        private Dictionary<string, object> CreateMeasurements(VisionImage inputImage, VisionImage outputImage, TimeSpan processingTime)
        {
            try
            {
                var measurements = new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["图像通道数"] = inputImage.Channels,
                    ["结构元素类型"] = ElementType.GetDisplayName(),
                    ["结构元素参数"] = GetStructuringElementInfo(),
                    ["反转输出"] = InvertOutput ? "是" : "否",
                    ["算法类型"] = "形态学膨胀",
                    ["算法特点"] = "扩张目标区域，填充小孔洞",
                    ["适用场景"] = "填充孔洞、连接断开部分、扩大目标",
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2)
                };
                
                // 尝试统计区域变化
                try
                {
                    var inputRegionCount = CountRegions(inputImage.HImage);
                    var outputRegionCount = CountRegions(outputImage.HImage);
                    measurements["处理前区域数"] = inputRegionCount;
                    measurements["处理后区域数"] = outputRegionCount;
                    measurements["区域数变化"] = outputRegionCount - inputRegionCount;
                }
                catch
                {
                    measurements["区域统计"] = "无法统计（非二值图像）";
                }
                
                return measurements;
            }
            catch (Exception ex)
            {
                // 如果统计失败，返回基础信息
                return new Dictionary<string, object>
                {
                    ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                    ["结构元素类型"] = ElementType.GetDisplayName(),
                    ["结构元素参数"] = GetStructuringElementInfo(),
                    ["算法类型"] = "形态学膨胀",
                    ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                    ["统计信息"] = $"统计失败: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// 获取结构元素信息
        /// </summary>
        /// <returns>结构元素描述字符串</returns>
        private string GetStructuringElementInfo()
        {
            return ElementType switch
            {
                StructuringElementType.Circle => $"圆形 r={Radius}",
                StructuringElementType.Rectangle => $"矩形 {Width}×{Height}",
                _ => ElementType.ToString()
            };
        }
        
        /// <summary>
        /// 统计区域数量
        /// </summary>
        /// <param name="image">图像</param>
        /// <returns>区域数量</returns>
        private int CountRegions(HObject image)
        {
            try
            {
                // 尝试将图像转换为区域并统计
                HOperatorSet.Threshold(image, out HObject regions, 128, 255);
                HOperatorSet.Connection(regions, out HObject connectedRegions);
                HOperatorSet.CountObj(connectedRegions, out HTuple count);
                regions?.Dispose();
                connectedRegions?.Dispose();
                return count.I;
            }
            catch
            {
                return 0;
            }
        }
        
        #endregion
    }
}