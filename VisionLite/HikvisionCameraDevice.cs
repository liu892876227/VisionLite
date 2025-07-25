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
    public class HikvisionCameraDevice : ICameraDevice
    {
        // --- 添加一个公共属性以暴露内部SDK对象 ---
        public MyCamera CameraSdkObject { get; private set; }
        public string DeviceID { get; private set; }
        public HSmartWindowControlWPF DisplayWindow { get; private set; }

        
        private MyCamera.MV_CC_DEVICE_INFO m_deviceInfo;
        private HObject m_Ho_Image;
        private volatile bool m_bGrabbing = false;
        private MyCamera.cbOutputdelegate ImageCallback;

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
            ImageCallback = new MyCamera.cbOutputdelegate(ProcessImageCallback);
            HOperatorSet.GenEmptyObj(out m_Ho_Image);
        }
        // --- 新增点 2: 实现统一的 SetParameter 接口方法 ---
        public bool SetParameter(string paramName, object value)
        {
            if (CameraSdkObject == null || !CameraSdkObject.MV_CC_IsDeviceConnected_NET()) return false;
            try
            {
                if (criticalParameters.Contains(paramName))
                {
                    bool wasGrabbing = m_bGrabbing;
                    bool wasContinuous = IsContinuousGrabbing();
                    if (wasGrabbing) { CameraSdkObject.MV_CC_StopGrabbing_NET(); m_bGrabbing = false; }
                    SetSdkParameter(paramName, value);
                    if (wasGrabbing)
                    {
                        CameraSdkObject.MV_CC_StartGrabbing_NET();
                        m_bGrabbing = true;
                        if (wasContinuous) { StartContinuousGrab(); }
                    }
                }
                else { SetSdkParameter(paramName, value); }
                return true;
            }
            catch (Exception ex) { MessageBox.Show($"[HIK] 设置参数 '{paramName}' 失败: {ex.Message}", "错误"); return false; }
        }

        // 辅助方法，用于根据值的类型调用不同的SDK Set函数
        private void SetSdkParameter(string paramName, object value)
        {
            if (value is long intVal) CameraSdkObject.MV_CC_SetIntValueEx_NET(paramName, intVal);
            else if (value is float floatVal) CameraSdkObject.MV_CC_SetFloatValue_NET(paramName, floatVal);
            else if (value is string stringVal) CameraSdkObject.MV_CC_SetEnumValueByString_NET(paramName, stringVal);
            // Add bool handling if needed
        }

        public Window ShowParametersWindow(Window owner)
        {
            var paramWindow = new HikvisionParametersWindow(this, this.CameraSdkObject);
            paramWindow.Owner = owner;
            return paramWindow;
        }

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
                // 5. 为GigE相机设置最佳网络包大小
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

        private void ProcessImageCallback(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO pFrameInfo, IntPtr pUser)
        {
            m_Ho_Image?.Dispose(); // Dispose previous image

            // Convert raw data to HObject
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

            // 使用 BeginInvoke 进行异步UI更新，解除后台线程的等待，从而避免死锁
            DisplayWindow.Dispatcher.BeginInvoke(new Action(Display));
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
