// WindowRoiState.cs
using HalconDotNet;
using System;

namespace VisionLite
{
    /// <summary>
    /// 封装单个显示窗口的ROI状态信息。
    /// </summary>
    public class WindowRoiState : IDisposable
    {
        /// <summary>
        /// 窗口上显示的交互式ROI对象 (HDrawingObject)。
        /// </summary>
        public HDrawingObject DrawingObject { get; set; }

        /// <summary>
        /// 窗口上显示的掩膜/橡皮擦ROI对象 (Region)。
        /// </summary>
        public HObject PaintedRoi { get; set; }

        /// <summary>
        /// 窗口上浮动的参数框 Adorner。
        /// </summary>
        public RoiAdorner RoiAdorner { get; set; }

        /// <summary>
        /// 缓存的ROI更新事件参数。
        /// </summary>
        public RoiUpdatedEventArgs LastRoiArgs { get; set; }

        /// <summary>
        /// 释放所有托管的Halcon资源。
        /// </summary>
        public void Dispose()
        {
            DrawingObject?.Dispose();
            PaintedRoi?.Dispose();
            // RoiAdorner 是WPF控件，由UI框架管理，无需手动Dispose
            // LastRoiArgs 内部的HObject在创建时已是拷贝，并在其析构函数中处理
        }
    }
}