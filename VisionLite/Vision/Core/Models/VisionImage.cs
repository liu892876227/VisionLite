using System;
using HalconDotNet;

namespace VisionLite.Vision.Core.Models
{
    /// <summary>
    /// 视觉图像封装类
    /// 封装Halcon HObject，提供统一的图像操作接口
    /// </summary>
    public class VisionImage : IDisposable
    {
        private bool _disposed = false;
        
        /// <summary>
        /// Halcon图像对象
        /// </summary>
        public HObject HImage { get; private set; }
        
        /// <summary>
        /// 图像宽度
        /// </summary>
        public int Width { get; private set; }
        
        /// <summary>
        /// 图像高度
        /// </summary>
        public int Height { get; private set; }
        
        /// <summary>
        /// 图像通道数
        /// </summary>
        public int Channels { get; private set; }
        
        /// <summary>
        /// 图像文件路径（如果是从文件加载）
        /// </summary>
        public string ImagePath { get; set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; private set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="hImage">Halcon图像对象</param>
        public VisionImage(HObject hImage)
        {
            if (hImage == null)
                throw new ArgumentNullException(nameof(hImage));
                
            HImage = hImage;
            CreateTime = DateTime.Now;
            
            // 获取图像信息
            UpdateImageInfo();
        }
        
        /// <summary>
        /// 从文件路径创建图像
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        /// <returns>VisionImage对象</returns>
        public static VisionImage FromFile(string imagePath)
        {
            try
            {
                HOperatorSet.ReadImage(out HObject image, imagePath);
                var visionImage = new VisionImage(image)
                {
                    ImagePath = imagePath
                };
                return visionImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法加载图像文件: {imagePath}", ex);
            }
        }
        
        /// <summary>
        /// 克隆图像
        /// </summary>
        /// <returns>新的VisionImage对象</returns>
        public VisionImage Clone()
        {
            HOperatorSet.CopyImage(HImage, out HObject clonedImage);
            return new VisionImage(clonedImage)
            {
                ImagePath = this.ImagePath
            };
        }
        
        /// <summary>
        /// 更新图像信息
        /// </summary>
        private void UpdateImageInfo()
        {
            try
            {
                // 获取图像尺寸
                HOperatorSet.GetImageSize(HImage, out HTuple width, out HTuple height);
                Width = width.I;
                Height = height.I;
                
                // 获取通道数
                HOperatorSet.CountChannels(HImage, out HTuple channels);
                Channels = channels.I;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("无法获取图像信息", ex);
            }
        }
        
        /// <summary>
        /// 保存图像到文件
        /// </summary>
        /// <param name="filePath">保存路径</param>
        public void SaveToFile(string filePath)
        {
            try
            {
                string fileExtension = System.IO.Path.GetExtension(filePath).ToLower();
                string format = fileExtension switch
                {
                    ".bmp" => "bmp",
                    ".jpg" or ".jpeg" => "jpeg",
                    ".png" => "png",
                    ".tif" or ".tiff" => "tiff",
                    _ => "bmp" // 默认格式
                };
                
                HOperatorSet.WriteImage(HImage, format, 0, filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法保存图像到: {filePath}", ex);
            }
        }
        
        /// <summary>
        /// 获取图像基本信息字符串
        /// </summary>
        /// <returns>图像信息</returns>
        public override string ToString()
        {
            return $"图像 [{Width}x{Height}x{Channels}] - {CreateTime:yyyy-MM-dd HH:mm:ss}";
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源的实际实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放Halcon对象
                    HImage?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~VisionImage()
        {
            Dispose(false);
        }
    }
}