// Communication/SimpleAddConnectionWindow.xaml.cs
// 简化的添加通讯连接窗口 - 提供简单直观的连接配置界面
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using S7.Net;

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
        /// ModbusTCP客户端重连间隔输入框
        /// </summary>
        private TextBox _reconnectIntervalTextBox;

        /// <summary>
        /// ModbusTCP客户端启用自动重连复选框
        /// </summary>
        private CheckBox _autoReconnectCheckBox;

        #region 串口相关控件字段

        /// <summary>
        /// 串口名称选择框
        /// </summary>
        private ComboBox _serialPortComboBox;

        /// <summary>
        /// 波特率选择框
        /// </summary>
        private ComboBox _baudRateComboBox;

        /// <summary>
        /// 数据位选择框
        /// </summary>
        private ComboBox _dataBitsComboBox;

        /// <summary>
        /// 停止位选择框
        /// </summary>
        private ComboBox _stopBitsComboBox;

        /// <summary>
        /// 奇偶校验选择框
        /// </summary>
        private ComboBox _parityComboBox;

        /// <summary>
        /// 流控制选择框
        /// </summary>
        private ComboBox _handshakeComboBox;

        /// <summary>
        /// 串口读取超时输入框
        /// </summary>
        private TextBox _serialReadTimeoutTextBox;

        /// <summary>
        /// 串口写入超时输入框
        /// </summary>
        private TextBox _serialWriteTimeoutTextBox;

        /// <summary>
        /// 数据格式选择框
        /// </summary>
        private ComboBox _dataFormatComboBox;

        /// <summary>
        /// 消息结束符输入框
        /// </summary>
        private TextBox _messageTerminatorTextBox;

        /// <summary>
        /// 串口重连间隔输入框
        /// </summary>
        private TextBox _serialReconnectIntervalTextBox;

        /// <summary>
        /// 串口启用日志复选框
        /// </summary>
        private CheckBox _serialEnableLoggingCheckBox;

        /// <summary>
        /// 串口自动重连复选框
        /// </summary>
        private CheckBox _serialAutoReconnectCheckBox;

        #endregion

        #region ADS相关控件字段

        /// <summary>
        /// ADS NetId输入框
        /// </summary>
        private TextBox _adsNetIdTextBox;

        /// <summary>
        /// ADS端口输入框
        /// </summary>
        private TextBox _adsPortTextBox;

        /// <summary>
        /// ADS超时时间输入框
        /// </summary>
        private TextBox _adsTimeoutTextBox;

        /// <summary>
        /// ADS显示名称输入框
        /// </summary>
        private TextBox _adsDisplayNameTextBox;

        /// <summary>
        /// ADS符号访问复选框
        /// </summary>
        private CheckBox _adsUseSymbolicAccessCheckBox;

        #endregion

        #region S7相关控件字段

        /// <summary>
        /// S7 PLC IP地址输入框
        /// </summary>
        private TextBox _s7IpTextBox;

        /// <summary>
        /// S7 CPU类型选择框
        /// </summary>
        private ComboBox _s7CpuTypeComboBox;

        /// <summary>
        /// S7机架号输入框
        /// </summary>
        private TextBox _s7RackTextBox;

        /// <summary>
        /// S7槽位号输入框
        /// </summary>
        private TextBox _s7SlotTextBox;

        /// <summary>
        /// S7连接超时输入框
        /// </summary>
        private TextBox _s7TimeoutTextBox;

        #endregion

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
            
            // 设置协议类型（按照XAML中ComboBoxItem的顺序：TcpClient=0, TcpServer=1, UdpClient=2, UdpServer=3, ModbusTcpServer=4, ModbusTcpClient=5, SerialPort=6, AdsClient=7, S7Client=8）
            ProtocolTypeComboBox.SelectedIndex = _config.Type switch
            {
                CommunicationType.TcpClient => 0,
                CommunicationType.TcpServer => 1,
                CommunicationType.UdpClient => 2,
                CommunicationType.UdpServer => 3,
                CommunicationType.ModbusTcpServer => 4,
                CommunicationType.ModbusTcpClient => 5,
                CommunicationType.SerialPort => 6,
                CommunicationType.AdsClient => 7,
                CommunicationType.S7Client => 8,
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
                if (_reconnectIntervalTextBox != null)
                    _reconnectIntervalTextBox.Text = _config.ModbusTcpClient.ReconnectInterval.ToString();
                if (_autoReconnectCheckBox != null)
                    _autoReconnectCheckBox.IsChecked = _config.ModbusTcpClient.AutoReconnect;

                // 串口配置加载
                if (_serialPortComboBox != null && _config.Type == CommunicationType.SerialPort)
                {
                    // 设置串口名称
                    for (int i = 0; i < _serialPortComboBox.Items.Count; i++)
                    {
                        if (((ComboBoxItem)_serialPortComboBox.Items[i]).Content.ToString() == _config.Serial.PortName)
                        {
                            _serialPortComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (_baudRateComboBox != null)
                {
                    // 设置波特率
                    for (int i = 0; i < _baudRateComboBox.Items.Count; i++)
                    {
                        if (((ComboBoxItem)_baudRateComboBox.Items[i]).Content.ToString() == _config.Serial.BaudRate.ToString())
                        {
                            _baudRateComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (_dataBitsComboBox != null)
                    _dataBitsComboBox.SelectedIndex = _config.Serial.DataBits - 5; // 5,6,7,8 -> 0,1,2,3
                if (_stopBitsComboBox != null)
                    _stopBitsComboBox.SelectedIndex = (int)_config.Serial.StopBits;
                if (_parityComboBox != null)
                    _parityComboBox.SelectedIndex = (int)_config.Serial.Parity;
                if (_handshakeComboBox != null)
                    _handshakeComboBox.SelectedIndex = (int)_config.Serial.Handshake;
                if (_serialReadTimeoutTextBox != null)
                    _serialReadTimeoutTextBox.Text = _config.Serial.ReadTimeout.ToString();
                if (_serialWriteTimeoutTextBox != null)
                    _serialWriteTimeoutTextBox.Text = _config.Serial.WriteTimeout.ToString();
                if (_dataFormatComboBox != null)
                    _dataFormatComboBox.SelectedIndex = (int)_config.Serial.DataFormat;
                if (_messageTerminatorTextBox != null)
                    _messageTerminatorTextBox.Text = _config.Serial.MessageTerminator.Replace("\r", "\\r").Replace("\n", "\\n");
                if (_serialReconnectIntervalTextBox != null)
                    _serialReconnectIntervalTextBox.Text = _config.Serial.ReconnectInterval.ToString();
                if (_serialEnableLoggingCheckBox != null)
                    _serialEnableLoggingCheckBox.IsChecked = _config.Serial.EnableLogging;
                if (_serialAutoReconnectCheckBox != null)
                    _serialAutoReconnectCheckBox.IsChecked = _config.Serial.AutoReconnect;

                // ADS参数
                if (_adsNetIdTextBox != null)
                    _adsNetIdTextBox.Text = _config.Ads.TargetAmsNetId;
                if (_adsPortTextBox != null)
                    _adsPortTextBox.Text = _config.Ads.TargetAmsPort.ToString();
                if (_adsTimeoutTextBox != null)
                    _adsTimeoutTextBox.Text = _config.Ads.Timeout.ToString();
                if (_adsDisplayNameTextBox != null)
                    _adsDisplayNameTextBox.Text = _config.Ads.DisplayName;
                if (_adsUseSymbolicAccessCheckBox != null)
                    _adsUseSymbolicAccessCheckBox.IsChecked = _config.Ads.UseSymbolicAccess;
                    
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
                "SerialPort" => CommunicationType.SerialPort,
                "AdsClient" => CommunicationType.AdsClient,
                "S7Client" => CommunicationType.S7Client,
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
            else if (_config.Type == CommunicationType.SerialPort)
            {
                CreateSerialPortParameterPanel(); // 串口参数面板
            }
            else if (_config.Type == CommunicationType.AdsClient)
            {
                CreateAdsParameterPanel(); // ADS通讯参数面板
            }
            else if (_config.Type == CommunicationType.S7Client)
            {
                CreateS7ParameterPanel(); // 西门子S7 PLC参数面板
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
            // 创建九行：服务器IP、端口、单元ID、连接超时、读取超时、写入超时、重连间隔、字节序、复选框面板
            for (int i = 0; i < 9; i++)
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

            // 重连间隔输入
            var reconnectIntervalLabel = new Label { Content = "重连间隔(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(reconnectIntervalLabel, 6);
            Grid.SetColumn(reconnectIntervalLabel, 0);
            ParameterGrid.Children.Add(reconnectIntervalLabel);

            _reconnectIntervalTextBox = new TextBox { Text = _config.ModbusTcpClient.ReconnectInterval.ToString(), Style = (Style)Resources["InputStyle"] };
            _reconnectIntervalTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_reconnectIntervalTextBox, 6);
            Grid.SetColumn(_reconnectIntervalTextBox, 1);
            ParameterGrid.Children.Add(_reconnectIntervalTextBox);

            // 字节序选择
            var byteOrderLabel = new Label { Content = "字节序:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(byteOrderLabel, 7);
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
            Grid.SetRow(_byteOrderComboBox, 7);
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


            // 将复选框面板添加到参数网格
            Grid.SetRow(checkBoxPanel, 8);
            Grid.SetColumn(checkBoxPanel, 0);
            Grid.SetColumnSpan(checkBoxPanel, 2);
            ParameterGrid.Children.Add(checkBoxPanel);
        }

        /// <summary>
        /// 创建串口参数面板
        /// </summary>
        private void CreateSerialPortParameterPanel()
        {
            // 创建十二行：串口名称、波特率、数据位、停止位、奇偶校验、流控制、读取超时、写入超时、数据格式、消息结束符、重连间隔、复选框面板
            for (int i = 0; i < 12; i++)
            {
                ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 串口名称选择
            var portLabel = new Label { Content = "串口名称:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(portLabel, 0);
            Grid.SetColumn(portLabel, 0);
            ParameterGrid.Children.Add(portLabel);

            var portPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _serialPortComboBox = new ComboBox 
            { 
                Width = 100,
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 5, 5)
            };
            
            // 添加系统可用的串口
            RefreshSerialPorts();
            _serialPortComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            portPanel.Children.Add(_serialPortComboBox);

            // 刷新串口按钮
            var refreshButton = new Button 
            { 
                Content = "刷新", 
                Width = 50, 
                Height = 25, 
                Margin = new Thickness(0, 5, 0, 5) 
            };
            refreshButton.Click += (s, e) => RefreshSerialPorts();
            portPanel.Children.Add(refreshButton);

            Grid.SetRow(portPanel, 0);
            Grid.SetColumn(portPanel, 1);
            ParameterGrid.Children.Add(portPanel);

            // 波特率选择
            var baudRateLabel = new Label { Content = "波特率:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(baudRateLabel, 1);
            Grid.SetColumn(baudRateLabel, 0);
            ParameterGrid.Children.Add(baudRateLabel);

            _baudRateComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            // 常用波特率
            string[] baudRates = { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600" };
            foreach (var rate in baudRates)
            {
                _baudRateComboBox.Items.Add(new ComboBoxItem { Content = rate });
            }
            _baudRateComboBox.SelectedIndex = 3; // 默认9600
            _baudRateComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_baudRateComboBox, 1);
            Grid.SetColumn(_baudRateComboBox, 1);
            ParameterGrid.Children.Add(_baudRateComboBox);

            // 数据位选择
            var dataBitsLabel = new Label { Content = "数据位:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(dataBitsLabel, 2);
            Grid.SetColumn(dataBitsLabel, 0);
            ParameterGrid.Children.Add(dataBitsLabel);

            _dataBitsComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            for (int i = 5; i <= 8; i++)
            {
                _dataBitsComboBox.Items.Add(new ComboBoxItem { Content = i.ToString() });
            }
            _dataBitsComboBox.SelectedIndex = 3; // 默认8位
            _dataBitsComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_dataBitsComboBox, 2);
            Grid.SetColumn(_dataBitsComboBox, 1);
            ParameterGrid.Children.Add(_dataBitsComboBox);

            // 停止位选择
            var stopBitsLabel = new Label { Content = "停止位:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(stopBitsLabel, 3);
            Grid.SetColumn(stopBitsLabel, 0);
            ParameterGrid.Children.Add(stopBitsLabel);

            _stopBitsComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _stopBitsComboBox.Items.Add(new ComboBoxItem { Content = "1", Tag = System.IO.Ports.StopBits.One });
            _stopBitsComboBox.Items.Add(new ComboBoxItem { Content = "1.5", Tag = System.IO.Ports.StopBits.OnePointFive });
            _stopBitsComboBox.Items.Add(new ComboBoxItem { Content = "2", Tag = System.IO.Ports.StopBits.Two });
            _stopBitsComboBox.SelectedIndex = 0; // 默认1位
            _stopBitsComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_stopBitsComboBox, 3);
            Grid.SetColumn(_stopBitsComboBox, 1);
            ParameterGrid.Children.Add(_stopBitsComboBox);

            // 奇偶校验选择
            var parityLabel = new Label { Content = "奇偶校验:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(parityLabel, 4);
            Grid.SetColumn(parityLabel, 0);
            ParameterGrid.Children.Add(parityLabel);

            _parityComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _parityComboBox.Items.Add(new ComboBoxItem { Content = "None", Tag = System.IO.Ports.Parity.None });
            _parityComboBox.Items.Add(new ComboBoxItem { Content = "Odd", Tag = System.IO.Ports.Parity.Odd });
            _parityComboBox.Items.Add(new ComboBoxItem { Content = "Even", Tag = System.IO.Ports.Parity.Even });
            _parityComboBox.Items.Add(new ComboBoxItem { Content = "Mark", Tag = System.IO.Ports.Parity.Mark });
            _parityComboBox.Items.Add(new ComboBoxItem { Content = "Space", Tag = System.IO.Ports.Parity.Space });
            _parityComboBox.SelectedIndex = 0; // 默认无校验
            _parityComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_parityComboBox, 4);
            Grid.SetColumn(_parityComboBox, 1);
            ParameterGrid.Children.Add(_parityComboBox);

            // 流控制选择
            var handshakeLabel = new Label { Content = "流控制:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(handshakeLabel, 5);
            Grid.SetColumn(handshakeLabel, 0);
            ParameterGrid.Children.Add(handshakeLabel);

            _handshakeComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _handshakeComboBox.Items.Add(new ComboBoxItem { Content = "None", Tag = System.IO.Ports.Handshake.None });
            _handshakeComboBox.Items.Add(new ComboBoxItem { Content = "XOnXOff", Tag = System.IO.Ports.Handshake.XOnXOff });
            _handshakeComboBox.Items.Add(new ComboBoxItem { Content = "RequestToSend", Tag = System.IO.Ports.Handshake.RequestToSend });
            _handshakeComboBox.Items.Add(new ComboBoxItem { Content = "RequestToSendXOnXOff", Tag = System.IO.Ports.Handshake.RequestToSendXOnXOff });
            _handshakeComboBox.SelectedIndex = 0; // 默认无流控制
            _handshakeComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_handshakeComboBox, 5);
            Grid.SetColumn(_handshakeComboBox, 1);
            ParameterGrid.Children.Add(_handshakeComboBox);

            // 读取超时
            var readTimeoutLabel = new Label { Content = "读取超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(readTimeoutLabel, 6);
            Grid.SetColumn(readTimeoutLabel, 0);
            ParameterGrid.Children.Add(readTimeoutLabel);

            _serialReadTimeoutTextBox = new TextBox { Text = _config.Serial.ReadTimeout.ToString(), Style = (Style)Resources["InputStyle"] };
            _serialReadTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_serialReadTimeoutTextBox, 6);
            Grid.SetColumn(_serialReadTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_serialReadTimeoutTextBox);

            // 写入超时
            var writeTimeoutLabel = new Label { Content = "写入超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(writeTimeoutLabel, 7);
            Grid.SetColumn(writeTimeoutLabel, 0);
            ParameterGrid.Children.Add(writeTimeoutLabel);

            _serialWriteTimeoutTextBox = new TextBox { Text = _config.Serial.WriteTimeout.ToString(), Style = (Style)Resources["InputStyle"] };
            _serialWriteTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_serialWriteTimeoutTextBox, 7);
            Grid.SetColumn(_serialWriteTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_serialWriteTimeoutTextBox);

            // 数据格式选择
            var dataFormatLabel = new Label { Content = "数据格式:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(dataFormatLabel, 8);
            Grid.SetColumn(dataFormatLabel, 0);
            ParameterGrid.Children.Add(dataFormatLabel);

            _dataFormatComboBox = new ComboBox 
            { 
                Height = 25, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _dataFormatComboBox.Items.Add(new ComboBoxItem { Content = "文本格式", Tag = SerialDataFormat.Text });
            _dataFormatComboBox.Items.Add(new ComboBoxItem { Content = "十六进制", Tag = SerialDataFormat.Hex });
            _dataFormatComboBox.Items.Add(new ComboBoxItem { Content = "二进制", Tag = SerialDataFormat.Binary });
            _dataFormatComboBox.SelectedIndex = 0; // 默认文本格式
            _dataFormatComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_dataFormatComboBox, 8);
            Grid.SetColumn(_dataFormatComboBox, 1);
            ParameterGrid.Children.Add(_dataFormatComboBox);

            // 消息结束符
            var terminatorLabel = new Label { Content = "消息结束符:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(terminatorLabel, 9);
            Grid.SetColumn(terminatorLabel, 0);
            ParameterGrid.Children.Add(terminatorLabel);

            _messageTerminatorTextBox = new TextBox { Text = "\\r\\n", Style = (Style)Resources["InputStyle"] };
            _messageTerminatorTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_messageTerminatorTextBox, 9);
            Grid.SetColumn(_messageTerminatorTextBox, 1);
            ParameterGrid.Children.Add(_messageTerminatorTextBox);

            // 重连间隔
            var reconnectLabel = new Label { Content = "重连间隔(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(reconnectLabel, 10);
            Grid.SetColumn(reconnectLabel, 0);
            ParameterGrid.Children.Add(reconnectLabel);

            _serialReconnectIntervalTextBox = new TextBox { Text = _config.Serial.ReconnectInterval.ToString(), Style = (Style)Resources["InputStyle"] };
            _serialReconnectIntervalTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_serialReconnectIntervalTextBox, 10);
            Grid.SetColumn(_serialReconnectIntervalTextBox, 1);
            ParameterGrid.Children.Add(_serialReconnectIntervalTextBox);

            // 复选框面板
            var serialCheckBoxPanel = new StackPanel 
            { 
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 5, 0, 0)
            };

            // 启用日志复选框
            _serialEnableLoggingCheckBox = new CheckBox 
            { 
                Content = "启用日志记录", 
                IsChecked = _config.Serial.EnableLogging,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _serialEnableLoggingCheckBox.Checked += (s, e) => UpdateButtonStates();
            _serialEnableLoggingCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            serialCheckBoxPanel.Children.Add(_serialEnableLoggingCheckBox);

            // 自动重连复选框
            _serialAutoReconnectCheckBox = new CheckBox 
            { 
                Content = "启用自动重连", 
                IsChecked = _config.Serial.AutoReconnect,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _serialAutoReconnectCheckBox.Checked += (s, e) => UpdateButtonStates();
            _serialAutoReconnectCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            serialCheckBoxPanel.Children.Add(_serialAutoReconnectCheckBox);

            Grid.SetRow(serialCheckBoxPanel, 11);
            Grid.SetColumn(serialCheckBoxPanel, 0);
            Grid.SetColumnSpan(serialCheckBoxPanel, 2);
            ParameterGrid.Children.Add(serialCheckBoxPanel);
        }

        /// <summary>
        /// 刷新串口列表
        /// </summary>
        private void RefreshSerialPorts()
        {
            if (_serialPortComboBox == null) return;

            string selectedPort = null;
            if (_serialPortComboBox.SelectedItem != null)
            {
                selectedPort = ((ComboBoxItem)_serialPortComboBox.SelectedItem).Content.ToString();
            }

            _serialPortComboBox.Items.Clear();

            try
            {
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                if (ports.Length == 0)
                {
                    _serialPortComboBox.Items.Add(new ComboBoxItem { Content = "无可用串口" });
                    _serialPortComboBox.SelectedIndex = 0;
                    _serialPortComboBox.IsEnabled = false;
                }
                else
                {
                    _serialPortComboBox.IsEnabled = true;
                    foreach (string port in ports.OrderBy(p => p))
                    {
                        _serialPortComboBox.Items.Add(new ComboBoxItem { Content = port });
                    }

                    // 尝试恢复之前的选择
                    bool found = false;
                    if (!string.IsNullOrEmpty(selectedPort))
                    {
                        for (int i = 0; i < _serialPortComboBox.Items.Count; i++)
                        {
                            if (((ComboBoxItem)_serialPortComboBox.Items[i]).Content.ToString() == selectedPort)
                            {
                                _serialPortComboBox.SelectedIndex = i;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        _serialPortComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _serialPortComboBox.Items.Add(new ComboBoxItem { Content = $"获取串口失败: {ex.Message}" });
                _serialPortComboBox.SelectedIndex = 0;
                _serialPortComboBox.IsEnabled = false;
            }
        }

        /// <summary>
        /// 创建ADS通讯参数面板
        /// </summary>
        private void CreateAdsParameterPanel()
        {
            // 创建五行：NetId、端口、超时时间、显示名称、符号访问
            for (int i = 0; i < 5; i++)
            {
                ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // AMS NetId输入
            var netIdLabel = new Label { Content = "AMS NetId:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(netIdLabel, 0);
            Grid.SetColumn(netIdLabel, 0);
            ParameterGrid.Children.Add(netIdLabel);

            _adsNetIdTextBox = new TextBox 
            { 
                Text = _config.Ads.TargetAmsNetId, 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "格式：IP地址.1.1（如：192.168.1.100.1.1）"
            };
            _adsNetIdTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_adsNetIdTextBox, 0);
            Grid.SetColumn(_adsNetIdTextBox, 1);
            ParameterGrid.Children.Add(_adsNetIdTextBox);

            // AMS端口输入
            var adsPortLabel = new Label { Content = "AMS端口:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(adsPortLabel, 1);
            Grid.SetColumn(adsPortLabel, 0);
            ParameterGrid.Children.Add(adsPortLabel);

            _adsPortTextBox = new TextBox 
            { 
                Text = _config.Ads.TargetAmsPort.ToString(), 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "PLC Runtime通常使用801"
            };
            _adsPortTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_adsPortTextBox, 1);
            Grid.SetColumn(_adsPortTextBox, 1);
            ParameterGrid.Children.Add(_adsPortTextBox);

            // 超时时间输入
            var timeoutLabel = new Label { Content = "超时时间(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(timeoutLabel, 2);
            Grid.SetColumn(timeoutLabel, 0);
            ParameterGrid.Children.Add(timeoutLabel);

            _adsTimeoutTextBox = new TextBox 
            { 
                Text = _config.Ads.Timeout.ToString(), 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "连接和读写操作的超时时间，建议3000-10000毫秒"
            };
            _adsTimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_adsTimeoutTextBox, 2);
            Grid.SetColumn(_adsTimeoutTextBox, 1);
            ParameterGrid.Children.Add(_adsTimeoutTextBox);

            // 显示名称输入
            var displayNameLabel = new Label { Content = "显示名称:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(displayNameLabel, 3);
            Grid.SetColumn(displayNameLabel, 0);
            ParameterGrid.Children.Add(displayNameLabel);

            _adsDisplayNameTextBox = new TextBox 
            { 
                Text = _config.Ads.DisplayName, 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "用于识别此PLC连接的友好名称"
            };
            _adsDisplayNameTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_adsDisplayNameTextBox, 3);
            Grid.SetColumn(_adsDisplayNameTextBox, 1);
            ParameterGrid.Children.Add(_adsDisplayNameTextBox);

            // 符号访问复选框
            var checkboxPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            _adsUseSymbolicAccessCheckBox = new CheckBox 
            { 
                Content = "启用符号访问",
                IsChecked = _config.Ads.UseSymbolicAccess,
                ToolTip = "启用后可通过变量名直接访问PLC变量（推荐）",
                Margin = new Thickness(0, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            _adsUseSymbolicAccessCheckBox.Checked += (s, e) => UpdateButtonStates();
            _adsUseSymbolicAccessCheckBox.Unchecked += (s, e) => UpdateButtonStates();
            checkboxPanel.Children.Add(_adsUseSymbolicAccessCheckBox);

            // 添加连接测试快捷按钮（用于ADS连接验证）
            var quickTestButton = new Button
            {
                Content = "快速测试",
                Width = 80,
                Height = 25,
                Margin = new Thickness(10, 5, 0, 5),
                ToolTip = "仅测试ADS连接，不验证变量访问"
            };
            quickTestButton.Click += async (s, e) => await QuickTestAdsConnection();
            checkboxPanel.Children.Add(quickTestButton);

            Grid.SetRow(checkboxPanel, 4);
            Grid.SetColumn(checkboxPanel, 1);
            ParameterGrid.Children.Add(checkboxPanel);
        }

        /// <summary>
        /// 创建S7 PLC参数面板
        /// </summary>
        private void CreateS7ParameterPanel()
        {
            // 创建四行：IP地址、CPU类型、机架/槽位、连接超时
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            ParameterGrid.ColumnDefinitions.Clear();
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            ParameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // IP地址输入
            var ipLabel = new Label { Content = "PLC IP地址:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(ipLabel, 0);
            Grid.SetColumn(ipLabel, 0);
            ParameterGrid.Children.Add(ipLabel);

            _s7IpTextBox = new TextBox 
            { 
                Text = _config.S7.IpAddress ?? "192.168.1.10", 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "西门子PLC的IP地址"
            };
            _s7IpTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_s7IpTextBox, 0);
            Grid.SetColumn(_s7IpTextBox, 1);
            ParameterGrid.Children.Add(_s7IpTextBox);

            // CPU类型选择
            var cpuTypeLabel = new Label { Content = "CPU类型:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(cpuTypeLabel, 1);
            Grid.SetColumn(cpuTypeLabel, 0);
            ParameterGrid.Children.Add(cpuTypeLabel);

            _s7CpuTypeComboBox = new ComboBox
            {
                Height = 25,
                Margin = new Thickness(0, 5, 0, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            _s7CpuTypeComboBox.Items.Add(new ComboBoxItem { Content = "S7-200", Tag = CpuType.S7200 });
            _s7CpuTypeComboBox.Items.Add(new ComboBoxItem { Content = "S7-300", Tag = CpuType.S7300 });
            _s7CpuTypeComboBox.Items.Add(new ComboBoxItem { Content = "S7-400", Tag = CpuType.S7400 });
            _s7CpuTypeComboBox.Items.Add(new ComboBoxItem { Content = "S7-1200", Tag = CpuType.S71200 });
            _s7CpuTypeComboBox.Items.Add(new ComboBoxItem { Content = "S7-1500", Tag = CpuType.S71500, IsSelected = true });
            _s7CpuTypeComboBox.SelectionChanged += (s, e) => UpdateButtonStates();
            Grid.SetRow(_s7CpuTypeComboBox, 1);
            Grid.SetColumn(_s7CpuTypeComboBox, 1);
            ParameterGrid.Children.Add(_s7CpuTypeComboBox);

            // 机架和槽位（水平排列）
            var rackSlotLabel = new Label { Content = "机架/槽位:", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(rackSlotLabel, 2);
            Grid.SetColumn(rackSlotLabel, 0);
            ParameterGrid.Children.Add(rackSlotLabel);

            var rackSlotPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            _s7RackTextBox = new TextBox 
            { 
                Text = _config.S7.Rack.ToString(), 
                Width = 50, 
                Height = 25, 
                Margin = new Thickness(0, 5, 10, 5),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "机架号，通常为0"
            };
            _s7RackTextBox.TextChanged += ParameterTextBox_TextChanged;
            rackSlotPanel.Children.Add(_s7RackTextBox);

            rackSlotPanel.Children.Add(new Label { Content = "/", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) });

            _s7SlotTextBox = new TextBox 
            { 
                Text = _config.S7.Slot.ToString(), 
                Width = 50, 
                Height = 25, 
                Margin = new Thickness(10, 5, 0, 5),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "槽位号，S7-1200/1500通常为1"
            };
            _s7SlotTextBox.TextChanged += ParameterTextBox_TextChanged;
            rackSlotPanel.Children.Add(_s7SlotTextBox);

            Grid.SetRow(rackSlotPanel, 2);
            Grid.SetColumn(rackSlotPanel, 1);
            ParameterGrid.Children.Add(rackSlotPanel);

            // 连接超时
            var timeoutLabel = new Label { Content = "连接超时(ms):", Style = (Style)Resources["LabelStyle"] };
            Grid.SetRow(timeoutLabel, 3);
            Grid.SetColumn(timeoutLabel, 0);
            ParameterGrid.Children.Add(timeoutLabel);

            _s7TimeoutTextBox = new TextBox 
            { 
                Text = _config.S7.ConnectionTimeout.ToString(), 
                Style = (Style)Resources["InputStyle"],
                ToolTip = "连接超时时间，单位毫秒"
            };
            _s7TimeoutTextBox.TextChanged += ParameterTextBox_TextChanged;
            Grid.SetRow(_s7TimeoutTextBox, 3);
            Grid.SetColumn(_s7TimeoutTextBox, 1);
            ParameterGrid.Children.Add(_s7TimeoutTextBox);
        }

        /// <summary>
        /// 快速测试ADS连接
        /// </summary>
        private async Task QuickTestAdsConnection()
        {
            try
            {
                // 从UI更新配置
                UpdateConfigFromUI();
                
                // 验证配置
                var validation = _config.Validate();
                if (!validation.isValid)
                {
                    MessageBox.Show($"配置验证失败：{validation.errorMessage}", "配置错误", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 执行快速连接测试
                bool result = await AdsConnectionTest.QuickConnectionTest(_config.Ads.TargetAmsNetId, _config.Ads.TargetAmsPort);
                
                if (result)
                {
                    MessageBox.Show("ADS连接测试成功！", "测试结果", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("ADS连接测试失败，请检查NetId、端口和网络连接。", "测试结果", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试过程中发生异常：{ex.Message}", "测试异常", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                if (_reconnectIntervalTextBox != null && int.TryParse(_reconnectIntervalTextBox.Text, out int reconnectInterval))
                    _config.ModbusTcpClient.ReconnectInterval = reconnectInterval;

                if (_autoReconnectCheckBox != null)
                    _config.ModbusTcpClient.AutoReconnect = _autoReconnectCheckBox.IsChecked ?? true;

                // 字节序配置
                if (_byteOrderComboBox != null && _byteOrderComboBox.SelectedItem is ComboBoxItem selectedItem)
                    _config.ModbusTcpClient.DataByteOrder = (ByteOrder)selectedItem.Tag;
            }

            // 串口特有参数
            if (_config.Type == CommunicationType.SerialPort)
            {
                if (_serialPortComboBox?.SelectedItem is ComboBoxItem portItem && !portItem.Content.ToString().Contains("无可用串口"))
                    _config.Serial.PortName = portItem.Content.ToString();

                if (_baudRateComboBox?.SelectedItem is ComboBoxItem baudItem)
                    if (int.TryParse(baudItem.Content.ToString(), out int baudRate))
                        _config.Serial.BaudRate = baudRate;

                if (_dataBitsComboBox?.SelectedItem is ComboBoxItem dataBitsItem)
                    if (int.TryParse(dataBitsItem.Content.ToString(), out int dataBits))
                        _config.Serial.DataBits = dataBits;

                if (_stopBitsComboBox?.SelectedItem is ComboBoxItem stopBitsItem)
                    _config.Serial.StopBits = (System.IO.Ports.StopBits)stopBitsItem.Tag;

                if (_parityComboBox?.SelectedItem is ComboBoxItem parityItem)
                    _config.Serial.Parity = (System.IO.Ports.Parity)parityItem.Tag;

                if (_handshakeComboBox?.SelectedItem is ComboBoxItem handshakeItem)
                    _config.Serial.Handshake = (System.IO.Ports.Handshake)handshakeItem.Tag;

                if (_serialReadTimeoutTextBox != null && int.TryParse(_serialReadTimeoutTextBox.Text, out int serialReadTimeout))
                    _config.Serial.ReadTimeout = serialReadTimeout;

                if (_serialWriteTimeoutTextBox != null && int.TryParse(_serialWriteTimeoutTextBox.Text, out int serialWriteTimeout))
                    _config.Serial.WriteTimeout = serialWriteTimeout;

                if (_dataFormatComboBox?.SelectedItem is ComboBoxItem dataFormatItem)
                    _config.Serial.DataFormat = (SerialDataFormat)dataFormatItem.Tag;

                if (_messageTerminatorTextBox != null)
                {
                    string terminator = _messageTerminatorTextBox.Text.Replace("\\r", "\r").Replace("\\n", "\n");
                    _config.Serial.MessageTerminator = terminator;
                }

                if (_serialReconnectIntervalTextBox != null && int.TryParse(_serialReconnectIntervalTextBox.Text, out int serialReconnectInterval))
                    _config.Serial.ReconnectInterval = serialReconnectInterval;

                if (_serialEnableLoggingCheckBox != null)
                    _config.Serial.EnableLogging = _serialEnableLoggingCheckBox.IsChecked ?? true;

                if (_serialAutoReconnectCheckBox != null)
                    _config.Serial.AutoReconnect = _serialAutoReconnectCheckBox.IsChecked ?? true;
            }

            // 更新ADS参数
            if (_config.Type == CommunicationType.AdsClient)
            {
                if (_adsNetIdTextBox != null && !string.IsNullOrWhiteSpace(_adsNetIdTextBox.Text))
                    _config.Ads.TargetAmsNetId = _adsNetIdTextBox.Text.Trim();

                if (_adsPortTextBox != null && int.TryParse(_adsPortTextBox.Text, out int adsPort))
                    _config.Ads.TargetAmsPort = adsPort;

                if (_adsTimeoutTextBox != null && int.TryParse(_adsTimeoutTextBox.Text, out int adsTimeout))
                    _config.Ads.Timeout = adsTimeout;

                if (_adsDisplayNameTextBox != null && !string.IsNullOrWhiteSpace(_adsDisplayNameTextBox.Text))
                    _config.Ads.DisplayName = _adsDisplayNameTextBox.Text.Trim();

                if (_adsUseSymbolicAccessCheckBox != null)
                    _config.Ads.UseSymbolicAccess = _adsUseSymbolicAccessCheckBox.IsChecked ?? true;
            }

            // 更新S7参数
            if (_config.Type == CommunicationType.S7Client)
            {
                if (_s7IpTextBox != null && !string.IsNullOrWhiteSpace(_s7IpTextBox.Text))
                    _config.S7.IpAddress = _s7IpTextBox.Text.Trim();

                if (_s7CpuTypeComboBox?.SelectedItem is ComboBoxItem cpuItem && cpuItem.Tag is CpuType cpuType)
                    _config.S7.CpuType = cpuType;

                if (_s7RackTextBox != null && int.TryParse(_s7RackTextBox.Text, out int rack))
                    _config.S7.Rack = rack;

                if (_s7SlotTextBox != null && int.TryParse(_s7SlotTextBox.Text, out int slot))
                    _config.S7.Slot = slot;

                if (_s7TimeoutTextBox != null && int.TryParse(_s7TimeoutTextBox.Text, out int timeout))
                    _config.S7.ConnectionTimeout = timeout;
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
                "SerialPort" => "串口通讯",
                "AdsClient" => "倍福PLC",
                "S7Client" => "西门子PLC",
                _ => string.Empty
            };
        }

        #endregion
    }
}