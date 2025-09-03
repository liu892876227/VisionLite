using System;
using System.Windows;
using VisionLite.Vision.UI.Windows;

namespace VisionLite.Vision
{
    /// <summary>
    /// 视觉算法工具启动器
    /// 用于启动独立的视觉算法测试工具
    /// </summary>
    public static class VisionToolLauncher
    {
        /// <summary>
        /// 启动视觉算法工具窗口
        /// </summary>
        public static void Launch()
        {
            try
            {
                var visionWindow = new VisionToolWindow();
                visionWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动视觉算法工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 启动视觉算法工具窗口（模态对话框）
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <returns>对话框结果</returns>
        public static bool? ShowDialog(Window owner = null)
        {
            try
            {
                var visionWindow = new VisionToolWindow();
                if (owner != null)
                {
                    visionWindow.Owner = owner;
                    visionWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                
                return visionWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动视觉算法工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}