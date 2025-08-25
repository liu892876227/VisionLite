// Communication/Managers/CommunicationConfigManager.cs
// 通讯配置管理器 - 负责通讯配置的持久化存储和加载
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VisionLite.Communication.Models;

namespace VisionLite.Communication.Managers
{
    /// <summary>
    /// 通讯配置管理器
    /// 提供配置的保存、加载、导入导出等功能
    /// </summary>
    public class CommunicationConfigManager
    {
        #region 常量定义

        /// <summary>
        /// 配置文件名
        /// </summary>
        private const string CONFIG_FILE_NAME = "CommunicationConfigs.json";

        /// <summary>
        /// 备份文件后缀
        /// </summary>
        private const string BACKUP_FILE_SUFFIX = ".backup";

        /// <summary>
        /// 配置文件版本号
        /// </summary>
        private const string CONFIG_VERSION = "1.0.0";

        #endregion

        #region 私有字段

        /// <summary>
        /// 配置文件完整路径
        /// </summary>
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisionLite",
            CONFIG_FILE_NAME
        );

        /// <summary>
        /// 备份文件完整路径
        /// </summary>
        private static readonly string BackupFilePath = ConfigFilePath + BACKUP_FILE_SUFFIX;

        #endregion

        #region 配置保存和加载

        /// <summary>
        /// 保存通讯配置列表到文件
        /// </summary>
        /// <param name="configs">配置列表</param>
        /// <returns>保存是否成功</returns>
        public static bool SaveConfigurations(List<CommunicationConfig> configs)
        {
            try
            {
                if (configs == null)
                {
                    System.Diagnostics.Debug.WriteLine("配置列表为空，跳过保存");
                    return false;
                }

                // 确保目录存在
                EnsureDirectoryExists();

                // 创建配置文件包装器
                var configFile = new ConfigurationFile
                {
                    Version = CONFIG_VERSION,
                    SaveTime = DateTime.Now,
                    Configurations = configs.Select(c => 
                    {
                        c.IsSaved = true;
                        return c;
                    }).ToList()
                };

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(configFile, Formatting.Indented, new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    NullValueHandling = NullValueHandling.Ignore
                });

                // 创建备份（如果原文件存在）
                CreateBackup();

                // 写入配置文件
                File.WriteAllText(ConfigFilePath, json, System.Text.Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"成功保存 {configs.Count} 个通讯配置到文件: {ConfigFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存通讯配置失败: {ex.Message}");
                
                // 尝试从备份恢复
                RestoreFromBackup();
                return false;
            }
        }

        /// <summary>
        /// 从文件加载通讯配置列表
        /// </summary>
        /// <returns>配置列表</returns>
        public static List<CommunicationConfig> LoadConfigurations()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("配置文件不存在，返回空列表");
                    return new List<CommunicationConfig>();
                }

                // 读取配置文件
                var json = File.ReadAllText(ConfigFilePath, System.Text.Encoding.UTF8);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine("配置文件为空，返回空列表");
                    return new List<CommunicationConfig>();
                }

                // 尝试反序列化新格式
                ConfigurationFile configFile;
                try
                {
                    configFile = JsonConvert.DeserializeObject<ConfigurationFile>(json);
                }
                catch (JsonException)
                {
                    // 如果新格式失败，尝试旧格式（直接反序列化为配置列表）
                    System.Diagnostics.Debug.WriteLine("尝试加载旧格式配置文件");
                    var oldConfigs = JsonConvert.DeserializeObject<List<CommunicationConfig>>(json);
                    return oldConfigs ?? new List<CommunicationConfig>();
                }

                // 验证配置文件版本
                if (!IsVersionCompatible(configFile.Version))
                {
                    System.Diagnostics.Debug.WriteLine($"配置文件版本不兼容: {configFile.Version}");
                    // 可以在这里实现版本迁移逻辑
                }

                var configurations = configFile.Configurations ?? new List<CommunicationConfig>();
                
                // 标记所有配置为已保存状态
                foreach (var config in configurations)
                {
                    config.IsSaved = true;
                }

                System.Diagnostics.Debug.WriteLine($"成功加载 {configurations.Count} 个通讯配置");
                return configurations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载通讯配置失败: {ex.Message}");
                
                // 尝试从备份文件恢复
                return LoadFromBackup();
            }
        }

        /// <summary>
        /// 保存单个配置
        /// </summary>
        /// <param name="config">要保存的配置</param>
        /// <returns>保存是否成功</returns>
        public static bool SaveSingleConfiguration(CommunicationConfig config)
        {
            try
            {
                var existingConfigs = LoadConfigurations();
                
                // 查找是否存在同名配置
                var existingIndex = existingConfigs.FindIndex(c => c.Name.Equals(config.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingIndex >= 0)
                {
                    // 更新现有配置
                    existingConfigs[existingIndex] = config;
                    System.Diagnostics.Debug.WriteLine($"更新现有配置: {config.Name}");
                }
                else
                {
                    // 添加新配置
                    existingConfigs.Add(config);
                    System.Diagnostics.Debug.WriteLine($"添加新配置: {config.Name}");
                }

                return SaveConfigurations(existingConfigs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存单个配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除指定配置
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <returns>删除是否成功</returns>
        public static bool DeleteConfiguration(string configName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configName))
                    return false;

                var existingConfigs = LoadConfigurations();
                var initialCount = existingConfigs.Count;
                
                existingConfigs.RemoveAll(c => c.Name.Equals(configName, StringComparison.OrdinalIgnoreCase));
                
                if (existingConfigs.Count < initialCount)
                {
                    System.Diagnostics.Debug.WriteLine($"删除配置: {configName}");
                    return SaveConfigurations(existingConfigs);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"未找到要删除的配置: {configName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除配置失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 配置恢复和导入导出

        /// <summary>
        /// 恢复通讯连接到主窗口
        /// </summary>
        /// <param name="mainWindow">主窗口实例</param>
        /// <returns>恢复成功的连接数量</returns>
        public static int RestoreCommunications(MainWindow mainWindow)
        {
            if (mainWindow?.communications == null)
                return 0;

            int successCount = 0;
            var configs = LoadConfigurations();
            
            foreach (var config in configs)
            {
                try
                {
                    // 跳过已存在的连接
                    if (mainWindow.communications.ContainsKey(config.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"连接已存在，跳过: {config.Name}");
                        continue;
                    }

                    // 创建通讯实例
                    var communication = CommunicationProtocolManager.Instance.CreateCommunicationInstance(config);
                    
                    if (communication != null)
                    {
                        mainWindow.communications.Add(config.Name, communication);
                        successCount++;
                        
                        // 如果配置了自动连接，尝试连接
                        if (config.AutoConnect)
                        {
                            _ = communication.OpenAsync();
                            System.Diagnostics.Debug.WriteLine($"自动连接: {config.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"恢复通讯连接失败 ({config.Name}): {ex.Message}");
                }
            }

            if (successCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"成功恢复 {successCount} 个通讯连接");
            }

            return successCount;
        }

        /// <summary>
        /// 导出配置到指定文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <param name="configs">要导出的配置列表（为空则导出所有）</param>
        /// <returns>导出是否成功</returns>
        public static bool ExportConfigurations(string filePath, List<CommunicationConfig> configs = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return false;

                var configurationsToExport = configs ?? LoadConfigurations();
                
                var exportFile = new ConfigurationFile
                {
                    Version = CONFIG_VERSION,
                    SaveTime = DateTime.Now,
                    Configurations = configurationsToExport,
                    ExportInfo = new ExportInfo
                    {
                        ExportTime = DateTime.Now,
                        ExportedBy = Environment.UserName,
                        MachineName = Environment.MachineName
                    }
                };

                var json = JsonConvert.SerializeObject(exportFile, Formatting.Indented);
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"成功导出 {configurationsToExport.Count} 个配置到: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导出配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从指定文件导入配置
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <param name="mergeMode">合并模式</param>
        /// <returns>导入的配置列表</returns>
        public static List<CommunicationConfig> ImportConfigurations(string filePath, ImportMergeMode mergeMode = ImportMergeMode.Merge)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return null;

                var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var importFile = JsonConvert.DeserializeObject<ConfigurationFile>(json);
                
                if (importFile?.Configurations == null)
                    return null;

                var importedConfigs = importFile.Configurations;

                if (mergeMode == ImportMergeMode.Replace)
                {
                    // 替换模式：直接返回导入的配置
                    SaveConfigurations(importedConfigs);
                    return importedConfigs;
                }
                else
                {
                    // 合并模式：与现有配置合并
                    var existingConfigs = LoadConfigurations();
                    var mergedConfigs = MergeConfigurations(existingConfigs, importedConfigs, mergeMode);
                    SaveConfigurations(mergedConfigs);
                    return mergedConfigs;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导入配置失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 确保配置目录存在
        /// </summary>
        private static void EnsureDirectoryExists()
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                System.Diagnostics.Debug.WriteLine($"创建配置目录: {directory}");
            }
        }

        /// <summary>
        /// 创建配置文件备份
        /// </summary>
        private static void CreateBackup()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    File.Copy(ConfigFilePath, BackupFilePath, true);
                    System.Diagnostics.Debug.WriteLine("创建配置文件备份");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从备份文件恢复
        /// </summary>
        private static void RestoreFromBackup()
        {
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    File.Copy(BackupFilePath, ConfigFilePath, true);
                    System.Diagnostics.Debug.WriteLine("从备份恢复配置文件");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从备份恢复失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从备份文件加载配置
        /// </summary>
        /// <returns>配置列表</returns>
        private static List<CommunicationConfig> LoadFromBackup()
        {
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    var json = File.ReadAllText(BackupFilePath, System.Text.Encoding.UTF8);
                    var configFile = JsonConvert.DeserializeObject<ConfigurationFile>(json);
                    
                    System.Diagnostics.Debug.WriteLine("从备份文件加载配置");
                    return configFile?.Configurations ?? new List<CommunicationConfig>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从备份文件加载失败: {ex.Message}");
            }
            
            return new List<CommunicationConfig>();
        }

        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        /// <param name="version">文件版本</param>
        /// <returns>是否兼容</returns>
        private static bool IsVersionCompatible(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return true; // 假设兼容

            // 简单的版本比较，实际可以实现更复杂的版本兼容性检查
            return version.StartsWith("1.");
        }

        /// <summary>
        /// 合并配置列表
        /// </summary>
        /// <param name="existing">现有配置</param>
        /// <param name="imported">导入的配置</param>
        /// <param name="mergeMode">合并模式</param>
        /// <returns>合并后的配置列表</returns>
        private static List<CommunicationConfig> MergeConfigurations(
            List<CommunicationConfig> existing, 
            List<CommunicationConfig> imported, 
            ImportMergeMode mergeMode)
        {
            var result = new List<CommunicationConfig>(existing);

            foreach (var importedConfig in imported)
            {
                var existingIndex = result.FindIndex(c => c.Name.Equals(importedConfig.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingIndex >= 0)
                {
                    switch (mergeMode)
                    {
                        case ImportMergeMode.Overwrite:
                            result[existingIndex] = importedConfig;
                            break;
                        case ImportMergeMode.Skip:
                            // 跳过重复的配置
                            break;
                        case ImportMergeMode.Rename:
                            importedConfig.Name = GenerateUniqueName(result, importedConfig.Name);
                            result.Add(importedConfig);
                            break;
                        default: // Merge
                            result[existingIndex] = importedConfig;
                            break;
                    }
                }
                else
                {
                    result.Add(importedConfig);
                }
            }

            return result;
        }

        /// <summary>
        /// 生成唯一的配置名称
        /// </summary>
        /// <param name="existingConfigs">现有配置列表</param>
        /// <param name="baseName">基础名称</param>
        /// <returns>唯一名称</returns>
        private static string GenerateUniqueName(List<CommunicationConfig> existingConfigs, string baseName)
        {
            var counter = 1;
            var candidateName = $"{baseName}_导入";
            
            while (existingConfigs.Any(c => c.Name.Equals(candidateName, StringComparison.OrdinalIgnoreCase)))
            {
                candidateName = $"{baseName}_导入_{counter}";
                counter++;
            }
            
            return candidateName;
        }

        #endregion

        #region 配置查询和统计

        /// <summary>
        /// 获取配置统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public static ConfigurationStatistics GetStatistics()
        {
            try
            {
                var configs = LoadConfigurations();
                
                var stats = new ConfigurationStatistics
                {
                    TotalConfigurations = configs.Count,
                    ProtocolCounts = configs.GroupBy(c => c.ProtocolType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    LastSaveTime = File.Exists(ConfigFilePath) ? File.GetLastWriteTime(ConfigFilePath) : (DateTime?)null,
                    ConfigFileSize = File.Exists(ConfigFilePath) ? new FileInfo(ConfigFilePath).Length : 0,
                    AutoConnectCount = configs.Count(c => c.AutoConnect)
                };

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取配置统计失败: {ex.Message}");
                return new ConfigurationStatistics();
            }
        }

        /// <summary>
        /// 检查配置名称是否已存在
        /// </summary>
        /// <param name="name">配置名称</param>
        /// <returns>是否已存在</returns>
        public static bool IsConfigurationNameExists(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                var configs = LoadConfigurations();
                return configs.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region 辅助类和枚举

    /// <summary>
    /// 配置文件包装器
    /// </summary>
    public class ConfigurationFile
    {
        /// <summary>
        /// 配置文件格式版本
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime SaveTime { get; set; }

        /// <summary>
        /// 配置列表
        /// </summary>
        public List<CommunicationConfig> Configurations { get; set; }

        /// <summary>
        /// 导出信息（仅用于导出文件）
        /// </summary>
        public ExportInfo ExportInfo { get; set; }
    }

    /// <summary>
    /// 导出信息
    /// </summary>
    public class ExportInfo
    {
        /// <summary>
        /// 导出时间
        /// </summary>
        public DateTime ExportTime { get; set; }

        /// <summary>
        /// 导出用户
        /// </summary>
        public string ExportedBy { get; set; }

        /// <summary>
        /// 导出机器名
        /// </summary>
        public string MachineName { get; set; }
    }

    /// <summary>
    /// 导入合并模式
    /// </summary>
    public enum ImportMergeMode
    {
        /// <summary>
        /// 合并模式：重复名称时覆盖现有配置
        /// </summary>
        Merge,

        /// <summary>
        /// 覆盖模式：重复名称时覆盖现有配置
        /// </summary>
        Overwrite,

        /// <summary>
        /// 跳过模式：重复名称时跳过导入
        /// </summary>
        Skip,

        /// <summary>
        /// 重命名模式：重复名称时自动重命名
        /// </summary>
        Rename,

        /// <summary>
        /// 替换模式：完全替换现有配置
        /// </summary>
        Replace
    }

    /// <summary>
    /// 配置统计信息
    /// </summary>
    public class ConfigurationStatistics
    {
        /// <summary>
        /// 配置总数
        /// </summary>
        public int TotalConfigurations { get; set; }

        /// <summary>
        /// 各协议类型的配置数量
        /// </summary>
        public Dictionary<string, int> ProtocolCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime? LastSaveTime { get; set; }

        /// <summary>
        /// 配置文件大小（字节）
        /// </summary>
        public long ConfigFileSize { get; set; }

        /// <summary>
        /// 启用自动连接的配置数量
        /// </summary>
        public int AutoConnectCount { get; set; }
    }

    #endregion
}