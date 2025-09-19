using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using VisionLite.Vision.Core.Attributes;
using VisionLite.Vision.Core.Base;
using VisionLite.Vision.Core.Models;

namespace VisionLite.Vision.Processors.Preprocessing.EdgeProcessors
{
    /// <summary>
    /// Sobel边缘检测处理器
    /// 使用Halcon的SobelAmp算子实现Sobel边缘检测算法
    /// </summary>
    public class SobelEdgeDetector : VisionProcessorBase
    {
        #region 属性

        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "Sobel边缘检测";

        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "边缘检测";

        /// <summary>
        /// 滤波器大小
        /// </summary>
        [Parameter("滤波器大小", "Sobel算子尺寸，必须为奇数", 3, 7, Step = 2, Group = "算子参数", Order = 10)]
        public int FilterSize { get; set; } = 3;

        /// <summary>
        /// 输出方向
        /// </summary>
        [Parameter("输出方向", "Sobel算子输出方向类型", Group = "算子参数", Order = 11)]
        public SobelDirection Direction { get; set; } = SobelDirection.Amplitude;

        /// <summary>
        /// 边缘阈值
        /// </summary>
        [Parameter("边缘阈值", "边缘幅值阈值，用于二值化", 1, 255, Group = "阈值参数", Order = 20)]
        public double Threshold { get; set; } = 20;

        /// <summary>
        /// 边缘极性
        /// </summary>
        [Parameter("边缘极性", "检测的边缘极性类型", Group = "检测参数", Order = 30)]
        public EdgePolarity Polarity { get; set; } = EdgePolarity.All;

        /// <summary>
        /// 边缘连接
        /// </summary>
        [Parameter("边缘连接", "边缘连接模式", Group = "后处理参数", Order = 35)]
        public EdgeConnection Connection { get; set; } = EdgeConnection.Connected8;

        /// <summary>
        /// 结果类型
        /// </summary>
        [Parameter("输出类型", "边缘检测结果输出类型", Group = "输出设置", Order = 40)]
        public EdgeResultType ResultType { get; set; } = EdgeResultType.BinaryEdges;

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

                // 执行Sobel边缘检测（暂时返回原图像，后续实现具体算法）
                var outputImage = await Task.Run(() => ExecuteSobelDetection(inputImage));

                // 计算处理时间
                var processingTime = DateTime.Now - startTime;

                // 创建测量结果
                var measurements = CreateMeasurements(inputImage, processingTime);

                // 返回成功结果
                return CreateSuccessResult(outputImage, processingTime, measurements);
            }
            catch (Exception ex)
            {
                // 返回失败结果
                return CreateFailureResult($"Sobel边缘检测处理失败: {ex.Message}", ex);
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

            // 确保滤波器大小为有效的奇数值
            FilterSize = AdjustFilterSize(FilterSize);
        }

        /// <summary>
        /// 调整滤波器大小为有效奇数值
        /// </summary>
        /// <param name="size">原始大小</param>
        /// <returns>调整后的有效大小</returns>
        private int AdjustFilterSize(int size)
        {
            // Halcon的Sobel算子支持3, 5, 7等奇数值
            if (size <= 3) return 3;
            if (size <= 5) return 5;
            return 7;
        }

        /// <summary>
        /// 执行Sobel边缘检测算法
        /// 使用Halcon的SobelAmp算子实现Sobel边缘检测
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>边缘检测后的图像</returns>
        private VisionImage ExecuteSobelDetection(VisionImage inputImage)
        {
            HObject gradientImage = null;
            HObject finalImage = null;

            try
            {
                // 检查输入图像是否有效
                if (inputImage?.HImage == null || !inputImage.HImage.IsInitialized())
                    throw new ArgumentNullException(nameof(inputImage), "输入图像对象为空或无效");

                // 根据方向类型执行不同的Sobel算法
                switch (Direction)
                {
                    case SobelDirection.Amplitude:
                        // Sobel梯度幅值检测
                        HOperatorSet.SobelAmp(inputImage.HImage, out gradientImage, "sum_abs", FilterSize);
                        System.Diagnostics.Debug.WriteLine($"Sobel幅值检测完成 - 滤波器大小:{FilterSize}");
                        break;

                    case SobelDirection.X:
                        // X方向梯度
                        HOperatorSet.SobelAmp(inputImage.HImage, out gradientImage, "x", FilterSize);
                        System.Diagnostics.Debug.WriteLine($"Sobel X方向梯度检测完成 - 滤波器大小:{FilterSize}");
                        break;

                    case SobelDirection.Y:
                        // Y方向梯度
                        HOperatorSet.SobelAmp(inputImage.HImage, out gradientImage, "y", FilterSize);
                        System.Diagnostics.Debug.WriteLine($"Sobel Y方向梯度检测完成 - 滤波器大小:{FilterSize}");
                        break;

                    default:
                        throw new ArgumentException($"不支持的Sobel方向: {Direction}");
                }

                // 获取输入图像的尺寸
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);

                // 如果结果类型是梯度幅值，直接返回原始梯度图像
                if (ResultType == EdgeResultType.GradientMagnitude)
                {
                    // 根据极性处理原始梯度图像
                    switch (Polarity)
                    {
                        case EdgePolarity.All:
                            if (Direction != SobelDirection.Amplitude)
                            {
                                HOperatorSet.AbsImage(gradientImage, out finalImage);
                            }
                            else
                            {
                                finalImage = gradientImage;
                                gradientImage = null; // 所有权转移
                            }
                            break;
                        case EdgePolarity.Positive:
                        case EdgePolarity.Negative:
                        default:
                            finalImage = gradientImage;
                            gradientImage = null; // 所有权转移
                            break;
                    }
                    System.Diagnostics.Debug.WriteLine("返回原始梯度图像");
                }
                else
                {
                    // 对于二值边缘和轮廓，需要进行阈值处理
                    HObject edgeRegion = null;

                    // 根据极性进行阈值处理得到区域
                    switch (Polarity)
                    {
                        case EdgePolarity.Positive:
                            HOperatorSet.Threshold(gradientImage, out edgeRegion, Threshold, 255);
                            System.Diagnostics.Debug.WriteLine($"应用正边缘阈值: {Threshold}");
                            break;

                        case EdgePolarity.Negative:
                            if (Direction == SobelDirection.Amplitude)
                            {
                                // 幅值图像没有负值，直接应用阈值
                                HOperatorSet.Threshold(gradientImage, out edgeRegion, Threshold, 255);
                            }
                            else
                            {
                                // 对于梯度图像，处理负值
                                HOperatorSet.Threshold(gradientImage, out edgeRegion, -255, -Threshold);
                            }
                            System.Diagnostics.Debug.WriteLine($"应用负边缘阈值: {Threshold}");
                            break;

                        case EdgePolarity.All:
                            if (Direction == SobelDirection.Amplitude)
                            {
                                // 幅值图像直接应用阈值
                                HOperatorSet.Threshold(gradientImage, out edgeRegion, Threshold, 255);
                            }
                            else
                            {
                                // 其他情况取绝对值后应用阈值
                                HOperatorSet.AbsImage(gradientImage, out HObject absImage);
                                HOperatorSet.Threshold(absImage, out edgeRegion, Threshold, 255);
                                absImage?.Dispose();
                            }
                            System.Diagnostics.Debug.WriteLine($"应用全边缘阈值: {Threshold}");
                            break;

                        default:
                            HOperatorSet.Threshold(gradientImage, out edgeRegion, Threshold, 255);
                            break;
                    }

                    // 根据连接模式进行后处理
                    if (Connection == EdgeConnection.Connected8 || Connection == EdgeConnection.Connected4)
                    {
                        HOperatorSet.Connection(edgeRegion, out HObject connectedRegion);
                        edgeRegion?.Dispose();
                        edgeRegion = connectedRegion;
                        System.Diagnostics.Debug.WriteLine($"应用{(Connection == EdgeConnection.Connected8 ? "8" : "4")}连通域连接");
                    }

                    // 根据结果类型生成最终输出
                    switch (ResultType)
                    {
                        case EdgeResultType.BinaryEdges:
                            // 将区域转换为二值图像
                            HOperatorSet.RegionToBin(edgeRegion, out finalImage, 255, 0, width, height);
                            break;

                        case EdgeResultType.EdgeContours:
                            // 将区域转换为XLD轮廓，然后绘制到图像上
                            HOperatorSet.GenContourRegionXld(edgeRegion, out HObject contours, "border");
                            HOperatorSet.GenImageConst(out HObject blankImage, "byte", width, height);
                            HOperatorSet.PaintXld(contours, blankImage, out finalImage, 255.0);
                            // 清理中间资源
                            contours?.Dispose();
                            blankImage?.Dispose();
                            break;

                        default:
                            HOperatorSet.RegionToBin(edgeRegion, out finalImage, 255, 0, width, height);
                            break;
                    }

                    // 清理区域对象
                    edgeRegion?.Dispose();
                }

                // 检查输出图像是否有效
                if (finalImage == null || !finalImage.IsInitialized())
                    throw new InvalidOperationException("Sobel边缘检测处理失败，最终输出图像为空");

                return new VisionImage(finalImage);
            }
            catch (Exception ex)
            {
                // 清理资源
                finalImage?.Dispose();
                throw new InvalidOperationException($"Halcon Sobel边缘检测操作失败: {ex.Message}", ex);
            }
            finally
            {
                // 确保在任何情况下都释放所有创建的Halcon对象
                gradientImage?.Dispose();
            }
        }

        /// <summary>
        /// 创建测量结果
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="processingTime">处理时间</param>
        /// <returns>测量结果字典</returns>
        private Dictionary<string, object> CreateMeasurements(VisionImage inputImage, TimeSpan processingTime)
        {
            return new Dictionary<string, object>
            {
                ["原始图像尺寸"] = $"{inputImage.Width} × {inputImage.Height}",
                ["图像通道数"] = inputImage.Channels,
                ["滤波器大小"] = FilterSize,
                ["输出方向"] = GetDirectionDisplayName(Direction),
                ["边缘阈值"] = Threshold,
                ["边缘极性"] = GetPolarityDisplayName(Polarity),
                ["输出类型"] = GetResultTypeDisplayName(ResultType),
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = "Sobel边缘检测",
                ["Halcon算子"] = "SobelAmp",
                ["算法特点"] = "基于一阶导数的边缘检测，计算效率高"
            };
        }

        /// <summary>
        /// 获取方向的显示名称
        /// </summary>
        private string GetDirectionDisplayName(SobelDirection direction)
        {
            return direction switch
            {
                SobelDirection.X => "X方向梯度",
                SobelDirection.Y => "Y方向梯度",
                SobelDirection.Amplitude => "梯度幅值",
                _ => direction.ToString()
            };
        }

        /// <summary>
        /// 获取极性的显示名称
        /// </summary>
        private string GetPolarityDisplayName(EdgePolarity polarity)
        {
            return polarity switch
            {
                EdgePolarity.Positive => "正边缘",
                EdgePolarity.Negative => "负边缘",
                EdgePolarity.All => "所有边缘",
                _ => polarity.ToString()
            };
        }

        /// <summary>
        /// 获取结果类型的显示名称
        /// </summary>
        private string GetResultTypeDisplayName(EdgeResultType resultType)
        {
            return resultType switch
            {
                EdgeResultType.BinaryEdges => "二值边缘图",
                EdgeResultType.EdgeContours => "边缘轮廓",
                EdgeResultType.GradientMagnitude => "梯度幅值图",
                _ => resultType.ToString()
            };
        }

        #endregion
    }
}