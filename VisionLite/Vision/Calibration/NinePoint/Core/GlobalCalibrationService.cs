using System;
using System.Collections.Generic;
using System.Linq;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 标定状态变更事件参数
    /// </summary>
    public class CalibrationChangedEventArgs : EventArgs
    {
        public NinePointCalibrationData Calibration { get; set; }
        public bool IsActive { get; set; }
        
        public CalibrationChangedEventArgs(NinePointCalibrationData calibration, bool isActive)
        {
            Calibration = calibration;
            IsActive = isActive;
        }
    }
    
    /// <summary>
    /// 标定结果数据
    /// </summary>
    public class CalibrationResultData
    {
        /// <summary>图像坐标点列表</summary>
        public List<Point2D> ImageCoordinates { get; set; } = new List<Point2D>();
        
        /// <summary>物理坐标点列表</summary>
        public List<Point2D> WorldCoordinates { get; set; } = new List<Point2D>();
        
        /// <summary>标定名称</summary>
        public string CalibrationName { get; set; } = "";
        
        /// <summary>物理单位</summary>
        public PhysicalUnit Unit { get; set; } = PhysicalUnit.Millimeter;
        
        /// <summary>变换误差</summary>
        public double TransformError { get; set; }
        
        /// <summary>是否有有效数据</summary>
        public bool HasData => ImageCoordinates.Count > 0 && WorldCoordinates.Count > 0;
    }
    
    /// <summary>
    /// 全局标定服务单例
    /// 管理程序运行期间的活动标定配置
    /// </summary>
    public class GlobalCalibrationService
    {
        private static readonly Lazy<GlobalCalibrationService> _instance = 
            new Lazy<GlobalCalibrationService>(() => new GlobalCalibrationService());
        
        /// <summary>单例实例</summary>
        public static GlobalCalibrationService Instance => _instance.Value;
        
        private NinePointCalibrationData _activeCalibration;
        
        /// <summary>当前活动的标定配置</summary>
        public NinePointCalibrationData ActiveCalibration 
        { 
            get => _activeCalibration;
            private set
            {
                var oldCalibration = _activeCalibration;
                _activeCalibration = value;
                
                // 触发标定变更事件
                CalibrationChanged?.Invoke(this, new CalibrationChangedEventArgs(value, IsCalibrationActive));
                
                System.Diagnostics.Debug.WriteLine($"标定状态变更: {(IsCalibrationActive ? "已激活" : "已清除")} - {value?.Name ?? "无"}");
            }
        }
        
        /// <summary>是否有活动的标定配置</summary>
        public bool IsCalibrationActive => ActiveCalibration?.IsValid == true;
        
        /// <summary>标定状态描述</summary>
        public string CalibrationStatus => IsCalibrationActive 
            ? $"已应用标定: {ActiveCalibration.Name}" 
            : "未应用标定";
        
        /// <summary>标定变更事件</summary>
        public event EventHandler<CalibrationChangedEventArgs> CalibrationChanged;
        
        private GlobalCalibrationService()
        {
            // 私有构造函数确保单例
        }
        
        /// <summary>
        /// 应用标定配置
        /// </summary>
        /// <param name="calibration">要应用的标定配置</param>
        /// <returns>是否成功应用</returns>
        public bool ApplyCalibration(NinePointCalibrationData calibration)
        {
            if (calibration == null)
            {
                System.Diagnostics.Debug.WriteLine("应用标定失败: 标定配置为空");
                return false;
            }
            
            if (!calibration.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"应用标定失败: 标定配置无效 - {calibration.Name}");
                return false;
            }
            
            ActiveCalibration = calibration;
            System.Diagnostics.Debug.WriteLine($"标定应用成功: {calibration.Name}");
            return true;
        }
        
        /// <summary>
        /// 清除标定配置
        /// </summary>
        public void ClearCalibration()
        {
            ActiveCalibration = null;
            System.Diagnostics.Debug.WriteLine("已清除标定配置");
        }
        
        /// <summary>
        /// 将图像坐标转换为物理坐标
        /// </summary>
        /// <param name="imagePoint">图像坐标点</param>
        /// <returns>物理坐标点，如果转换失败返回原坐标</returns>
        public Point2D TransformImageToWorld(Point2D imagePoint)
        {
            if (!IsCalibrationActive)
            {
                System.Diagnostics.Debug.WriteLine("坐标变换失败: 无活动标定配置");
                return imagePoint; // 返回原坐标作为降级处理
            }
            
            try
            {
                return CalibrationTransform.ImageToWorld(imagePoint, ActiveCalibration.TransformMatrix);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"图像到物理坐标变换失败: {ex.Message}");
                return imagePoint; // 返回原坐标作为降级处理
            }
        }
        
        /// <summary>
        /// 将物理坐标转换为图像坐标
        /// </summary>
        /// <param name="worldPoint">物理坐标点</param>
        /// <returns>图像坐标点，如果转换失败返回原坐标</returns>
        public Point2D TransformWorldToImage(Point2D worldPoint)
        {
            if (!IsCalibrationActive)
            {
                System.Diagnostics.Debug.WriteLine("坐标变换失败: 无活动标定配置");
                return worldPoint; // 返回原坐标作为降级处理
            }
            
            try
            {
                return CalibrationTransform.WorldToImage(worldPoint, ActiveCalibration.InverseTransformMatrix);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"物理到图像坐标变换失败: {ex.Message}");
                return worldPoint; // 返回原坐标作为降级处理
            }
        }
        
        /// <summary>
        /// 批量转换图像坐标到物理坐标
        /// </summary>
        /// <param name="imagePoints">图像坐标点列表</param>
        /// <returns>物理坐标点列表</returns>
        public List<Point2D> TransformImageToWorldBatch(List<Point2D> imagePoints)
        {
            if (!IsCalibrationActive || imagePoints == null)
                return imagePoints?.ToList() ?? new List<Point2D>();
            
            try
            {
                return CalibrationTransform.TransformPoints(imagePoints, ActiveCalibration.TransformMatrix);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"批量坐标变换失败: {ex.Message}");
                return imagePoints.ToList(); // 返回原坐标作为降级处理
            }
        }
        
        /// <summary>
        /// 获取标定摘要信息
        /// </summary>
        /// <returns>标定摘要</returns>
        public string GetCalibrationSummary()
        {
            if (!IsCalibrationActive)
                return "无活动标定";
            
            return $"标定: {ActiveCalibration.Name}\n" +
                   $"误差: {ActiveCalibration.CalibrationError:F3} {ActiveCalibration.Unit}\n" +
                   $"点数: {ActiveCalibration.ValidPointCount}\n" +
                   $"时间: {ActiveCalibration.CalibrationTime:yyyy-MM-dd HH:mm}";
        }
    }
}