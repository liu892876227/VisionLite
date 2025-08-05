//ROIs\RoiBase.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HalconDotNet;

namespace VisionLite.ROIs
{
    public abstract class RoiBase
    {
        public int Id { get; set; } // 唯一ID
        public string Type { get; protected set; }
        public Color Color { get; set; } = Colors.LimeGreen;

        // 核心方法：每个ROI子类必须实现如何将自己绘制到Halcon窗口上
        public abstract void Draw(HWindow window);
    }
}
