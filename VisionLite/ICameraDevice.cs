using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using System.Windows;

namespace VisionLite
{
    /// <summary>
    /// 定义所有相机设备必须实现的通用接口
    /// </summary>
    public interface ICameraDevice
    {
        string DeviceID { get; }
        HSmartWindowControlWPF DisplayWindow { get; }

        bool Open();
        void Close();
        void GrabAndDisplay();
        void StartContinuousGrab();
        void StopContinuousGrab();
        
        bool IsContinuousGrabbing();
        bool SetParameter(string paramName, object value);
        Window ShowParametersWindow(Window owner);
    }
}
