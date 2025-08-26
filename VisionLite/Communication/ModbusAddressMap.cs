using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;

namespace VisionLite.Communication
{
    /// <summary>
    /// Modbus数据类型枚举
    /// </summary>
    public enum ModbusDataType
    {
        [Description("布尔值")]
        Boolean,
        [Description("16位无符号整数")]
        UInt16,
        [Description("16位有符号整数")]
        Int16,
        [Description("32位浮点数")]
        Float
    }

    /// <summary>
    /// Modbus功能区类型
    /// </summary>
    public enum ModbusFunctionArea
    {
        [Description("线圈")]
        Coils = 0,
        [Description("离散输入")]
        DiscreteInputs = 1,
        [Description("输入寄存器")]
        InputRegisters = 3,
        [Description("保持寄存器")]
        HoldingRegisters = 4
    }

    /// <summary>
    /// Modbus地址映射项（简化版）
    /// </summary>
    public class ModbusAddressItem
    {
        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 变量描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 功能区类型
        /// </summary>
        public ModbusFunctionArea FunctionArea { get; set; }

        /// <summary>
        /// Modbus地址（1-based）
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public ModbusDataType DataType { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 数据长度（寄存器数量）
        /// </summary>
        [JsonIgnore]
        public ushort Length => GetLengthByDataType();

        /// <summary>
        /// 标准Modbus地址（带功能区前缀）
        /// </summary>
        [JsonIgnore]
        public string StandardAddress => GetStandardAddress();

        /// <summary>
        /// 根据数据类型获取长度
        /// </summary>
        private ushort GetLengthByDataType()
        {
            return DataType switch
            {
                ModbusDataType.Boolean => 1,
                ModbusDataType.UInt16 => 1,
                ModbusDataType.Int16 => 1,
                ModbusDataType.Float => 2,
                _ => 1
            };
        }

        /// <summary>
        /// 获取标准地址格式
        /// </summary>
        private string GetStandardAddress()
        {
            return FunctionArea switch
            {
                ModbusFunctionArea.Coils => $"{Address:D5}",
                ModbusFunctionArea.DiscreteInputs => $"{10000 + Address:D5}",
                ModbusFunctionArea.InputRegisters => $"{30000 + Address:D5}",
                ModbusFunctionArea.HoldingRegisters => $"{40000 + Address:D5}",
                _ => Address.ToString()
            };
        }

        /// <summary>
        /// 验证映射项
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("变量名称不能为空");

            if (Address == 0)
                errors.Add("地址不能为0");

            // 检查数据类型与功能区的匹配
            if (DataType == ModbusDataType.Boolean && 
                (FunctionArea == ModbusFunctionArea.InputRegisters || FunctionArea == ModbusFunctionArea.HoldingRegisters))
                errors.Add("寄存器区不支持布尔类型");

            if (DataType != ModbusDataType.Boolean && 
                (FunctionArea == ModbusFunctionArea.Coils || FunctionArea == ModbusFunctionArea.DiscreteInputs))
                errors.Add("线圈区只支持布尔类型");

            return errors;
        }
    }

    /// <summary>
    /// Modbus地址映射表（简化版）
    /// </summary>
    public class ModbusAddressMap
    {
        /// <summary>
        /// 映射表名称
        /// </summary>
        public string Name { get; set; } = "默认地址映射";

        /// <summary>
        /// 地址映射项列表
        /// </summary>
        public List<ModbusAddressItem> Items { get; set; } = new List<ModbusAddressItem>();

        /// <summary>
        /// 验证整个映射表
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var addressUsage = new Dictionary<string, string>(); // key: "FunctionArea:Address", value: ItemName

            foreach (var item in Items.Where(i => i.Enabled))
            {
                // 验证单个项
                var itemErrors = item.Validate();
                errors.AddRange(itemErrors.Select(e => $"{item.Name}: {e}"));

                // 检查地址冲突
                for (ushort i = 0; i < item.Length; i++)
                {
                    var checkAddress = (ushort)(item.Address + i);
                    var key = $"{item.FunctionArea}:{checkAddress}";
                    
                    if (addressUsage.ContainsKey(key))
                    {
                        errors.Add($"地址冲突: {item.Name} 和 {addressUsage[key]} 都使用了 {item.FunctionArea}:{checkAddress}");
                    }
                    else
                    {
                        addressUsage[key] = item.Name;
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// 获取指定功能区的最大地址
        /// </summary>
        public ushort GetMaxAddress(ModbusFunctionArea functionArea)
        {
            return Items
                .Where(item => item.Enabled && item.FunctionArea == functionArea)
                .Select(item => (ushort)(item.Address + item.Length - 1))
                .DefaultIfEmpty((ushort)0)
                .Max();
        }
    }
}