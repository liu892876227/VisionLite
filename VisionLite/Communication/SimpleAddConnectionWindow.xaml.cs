// Communication/SimpleAddConnectionWindow.xaml.cs
// 简化的添加通讯连接窗口 - 提供简单直观的连接配置界面
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VisionLite.Communication
{
    /// <summary>
    /// 简化的添加通讯连接窗口
    /// 提供简单直观的界面来配置TCP连接
    /// </summary>
    public partial class SimpleAddConnectionWindow : Window
    {
        #region 私有字段

        /// <summary>
        /// 当前的连接配置
        /// </summary>
        private SimpleConnectionConfig _config;

        /// <summary>
        /// IP地址输入框（TCP客户端使用）
        /// </summary>
        private TextBox _ipTextBox;

        /// <summary>
        /// 端口号输入框
        /// </summary>
        private TextBox _portTextBox;

        /// <summary>
        /// 是否为编辑模式
        /// </summary>
        public bool IsEditMode { get; private set; }

        #endregion

        #region 属性

        /// <summary>
        /// 配置结果，成功配置后返回给调用方
        /// </summary>
        public SimpleConnectionConfig Result { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 新建连接的构造函数
        /// </summary>
        public SimpleAddConnectionWindow()
        {
            InitializeComponent();
            IsEditMode = false;
            _config = new SimpleConnectionConfig
            {
                Name = "新连接",
                Type = CommunicationType.TcpClient,
                IpAddress = "127.0.0.1",
                Port = 8080
            };
            InitializeWindow();
        }

        /// <summary>
        /// 编辑现有连接的构造函数
        /// </summary>
        /// <param name="config">要编辑的配置</param>
        public SimpleAddConnectionWindow(SimpleConnectionConfig config) : this()
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            IsEditMode = true;
            _config = config.Clone();
            Title = "编辑通讯连接";
            LoadExistingConfig();
        }

        #endregion

        #region 窗口初始化

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            // 设置默认连接名称
            ConnectionNameTextBox.Text = _config.Name;
            
            // 默认选择TCP客户端
            ProtocolTypeComboBox.SelectedIndex = 0;
            
            // 初始化参数界面
            UpdateParameterPanel();
        }

        /// <summary>
        /// 加载现有配置（编辑模式）
        /// </summary>
        private void LoadExistingConfig()
        {
            ConnectionNameTextBox.Text = _config.Name;
            
            // 设置协议类型
            ProtocolTypeComboBox.SelectedIndex = _config.Type == CommunicationType.TcpClient ? 0 : 1;
            
            // 在参数面板更新后设置参数值
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_ipTextBox != null)
                    _ipTextBox.Text = _config.IpAddress;
                if (_portTextBox != null)
                    _portTextBox.Text = _config.Port.ToString();
                    
                UpdateButtonStates();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #endregion

        #region 界面更新

        /// <summary>
        /// 更新参数配置面板
        /// </summary>
        private void UpdateParameterPanel()
        {
            ParameterGrid.Children.Clear();
            ParameterGrid.RowDefinitions.Clear();

            var selectedItem = ProtocolTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            var protocolType = selectedItem.Tag.ToString();
            _config.Type = protocolType == "TcpClient" ? CommunicationType.TcpClient : CommunicationType.TcpServer;

            if (_config.Type == CommunicationType.TcpClient)
            {
                CreateTcpClientParameterPanel();
            }
            else
            {
                CreateTcpServerParameterPanel();
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// 创建TCP客户端参数面板
        /// </summary>
        private void CreateTcpClientParameterPanel()
        {
            // 创建两行：IP地址和端口
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // IP地址输入
            var ipLabel = new Label { Content = "IP地址:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(ipLabel, 0);
            Grid.SetColumn(ipLabel, 0);
            ParameterGrid.Children.Add(ipLabel);

            _ipTextBox = new TextBox { Text = _config.IpAddress, Style = (Style)Resources["InputStyle"] };
            _ipTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_ipTextBox, 0);
            Grid.SetColumn(_ipTextBox, 1);
            ParameterGrid.Children.Add(_ipTextBox);

            // 端口输入
            var portLabel = new Label { Content = "端口:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(portLabel, 1);
            Grid.SetColumn(portLabel, 0);
            ParameterGrid.Children.Add(portLabel);

            _portTextBox = new TextBox { Text = _config.Port.ToString(), Style = (Style)Resources["InputStyle"] };
            _portTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_portTextBox, 1);
            Grid.SetColumn(_portTextBox, 1);
            ParameterGrid.Children.Add(_portTextBox);
        }

        /// <summary>
        /// 创建TCP服务器参数面板
        /// </summary>
        private void CreateTcpServerParameterPanel()
        {
            // 创建一行：监听端口
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 清空IP输入框引用
            _ipTextBox = null;

            // 端口输入
            var portLabel = new Label { Content = "监听端口:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(portLabel, 0);
            Grid.SetColumn(portLabel, 0);
            ParameterGrid.Children.Add(portLabel);

            _portTextBox = new TextBox { Text = _config.Port.ToString(), Style = (Style)Resources["InputStyle"] };
            _portTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_portTextBox, 0);
            Grid.SetColumn(_portTextBox, 1);
            ParameterGrid.Children.Add(_portTextBox);
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            UpdateConfigFromUI();
            var validation = _config.Validate();
            
            OKButton.IsEnabled = validation.isValid;
            TestButton.IsEnabled = validation.isValid;
        }

        #endregion

        #region 数据更新

        /// <summary>
        /// 从界面更新配置对象
        /// </summary>
        private void UpdateConfigFromUI()
        {
            _config.Name = ConnectionNameTextBox.Text?.Trim();

            if (_ipTextBox != null)
                _config.IpAddress = _ipTextBox.Text?.Trim();

            if (_portTextBox != null && int.TryParse(_portTextBox.Text, out int port))
                _config.Port = port;
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 连接名称文本框内容改变事件
        /// </summary>
        private void ConnectionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        /// <summary>
        /// 协议类型选择改变事件
        /// </summary>
        private void ProtocolTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParameterPanel();
        }

        /// <summary>
        /// 参数输入框内容改变事件
        /// </summary>
        private void ParameterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        /// <summary>
        /// 测试连接按钮点击事件
        /// </summary>
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            TestButton.IsEnabled = false;
            TestButton.Content = "测试中...";

            try
            {
                UpdateConfigFromUI();
                var validation = _config.Validate();
                
                if (!validation.isValid)
                {
                    MessageBox.Show(validation.errorMessage, "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建临时连接进行测试
                using (var testComm = _config.CreateCommunication())
                {
                    var connectTask = testComm.OpenAsync();
                    var timeoutTask = Task.Delay(5000);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == connectTask)
                    {
                        bool connected = await connectTask;
                        if (connected)
                        {
                            MessageBox.Show("连接测试成功！", "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
                            testComm.Close();
                        }
                        else
                        {
                            MessageBox.Show("连接测试失败", "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("连接测试超时（5秒）", "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                        testComm.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试连接时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestButton.Content = "测试连接";
                TestButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateConfigFromUI();
                var validation = _config.Validate();
                
                if (!validation.isValid)
                {
                    MessageBox.Show(validation.errorMessage, "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Result = _config.Clone();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
}