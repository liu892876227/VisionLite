using System;
using System.Windows;

namespace VisionLite.Vision.Calibration.NinePoint.UI
{
    /// <summary>
    /// 标定名称输入对话框
    /// </summary>
    public partial class CalibrationNameDialog : Window
    {
        /// <summary>标定名称</summary>
        public string CalibrationName
        {
            get => txtCalibrationName.Text;
            set => txtCalibrationName.Text = value;
        }
        
        /// <summary>描述信息</summary>
        public string Description
        {
            get => txtDescription.Text;
            set => txtDescription.Text = value;
        }
        
        public CalibrationNameDialog()
        {
            InitializeComponent();
            
            // 设置默认名称
            CalibrationName = $"标定_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            Loaded += (s, e) => txtCalibrationName.Focus();
        }
        
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CalibrationName))
            {
                MessageBox.Show("请输入标定名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCalibrationName.Focus();
                return;
            }
            
            DialogResult = true;
            Close();
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}