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
        /// ModbusTCP单元ID输入框
        /// </summary>
        private TextBox _unitIdTextBox;

        /// <summary>
        /// ModbusTCP最大客户端数输入框
        /// </summary>
        private TextBox _maxClientsTextBox;

        /// <summary>
        /// ModbusTCP启用日志复选框
        /// </summary>
        private CheckBox _enableLoggingCheckBox;

        /// <summary>
        /// ModbusTCP字节序选择框
        /// </summary>
        private ComboBox _byteOrderComboBox;

        /// <summary>
        /// ModbusTCP客户端连接超时输入框
        /// </summary>
        private TextBox _connectionTimeoutTextBox;

        /// <summary>
        /// ModbusTCP客户端读取超时输入框
        /// </summary>
        private TextBox _readTimeoutTextBox;

        /// <summary>
        /// ModbusTCP客户端写入超时输入框
        /// </summary>
        private TextBox _writeTimeoutTextBox;

        /// <summary>
        /// ModbusTCP客户端轮询间隔输入框
        /// </summary>
        private TextBox _pollingIntervalTextBox;

        /// <summary>
        /// ModbusTCP客户端重连间隔输入框
        /// </summary>
        private TextBox _reconnectIntervalTextBox;

        /// <summary>
        /// ModbusTCP客户端启用自动重连复选框
        /// </summary>
        private CheckBox _autoReconnectCheckBox;

        /// <summary>
        /// ModbusTCP客户端启用轮询复选框
        /// </summary>
        private CheckBox _enablePollingCheckBox;

        /// <summary>
        /// 是否为编辑模式
        /// </summary>
        public bool IsEditMode { get; private set; }

        /// <summary>
        /// 用户是否手动修改过连接名称
        /// </summary>
        private bool _isNameManuallyChanged = false;

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
                Name = "TCP客户端",  // 默认名称改为协议类型名称
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
            
            // 如果是编辑模式，标记为已手动修改，防止名称被自动更改
            if (IsEditMode)
            {
                _isNameManuallyChanged = true;
            }
            
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
            
            // 设置协议类型（按照XAML中ComboBoxItem的顺序：TcpClient=0, TcpServer=1, UdpClient=2, UdpServer=3, ModbusTcpServer=4, ModbusTcpClient=5）
            ProtocolTypeComboBox.SelectedIndex = _config.Type switch
            {
                CommunicationType.TcpClient => 0,
                CommunicationType.TcpServer => 1,
                CommunicationType.UdpClient => 2,
                CommunicationType.UdpServer => 3,
                CommunicationType.ModbusTcpServer => 4,
                CommunicationType.ModbusTcpClient => 5,
                _ => 0
            };
            
            // 在参数面板更新后设置参数值
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_ipTextBox != null)
                    _ipTextBox.Text = _config.IpAddress;
                if (_portTextBox != null)
                    _portTextBox.Text = _config.Port.ToString();
                if (_unitIdTextBox != null && _config.Type == CommunicationType.ModbusTcpServer)
                    _unitIdTextBox.Text = _config.ModbusTcp.UnitId.ToString();
                if (_unitIdTextBox != null && _config.Type == CommunicationType.ModbusTcpClient)
                    _unitIdTextBox.Text = _config.ModbusTcpClient.UnitId.ToString();
                if (_maxClientsTextBox != null)
                    _maxClientsTextBox.Text = _config.ModbusTcp.MaxClients.ToString();
                if (_enableLoggingCheckBox != null && _config.Type == CommunicationType.ModbusTcpServer)
                    _enableLoggingCheckBox.IsChecked = _config.ModbusTcp.EnableLogging;
                if (_enableLoggingCheckBox != null && _config.Type == CommunicationType.ModbusTcpClient)
                    _enableLoggingCheckBox.IsChecked = _config.ModbusTcpClient.EnableLogging;
                if (_connectionTimeoutTextBox != null)
                    _connectionTimeoutTextBox.Text = _config.ModbusTcpClient.ConnectionTimeout.ToString();
                if (_readTimeoutTextBox != null)
                    _readTimeoutTextBox.Text = _config.ModbusTcpClient.ReadTimeout.ToString();
                if (_writeTimeoutTextBox != null)
                    _writeTimeoutTextBox.Text = _config.ModbusTcpClient.WriteTimeout.ToString();
                if (_pollingIntervalTextBox != null)
                    _pollingIntervalTextBox.Text = _config.ModbusTcpClient.PollingInterval.ToString();
                if (_reconnectIntervalTextBox != null)
                    _reconnectIntervalTextBox.Text = _config.ModbusTcpClient.ReconnectInterval.ToString();
                if (_autoReconnectCheckBox != null)
                    _autoReconnectCheckBox.IsChecked = _config.ModbusTcpClient.AutoReconnect;
                if (_enablePollingCheckBox != null)
                    _enablePollingCheckBox.IsChecked = _config.ModbusTcpClient.EnablePolling;
                    
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
            _config.Type = protocolType switch
            {
                "TcpClient" => CommunicationType.TcpClient,
                "TcpServer" => CommunicationType.TcpServer,
                "UdpClient" => CommunicationType.UdpClient,
                "UdpServer" => CommunicationType.UdpServer,
                "ModbusTcpServer" => CommunicationType.ModbusTcpServer,
                "ModbusTcpClient" => CommunicationType.ModbusTcpClient,
                _ => CommunicationType.TcpClient
            };

            // 根据协议类型设置默认端口
            if (_config.Type == CommunicationType.UdpClient || _config.Type == CommunicationType.UdpServer)
            {
                _config.Port = 8081; // UDP默认端口
            }
            else if (_config.Type == CommunicationType.ModbusTcpServer || _config.Type == CommunicationType.ModbusTcpClient)
            {
                _config.Port = 502; // ModbusTCP默认端口
            }
            else
            {
                _config.Port = 8080; // TCP默认端口
            }

            if (_config.Type == CommunicationType.TcpClient || _config.Type == CommunicationType.UdpClient)
            {
                CreateTcpClientParameterPanel(); // 客户端参数面板（IP+端口）
            }
            else if (_config.Type == CommunicationType.ModbusTcpServer)
            {
                CreateModbusTcpServerParameterPanel(); // ModbusTCP服务器参数面板
            }
            else if (_config.Type == CommunicationType.ModbusTcpClient)
            {
                CreateModbusTcpClientParameterPanel(); // ModbusTCP客户端参数面板
            }
            else
            {
                CreateTcpServerParameterPanel(); // 服务器参数面板（仅端口）
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
        /// 创建ModbusTCP服务器参数面板
        /// </summary>
        private void CreateModbusTcpServerParameterPanel()
        {
            // 创建六行：监听IP、监听端口、单元ID、最大客户端数、字节序、启用日志
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // IP地址输入
            var ipLabel = new Label { Content = "监听IP:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(ipLabel, 0);
            Grid.SetColumn(ipLabel, 0);
            ParameterGrid.Children.Add(ipLabel);

            _ipTextBox = new TextBox { Text = _config.IpAddress ?? "127.0.0.1", Style = (Style)Resources["InputStyle"] };
            _ipTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_ipTextBox, 0);
            Grid.SetColumn(_ipTextBox, 1);
            ParameterGrid.Children.Add(_ipTextBox);

            // 端口输入
            var portLabel = new Label { Content = "监听端口:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(portLabel, 1);
            Grid.SetColumn(portLabel, 0);
            ParameterGrid.Children.Add(portLabel);

            _portTextBox = new TextBox { Text = _config.Port.ToString(), Style = (Style)Resources["InputStyle"] };
            _portTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_portTextBox, 1);
            Grid.SetColumn(_portTextBox, 1);
            ParameterGrid.Children.Add(_portTextBox);

            // 单元ID输入
            var unitIdLabel = new Label { Content = "单元ID:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(unitIdLabel, 2);
            Grid.SetColumn(unitIdLabel, 0);
            ParameterGrid.Children.Add(unitIdLabel);

            _unitIdTextBox = new TextBox { Text = _config.ModbusTcp.UnitId.ToString(), Style = (Style)Resources["InputStyle"] };
            _unitIdTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_unitIdTextBox, 2);
            Grid.SetColumn(_unitIdTextBox, 1);
            ParameterGrid.Children.Add(_unitIdTextBox);

            // 最大客户端数输入
            var maxClientsLabel = new Label { Content = "最大客户端:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(maxClientsLabel, 3);
            Grid.SetColumn(maxClientsLabel, 0);
            ParameterGrid.Children.Add(maxClientsLabel);

            _maxClientsTextBox = new TextBox { Text = _config.ModbusTcp.MaxClients.ToString(), Style = (Style)Resources["InputStyle"] };
            _maxClientsTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_maxClientsTextBox, 3);
            Grid.SetColumn(_maxClientsTextBox, 1);
            ParameterGrid.Children.Add(_maxClientsTextBox);

            // 字节序选择
            var byteOrderLabel = new Label { Content = "字节序:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(byteOrderLabel, 4);
            Grid.SetColumn(byteOrderLabel, 0);
            ParameterGrid.Children.Add(byteOrderLabel);

            _byteOrderComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "ABCD（大端序，标准）", Tag = ByteOrder.ABCD });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "BADC（字节内交换）", Tag = ByteOrder.BADC });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "CDAB（寄存器交换）", Tag = ByteOrder.CDAB });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "DCBA（完全反序）", Tag = ByteOrder.DCBA });
            _byteOrderComboBox.SelectedIndex = (int)_config.ModbusTcp.DataByteOrder;
            _byteOrderComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_byteOrderComboBox, 4);
            Grid.SetColumn(_byteOrderComboBox, 1);
            ParameterGrid.Children.Add(_byteOrderComboBox);

            // 启用日志复选框
            _enableLoggingCheckBox = new CheckBox 
            { 
                Content = "启用请求日志", 
                IsChecked = _config.ModbusTcp.EnableLogging,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            _enableLoggingCheckBox.Checked += (s, e) => UpdateButtonStates();
            _enableLoggingCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            Grid.SetRow(_enableLoggingCheckBox, 5);
            Grid.SetColumn(_enableLoggingCheckBox, 1);
            ParameterGrid.Children.Add(_enableLoggingCheckBox);
        }

        /// <summary>
        /// 创建ModbusTCP客户端参数面板
        /// </summary>
        private void CreateModbusTcpClientParameterPanel()
        {
            // 创建八行：服务器IP、端口、单元ID、连接超时、读取超时、写入超时、轮询间隔、重连间隔
            // 加上三行复选框：启用日志、自动重连、启用轮询
            // 十行：IP、端口、单元ID、连接超时、读取超时、写入超时、轮询间隔、重连间隔、字节序、复选框面板
            for (int i = 0; i < 10; i++)
            {
                ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // IP地址输入
            var ipLabel = new Label { Content = "服务器IP:", Style = (Style)Resources["LabelStyle"] };
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

            // 单元ID输入
            var unitIdLabel = new Label { Content = "单元ID:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(unitIdLabel, 2);
            Grid.SetColumn(unitIdLabel, 0);
            ParameterGrid.Children.Add(unitIdLabel);

            _unitIdTextBox = new TextBox { Text = _config.ModbusTcpClient.UnitId.ToString(), Style = (Style)Resources["InputStyle"] };
            _unitIdTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_unitIdTextBox, 2);
            Grid.SetColumn(_unitIdTextBox, 1);
            ParameterGrid.Children.Add(_unitIdTextBox);

            // 连接超时输入
            var connTimeoutLabel = new Label { Content = "连接超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(connTimeoutLabel, 3);
            Grid.SetColumn(connTimeoutLabel, 0);
            ParameterGrid.Children.Add(connTimeoutLabel);

            _connectionTimeoutTextBox = new TextBox { Text = _config.ModbusTcpClient.ConnectionTimeout.ToString(), Style = (Style)Resources["InputStyle"] };
            _connectionTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_connectionTimeoutTextBox, 3);
            Grid.SetColumn(_connectionTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_connectionTimeoutTextBox);

            // 读取超时输入
            var readTimeoutLabel = new Label { Content = "读取超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(readTimeoutLabel, 4);
            Grid.SetColumn(readTimeoutLabel, 0);
            ParameterGrid.Children.Add(readTimeoutLabel);

            _readTimeoutTextBox = new TextBox { Text = _config.ModbusTcpClient.ReadTimeout.ToString(), Style = (Style)Resources["InputStyle"] };
            _readTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_readTimeoutTextBox, 4);
            Grid.SetColumn(_readTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_readTimeoutTextBox);

            // 写入超时输入
            var writeTimeoutLabel = new Label { Content = "写入超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(writeTimeoutLabel, 5);
            Grid.SetColumn(writeTimeoutLabel, 0);
            ParameterGrid.Children.Add(writeTimeoutLabel);

            _writeTimeoutTextBox = new TextBox { Text = _config.ModbusTcpClient.WriteTimeout.ToString(), Style = (Style)Resources["InputStyle"] };
            _writeTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_writeTimeoutTextBox, 5);
            Grid.SetColumn(_writeTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_writeTimeoutTextBox);

            // 轮询间隔输入
            var pollingIntervalLabel = new Label { Content = "轮询间隔(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(pollingIntervalLabel, 6);
            Grid.SetColumn(pollingIntervalLabel, 0);
            ParameterGrid.Children.Add(pollingIntervalLabel);

            _pollingIntervalTextBox = new TextBox { Text = _config.ModbusTcpClient.PollingInterval.ToString(), Style = (Style)Resources["InputStyle"] };
            _pollingIntervalTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_pollingIntervalTextBox, 6);
            Grid.SetColumn(_pollingIntervalTextBox, 1);
            ParameterGrid.Children.Add(_pollingIntervalTextBox);

            // 重连间隔输入
            var reconnectIntervalLabel = new Label { Content = "重连间隔(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(reconnectIntervalLabel, 7);
            Grid.SetColumn(reconnectIntervalLabel, 0);
            ParameterGrid.Children.Add(reconnectIntervalLabel);

            _reconnectIntervalTextBox = new TextBox { Text = _config.ModbusTcpClient.ReconnectInterval.ToString(), Style = (Style)Resources["InputStyle"] };
            _reconnectIntervalTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_reconnectIntervalTextBox, 7);
            Grid.SetColumn(_reconnectIntervalTextBox, 1);
            ParameterGrid.Children.Add(_reconnectIntervalTextBox);

            // 字节序选择
            var byteOrderLabel = new Label { Content = "字节序:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(byteOrderLabel, 8);
            Grid.SetColumn(byteOrderLabel, 0);
            ParameterGrid.Children.Add(byteOrderLabel);

            _byteOrderComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "ABCD（大端序，标准）", Tag = ByteOrder.ABCD });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "BADC（字节内交换）", Tag = ByteOrder.BADC });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "CDAB（寄存器交换）", Tag = ByteOrder.CDAB });
            _byteOrderComboBox.Items.Add(new ComboBoxItem { Content = "DCBA（完全反序）", Tag = ByteOrder.DCBA });
            _byteOrderComboBox.SelectedIndex = (int)_config.ModbusTcpClient.DataByteOrder;
            _byteOrderComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_byteOrderComboBox, 8);
            Grid.SetColumn(_byteOrderComboBox, 1);
            ParameterGrid.Children.Add(_byteOrderComboBox);

            // 创建一个用于放置复选框的StackPanel
            var checkBoxPanel = new StackPanel 
            { 
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 5, 0, 0)
            };

            // 启用日志复选框
            _enableLoggingCheckBox = new CheckBox 
            { 
                Content = "启用请求日志", 
                IsChecked = _config.ModbusTcpClient.EnableLogging,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _enableLoggingCheckBox.Checked += (s, e) => UpdateButtonStates();
            _enableLoggingCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            checkBoxPanel.Children.Add(_enableLoggingCheckBox);

            // 自动重连复选框
            _autoReconnectCheckBox = new CheckBox 
            { 
                Content = "启用自动重连", 
                IsChecked = _config.ModbusTcpClient.AutoReconnect,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _autoReconnectCheckBox.Checked += (s, e) => UpdateButtonStates();
            _autoReconnectCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            checkBoxPanel.Children.Add(_autoReconnectCheckBox);

            // 启用轮询复选框
            _enablePollingCheckBox = new CheckBox 
            { 
                Content = "启用数据轮询", 
                IsChecked = _config.ModbusTcpClient.EnablePolling,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _enablePollingCheckBox.Checked += (s, e) => UpdateButtonStates();
            _enablePollingCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            checkBoxPanel.Children.Add(_enablePollingCheckBox);

            // 将复选框面板添加到参数网格
            Grid.SetRow(checkBoxPanel, 9);
            Grid.SetColumn(checkBoxPanel, 0);
            Grid.SetColumnSpan(checkBoxPanel, 2);
            ParameterGrid.Children.Add(checkBoxPanel);
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

            // ModbusTCP服务器特有参数
            if (_config.Type == CommunicationType.ModbusTcpServer)
            {
                if (_unitIdTextBox != null && byte.TryParse(_unitIdTextBox.Text, out byte unitId))
                    _config.ModbusTcp.UnitId = unitId;

                if (_maxClientsTextBox != null && int.TryParse(_maxClientsTextBox.Text, out int maxClients))
                    _config.ModbusTcp.MaxClients = maxClients;

                if (_enableLoggingCheckBox != null)
                    _config.ModbusTcp.EnableLogging = _enableLoggingCheckBox.IsChecked ?? true;

                // 字节序配置
                if (_byteOrderComboBox != null && _byteOrderComboBox.SelectedItem is ComboBoxItem selectedItem)
                    _config.ModbusTcp.DataByteOrder = (ByteOrder)selectedItem.Tag;
            }

            // ModbusTCP客户端特有参数
            if (_config.Type == CommunicationType.ModbusTcpClient)
            {
                if (_unitIdTextBox != null && byte.TryParse(_unitIdTextBox.Text, out byte unitId))
                    _config.ModbusTcpClient.UnitId = unitId;

                if (_enableLoggingCheckBox != null)
                    _config.ModbusTcpClient.EnableLogging = _enableLoggingCheckBox.IsChecked ?? true;

                if (_connectionTimeoutTextBox != null && int.TryParse(_connectionTimeoutTextBox.Text, out int connTimeout))
                    _config.ModbusTcpClient.ConnectionTimeout = connTimeout;

                if (_readTimeoutTextBox != null && int.TryParse(_readTimeoutTextBox.Text, out int readTimeout))
                    _config.ModbusTcpClient.ReadTimeout = readTimeout;

                if (_writeTimeoutTextBox != null && int.TryParse(_writeTimeoutTextBox.Text, out int writeTimeout))
                    _config.ModbusTcpClient.WriteTimeout = writeTimeout;

                if (_pollingIntervalTextBox != null && int.TryParse(_pollingIntervalTextBox.Text, out int pollingInterval))
                    _config.ModbusTcpClient.PollingInterval = pollingInterval;

                if (_reconnectIntervalTextBox != null && int.TryParse(_reconnectIntervalTextBox.Text, out int reconnectInterval))
                    _config.ModbusTcpClient.ReconnectInterval = reconnectInterval;

                if (_autoReconnectCheckBox != null)
                    _config.ModbusTcpClient.AutoReconnect = _autoReconnectCheckBox.IsChecked ?? true;

                if (_enablePollingCheckBox != null)
                    _config.ModbusTcpClient.EnablePolling = _enablePollingCheckBox.IsChecked ?? true;

                // 字节序配置
                if (_byteOrderComboBox != null && _byteOrderComboBox.SelectedItem is ComboBoxItem selectedItem)
                    _config.ModbusTcpClient.DataByteOrder = (ByteOrder)selectedItem.Tag;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 连接名称文本框内容改变事件
        /// </summary>
        private void ConnectionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 设置用户已手动修改连接名称标志
            if (ConnectionNameTextBox.IsFocused || ConnectionNameTextBox.IsKeyboardFocused)
            {
                _isNameManuallyChanged = true;
            }
            
            UpdateButtonStates();
        }

        /// <summary>
        /// 协议类型选择改变事件
        /// </summary>
        private void ProtocolTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParameterPanel();
            
            // 如果用户没有手动修改过连接名称，则自动更新为协议类型名称
            if (!_isNameManuallyChanged)
            {
                var protocolName = GetProtocolDisplayName();
                if (!string.IsNullOrEmpty(protocolName))
                {
                    ConnectionNameTextBox.Text = protocolName;
                }
            }
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

        /// <summary>
        /// 获取当前选择的协议类型的显示名称
        /// </summary>
        /// <returns>协议类型的中文显示名称</returns>
        private string GetProtocolDisplayName()
        {
            var selectedItem = ProtocolTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag == null) return string.Empty;

            return selectedItem.Tag.ToString() switch
            {
                "TcpClient" => "TCP客户端",
                "TcpServer" => "TCP服务器",
                "UdpClient" => "UDP客户端", 
                "UdpServer" => "UDP服务器",
                "ModbusTcpServer" => "ModbusTCP服务器",
                "ModbusTcpClient" => "ModbusTCP客户端",
                _ => string.Empty
            };
        }

        #endregion
    }
}