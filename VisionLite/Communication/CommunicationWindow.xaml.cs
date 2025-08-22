// Communication/CommunicationWindow.xaml.cs
// 通讯管理窗口 - 用于配置和监控通讯连接的用户界面
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;


namespace VisionLite.Communication
{
    /// <summary>
    /// 通讯管理窗口类
    /// 提供通讯连接的可视化管理界面，包括：
    /// 1. 连接列表管理 - 添加、删除、选择通讯实例
    /// 2. 连接配置 - IP地址、端口、连接参数设置
    /// 3. 连接控制 - 连接、断开操作
    /// 4. 消息收发 - 手动发送测试消息，监控接收消息
    /// 5. 日志显示 - 实时显示通讯状态和消息内容
    /// 
    /// 界面特点：
    /// - 支持多个并发连接的管理
    /// - 实时状态指示（连接状态用颜色区分）
    /// - 消息格式智能识别（JSON/简单文本）
    /// - 完整的业务逻辑演示（触发、参数设置、结果查询等）
    /// </summary>
    public partial class CommunicationWindow : Window
    {
        #region 私有字段
        
        /// <summary>
        /// 主窗口引用，用于访问主窗口的通讯连接集合
        /// </summary>
        private readonly MainWindow _mainWindow;
        
        /// <summary>
        /// 当前选中的通讯连接实例
        /// 用于界面操作（连接、断开、发送消息等）
        /// </summary>
        private ICommunication _selectedCommunication;

        /// <summary>
        /// 当前选中的TCP服务器实例（如果是服务器类型）
        /// 用于服务器特有的操作（客户端管理等）
        /// </summary>
        private ITcpServer _selectedTcpServer;
        
        #endregion

        #region 构造函数和初始化
        
        /// <summary>
        /// 构造通讯管理窗口
        /// </summary>
        /// <param name="owner">父窗口（主窗口），用于访问通讯连接集合</param>
        public CommunicationWindow(MainWindow owner)
        {
            InitializeComponent();
            Owner = owner;  // 设置父窗口，使窗口居中显示
            _mainWindow = owner;
            RefreshConnectionList();  // 初始化连接列表
        }
        
        #endregion

        #region 私有辅助方法
        
        /// <summary>
        /// 刷新连接列表显示
        /// 从主窗口的通讯连接集合中更新界面列表
        /// </summary>
        private void RefreshConnectionList()
        {
            ConnectionListBox.Items.Clear();
            foreach (var comm in _mainWindow.communications.Values)
            {
                ConnectionListBox.Items.Add(comm.Name);
            }
        }
        
        #endregion

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // 根据当前选择的连接类型创建不同的实例
            bool isServer = ConnectionTypeComboBox.SelectedIndex == 1;
            
            if (isServer)
            {
                string newName = $"TCP_Server_{_mainWindow.communications.Count + 1}";
                var newTcpServer = new TcpServer(newName, 8080);
                _mainWindow.communications.Add(newName, newTcpServer);
            }
            else
            {
                string newName = $"TCP_Client_{_mainWindow.communications.Count + 1}";
                var newTcp = new TcpCommunication(newName, "127.0.0.1", 8080);
                _mainWindow.communications.Add(newName, newTcp);
            }
            
            RefreshConnectionList();
            ConnectionListBox.SelectedItem = ConnectionListBox.Items[ConnectionListBox.Items.Count - 1];
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionListBox.SelectedItem is string selectedName)
            {
                if (_mainWindow.communications.TryGetValue(selectedName, out var commToDelete))
                {
                    commToDelete.Dispose();
                    _mainWindow.communications.Remove(selectedName);
                    RefreshConnectionList();
                    ConfigGroupBox.IsEnabled = false;
                }
            }
        }

        private void ConnectionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionListBox.SelectedItem is string selectedName)
            {
                if (_mainWindow.communications.TryGetValue(selectedName, out var comm))
                {
                    // 取消之前连接的事件订阅
                    if (_selectedCommunication != null)
                    {
                        _selectedCommunication.StatusChanged -= OnStatusChanged;
                        _selectedCommunication.MessageReceived -= OnMessageReceived;
                    }
                    
                    if (_selectedTcpServer != null)
                    {
                        _selectedTcpServer.ClientConnected -= OnClientConnected;
                        _selectedTcpServer.ClientDisconnected -= OnClientDisconnected;
                    }

                    _selectedCommunication = comm;
                    _selectedTcpServer = comm as ITcpServer;

                    // 订阅事件
                    _selectedCommunication.StatusChanged += OnStatusChanged;
                    _selectedCommunication.MessageReceived += OnMessageReceived;
                    
                    if (_selectedTcpServer != null)
                    {
                        _selectedTcpServer.ClientConnected += OnClientConnected;
                        _selectedTcpServer.ClientDisconnected += OnClientDisconnected;
                    }

                    ConfigGroupBox.IsEnabled = true;
                    NameTextBox.Text = comm.Name;
                    
                    // 根据类型更新界面
                    if (comm is TcpServer server)
                    {
                        ConnectionTypeComboBox.SelectedIndex = 1; // 服务器
                        IpLabel.Content = "监听地址:";
                        IpTextBox.Text = "0.0.0.0";
                        IpTextBox.IsEnabled = false;
                        
                        // 使用反射获取端口信息
                        var portField = typeof(TcpServer).GetField("_port", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        PortTextBox.Text = portField?.GetValue(server).ToString();
                        
                        ServerInfoPanel.Visibility = Visibility.Visible;
                        UpdateClientList();
                    }
                    else if (comm is TcpCommunication tcp)
                    {
                        ConnectionTypeComboBox.SelectedIndex = 0; // 客户端
                        IpLabel.Content = "IP 地址:";
                        IpTextBox.IsEnabled = true;
                        
                        var ipField = typeof(TcpCommunication).GetField("_ipAddress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var portField = typeof(TcpCommunication).GetField("_port", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        IpTextBox.Text = ipField?.GetValue(tcp).ToString();
                        PortTextBox.Text = portField?.GetValue(tcp).ToString();
                        
                        ServerInfoPanel.Visibility = Visibility.Collapsed;
                    }
                    
                    UpdateStatusUI(comm.Status);
                }
            }
        }

        private void OnStatusChanged(ConnectionStatus status)
        {
            Dispatcher.Invoke(() => UpdateStatusUI(status));
        }

        /// <summary>
        /// 客户端连接事件处理
        /// </summary>
        private void OnClientConnected(string clientId)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateClientList();
                LogMessage($"客户端已连接: {clientId}", Brushes.Green);
            });
        }

        /// <summary>
        /// 客户端断开事件处理
        /// </summary>
        private void OnClientDisconnected(string clientId)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateClientList();
                LogMessage($"客户端已断开: {clientId}", Brushes.Orange);
            });
        }

        /// <summary>
        /// 更新客户端列表显示
        /// </summary>
        private void UpdateClientList()
        {
            if (_selectedTcpServer == null)
                return;

            ClientListBox.Items.Clear();
            var clients = _selectedTcpServer.GetConnectedClients();
            foreach (var client in clients)
            {
                ClientListBox.Items.Add(client);
            }
            
            ClientCountTextBlock.Text = $"连接数: {_selectedTcpServer.ClientCount}";
        }

        /// <summary>
        /// 连接类型下拉框选择变化事件
        /// </summary>
        private void ConnectionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isServer = ConnectionTypeComboBox.SelectedIndex == 1;
            
            if (isServer)
            {
                IpLabel.Content = "监听地址:";
                IpTextBox.Text = "0.0.0.0";
                IpTextBox.IsEnabled = false;
                ServerInfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                IpLabel.Content = "IP 地址:";
                IpTextBox.Text = "127.0.0.1";
                IpTextBox.IsEnabled = true;
                ServerInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 客户端列表选择变化事件
        /// </summary>
        private void ClientListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 可以在这里添加对选中客户端的特殊处理
        }

        /// <summary>
        /// 断开选中客户端按钮点击事件
        /// </summary>
        private void DisconnectClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTcpServer == null || ClientListBox.SelectedItem == null)
                return;

            string clientId = ClientListBox.SelectedItem.ToString();
            if (_selectedTcpServer.DisconnectClient(clientId))
            {
                LogMessage($"已主动断开客户端: {clientId}", Brushes.Blue);
            }
            else
            {
                LogMessage($"断开客户端失败: {clientId}", Brushes.Red);
            }
        }
        /// <summary>
        /// 格式化消息用于显示
        /// </summary>
        private string FormatMessageForDisplay(Message message)
        {
            var parts = new List<string>
      {
          $"[{message.Type}]",
          message.Command,
          $"(ID:{message.Id})"
      };

            // 如果有参数，显示参数信息
            if (message.Parameters.Count > 0)
            {
                var paramStrs = message.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value}");
                parts.Add($"[{string.Join(", ", paramStrs)}]");
            }

            // 显示时间戳
            parts.Add($"@ {message.Timestamp:HH:mm:ss.fff}");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// 处理接收到的消息 - 业务逻辑处理
        /// </summary>
        private void HandleReceivedMessage(Message message)
        {
            switch (message.Command.ToUpper())
            {
                case "HEARTBEAT":
                    // 心跳消息 - 更新连接状态显示
                    UpdateLastHeartbeatTime();
                    break;

                case "TRIGGER":
                    // 触发命令 - 可能需要通知主窗口执行采集
                    NotifyMainWindowForTrigger(message);
                    break;

                case "SET_PARAMS":
                    // 参数设置命令 - 解析并应用参数
                    ApplyParameters(message);
                    break;

                case "GET_RESULT":
                    // 结果查询 - 返回检测结果
                    SendInspectionResult(message);
                    break;

                case "RESPONSE_OK":
                case "RESPONSE_ERROR":
                    // 响应消息 - 匹配原始命令
                    HandleResponse(message);
                    break;

                default:
                    // 未知命令 - 记录日志
                    LogMessage($"收到未知命令: {message.Command}", Brushes.Yellow);
                    break;
            }
        }

     

       
        /// <summary>
        /// 消息接收处理 - 展示如何处理不同类型的接收消息
        /// </summary>
        private void OnMessageReceived(Message message)
        {
            Dispatcher.Invoke(() =>
            {
                // 根据消息类型使用不同的颜色和处理逻辑
                Brush color;
                switch (message.Type)
                {
                    case MessageType.Command:
                        color = Brushes.Blue;
                        break;
                    case MessageType.Response:
                        color = Brushes.Green;
                        break;
                    case MessageType.Event:
                        color = Brushes.Orange;
                        break;
                    case MessageType.Heartbeat:
                        color = Brushes.Gray;
                        break;
                    default:
                        color = Brushes.Black;
                        break;
                }

                // 格式化显示消息
                string displayText = FormatMessageForDisplay(message);
                LogMessage($"接收 << {displayText}", color);

                // 根据消息类型进行特殊处理
                HandleReceivedMessage(message);
            });
        }
        private void UpdateStatusUI(ConnectionStatus status)
        {
            bool isServer = _selectedCommunication is ITcpServer;
            
            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    StatusTextBlock.Text = "未连接";
                    StatusTextBlock.Foreground = Brushes.Gray;
                    ConnectButton.Content = isServer ? "启动" : "连接";
                    break;
                case ConnectionStatus.Connecting:
                    StatusTextBlock.Text = isServer ? "启动中..." : "连接中...";
                    StatusTextBlock.Foreground = Brushes.Orange;
                    ConnectButton.Content = isServer ? "启动中..." : "连接中...";
                    break;
                case ConnectionStatus.Connected:
                    StatusTextBlock.Text = isServer ? "正在监听" : "已连接";
                    StatusTextBlock.Foreground = Brushes.Green;
                    ConnectButton.Content = isServer ? "停止" : "断开";
                    
                    // 如果是服务器，更新客户端列表
                    if (isServer)
                    {
                        UpdateClientList();
                    }
                    break;
                case ConnectionStatus.Error:
                    StatusTextBlock.Text = "错误";
                    StatusTextBlock.Foreground = Brushes.Red;
                    ConnectButton.Content = isServer ? "启动" : "连接";
                    break;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication == null) return;

            if (_selectedCommunication.Status == ConnectionStatus.Connected)
            {
                _selectedCommunication.Close();
            }
            else
            {
                await _selectedCommunication.OpenAsync();
            }
        }

        /// <summary>
        /// 发送按钮点击事件 - 展示如何发送不同类型的消息
        /// </summary>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCommunication?.Status != ConnectionStatus.Connected ||
                string.IsNullOrEmpty(SendTextBox.Text))
                return;

            string inputText = SendTextBox.Text.Trim();
            Message messageToSend;

            // 根据输入内容智能判断消息类型
            if (inputText.StartsWith("{"))
            {
                // JSON格式 - 直接解析为Message
                try
                {
                    var jsonObj = JObject.Parse(inputText);

                    messageToSend = new Message
                    {
                        Command = jsonObj["cmd"]?.ToString() ?? "UNKNOWN",
                        RawJsonBody = inputText
                    };

                    // 如果JSON中有params，也解析出来
                    if (jsonObj["params"] is JObject paramsObj)
                    {
                        foreach (var param in paramsObj)
                        {
                            messageToSend.Parameters[param.Key] = param.Value?.ToString();
                        }
                    }
                }
                catch (Exception)
                {
                    LogMessage("JSON格式错误", Brushes.Red);
                    return;
                }
            }
            else if (inputText.Contains("|"))
            {
                // 简单格式：COMMAND|param1=value1|param2=value2
                var parts = inputText.Split('|');
                messageToSend = new Message { Command = parts[0] };

                // 解析参数
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].Contains("="))
                    {
                        var kvp = parts[i].Split('=');
                        if (kvp.Length == 2)
                        {
                            messageToSend.Parameters[kvp[0]] = kvp[1];
                        }
                    }
                }
            }
            else
            {
                // 纯命令格式
                messageToSend = MessageBuilder.CreateCommand(inputText);
            }

            // 发送消息
            bool success = await _selectedCommunication.SendAsync(messageToSend);
            if (success)
            {
                LogMessage($"发送 >> [{messageToSend.Type}] {messageToSend.Command} (ID:{messageToSend.Id})",
                    Brushes.DarkGreen);
                SendTextBox.Clear();
            }
            else
            {
                LogMessage($"发送失败: {messageToSend}", Brushes.Red);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void LogMessage(string message, Brush color)
        {
            string log = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
            LogTextBox.AppendText(log);
            LogScrollViewer.ScrollToEnd();
        }

        /// <summary>
        /// 通知主窗口执行触发操作
        /// </summary>
        private void NotifyMainWindowForTrigger(Message message)
        {
            // 这里可以调用主窗口的触发方法
            // _mainWindow.TriggerCapture();
            LogMessage($"触发请求已接收 (ID:{message.Id})", Brushes.Blue);
            
            // 发送响应确认
            var response = MessageBuilder.CreateResponse(message.Id, "触发命令已执行", true);
            _ = _selectedCommunication?.SendAsync(response);
        }

        /// <summary>
        /// 处理响应消息
        /// </summary>
        private void HandleResponse(Message message)
        {
            var originalId = message.GetStringParameter("original_id");
            var result = message.GetStringParameter("result");
            var success = message.GetBoolParameter("success");
            
            string status = success ? "成功" : "失败";
            LogMessage($"收到响应 (原ID:{originalId}): {status} - {result}", 
                success ? Brushes.Green : Brushes.Red);
        }

        /// <summary>
        /// 更新最后心跳时间
        /// </summary>
        private void UpdateLastHeartbeatTime()
        {
            LogMessage("心跳信号正常", Brushes.Gray);
        }

        /// <summary>
        /// 应用参数设置
        /// </summary>
        private void ApplyParameters(Message message)
        {
            foreach (var param in message.Parameters)
            {
                switch (param.Key.ToLower())
                {
                    case "exposuretime":
                        int exposure = message.GetIntParameter("ExposureTime", 1000);
                        LogMessage($"设置曝光时间: {exposure}μs", Brushes.Blue);
                        break;

                    case "gain":
                        double gain = message.GetDoubleParameter("Gain", 1.0);
                        LogMessage($"设置增益: {gain}", Brushes.Blue);
                        break;

                    default:
                        LogMessage($"未知参数: {param.Key} = {param.Value}", Brushes.Yellow);
                        break;
                }
            }

            // 发送确认响应
            var response = MessageBuilder.CreateResponse(message.Id, "参数设置完成", true);
            _ = _selectedCommunication?.SendAsync(response);
        }

        /// <summary>
        /// 发送检测结果
        /// </summary>
        private void SendInspectionResult(Message message)
        {
            // 模拟获取检测结果
            var response = MessageBuilder.CreateResponse(message.Id, "检测完成", true,
                ("result", "PASS"),
                ("score", 95.6),
                ("defects", "无缺陷"),
                ("window_id", message.GetIntParameter("window", 1))
            );

            _ = _selectedCommunication?.SendAsync(response);
            LogMessage("检测结果已发送", Brushes.Green);
        }
    }
}