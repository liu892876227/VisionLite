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
    /// 使用Halcon MVision接口的相机实现
    /// </summary>
    public class HalconCameraDevice : ICameraDevice
    {
        public event Action<ICameraDevice, HObject> ImageAcquired; // 实现接口定义的事件
        public string DeviceID { get; private set; } // 相机的唯一ID
        public HSmartWindowControlWPF DisplayWindow { get; private set; } // 绑定的显示窗口

        public HTuple m_pAcqHandle; // 相机的句柄
        public HObject m_Ho_Image; // 用于存储该相机捕获的图像

        private bool isGrabbing = false; // 标记相机采集流是否已通过 GrabImageStart 启动

        // 用于控制连续采集的成员变量
        private Thread continuousGrabThread; // 用于连续采集的后台线程
        private volatile bool isContinuousGrabbing = false;

        // 定义一个列表，包含那些需要停止采集才能修改的关键参数
        private readonly List<string> criticalParameters = new List<string>
        {
            "Width", "Height", "OffsetX", "OffsetY", "PixelFormat"
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        public HalconCameraDevice(string deviceId, HSmartWindowControlWPF window)
        {
            this.DeviceID = deviceId;
            this.DisplayWindow = window;
            HOperatorSet.GenEmptyObj(out m_Ho_Image);
        }

        public Window ShowParametersWindow(Window owner)
        {
            var paramWindow = new HalconParametersWindow(this);
            paramWindow.Owner = owner;
            return paramWindow;
        }

        /// <summary>
        /// 打开并配置相机
        /// </summary>
        public bool Open()
        {
            try
            {
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
            if (m_pAcqHandle == null) return;
            // --- 新增：在 try 外部声明 tempImage，以便 finally 可以访问 ---
            HObject tempImage = null;

            try
            {
                // --- 在需要时才启动采集流 ---
                // 如果采集流尚未启动，则在这里启动它
                if (!isGrabbing)
                {
                    HOperatorSet.GrabImageStart(m_pAcqHandle, -1);
                    isGrabbing = true;
                }

                HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerSoftware", "do_it");
                HOperatorSet.GrabImageAsync(out tempImage, m_pAcqHandle, 5000);


                if (tempImage != null && tempImage.IsInitialized())
                {
                    // --- 核心修改 开始 ---
                    // 1. 【修改】触发事件，将 tempImage 的所有权转移给 MainWindow
                    ImageAcquired?.Invoke(this, tempImage);

                    // 2. 【新增】所有权已转移，将本地引用设为null，防止 finally 块将其错误地释放
                    tempImage = null;
                }
                //tempImage?.Dispose(); // 释放临时的 tempImage
            }
            catch (HalconException ex)
            {
                // 忽略单次采集的超时错误，避免在调整参数后第一次触发时因未来得及出图而报错
                if (ex.GetErrorCode() != 5322) // H_ERR_TIMEOUT
                {
                    MessageBox.Show($"从设备 {DeviceID} 采集图像失败: \n{ex.GetErrorMessage()}", "错误");
                }
            }
            // --- 新增 finally 块，确保资源安全 ---
            finally
            {
                // 如果图像没有被成功上报（即 tempImage 不为 null），则在这里释放
                tempImage?.Dispose();
            }
        }

        // 开始连续采集的方法
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

                // 打开连续采集的开关
                isContinuousGrabbing = true;
                // 创建并启动一个新的后台线程，让它去执行 ContinuousGrabLoop 方法
                continuousGrabThread = new Thread(ContinuousGrabLoop);
                continuousGrabThread.IsBackground = true; // 设置为后台线程，这样主程序退出时它会自动结束
                continuousGrabThread.Start();
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"开始连续采集失败: \n{ex.GetErrorMessage()}", "错误");
            }
        }

        // 停止连续采集的方法
        public void StopContinuousGrab()
        {
            // 1. 设置标志位，通知后台线程停止循环。
            //    由于 isContinuousGrabbing 是 volatile 的，这个更改会立即被后台线程看到。
            isContinuousGrabbing = false;

            // 2. 移除 Join() 调用。
            //    我们不再阻塞UI线程等待后台线程结束。
            //    后台线程会在完成最后一次循环（最多几毫秒）后自行终止。
            //    continuousGrabThread?.Join(200); // <--- 移除或注释掉这行致命的代码
        }

        // 后台线程真正执行的循环体
        private void ContinuousGrabLoop()
        {
            

            // 只要开关是开着的，就一直循环
            while (isContinuousGrabbing)
            {
                // --- 新增：在每次循环开始时将 tempImage 设为 null ---
                HObject tempImage = null;
                try
                {
                    // 和单次采集一样，先发触发命令，再异步获取图像
                    HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "TriggerSoftware", "do_it");
                    HOperatorSet.GrabImageAsync(out tempImage, m_pAcqHandle, 1000); // 超时设短一点

                    if (tempImage != null && tempImage.IsInitialized())
                    {
                        // --- 核心修改 开始 ---
                        // 1. 【修改】直接触发事件，不再需要 Dispatcher.Invoke
                        ImageAcquired?.Invoke(this, tempImage);

                        // 2. 【新增】转移所有权
                        tempImage = null;
                    }
                }
                catch (HalconException) { Thread.Sleep(50); }
                finally
                {
                    tempImage?.Dispose();
                }
            }
             
        }

        /// <summary>
        /// 在绑定的窗口中显示图像
        /// </summary>
        private void Display()
        {
            if (m_Ho_Image == null || !m_Ho_Image.IsInitialized()) return;

            try
            {
                HWindow window = DisplayWindow.HalconWindow;                                // 从WPF控件中获取到真正的HALCON窗口对象
                HOperatorSet.GetImageSize(m_Ho_Image, out HTuple width, out HTuple height);   // 获取图像的宽和高
                window.SetPart(0, 0, height.I - 1, width.I - 1);                            // 设置窗口的显示区域，让它刚好能完整显示整张图片
                //window.ClearWindow();                                                     // 清除窗口上之前的内容
                window.DispObj(m_Ho_Image);                                                   // 在窗口上把图像画出来
            }

            catch (HalconException)
            {
                // 在快速刷新时，对象可能已被释放，忽略此错误
            }
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


        public bool IsContinuousGrabbing() => isContinuousGrabbing;



        // --- 内部方法，供参数窗口调用 ---
        public bool SetParameter(string paramName, object value)
        {
            // ... (这个方法保持原样，仅修改变量名)
            if (m_pAcqHandle == null) return false;
            try
            {
                HTuple halconValue = new HTuple(value);
                if (criticalParameters.Contains(paramName))
                {
                    bool wasContinuous = this.isContinuousGrabbing;
                    if (wasContinuous) StopContinuousGrab();
                    if (isGrabbing)
                    {
                        HOperatorSet.SetFramegrabberParam(m_pAcqHandle, "AcquisitionStop", "do_it");
                        isGrabbing = false;
                    }
                    HOperatorSet.SetFramegrabberParam(m_pAcqHandle, paramName, halconValue);
                    if (wasContinuous)
                    {
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
                MessageBox.Show($"[Halcon] 设置参数 '{paramName}' 失败: \n{hex.GetErrorMessage()}", "错误");
                return false;
            }
        }

        // 供参数窗口访问句柄
        public HTuple GetAcqHandle() => m_pAcqHandle;
    }

}