//HalconCameraDevice.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HalconDotNet;
using System.Windows;
using System.Threading;

namespace VisionLite
{
    /// <summary>
    /// ICameraDevice接口的具体实现，使用Halcon的MVision Framegrabber接口来控制相机。
    /// 这是一个通用的接口，可以驱动多种符合GenICam标准的相机。
    /// </summary>
    public class HalconCameraDevice : ICameraDevice
    {
        // --- 接口属性实现 ---
        public string DeviceID { get; private set; } // 获取相机的唯一标识符（由Halcon在枚举时生成的ID）
        public HSmartWindowControlWPF DisplayWindow { get; private set; } // 获取此相机实例绑定的WPF显示控件

        public HTuple m_pAcqHandle; //  Halcon图像采集设备的句柄
        public HObject m_Ho_Image; // 用于存储该相机捕获的最新一帧Halcon图像

        // 私有成员变量
        private bool isGrabbing = false; // 标记相机的采集流（grabbing stream）是否已通过GrabImageStart启动

        
        private Thread continuousGrabThread; // 用于执行连续采集的后台线程
        private volatile bool isContinuousGrabbing = false;// 控制后台连续采集线程循环的标志位

        // 定义一个列表，包含那些需要停止采集才能修改的关键参数
        private readonly List<string> criticalParameters = new List<string>
        {
            "Width", "Height", "OffsetX", "OffsetY", "PixelFormat"
        };

        /// <summary>
        /// 构造函数，初始化相机设备。
        /// </summary>
        /// <param name="deviceId">由Halcon InfoFramegrabber返回的设备唯一ID。</param>
        /// <param name="window">此相机将要绑定的WPF显示控件。</param>
        public HalconCameraDevice(string deviceId, HSmartWindowControlWPF window)
        {
            this.DeviceID = deviceId;
            this.DisplayWindow = window;
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
                // 打开指定的图像采集设备
                HOperatorSet.OpenFramegrabber("MVision", 1, 1, 0, 0, 0, 0,
                    "progressive",          //HTuple field,指定采集的是半场还是全场图像。'default' 或 'progressive'。通常使用 'default'。
                    8,                      //HTuple bitsPerChannel,每个通道的像素位数。-1 表示使用默认值。常见值为 8 (对于 Mono8 或 RGB8)。
                    "default",              //HTuple colorSpace,图像的颜色空间。'default', 'gray', 'rgb'。根据相机类型和需求选择。
                    -1,                     //HTuple generic,通用参数，用于传递一些不常用的或设备特定的参数。-1 表示不使用。
                    "false",                //HTuple externalTrigger,是否使用外部触发。'false' (不使用), 'true' (使用)。
                    "auto",                 //HTuple cameraType,使用的相机类型。'default' 表示使用特定于设备的默认值。
                    DeviceID,               //HTuple device,图像采集设备的名称或索引。'default' 或相机的唯一标识符，例如序列号。[4] 在某些情况下，也可以是 '[0]' 或 'cam0'。
                    0,                      //HTuple port,图像采集设备连接的端口。通常设为 0 或 -1 (默认值)。
                    -1,                     //HTuple lineIn,多路复用相机的输入线。-1 表示使用硬件默认值。
                    out m_pAcqHandle);     //输出参数，返回打开的图像采集设备的句柄。

                // 设置软触发模式
                HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerMode", "On");
                HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerSource", "Software");

                // 在Open成功后，立即启动采集流
                HOperatorSet.GrabImageStart(m_pAcqHandle, -1);
                isGrabbing = true;

                return true; // 打开成功
            }
            catch (HalconException ex)
            {
                // 如果启动采集流失败，也要确保isGrabbing为false
                isGrabbing = false;

                throw new Exception($"打开设备 {DeviceID} 失败: {ex.GetErrorMessage()}");
            }
        }

        /// <summary>
        /// 执行一次单次触发采集，并将结果显示在绑定的窗口中。
        /// </summary>
        public void GrabAndDisplay()
        {
            if (m_pAcqHandle == null || !isGrabbing)
            {
                throw new Exception("相机句柄无效或采集流未启动。");
            }

            try
            {
                // 发送软触发命令
                HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerSoftware", "do_it");
                // 异步获取一帧图像，设置5秒超时
                HOperatorSet.GrabImageAsync(out HObject tempImage, m_pAcqHandle, 5000);

                // 成功获取图像后，先释放旧的图像内存
                m_Ho_Image?.Dispose();
                // 将新采集的图像赋值给类成员变量
                m_Ho_Image = tempImage;
                // 在自己的窗口中显示
                Display();
            }
            catch (HalconException ex)
            {
                // 忽略单次采集的超时错误，避免在调整参数后第一次触发时因未来得及出图而报错
                if (ex.GetErrorCode() != 5322) // H_ERR_TIMEOUT
                {
                    throw new Exception($"从设备 {DeviceID} 采集图像失败: {ex.GetErrorMessage()}");
                }
            }
        }

        /// <summary>
        /// 开始自由运行的连续采集模式。
        /// </summary>
        public void StartContinuousGrab()
        {
            if (m_pAcqHandle == null || isContinuousGrabbing)
            {
                // 如果相机未打开、未开始异步采集、或者已经正在连续采集中，则不做任何事
                return;
            }

            try
            {
                if (!isGrabbing)
                {
                    HOperatorSet.GrabImageStart(m_pAcqHandle, -1);
                    isGrabbing = true;
                }
                // Halcon的连续采集是通过在后台线程中不断发送软触发命令来模拟的
                // 打开连续采集的开关
                isContinuousGrabbing = true;
                // 创建并启动一个新的后台线程，让它去执行 ContinuousGrabLoop 方法
                continuousGrabThread = new Thread(ContinuousGrabLoop);
                continuousGrabThread.IsBackground = true; // 设置为后台线程，这样主程序退出时它会自动结束
                continuousGrabThread.Start();
            }
            catch (HalconException ex)
            {
                throw new Exception($"开始连续采集失败: {ex.GetErrorMessage()}");
            }
        }

        /// <summary>
        /// 停止连续采集。
        /// </summary>
        public void StopContinuousGrab()
        {
            // 只需将标志位设为false，后台线程就会在下一次循环检查时自动退出
            isContinuousGrabbing = false;
        }

        /// <summary>
        /// 关闭相机并释放资源
        /// </summary>
        public void Close()
        {

            // 确保在关闭相机前，先停止连续采集
            StopContinuousGrab(); // 确保后台线程已停止

            if (m_pAcqHandle != null)
            {
                try
                {
                    // 如果正在采集，需要先停止
                    HOperatorSet.CloseFramegrabber(m_pAcqHandle);
                }
                catch (HalconException) { /* 忽略关闭错误 */ }

                // 重置状态
                m_pAcqHandle = null;
                isGrabbing = false;
            }
            // 无论相机是否打开，都尝试释放图像对象占用的内存
            m_Ho_Image?.Dispose();
        }
        /// <summary>
        /// 检查相机当前是否处于连续采集模式。
        /// </summary>
        public bool IsContinuousGrabbing() => isContinuousGrabbing;
        /// <summary>
        /// 安全地设置相机参数。
        /// </summary>
        public bool SetParameter(string paramName, object value)
        {

            if (m_pAcqHandle == null) return false;
            try
            {
                HTuple halconValue = new HTuple(value);
                if (criticalParameters.Contains(paramName))
                {
                    bool wasContinuous = this.isContinuousGrabbing;
                    if (wasContinuous) StopContinuousGrab();
                    // 对于Halcon接口，设置关键参数前需要先调用AcquisitionStop
                    if (isGrabbing)
                    {
                        HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "AcquisitionStop", "do_it");
                        isGrabbing = false;
                    }
                    HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, halconValue);
                    if (wasContinuous)
                    {
                        // 重启采集流
                        HOperatorSet.GrabImageStart(m_pAcqHandle, -1);
                        isGrabbing = true;
                        StartContinuousGrab();
                    }
                }
                else
                {
                    HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, halconValue);
                }
                return true;
            }
            catch (HalconException hex)
            {
                throw hex;
            }
        }
        /// <summary>
        /// 获取相机当前持有的最新图像对象。
        /// </summary>
        public HObject GetCurrentImage()
        {
            return m_Ho_Image;
        }
        /// <summary>
        /// 供参数窗口访问句柄。
        /// </summary>
        public HTuple GetAcqHandle() => m_pAcqHandle;
        #endregion

        #region 内部辅助方法
        /// <summary>
        /// 后台线程循环体，用于模拟连续采集。
        /// </summary>
        private void ContinuousGrabLoop()
        {
            HObject tempImage = new HObject(); // 在循环外创建临时图像变量，提高效率

            // 只要开关是开着的，就一直循环
            while (isContinuousGrabbing)
            {
                try
                {
                    // 和单次采集一样，先发触发命令，再异步获取图像
                    HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerSoftware", "do_it");
                    HOperatorSet.GrabImageAsync(out tempImage, m_pAcqHandle, 1000); // 超时设短一点

                    // 在UI主线程上更新显示
                    DisplayWindow.Dispatcher.Invoke(() =>
                    {
                        m_Ho_Image?.Dispose();
                        m_Ho_Image = tempImage.CopyObj(1, -1); // 必须复制一份图像，因为tempImage马上会被下一次循环覆盖
                        Display();
                    });
                }
                catch (HalconException)
                {
                    // 如果采集超时或发生错误，短暂休眠后继续尝试
                    Thread.Sleep(50);
                }
            }
            tempImage.Dispose(); // 循环结束后，释放临时图像变量
        }

        /// <summary>
        /// 在绑定的WPF窗口中显示当前图像。
        /// </summary>
        private void Display()
        {
            if (m_Ho_Image == null || !m_Ho_Image.IsInitialized()) return;

            try
            {
                HOperatorSet.GetImageSize(m_Ho_Image, out HTuple width, out HTuple height);

                
                // 必须在UI线程上更新WPF依赖属性
                DisplayWindow.Dispatcher.Invoke(() =>
                {
                    DisplayWindow.HImagePart = new Rect(0, 0, width.I, height.I);
                });
                DisplayWindow.HalconWindow.ClearWindow();
                // HImagePart设置后，控件会自动调用SetPart，我们只需显示对象即可
                DisplayWindow.HalconWindow.DispObj(m_Ho_Image);
            }

            catch (HalconException)
            {
                // 在快速刷新时，对象可能已被释放，忽略此错误
            }
        }

        #endregion





    }

}