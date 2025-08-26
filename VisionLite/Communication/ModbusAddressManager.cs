using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace VisionLite.Communication
{
    /// <summary>
    /// Modbus地址映射管理器（简化版）
    /// </summary>
    public class ModbusAddressManager
    {
        private ModbusAddressMap _currentMap;

        public ModbusAddressManager()
        {
            _currentMap = new ModbusAddressMap();
        }

        /// <summary>
        /// 当前地址映射表
        /// </summary>
        public ModbusAddressMap CurrentMap => _currentMap;

        /// <summary>
        /// 地址映射变化事件
        /// </summary>
        public event Action<ModbusAddressMap> MapChanged;

        /// <summary>
        /// 加载地址映射文件
        /// </summary>
        public bool LoadMap(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var map = JsonConvert.DeserializeObject<ModbusAddressMap>(json);
                
                if (map != null)
                {
                    _currentMap = map;
                    MapChanged?.Invoke(_currentMap);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载地址映射失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 保存地址映射文件
        /// </summary>
        public bool SaveMap(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_currentMap, Formatting.Indented);
                
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存地址映射失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建默认地址映射模板
        /// </summary>
        public void CreateDefaultTemplate()
        {
            _currentMap.Items.Clear();
            _currentMap.Name = "默认ModbusTCP地址映射";

            // 添加常用的系统状态映射
            _currentMap.Items.AddRange(new[]
            {
                new ModbusAddressItem
                {
                    Name = "SystemRunning",
                    Description = "系统运行状态",
                    FunctionArea = ModbusFunctionArea.DiscreteInputs,
                    Address = 1,
                    DataType = ModbusDataType.Boolean,
                    DefaultValue = "false"
                },
                new ModbusAddressItem
                {
                    Name = "SystemError",
                    Description = "系统错误状态",
                    FunctionArea = ModbusFunctionArea.DiscreteInputs,
                    Address = 2,
                    DataType = ModbusDataType.Boolean,
                    DefaultValue = "false"
                },
                new ModbusAddressItem
                {
                    Name = "StartCommand",
                    Description = "启动命令",
                    FunctionArea = ModbusFunctionArea.Coils,
                    Address = 1,
                    DataType = ModbusDataType.Boolean,
                    DefaultValue = "false"
                },
                new ModbusAddressItem
                {
                    Name = "StopCommand",
                    Description = "停止命令",
                    FunctionArea = ModbusFunctionArea.Coils,
                    Address = 2,
                    DataType = ModbusDataType.Boolean,
                    DefaultValue = "false"
                },
                new ModbusAddressItem
                {
                    Name = "Temperature",
                    Description = "当前温度值",
                    FunctionArea = ModbusFunctionArea.InputRegisters,
                    Address = 1,
                    DataType = ModbusDataType.Float,
                    DefaultValue = "25.0"
                },
                new ModbusAddressItem
                {
                    Name = "SetPoint",
                    Description = "温度设定值",
                    FunctionArea = ModbusFunctionArea.HoldingRegisters,
                    Address = 1,
                    DataType = ModbusDataType.Float,
                    DefaultValue = "50.0"
                }
            });

            MapChanged?.Invoke(_currentMap);
        }

        /// <summary>
        /// 验证当前映射表
        /// </summary>
        public List<string> ValidateCurrentMap()
        {
            return _currentMap.Validate();
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string GetConfigFilePath(string serverName)
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisionLite", "ModbusMaps");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, $"{serverName}_AddressMap.json");
        }
    }
}