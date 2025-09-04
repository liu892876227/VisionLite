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
    /// 形态学开运算处理器
    /// 使用Halcon OpeningCircle和OpeningRectangle1算子实现形态学开运算操作
    /// 开运算 = 先腐蚀后膨胀，用于去除小噪点、平滑边界
    /// </summary>
    public class OpeningProcessor : VisionProcessorBase
    {
        #region 属性
        
        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "形态学开运算";
        
        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "图像预处理";
        
        /// <summary>
        /// 结构元素类型
        /// </summary>
        [Parameter("结构元素类型", "选择圆形或矩形结构元素，影响开运算的形状特征", Group = "参数设置")]
        public StructuringElementType ElementType { get; set; } = StructuringElementType.Circle;
        
        /// <summary>
        /// 半径
        /// </summary>
        [Parameter("半径", "圆形结构元素的半径，值越大去噪效果越强但细节损失越多", 0.5, 50.0, DecimalPlaces = 1, Group = "参数设置")]
        public double Radius { get; set; } = 3.5;
        
        /// <summary>
        /// 宽度
        /// </summary>
        [Parameter("宽度", "矩形结构元素的宽度，必须为奇数", 1, 201, DecimalPlaces = 0, Group = "参数设置")]
        public int Width { get; set; } = 11;
        
        /// <summary>
        /// 高度
        /// </summary>
        [Parameter("高度", "矩形结构元素的高度，必须为奇数", 1, 201, DecimalPlaces = 0, Group = "参数设置")]
        public int Height { get; set; } = 11;
        
        /// <summary>
        /// 反转输出
        /// </summary>
        [Parameter("反转输出", "是否反转开运算结果", Group = "其他设置")]
        public bool InvertOutput { get; set; } = false;
        
        #endregion
        
        #region 私有字段
        
        /// <summary>
        /// 当前使用的算法路径（用于测量结果显示）
        /// </summary>
        private string _usedAlgorithm = "";
        
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
                
                // 执行自适应形态学开运算
                var outputImage = await Task.Run(() => ExecuteAdaptiveOpening(inputImage));
                
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
                return CreateFailureResult($"形态学开运算处理失败: {ex.Message}", ex);
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
        /// 执行自适应形态学开运算算法
        /// 自动检测输入类型（区域或图像）并选择合适的Halcon算子
        /// </summary>
        /// <param name="inputImage">输入图像或区域</param>
        /// <returns>开运算处理后的结果</returns>
        private VisionImage ExecuteAdaptiveOpening(VisionImage inputImage)
        {
            HObject outputImage = null;
            string usedAlgorithm = "";
            
            try
            {
                // 检查输入对象是否有效
                if (inputImage.HImage == null)
                    throw new ArgumentNullException("输入对象为空");
                
                // 使用GetObjClass检测输入对象类型
                HOperatorSet.GetObjClass(inputImage.HImage, out HTuple objectClass);
                string objType = objectClass.S;
                
                if (objType == "region")
                {
                    // 处理区域输入：使用区域形态学算子
                    usedAlgorithm = ProcessRegionInput(inputImage.HImage, out outputImage);
                }
                else if (objType == "image")
                {
                    // 处理图像输入：使用灰度形态学算子
                    usedAlgorithm = ProcessImageInput(inputImage.HImage, out outputImage);
                }
                else
                {
                    throw new NotSupportedException($"不支持的HObject类型: {objType}");
                }
                
                // 检查输出是否有效
                if (outputImage == null)
                    throw new InvalidOperationException("自适应形态学开运算处理失败，输出对象为空");
                
                // 验证输出对象
                ValidateOutput(outputImage);
                
                // 如果需要反转输出
                if (InvertOutput)
                {
                    ApplyInversion(ref outputImage);
                }
                
                // 保存使用的算法信息供测量结果使用
                _usedAlgorithm = usedAlgorithm;
                
                return new VisionImage(outputImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                outputImage?.Dispose();
                throw new InvalidOperationException($"自适应形态学开运算操作失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 处理区域输入，使用区域形态学算子
        /// </summary>
        /// <param name="regions">输入区域</param>
        /// <param name="outputImage">输出图像</param>
        /// <returns>使用的算法描述</returns>
        private string ProcessRegionInput(HObject regions, out HObject outputImage)
        {
            HObject openedRegions = null;
            string algorithm;
            
            try
            {
                // 根据结构元素类型执行区域开运算
                switch (ElementType)
                {
                    case StructuringElementType.Circle:
                        HOperatorSet.OpeningCircle(regions, out openedRegions, Radius);
                        algorithm = $"区域开运算-圆形(r={Radius})";
                        break;
                        
                    case StructuringElementType.Rectangle:
                        HOperatorSet.OpeningRectangle1(regions, out openedRegions, Width, Height);
                        algorithm = $"区域开运算-矩形({Width}×{Height})";
                        break;
                        
                    default:
                        throw new NotSupportedException($"不支持的结构元素类型: {ElementType}");
                }
                
                // 获取输出图像尺寸（从输入区域推断）
                HOperatorSet.SmallestRectangle1(regions, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                int width = Math.Max(512, col2.I - col1.I + 50);
                int height = Math.Max(512, row2.I - row1.I + 50);
                
                // 将开运算后的区域转换为二值图像
                HOperatorSet.RegionToBin(openedRegions, out outputImage, 255, 0, width, height);
                
                openedRegions?.Dispose();
                return algorithm;
            }
            catch
            {
                openedRegions?.Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// 处理图像输入，使用灰度形态学算子
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="outputImage">输出图像</param>
        /// <returns>使用的算法描述</returns>
        private string ProcessImageInput(HObject image, out HObject outputImage)
        {
            string algorithm;
            
            // 根据结构元素类型执行灰度开运算
            switch (ElementType)
            {
                case StructuringElementType.Circle:
                    // 灰度形态学算子要求圆形半径≥1.0
                    double adjustedRadius = Math.Max(1.0, Radius);
                    HOperatorSet.GrayOpeningShape(image, out outputImage, adjustedRadius, adjustedRadius, "octagon");
                    algorithm = adjustedRadius != Radius ? 
                        $"灰度开运算-圆形(r={adjustedRadius}，从{Radius}自动调整)" : 
                        $"灰度开运算-圆形(r={adjustedRadius})";
                    break;
                    
                case StructuringElementType.Rectangle:
                    // 矩形结构元素确保为奇数且≥1
                    int adjustedWidth = Math.Max(1, Width);
                    int adjustedHeight = Math.Max(1, Height);
                    if (adjustedWidth % 2 == 0) adjustedWidth++;
                    if (adjustedHeight % 2 == 0) adjustedHeight++;
                    
                    HOperatorSet.GrayOpeningRect(image, out outputImage, adjustedWidth, adjustedHeight);
                    algorithm = (adjustedWidth != Width || adjustedHeight != Height) ?
                        $"灰度开运算-矩形({adjustedWidth}×{adjustedHeight}，从{Width}×{Height}自动调整)" :
                        $"灰度开运算-矩形({adjustedWidth}×{adjustedHeight})";
                    break;
                    
                default:
                    throw new NotSupportedException($"不支持的结构元素类型: {ElementType}");
            }
            
            return algorithm;
        }
        
        /// <summary>
        /// 验证输出对象有效性
        /// </summary>
        /// <param name="output">输出对象</param>
        private void ValidateOutput(HObject output)
        {
            try
            {
                HOperatorSet.GetObjClass(output, out HTuple objClass);
                string type = objClass.S;
                
                if (type == "image")
                {
                    HOperatorSet.GetImageSize(output, out HTuple width, out HTuple height);
                    if (width.I <= 0 || height.I <= 0)
                        throw new InvalidOperationException($"输出图像尺寸无效: {width.I}×{height.I}");
                }
                else if (type == "region")
                {
                    HOperatorSet.CountObj(output, out HTuple count);
                    // 区域数量为0也是有效的（空区域）
                }
                else
                {
                    throw new InvalidOperationException($"不支持的输出对象类型: {type}");
                }
            }
            catch (HalconException halconEx)
            {
                throw new InvalidOperationException($"形态学开运算算子执行异常: {halconEx.Message}", halconEx);
            }
        }
        
        /// <summary>
        /// 应用输出反转
        /// </summary>
        /// <param name="outputImage">要反转的图像</param>
        private void ApplyInversion(ref HObject outputImage)
        {
            try
            {
                HOperatorSet.GetObjClass(outputImage, out HTuple objClass);
                string type = objClass.S;
                
                if (type == "image")
                {
                    HOperatorSet.InvertImage(outputImage, out HObject invertedImage);
                    outputImage?.Dispose();
                    outputImage = invertedImage;
                }
                else if (type == "region")
                {
                    // 对于区域，需要先转换为图像，反转后再转回区域
                    HOperatorSet.SmallestRectangle1(outputImage, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                    int width = Math.Max(512, col2.I - col1.I + 50);
                    int height = Math.Max(512, row2.I - row1.I + 50);
                    
                    HOperatorSet.RegionToBin(outputImage, out HObject tempImage, 255, 0, width, height);
                    HOperatorSet.InvertImage(tempImage, out HObject invertedImage);
                    HOperatorSet.Threshold(invertedImage, out HObject invertedRegion, 128, 255);
                    
                    outputImage?.Dispose();
                    tempImage?.Dispose();
                    invertedImage?.Dispose();
                    outputImage = invertedRegion;
                }
            }
            catch (HalconException halconEx)
            {
                throw new InvalidOperationException($"输出反转失败: {halconEx.Message}", halconEx);
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
                    ["算法类型"] = "自适应形态学开运算",
                    ["使用的算法"] = string.IsNullOrEmpty(_usedAlgorithm) ? "未知" : _usedAlgorithm,
                    ["算法特点"] = "自动选择区域或灰度开运算算子",
                    ["适用场景"] = "去除小噪点、平滑边界、分离粘连目标",
                    ["运算顺序"] = "腐蚀 → 膨胀",
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
                    ["算法类型"] = "自适应形态学开运算",
                    ["使用的算法"] = string.IsNullOrEmpty(_usedAlgorithm) ? "未知" : _usedAlgorithm,
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