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
        public string DeviceID { get; private set; }// 获取相机的唯一标识符（序列号）
        public HSmartWindowControlWPF DisplayWindow { get; private set; }// 获取此相机实例绑定的WPF显示控件

        // --- 私有成员变量 ---
        private MyCamera.MV_CC_DEVICE_INFO m_deviceInfo;                        // 存储从SDK枚举到的设备信息结构体
        private HObject m_Ho_Image;
        /// <summary>
        /// 标记相机的采集流（grabbing stream）是否已通过StartGrabbing启动。
        /// 使用'volatile'关键字确保在多线程访问时其值的可见性。
        /// </summary>// 用于存储该相机捕获的最新一帧Halcon图像
        private volatile bool m_bGrabbing = false;                             
        private MyCamera.cbOutputdelegate ImageCallback;                        // 存储图像回调委托的实例，防止被垃圾回收

        // 定义了哪些参数在设置前需要先停止采集流，以确保硬件能够正确应用这些可能会改变图像尺寸或格式的关键更改
        private readonly List<string> criticalParameters = new List<string>
        {
            "Width", "Height", "OffsetX", "OffsetY", "PixelFormat"
        };
        /// <summary>
        /// 构造函数，初始化相机设备。
        /// </summary>
        /// <param name="info">包含设备唯一ID等信息的DeviceInfo对象。</param>
        /// <param name="window">此相机将要绑定的WPF显示控件。</param>
        public HikvisionCameraDevice(DeviceInfo info, HSmartWindowControlWPF window)
        {
            this.DeviceID = info.UniqueID;
            this.DisplayWindow = window;
            CameraSdkObject = new MyCamera();
            m_deviceInfo = new MyCamera.MV_CC_DEVICE_INFO();

            // 将 ProcessImageCallback 方法绑定到回调委托上，当SDK有新图像时会调用它
            // 这是一个异步过程，由SDK的内部线程触发。
            ImageCallback = new MyCamera.cbOutputdelegate(ProcessImageCallback);
            // 初始化一个空的Halcon图像对象，避免在使用前为null
            HOperatorSet.GenEmptyObj(out m_Ho_Image);
        }

        #region 核心功能方法 (ICameraDevice接口实现)
        /// <summary>
        /// 打开设备连接，配置初始参数，并启动采集流，使相机进入“等待触发”状态。
        /// </summary>
        /// <returns>操作是否成功。</returns>
        public bool Open()
        {
            try
            {
                // 枚举所有GigE和USB相机
                var deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
                int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref deviceList);
                if (nRet != 0)
                {
                    throw new Exception($"[HIK] 枚举设备失败！错误码: 0x{nRet:X}");
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
                    throw new Exception($"[HIK] 未能在设备列表中找到序列号为 '{this.DeviceID}' 的设备。");
                }

                // 创建设备句柄
                nRet = CameraSdkObject.MV_CC_CreateDevice_NET(ref m_deviceInfo);
                if (nRet != 0)
                {
                    throw new Exception($"[HIK] 创建设备句柄失败！错误码: 0x{nRet:X}");
                }
                // 打开设备
                nRet = CameraSdkObject.MV_CC_OpenDevice_NET();
                if (nRet != 0)
                {
                    // 如果打开失败，需要销毁已创建的句柄
                    CameraSdkObject.MV_CC_DestroyDevice_NET();
                    throw new Exception($"[HIK] 打开设备失败！错误码: 0x{nRet:X}");
                }
                // 对于GigE相机，设置最佳网络包大小以优化传输性能
                if (m_deviceInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    int nPacketSize = CameraSdkObject.MV_CC_GetOptimalPacketSize_NET();
                    if (nPacketSize > 0) CameraSdkObject.MV_CC_SetIntValue_NET("GevSCPSPacketSize", (uint)nPacketSize);
                }
                // 注册回调函数，告诉SDK新图像数据应该发往 ProcessImageCallback 方法
                CameraSdkObject.MV_CC_RegisterImageCallBack_NET(ImageCallback, IntPtr.Zero);

                // 在Open成功后，立即启动采集流并设置为软触发模式 ---
                // 设置为连续采集模式，这是软触发和自由运行的基础
                CameraSdkObject.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                // 默认开启触发模式，等待软触发命令
                CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                // 将触发源设置为“软触发”
                CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);

                // 启动采集流。这会打开数据通道，让相机准备好接收触发命令
                nRet = CameraSdkObject.MV_CC_StartGrabbing_NET();
                if (nRet != 0)
                {
                    // 如果失败，必须清理资源并抛出异常
                    CameraSdkObject.MV_CC_CloseDevice_NET();
                    CameraSdkObject.MV_CC_DestroyDevice_NET();
                    throw new Exception($"[HIK] 启动采集流失败！错误码: 0x{nRet:X}");
                }
                m_bGrabbing = true; // 标记采集流已成功启动

                return true;
            }

            catch (Exception ex)
            {
                // 捕获所有可能的异常，确保状态正确并向上层报告错误
                m_bGrabbing = false;
                // --- 重新抛出捕获的异常或包装它 ---
                // 如果异常不是我们自己抛出的，就包装一下
                if (!(ex.Message.StartsWith("[HIK]")))
                {
                    throw new Exception($"[HIK] 打开设备时发生未知异常: {ex.Message}");
                }
                // 如果是我们自己抛出的，就直接再次抛出
                throw;
            }
        }

        /// <summary>
        /// 安全地设置相机参数，并处理设置关键参数时需要暂停采集的逻辑。
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
                    bool wasContinuous = IsContinuousGrabbing();// 记录下在停止前是否是连续模式
                    if (wasGrabbing) { CameraSdkObject.MV_CC_StopGrabbing_NET(); m_bGrabbing = false; }
                    SetSdkParameter(paramName, value);// 设置参数

                    // 如果之前在采集，则恢复采集状态
                    if (wasGrabbing)
                    {
                        CameraSdkObject.MV_CC_StartGrabbing_NET();
                        m_bGrabbing = true;

                        // 如果之前是连续模式，需要重新设置为连续模式
                        if (wasContinuous) { StartContinuousGrab(); }
                    }
                }
                else { SetSdkParameter(paramName, value); }
                return true;
            }
            catch (Exception ex) { throw new Exception($"[HIK] 设置参数 '{paramName}' 失败: {ex.Message}", ex); }
        }

        /// <summary>
        /// 执行一次单次触发采集。要求相机已处于“等待触发”的状态。
        /// </summary>
        public void GrabAndDisplay()
        {
            // --- 增加对采集流状态的检查 ---
            if (CameraSdkObject == null || !m_bGrabbing)
            {
                throw new Exception("相机句柄无效或采集流未启动。");
            }
            // 直接发送软触发命令。所有准备工作都已在Open()中完成
            CameraSdkObject.MV_CC_SetCommandValue_NET("TriggerSoftware");
        }
        /// <summary>
        /// 开始自由运行的连续采集模式。
        /// </summary>
        public void StartContinuousGrab()
        {
            // 连续采集意味着关闭触发模式，让相机自由运行出图。
            // 采集流已在Open()中启动，所以这里只需切换模式。
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
        }
        /// <summary>
        /// 停止连续采集，并使相机恢复到“等待触发”的状态。
        /// </summary>
        public void StopContinuousGrab()
        {
            // 停止连续采集后，我们希望相机回到“等待软触发”的状态，以便单次采集可以工作
            CameraSdkObject.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
        }
        /// <summary>
        /// 关闭与相机的连接并释放所有相关资源。
        /// </summary>
        public void Close()
        {
            // --- 确保在关闭设备前，停止采集流 ---
            if (m_bGrabbing)
            {
                CameraSdkObject?.MV_CC_StopGrabbing_NET();
                m_bGrabbing = false; // 重置标志位
            }
            // 依次关闭设备和销毁句柄
            CameraSdkObject?.MV_CC_CloseDevice_NET();
            CameraSdkObject?.MV_CC_DestroyDevice_NET();
            // 释放Halcon图像对象占用的内存
            m_Ho_Image?.Dispose();
        }
        /// <summary>
        /// 检查相机当前是否处于连续采集模式。
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
        /// <summary>
        /// 获取相机当前持有的最新图像对象。
        /// </summary>
        public HObject GetCurrentImage()
        {
            return m_Ho_Image;
        }
        #endregion

        #region 内部辅助方法



        /// <summary>
        /// 当SDK内部线程捕获到一帧图像时，此回调方法会被调用。
        /// **注意：此方法运行在非UI线程上。**
        /// </summary>
        //private void ProcessImageCallback(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO pFrameInfo, IntPtr pUser)
        //{
        //    // 声明一个临时的、代表相机易失缓冲区的图像对象
        //    HObject volatileImage = null;
        //    try
        //    {
        //        // --- 核心转换逻辑: 将海康的图像数据(IntPtr)转换为一个临时的HObject ---
        //        int width = pFrameInfo.nWidth;
        //        int height = pFrameInfo.nHeight;
        //        string channelType = "";

        //        HOperatorSet.GenEmptyObj(out volatileImage); // 先初始化，防止后续失败导致对象无效

        //        switch (pFrameInfo.enPixelType)
        //        {
        //            case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
        //                HOperatorSet.GenImage1(out volatileImage, "byte", width, height, pData);
        //                break;
        //            case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
        //                channelType = "bgr";
        //                HOperatorSet.GenImageInterleaved(out volatileImage, pData, channelType, width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
        //                break;
        //            case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
        //                channelType = "rgb";
        //                HOperatorSet.GenImageInterleaved(out volatileImage, pData, channelType, width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
        //                break;
        //            case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
        //                HOperatorSet.GenImage1(out HObject bayerImage, "byte", width, height, pData);
        //                HOperatorSet.CfaToRgb(bayerImage, out volatileImage, "bayer_rg", "bilinear");
        //                bayerImage.Dispose();
        //                break;
        //            default:
        //                // 对于不支持的格式，volatileImage 保持为空对象
        //                break;
        //        }
        //        // 释放上一帧的稳定图像内存
        //        m_Ho_Image?.Dispose();
        //        // 从易失的缓冲区图像，创建一个全新的、内存独立的稳定副本
        //        HOperatorSet.CopyImage(volatileImage, out m_Ho_Image);

        //        // --- 线程安全地更新UI ---
        //        // 使用Dispatcher.BeginInvoke将显示任务“派发”给UI线程，
        //        // 当前后台线程无需等待，可以立刻返回去接收下一帧，保证了采集效率。
        //        DisplayWindow.Dispatcher.BeginInvoke(new Action(Display));
        //    }
        //    finally
        //    {
        //        // 确保代表缓冲区的临时对象被释放
        //        volatileImage?.Dispose();
        //    }
        //}
        private void ProcessImageCallback(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO pFrameInfo, IntPtr pUser)
        {
            // 在后台线程中，只做最核心的一件事：将SDK的内存数据转为一个临时的Halcon图像对象。
            HObject volatileImage = null;
            try
            {
                int width = pFrameInfo.nWidth;
                int height = pFrameInfo.nHeight;
                HOperatorSet.GenEmptyObj(out volatileImage);

                switch (pFrameInfo.enPixelType)
                {
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                        HOperatorSet.GenImage1(out volatileImage, "byte", width, height, pData);
                        break;
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                        HOperatorSet.GenImageInterleaved(out volatileImage, pData, "bgr", width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
                        break;
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                        HOperatorSet.GenImageInterleaved(out volatileImage, pData, "rgb", width, height, -1, "byte", 0, 0, 0, 0, -1, 0);
                        break;

                    // 【【【 针对编译错误的最终核心修正 】】】
                    case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                        HObject bayerImage = null; // 在 try-finally 块之外声明
                        try
                        {
                            // 现在 bayerImage 可以作为 out 参数被传递
                            HOperatorSet.GenImage1(out bayerImage, "byte", width, height, pData);
                            HOperatorSet.CfaToRgb(bayerImage, out volatileImage, "bayer_rg", "bilinear");
                        }
                        finally
                        {
                            // 在 finally 块中确保释放，这和 using 的作用一样安全
                            bayerImage?.Dispose();
                        }
                        break;

                    default:
                        volatileImage?.Dispose();
                        return; // 不支持的格式，直接返回
                }

                // 检查转换是否成功
                if (volatileImage == null || !volatileImage.IsInitialized() || volatileImage.CountObj() == 0)
                {
                    volatileImage?.Dispose();
                    return;
                }

                // 将【临时的volatileImage】直接异步调度到UI线程。
                // UI线程会负责拷贝、显示和释放它。
                DisplayWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 1. 在UI线程中，释放旧的稳定图像
                        m_Ho_Image?.Dispose();

                        // 2. 在UI线程中，从临时的 volatileImage 拷贝一份到 m_Ho_Image
                        HOperatorSet.CopyImage(volatileImage, out m_Ho_Image);

                        // 3. 在UI线程中，调用新的 Display 方法来显示刚拷贝好的稳定图像
                        Display(m_Ho_Image); // 注意：这里需要你之前已将 Display 方法修改为接受 HObject 参数
                    }
                    finally
                    {
                        // 4. 【关键】在UI线程中，所有操作完成后，再释放这个临时的 volatileImage。
                        // 这样就完全避免了竞态条件。
                        volatileImage?.Dispose();
                    }
                }));
            }
            catch (Exception)
            {
                // 如果在后台转换过程中发生异常，也要确保释放对象
                volatileImage?.Dispose();
            }
        }
        /// <summary>
        /// 在绑定的WPF窗口中显示当前图像。
        /// 此方法总是在UI线程上被调用。
        /// </summary>
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
            catch (HalconException)
            {
                // 在快速刷新或窗口关闭时，可能会发生异常，这里静默处理以增强程序稳定性。
            }
        }

        /// <summary>
        /// 根据值的类型调用不同的海康SDK函数来设置参数。
        /// </summary>
        private void SetSdkParameter(string paramName, object value)
        {
            if (value is long intVal) CameraSdkObject.MV_CC_SetIntValueEx_NET(paramName, intVal);
            else if (value is float floatVal) CameraSdkObject.MV_CC_SetFloatValue_NET(paramName, floatVal);
            else if (value is string stringVal) CameraSdkObject.MV_CC_SetEnumValueByString_NET(paramName, stringVal);
            // 这里可以根据需要添加对 bool 等其他类型的处理
        }
        /// <summary>
        /// 从海康设备信息结构体中提取序列号。
        /// </summary>
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
        #endregion

    }
}
