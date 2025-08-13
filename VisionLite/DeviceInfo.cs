// DeviceInfo.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionLite
{
    /// <summary>
    /// 定义了支持的相机SDK类型。
    /// 使软件可以在一个统一的框架下处理不同厂商的相机。
    /// </summary>
    public enum CameraSdkType
    {
        Hikvision,
        HalconMVision
    }
    /// <summary>
    /// 一个数据模型类，用于封装被发现的相机设备的核心信息。
    /// 这个类的实例会被填充到UI控件（如ComboBox）中。
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// 在UI上显示的设备名称，通常包含品牌、型号和序列号等易于识别的信息。
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// 设备的唯一标识符，通常是硬件序列号。
        /// 用于在程序内部精确地识别和管理设备。
        /// </summary>
        public string UniqueID { get; set; } 
        /// <summary>
        /// 驱动此相机所使用的SDK类型。
        /// 程序将根据这个类型来决定实例化哪一个ICameraDevice的实现类。
        /// </summary>
        public CameraSdkType SdkType { get; set; }

        /// <summary>
        /// 重写ToString()方法，使得当DeviceInfo对象被添加到ComboBox等控件时，
        /// 能够自动显示我们期望的DisplayName，而不是默认的类名。
        /// </summary>
        /// <returns>设备的显示名称。</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
