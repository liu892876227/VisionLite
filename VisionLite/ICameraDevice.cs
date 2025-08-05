
//ICameraDevice.cs
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
    /// 定义所有相机设备必须实现的通用接口。
    /// 这个接口是硬件抽象层的核心，它使得主窗口(MainWindow)可以以统一的方式
    /// 与不同品牌、不同SDK的相机进行交互，而无需关心其内部实现细节。
    /// </summary>
    public interface ICameraDevice
    {
        /// <summary>
        /// 获取设备的唯一标识符（通常是序列号）。
        /// </summary>
        string DeviceID { get; }

        /// <summary>
        /// 获取与此相机设备绑定的WPF显示控件。
        /// </summary>
        HSmartWindowControlWPF DisplayWindow { get; }

        /// <summary>
        /// 打开与相机的连接。
        /// </summary>
        /// <returns>如果成功打开则返回true，否则返回false。</returns>
        bool Open();

        /// <summary>
        /// 关闭与相机的连接并释放所有相关资源。
        /// </summary>
        void Close();

        /// <summary>
        /// 执行一次单次触发采集，并将结果显示在绑定的窗口中。
        /// </summary>
        void GrabAndDisplay();

        /// <summary>
        /// 开始自由运行的连续采集模式。
        /// </summary>
        void StartContinuousGrab();

        /// <summary>
        /// 停止连续采集。
        /// </summary>
        void StopContinuousGrab();

        /// <summary>
        /// 检查相机当前是否处于连续采集模式。
        /// </summary>
        /// <returns>如果是则返回true，否则返回false。</returns>
        bool IsContinuousGrabbing();

        /// <summary>
        /// 安全地设置相机参数。
        /// 实现类内部必须处理好因相机状态（如采集中/停止）不同而导致的参数设置限制。
        /// </summary>
        /// <param name="paramName">要设置的参数的名称（SDK或GenICam标准名称）。</param>
        /// <param name="value">要设置的值。</param>
        /// <returns>如果设置成功则返回true，否则返回false。</returns>
        bool SetParameter(string paramName, object value);

        /// <summary>
        /// 创建并返回一个与此相机类型对应的参数设置窗口。
        /// </summary>
        /// <param name="owner">该参数窗口的父窗口。</param>
        /// <returns>一个Window派生类的实例。</returns>
        Window ShowParametersWindow(Window owner);

        /// <summary>
        /// 当设备成功采集到一帧新图像时触发。
        /// 订阅者会收到一个 HObject 类型的参数，即采集到的新图像。
        /// </summary>
        event Action<ICameraDevice, HObject> ImageAcquired;
    }
}
