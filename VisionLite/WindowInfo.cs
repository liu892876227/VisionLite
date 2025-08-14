// WindowInfo.cs
using System.ComponentModel;
using System.Windows;
using System.Runtime.CompilerServices;

namespace VisionLite
{
    /// <summary>
    /// 一个数据模型，用于封装单个图像显示窗口的状态信息。
    /// 实现INotifyPropertyChanged接口，以便未来可以轻松地与WPF数据绑定集成。
    /// </summary>
    public class WindowInfo : INotifyPropertyChanged
    {
        private string _sourceName = "空闲";
        private string _resolution = "N/A";
        private string _zoomFactor = "100%";
        // 存储原始图像尺寸，用于精确计算缩放

        public Size OriginalImageSize { get; set; } = new Size(0, 0);
        public string SourceName
        {
            get => _sourceName;
            set { _sourceName = value; OnPropertyChanged(); }
        }

        public string Resolution
        {
            get => _resolution;
            set { _resolution = value; OnPropertyChanged(); }
        }

        public string ZoomFactor
        {
            get => _zoomFactor;
            set { _zoomFactor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}