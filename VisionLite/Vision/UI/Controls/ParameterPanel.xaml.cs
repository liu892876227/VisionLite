using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VisionLite.Vision.Core.Interfaces;
using VisionLite.Vision.Core.Models;
using VisionLite.Vision.Processors.Preprocessing.FilterProcessors;

namespace VisionLite.Vision.UI.Controls
{
    /// <summary>
    /// 参数面板控件
    /// 动态生成算法参数的UI控件
    /// </summary>
    public partial class ParameterPanel : UserControl
    {
        #region 私有字段
        
        private IVisionProcessor _currentProcessor;
        private List<ParameterInfo> _currentParameters;
        private Dictionary<string, FrameworkElement> _parameterControls;
        
        #endregion
        
        #region 事件定义
        
        /// <summary>
        /// 参数值变化事件
        /// </summary>
        public event EventHandler<ParameterChangedEventArgs> ParameterChanged;
        
        /// <summary>
        /// 参数应用事件
        /// </summary>
        public event EventHandler ParametersApplied;
        
        /// <summary>
        /// 参数重置事件
        /// </summary>
        public event EventHandler ParametersReset;
        
        #endregion
        
        #region 构造函数
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ParameterPanel()
        {
            InitializeComponent();
            _parameterControls = new Dictionary<string, FrameworkElement>();
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 设置当前处理器
        /// </summary>
        /// <param name="processor">视觉处理器</param>
        public void SetProcessor(IVisionProcessor processor)
        {
            try
            {
                _currentProcessor = processor;
                
                if (processor != null)
                {
                    TitleText.Text = $"{processor.ProcessorName} - 参数配置";
                    _currentParameters = processor.GetParameters();
                    CreateParameterControls();
                }
                else
                {
                    TitleText.Text = "参数配置";
                    ClearParameterControls();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置处理器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearParameterControls();
            }
        }
        
        /// <summary>
        /// 应用参数到处理器
        /// </summary>
        public void ApplyParametersToProcessor()
        {
            if (_currentProcessor == null || _currentParameters == null)
                return;
                
            try
            {
                foreach (var parameter in _currentParameters)
                {
                    _currentProcessor.SetParameter(parameter.Name, parameter.Value);
                }
                
                ParametersApplied?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用参数失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 创建参数控件
        /// </summary>
        private void CreateParameterControls()
        {
            ClearParameterControls();
            
            if (_currentParameters == null || _currentParameters.Count == 0)
            {
                NoParametersText.Visibility = Visibility.Visible;
                return;
            }
            
            NoParametersText.Visibility = Visibility.Collapsed;
            
            // 按组分组参数
            var groupedParameters = _currentParameters
                .GroupBy(p => p.Group ?? "基础设置")
                .OrderBy(g => g.Key == "基础设置" ? 0 : 1);
            
            foreach (var group in groupedParameters)
            {
                // 创建组标题（如果不是默认组）
                if (group.Key != "基础设置")
                {
                    var groupHeader = new TextBlock
                    {
                        Text = group.Key,
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        Margin = new Thickness(0, 10, 0, 5),
                        Foreground = System.Windows.Media.Brushes.DarkBlue
                    };
                    ParametersPanel.Children.Add(groupHeader);
                    
                    var separator = new Separator { Margin = new Thickness(0, 0, 0, 5) };
                    ParametersPanel.Children.Add(separator);
                }
                
                // 创建组内参数控件
                var sortedParameters = group.OrderBy(p => p.Order).ThenBy(p => p.DisplayName);
                foreach (var parameter in sortedParameters)
                {
                    // 现在显示所有参数，包括高级参数
                    CreateParameterControl(parameter);
                }
            }
        }
        
        /// <summary>
        /// 创建单个参数控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        private void CreateParameterControl(ParameterInfo parameter)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            
            // 创建标签
            var label = new TextBlock
            {
                Text = parameter.DisplayName ?? parameter.Name,
                Style = (Style)FindResource("ParameterLabelStyle")
            };
            container.Children.Add(label);
            
            // 创建对应的输入控件
            FrameworkElement inputControl = CreateInputControl(parameter);
            if (inputControl != null)
            {
                container.Children.Add(inputControl);
                _parameterControls[parameter.Name] = inputControl;
            }
            
            // 添加描述文本（如果有）
            if (!string.IsNullOrEmpty(parameter.Description))
            {
                var description = new TextBlock
                {
                    Text = parameter.Description,
                    Style = (Style)FindResource("ParameterDescriptionStyle")
                };
                container.Children.Add(description);
            }
            
            ParametersPanel.Children.Add(container);
        }
        
        /// <summary>
        /// 根据参数类型创建输入控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <returns>输入控件</returns>
        private FrameworkElement CreateInputControl(ParameterInfo parameter)
        {
            switch (parameter.ParameterType)
            {
                case ParameterType.Double:
                case ParameterType.Integer:
                    return CreateNumericControl(parameter);
                    
                case ParameterType.Boolean:
                    return CreateBooleanControl(parameter);
                    
                case ParameterType.String:
                    return CreateStringControl(parameter);
                    
                case ParameterType.Enum:
                    return CreateEnumControl(parameter);
                    
                default:
                    return CreateStringControl(parameter);
            }
        }
        
        /// <summary>
        /// 创建数值控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <returns>数值控件</returns>
        private FrameworkElement CreateNumericControl(ParameterInfo parameter)
        {
            var panel = new StackPanel();
            
            // 如果有范围限制，创建滑块+文本框组合
            if (parameter.HasRange)
            {
                var sliderPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                // 文本框
                var textBox = new TextBox
                {
                    Text = FormatNumericValue(parameter.Value, parameter.ParameterType, parameter.DecimalPlaces),
                    Width = 80,
                    Style = (Style)FindResource("ParameterTextBoxStyle")
                };
                
                // 滑块
                var slider = new Slider
                {
                    Minimum = parameter.MinValue,
                    Maximum = parameter.MaxValue,
                    Value = Convert.ToDouble(parameter.Value ?? 0),
                    Width = 150,
                    Style = (Style)FindResource("ParameterSliderStyle")
                };
                
                // 绑定事件
                textBox.TextChanged += (s, e) =>
                {
                    if (ParseNumericValue(textBox.Text, parameter.ParameterType, out var value))
                    {
                        parameter.Value = value;
                        slider.Value = Convert.ToDouble(value);
                        OnParameterChanged(parameter);
                    }
                };
                
                slider.ValueChanged += (s, e) =>
                {
                    var value = parameter.ParameterType == ParameterType.Integer 
                        ? (object)(int)slider.Value 
                        : slider.Value;
                    parameter.Value = value;
                    textBox.Text = FormatNumericValue(value, parameter.ParameterType, parameter.DecimalPlaces);
                    OnParameterChanged(parameter);
                };
                
                sliderPanel.Children.Add(textBox);
                sliderPanel.Children.Add(slider);
                panel.Children.Add(sliderPanel);
                
                // 添加范围提示
                var rangeText = new TextBlock
                {
                    Text = $"范围: {parameter.MinValue} ~ {parameter.MaxValue}",
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                panel.Children.Add(rangeText);
            }
            else
            {
                // 只有文本框
                var textBox = new TextBox
                {
                    Text = FormatNumericValue(parameter.Value, parameter.ParameterType, parameter.DecimalPlaces),
                    Style = (Style)FindResource("ParameterTextBoxStyle")
                };
                
                textBox.TextChanged += (s, e) =>
                {
                    if (ParseNumericValue(textBox.Text, parameter.ParameterType, out var value))
                    {
                        parameter.Value = value;
                        OnParameterChanged(parameter);
                    }
                };
                
                panel.Children.Add(textBox);
            }
            
            return panel;
        }
        
        /// <summary>
        /// 创建布尔控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <returns>布尔控件</returns>
        private FrameworkElement CreateBooleanControl(ParameterInfo parameter)
        {
            var checkBox = new CheckBox
            {
                IsChecked = Convert.ToBoolean(parameter.Value ?? false),
                Style = (Style)FindResource("ParameterCheckBoxStyle")
            };
            
            checkBox.Checked += (s, e) =>
            {
                parameter.Value = true;
                OnParameterChanged(parameter);
            };
            
            checkBox.Unchecked += (s, e) =>
            {
                parameter.Value = false;
                OnParameterChanged(parameter);
            };
            
            return checkBox;
        }
        
        /// <summary>
        /// 创建字符串控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <returns>字符串控件</returns>
        private FrameworkElement CreateStringControl(ParameterInfo parameter)
        {
            var textBox = new TextBox
            {
                Text = parameter.Value?.ToString() ?? string.Empty,
                Style = (Style)FindResource("ParameterTextBoxStyle")
            };
            
            textBox.TextChanged += (s, e) =>
            {
                parameter.Value = textBox.Text;
                OnParameterChanged(parameter);
            };
            
            return textBox;
        }
        
        /// <summary>
        /// 创建枚举控件
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <returns>枚举控件</returns>
        private FrameworkElement CreateEnumControl(ParameterInfo parameter)
        {
            var comboBox = new ComboBox
            {
                Style = (Style)FindResource("ParameterComboBoxStyle")
            };
            
            // 创建映射字典，用于中文显示名称和枚举值的对应
            var displayNameMap = new Dictionary<string, object>();
            
            // 添加枚举选项
            foreach (var enumValue in parameter.EnumValues)
            {
                string displayName = GetEnumDisplayName(enumValue);
                comboBox.Items.Add(displayName);
                displayNameMap[displayName] = enumValue;
            }
            
            // 设置当前选中项
            if (parameter.Value != null)
            {
                string currentDisplayName = GetEnumDisplayName(parameter.Value);
                comboBox.SelectedItem = currentDisplayName;
            }
            
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    var selectedDisplayName = comboBox.SelectedItem.ToString();
                    if (displayNameMap.TryGetValue(selectedDisplayName, out var enumValue))
                    {
                        parameter.Value = enumValue;
                        OnParameterChanged(parameter);
                    }
                }
            };
            
            return comboBox;
        }
        
        /// <summary>
        /// 获取枚举值的中文显示名称
        /// </summary>
        /// <param name="enumValue">枚举值</param>
        /// <returns>中文显示名称</returns>
        private string GetEnumDisplayName(object enumValue)
        {
            if (enumValue is FilterDirection filterDirection)
                return filterDirection.GetDisplayName();
            else if (enumValue.GetType().Name == "FilterShape")
            {
                // 使用反射调用GetDisplayName扩展方法
                try
                {
                    var type = enumValue.GetType();
                    var extensionType = type.Assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "FilterShapeExtensions");
                    if (extensionType != null)
                    {
                        var method = extensionType.GetMethod("GetDisplayName");
                        if (method != null)
                        {
                            return method.Invoke(null, new[] { enumValue })?.ToString() ?? enumValue.ToString();
                        }
                    }
                }
                catch
                {
                    // 如果反射失败，返回默认值
                }
                return enumValue.ToString();
            }
            else
                return enumValue?.ToString() ?? "";
        }
        
        /// <summary>
        /// 清空参数控件
        /// </summary>
        private void ClearParameterControls()
        {
            ParametersPanel.Children.Clear();
            _parameterControls.Clear();
            NoParametersText.Visibility = Visibility.Visible;
        }
        
        /// <summary>
        /// 格式化数值显示
        /// </summary>
        private string FormatNumericValue(object value, ParameterType type, int decimalPlaces)
        {
            if (value == null) return "0";
            
            if (type == ParameterType.Integer)
                return Convert.ToInt32(value).ToString();
            else
                return Convert.ToDouble(value).ToString($"F{decimalPlaces}");
        }
        
        /// <summary>
        /// 解析数值
        /// </summary>
        private bool ParseNumericValue(string text, ParameterType type, out object value)
        {
            value = null;
            
            if (type == ParameterType.Integer)
            {
                if (int.TryParse(text, out int intValue))
                {
                    value = intValue;
                    return true;
                }
            }
            else if (type == ParameterType.Double)
            {
                if (double.TryParse(text, out double doubleValue))
                {
                    value = doubleValue;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 触发参数变化事件
        /// </summary>
        private void OnParameterChanged(ParameterInfo parameter)
        {
            ParameterChanged?.Invoke(this, new ParameterChangedEventArgs
            {
                ParameterName = parameter.Name,
                OldValue = null, // 可以扩展记录旧值
                NewValue = parameter.Value
            });
        }
        
        #endregion
        
        #region 事件处理
        
        /// <summary>
        /// 重置按钮点击
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParameters != null)
            {
                foreach (var parameter in _currentParameters)
                {
                    parameter.ResetToDefault();
                }
                
                // 重新创建控件以反映重置的值
                CreateParameterControls();
                
                ParametersReset?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// 应用按钮点击
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyParametersToProcessor();
        }
        
        #endregion
    }
    
    /// <summary>
    /// 参数变化事件参数
    /// </summary>
    public class ParameterChangedEventArgs : EventArgs
    {
        public string ParameterName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }
}