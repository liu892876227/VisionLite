using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 全局标定管理器
    /// 管理所有九点标定数据的存储、加载和状态维护
    /// </summary>
    public class GlobalCalibrationManager
    {
        private static readonly Lazy<GlobalCalibrationManager> _instance = 
            new Lazy<GlobalCalibrationManager>(() => new GlobalCalibrationManager());
        
        /// <summary>单例实例</summary>
        public static GlobalCalibrationManager Instance => _instance.Value;
        
        /// <summary>当前活动的标定数据</summary>
        public NinePointCalibrationData CurrentCalibration { get; private set; }
        
        /// <summary>所有已保存的标定配置</summary>
        public Dictionary<string, NinePointCalibrationData> SavedCalibrations { get; private set; }
        
        /// <summary>标定数据存储目录</summary>
        public string CalibrationDataPath { get; set; } = GetProjectRootCalibrationPath();
        
        /// <summary>当前标定状态</summary>
        public CalibrationStatus Status { get; private set; } = CalibrationStatus.NotCalibrated;
        
        /// <summary>标定状态变化事件</summary>
        public event EventHandler<CalibrationStatusChangedEventArgs> StatusChanged;
        
        /// <summary>标定数据更新事件</summary>
        public event EventHandler<CalibrationDataUpdatedEventArgs> CalibrationUpdated;
        
        /// <summary>标定列表变化事件</summary>
        public event EventHandler CalibrationListChanged;

        /// <summary>错误消息事件</summary>
        public event EventHandler<string> ErrorOccurred;
        
        private GlobalCalibrationManager()
        {
            SavedCalibrations = new Dictionary<string, NinePointCalibrationData>();
            EnsureDataDirectoryExists();
            LoadAllCalibrations();
        }
        
        /// <summary>
        /// 设置当前活动标定
        /// </summary>
        public bool SetCurrentCalibration(string calibrationName)
        {
            if (string.IsNullOrEmpty(calibrationName))
            {
                CurrentCalibration = null;
                UpdateStatus(CalibrationStatus.NotCalibrated);
                return false;
            }
            
            if (SavedCalibrations.ContainsKey(calibrationName))
            {
                CurrentCalibration = SavedCalibrations[calibrationName];
                UpdateStatus(CurrentCalibration.IsValid ? CalibrationStatus.Calibrated : CalibrationStatus.Invalid);
                CalibrationUpdated?.Invoke(this, new CalibrationDataUpdatedEventArgs(CurrentCalibration));
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 创建新的标定配置
        /// </summary>
        public NinePointCalibrationData CreateNewCalibration(string name, string description = "")
        {
            var calibration = new NinePointCalibrationData
            {
                Name = name,
                Description = description,
                CalibrationTime = DateTime.Now
            };
            
            CurrentCalibration = calibration;
            SavedCalibrations[name] = calibration;
            UpdateStatus(CalibrationStatus.Calibrating);
            
            CalibrationListChanged?.Invoke(this, EventArgs.Empty);
            CalibrationUpdated?.Invoke(this, new CalibrationDataUpdatedEventArgs(calibration));
            
            return calibration;
        }
        
        /// <summary>
        /// 删除标定配置
        /// </summary>
        public bool DeleteCalibration(string name)
        {
            if (!SavedCalibrations.ContainsKey(name))
                return false;
                
            SavedCalibrations.Remove(name);
            
            // 删除对应的文件
            var filePath = GetCalibrationFilePath(name);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"删除标定文件失败: {ex.Message}");
                }
            }
            
            // 如果删除的是当前标定，清空当前标定
            if (CurrentCalibration?.Name == name)
            {
                CurrentCalibration = null;
                UpdateStatus(CalibrationStatus.NotCalibrated);
            }
            
            CalibrationListChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        
        /// <summary>
        /// 保存标定数据
        /// </summary>
        public async Task<bool> SaveCalibrationAsync(NinePointCalibrationData calibration)
        {
            if (calibration == null || string.IsNullOrEmpty(calibration.Name))
            {
                return false;
            }
                
            try
            {
                // 确保目录存在
                EnsureDataDirectoryExists();
                
                var filePath = GetCalibrationFilePath(calibration.Name);
                
                var json = JsonConvert.SerializeObject(calibration, Formatting.Indented);
                
                await Task.Run(() => File.WriteAllText(filePath, json));
                
                // 验证文件是否真的创建了
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                }
                else
                {
                }
                
                SavedCalibrations[calibration.Name] = calibration;
                CalibrationListChanged?.Invoke(this, EventArgs.Empty);
                
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"保存标定数据失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 加载标定数据
        /// </summary>
        public async Task<NinePointCalibrationData> LoadCalibrationAsync(string name)
        {
            try
            {
                var filePath = GetCalibrationFilePath(name);
                if (!File.Exists(filePath))
                    return null;
                    
                var json = await Task.Run(() => File.ReadAllText(filePath));
                var calibration = JsonConvert.DeserializeObject<NinePointCalibrationData>(json);
                
                if (calibration != null)
                {
                    SavedCalibrations[name] = calibration;
                }
                
                return calibration;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"加载标定数据失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 加载所有标定配置
        /// </summary>
        public async Task LoadAllCalibrationsAsync()
        {
            await Task.Run(() => LoadAllCalibrations());
        }
        
        /// <summary>
        /// 获取标定列表
        /// </summary>
        public List<string> GetCalibrationNames()
        {
            return SavedCalibrations.Keys.ToList();
        }
        
        /// <summary>
        /// 验证标定是否有效
        /// </summary>
        public bool IsCalibrationValid()
        {
            return CurrentCalibration?.IsValid == true && Status == CalibrationStatus.Calibrated;
        }
        
        /// <summary>
        /// 图像坐标转世界坐标
        /// </summary>
        public Point2D ImageToWorld(Point2D imagePoint)
        {
            if (!IsCalibrationValid())
                return new Point2D();
                
            return TransformCalculator.ImageToWorld(imagePoint, CurrentCalibration.TransformMatrix);
        }
        
        /// <summary>
        /// 世界坐标转图像坐标
        /// </summary>
        public Point2D WorldToImage(Point2D worldPoint)
        {
            if (!IsCalibrationValid())
                return new Point2D();
                
            return TransformCalculator.WorldToImage(worldPoint, CurrentCalibration.InverseTransformMatrix);
        }
        
        /// <summary>
        /// 获取当前标定信息摘要
        /// </summary>
        public CalibrationSummary GetCurrentCalibrationSummary()
        {
            if (CurrentCalibration == null)
                return new CalibrationSummary { Status = Status };
                
            return new CalibrationSummary
            {
                Name = CurrentCalibration.Name,
                Status = Status,
                CalibrationTime = CurrentCalibration.CalibrationTime,
                ValidPointCount = CurrentCalibration.ValidPointCount,
                CalibrationError = CurrentCalibration.CalibrationError,
                Quality = CurrentCalibration.Quality,
                Unit = CurrentCalibration.Unit,
                TransformType = CurrentCalibration.TransformType
            };
        }
        
        #region 私有方法
        
        /// <summary>
        /// 获取项目根目录的CalibrationConfig路径
        /// </summary>
        private static string GetProjectRootCalibrationPath()
        {
            try
            {
                // 从可执行文件目录往上找到项目根目录
                // exe路径: E:\mySoftware\VisionLite\VisionLite\bin\x64\Debug\VisionLite.exe
                // 项目根: E:\mySoftware\VisionLite
                var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // 往上四层：Debug -> x64 -> bin -> VisionLite(项目) -> VisionLite(解决方案根)
                var projectRoot = Directory.GetParent(exeDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
                
                if (!string.IsNullOrEmpty(projectRoot) && Directory.Exists(projectRoot))
                {
                    var calibrationPath = Path.Combine(projectRoot, "CalibrationConfig");
                    return calibrationPath;
                }
                else
                {
                    // 备选方案：使用相对路径
                    var relativePath = Path.Combine(exeDirectory, "..", "..", "..", "..", "CalibrationConfig");
                    return Path.GetFullPath(relativePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VisionLite] 获取项目根目录标定路径失败: {ex.Message}");
                // 静态方法中无法触发事件，错误将在调用方处理
                // 最后的备选方案：使用当前目录
                return Path.Combine(Directory.GetCurrentDirectory(), "CalibrationConfig");
            }
        }
        
        private void EnsureDataDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(CalibrationDataPath))
                {
                    Directory.CreateDirectory(CalibrationDataPath);
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"创建标定配置目录失败: {ex.Message}");
                // 如果创建失败，回退到临时目录
                CalibrationDataPath = Path.Combine(Path.GetTempPath(), "VisionLite", "CalibrationConfig");
                try
                {
                    Directory.CreateDirectory(CalibrationDataPath);
                }
                catch
                {
                    // 创建临时目录失败，忽略错误
                }
            }
        }
        
        private void LoadAllCalibrations()
        {
            try
            {
                if (!Directory.Exists(CalibrationDataPath))
                    return;
                    
                var jsonFiles = Directory.GetFiles(CalibrationDataPath, "*.json");
                
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var calibration = JsonConvert.DeserializeObject<NinePointCalibrationData>(json);
                        
                        if (calibration != null && !string.IsNullOrEmpty(calibration.Name))
                        {
                            SavedCalibrations[calibration.Name] = calibration;
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, $"加载标定文件失败 {System.IO.Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"加载标定目录失败: {ex.Message}");
            }
        }
        
        private string GetCalibrationFilePath(string name)
        {
            var fileName = $"{name}.json";
            var fullPath = Path.Combine(CalibrationDataPath, fileName);
            return fullPath;
        }
        
        private void UpdateStatus(CalibrationStatus newStatus)
        {
            if (Status != newStatus)
            {
                var oldStatus = Status;
                Status = newStatus;
                StatusChanged?.Invoke(this, new CalibrationStatusChangedEventArgs(oldStatus, newStatus));
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 标定状态变化事件参数
    /// </summary>
    public class CalibrationStatusChangedEventArgs : EventArgs
    {
        public CalibrationStatus OldStatus { get; }
        public CalibrationStatus NewStatus { get; }
        
        public CalibrationStatusChangedEventArgs(CalibrationStatus oldStatus, CalibrationStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }
    
    /// <summary>
    /// 标定数据更新事件参数
    /// </summary>
    public class CalibrationDataUpdatedEventArgs : EventArgs
    {
        public NinePointCalibrationData CalibrationData { get; }
        
        public CalibrationDataUpdatedEventArgs(NinePointCalibrationData calibrationData)
        {
            CalibrationData = calibrationData;
        }
    }
    
    /// <summary>
    /// 标定摘要信息
    /// </summary>
    public class CalibrationSummary
    {
        public string Name { get; set; }
        public CalibrationStatus Status { get; set; }
        public DateTime CalibrationTime { get; set; }
        public int ValidPointCount { get; set; }
        public double CalibrationError { get; set; }
        public CalibrationQuality Quality { get; set; }
        public PhysicalUnit Unit { get; set; }
        public string TransformType { get; set; }
    }
}