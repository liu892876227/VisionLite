// Communication/NewCommunicationWindow.xaml.cs
// 新版通讯管理窗口 - 按照用户需求重新设计的通讯管理界面
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VisionLite.Communication.Models;
using VisionLite.Communication.Protocols;
using VisionLite.Communication.Managers;
using VisionLite.Communication.UI;

namespace VisionLite.Communication
{
    /// <summary>
    /// 新版通讯管理窗口
    /// 实现用户要求的界面布局：左侧连接列表，右侧参数配置和日志
    /// </summary>
    public partial class NewCommunicationWindow : Window
    {
        #region 私有字段和属性

        /// <summary>
        /// 主窗口引用
        /// </summary>
        private readonly MainWindow _mainWindow;

        /// <summary>
        /// 连接配置的可观察集合，用于DataGrid绑定
        /// </summary>
        private ObservableCollection<CommunicationDisplayItem> _connectionItems;

        /// <summary>
        /// 当前选中的通讯连接实例
        /// </summary>
        private ICommunication _selectedCommunication;

        /// <summary>
        /// 当前选中的TCP服务器实例（如果是服务器类型）
        /// </summary>
        private ITcpServer _selectedTcpServer;

        /// <summary>
        /// 当前选中的连接配置
        /// </summary>
        private CommunicationConfig _selectedConfig;

        /// <summary>
        /// 参数控件映射字典
        /// </summary>
        private Dictionary<string, UIElement> _parameterControls = new Dictionary<string, UIElement>();

        /// <summary>
        /// 当前协议的参数定义
        /// </summary>
        private List<ParameterDefinition> _parameterDefinitions = new List<ParameterDefinition>();

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">主窗口实例</param>
        public NewCommunicationWindow(MainWindow owner)
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
                // 确保协议已加载
                LoadProtocols();
                
                // 初始化连接列表
                InitializeConnectionList();
                
                // 加载现有连接配置
                LoadExistingConnections();
            }
            catch (Exception ex)
            {
                ShowError($"初始化窗口失败: {ex.Message}");
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
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TcpClientProtocol).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TcpServerProtocol).TypeHandle);
                
                // 检查协议注册数量
                var protocolCount = CommunicationProtocolManager.Instance.RegisteredProtocolCount;
                System.Diagnostics.Debug.WriteLine($"已注册协议数量: {protocolCount}");
                
                if (protocolCount == 0)
                {
                    // 如果静态构造函数没有工作，手动注册协议
                    System.Diagnostics.Debug.WriteLine("静态构造函数未触发，手动注册协议");
                    CommunicationProtocolManager.Instance.RegisterProtocol(new TcpClientProtocol());
                    CommunicationProtocolManager.Instance.RegisterProtocol(new TcpServerProtocol());
                    
                    protocolCount = CommunicationProtocolManager.Instance.RegisteredProtocolCount;
                    System.Diagnostics.Debug.WriteLine($"手动注册后协议数量: {protocolCount}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载协议时发生异常: {ex.Message}");
                
                // 异常情况下也尝试手动注册
                try
                {
                    CommunicationProtocolManager.Instance.RegisterProtocol(new TcpClientProtocol());
                    CommunicationProtocolManager.Instance.RegisterProtocol(new TcpServerProtocol());
                }
                catch (Exception regEx)
                {
                    System.Diagnostics.Debug.WriteLine($"手动注册协议失败: {regEx.Message}");
                }
            }
        }

        /// <summary>
        /// 初始化连接列表
        /// </summary>
        private void InitializeConnectionList()
        {
            _connectionItems = new ObservableCollection<CommunicationDisplayItem>();
            ConnectionDataGrid.ItemsSource = _connectionItems;
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 加载现有连接
        /// </summary>
        private void LoadExistingConnections()
        {
            try
            {
                _connectionItems.Clear();
                
                if (_mainWindow?.communications != null)
                {
                    foreach (var comm in _mainWindow.communications.Values)
                    {
                        var displayItem = new CommunicationDisplayItem
                        {
                            Name = comm.Name,
                            ProtocolDisplayName = GetProtocolDisplayName(comm),
                            Status = comm.Status,
                            Communication = comm
                        };
                        
                        _connectionItems.Add(displayItem);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"加载了 {_connectionItems.Count} 个现有连接");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载现有连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取协议显示名称
        /// </summary>
        /// <param name="communication">通讯实例</param>
        /// <returns>协议显示名称</returns>
        private string GetProtocolDisplayName(ICommunication communication)
        {
            return communication switch
            {
                TcpCommunication => "TCP客户端",
                TcpServer => "TCP服务器",
                _ => "未知协议"
            };
        }

        #endregion

        #region 事件处理 - 连接管理按钮

        /// <summary>
        /// 添加连接按钮点击事件
        /// </summary>
        private void AddConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configWindow = new CommunicationConfigWindow()
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (configWindow.ShowDialog() == true)
                {
                    var config = configWindow.Result;
                    if (config != null)
                    {
                        CreateAndAddConnection(config);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"添加连接时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 编辑连接按钮点击事件
        /// </summary>
        private void EditConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedConfig == null)
                {
                    ShowWarning("请先选择要编辑的连接");
                    return;
                }

                var configWindow = new CommunicationConfigWindow(_selectedConfig)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (configWindow.ShowDialog() == true)
                {
                    var updatedConfig = configWindow.Result;
                    if (updatedConfig != null)
                    {
                        UpdateConnection(updatedConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"编辑连接时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除连接按钮点击事件
        /// </summary>
        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedCommunication == null)
                {
                    ShowWarning("请先选择要删除的连接");
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要删除连接 '{_selectedCommunication.Name}' 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    RemoveConnection(_selectedCommunication.Name);
                }
            }
            catch (Exception ex)
            {
                ShowError($"删除连接时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 复制连接按钮点击事件
        /// </summary>
        private void CopyConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedConfig == null)
                {
                    ShowWarning("请先选择要复制的连接");
                    return;
                }

                var copiedConfig = _selectedConfig.Clone();
                copiedConfig.Name = GenerateUniqueName(copiedConfig.Name + "_副本");

                var configWindow = new CommunicationConfigWindow(copiedConfig)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (configWindow.ShowDialog() == true)
                {
                    var newConfig = configWindow.Result;
                    if (newConfig != null)
                    {
                        CreateAndAddConnection(newConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"复制连接时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理 - 连接选择和控制

        /// <summary>
        /// 连接列表选择变化事件
        /// </summary>
        private void ConnectionDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = ConnectionDataGrid.SelectedItem as CommunicationDisplayItem;
                
                if (selectedItem != null)
                {
                    SelectConnection(selectedItem);
                }
                else
                {
                    ClearSelection();
                }

                UpdateUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择连接时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 连接/断开按钮点击事件
        /// </summary>
        private async void ConnectionToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication == null)
                return;

            try
            {
                ConnectionToggleButton.IsEnabled = false;
                
                if (_selectedCommunication.Status == ConnectionStatus.Connected)
                {
                    ConnectionToggleButton.Content = "断开中...";
                    _selectedCommunication.Close();
                }
                else
                {
                    ConnectionToggleButton.Content = "连接中...";
                    await _selectedCommunication.OpenAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"连接操作失败: {ex.Message}");
            }
            finally
            {
                ConnectionToggleButton.IsEnabled = true;
                UpdateConnectionStatus();
            }
        }

        /// <summary>
        /// 管理客户端按钮点击事件
        /// </summary>
        private void ManageClients_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTcpServer == null)
                    return;

                // 这里可以打开一个客户端管理窗口
                // 暂时显示客户端列表信息
                var clients = _selectedTcpServer.GetConnectedClients();
                var clientInfo = clients?.Any() == true 
                    ? string.Join("\n", clients) 
                    : "没有连接的客户端";

                MessageBox.Show($"已连接的客户端:\n{clientInfo}", 
                              "客户端管理", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"管理客户端时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理 - 消息发送和日志

        /// <summary>
        /// 发送消息按钮点击事件
        /// </summary>
        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication?.Status != ConnectionStatus.Connected ||
                string.IsNullOrWhiteSpace(SendMessageTextBox.Text))
                return;

            try
            {
                var messageText = SendMessageTextBox.Text.Trim();
                
                // 创建简单的文本消息
                var message = MessageBuilder.CreateCommand(messageText);
                
                bool success = await _selectedCommunication.SendAsync(message);
                
                if (success)
                {
                    LogMessage($"发送 >> {messageText}", Brushes.DarkGreen);
                    SendMessageTextBox.Clear();
                }
                else
                {
                    LogMessage($"发送失败: {messageText}", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"发送异常: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        #endregion

        #region 连接管理逻辑

        /// <summary>
        /// 创建并添加新连接
        /// </summary>
        /// <param name="config">连接配置</param>
        private void CreateAndAddConnection(CommunicationConfig config)
        {
            try
            {
                // 创建通讯实例
                var communication = CommunicationProtocolManager.Instance.CreateCommunicationInstance(config);
                
                if (communication != null)
                {
                    // 添加到主窗口的通讯集合
                    _mainWindow.communications[config.Name] = communication;
                    
                    // 添加到显示列表
                    var displayItem = new CommunicationDisplayItem
                    {
                        Name = config.Name,
                        ProtocolDisplayName = config.ProtocolDisplayName,
                        Status = communication.Status,
                        Communication = communication
                    };
                    
                    _connectionItems.Add(displayItem);
                    
                    // 保存配置
                    CommunicationConfigManager.SaveSingleConfiguration(config);
                    
                    // 选中新添加的连接
                    ConnectionDataGrid.SelectedItem = displayItem;
                    
                    LogMessage($"成功添加连接: {config.Name}", Brushes.Green);
                }
            }
            catch (Exception ex)
            {
                ShowError($"创建连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新现有连接
        /// </summary>
        /// <param name="config">更新后的配置</param>
        private void UpdateConnection(CommunicationConfig config)
        {
            try
            {
                var oldName = _selectedCommunication.Name;
                
                // 如果名称发生变化，需要更新主窗口的字典
                if (!oldName.Equals(config.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _mainWindow.communications.Remove(oldName);
                    
                    // 删除旧配置
                    CommunicationConfigManager.DeleteConfiguration(oldName);
                }
                
                // 创建新的通讯实例
                var newCommunication = CommunicationProtocolManager.Instance.CreateCommunicationInstance(config);
                
                if (newCommunication != null)
                {
                    // 停止旧连接
                    _selectedCommunication.Close();
                    _selectedCommunication.Dispose();
                    
                    // 更新主窗口字典
                    _mainWindow.communications[config.Name] = newCommunication;
                    
                    // 更新显示列表
                    var selectedItem = ConnectionDataGrid.SelectedItem as CommunicationDisplayItem;
                    if (selectedItem != null)
                    {
                        selectedItem.Name = config.Name;
                        selectedItem.ProtocolDisplayName = config.ProtocolDisplayName;
                        selectedItem.Status = newCommunication.Status;
                        selectedItem.Communication = newCommunication;
                    }
                    
                    // 重新选择连接以更新界面
                    SelectConnection(selectedItem);
                    
                    // 保存配置
                    CommunicationConfigManager.SaveSingleConfiguration(config);
                    
                    LogMessage($"成功更新连接: {config.Name}", Brushes.Green);
                }
            }
            catch (Exception ex)
            {
                ShowError($"更新连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        /// <param name="connectionName">连接名称</param>
        private void RemoveConnection(string connectionName)
        {
            try
            {
                // 从主窗口移除
                if (_mainWindow.communications.TryGetValue(connectionName, out var communication))
                {
                    communication.Close();
                    communication.Dispose();
                    _mainWindow.communications.Remove(connectionName);
                }
                
                // 从显示列表移除
                var itemToRemove = _connectionItems.FirstOrDefault(item => 
                    item.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
                
                if (itemToRemove != null)
                {
                    _connectionItems.Remove(itemToRemove);
                }
                
                // 删除保存的配置
                CommunicationConfigManager.DeleteConfiguration(connectionName);
                
                // 清空选择
                ClearSelection();
                UpdateUI();
                
                LogMessage($"成功删除连接: {connectionName}", Brushes.Orange);
            }
            catch (Exception ex)
            {
                ShowError($"删除连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 选择连接
        /// </summary>
        /// <param name="selectedItem">选中的显示项</param>
        private void SelectConnection(CommunicationDisplayItem selectedItem)
        {
            try
            {
                // 取消之前连接的事件订阅
                UnsubscribeFromCurrentConnection();
                
                _selectedCommunication = selectedItem.Communication;
                _selectedTcpServer = _selectedCommunication as ITcpServer;
                
                // 尝试从配置中重建配置对象
                _selectedConfig = CreateConfigFromConnection(_selectedCommunication);
                
                // 订阅新连接的事件
                SubscribeToConnection(_selectedCommunication);
                
                // 构建参数显示面板
                BuildParameterDisplayPanel();
                
                // 更新连接状态显示
                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择连接时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空选择
        /// </summary>
        private void ClearSelection()
        {
            UnsubscribeFromCurrentConnection();
            
            _selectedCommunication = null;
            _selectedTcpServer = null;
            _selectedConfig = null;
            
            ClearParameterPanel();
            
            ConnectionStatusText.Text = "未选择连接";
            ConnectionToggleButton.Content = "连接";
        }

        #endregion

        #region 参数面板管理

        /// <summary>
        /// 构建参数显示面板
        /// </summary>
        private void BuildParameterDisplayPanel()
        {
            try
            {
                ClearParameterPanel();
                
                if (_selectedConfig == null)
                    return;

                // 获取协议实例
                var protocol = CommunicationProtocolManager.Instance.GetProtocol(_selectedConfig.ProtocolType);
                if (protocol == null)
                    return;

                // 获取参数定义
                _parameterDefinitions = protocol.GetParameterDefinitions();
                if (_parameterDefinitions == null || _parameterDefinitions.Count == 0)
                {
                    var noParamsText = new TextBlock
                    {
                        Text = "该连接没有可配置的参数",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontStyle = FontStyles.Italic,
                        Foreground = Brushes.Gray
                    };
                    DynamicParameterPanel.Children.Add(noParamsText);
                    return;
                }

                // 创建只读的参数显示
                foreach (var paramDef in _parameterDefinitions.Where(p => p.IsVisible))
                {
                    CreateReadOnlyParameterDisplay(paramDef);
                }

                ParameterGroupBox.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"构建参数面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建只读参数显示
        /// </summary>
        /// <param name="paramDef">参数定义</param>
        private void CreateReadOnlyParameterDisplay(ParameterDefinition paramDef)
        {
            try
            {
                var parameterRow = new Grid
                {
                    Margin = new Thickness(0, 3, 0, 3)
                };
                
                parameterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                parameterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // 参数名标签
                var nameLabel = new TextBlock
                {
                    Text = paramDef.DisplayName + ":",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(nameLabel, 0);
                
                // 参数值显示
                var valueText = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = Brushes.DarkBlue
                };
                
                // 获取参数值
                var value = _selectedConfig?.GetStringParameter(paramDef.Key, paramDef.DefaultValue?.ToString() ?? "");
                valueText.Text = value ?? "";
                
                Grid.SetColumn(valueText, 1);
                
                parameterRow.Children.Add(nameLabel);
                parameterRow.Children.Add(valueText);
                
                DynamicParameterPanel.Children.Add(parameterRow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建参数显示失败 ({paramDef.Key}): {ex.Message}");
            }
        }

        /// <summary>
        /// 清空参数面板
        /// </summary>
        private void ClearParameterPanel()
        {
            DynamicParameterPanel.Children.Clear();
            _parameterControls.Clear();
            _parameterDefinitions.Clear();
            
            var hintText = new TextBlock
            {
                Text = "请选择连接以查看参数配置",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
            DynamicParameterPanel.Children.Add(hintText);
            
            ParameterGroupBox.IsEnabled = false;
        }

        #endregion

        #region 事件订阅管理

        /// <summary>
        /// 订阅连接事件
        /// </summary>
        /// <param name="communication">通讯实例</param>
        private void SubscribeToConnection(ICommunication communication)
        {
            if (communication == null)
                return;

            try
            {
                communication.StatusChanged += OnStatusChanged;
                communication.MessageReceived += OnMessageReceived;
                
                if (communication is ITcpServer server)
                {
                    server.ClientConnected += OnClientConnected;
                    server.ClientDisconnected += OnClientDisconnected;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"订阅连接事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消当前连接的事件订阅
        /// </summary>
        private void UnsubscribeFromCurrentConnection()
        {
            if (_selectedCommunication == null)
                return;

            try
            {
                _selectedCommunication.StatusChanged -= OnStatusChanged;
                _selectedCommunication.MessageReceived -= OnMessageReceived;
                
                if (_selectedTcpServer != null)
                {
                    _selectedTcpServer.ClientConnected -= OnClientConnected;
                    _selectedTcpServer.ClientDisconnected -= OnClientDisconnected;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消事件订阅失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 连接状态变化事件处理
        /// </summary>
        /// <param name="status">新状态</param>
        private void OnStatusChanged(ConnectionStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateConnectionStatus();
                
                // 更新列表中的状态
                var selectedItem = ConnectionDataGrid.SelectedItem as CommunicationDisplayItem;
                if (selectedItem != null)
                {
                    selectedItem.Status = status;
                }
            });
        }

        /// <summary>
        /// 消息接收事件处理
        /// </summary>
        /// <param name="message">接收到的消息</param>
        private void OnMessageReceived(Message message)
        {
            Dispatcher.Invoke(() =>
            {
                var color = message.Type switch
                {
                    MessageType.Command => Brushes.Blue,
                    MessageType.Response => Brushes.Green,
                    MessageType.Event => Brushes.Orange,
                    MessageType.Heartbeat => Brushes.Gray,
                    _ => Brushes.Black
                };
                
                LogMessage($"接收 << [{message.Type}] {message.Command}", color);
            });
        }

        /// <summary>
        /// 客户端连接事件处理
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        private void OnClientConnected(string clientId)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateServerInfo();
                LogMessage($"客户端已连接: {clientId}", Brushes.Green);
            });
        }

        /// <summary>
        /// 客户端断开事件处理
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        private void OnClientDisconnected(string clientId)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateServerInfo();
                LogMessage($"客户端已断开: {clientId}", Brushes.Orange);
            });
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                bool hasSelection = _selectedCommunication != null;
                
                EditConnectionButton.IsEnabled = hasSelection;
                DeleteConnectionButton.IsEnabled = hasSelection;
                CopyConnectionButton.IsEnabled = hasSelection;
                ConnectionToggleButton.IsEnabled = hasSelection;
                SendButton.IsEnabled = hasSelection && _selectedCommunication?.Status == ConnectionStatus.Connected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新UI状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新连接状态显示
        /// </summary>
        private void UpdateConnectionStatus()
        {
            if (_selectedCommunication == null)
            {
                ConnectionStatusText.Text = "未选择连接";
                ConnectionToggleButton.Content = "连接";
                ConnectionToggleButton.IsEnabled = false;
                return;
            }

            bool isServer = _selectedCommunication is ITcpServer;
            
            switch (_selectedCommunication.Status)
            {
                case ConnectionStatus.Disconnected:
                    ConnectionStatusText.Text = "未连接";
                    ConnectionStatusText.Foreground = Brushes.Gray;
                    ConnectionToggleButton.Content = isServer ? "启动" : "连接";
                    break;
                    
                case ConnectionStatus.Connecting:
                    ConnectionStatusText.Text = isServer ? "启动中..." : "连接中...";
                    ConnectionStatusText.Foreground = Brushes.Orange;
                    ConnectionToggleButton.Content = isServer ? "启动中..." : "连接中...";
                    break;
                    
                case ConnectionStatus.Connected:
                    ConnectionStatusText.Text = isServer ? "正在监听" : "已连接";
                    ConnectionStatusText.Foreground = Brushes.Green;
                    ConnectionToggleButton.Content = isServer ? "停止" : "断开";
                    break;
                    
                case ConnectionStatus.Error:
                    ConnectionStatusText.Text = "错误";
                    ConnectionStatusText.Foreground = Brushes.Red;
                    ConnectionToggleButton.Content = isServer ? "启动" : "连接";
                    break;
            }

            UpdateServerInfo();
            UpdateUI();
        }

        /// <summary>
        /// 更新服务器信息显示
        /// </summary>
        private void UpdateServerInfo()
        {
            if (_selectedTcpServer != null)
            {
                ServerInfoPanel.Visibility = Visibility.Visible;
                ClientCountText.Text = _selectedTcpServer.ClientCount.ToString();
            }
            else
            {
                ServerInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从通讯实例创建配置对象
        /// </summary>
        /// <param name="communication">通讯实例</param>
        /// <returns>配置对象</returns>
        private CommunicationConfig CreateConfigFromConnection(ICommunication communication)
        {
            // 这里应该从保存的配置中加载，或者从通讯实例中反推配置
            // 简化实现，返回基本配置
            return new CommunicationConfig
            {
                Name = communication.Name,
                ProtocolType = communication switch
                {
                    TcpCommunication => "TCP_CLIENT",
                    TcpServer => "TCP_SERVER",
                    _ => "UNKNOWN"
                },
                ProtocolDisplayName = GetProtocolDisplayName(communication)
            };
        }

        /// <summary>
        /// 生成唯一名称
        /// </summary>
        /// <param name="baseName">基础名称</param>
        /// <returns>唯一名称</returns>
        private string GenerateUniqueName(string baseName)
        {
            var counter = 1;
            var candidateName = baseName;
            
            while (_connectionItems.Any(item => 
                item.Name.Equals(candidateName, StringComparison.OrdinalIgnoreCase)))
            {
                candidateName = $"{baseName}_{counter}";
                counter++;
            }
            
            return candidateName;
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="color">消息颜色</param>
        private void LogMessage(string message, Brush color)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {message}{Environment.NewLine}";
                
                LogTextBox.AppendText(logEntry);
                LogScrollViewer.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"记录日志失败: {ex.Message}");
            }
        }

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

        #endregion
    }

    /// <summary>
    /// 连接显示项，用于DataGrid绑定
    /// </summary>
    public class CommunicationDisplayItem
    {
        public string Name { get; set; }
        public string ProtocolDisplayName { get; set; }
        public ConnectionStatus Status { get; set; }
        public ICommunication Communication { get; set; }
    }

    /// <summary>
    /// 状态到颜色的转换器
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionStatus status)
            {
                return status switch
                {
                    ConnectionStatus.Connected => Brushes.Green,
                    ConnectionStatus.Connecting => Brushes.Orange,
                    ConnectionStatus.Error => Brushes.Red,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}