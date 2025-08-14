// InfoWindowAdorner.cs
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VisionLite
{
    /// <summary>
    /// 一个自定义Adorner，用于在图像窗口的左下角显示状态信息。
    /// </summary>
    public class InfoWindowAdorner : Adorner
    {
       
        private string _textToDisplay = "";

        public InfoWindowAdorner(UIElement adornedElement) : base(adornedElement)
        {
            // 这个Adorner不应该影响鼠标交互
            IsHitTestVisible = false;
        }

        /// <summary>
        /// 更新Adorner显示的内容。
        /// </summary>
        public void Update(WindowInfo info)
        {
            if (info == null)
            {
                _textToDisplay = "";
            }
            else
            {
                _textToDisplay = $"{info.SourceName} | {info.Resolution} | {info.ZoomFactor} | {info.MouseCoordinates} | {info.PixelValue}";
            }

            // 强制WPF重新渲染这个Adorner
            InvalidateVisual();
        }

        /// <summary>
        /// 重写OnRender方法，这是WPF要求我们绘制Adorner内容的地方。
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (string.IsNullOrEmpty(_textToDisplay)) return;

            // FormattedText 对象在创建后其文本是不可变的。
            // 因此，我们必须在每次渲染时根据最新的 _textToDisplay 字符串创建一个新的 FormattedText 实例。
            FormattedText formattedText = new FormattedText(
                _textToDisplay, // 使用最新的文本
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), // 选择一个清晰的字体
                11, // 字体大小
                Brushes.White, // 字体颜色
                1.25 // PixelsPerDip (对应于 120 DPI)
            );

            // 在文本后面绘制一个半透明的黑色背景，以增加可读性
            Rect backgroundRect = new Rect(
                5, // 左边距
                this.AdornedElement.RenderSize.Height - formattedText.Height - 5, // Y坐标，位于底部并有边距
                formattedText.Width + 10, // 背景宽度，比文本宽一点
                formattedText.Height + 4  // 背景高度，比文本高一点
            );

            // 圆角背景
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), null, backgroundRect, 2, 2);

            // 绘制文本
            drawingContext.DrawText(formattedText, new Point(backgroundRect.X + 5, backgroundRect.Y + 2));
        }
    }
}
