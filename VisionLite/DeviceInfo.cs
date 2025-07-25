using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionLite
{
    public enum CameraSdkType
    {
        Hikvision,
        HalconMVision
    }

    public class DeviceInfo
    {
        public string DisplayName { get; set; }
        public string UniqueID { get; set; } // 通常是序列号
        public CameraSdkType SdkType { get; set; }

        // 重写 ToString() 方法，以便在ComboBox中正确显示
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
