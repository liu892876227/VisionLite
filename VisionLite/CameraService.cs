using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HalconDotNet;

namespace VisionLite
{
    public class CameraService : IDisposable
    {
        private HTuple _acqHandle;
        private CancellationTokenSource _cts;
        private Task _grabbingTask;
        public bool IsGrabbing { get; private set; } = false;

        // 当有新图像时触发的事件
        public event Action<HImage> NewImageAvailable;

        
        public List<string> DiscoverDevices()
        {
            var deviceList = new List<string>();
            try
            {
                HOperatorSet.InfoFramegrabber("GenICamTL", "device", out HTuple information, out HTuple values);
                if (values != null && values.Length > 0)
                {
                    foreach (var val in values.SArr)
                    {
                        deviceList.Add(val);
                    }
                }
            }
            catch (HOperatorException ex)
            {
                Console.WriteLine($"Error discovering devices: {ex.GetErrorMessage()}");
            }
            return deviceList;
        }
        public void OpenDevice(string deviceIdentifier)
        {
            if (_acqHandle != null)
            {
                CloseDevice();
            }
            try
            {
                HOperatorSet.OpenFramegrabber(
                    "GenICamTL", 0, 0, 0, 0, "default", "default", "default", "progressive", -1, "default", "default",
                    "default", deviceIdentifier, 0, -1, out _acqHandle);
            }
            catch (HOperatorException ex)
            {
                _acqHandle = null;
                throw new Exception($"Failed to open device '{deviceIdentifier}'. HALCON error: {ex.GetErrorMessage()}");
            }
        }


        // 开始连续采集 (已修正)
        public void StartGrabbing()
        {
            if (_acqHandle == null || IsGrabbing) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsGrabbing = true;

            _grabbingTask = Task.Run(() =>
            {
                HOperatorSet.GrabImageStart(_acqHandle, -1);
                while (!token.IsCancellationRequested)
                {
                    HObject ho_Image = null; // 声明一个HObject变量
                    try
                    {
                        // 1. 使用 HObject 接收图像
                        HOperatorSet.GrabImageAsync(out ho_Image, _acqHandle, -1);

                        // 2. 用 HObject 构造 HImage
                        HImage image = new HImage(ho_Image);

                        // 3. 触发事件，将新的 HImage 实例传递出去
                        NewImageAvailable?.Invoke(image);

                        // 注意：此时 image 对象的生命周期已经交给事件的订阅者(ViewModel)管理
                        // ViewModel 会负责显示和最终的 Dispose。
                    }
                    catch (HOperatorException ex)
                    {
                        // 采集超时(5322)是常见情况，可以忽略或记录日志，其他错误可能需要停止
                        if (ex.GetErrorCode() != 5322)
                        {
                            Console.WriteLine($"Grabbing error: {ex.Message}");
                            break;
                        }
                    }
                    finally
                    {
                        // 4. 无论是否成功，都必须释放临时的 HObject 句柄，防止内存泄漏！
                        ho_Image?.Dispose();
                    }
                }
                IsGrabbing = false;
            }, token);
        }

        
        public void StopGrabbing()
        {
            if (!IsGrabbing) return;
            _cts?.Cancel();
            try
            {
                // 添加一个小的超时，以防任务卡住
                _grabbingTask?.Wait(2000);
            }
            catch (Exception ex)
            {

                
                Console.WriteLine($"Grabbing error: {ex.Message}");
            }
        }
        public void CloseDevice()
        {
            if (IsGrabbing)
            {
                StopGrabbing();
            }
            if (_acqHandle != null)
            {
                HOperatorSet.CloseFramegrabber(_acqHandle);
                _acqHandle = null;
            }
        }
        public void Dispose()
        {
            CloseDevice();
            _cts?.Dispose();
        }
    }
    }
