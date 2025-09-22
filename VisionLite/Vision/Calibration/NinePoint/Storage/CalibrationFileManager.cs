using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VisionLite.Vision.Calibration.NinePoint.Core;

namespace VisionLite.Vision.Calibration.NinePoint.Storage
{
    /// <summary>
    /// 标定文件管理器
    /// 负责标定数据的文件存储、导入导出和备份功能
    /// </summary>
    public class CalibrationFileManager
    {
        /// <summary>默认标定数据扩展名</summary>
        public static readonly string CalibrationFileExtension = ".vl9pc";
        
        /// <summary>备份文件扩展名</summary>
        public static readonly string BackupFileExtension = ".backup";
        
        /// <summary>JSON格式扩展名</summary>
        public static readonly string JsonFileExtension = ".json";
        
        /// <summary>
        /// 导出标定数据到文件
        /// </summary>
        public static async Task<bool> ExportCalibrationAsync(NinePointCalibrationData calibration, string filePath)
        {
            if (calibration == null || string.IsNullOrEmpty(filePath))
                return false;
                
            try
            {
                var exportData = new CalibrationExportData
                {
                    CalibrationData = calibration,
                    ExportTime = DateTime.Now,
                    ExportVersion = "1.0",
                    ApplicationName = "VisionLite",
                    Checksum = CalculateChecksum(calibration)
                };
                
                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(filePath, json));

                return true;
            }
            catch (Exception ex)
            {
                // 文件导出失败（重要错误）
                Console.WriteLine($"[VisionLite] 标定数据导出失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 从文件导入标定数据
        /// </summary>
        public static async Task<NinePointCalibrationData> ImportCalibrationAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;
                
            try
            {
                var json = await Task.Run(() => File.ReadAllText(filePath));
                
                // 尝试作为导出格式解析
                try
                {
                    var exportData = JsonConvert.DeserializeObject<CalibrationExportData>(json);
                    if (exportData?.CalibrationData != null && ValidateChecksum(exportData))
                    {
                        return exportData.CalibrationData;
                    }
                }
                catch
                {
                    // 如果导出格式解析失败，尝试直接解析为标定数据
                }
                
                // 尝试直接解析为标定数据
                var calibration = JsonConvert.DeserializeObject<NinePointCalibrationData>(json);
                return calibration;
            }
            catch (Exception ex)
            {
                // 文件导入失败（重要错误）
                Console.WriteLine($"[VisionLite] 标定数据导入失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 创建标定数据备份
        /// </summary>
        public static async Task<bool> CreateBackupAsync(NinePointCalibrationData calibration, string backupDirectory)
        {
            if (calibration == null || string.IsNullOrEmpty(backupDirectory))
                return false;
                
            try
            {
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{calibration.Name}_{timestamp}{BackupFileExtension}";
                var backupFilePath = Path.Combine(backupDirectory, backupFileName);
                
                return await ExportCalibrationAsync(calibration, backupFilePath);
            }
            catch (Exception ex)
            {
                // 备份创建失败（重要错误）
                Console.WriteLine($"[VisionLite] 创建标定备份失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 恢复指定的备份文件
        /// </summary>
        public static async Task<NinePointCalibrationData> RestoreBackupAsync(string backupFilePath)
        {
            return await ImportCalibrationAsync(backupFilePath);
        }
        
        /// <summary>
        /// 获取指定目录下的所有备份文件
        /// </summary>
        public static List<BackupFileInfo> GetBackupFiles(string backupDirectory)
        {
            var backupFiles = new List<BackupFileInfo>();
            
            try
            {
                if (!Directory.Exists(backupDirectory))
                    return backupFiles;
                    
                var files = Directory.GetFiles(backupDirectory, $"*{BackupFileExtension}");
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var backupInfo = new BackupFileInfo
                    {
                        FilePath = file,
                        FileName = fileInfo.Name,
                        CreatedTime = fileInfo.CreationTime,
                        Size = fileInfo.Length
                    };
                    
                    // 尝试从文件名解析标定名称和时间戳
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    var parts = nameWithoutExt.Split('_');
                    if (parts.Length >= 2)
                    {
                        backupInfo.CalibrationName = string.Join("_", parts, 0, parts.Length - 1);
                    }
                    
                    backupFiles.Add(backupInfo);
                }
                
                // 按创建时间降序排列
                backupFiles.Sort((a, b) => b.CreatedTime.CompareTo(a.CreatedTime));
            }
            catch
            {
                // 获取备份文件列表失败（不重要，使用匿名catch）
            }
            
            return backupFiles;
        }
        
        /// <summary>
        /// 清理过期的备份文件
        /// </summary>
        public static async Task<int> CleanupOldBackupsAsync(string backupDirectory, int maxBackupsPerCalibration = 10, int maxDaysToKeep = 30)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(backupDirectory))
                        return 0;
                        
                    var backupFiles = GetBackupFiles(backupDirectory);
                    var cutoffDate = DateTime.Now.AddDays(-maxDaysToKeep);
                    var deletedCount = 0;
                    
                    // 按标定名称分组
                    var groupedBackups = new Dictionary<string, List<BackupFileInfo>>();
                    foreach (var backup in backupFiles)
                    {
                        var key = backup.CalibrationName ?? "Unknown";
                        if (!groupedBackups.ContainsKey(key))
                            groupedBackups[key] = new List<BackupFileInfo>();
                        groupedBackups[key].Add(backup);
                    }
                    
                    // 清理每个标定的过期备份
                    foreach (var group in groupedBackups)
                    {
                        var calibrationBackups = group.Value;
                        calibrationBackups.Sort((a, b) => b.CreatedTime.CompareTo(a.CreatedTime));
                        
                        // 删除超出数量限制的备份
                        for (int i = maxBackupsPerCalibration; i < calibrationBackups.Count; i++)
                        {
                            DeleteBackupFile(calibrationBackups[i].FilePath);
                            deletedCount++;
                        }
                        
                        // 删除超出时间限制的备份
                        for (int i = 0; i < Math.Min(maxBackupsPerCalibration, calibrationBackups.Count); i++)
                        {
                            if (calibrationBackups[i].CreatedTime < cutoffDate)
                            {
                                DeleteBackupFile(calibrationBackups[i].FilePath);
                                deletedCount++;
                            }
                        }
                    }
                    
                    return deletedCount;
                }
                catch
                {
                    // 清理备份文件失败（不重要，使用匿名catch）
                    return 0;
                }
            });
        }
        
        /// <summary>
        /// 批量导出标定数据
        /// </summary>
        public static async Task<bool> BatchExportCalibrationsAsync(Dictionary<string, NinePointCalibrationData> calibrations, string exportDirectory)
        {
            if (calibrations == null || calibrations.Count == 0 || string.IsNullOrEmpty(exportDirectory))
                return false;
                
            try
            {
                if (!Directory.Exists(exportDirectory))
                {
                    Directory.CreateDirectory(exportDirectory);
                }
                
                var tasks = new List<Task<bool>>();
                
                foreach (var kvp in calibrations)
                {
                    var fileName = $"{kvp.Key}{CalibrationFileExtension}";
                    var filePath = Path.Combine(exportDirectory, fileName);
                    tasks.Add(ExportCalibrationAsync(kvp.Value, filePath));
                }
                
                var results = await Task.WhenAll(tasks);
                
                // 检查是否所有导出都成功
                foreach (var result in results)
                {
                    if (!result) return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // 批量导出失败（重要错误）
                Console.WriteLine($"[VisionLite] 批量导出标定数据失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取文件信息摘要
        /// </summary>
        public static async Task<CalibrationFileInfo> GetFileInfoAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;
                
            try
            {
                var fileInfo = new FileInfo(filePath);
                var calibration = await ImportCalibrationAsync(filePath);
                
                return new CalibrationFileInfo
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedTime = fileInfo.CreationTime,
                    ModifiedTime = fileInfo.LastWriteTime,
                    CalibrationName = calibration?.Name,
                    IsValid = calibration?.IsValid == true,
                    ValidPointCount = calibration?.ValidPointCount ?? 0,
                    CalibrationError = calibration?.CalibrationError ?? 0,
                    Quality = calibration?.Quality ?? CalibrationQuality.Unusable
                };
            }
            catch (Exception ex)
            {
                // 获取文件信息失败（重要错误）
                Console.WriteLine($"[VisionLite] 获取标定文件信息失败: {ex.Message}");
                return null;
            }
        }
        
        #region 私有方法
        
        private static string CalculateChecksum(NinePointCalibrationData calibration)
        {
            try
            {
                var json = JsonConvert.SerializeObject(calibration);
                var hash = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hash);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private static bool ValidateChecksum(CalibrationExportData exportData)
        {
            if (exportData?.CalibrationData == null || string.IsNullOrEmpty(exportData.Checksum))
                return false;
                
            var calculatedChecksum = CalculateChecksum(exportData.CalibrationData);
            return calculatedChecksum == exportData.Checksum;
        }
        
        private static void DeleteBackupFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // 删除备份文件失败（不重要，使用匿名catch）
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 标定导出数据格式
    /// </summary>
    public class CalibrationExportData
    {
        public NinePointCalibrationData CalibrationData { get; set; }
        public DateTime ExportTime { get; set; }
        public string ExportVersion { get; set; }
        public string ApplicationName { get; set; }
        public string Checksum { get; set; }
    }
    
    /// <summary>
    /// 备份文件信息
    /// </summary>
    public class BackupFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string CalibrationName { get; set; }
        public DateTime CreatedTime { get; set; }
        public long Size { get; set; }
    }
    
    /// <summary>
    /// 标定文件信息
    /// </summary>
    public class CalibrationFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string CalibrationName { get; set; }
        public bool IsValid { get; set; }
        public int ValidPointCount { get; set; }
        public double CalibrationError { get; set; }
        public CalibrationQuality Quality { get; set; }
    }
}