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
    /// 封装单个相机设备的所有信息和操作
    /// </summary>
    public class CameraDevice
    {
        public string DeviceID { get; private set; } // 相机的唯一ID
        public HTuple AcqHandle { get; private set; } // 相机的句柄
        public HObject Ho_Image { get; set; } // 用于存储该相机捕获的图像
        public HSmartWindowControlWPF DisplayWindow { get; private set; } // 绑定的显示窗口

        private bool isGrabbing = false; // 标记相机是否已开始异步采集

        // 用于控制连续采集的成员变量
        private Thread continuousGrabThread; // 用于连续采集的后台线程
        private volatile bool isContinuousGrabbing = false;

        // 确保有这个公共方法，让外部可以查询状态
        public bool IsContinuousGrabbing()
        {
            return isContinuousGrabbing;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CameraDevice(string deviceId, HSmartWindowControlWPF window)
        {
            this.DeviceID = deviceId;
            this.DisplayWindow = window;

            // 为了解决C#对out参数的限制，使用临时变量进行传递
            HOperatorSet.GenEmptyObj(out HObject tempImage);
            this.Ho_Image = tempImage;
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
                    out HTuple handle);     //输出参数，返回打开的图像采集设备的句柄。
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

                HOperatorSet.GrabImageAsync(out HObject tempImage, AcqHandle, 5000);
                this.Ho_Image = tempImage;

                // 在自己的窗口中显示
                Display();
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"从设备 {DeviceID} 采集图像失败: \n{ex.GetErrorMessage()}", "错误");
            }
        }

        // 开始连续采集的方法
        public void StartContinuousGrab()
        {
            if (AcqHandle == null || !isGrabbing || isContinuousGrabbing)
            {
                // 如果相机未打开、未开始异步采集、或者已经正在连续采集中，则不做任何事
                return;
            }

            // 打开连续采集的开关
            isContinuousGrabbing = true;

            // 创建并启动一个新的后台线程，让它去执行 ContinuousGrabLoop 方法
            continuousGrabThread = new Thread(ContinuousGrabLoop);
            continuousGrabThread.IsBackground = true; // 设置为后台线程，这样主程序退出时它会自动结束
            continuousGrabThread.Start();
        }

        // 停止连续采集的方法
        public void StopContinuousGrab()
        {
            // 关闭连续采集的开关
            isContinuousGrabbing = false;

            // 等待后台线程自己结束
            continuousGrabThread?.Join(200); // 最多等待200毫秒
        }

        // 后台线程真正执行的循环体
        private void ContinuousGrabLoop()
        {
            HObject tempImage = new HObject(); // 在循环外创建临时图像变量，提高效率

            // 只要开关是开着的，就一直循环
            while (isContinuousGrabbing)
            {
                try
                {
                    // 和单次采集一样，先发触发命令，再异步获取图像
                    HOperatorSet.SetFramegrabberParam(AcqHandle, "TriggerSoftware", "do_it");
                    HOperatorSet.GrabImageAsync(out tempImage, AcqHandle, 1000); // 超时设短一点

                    // 在UI主线程上更新显示
                    DisplayWindow.Dispatcher.Invoke(() =>
                    {
                        Ho_Image?.Dispose();
                        Ho_Image = tempImage.CopyObj(1, -1); // 必须复制一份图像，因为tempImage马上会被下一次循环覆盖
                        Display();
                    });
                }
                catch (HalconException ex)
                {
                    // 如果在连续采集中发生超时等错误，不弹窗，而是在控制台输出
                    // 避免大量的弹窗卡死程序
                    if (ex.GetErrorCode() == 5322) // 如果是超时错误
                    {
                        Console.WriteLine($"设备 {DeviceID} 连续采集超时...");
                    }
                    // 遇到错误可以稍微暂停一下，避免CPU空转
                    Thread.Sleep(50);
                }
            }
            tempImage.Dispose(); // 循环结束后，释放临时图像变量
        }

        /// <summary>
        /// 在绑定的窗口中显示图像
        /// </summary>
        private void Display()
        {
            if (Ho_Image == null || !Ho_Image.IsInitialized()) return;

            HWindow window = DisplayWindow.HalconWindow;                                // 从WPF控件中获取到真正的HALCON窗口对象
            HOperatorSet.GetImageSize(Ho_Image, out HTuple width, out HTuple height);   // 获取图像的宽和高
            window.SetPart(0, 0, height.I - 1, width.I - 1);                            // 设置窗口的显示区域，让它刚好能完整显示整张图片
            window.ClearWindow();                                                       // 清除窗口上之前的内容
            window.DispObj(Ho_Image);                                                   // 在窗口上把图像画出来

        }

        /// <summary>
        /// 关闭相机并释放资源
        /// </summary>
        public void Close()
        {

            // 确保在关闭相机前，先停止连续采集
            StopContinuousGrab(); // 确保后台线程已停止

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

