// Communication/SimpleCommunicationWindow.xaml.cs
// 简化的通讯管理窗口 - 使用简单直观的配置系统
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace VisionLite.Communication
{
    /// <summary>
    /// 简化的通讯管理窗口
    /// 实现用户要求的界面布局，但使用简化的配置系统
    /// </summary>
    public partial class SimpleCommunicationWindow : Window
    {
        #region 私有字段和属性

        /// <summary>
        /// 主窗口引用
        /// </summary>
        private readonly MainWindow _mainWindow;

        /// <summary>
        /// 连接配置的可观察集合，用于DataGrid绑定
        /// </summary>
        private ObservableCollection<ConnectionDisplayItem> _connectionItems;

        /// <summary>
        /// 当前选中的通讯连接实例
        /// </summary>
        private ICommunication _selectedCommunication;

        /// <summary>
        /// 当前选中的连接配置
        /// </summary>
        private SimpleConnectionConfig _selectedConfig;

        /// <summary>
        /// 所有活动的通讯连接字典
        /// </summary>
        private Dictionary<string, ICommunication> _activeCommunications = new Dictionary<string, ICommunication>();

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">主窗口实例</param>
        public SimpleCommunicationWindow(MainWindow owner)
        {
            InitializeComponent();
            Owner = owner;
            _mainWindow = owner;
            
            InitializeWindow();
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // 初始化连接列表
                _connectionItems = new ObservableCollection<ConnectionDisplayItem>();
                ConnectionDataGrid.ItemsSource = _connectionItems;
                
                // 加载已保存的连接（如果有的话）
                LoadSavedConnections();
                
                // 更新界面状态
                UpdateUI();
            }
            catch (Exception ex)
            {
                ShowError($"初始化窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载已保存的连接配置
        /// </summary>
        private void LoadSavedConnections()
        {
            // 这里可以从文件或配置中加载已保存的连接
            // 暂时为空，用户需要手动添加连接
        }

        #endregion

        #region 界面更新

        /// <summary>
        /// 更新界面状态
        /// </summary>
        private void UpdateUI()
        {
            bool hasSelection = ConnectionDataGrid.SelectedItem != null;
            
            // 更新按钮状态
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
            CopyButton.IsEnabled = hasSelection;
            ConnectButton.IsEnabled = hasSelection;
            
            if (hasSelection)
            {
                var selectedItem = ConnectionDataGrid.SelectedItem as ConnectionDisplayItem;
                _selectedConfig = selectedItem?.Config;
                _selectedCommunication = selectedItem?.Communication;
                
                // 更新参数显示
                UpdateParameterDisplay();
                
                // 更新连接控制
                UpdateConnectionControl();
                
                // 更新发送控制
                SendButton.IsEnabled = _selectedCommunication?.Status == ConnectionStatus.Connected;
                
                // 更新ModbusTCP专用界面显示
                UpdateModbusTcpPanel();
            }
            else
            {
                _selectedConfig = null;
                _selectedCommunication = null;
                ClearParameterDisplay();
                UpdateConnectionStatus("未选择", Colors.Gray);
                SendButton.IsEnabled = false;
                
                // 隐藏ModbusTCP专用界面
                ModbusTcpOperationPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 更新参数显示面板
        /// </summary>
        private void UpdateParameterDisplay()
        {
            ParameterDisplayPanel.Children.Clear();
            
            if (_selectedConfig == null)
            {
                EmptyParameterHint.Visibility = Visibility.Visible;
                return;
            }

            EmptyParameterHint.Visibility = Visibility.Collapsed;

            // 创建参数显示网格
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // 显示连接名称
            AddParameterRow(grid, row++, "连接名称:", _selectedConfig.Name);

            // 显示协议类型
            AddParameterRow(grid, row++, "协议类型:", _selectedConfig.TypeDisplayName);

            // 根据协议类型显示不同参数
            if (_selectedConfig.Type == CommunicationType.TcpClient)
            {
                AddParameterRow(grid, row++, "目标IP:", _selectedConfig.IpAddress);
                AddParameterRow(grid, row++, "目标端口:", _selectedConfig.Port.ToString());
            }
            else if (_selectedConfig.Type == CommunicationType.TcpServer)
            {
                AddParameterRow(grid, row++, "监听端口:", _selectedConfig.Port.ToString());
            }

            ParameterDisplayPanel.Children.Add(grid);
        }

        /// <summary>
        /// 添加参数显示行
        /// </summary>
        /// <param name="grid">父网格</param>
        /// <param name="row">行号</param>
        /// <param name="label">标签文本</param>
        /// <param name="value">参数值</param>
        private void AddParameterRow(Grid grid, int row, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var labelBlock = new TextBlock 
            { 
                Text = label, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBox 
            { 
                Text = value, 
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 2, 0, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        /// <summary>
        /// 清空参数显示
        /// </summary>
        private void ClearParameterDisplay()
        {
            ParameterDisplayPanel.Children.Clear();
            EmptyParameterHint.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 更新连接控制状态
        /// </summary>
        private void UpdateConnectionControl()
        {
            if (_selectedCommunication != null)
            {
                bool isConnected = _selectedCommunication.Status == ConnectionStatus.Connected;
                ConnectButton.Content = isConnected ? "断开" : "连接";
                
                string status = isConnected ? "已连接" : "未连接";
                Color color = isConnected ? Colors.Green : Colors.Red;
                UpdateConnectionStatus(status, color);
            }
            else
            {
                ConnectButton.Content = "连接";
                UpdateConnectionStatus("未选择", Colors.Gray);
            }
        }

        /// <summary>
        /// 更新连接状态显示
        /// </summary>
        /// <param name="statusText">状态文本</param>
        /// <param name="statusColor">状态颜色</param>
        private void UpdateConnectionStatus(string statusText, Color statusColor)
        {
            ConnectionStatusText.Text = statusText;
            ConnectionStatusIndicator.Fill = new SolidColorBrush(statusColor);
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 添加新连接
        /// </summary>
        /// <param name="config">连接配置</param>
        private void AddConnection(SimpleConnectionConfig config)
        {
            try
            {
                // 创建通讯实例
                var communication = config.CreateCommunication();
                
                // 订阅事件
                SubscribeCommunicationEvents(communication);
                
                // 添加到显示列表
                var displayItem = new ConnectionDisplayItem
                {
                    Config = config,
                    Communication = communication,
                    Name = config.Name,
                    Type = config.Type,
                    Description = config.Description,
                    Status = ConnectionStatus.Disconnected
                };
                
                _connectionItems.Add(displayItem);
                _activeCommunications[config.Name] = communication;
                
                // 选中新添加的连接
                ConnectionDataGrid.SelectedItem = displayItem;
            }
            catch (Exception ex)
            {
                ShowError($"添加连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 订阅通讯事件
        /// </summary>
        /// <param name="communication">通讯实例</param>
        private void SubscribeCommunicationEvents(ICommunication communication)
        {
            communication.MessageReceived += (message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // 如果是日志消息，直接输出内容；否则按原格式输出
                    if (message.Command == "LOG" && message.Parameters.ContainsKey("content"))
                    {
                        var logContent = message.Parameters["content"]?.ToString();
                        if (!string.IsNullOrEmpty(logContent))
                        {
                            // 直接输出日志内容，因为它已经包含时间戳格式
                            LogTextBox.AppendText($"{logContent}\r\n");
                            LogTextBox.ScrollToEnd();
                        }
                    }
                    else
                    {
                        LogMessage($"接收: [{message.Id}] {message.Command}");
                    }
                });
            };

            communication.StatusChanged += (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateConnectionItemStatus(communication, status == ConnectionStatus.Connected);
                    if (communication == _selectedCommunication)
                    {
                        UpdateConnectionControl();
                    }
                    
                    // 记录连接状态变化到日志
                    string statusText = status switch
                    {
                        ConnectionStatus.Connecting => "正在连接",
                        ConnectionStatus.Connected => "连接成功",
                        ConnectionStatus.Disconnected => "连接断开",
                        ConnectionStatus.Error => "连接错误",
                        _ => status.ToString()
                    };
                    
                    // 对于UDP客户端连接成功时，显示分配的本地端口
                    if (status == ConnectionStatus.Connected && communication is UdpCommunication udpClient)
                    {
                        var localPort = udpClient.LocalPort;
                        if (localPort.HasValue)
                        {
                            LogMessage($"状态: {statusText} - UDP客户端本地监听端口: {localPort.Value}");
                            return;
                        }
                    }
                    
                    LogMessage($"状态: {statusText}");
                });
            };
        }

        /// <summary>
        /// 更新连接项状态
        /// </summary>
        /// <param name="communication">通讯实例</param>
        /// <param name="connected">连接状态</param>
        private void UpdateConnectionItemStatus(ICommunication communication, bool connected)
        {
            var item = _connectionItems.FirstOrDefault(x => x.Communication == communication);
            if (item != null)
            {
                item.Status = connected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 添加按钮点击事件
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new SimpleAddConnectionWindow();
            addWindow.Owner = this;
            
            if (addWindow.ShowDialog() == true)
            {
                AddConnection(addWindow.Result);
            }
        }

        /// <summary>
        /// 编辑按钮点击事件
        /// </summary>
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConfig == null) return;

            var editWindow = new SimpleAddConnectionWindow(_selectedConfig);
            editWindow.Owner = this;
            
            if (editWindow.ShowDialog() == true)
            {
                // 更新配置
                var selectedItem = ConnectionDataGrid.SelectedItem as ConnectionDisplayItem;
                if (selectedItem != null)
                {
                    // 断开旧连接
                    if (_selectedCommunication?.Status == ConnectionStatus.Connected)
                    {
                        _selectedCommunication.Close();
                    }

                    // 移除旧的通讯实例
                    _activeCommunications.Remove(selectedItem.Config.Name);
                    
                    // 更新配置
                    selectedItem.Config = editWindow.Result;
                    selectedItem.Name = editWindow.Result.Name;
                    selectedItem.Type = editWindow.Result.Type;
                    selectedItem.Description = editWindow.Result.Description;
                    
                    // 创建新的通讯实例
                    var newCommunication = editWindow.Result.CreateCommunication();
                    SubscribeCommunicationEvents(newCommunication);
                    selectedItem.Communication = newCommunication;
                    
                    _activeCommunications[editWindow.Result.Name] = newCommunication;
                    
                    UpdateUI();
                }
            }
        }

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ConnectionDataGrid.SelectedItem as ConnectionDisplayItem;
            if (selectedItem == null) return;

            var result = MessageBox.Show($"确定要删除连接 '{selectedItem.Name}' 吗？", 
                                       "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // 断开连接
                if (selectedItem.Communication?.Status == ConnectionStatus.Connected)
                {
                    selectedItem.Communication.Close();
                }

                // 移除
                _connectionItems.Remove(selectedItem);
                _activeCommunications.Remove(selectedItem.Config.Name);
                
                UpdateUI();
            }
        }

        /// <summary>
        /// 复制按钮点击事件
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConfig == null) return;

            var copyConfig = _selectedConfig.Clone();
            copyConfig.Name = $"{copyConfig.Name} - 副本";
            
            var addWindow = new SimpleAddConnectionWindow(copyConfig);
            addWindow.Owner = this;
            
            if (addWindow.ShowDialog() == true)
            {
                AddConnection(addWindow.Result);
            }
        }

        /// <summary>
        /// 连接列表选择改变事件
        /// </summary>
        private void ConnectionDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        /// <summary>
        /// 连接/断开按钮点击事件
        /// </summary>
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication == null) return;

            try
            {
                ConnectButton.IsEnabled = false;
                
                if (_selectedCommunication.Status == ConnectionStatus.Connected)
                {
                    // 断开连接
                    _selectedCommunication.Close();
                    LogMessage($"已断开连接: {_selectedConfig.Name}");
                }
                else
                {
                    // 建立连接
                    LogMessage($"正在连接: {_selectedConfig.Name}...");
                    bool connected = await _selectedCommunication.OpenAsync();
                    
                    if (connected)
                    {
                        LogMessage($"连接成功: {_selectedConfig.Name}");
                    }
                    else
                    {
                        LogMessage($"连接失败: {_selectedConfig.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"连接操作异常: {ex.Message}");
            }
            finally
            {
                ConnectButton.IsEnabled = true;
                UpdateUI();
            }
        }

        /// <summary>
        /// 发送按钮点击事件
        /// </summary>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        /// <summary>
        /// 发送输入框按键事件
        /// </summary>
        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendButton.IsEnabled)
            {
                SendMessage();
            }
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 发送消息
        /// </summary>
        private async void SendMessage()
        {
            if (_selectedCommunication?.Status != ConnectionStatus.Connected) return;
            
            string messageText = SendTextBox.Text;
            if (string.IsNullOrWhiteSpace(messageText)) return;

            try
            {
                // 创建消息对象
                var message = new Message { Command = messageText };
                bool success = await _selectedCommunication.SendAsync(message);
                
                if (success)
                {
                    LogMessage($"发送: [{message.Id}] {message.Command}");
                    SendTextBox.Clear();
                }
                else
                {
                    LogMessage($"发送失败: [{message.Id}] {message.Command}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"发送异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">消息内容</param>
        private void LogMessage(string message)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LogTextBox.AppendText($"[{timeStamp}] {message}\r\n");
            LogTextBox.ScrollToEnd();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 显示错误消息
        /// </summary>
        /// <param name="message">错误消息</param>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // 关闭所有连接
            foreach (var comm in _activeCommunications.Values)
            {
                try
                {
                    if (comm.Status == ConnectionStatus.Connected)
                    {
                        comm.Close();
                    }
                    comm.Dispose();
                }
                catch { }
            }
            
            _activeCommunications.Clear();
            
            // 激活主窗口，确保它显示在最前层
            try
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Activate();
                    _mainWindow.Focus();
                }
            }
            catch { }
            
            base.OnClosed(e);
        }

        #endregion

        #region ModbusTCP专用操作面板

        /// <summary>
        /// 更新ModbusTCP专用操作面板的显示状态
        /// </summary>
        private void UpdateModbusTcpPanel()
        {
            if (_selectedConfig?.Type == CommunicationType.ModbusTcpClient)
            {
                ModbusTcpOperationPanel.Visibility = Visibility.Visible;
                InitializeModbusTcpPanel();
            }
            else
            {
                ModbusTcpOperationPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 初始化ModbusTCP操作面板的默认设置
        /// </summary>
        private void InitializeModbusTcpPanel()
        {
            // 设置默认值
            if (OperationTypeComboBox.SelectedIndex == -1)
                OperationTypeComboBox.SelectedIndex = 0; // 默认选择读取
            
            if (DataTypeComboBox.SelectedIndex == -1)
                DataTypeComboBox.SelectedIndex = 0; // 默认选择线圈
            
            if (DisplayFormatComboBox.SelectedIndex == -1)
                DisplayFormatComboBox.SelectedIndex = 0; // 默认选择十进制

            // 更新控件状态
            UpdateModbusTcpControls();
        }

        /// <summary>
        /// 更新ModbusTCP控件的启用状态
        /// </summary>
        private void UpdateModbusTcpControls()
        {
            bool isConnected = _selectedCommunication?.Status == ConnectionStatus.Connected;
            
            ExecuteOperationButton.IsEnabled = isConnected;
            
            // 根据操作类型更新数量/数值输入框的提示
            if (OperationTypeComboBox.SelectedItem is ComboBoxItem operationItem)
            {
                string operationType = operationItem.Tag?.ToString() ?? "";
                if (operationType.ToUpper() == "WRITE")
                {
                    // 写入操作，显示为"数值"
                    if (DataTypeComboBox.SelectedItem is ComboBoxItem dataItem)
                    {
                        string dataType = dataItem.Tag?.ToString() ?? "";
                        if (dataType == "COIL")
                        {
                            QuantityValueTextBox.Text = "1"; // 线圈默认值
                        }
                        else if (dataType == "FLOAT")
                        {
                            QuantityValueTextBox.Text = "3.14159"; // 浮点数示例值
                        }
                        else
                        {
                            QuantityValueTextBox.Text = "1234"; // 寄存器默认值
                        }
                    }
                }
                else
                {
                    // 读取操作，显示为"数量"
                    if (DataTypeComboBox.SelectedItem is ComboBoxItem dataItem)
                    {
                        string dataType = dataItem.Tag?.ToString() ?? "";
                        if (dataType == "FLOAT")
                        {
                            QuantityValueTextBox.Text = "2"; // 浮点数占用2个寄存器
                        }
                        else
                        {
                            QuantityValueTextBox.Text = "10"; // 默认读取10个
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 操作类型选择变化事件
        /// </summary>
        private void OperationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateModbusTcpControls();
        }

        /// <summary>
        /// 数据类型选择变化事件
        /// </summary>
        private void DataTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateModbusTcpControls();
        }

        /// <summary>
        /// 执行操作按钮点击事件
        /// </summary>
        private async void ExecuteOperationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication?.Status != ConnectionStatus.Connected)
            {
                MessageBox.Show("请先连接到ModbusTCP服务器", "连接错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var startTime = DateTime.Now;
                LastOperationTime.Text = startTime.ToString("HH:mm:ss");
                OperationResult.Text = "执行中...";
                OperationResult.Foreground = new SolidColorBrush(Colors.Orange);
                
                ExecuteOperationButton.IsEnabled = false;

                // 构建命令
                string command = BuildModbusCommand();
                if (string.IsNullOrEmpty(command))
                {
                    OperationResult.Text = "参数错误";
                    OperationResult.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }

                LastOperationText.Text = command;

                // 执行命令
                var message = new Message { Command = command };
                bool success = await _selectedCommunication.SendAsync(message);

                var duration = DateTime.Now - startTime;
                OperationDuration.Text = $"{duration.TotalMilliseconds:F0}ms";

                if (success)
                {
                    OperationResult.Text = "成功";
                    OperationResult.Foreground = new SolidColorBrush(Colors.Green);
                    ReturnedData.Text = "操作完成"; // 这里可以根据实际返回数据更新
                }
                else
                {
                    OperationResult.Text = "失败";
                    OperationResult.Foreground = new SolidColorBrush(Colors.Red);
                    ReturnedData.Text = "操作失败，请查看日志";
                }
            }
            catch (Exception ex)
            {
                OperationResult.Text = "异常";
                OperationResult.Foreground = new SolidColorBrush(Colors.Red);
                ReturnedData.Text = ex.Message;
                
                LogMessage($"ModbusTCP操作异常: {ex.Message}");
            }
            finally
            {
                ExecuteOperationButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 根据界面选择构建Modbus命令
        /// </summary>
        private string BuildModbusCommand()
        {
            try
            {
                if (OperationTypeComboBox.SelectedItem is not ComboBoxItem operationItem ||
                    DataTypeComboBox.SelectedItem is not ComboBoxItem dataItem)
                    return null;

                string operationType = operationItem.Tag?.ToString() ?? "";
                string dataType = dataItem.Tag?.ToString() ?? "";
                string address = StartAddressTextBox.Text?.Trim() ?? "0";
                string quantityValue = QuantityValueTextBox.Text?.Trim() ?? "1";

                if (!int.TryParse(address, out _))
                {
                    MessageBox.Show("地址格式错误", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (operationType.ToUpper() == "READ")
                {
                    // 读取操作
                    return dataType switch
                    {
                        "COIL" => $"READ_COIL {address} {quantityValue}",
                        "HOLDING" => $"READ_HOLDING {address} {quantityValue}",
                        "INPUT" => $"READ_INPUT {address} {quantityValue}",
                        "DISCRETE" => $"READ_DISCRETE {address} {quantityValue}",
                        "FLOAT" => $"READ_HOLDING {address} 2", // 浮点数占用2个寄存器
                        _ => null
                    };
                }
                else
                {
                    // 写入操作
                    return dataType switch
                    {
                        "COIL" => $"WRITE_COIL {address} {quantityValue}",
                        "HOLDING" => $"WRITE_REGISTER {address} {quantityValue}",
                        "FLOAT" => $"WRITE_FLOAT {address} {quantityValue}",
                        _ => null
                    };
                }
            }
            catch (Exception ex)
            {
                LogMessage($"构建命令失败: {ex.Message}");
                return null;
            }
        }


        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 连接显示项
    /// </summary>
    public class ConnectionDisplayItem : INotifyPropertyChanged
    {
        private ConnectionStatus _status;
        private string _name;
        private CommunicationType _type;
        private string _description;
        
        public SimpleConnectionConfig Config { get; set; }
        public ICommunication Communication { get; set; }
        
        public string Name 
        { 
            get => _name;
            set 
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public CommunicationType Type 
        { 
            get => _type;
            set 
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string Description 
        { 
            get => _description;
            set 
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public ConnectionStatus Status 
        { 
            get => _status;
            set 
            {
                if (_status != value)
                {
                    _status = value;
                    StatusBrush = value switch
                    {
                        ConnectionStatus.Connected => Brushes.Green,
                        ConnectionStatus.Connecting => Brushes.Orange,
                        ConnectionStatus.Error => Brushes.Red,
                        _ => Brushes.Gray
                    };
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }
        
        public Brush StatusBrush { get; private set; } = Brushes.Gray;
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 连接类型到显示名称转换器
    /// </summary>
    public class ConnectionTypeToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommunicationType type)
            {
                return type switch
                {
                    CommunicationType.TcpClient => "TCP客户端",
                    CommunicationType.TcpServer => "TCP服务器",
                    CommunicationType.UdpClient => "UDP客户端",
                    CommunicationType.UdpServer => "UDP服务器",
                    CommunicationType.ModbusTcpServer => "ModbusTCP服务器",
                    CommunicationType.ModbusTcpClient => "ModbusTCP客户端",
                    _ => "未知"
                };
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}