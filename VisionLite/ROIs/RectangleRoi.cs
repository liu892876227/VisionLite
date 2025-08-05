//ROIs\RectangleRoi.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HalconDotNet;

namespace VisionLite.ROIs
{
    public class RectangleRoi : RoiBase
    {
        public double Row1 { get; set; } // 左上角 Y
        public double Column1 { get; set; } // 左上角 X
        public double Row2 { get; set; } // 右下角 Y
        public double Column2 { get; set; } // 右下角 X

        public RectangleRoi()
        {
            Type = "Rectangle";
        }

        public override void Draw(HWindow window)
        {
            window.SetColor(Color.ToString().ToLower()); // Halcon接受 "red", "green" 等字符串
            window.DispRectangle1(Row1, Column1, Row2, Column2);
        }

        public HRegion GetRegion()
        {
            HRegion region = new HRegion();
            region.GenRectangle1(Row1, Column1, Row2, Column2);
            return region;
        }
    }
}
