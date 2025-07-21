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
    /// 封装单个相机设备的所有信息和操作
    /// </summary>
    public class CameraDevice
    {
        public string DeviceID { get; private set; } // 相机的唯一ID
        public HTuple AcqHandle { get; private set; } // 相机的句柄
        public HObject Ho_Image { get; set; } // 用于存储该相机捕获的图像
        public HSmartWindowControlWPF DisplayWindow { get; private set; } // 绑定的显示窗口

        private bool isGrabbing = false; // 标记相机是否已开始异步采集

        /// <summary>
        /// 构造函数
        /// </summary>
        public CameraDevice(string deviceId, HSmartWindowControlWPF window)
        {
            this.DeviceID = deviceId;
            this.DisplayWindow = window;

            // 为了解决C#对out参数的限制，使用临时变量进行传递
            HObject tempImage;
            HOperatorSet.GenEmptyObj(out tempImage);
            this.Ho_Image = tempImage;
        }

        /// <summary>
        /// 打开并配置相机
        /// </summary>
        public bool Open()
        {
            try
            {
                HOperatorSet.OpenFramegrabber("MVision", 1, 1, 0, 0, 0, 0, "progressive", 8, "default", -1, "false", "auto", DeviceID, 0, -1, out HTuple handle);
                this.AcqHandle = handle;

                // 设置软触发模式
                HOperatorSet.SetFramegrabberParam(AcqHandle, "TriggerMode", "On");
                HOperatorSet.SetFramegrabberParam(AcqHandle, "TriggerSource", "Software");

                // 开始异步采集
                HOperatorSet.GrabImageStart(AcqHandle, -1);
                isGrabbing = true;

                return true; // 打开成功
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"打开设备 {DeviceID} 失败: \n{ex.GetErrorMessage()}", "错误");
                return false;
            }
        }

        /// <summary>
        /// 单次触发并显示图像
        /// </summary>
        public void GrabAndDisplay()
        {
            if (AcqHandle == null || !isGrabbing) return;

            try
            {
                Ho_Image?.Dispose();
                HOperatorSet.SetFramegrabberParam(AcqHandle, "TriggerSoftware", "do_it");

                HObject tempImage;
                HOperatorSet.GrabImageAsync(out tempImage, AcqHandle, 5000);
                this.Ho_Image = tempImage;

                // 在自己的窗口中显示
                Display();
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"从设备 {DeviceID} 采集图像失败: \n{ex.GetErrorMessage()}", "错误");
            }
        }

        /// <summary>
        /// 在绑定的窗口中显示图像
        /// </summary>
        private void Display()
        {
            if (Ho_Image == null || !Ho_Image.IsInitialized()) return;

            HWindow window = DisplayWindow.HalconWindow;
            HOperatorSet.GetImageSize(Ho_Image, out HTuple width, out HTuple height);
            window.SetPart(0, 0, height.I - 1, width.I - 1);
            window.ClearWindow();
            window.DispObj(Ho_Image);
        }

        /// <summary>
        /// 关闭相机并释放资源
        /// </summary>
        public void Close()
        {
            if (AcqHandle != null)
            {
                try
                {
                    // 如果正在采集，需要先停止
                    HOperatorSet.CloseFramegrabber(AcqHandle);
                }
                catch (HalconException) { /* 忽略关闭错误 */ }

                // 重置状态
                AcqHandle = null;
                isGrabbing = false;
            }
            // 无论相机是否打开，都尝试释放图像对象占用的内存
            Ho_Image?.Dispose();
        }
    }
}

