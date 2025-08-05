using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HalconDotNet;
using VisionLite.ROIs;

namespace VisionLite
{
    /// <summary>
    /// RoiToolWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RoiToolWindow : Window
    {
        private HObject SourceImage;    // 持有主窗口的完整图像
        private RoiBase AssociatedRoi;  // 持有在主窗口上绘制的那个ROI对象

        /// <summary>
        /// 当ROI形状需要被替换时触发的事件。
        /// 它会向外传递一个新创建的ROI对象。
        /// </summary>
        public event Action<RoiBase> OnRoiShapeChanged;

        public RoiToolWindow(HObject sourceImage, RoiBase roi)
        {
            InitializeComponent();

            this.SourceImage = sourceImage;
            this.AssociatedRoi = roi;

            // 默认选中 "矩形"
            RoiShapeComboBox.SelectedIndex = 0;

            // 订阅窗口的 Loaded 事件，在事件处理器中调用 UpdatePreview()
            this.Loaded += RoiToolWindow_Loaded;
        }

        // --- 新增代码开始 ---
        /// <summary>
        /// 当窗口及其所有内容都加载完成后，此事件会被触发。
        /// 这是执行依赖于UI控件内部状态的操作的最佳时机。
        /// </summary>
        private void RoiToolWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 在这里调用，可以确保 RoiPreviewWindow.HalconWindow 已经被初始化
            UpdatePreview();
        }

        //public void UpdatePreview()
        //{
        //    if (SourceImage == null || !SourceImage.IsInitialized() || AssociatedRoi == null || !this.IsLoaded)
        //    {
        //        return;
        //    }

        //    // 清空预览窗口
        //    RoiPreviewWindow.HalconWindow.ClearWindow();

        //    try
        //    {
        //        // 这是一个通用的方法，但对于不同ROI类型，需要不同处理
        //        // 这里我们先只实现矩形的情况
        //        if (AssociatedRoi is RectangleRoi rectRoi)
        //        {
        //            // 使用ReduceDomain来获取ROI区域的图像
        //            HOperatorSet.ReduceDomain(SourceImage, rectRoi.GetRegion(), out HObject imageReduced);


        //            // 在预览窗口中显示裁剪后的图像
        //            HWindow preview = RoiPreviewWindow.HalconWindow;
        //            preview.SetPart(rectRoi.Row1, rectRoi.Column1, rectRoi.Row2, rectRoi.Column2); // 设置显示区域
        //            preview.DispObj(imageReduced);

        //            imageReduced.Dispose(); // 释放临时对象
        //        }
        //        // 在这里可以为其他ROI类型添加预览逻辑
        //        // else if (AssociatedRoi is CircleRoi circleRoi) { ... }
        //    }
        //    catch (HalconException)
        //    {
        //        // 忽略错误，例如ROI超出图像范围时
        //    }
        //}

        // In RoiToolWindow.xaml.cs

        // In RoiToolWindow.xaml.cs

        // In RoiToolWindow.xaml.cs

        public void UpdatePreview()
        {
            // 检查输入数据是否有效
            if (SourceImage == null || !SourceImage.IsInitialized() || AssociatedRoi == null || !this.IsLoaded)
            {
                return;
            }

            HWindow preview = RoiPreviewWindow.HalconWindow;
            if (preview == null) return;

            preview.ClearWindow();

            try
            {
                if (AssociatedRoi is RectangleRoi rectRoi)
                {
                    // --- 使用最直接的 CropRectangle1 算子 ---

                    // 步骤 1: 直接从原始图像(SourceImage)中，根据ROI的四个坐标，
                    //         裁剪出一个全新的、独立的小图像(croppedImage)。
                    //         这个方法不需要任何 ReduceDomain 的预处理。
                    HOperatorSet.CropRectangle1(SourceImage, out HObject croppedImage,
                                                rectRoi.Row1, rectRoi.Column1, rectRoi.Row2, rectRoi.Column2);

                    // 步骤 2: 获取这个新裁剪出的小图像的尺寸。
                    HOperatorSet.GetImageSize(croppedImage, out HTuple width, out HTuple height);

                    // 步骤 3: 设置预览窗口的视野(SetPart)以完美匹配这个小图像。
                    //         因为小图像的坐标总是从(0,0)开始，所以SetPart也从(0,0)开始。
                    preview.SetPart(0, 0, height.I - 1, width.I - 1);

                    // 步骤 4: 显示这张干净、独立的小图像。
                    preview.DispObj(croppedImage);

                    // 步骤 5: 释放临时的裁剪图像，防止内存泄漏。
                    croppedImage.Dispose();
                }
            }
            catch (HalconException ex)
            {
                // 如果这里还出错，说明ROI的坐标可能超出了图像范围。
                // 可以在这里加一个消息框来查看具体的错误信息。
                // MessageBox.Show("Halcon Error in UpdatePreview: " + ex.GetErrorMessage());
            }
        }
        /// <summary>
        /// 当用户在下拉框中选择新的ROI形状时调用。
        /// </summary>
        private void RoiShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 检查窗口是否已加载，事件是否被订阅，防止在初始化阶段或无效状态下触发
            if (!this.IsLoaded || OnRoiShapeChanged == null) return;

            RoiBase newRoi = null;
            var selectedItem = (ComboBoxItem)RoiShapeComboBox.SelectedItem;
            if (selectedItem == null) return;

            // 获取当前ROI的中心点，以便新ROI能出现在大致相同的位置
            double centerX = 0, centerY = 0;
            if (AssociatedRoi is RectangleRoi rect)
            {
                centerY = (rect.Row1 + rect.Row2) / 2;
                centerX = (rect.Column1 + rect.Column2) / 2;
            }
            // 你可以为其他ROI类型也添加获取中心点的逻辑

            switch (selectedItem.Content.ToString())
            {
                case "矩形":
                    // 创建一个新的默认大小的矩形
                    newRoi = new RectangleRoi { Row1 = centerY - 400, Column1 = centerX - 400, Row2 = centerY + 400, Column2 = centerX + 400 };
                    break;
                case "圆形":
                    MessageBox.Show("圆形ROI功能尚未实现！", "提示");
                    // 这里是未来实现圆形ROI的地方
                    // newRoi = new CircleRoi { CenterRow = centerY, CenterColumn = centerX, Radius = 100 };
                    break;
                    // 其他 case...
            }

            if (newRoi != null)
            {
                // 更新本窗口关联的ROI对象
                this.AssociatedRoi = newRoi;
                // 触发事件，将这个新创建的ROI对象“广播”出去，通知主窗口进行更换
                OnRoiShapeChanged.Invoke(newRoi);
                // 用新的ROI信息更新自己的预览窗口
                UpdatePreview();
            }
        }


    }
}
