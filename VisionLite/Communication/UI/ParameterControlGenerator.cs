// Communication/UI/ParameterControlGenerator.cs
// 参数控件动态生成器 - 根据参数定义动态创建对应的WPF控件
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VisionLite.Communication.Models;
using Microsoft.Win32;

namespace VisionLite.Communication.UI
{
    /// <summary>
    /// 参数控件生成器
    /// 根据参数定义动态创建相应的WPF输入控件
    /// </summary>
    public static class ParameterControlGenerator
    {
        #region 主要方法

        /// <summary>
        /// 为指定参数创建对应的输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>创建的控件</returns>
        public static UIElement CreateControlForParameter(ParameterDefinition paramDef, object currentValue = null)
        {
            if (paramDef == null)
                throw new ArgumentNullException(nameof(paramDef));

            // 如果参数不可见，返回空面板
            if (!paramDef.IsVisible)
                return new Grid { Visibility = Visibility.Collapsed };

            try
            {
                UIElement control = paramDef.Type switch
                {
                    ParameterType.String => CreateStringControl(paramDef, currentValue),
                    ParameterType.Integer => CreateIntegerControl(paramDef, currentValue),
                    ParameterType.Double => CreateDoubleControl(paramDef, currentValue),
                    ParameterType.Boolean => CreateBooleanControl(paramDef, currentValue),
                    ParameterType.IPAddress => CreateIPAddressControl(paramDef, currentValue),
                    ParameterType.Port => CreatePortControl(paramDef, currentValue),
                    ParameterType.ComboBox => CreateComboBoxControl(paramDef, currentValue),
                    ParameterType.FilePath => CreateFilePathControl(paramDef, currentValue),
                    ParameterType.Password => CreatePasswordControl(paramDef, currentValue),
                    _ => CreateStringControl(paramDef, currentValue) // 默认为字符串控件
                };

                // 设置通用属性
                SetCommonProperties(control, paramDef);
                
                return control;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建参数控件时发生异常: {ex.Message}");
                // 返回错误提示控件
                return CreateErrorControl($"创建控件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从控件中提取参数值
        /// </summary>
        /// <param name="control">控件实例</param>
        /// <param name="paramDef">参数定义</param>
        /// <returns>提取的参数值</returns>
        public static object ExtractValueFromControl(UIElement control, ParameterDefinition paramDef)
        {
            if (control == null || paramDef == null)
                return paramDef?.DefaultValue;

            try
            {
                return paramDef.Type switch
                {
                    ParameterType.String => ExtractStringValue(control),
                    ParameterType.Integer => ExtractIntegerValue(control),
                    ParameterType.Double => ExtractDoubleValue(control),
                    ParameterType.Boolean => ExtractBooleanValue(control),
                    ParameterType.IPAddress => ExtractStringValue(control),
                    ParameterType.Port => ExtractIntegerValue(control),
                    ParameterType.ComboBox => ExtractComboBoxValue(control),
                    ParameterType.FilePath => ExtractFilePathValue(control),
                    ParameterType.Password => ExtractPasswordValue(control),
                    _ => ExtractStringValue(control)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从控件提取值时发生异常: {ex.Message}");
                return paramDef.DefaultValue;
            }
        }

        /// <summary>
        /// 验证控件中的值是否有效
        /// </summary>
        /// <param name="control">控件实例</param>
        /// <param name="paramDef">参数定义</param>
        /// <returns>验证结果</returns>
        public static Models.ValidationResult ValidateControlValue(UIElement control, ParameterDefinition paramDef)
        {
            if (control == null || paramDef == null)
                return new Models.ValidationResult(false, "控件或参数定义为空");

            try
            {
                var value = ExtractValueFromControl(control, paramDef);
                return paramDef.ValidateValue(value);
            }
            catch (Exception ex)
            {
                return new Models.ValidationResult(false, $"验证控件值时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 控件创建方法

        /// <summary>
        /// 创建字符串输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>文本框控件</returns>
        private static TextBox CreateStringControl(ParameterDefinition paramDef, object currentValue)
        {
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? paramDef.DefaultValue?.ToString() ?? "",
                ToolTip = paramDef.Description,
                Tag = paramDef.Key,
                IsEnabled = paramDef.IsEditable,
                MaxLines = 1
            };

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                textBox.Width = paramDef.ControlWidth.Value;

            // 添加输入验证
            if (!string.IsNullOrEmpty(paramDef.ValidationPattern))
            {
                textBox.TextChanged += (sender, e) =>
                {
                    var tb = sender as TextBox;
                    var validation = paramDef.ValidateValue(tb.Text);
                    SetValidationState(tb, validation);
                };
            }

            return textBox;
        }

        /// <summary>
        /// 创建整数输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>整数输入控件</returns>
        private static TextBox CreateIntegerControl(ParameterDefinition paramDef, object currentValue)
        {
            var textBox = new TextBox
            {
                Tag = paramDef.Key,
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable
            };

            // 设置初始值
            if (currentValue != null && int.TryParse(currentValue.ToString(), out int intValue))
            {
                textBox.Text = intValue.ToString();
            }
            else if (paramDef.DefaultValue != null && int.TryParse(paramDef.DefaultValue.ToString(), out int defaultValue))
            {
                textBox.Text = defaultValue.ToString();
            }

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                textBox.Width = paramDef.ControlWidth.Value;

            // 添加数字输入限制
            textBox.PreviewTextInput += (sender, e) =>
            {
                // 只允许输入数字和负号
                e.Handled = !IsValidIntegerInput(e.Text);
            };

            // 添加验证
            textBox.TextChanged += (sender, e) =>
            {
                var tb = sender as TextBox;
                var validation = paramDef.ValidateValue(tb.Text);
                SetValidationState(tb, validation);
            };

            return textBox;
        }

        /// <summary>
        /// 创建浮点数输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>浮点数输入控件</returns>
        private static TextBox CreateDoubleControl(ParameterDefinition paramDef, object currentValue)
        {
            var textBox = new TextBox
            {
                Tag = paramDef.Key,
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable
            };

            // 设置初始值
            if (currentValue != null && double.TryParse(currentValue.ToString(), out double doubleValue))
            {
                textBox.Text = doubleValue.ToString(CultureInfo.InvariantCulture);
            }
            else if (paramDef.DefaultValue != null && double.TryParse(paramDef.DefaultValue.ToString(), out double defaultValue))
            {
                textBox.Text = defaultValue.ToString(CultureInfo.InvariantCulture);
            }

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                textBox.Width = paramDef.ControlWidth.Value;

            // 添加数字输入限制
            textBox.PreviewTextInput += (sender, e) =>
            {
                // 允许数字、小数点和负号
                e.Handled = !IsValidDoubleInput(e.Text);
            };

            // 添加验证
            textBox.TextChanged += (sender, e) =>
            {
                var tb = sender as TextBox;
                var validation = paramDef.ValidateValue(tb.Text);
                SetValidationState(tb, validation);
            };

            return textBox;
        }

        /// <summary>
        /// 创建布尔值控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>复选框控件</returns>
        private static CheckBox CreateBooleanControl(ParameterDefinition paramDef, object currentValue)
        {
            var checkBox = new CheckBox
            {
                Content = paramDef.DisplayName,
                Tag = paramDef.Key,
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable
            };

            // 设置初始值
            if (currentValue is bool boolValue)
            {
                checkBox.IsChecked = boolValue;
            }
            else if (paramDef.DefaultValue is bool defaultBool)
            {
                checkBox.IsChecked = defaultBool;
            }
            else if (bool.TryParse(currentValue?.ToString() ?? paramDef.DefaultValue?.ToString(), out bool parsedValue))
            {
                checkBox.IsChecked = parsedValue;
            }

            return checkBox;
        }

        /// <summary>
        /// 创建IP地址输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>IP地址输入控件</returns>
        private static TextBox CreateIPAddressControl(ParameterDefinition paramDef, object currentValue)
        {
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? paramDef.DefaultValue?.ToString() ?? "",
                Tag = paramDef.Key,
                ToolTip = paramDef.Description + "\n格式：xxx.xxx.xxx.xxx",
                IsEnabled = paramDef.IsEditable
            };

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                textBox.Width = paramDef.ControlWidth.Value;

            // 添加IP地址格式验证
            textBox.TextChanged += (sender, e) =>
            {
                var tb = sender as TextBox;
                var validation = paramDef.ValidateValue(tb.Text);
                SetValidationState(tb, validation);
            };

            return textBox;
        }

        /// <summary>
        /// 创建端口号输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>端口号输入控件</returns>
        private static TextBox CreatePortControl(ParameterDefinition paramDef, object currentValue)
        {
            var textBox = new TextBox
            {
                Tag = paramDef.Key,
                ToolTip = paramDef.Description + "\n范围：1-65535",
                IsEnabled = paramDef.IsEditable
            };

            // 设置初始值
            if (currentValue != null && int.TryParse(currentValue.ToString(), out int portValue))
            {
                textBox.Text = portValue.ToString();
            }
            else if (paramDef.DefaultValue != null && int.TryParse(paramDef.DefaultValue.ToString(), out int defaultPort))
            {
                textBox.Text = defaultPort.ToString();
            }

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                textBox.Width = paramDef.ControlWidth.Value;

            // 添加端口号输入限制
            textBox.PreviewTextInput += (sender, e) =>
            {
                // 只允许输入数字
                e.Handled = !IsValidPortInput(e.Text);
            };

            // 添加端口范围验证
            textBox.TextChanged += (sender, e) =>
            {
                var tb = sender as TextBox;
                var validation = paramDef.ValidateValue(tb.Text);
                SetValidationState(tb, validation);
            };

            return textBox;
        }

        /// <summary>
        /// 创建下拉选择控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>下拉框控件</returns>
        private static ComboBox CreateComboBoxControl(ParameterDefinition paramDef, object currentValue)
        {
            var comboBox = new ComboBox
            {
                Tag = paramDef.Key,
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable
            };

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                comboBox.Width = paramDef.ControlWidth.Value;

            // 添加选项
            if (paramDef.ValidValues != null)
            {
                foreach (var validValue in paramDef.ValidValues)
                {
                    comboBox.Items.Add(validValue.ToString());
                }
            }

            // 设置选中项
            if (currentValue != null)
            {
                comboBox.SelectedItem = currentValue.ToString();
            }
            else if (paramDef.DefaultValue != null)
            {
                comboBox.SelectedItem = paramDef.DefaultValue.ToString();
            }

            // 如果没有找到匹配项，选择第一项
            if (comboBox.SelectedItem == null && comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }

            return comboBox;
        }

        /// <summary>
        /// 创建文件路径选择控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>文件路径选择控件</returns>
        private static DockPanel CreateFilePathControl(ParameterDefinition paramDef, object currentValue)
        {
            var dockPanel = new DockPanel
            {
                Tag = paramDef.Key,
                LastChildFill = true
            };

            // 浏览按钮
            var browseButton = new Button
            {
                Content = "浏览...",
                Width = 60,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "点击选择文件"
            };
            DockPanel.SetDock(browseButton, Dock.Right);

            // 文件路径文本框
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? paramDef.DefaultValue?.ToString() ?? "",
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable,
                IsReadOnly = !paramDef.IsEditable
            };

            // 浏览按钮点击事件
            browseButton.Click += (sender, e) =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = $"选择 {paramDef.DisplayName}",
                    Filter = "所有文件 (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    textBox.Text = dialog.FileName;
                }
            };

            dockPanel.Children.Add(browseButton);
            dockPanel.Children.Add(textBox);

            return dockPanel;
        }

        /// <summary>
        /// 创建密码输入控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        /// <param name="currentValue">当前值</param>
        /// <returns>密码框控件</returns>
        private static PasswordBox CreatePasswordControl(ParameterDefinition paramDef, object currentValue)
        {
            var passwordBox = new PasswordBox
            {
                Password = currentValue?.ToString() ?? paramDef.DefaultValue?.ToString() ?? "",
                Tag = paramDef.Key,
                ToolTip = paramDef.Description,
                IsEnabled = paramDef.IsEditable
            };

            // 设置宽度
            if (paramDef.ControlWidth.HasValue)
                passwordBox.Width = paramDef.ControlWidth.Value;

            return passwordBox;
        }

        /// <summary>
        /// 创建错误提示控件
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>错误提示控件</returns>
        private static TextBlock CreateErrorControl(string errorMessage)
        {
            return new TextBlock
            {
                Text = errorMessage,
                Foreground = Brushes.Red,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            };
        }

        #endregion

        #region 值提取方法

        /// <summary>
        /// 从文本框控件提取字符串值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>字符串值</returns>
        private static string ExtractStringValue(UIElement control)
        {
            return control switch
            {
                TextBox textBox => textBox.Text,
                _ => ""
            };
        }

        /// <summary>
        /// 从控件提取整数值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>整数值</returns>
        private static int ExtractIntegerValue(UIElement control)
        {
            if (control is TextBox textBox)
            {
                if (int.TryParse(textBox.Text, out int value))
                    return value;
            }
            return 0;
        }

        /// <summary>
        /// 从控件提取浮点数值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>浮点数值</returns>
        private static double ExtractDoubleValue(UIElement control)
        {
            if (control is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    return value;
            }
            return 0.0;
        }

        /// <summary>
        /// 从复选框提取布尔值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>布尔值</returns>
        private static bool ExtractBooleanValue(UIElement control)
        {
            return control is CheckBox checkBox && checkBox.IsChecked == true;
        }

        /// <summary>
        /// 从下拉框提取选中值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>选中值</returns>
        private static string ExtractComboBoxValue(UIElement control)
        {
            return control is ComboBox comboBox ? comboBox.SelectedItem?.ToString() ?? "" : "";
        }

        /// <summary>
        /// 从文件路径控件提取路径值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>文件路径</returns>
        private static string ExtractFilePathValue(UIElement control)
        {
            if (control is DockPanel dockPanel)
            {
                var textBox = dockPanel.Children.OfType<TextBox>().FirstOrDefault();
                return textBox?.Text ?? "";
            }
            return "";
        }

        /// <summary>
        /// 从密码框提取密码值
        /// </summary>
        /// <param name="control">控件</param>
        /// <returns>密码值</returns>
        private static string ExtractPasswordValue(UIElement control)
        {
            return control is PasswordBox passwordBox ? passwordBox.Password : "";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置控件的通用属性
        /// </summary>
        /// <param name="control">控件</param>
        /// <param name="paramDef">参数定义</param>
        private static void SetCommonProperties(UIElement control, ParameterDefinition paramDef)
        {
            if (control is FrameworkElement frameworkElement)
            {
                // 设置边距
                frameworkElement.Margin = new Thickness(2);
                
                // 设置垂直对齐
                frameworkElement.VerticalAlignment = VerticalAlignment.Center;
                
                // 设置最小高度
                frameworkElement.MinHeight = 22;
                
                // 如果指定了宽度且当前控件还没有设置宽度
                if (paramDef.ControlWidth.HasValue && double.IsNaN(frameworkElement.Width))
                {
                    frameworkElement.Width = paramDef.ControlWidth.Value;
                }
            }
        }

        /// <summary>
        /// 设置控件的验证状态显示
        /// </summary>
        /// <param name="control">控件</param>
        /// <param name="validation">验证结果</param>
        private static void SetValidationState(Control control, Models.ValidationResult validation)
        {
            if (validation.IsValid)
            {
                // 清除错误状态
                control.BorderBrush = SystemColors.ControlDarkBrush;
                control.ToolTip = control.Tag is string tag ? 
                    $"参数: {tag}" : 
                    control.ToolTip?.ToString()?.Split('\n')[0]; // 保留原始提示的第一行
            }
            else
            {
                // 设置错误状态
                control.BorderBrush = Brushes.Red;
                control.ToolTip = $"{control.ToolTip}\n错误: {validation.ErrorMessage}";
            }
        }

        /// <summary>
        /// 验证整数输入字符是否有效
        /// </summary>
        /// <param name="input">输入字符</param>
        /// <returns>是否有效</returns>
        private static bool IsValidIntegerInput(string input)
        {
            return Regex.IsMatch(input, @"^[0-9\-]+$");
        }

        /// <summary>
        /// 验证浮点数输入字符是否有效
        /// </summary>
        /// <param name="input">输入字符</param>
        /// <returns>是否有效</returns>
        private static bool IsValidDoubleInput(string input)
        {
            return Regex.IsMatch(input, @"^[0-9\.\-]+$");
        }

        /// <summary>
        /// 验证端口号输入字符是否有效
        /// </summary>
        /// <param name="input">输入字符</param>
        /// <returns>是否有效</returns>
        private static bool IsValidPortInput(string input)
        {
            return Regex.IsMatch(input, @"^[0-9]+$");
        }

        #endregion
    }
}