// HikvisionCameraDevice.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using HalconDotNet;
using MvCamCtrl.NET;

namespace VisionLite
{
    /// <summary>
    /// ICameraDevice接口的具体实现，使用海康MVS SDK来控制相机。
    /// 【已重构为与HalconDevice一致的后台线程同步轮询模式】
    /// </summary>
    public class HikvisionCameraDevice : ICameraDevice
    {
        public MyCamera CameraSdkObject { get; private set; }
        public string DeviceID { get; private set; }
        public HSmartWindowControlWPF DisplayWindow { get; private set; }

        private MyCamera.MV_CC_DEVICE_INFO m_deviceInfo;
        private HObject m_Ho_Image;

        // 【新增】用于同步轮询的后台线程和控制标志
        private Thread continuousGrabThread;
        private volatile bool isContinuousGrabbing = false;
        private volatile bool isGrabbingStreamActive = false;

        // 【新增】用于图像数据转换的缓冲区
        private byte[] m_pDataForRed;
        private IntPtr m_pBufForDriver;
        private uint m_nBufSizeForDriver;


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
            HOperatorSet.GenEmptyObj(out m_Ho_Image);
        }

        #region 核心功能方法 (ICameraDevice接口实现)
        public bool Open()
        {
            try
            {
                // 枚举设备并找到匹配的设备... (此部分逻辑不变)
                var deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
                int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref deviceList);
                if (nRet != 0) throw new Exception($"[HIK] 枚举设备失败！错误码: 0x{nRet:X}");

                bool deviceFound = false;
                for (int i = 0; i < deviceList.nDeviceNum; i++)
                {
                    var stDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(deviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                    if (GetSerialNumber(stDevInfo) == this.DeviceID)
                    {
                        m_deviceInfo = stDevInfo;
                        deviceFound = true;
                        break;
                    }
                }
                if (!deviceFound) throw new Exception($"[HIK] 未找到序列号为 '{this.DeviceID}' 的设备。");

                // 创建和打开设备... (此部分逻辑不变)
                nRet = CameraSdkObject.MV_CC_CreateDevice_NET(ref m_deviceInfo);
                if (nRet != 0) throw new Exception($"[HIK] 创建设备句柄失败！错误码: 0x{nRet:X}");

                nRet = CameraSdkObject.MV_CC_OpenDevice_NET();
                if (nRet != 0)
                {
                    CameraSdkObject.MV_CC_DestroyDevice_NET();
                    throw new Exception($"[HIK] 打开设备失败！错误码: 0x{nRet:X}");
                }

                if (m_deviceInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    int nPacketSize = CameraSdkObject.MV_CC_GetOptimalPacketSize_NET();
                    if (nPacketSize > 0) CameraSdkObject.MV_CC_SetIntValue_NET("GevSCPSPacketSize", (uint)nPacketSize);
                }

                // 【修改】不再注册回调
                // CameraSdkObject.MV_CC_RegisterImageCallBack_NET(ImageCallback, IntPtr.Zero);

                // 【修改】获取Payload大小用于后续缓冲区分配
                MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
                nRet = CameraSdkObject.MV_CC_GetIntValue_NET("PayloadSize", ref stParam);
                if (nRet != 0) throw new Exception($"[HIK] 获取PayloadSize失败! nRet=0x{nRet:X}");
                m_nBufSizeForDriver = stParam.nCurValue;
                m_pBufForDriver = Marshal.AllocHGlobal((int)m_nBufSizeForDriver);


                // 启动采集流，让相机准备好出图
                nRet = CameraSdkObject.MV_CC_StartGrabbing_NET();
                if (nRet != 0)
                {
                    CameraSdkObject.MV_CC_CloseDevice_NET();
                    CameraSdkObject.MV_CC_DestroyDevice_NET();
                    throw new Exception($"[HIK] 启动采集流失败！错误码: 0x{nRet:X}");
                }
                isGrabbingStreamActive = true;

                return true;
            }
            catch (Exception )
            {
                isGrabbingStreamActive = false;
                if (m_pBufForDriver != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(m_pBufForDriver);
                    m_pBufForDriver = IntPtr.Zero;
                }
                throw;
            }
        }

        public void GrabAndDisplay()
        {
            if (CameraSdkObject == null || !isGrabbingStreamActive)
            {
                throw new Exception("相机句柄无效或采集流未启动。");
            }

            // 切换到单次触发模式
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);

            // 发送软触发
            int nRet = CameraSdkObject.MV_CC_SetCommandValue_NET("TriggerSoftware");
            if (nRet != 0) throw new Exception($"[HIK] 软触发失败! nRet=0x{nRet:X}");

            // 同步获取一帧图像并显示
            GrabSingleFrameAndDisplay();
        }

        public void StartContinuousGrab()
        {
            if (CameraSdkObject == null || isContinuousGrabbing) return;

            // 切换到连续模式，让相机自由出图
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

            isContinuousGrabbing = true;
            continuousGrabThread = new Thread(ContinuousGrabLoop);
            continuousGrabThread.IsBackground = true;
            continuousGrabThread.Start();
        }

        public void StopContinuousGrab()
        {
            isContinuousGrabbing = false;
        }

        public void Close()
        {
            StopContinuousGrab();

            if (continuousGrabThread != null && continuousGrabThread.IsAlive)
            {
                continuousGrabThread.Join(100);
            }

            if (isGrabbingStreamActive)
            {
                CameraSdkObject?.MV_CC_StopGrabbing_NET();
                isGrabbingStreamActive = false;
            }
            CameraSdkObject?.MV_CC_CloseDevice_NET();
            CameraSdkObject?.MV_CC_DestroyDevice_NET();

            m_Ho_Image?.Dispose();

            if (m_pBufForDriver != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_pBufForDriver);
                m_pBufForDriver = IntPtr.Zero;
            }
        }

        public bool IsContinuousGrabbing() => isContinuousGrabbing;

        public bool SetParameter(string paramName, object value)
        {
            if (CameraSdkObject == null || !CameraSdkObject.MV_CC_IsDeviceConnected_NET()) return false;
            try
            {
                if (criticalParameters.Contains(paramName))
                {
                    bool wasContinuous = isContinuousGrabbing;
                    if (wasContinuous) StopContinuousGrab();

                    if (isGrabbingStreamActive)
                    {
                        CameraSdkObject.MV_CC_StopGrabbing_NET();
                        isGrabbingStreamActive = false;
                    }

                    SetSdkParameter(paramName, value);

                    CameraSdkObject.MV_CC_StartGrabbing_NET();
                    isGrabbingStreamActive = true;

                    if (wasContinuous) StartContinuousGrab();
                }
                else
                {
                    SetSdkParameter(paramName, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"[HIK] 设置参数 '{paramName}' 失败: {ex.Message}", ex);
            }
        }

        public HObject GetCurrentImage() => m_Ho_Image;
        #endregion

        #region 内部辅助方法
        private void ContinuousGrabLoop()
        {
            while (isContinuousGrabbing)
            {
                try
                {
                    HObject tempImage = GrabSingleFrame();
                    if (tempImage == null || !tempImage.IsInitialized())
                    {
                        Thread.Sleep(10); // 获取失败时短暂休眠
                        continue;
                    }

                    DisplayWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            m_Ho_Image?.Dispose();
                            m_Ho_Image = tempImage;
                            Display(m_Ho_Image);
                        }
                        catch (Exception)
                        {
                            tempImage?.Dispose();
                        }
                    }));
                }
                catch (Exception)
                {
                    Thread.Sleep(10);
                }
            }
            // 循环结束后，恢复为等待触发模式
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
        }

        private void GrabSingleFrameAndDisplay()
        {
            HObject tempImage = GrabSingleFrame();
            if (tempImage == null) return;

            DisplayWindow.Dispatcher.Invoke(() =>
            {
                m_Ho_Image?.Dispose();
                m_Ho_Image = tempImage;
                Display(m_Ho_Image);
            });
        }

        
        // 核心的同步取图和转换函数
        private HObject GrabSingleFrame()
        {
            // 1. 同步获取一帧原始图像数据
            MyCamera.MV_FRAME_OUT_INFO_EX stFrameInfo = new MyCamera.MV_FRAME_OUT_INFO_EX();
            int nRet = CameraSdkObject.MV_CC_GetOneFrameTimeout_NET(m_pBufForDriver, m_nBufSizeForDriver, ref stFrameInfo, 1000);
            if (nRet != 0) return null;

            int width = (int)stFrameInfo.nWidth;
            int height = (int)stFrameInfo.nHeight;
            HObject image = null;
            HOperatorSet.GenEmptyObj(out image);

            try
            {
                switch (stFrameInfo.enPixelType)
                {
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                        HOperatorSet.GenImage1(out image, "byte", width, height, m_pBufForDriver);
                        break;

                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:

                        uint nDestBufSize = (uint)(width * height * 3);
                        if (m_pDataForRed == null || m_pDataForRed.Length != nDestBufSize)
                        {
                            m_pDataForRed = new byte[nDestBufSize];
                        }

                        // 【【【 核心修正：添加 (ushort) 强制类型转换 】】】
                        MyCamera.MV_PIXEL_CONVERT_PARAM stConvertParam = new MyCamera.MV_PIXEL_CONVERT_PARAM
                        {
                            nWidth = (ushort)width,       // 强制转换为 ushort
                            nHeight = (ushort)height,     // 强制转换为 ushort
                            pSrcData = m_pBufForDriver,
                            nSrcDataLen = stFrameInfo.nFrameLen,
                            enSrcPixelType = stFrameInfo.enPixelType,
                            enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Planar,
                            pDstBuffer = Marshal.UnsafeAddrOfPinnedArrayElement(m_pDataForRed, 0),
                            nDstBufferSize = nDestBufSize
                        };

                        nRet = CameraSdkObject.MV_CC_ConvertPixelType_NET(ref stConvertParam);
                        if (nRet != 0)
                        {
                            image.Dispose();
                            return null;
                        }

                        IntPtr pR = stConvertParam.pDstBuffer;
                        IntPtr pG = new IntPtr(pR.ToInt64() + width * height);
                        IntPtr pB = new IntPtr(pG.ToInt64() + width * height);

                        HOperatorSet.GenImage3(out image, "byte", width, height, pR, pG, pB);
                        break;

                    default:
                        image.Dispose();
                        return null;
                }
                return image;
            }
            catch (Exception)
            {
                image?.Dispose();
                return null;
            }
        }
        private void Display(HObject imageToShow)
        {
            if (imageToShow == null || !imageToShow.IsInitialized()) return;
            try
            {
                HOperatorSet.GetImageSize(imageToShow, out HTuple imgWidth, out HTuple imgHeight);
                DisplayWindow.HImagePart = new Rect(0, 0, imgWidth.I, imgHeight.I);
                DisplayWindow.HalconWindow.ClearWindow();
                DisplayWindow.HalconWindow.DispObj(imageToShow);
            }
            catch (HalconException) { }
        }

        private void SetSdkParameter(string paramName, object value)
        {
            if (value is long intVal) CameraSdkObject.MV_CC_SetIntValueEx_NET(paramName, intVal);
            else if (value is float floatVal) CameraSdkObject.MV_CC_SetFloatValue_NET(paramName, floatVal);
            else if (value is string stringVal) CameraSdkObject.MV_CC_SetEnumValueByString_NET(paramName, stringVal);
        }

        private string GetSerialNumber(MyCamera.MV_CC_DEVICE_INFO devInfo)
        {
            if (devInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                var gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
                return gigeInfo.chSerialNumber;
            }
            if (devInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                var usbInfo = (MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(devInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO));
                return usbInfo.chSerialNumber;
            }
            return null;
        }
        #endregion
    }
}