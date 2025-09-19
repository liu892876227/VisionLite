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
    /// Canny边缘检测处理器
    /// 使用Halcon的EdgesImage算子实现Canny边缘检测算法
    /// </summary>
    public class CannyEdgeDetector : VisionProcessorBase
    {
        #region 属性

        /// <summary>
        /// 处理器名称
        /// </summary>
        public override string ProcessorName => "Canny边缘检测";

        /// <summary>
        /// 处理器分类
        /// </summary>
        public override string Category => "边缘检测";

        /// <summary>
        /// 低阈值
        /// </summary>
        [Parameter("低阈值", "边缘检测低阈值，用于弱边缘检测", 1, 100, Group = "阈值参数", Order = 10)]
        public double LowThreshold { get; set; } = 10;

        /// <summary>
        /// 高阈值
        /// </summary>
        [Parameter("高阈值", "边缘检测高阈值，用于强边缘检测", 1, 255, Group = "阈值参数", Order = 11)]
        public double HighThreshold { get; set; } = 50;

        /// <summary>
        /// 高斯标准差
        /// </summary>
        [Parameter("高斯标准差", "高斯滤波标准差，控制边缘检测的敏感度", 0.5, 10.0, Group = "预处理参数", Order = 20)]
        public double Alpha { get; set; } = 1.0;

        /// <summary>
        /// 边缘连接
        /// </summary>
        [Parameter("边缘连接", "边缘连接模式", Group = "后处理参数", Order = 30)]
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

                // 执行Canny边缘检测（暂时返回原图像，后续实现具体算法）
                var outputImage = await Task.Run(() => ExecuteCannyDetection(inputImage));

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
                return CreateFailureResult($"Canny边缘检测处理失败: {ex.Message}", ex);
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

            // 验证阈值关系
            if (LowThreshold >= HighThreshold)
                throw new ArgumentException("低阈值必须小于高阈值");
        }

        /// <summary>
        /// 执行Canny边缘检测算法
        /// 使用Halcon的EdgesImage算子实现Canny边缘检测
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>边缘检测后的图像</returns>
        private VisionImage ExecuteCannyDetection(VisionImage inputImage)
        {
            HObject edgeAmplitudeImage = null;
            HObject edgeDir = null;
            HObject finalImage = null;

            try
            {
                // 检查输入图像是否有效
                if (inputImage?.HImage == null || !inputImage.HImage.IsInitialized())
                    throw new ArgumentNullException(nameof(inputImage), "输入图像对象为空或无效");

                // 执行Canny边缘检测
                
                HOperatorSet.EdgesImage(
                    inputImage.HImage,
                    out edgeAmplitudeImage, // 输出：边缘幅度图像 (Image)
                    out edgeDir,            // 输出：边缘方向图像 (Image)
                    "canny",
                    Alpha,
                    "nms",
                    LowThreshold,
                    HighThreshold);

                System.Diagnostics.Debug.WriteLine($"Canny边缘检测完成 - Alpha:{Alpha}, 低阈值:{LowThreshold}, 高阈值:{HighThreshold}");

                // 获取输入图像的尺寸用于创建输出图像
                HOperatorSet.GetImageSize(inputImage.HImage, out HTuple width, out HTuple height);

                // edgeAmplitudeImage 已经是结果了，非边缘点为0，边缘点为梯度值
                // 如果需要一个纯粹的 0 和 255 的二值图像，需要进行阈值处理
                HOperatorSet.Threshold(edgeAmplitudeImage, out HObject edgeRegion, 1, 255);

                // 根据连接模式进行后处理
                if (Connection == EdgeConnection.Connected8 || Connection == EdgeConnection.Connected4)
                {
                    HOperatorSet.Connection(edgeRegion, out HObject connectedRegion);
                    edgeRegion.Dispose(); // 释放旧区域
                    edgeRegion = connectedRegion; // 更新为连接后的区域
                }

                // 根据结果类型生成最终的输出图像
                switch (ResultType)
                {
                    case EdgeResultType.BinaryEdges:
                        // 将最终的区域转换为二值图像
                        HOperatorSet.RegionToBin(edgeRegion, out finalImage, 255, 0, width, height);
                        break;

                    case EdgeResultType.EdgeContours:
                        // 首先，将区域转换为XLD轮廓
                        HOperatorSet.GenContourRegionXld(edgeRegion, out HObject contours, "border");
                        // 然后，将XLD轮廓绘制到一张新的空白图像上
                        HOperatorSet.GenImageConst(out HObject blankImage, "byte", width, height);
                        HOperatorSet.PaintXld(contours, blankImage, out finalImage, 255.0);
                        // 清理中间资源
                        contours.Dispose();
                        blankImage.Dispose();
                        break;

                    case EdgeResultType.GradientMagnitude:
                        // 对于Canny，返回原始的边缘幅度图像
                        finalImage = edgeAmplitudeImage;
                        edgeAmplitudeImage = null; // 所有权已转移，防止被重复释放
                        break;

                    default:
                        // 默认返回二值图像
                        HOperatorSet.RegionToBin(edgeRegion, out finalImage, 255, 0, width, height);
                        break;
                }

                // 检查输出图像是否有效
                if (finalImage == null || !finalImage.IsInitialized())
                    throw new InvalidOperationException("Canny边缘检测处理失败，最终输出图像为空");

                return new VisionImage(finalImage);
            }
            catch (Exception ex)
            {
                // 重新抛出包装后的异常
                throw new InvalidOperationException($"Halcon Canny边缘检测操作失败: {ex.Message}", ex);
            }
            finally
            {
                // 确保在任何情况下都释放所有创建的Halcon对象
                edgeAmplitudeImage?.Dispose();
                edgeDir?.Dispose();
                
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
                ["低阈值"] = LowThreshold,
                ["高阈值"] = HighThreshold,
                ["高斯标准差"] = Alpha,
                ["边缘连接"] = GetConnectionDisplayName(Connection),
                ["输出类型"] = GetResultTypeDisplayName(ResultType),
                ["处理时间(ms)"] = Math.Round(processingTime.TotalMilliseconds, 2),
                ["算法类型"] = "Canny边缘检测",
                ["Halcon算子"] = "EdgesImage",
                ["算法特点"] = "双阈值边缘检测，具有良好的边缘定位精度"
            };
        }

        /// <summary>
        /// 获取连接模式的显示名称
        /// </summary>
        private string GetConnectionDisplayName(EdgeConnection connection)
        {
            return connection switch
            {
                EdgeConnection.Connected4 => "4连通",
                EdgeConnection.Connected8 => "8连通",
                _ => connection.ToString()
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