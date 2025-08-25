// Communication/CommunicationConfigWindow.xaml.cs
// 新建通讯连接配置窗口 - 提供图形化的协议配置界面
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VisionLite.Communication.Models;
using VisionLite.Communication.Protocols;
using VisionLite.Communication.Managers;
using VisionLite.Communication.UI;

namespace VisionLite.Communication
{
    /// <summary>
    /// 通讯连接配置窗口
    /// 提供用户友好的图形界面来配置新的通讯连接
    /// </summary>
    public partial class CommunicationConfigWindow : Window
    {
        #region 私有字段和属性

        /// <summary>
        /// 当前选中的协议实例
        /// </summary>
        private ICommunicationProtocol _selectedProtocol;

        /// <summary>
        /// 参数控件映射字典，Key为参数名称，Value为对应的UI控件
        /// </summary>
        private Dictionary<string, UIElement> _parameterControls = new Dictionary<string, UIElement>();

        /// <summary>
        /// 参数定义列表，用于验证和值提取
        /// </summary>
        private List<ParameterDefinition> _parameterDefinitions = new List<ParameterDefinition>();

        /// <summary>
        /// 配置结果，成功配置后返回给调用方
        /// </summary>
        public CommunicationConfig Result { get; private set; }

        /// <summary>
        /// 是否为编辑模式（修改现有配置）
        /// </summary>
        public bool IsEditMode { get; private set; }

        /// <summary>
        /// 编辑模式下的原始配置
        /// </summary>
        private CommunicationConfig _originalConfig;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建新连接的构造函数
        /// </summary>
        public CommunicationConfigWindow()
        {
            InitializeComponent();
            IsEditMode = false;
            InitializeWindow();
        }

        /// <summary>
        /// 编辑现有连接的构造函数
        /// </summary>
        /// <param name="config">要编辑的配置</param>
        public CommunicationConfigWindow(CommunicationConfig config) : this()
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            IsEditMode = true;
            _originalConfig = config.Clone();
            Title = "编辑通讯连接";
            LoadExistingConfig(config);
        }

        #endregion

        #region 窗口初始化

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                InitializeProtocolComboBox();
                UpdateUI();
            }
            catch (Exception ex)
            {
                ShowError($"初始化窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化协议选择下拉框
        /// </summary>
        private void InitializeProtocolComboBox()
        {
            try
            {
                // 确保协议已加载
                LoadProtocols();
                
                var protocols = CommunicationProtocolManager.Instance.GetAllProtocols();
                
                if (protocols.Count == 0)
                {
                    ShowWarning("没有找到已注册的通讯协议。请确保协议已正确加载。");
                    return;
                }

                ProtocolComboBox.ItemsSource = protocols.OrderBy(p => p.DisplayName);
                
                // 如果只有一个协议，自动选择
                if (protocols.Count == 1)
                {
                    ProtocolComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                ShowError($"加载协议列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载协议（触发协议自动注册）
        /// </summary>
        private void LoadProtocols()
        {
            try
            {
                // 显式触发协议类的静态构造函数
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(VisionLite.Communication.Protocols.TcpClientProtocol).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(VisionLite.Communication.Protocols.TcpServerProtocol).TypeHandle);
                
                // 检查协议注册数量
                var protocolCount = CommunicationProtocolManager.Instance.RegisteredProtocolCount;
                System.Diagnostics.Debug.WriteLine($"配置窗口 - 已注册协议数量: {protocolCount}");
                
                if (protocolCount == 0)
                {
                    // 如果静态构造函数没有工作，手动注册协议
                    System.Diagnostics.Debug.WriteLine("配置窗口 - 静态构造函数未触发，手动注册协议");
                    CommunicationProtocolManager.Instance.RegisterProtocol(new VisionLite.Communication.Protocols.TcpClientProtocol());
                    CommunicationProtocolManager.Instance.RegisterProtocol(new VisionLite.Communication.Protocols.TcpServerProtocol());
                    
                    protocolCount = CommunicationProtocolManager.Instance.RegisteredProtocolCount;
                    System.Diagnostics.Debug.WriteLine($"配置窗口 - 手动注册后协议数量: {protocolCount}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置窗口 - 加载协议时发生异常: {ex.Message}");
                
                // 异常情况下也尝试手动注册
                try
                {
                    CommunicationProtocolManager.Instance.RegisterProtocol(new VisionLite.Communication.Protocols.TcpClientProtocol());
                    CommunicationProtocolManager.Instance.RegisterProtocol(new VisionLite.Communication.Protocols.TcpServerProtocol());
                }
                catch (Exception regEx)
                {
                    System.Diagnostics.Debug.WriteLine($"配置窗口 - 手动注册协议失败: {regEx.Message}");
                }
            }
        }

        /// <summary>
        /// 加载现有配置（编辑模式）
        /// </summary>
        /// <param name="config">要编辑的配置</param>
        private void LoadExistingConfig(CommunicationConfig config)
        {
            try
            {
                // 设置连接名称
                ConnectionNameTextBox.Text = config.Name;

                // 选择对应的协议
                var protocol = CommunicationProtocolManager.Instance.GetProtocol(config.ProtocolType);
                if (protocol != null)
                {
                    ProtocolComboBox.SelectedItem = protocol;
                    
                    // 等待协议选择处理完成后再设置参数值
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        SetParameterValues(config.Parameters);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    ShowWarning($"未找到协议类型: {config.ProtocolType}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"加载配置失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 协议选择变化事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedProtocol = ProtocolComboBox.SelectedItem as ICommunicationProtocol;
                
                if (_selectedProtocol != null)
                {
                    // 更新协议描述
                    ProtocolDescriptionText.Text = _selectedProtocol.Description;
                    
                    // 生成参数配置界面
                    BuildParameterPanel(_selectedProtocol);
                    
                    // 加载配置模板
                    LoadConfigTemplates(_selectedProtocol);
                    
                    // 生成默认连接名称（如果是新建模式且名称为空）
                    if (!IsEditMode && string.IsNullOrWhiteSpace(ConnectionNameTextBox.Text))
                    {
                        GenerateDefaultConnectionName(_selectedProtocol);
                    }
                }
                else
                {
                    // 清空参数面板
                    ClearParameterPanel();
                    ProtocolDescriptionText.Text = "请选择协议类型以查看详细描述";
                    TemplateGroupBox.Visibility = Visibility.Collapsed;
                }

                UpdateUI();
            }
            catch (Exception ex)
            {
                ShowError($"切换协议时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置模板选择变化事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void ConfigTemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedTemplate = ConfigTemplateComboBox.SelectedItem as CommunicationConfigTemplate;
                if (selectedTemplate != null)
                {
                    // 更新模板描述
                    TemplateDescriptionText.Text = selectedTemplate.Description;
                    
                    // 应用模板参数
                    ApplyTemplate(selectedTemplate);
                }
                else
                {
                    TemplateDescriptionText.Text = "";
                }
            }
            catch (Exception ex)
            {
                ShowError($"应用配置模板时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试连接按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProtocol == null)
            {
                ShowWarning("请先选择协议类型");
                return;
            }

            try
            {
                // 禁用测试按钮，防止重复点击
                TestConnectionButton.IsEnabled = false;
                TestConnectionButton.Content = "测试中...";

                // 收集当前参数
                var parameters = CollectParameters();
                if (parameters == null)
                    return; // 参数收集失败，错误信息已显示

                // 执行连接测试
                var testResult = await _selectedProtocol.TestConnectionAsync(parameters, 10000);
                
                if (testResult.Success)
                {
                    ShowInfo($"连接测试成功！\n{testResult.ResponseInfo}\n延迟: {testResult.LatencyMs}ms");
                }
                else
                {
                    ShowWarning($"连接测试失败：\n{testResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"测试连接时发生异常: {ex.Message}");
            }
            finally
            {
                // 恢复测试按钮状态
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "测试连接";
            }
        }

        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsEditMode && _originalConfig != null)
                {
                    // 编辑模式：恢复到原始配置
                    LoadExistingConfig(_originalConfig);
                    ShowInfo("已恢复到原始配置");
                }
                else if (_selectedProtocol != null)
                {
                    // 新建模式：重置为协议默认值
                    var defaultConfig = _selectedProtocol.GetDefaultConfig();
                    SetParameterValues(defaultConfig.Parameters);
                    ConnectionNameTextBox.Text = defaultConfig.Name;
                    ShowInfo("已重置为默认配置");
                }
            }
            catch (Exception ex)
            {
                ShowError($"重置配置时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证并收集配置
                var config = ValidateAndCollectConfiguration();
                if (config == null)
                    return; // 验证失败，错误信息已显示

                Result = config;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"保存配置时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }

        #endregion

        #region 参数界面生成和管理

        /// <summary>
        /// 构建参数配置面板
        /// </summary>
        /// <param name="protocol">协议实例</param>
        private void BuildParameterPanel(ICommunicationProtocol protocol)
        {
            try
            {
                // 清除现有内容
                ClearParameterPanel();

                // 获取参数定义
                _parameterDefinitions = protocol.GetParameterDefinitions();
                if (_parameterDefinitions == null || _parameterDefinitions.Count == 0)
                {
                    var noParamsText = new TextBlock
                    {
                        Text = "该协议不需要配置参数",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.Gray
                    };
                    DynamicParameterPanel.Children.Add(noParamsText);
                    return;
                }

                // 为每个参数创建控件
                foreach (var paramDef in _parameterDefinitions.Where(p => p.IsVisible))
                {
                    CreateParameterControl(paramDef);
                }
            }
            catch (Exception ex)
            {
                ShowError($"构建参数面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为单个参数创建控件
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        private void CreateParameterControl(ParameterDefinition paramDef)
        {
            try
            {
                // 创建参数行容器
                var parameterRow = new Grid
                {
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                parameterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                parameterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // 创建标签
                var label = new Label
                {
                    Content = paramDef.DisplayName + (paramDef.IsRequired ? ":" : ":"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(label, 0);

                // 创建输入控件容器
                var controlContainer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(controlContainer, 1);

                // 生成输入控件
                var inputControl = ParameterControlGenerator.CreateControlForParameter(paramDef);
                _parameterControls[paramDef.Key] = inputControl;

                controlContainer.Children.Add(inputControl);

                // 添加必需标记
                if (paramDef.IsRequired)
                {
                    var requiredMark = new TextBlock
                    {
                        Text = " *",
                        Foreground = System.Windows.Media.Brushes.Red,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    controlContainer.Children.Add(requiredMark);
                }

                // 添加到行
                parameterRow.Children.Add(label);
                parameterRow.Children.Add(controlContainer);

                // 添加到主面板
                DynamicParameterPanel.Children.Add(parameterRow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建参数控件失败 ({paramDef.Key}): {ex.Message}");
            }
        }

        /// <summary>
        /// 清空参数配置面板
        /// </summary>
        private void ClearParameterPanel()
        {
            DynamicParameterPanel.Children.Clear();
            _parameterControls.Clear();
            _parameterDefinitions.Clear();
            
            // 显示空状态提示
            EmptyParameterHint = new TextBlock
            {
                Text = "请选择协议类型以配置相关参数",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
            DynamicParameterPanel.Children.Add(EmptyParameterHint);
        }

        /// <summary>
        /// 设置参数控件的值
        /// </summary>
        /// <param name="parameters">参数值字典</param>
        private void SetParameterValues(Dictionary<string, object> parameters)
        {
            if (parameters == null || _parameterDefinitions == null)
                return;

            try
            {
                foreach (var paramDef in _parameterDefinitions)
                {
                    if (_parameterControls.TryGetValue(paramDef.Key, out UIElement control) &&
                        parameters.TryGetValue(paramDef.Key, out object value))
                    {
                        SetControlValue(control, paramDef, value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置参数值时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置单个控件的值
        /// </summary>
        /// <param name="control">控件</param>
        /// <param name="paramDef">参数定义</param>
        /// <param name="value">要设置的值</param>
        private void SetControlValue(UIElement control, ParameterDefinition paramDef, object value)
        {
            try
            {
                switch (paramDef.Type)
                {
                    case ParameterType.String:
                    case ParameterType.IPAddress:
                        if (control is TextBox textBox)
                            textBox.Text = value?.ToString() ?? "";
                        break;

                    case ParameterType.Integer:
                    case ParameterType.Port:
                        if (control is TextBox intTextBox && value != null)
                            intTextBox.Text = value.ToString();
                        break;

                    case ParameterType.Double:
                        if (control is TextBox doubleTextBox && value != null)
                            doubleTextBox.Text = value.ToString();
                        break;

                    case ParameterType.Boolean:
                        if (control is CheckBox checkBox && value is bool boolValue)
                            checkBox.IsChecked = boolValue;
                        break;

                    case ParameterType.ComboBox:
                        if (control is ComboBox comboBox && value != null)
                            comboBox.SelectedItem = value.ToString();
                        break;

                    case ParameterType.FilePath:
                        if (control is DockPanel dockPanel && value != null)
                        {
                            var fileTextBox = dockPanel.Children.OfType<TextBox>().FirstOrDefault();
                            if (fileTextBox != null)
                                fileTextBox.Text = value.ToString();
                        }
                        break;

                    case ParameterType.Password:
                        if (control is PasswordBox passwordBox && value != null)
                            passwordBox.Password = value.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置控件值失败: {ex.Message}");
            }
        }

        #endregion

        #region 配置模板管理

        /// <summary>
        /// 加载配置模板
        /// </summary>
        /// <param name="protocol">协议实例</param>
        private void LoadConfigTemplates(ICommunicationProtocol protocol)
        {
            try
            {
                var templates = protocol.GetConfigTemplates();
                if (templates != null && templates.Count > 0)
                {
                    // 添加默认空项
                    var templatesWithEmpty = new List<CommunicationConfigTemplate>
                    {
                        new CommunicationConfigTemplate { Name = "-- 选择模板 --", Description = "" }
                    };
                    templatesWithEmpty.AddRange(templates.OrderBy(t => t.IsRecommended ? 0 : 1).ThenBy(t => t.Name));

                    ConfigTemplateComboBox.ItemsSource = templatesWithEmpty;
                    ConfigTemplateComboBox.SelectedIndex = 0;
                    TemplateGroupBox.Visibility = Visibility.Visible;
                }
                else
                {
                    TemplateGroupBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置模板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用配置模板
        /// </summary>
        /// <param name="template">配置模板</param>
        private void ApplyTemplate(CommunicationConfigTemplate template)
        {
            try
            {
                if (template?.Parameters != null && template.Parameters.Count > 0)
                {
                    SetParameterValues(template.Parameters);
                    ShowInfo($"已应用配置模板: {template.Name}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"应用配置模板失败: {ex.Message}");
            }
        }

        #endregion

        #region 数据收集和验证

        /// <summary>
        /// 收集参数值
        /// </summary>
        /// <returns>参数字典</returns>
        private Dictionary<string, object> CollectParameters()
        {
            try
            {
                var parameters = new Dictionary<string, object>();

                foreach (var paramDef in _parameterDefinitions)
                {
                    if (_parameterControls.TryGetValue(paramDef.Key, out UIElement control))
                    {
                        var value = ParameterControlGenerator.ExtractValueFromControl(control, paramDef);
                        if (value != null)
                        {
                            parameters[paramDef.Key] = value;
                        }
                    }
                }

                return parameters;
            }
            catch (Exception ex)
            {
                ShowError($"收集参数时发生异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证并收集完整配置
        /// </summary>
        /// <returns>验证通过的配置对象</returns>
        private CommunicationConfig ValidateAndCollectConfiguration()
        {
            try
            {
                // 验证基本信息
                if (string.IsNullOrWhiteSpace(ConnectionNameTextBox.Text))
                {
                    ShowWarning("请输入连接名称");
                    ConnectionNameTextBox.Focus();
                    return null;
                }

                if (_selectedProtocol == null)
                {
                    ShowWarning("请选择协议类型");
                    ProtocolComboBox.Focus();
                    return null;
                }

                // 收集参数
                var parameters = CollectParameters();
                if (parameters == null)
                    return null;

                // 验证参数
                foreach (var paramDef in _parameterDefinitions)
                {
                    if (_parameterControls.TryGetValue(paramDef.Key, out UIElement control))
                    {
                        var validation = ParameterControlGenerator.ValidateControlValue(control, paramDef);
                        if (!validation.IsValid)
                        {
                            ShowWarning($"参数验证失败: {validation.ErrorMessage}");
                            if (control is Control controlElement)
                                controlElement.Focus();
                            return null;
                        }
                    }
                }

                // 创建配置对象
                var config = new CommunicationConfig
                {
                    Name = ConnectionNameTextBox.Text.Trim(),
                    ProtocolType = _selectedProtocol.ProtocolName,
                    ProtocolDisplayName = _selectedProtocol.DisplayName,
                    Parameters = parameters,
                    Description = $"基于 {_selectedProtocol.DisplayName} 协议的通讯连接"
                };

                // 最终验证
                var configValidation = config.ValidateConfig(_parameterDefinitions);
                if (!configValidation.IsValid)
                {
                    ShowWarning($"配置验证失败: {configValidation.ErrorMessage}");
                    return null;
                }

                return config;
            }
            catch (Exception ex)
            {
                ShowError($"验证配置时发生异常: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 生成默认连接名称
        /// </summary>
        /// <param name="protocol">协议实例</param>
        private void GenerateDefaultConnectionName(ICommunicationProtocol protocol)
        {
            try
            {
                var baseName = protocol.DisplayName;
                var counter = 1;
                var finalName = baseName;

                // 简单的名称冲突检查（这里可以扩展为检查现有连接列表）
                // 实际使用时应该检查MainWindow中已存在的连接名称
                while (counter <= 999)
                {
                    ConnectionNameTextBox.Text = finalName;
                    break; // 暂时不做冲突检查，直接使用基础名称
                    
                    // finalName = $"{baseName}_{counter}";
                    // counter++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成默认连接名称失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                bool hasProtocol = _selectedProtocol != null;
                bool hasName = !string.IsNullOrWhiteSpace(ConnectionNameTextBox.Text);
                
                TestConnectionButton.IsEnabled = hasProtocol && hasName;
                OKButton.IsEnabled = hasProtocol && hasName;
                ResetButton.IsEnabled = hasProtocol;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新UI状态失败: {ex.Message}");
            }
        }

        #endregion

        #region 消息显示方法

        /// <summary>
        /// 显示错误消息
        /// </summary>
        /// <param name="message">错误消息</param>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        /// <param name="message">警告消息</param>
        private void ShowWarning(string message)
        {
            MessageBox.Show(message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        /// <param name="message">信息消息</param>
        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 窗口事件

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                // 设置焦点到连接名称输入框
                ConnectionNameTextBox.Focus();
                ConnectionNameTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"窗口初始化事件处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 连接名称文本变化事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void ConnectionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateUI();
        }

        #endregion
    }
}