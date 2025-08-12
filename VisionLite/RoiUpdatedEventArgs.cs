using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionLite
{
    /// <summary>
    /// 用于在 RoiUpdated 事件中传递ROI参数的自定义事件参数类。
    /// </summary>
    public class RoiUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// 格式化好的、可直接显示的参数字符串。
        /// </summary>
        public string ParametersAsString { get; }

        /// <summary>
        /// 一个字典，包含所有原始的ROI参数键值对。
        /// </summary>
        public Dictionary<string, double> Parameters { get; }

        public RoiUpdatedEventArgs(Dictionary<string, double> parameters)
        {
            Parameters = parameters;
            // 自动生成格式化的字符串
            ParametersAsString = string.Join(" | ", parameters.Select(kvp => $"{ParameterTranslator.Translate(kvp.Key).Replace(":", "")}: {kvp.Value:F2}"));
        }
    }
}