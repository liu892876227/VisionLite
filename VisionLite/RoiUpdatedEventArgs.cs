// RoiUpdatedEventArgs.cs
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VisionLite
{
    /// <summary>
    /// 自定义的事件参数类，用于在 RoiUpdated 事件中封装和传递ROI的详细信息。
    /// 这个类不仅传递原始数据，还负责预处理数据（如格式化字符串、计算定位点），
    /// 使事件的消费者（MainWindow）可以直接使用，简化了主窗口的逻辑。
    /// </summary>
    public class RoiUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// 格式化好的、可直接显示的参数字符串。
        /// </summary>
        public string ParametersAsString { get; }

        /// <summary>
        /// 获取一个字典，包含所有原始的ROI参数键值对，用于可能的进一步计算。
        /// </summary>
        public Dictionary<string, double> Parameters { get; }

        /// <summary>
        /// 获取一个经过计算的、用于定位Adorner的“锚点”坐标。
        /// 这是在Halcon的图像坐标系下的坐标。如果无法计算，则为null。
        /// </summary>
        public Point? Position { get; }
        /// <summary>
        /// 构造函数，接收ROI的原始参数和几何形状，并进行处理和封装。
        /// </summary>
        /// <param name="parameters">从HDrawingObject获取的原始参数字典。</param>
        /// <param name="contour">代表ROI几何外形的Halcon对象（通常是HXLDCont）。</param>
        public RoiUpdatedEventArgs(Dictionary<string, double> parameters, HObject contour)
        {
            Parameters = parameters;
            // --- 格式化参数字符串 ---
            // 使用LINQ的Select来遍历字典中的每个键值对，
            // 并利用字符串格式化功能（,-15）来创建对齐的文本列。
            // 最后用换行符将它们连接成一个多行字符串。
            ParametersAsString = string.Join(Environment.NewLine,
                parameters.Select(kvp => $"{ParameterTranslator.Translate(kvp.Key),-5}{kvp.Value:F2}"));

            // --- 使用Halcon的几何运算来精确定位 ---
            if (contour != null && contour.IsInitialized() && contour.CountObj() > 0)
            {
                try
                {
                    // 为不同类型的ROI提供最优的定位逻辑
                    // 检查参数是否符合直线或Rectangle1的特征
                    if (parameters.ContainsKey("row1") && parameters.ContainsKey("column1") &&
                        parameters.ContainsKey("row2") && parameters.ContainsKey("column2") &&
                        contour.GetObjClass() == "xld_cont") // 确认是XLD轮廓（直线是其中一种）
                    {
                        // 对于直线或矩形，将定位点放在其右下角，这通常更符合直觉
                        Position = new Point(parameters["column2"], parameters["row2"]);
                    }
                    else
                    {
                        // 对于其他所有复杂或旋转的形状（如圆形、椭圆、Rectangle2），
                        // 使用SmallestRectangle1算子获取其最小外接水平矩形。
                        // 这是最通用的、能保证定位在视觉最右侧的方法。
                        HOperatorSet.SmallestRectangle1(contour, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);

                        // 定位点被设置为：最右侧的X坐标 (col2) 和 垂直方向的中心点
                        Position = new Point(col2.D, (row1.D + row2.D) / 2.0);
                    }
                }
                catch (HalconException)
                {
                    Position = null;  // 如果Halcon算子执行失败，则将Position设为null，Adorner将不会显示
                }
                finally
                {
                    // 事件参数中传递的contour是一个临时的拷贝，我们有责任在这里释放它以防止内存泄漏
                    contour.Dispose();
                }
            }
            else
            {
                // 如果没有有效的contour传入，则无法计算位置
                Position = null;
            }
        }
    }
}