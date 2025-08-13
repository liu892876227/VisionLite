// RoiAdorner.cs
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace VisionLite
{
    /// <summary>
    /// 一个自定义的 Adorner，用于在视觉元素上方浮动显示ROI参数。
    /// </summary>
    public class RoiAdorner : Adorner
    {
        /// <summary>
        /// 用于显示参数文本的UI元素。
        /// </summary>
        private readonly TextBlock _textBlock;
        /// <summary>
        /// 包裹TextBlock的边框，用于提供背景、边框样式和圆角。
        /// </summary>
        private readonly Border _border;
        /// <summary>
        /// Adorner左上角在被装饰元素坐标系中的位置。
        /// </summary>
        private Point _position;

        /// <summary>
        /// 构造函数，初始化Adorner的视觉外观。
        /// </summary>
        /// <param name="adornedElement">需要被装饰的UI元素。</param>
        public RoiAdorner(UIElement adornedElement) : base(adornedElement)
        {
            // 创建用于显示的TextBlock
            _textBlock = new TextBlock
            {
                Foreground = Brushes.White,
                Padding = new Thickness(5),
                FontSize = 12,
                FontFamily = new FontFamily("Consolas")// 使用等宽字体，确保文本能完美对齐
            };

            // --- 创建一个Border来包裹TextBlock，以实现更丰富的视觉效果 ---
            _border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                BorderBrush = Brushes.DimGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = _textBlock
            };
            IsHitTestVisible = false; // 确保整个Adorner不拦截鼠标事件
            AddVisualChild(_border);
        }

        /// <summary>
        /// 公共接口方法，用于从外部更新Adorner显示的文本和位置。
        /// </summary>
        /// <param name="text">要显示的格式化参数文本。</param>
        /// <param name="position">文本框左上角在被装饰元素坐标系中的新位置。</param>
        public void Update(string text, Point position)
        {
            _textBlock.Text = text;
            _position = position;
            // 如果文本为空，则隐藏
            this.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            // 强制重新排列和渲染Adorner
            InvalidateMeasure();
            InvalidateArrange();
        }

        /// <summary>
        /// 重写WPF布局过程的“测量”阶段。
        /// 它的任务是计算并返回Adorner（即Border）需要多大的尺寸。
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            _border.Measure(constraint); // 让Border及其子元素（TextBlock）自己计算所需尺寸
            return _border.DesiredSize;  // 返回计算出的尺寸
        }

        /// <summary>
        /// 重写WPF布局过程的“排列”阶段。
        /// 它的任务是确定Adorner（即Border）在可用空间中的最终位置和大小。
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            // 将Border精确地放置在我们通过Update方法设置的_position位置，并使用其测量出的尺寸
            _border.Arrange(new Rect(_position, finalSize));
            return finalSize;// 返回最终占用的尺寸
        }

        /// <summary>
        /// 重写以告知WPF可视化树，我们的第一个（也是唯一一个）子元素是_border。
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            return _border;
        }
        /// <summary>
        /// 重写以告知WPF可视化树，我们的Adorner有一个子元素。
        /// </summary>
        protected override int VisualChildrenCount => 1;
    }
}