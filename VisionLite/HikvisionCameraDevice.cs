// HikvisionCameraDevice.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using MvCamCtrl.NET;

namespace VisionLite
{
    /// <summary>
    /// ICameraDevice接口的具体实现，使用海康MVS SDK来控制相机。
    /// </summary>
    public class HikvisionCameraDevice : ICameraDevice
    {
        /// <summary>
        /// 公共属性，暴露底层的海康SDK对象，以便参数窗口可以查询参数列表。
        /// </summary>
        public MyCamera CameraSdkObject { get; private set; }

        // --- 接口属性实现 ---
        public string DeviceID { get; private set; }
        public HSmartWindowControlWPF DisplayWindow { get; private set; }

        // --- 私有成员变量 ---
        private MyCamera.MV_CC_DEVICE_INFO m_deviceInfo;
        private HObject m_Ho_Image;
        private volatile bool m_bGrabbing = false;                              // 标记采集流是否已启动
        private MyCamera.cbOutputdelegate ImageCallback;                        // 图像回调委托

        // 定义了哪些参数在设置前需要先停止采集
        private readonly List<string> criticalParameters = new List<string>
        {
            "Width", "Height", "OffsetX", "OffsetY", "PixelFormat"
        };

        public HikvisionCameraDevice(DeviceInfo info, HSmartWindowControlWPF window)
        {
            this.DeviceID = info.UniqueID;
            this.DisplayWindow = window;
            CameraSdkObject = new MyCamera();
            m_deviceInfo = new MyCamera.MV_CC_DEVICE_INFO();

            // 将 ProcessImageCallback 方法绑定到回调委托上
            ImageCallback = new MyCamera.cbOutputdelegate(ProcessImageCallback);
            HOperatorSet.GenEmptyObj(out m_Ho_Image);
        }

        /// <summary>
        /// 实现接口的Open方法，完成设备连接和初始化。
        /// </summary>
        public bool Open()
        {
            try
            {
                var deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
                int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref deviceList);
                if (nRet != 0)
                {
                    MessageBox.Show($"[HIK] 枚举设备失败！错误码: 0x{nRet:X}", "打开失败");
                    return false;
                }
                // 根据唯一的序列号(DeviceID)在列表中查找设备
                bool deviceFound = false;
                for (int i = 0; i < deviceList.nDeviceNum; i++)
                {
                    var stDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(deviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                    string currentSerial = GetSerialNumber(stDevInfo);
                    if (currentSerial == this.DeviceID)
                    {
                        m_deviceInfo = stDevInfo;
                        deviceFound = true;
                        break;
                    }
                }
                if (!deviceFound)
                {
                    MessageBox.Show($"[HIK] 未能在设备列表中找到序列号为 '{this.DeviceID}' 的设备。", "打开失败");
                    return false;
                }

                // 创建设备句柄
                nRet = CameraSdkObject.MV_CC_CreateDevice_NET(ref m_deviceInfo);
                if (nRet != 0)
                {
                    MessageBox.Show($"[HIK] 创建设备句柄失败！错误码: 0x{nRet:X}", "打开失败");
                    return false;
                }
                // 打开设备
                nRet = CameraSdkObject.MV_CC_OpenDevice_NET();
                if (nRet != 0)
                {
                    // 如果打开失败，需要销毁已创建的句柄
                    CameraSdkObject.MV_CC_DestroyDevice_NET();
                    MessageBox.Show($"[HIK] 打开设备失败！错误码: 0x{nRet:X}", "打开失败");
                    return false;
                }
                // 为GigE相机设置最佳网络包大小
                if (m_deviceInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    int nPacketSize = CameraSdkObject.MV_CC_GetOptimalPacketSize_NET();
                    if (nPacketSize > 0) CameraSdkObject.MV_CC_SetIntValue_NET("GevSCPSPacketSize", (uint)nPacketSize);
                }
                // 注册回调函数
                CameraSdkObject.MV_CC_RegisterImageCallBack_NET(ImageCallback, IntPtr.Zero);
                return true;
            }

            catch (Exception ex)
            {
                MessageBox.Show($"[HIK] 打开设备时发生未知异常: {ex.Message}", "严重错误");
                return false;
            }
        }

        /// <summary>
        /// 实现接口的SetParameter方法，提供安全设置参数的逻辑。
        /// </summary>
        public bool SetParameter(string paramName, object value)
        {
            if (CameraSdkObject == null || !CameraSdkObject.MV_CC_IsDeviceConnected_NET()) return false;
            try
            {
                // 如果是“关键参数”，则执行“停止-设置-重启”的原子操作
                if (criticalParameters.Contains(paramName))
                {
                    bool wasGrabbing = m_bGrabbing;
                    bool wasContinuous = IsContinuousGrabbing();                                        // 记录下在停止前是否是连续模式
                    if (wasGrabbing) { CameraSdkObject.MV_CC_StopGrabbing_NET(); m_bGrabbing = false; }
                    SetSdkParameter(paramName, value);

                    // 如果之前在采集，则恢复采集状态
                    if (wasGrabbing)
                    {
                        CameraSdkObject.MV_CC_StartGrabbing_NET();
                        m_bGrabbing = true;

                        // 特别地，如果之前是连续模式，需要重新调用StartContinuousGrab来恢复正确的触发模式
                        if (wasContinuous) { StartContinuousGrab(); }
                    }
                }
                else { SetSdkParameter(paramName, value); }
                return true;
            }
            catch (Exception ex) { MessageBox.Show($"[HIK] 设置参数 '{paramName}' 失败: {ex.Message}", "错误"); return false; }
        }

        // 根据值的类型调用不同的海康SDK函数
        private void SetSdkParameter(string paramName, object value)
        {
            if (value is long intVal) CameraSdkObject.MV_CC_SetIntValueEx_NET(paramName, intVal);
            else if (value is float floatVal) CameraSdkObject.MV_CC_SetFloatValue_NET(paramName, floatVal);
            else if (value is string stringVal) CameraSdkObject.MV_CC_SetEnumValueByString_NET(paramName, stringVal);
            // Add bool handling if needed
        }

        /// <summary>
        /// 实现接口的ShowParametersWindow方法，创建并返回海康专属的参数窗口。
        /// </summary>
        public Window ShowParametersWindow(Window owner)
        {
            var paramWindow = new HikvisionParametersWindow(this, this.CameraSdkObject);
            paramWindow.Owner = owner;
            return paramWindow;
        }

        /// <summary>
        /// 当SDK内部线程捕获到一帧图像时，此回调方法会被调用。
        /// **注意：此方法运行在非UI线程上。**
        /// </summary>
        private void ProcessImageCallback(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO pFrameInfo, IntPtr pUser)
        {
            m_Ho_Image?.Dispose();                              // 释放上一帧的Halcon图像内存

            // --- 核心转换逻辑: 将海康的图像数据(IntPtr)转换为Halcon的HObject ---
            int width = pFrameInfo.nWidth;
            int height = pFrameInfo.nHeight;
            string channelType = "";


            switch (pFrameInfo.enPixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                    HOperatorSet.GenImage1(out m_Ho_Image, "byte", width, height, pData);
                    break;
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                    channelType = "bgr";

                    HOperatorSet.GenImageInterleaved(out m_Ho_Image, pData, channelType, width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
                    break;
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                    channelType = "rgb";

                    HOperatorSet.GenImageInterleaved(out m_Ho_Image, pData, channelType, width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
                    break;
                // Add more conversions if needed (e.g., for Bayer patterns)
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                    HOperatorSet.GenImage1(out HObject bayerImage, "byte", width, height, pData);
                    HOperatorSet.CfaToRgb(bayerImage, out m_Ho_Image, "bayer_rg", "bilinear");
                    bayerImage.Dispose();
                    break;
                default:
                    // For unsupported formats, create an empty image
                    HOperatorSet.GenEmptyObj(out m_Ho_Image);
                    break;
            }

            // --- 线程安全地更新UI ---
            // 使用BeginInvoke将显示任务“派发”给UI线程，当前后台线程无需等待，可以立刻返回去接收下一帧
            DisplayWindow.Dispatcher.BeginInvoke(new Action(Display));
        }

        private string GetSerialNumber(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
                return gigeInfo.chSerialNumber;
            }
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO usbInfo = (MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO));
                return usbInfo.chSerialNumber;
            }
            return null;
        }

        public void GrabAndDisplay()
        {
            // 单次触发的正确状态设置
            CameraSdkObject.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
            if (!m_bGrabbing)
            {
                int nRet = CameraSdkObject.MV_CC_StartGrabbing_NET();
                if (nRet != 0) return;
                m_bGrabbing = true;
            }
            CameraSdkObject.MV_CC_SetCommandValue_NET("TriggerSoftware");
        }

        public void StartContinuousGrab()
        {
            // 连续采集的正确状态设置
            CameraSdkObject.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
            int nRet = CameraSdkObject.MV_CC_StartGrabbing_NET();
            if (nRet == 0) m_bGrabbing = true;
        }

        public void StopContinuousGrab()
        {
            int nRet = CameraSdkObject.MV_CC_StopGrabbing_NET();
            if (nRet == 0) m_bGrabbing = false;
        }

        

        private void Display()
        {
            if (m_Ho_Image == null || !m_Ho_Image.IsInitialized()) return;
            try
            {
                HWindow window = DisplayWindow.HalconWindow;
                HOperatorSet.GetImageSize(m_Ho_Image, out HTuple imgWidth, out HTuple imgHeight);
                window.SetPart(0, 0, imgHeight.I - 1, imgWidth.I - 1);
                window.DispObj(m_Ho_Image);
            }
            catch (HalconException) { }
        }

        public void Close()
        {
            if (m_bGrabbing) StopContinuousGrab();
            CameraSdkObject?.MV_CC_CloseDevice_NET();
            CameraSdkObject?.MV_CC_DestroyDevice_NET();
            m_Ho_Image?.Dispose();
        }

        
        /// <summary>
        /// 判断相机是否处于自由运行的连续采集模式（即用户理解的“连续触发”）
        /// </summary>
        public bool IsContinuousGrabbing()
        {
            // 必须先确保采集流已启动
            if (!m_bGrabbing || CameraSdkObject == null)
            {
                return false;
            }
            // 判断的依据是 TriggerMode 是否为 OFF
            var triggerMode = new MyCamera.MVCC_ENUMVALUE();
            int nRet = CameraSdkObject.MV_CC_GetEnumValue_NET("TriggerMode", ref triggerMode);
            // 如果成功获取到参数，并且当前值为 OFF，则我们认为它在“连续采集”
            return nRet == 0 && triggerMode.nCurValue == (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF;
        }

        
    }
}
