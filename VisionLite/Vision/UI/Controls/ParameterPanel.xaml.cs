using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        
        // 事件抑制机制，防止输入框与滑块之间的双重事件触发
        private bool _isUpdatingSliderFromTextBox = false;
        
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
        
        /// <summary>
        /// 更新参数值并刷新UI控件
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="newValue">新值</param>
        public void UpdateParameterValue(string parameterName, object newValue)
        {
            if (_currentParameters == null || string.IsNullOrEmpty(parameterName))
                return;
                
            try
            {
                // 更新参数值
                var parameter = _currentParameters.FirstOrDefault(p => p.Name == parameterName);
                if (parameter != null)
                {
                    parameter.Value = newValue;
                    
                    // 更新UI控件显示
                    if (_parameterControls.TryGetValue(parameterName, out var control))
                    {
                        UpdateControlValue(control, parameter, newValue);
                    }
                    
                    // 同步到处理器
                    if (_currentProcessor != null)
                    {
                        _currentProcessor.SetParameter(parameterName, newValue);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新参数失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 静默更新参数值并刷新UI控件（不触发事件）
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="newValue">新值</param>
        public void UpdateParameterValueSilently(string parameterName, object newValue)
        {
            if (_currentParameters == null || string.IsNullOrEmpty(parameterName))
                return;
                
            try
            {
                // 更新参数值
                var parameter = _currentParameters.FirstOrDefault(p => p.Name == parameterName);
                if (parameter != null)
                {
                    parameter.Value = newValue;
                    
                    // 更新UI控件显示（但不同步到处理器，避免事件循环）
                    if (_parameterControls.TryGetValue(parameterName, out var control))
                    {
                        UpdateControlValue(control, parameter, newValue);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"静默更新参数失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新控件显示值
        /// </summary>
        private void UpdateControlValue(FrameworkElement control, ParameterInfo parameter, object newValue)
        {
            try
            {
                if (control is StackPanel stackPanel)
                {
                    // 处理数值控件（StackPanel包含TextBox和Slider）
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is StackPanel childStackPanel && childStackPanel.Orientation == Orientation.Horizontal)
                        {
                            // 查承数值控件的横向面板（含有TextBox和Slider）
                            foreach (var grandChild in childStackPanel.Children)
                            {
                                if (grandChild is TextBox textBox)
                                {
                                    textBox.Text = FormatNumericValue(newValue, parameter.ParameterType, parameter.DecimalPlaces);
                                }
                                else if (grandChild is Slider slider)
                                {
                                    // 同时更新滑块值（抑制事件触发）
                                    _isUpdatingSliderFromTextBox = true;
                                    slider.Value = Convert.ToDouble(newValue);
                                    _isUpdatingSliderFromTextBox = false;
                                }
                            }
                        }
                        else if (child is TextBox directTextBox)
                        {
                            // 处理直接的TextBox（没有范围限制的情况）
                            directTextBox.Text = FormatNumericValue(newValue, parameter.ParameterType, parameter.DecimalPlaces);
                        }
                        else if (child is CheckBox checkBox)
                        {
                            // 处理布尔控件
                            checkBox.IsChecked = Convert.ToBoolean(newValue);
                        }
                        else if (child is ComboBox comboBox && parameter.ParameterType == ParameterType.Enum)
                        {
                            // 处理枚举控件
                            for (int i = 0; i < parameter.EnumValues.Count; i++)
                            {
                                if (parameter.EnumValues[i].Equals(newValue))
                                {
                                    if (i < parameter.EnumDisplayNames.Count)
                                    {
                                        comboBox.SelectedItem = parameter.EnumDisplayNames[i];
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (control is TextBox textBox)
                {
                    // 直接的TextBox
                    textBox.Text = FormatNumericValue(newValue, parameter.ParameterType, parameter.DecimalPlaces);
                }
                else if (control is ComboBox comboBox && parameter.ParameterType == ParameterType.Enum)
                {
                    // 直接的ComboBox
                    for (int i = 0; i < parameter.EnumValues.Count; i++)
                    {
                        if (parameter.EnumValues[i].Equals(newValue))
                        {
                            if (i < parameter.EnumDisplayNames.Count)
                            {
                                comboBox.SelectedItem = parameter.EnumDisplayNames[i];
                            }
                            break;
                        }
                    }
                }
                else if (control is CheckBox directCheckBox)
                {
                    // 直接的CheckBox
                    directCheckBox.IsChecked = Convert.ToBoolean(newValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新控件值失败: {ex.Message}");
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
                
                // 绑定事件 - 改为失焦和回车确认
                Action<TextBox> applyTextBoxValue = (tb) =>
                {
                    if (ParseNumericValue(tb.Text, parameter.ParameterType, out var value))
                    {
                        // 验证范围
                        var doubleValue = Convert.ToDouble(value);
                        if (doubleValue < parameter.MinValue || doubleValue > parameter.MaxValue)
                        {
                            doubleValue = Math.Max(parameter.MinValue, Math.Min(parameter.MaxValue, doubleValue));
                            value = parameter.ParameterType == ParameterType.Integer ? (object)(int)doubleValue : doubleValue;
                            tb.Text = FormatNumericValue(value, parameter.ParameterType, parameter.DecimalPlaces);
                        }
                        
                        parameter.Value = value;
                        
                        // 抑制滑块事件触发，防止双重事件
                        _isUpdatingSliderFromTextBox = true;
                        slider.Value = Convert.ToDouble(value);
                        _isUpdatingSliderFromTextBox = false;
                        
                        OnParameterChanged(parameter);
                    }
                };
                
                textBox.LostFocus += (s, e) => applyTextBoxValue(textBox);
                textBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        applyTextBoxValue(textBox);
                        e.Handled = true;
                    }
                };
                
                slider.ValueChanged += (s, e) =>
                {
                    // 如果是从TextBox更新的，跳过处理，防止双重事件触发
                    if (_isUpdatingSliderFromTextBox) return;
                    
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
                
                // 改为失焦和回车确认
                Action<TextBox> applyTextBoxValue = (tb) =>
                {
                    if (ParseNumericValue(tb.Text, parameter.ParameterType, out var value))
                    {
                        parameter.Value = value;
                        OnParameterChanged(parameter);
                    }
                };
                
                textBox.LostFocus += (s, e) => applyTextBoxValue(textBox);
                textBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        applyTextBoxValue(textBox);
                        e.Handled = true;
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
            
            // 改为失焦和回车确认
            Action<TextBox> applyTextValue = (tb) =>
            {
                parameter.Value = tb.Text;
                OnParameterChanged(parameter);
            };
            
            textBox.LostFocus += (s, e) => applyTextValue(textBox);
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    applyTextValue(textBox);
                    e.Handled = true;
                }
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
            
            // 添加枚举选项（优先使用EnumDisplayNames）
            for (int i = 0; i < parameter.EnumValues.Count; i++)
            {
                var enumValue = parameter.EnumValues[i];
                string displayName;
                
                // 优先使用ParameterInfo中的EnumDisplayNames
                if (i < parameter.EnumDisplayNames.Count && !string.IsNullOrEmpty(parameter.EnumDisplayNames[i]))
                {
                    displayName = parameter.EnumDisplayNames[i];
                }
                else
                {
                    // 备用方案：使用原有的GetEnumDisplayName方法
                    displayName = GetEnumDisplayName(enumValue);
                }
                
                comboBox.Items.Add(displayName);
                displayNameMap[displayName] = enumValue;
            }
            
            // 设置当前选中项
            if (parameter.Value != null)
            {
                // 查找当前值对应的显示名称
                for (int i = 0; i < parameter.EnumValues.Count; i++)
                {
                    if (parameter.EnumValues[i].Equals(parameter.Value))
                    {
                        if (i < parameter.EnumDisplayNames.Count)
                        {
                            comboBox.SelectedItem = parameter.EnumDisplayNames[i];
                        }
                        else
                        {
                            comboBox.SelectedItem = GetEnumDisplayName(parameter.Value);
                        }
                        break;
                    }
                }
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
            // 支持StatisticalMethod枚举的中文显示
            else if (enumValue.GetType().Name == "StatisticalMethod")
            {
                try
                {
                    var type = enumValue.GetType();
                    var extensionType = type.Assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "StatisticalMethodExtensions");
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
            // 支持BorderMode枚举的中文显示
            else if (enumValue.GetType().Name == "BorderMode")
            {
                try
                {
                    var type = enumValue.GetType();
                    var extensionType = type.Assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "BorderModeExtensions");
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