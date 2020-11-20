﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.IO;
using System.ServiceProcess;
using System.Linq;
using System.Diagnostics;
using System.Windows.Controls;
using System.Drawing;
using System.Threading.Tasks;

using ZitiDesktopEdge.Models;
using ZitiDesktopEdge.DataStructures;
using ZitiDesktopEdge.ServiceClient;

using NLog;
using NLog.Config;
using NLog.Targets;

namespace ZitiDesktopEdge {

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public System.Windows.Forms.NotifyIcon notifyIcon;
		public string Position = "Bottom";
		private DateTime _startDate;
		private System.Windows.Forms.Timer _tunnelUptimeTimer;
		private DataClient serviceClient = null;
		MonitorClient monitorClient = null;
		private bool _isAttached = true;
		private bool _isServiceInError = false;
		private int _right = 75;
		private int _left = 75;
		private int _top = 30;
		private double _maxHeight = 800d;
		private string[] suffixes = { "Bps", "kBps", "mBps", "gBps", "tBps", "pBps" };

		private List<ZitiIdentity> identities {
			get {
				return (List<ZitiIdentity>)Application.Current.Properties["Identities"];
			}
		}

		public MainWindow() {
			InitializeComponent();

			#if DEBUG
			System.Environment.SetEnvironmentVariable("ZITI_EXTENDED_DEBUG", "true");
#endif
			var asm = System.Reflection.Assembly.GetExecutingAssembly();
			var curdir = Path.GetDirectoryName(asm.Location);
			string nlogFile = Path.Combine(curdir, asm.ManifestModule.Name + ".log.config");

			if (File.Exists(nlogFile)) {
				LogManager.Configuration = new XmlLoggingConfiguration(nlogFile);
			} else {
				var config = new LoggingConfiguration();
				var logname = asm.GetName().Name;
				// Targets where to log to: File and Console
				var logfile = new FileTarget("logfile") {
					FileName = $"{logname}.log",
					ArchiveEvery = FileArchivePeriod.Day,
					ArchiveNumbering = ArchiveNumberingMode.Rolling,
					MaxArchiveFiles = 7,
					Layout = "${longdate}|${level:uppercase=true:padding=5}|${logger}|${message}",
					//ArchiveAboveSize = 10000,
				};
				var logconsole = new ConsoleTarget("logconsole");

				// Rules for mapping loggers to targets            
				config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
				config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

				// Apply config           
				LogManager.Configuration = config;
			}
			Logger.Info("service started - logger initialized");

			App.Current.MainWindow.WindowState = WindowState.Normal;
			App.Current.MainWindow.Closing += MainWindow_Closing;
			App.Current.MainWindow.Deactivated += MainWindow_Deactivated;
			App.Current.MainWindow.Activated += MainWindow_Activated;
			notifyIcon = new System.Windows.Forms.NotifyIcon();
			notifyIcon.Visible = true;
			notifyIcon.Click += TargetNotifyIcon_Click;
			notifyIcon.Visible = true;
			IdentityMenu.OnDetach += OnDetach;
			MainMenu.OnDetach += OnDetach;

			SetNotifyIcon("white");
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
			OnDetach(e);
		}

		private void OnDetach(MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left) {
				_isAttached = false;
				IdentityMenu.Arrow.Visibility = Visibility.Collapsed;
				Arrow.Visibility = Visibility.Collapsed;
				MainMenu.Detach();
				this.DragMove();
			}
		}

		private void MainWindow_Activated(object sender, EventArgs e) {
			this.Visibility = Visibility.Visible;
			Placement();
		}

		private void MainWindow_Deactivated(object sender, EventArgs e) {
			if (this._isAttached) {
#if DEBUG
				Debug.WriteLine("debug is enabled - windows pinned");
#else
				this.Visibility = Visibility.Collapsed;
#endif
			}
		}

		private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			notifyIcon.Visible = false;
			notifyIcon.Icon.Dispose();
			notifyIcon.Dispose();
		}

		private void SetCantDisplay(string msg, string detailMessage) {
			NoServiceView.Visibility = Visibility.Visible;
			ErrorMsg.Content = msg;
			ErrorMsgDetail.Content = detailMessage;
			SetNotifyIcon("red");
			_isServiceInError = true;
			UpdateServiceView();
		}
		private void SetCantDisplay(string msg) {
			SetCantDisplay("Service Not Started", msg);
		}

		private void TargetNotifyIcon_Click(object sender, EventArgs e) {
			this.Show();
			this.Activate();
		}

		private void UpdateServiceView() {
			if (_isServiceInError) {
				AddIdAreaButton.Opacity = 0.1;
				AddIdAreaButton.IsEnabled = false;
				AddIdButton.Opacity = 0.1;
				AddIdButton.IsEnabled = false;
				DisconnectButton.Visibility = Visibility.Collapsed;
				ConnectButton.Visibility = Visibility.Visible;
				ConnectButton.Opacity = 0.1;
				StatArea.Opacity = 0.1;
			} else {
				AddIdAreaButton.Opacity = 1.0;
				AddIdAreaButton.IsEnabled = true;
				AddIdButton.Opacity = 1.0;
				AddIdButton.IsEnabled = true;
				ConnectButton.Visibility = Visibility.Collapsed;
				DisconnectButton.Visibility = Visibility.Visible;
				StatArea.Opacity = 1.0;
				ConnectButton.Opacity = 1.0;
			}
		}

		async private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			// add a new service client
			serviceClient = new DataClient();
			serviceClient.OnClientConnected += ServiceClient_OnClientConnected;
			serviceClient.OnClientDisconnected += ServiceClient_OnClientDisconnected;
			serviceClient.OnIdentityEvent += ServiceClient_OnIdentityEvent;
			serviceClient.OnMetricsEvent += ServiceClient_OnMetricsEvent;
			serviceClient.OnServiceEvent += ServiceClient_OnServiceEvent;
			serviceClient.OnTunnelStatusEvent += ServiceClient_OnTunnelStatusEvent;

			monitorClient = new MonitorClient();
			monitorClient.OnClientConnected += MonitorClient_OnClientConnected;
			monitorClient.OnMonitorStatusEvent += MonitorClient_OnMonitorStatusEvent;

			Application.Current.Properties.Add("ServiceClient", serviceClient);
			Application.Current.Properties.Add("Identities", new List<ZitiIdentity>());
			MainMenu.OnAttachmentChange += AttachmentChanged;
			MainMenu.OnLogLevelChanged += LogLevelChanged;
			IdentityMenu.OnError += IdentityMenu_OnError;

			try {
				await serviceClient.ConnectAsync();
				await serviceClient.WaitForConnectionAsync();
			} catch /*ignored for now (Exception ex) */{
				SetCantDisplay("Start the Ziti Tunnel Service to continue");
				serviceClient.Reconnect();
			}

			try {
				await monitorClient.ConnectAsync();
				await monitorClient.WaitForConnectionAsync();
			} catch /*ignored for now (Exception ex) */{
				monitorClient.Reconnect();
			}

			IdentityMenu.OnForgot += IdentityForgotten;
			Placement();
		}

		private void MonitorClient_OnMonitorStatusEvent(object sender, MonitorStatusEvent evt) {
			Debug.WriteLine("MonitorClient_OnMonitorStatusEvent");
			ServiceControllerStatus status = (ServiceControllerStatus)Enum.Parse(typeof(ServiceControllerStatus), evt.Status);
			action = evt.Status;
			switch (status) {
				case ServiceControllerStatus.Running:
					Logger.Info("Service is started");
					action = "stop";
					break;
				case ServiceControllerStatus.Stopped:
					Logger.Info("Service is stopped");
					action = "start";
					break;
				case ServiceControllerStatus.StopPending:
					Logger.Info("Service is stopping...");
					break;
				case ServiceControllerStatus.StartPending:
					Logger.Info("Service is starting...");
					break;
				case ServiceControllerStatus.PausePending:
					Logger.Warn("UNEXPECTED STATUS: PausePending");
					break;
				case ServiceControllerStatus.Paused:
					Logger.Warn("UNEXPECTED STATUS: Paused");
					break;
				default:
					Logger.Warn("UNEXPECTED STATUS: {0}", evt.Status);
					break;
			}
			this.Dispatcher.Invoke(() => {
				this.serviceStatus.Content = "Service is :" + evt.Status;
			});
		}

		private void MonitorClient_OnClientConnected(object sender, object e) {
			Debug.WriteLine("MonitorClient_OnClientConnected");
		}

		private void LogLevelChanged(string level) {
			serviceClient.SetLogLevelAsync(level).Wait();
		}

		private void IdentityMenu_OnError(string message) {
			ShowError("Identity Error", message);
		}

		private void ServiceClient_OnClientConnected(object sender, object e) {
			this.Dispatcher.Invoke(() => {
				//e is _ALWAYS_ null at this time use this to display something if you want
				NoServiceView.Visibility = Visibility.Collapsed;
				_isServiceInError = false;
				UpdateServiceView();
				SetNotifyIcon("white");
				LoadIdentities(true);
			});
		}

		private void ServiceClient_OnClientDisconnected(object sender, object e) {
			this.Dispatcher.Invoke(() => {
				IdList.Children.Clear();
				SetCantDisplay("Start the Ziti Tunnel Service to continue");
			});
		}

		private void ServiceClient_OnIdentityEvent(object sender, IdentityEvent e) {
			if (e == null) return;

			ZitiIdentity zid = ZitiIdentity.FromClient(e.Id);
			Debug.WriteLine($"==== IdentityEvent    : action:{e.Action} fingerprint:{e.Id.FingerPrint} name:{e.Id.Name} ");

			this.Dispatcher.Invoke(() => {
				if (e.Action == "added") {
					var found = identities.Find(i => i.Fingerprint == e.Id.FingerPrint);
					if (found == null) {
						identities.Add(zid);
						LoadIdentities(true);
					} else {
						//if we get here exit out so that LoadIdentities() doesn't get called
						found.IsEnabled = true;
						return;
					}
				} else {
					IdentityForgotten(ZitiIdentity.FromClient(e.Id));
				}
			});
			Debug.WriteLine($"IDENTITY EVENT. Action: {e.Action} fingerprint: {zid.Fingerprint}");
		}

		private void ServiceClient_OnMetricsEvent(object sender, List<Identity> ids) {
			if (ids != null) {
				long totalUp = 0;
				long totalDown = 0;
				foreach (var id in ids) {
					//Debug.WriteLine($"==== MetricsEvent     : id {id.Name} down: {id.Metrics.Down} up:{id.Metrics.Up}");
					if (id?.Metrics != null) {
						totalDown += id.Metrics.Down;
						totalUp += id.Metrics.Up;
					}
				}
				this.Dispatcher.Invoke(() => {
					SetSpeed(totalUp, UploadSpeed, UploadSpeedLabel);
					SetSpeed(totalDown, DownloadSpeed, DownloadSpeedLabel);
				});
			}
		}

		public void SetSpeed(decimal bytes, Label speed, Label speedLabel) {
			int counter = 0;
			while (Math.Round(bytes / 1024) >= 1) {
				bytes = bytes / 1024;
				counter++;
			}
			speed.Content = bytes.ToString("0.0");
			speedLabel.Content = suffixes[counter];
		}

		private void ServiceClient_OnServiceEvent(object sender, ServiceEvent e) {
			if (e == null) return;

			Debug.WriteLine($"==== ServiceEvent     : action:{e.Action} fingerprint:{e.Fingerprint} name:{e.Service.Name} ");
			this.Dispatcher.Invoke(() => {
				var found = identities.Find(id => id.Fingerprint == e.Fingerprint);

				if (found == null) {
					Debug.WriteLine($"{e.Action} service event for {e.Service.Name} but the provided identity fingerprint {e.Fingerprint} is not found!");
					return;
				}

				if (e.Action == "added") {
					ZitiService zs = new ZitiService(e.Service);
					var svc = found.Services.Find(s => s.Name == zs.Name);
					if (svc == null) found.Services.Add(zs);
					else Debug.WriteLine("the service named " + zs.Name + " is already accounted for on this identity.");
				} else {
					Debug.WriteLine("removing the service named: " + e.Service.Name);
					found.Services.RemoveAll(s => s.Name == e.Service.Name);
				}
				LoadIdentities(false);
				IdentityDetails deets = ((MainWindow)Application.Current.MainWindow).IdentityMenu;
				if (deets.IsVisible) {
					deets.UpdateView();
				}
			});
		}

		private void ServiceClient_OnTunnelStatusEvent(object sender, TunnelStatusEvent e) {
			if (e == null) return; //just skip it for now...
			Debug.WriteLine($"==== TunnelStatusEvent: ");
			Application.Current.Properties.Add("CurrentTunnelStatus", e.Status);
			e.Status.Dump(Console.Out);
			this.Dispatcher.Invoke(() => {
				if (e.ApiVersion != DataClient.EXPECTED_API_VERSION) {
					SetCantDisplay("Version mismatch!", "The version of the Service is not compatible");
					return;
				}
				this.MainMenu.LogLevel = e.Status.LogLevel;
				InitializeTimer((int)e.Status.Duration);
				LoadStatusFromService(e.Status);
				LoadIdentities(true);

				IdentityDetails deets = ((MainWindow)Application.Current.MainWindow).IdentityMenu;
				if (deets.IsVisible) {
					deets.UpdateView();
				}
			});
		}

		private void IdentityForgotten(ZitiIdentity forgotten) {
			ZitiIdentity idToRemove = null;
			foreach (var id in identities) {
				if (id.Fingerprint == forgotten.Fingerprint) {
					idToRemove = id;
					break;
				}
			}
			identities.Remove(idToRemove);
			LoadIdentities(false);
		}

		private void AttachmentChanged(bool attached) {
			_isAttached = attached;
			if (!_isAttached) {
				SetLocation();
			}
			Placement();
			MainMenu.Visibility = Visibility.Collapsed;
		}

		private void LoadStatusFromService(TunnelStatus status) {
			//clear any identities
			this.identities.Clear();

			if (status != null) {
				_isServiceInError = false;
				UpdateServiceView();
				NoServiceView.Visibility = Visibility.Collapsed;
				SetNotifyIcon("white");
				if (status.Active) {
					InitializeTimer((int)status.Duration);
					ConnectButton.Visibility = Visibility.Collapsed;
					DisconnectButton.Visibility = Visibility.Visible;
					SetNotifyIcon("green");
				} else {
					ConnectButton.Visibility = Visibility.Visible;
					DisconnectButton.Visibility = Visibility.Collapsed;
				}
				if (!Application.Current.Properties.Contains("ip")) {
					Application.Current.Properties.Add("ip", status?.IpInfo?.Ip);
				}
				if (!Application.Current.Properties.Contains("subnet")) {
					Application.Current.Properties.Add("subnet", status?.IpInfo?.Subnet);
				}
				if (!Application.Current.Properties.Contains("mtu")) {
					Application.Current.Properties.Add("mtu", status?.IpInfo?.MTU);
				}
				if (!Application.Current.Properties.Contains("dns")) {
					Application.Current.Properties.Add("dns", status?.IpInfo?.DNS);
				}

				foreach (var id in status.Identities) {
					updateViewWithIdentity(id);
				}
				LoadIdentities(true);
			} else {
				SetCantDisplay("Start the Ziti Tunnel Service to continue");
			}
		}

		private void updateViewWithIdentity(Identity id) {
			var zid = ZitiIdentity.FromClient(id);
			foreach (var i in identities) {
				if (i.Fingerprint == zid.Fingerprint) {
					identities.Remove(i);
					break;
				}
			}
			identities.Add(zid);
		}
		private void SetNotifyIcon(string iconPrefix) {
			var iconUri = new Uri("pack://application:,,/Assets/Images/ziti-" + iconPrefix + ".ico");
			System.IO.Stream iconStream = System.Windows.Application.GetResourceStream(iconUri).Stream;
			notifyIcon.Icon = new System.Drawing.Icon(iconStream);

			Application.Current.MainWindow.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
		}

		private void LoadIdentities(Boolean repaint) {
			IdList.Children.Clear();
			IdList.Height = 0;
			IdList.MaxHeight = _maxHeight - 520;
			ZitiIdentity[] ids = identities.ToArray();
			double height = 490 + (ids.Length * 60);
			if (height > _maxHeight) height = _maxHeight;
			this.Height = height;
			IdentityMenu.SetHeight(this.Height - 160);
			bool isActive = false;
			for (int i = 0; i < ids.Length; i++) {
				IdentityItem id = new IdentityItem();
				if (ids[i].IsEnabled) {
					isActive = true;
					SetNotifyIcon("green");
					ConnectButton.Visibility = Visibility.Collapsed;
					DisconnectButton.Visibility = Visibility.Visible;
				}
				id.OnStatusChanged += Id_OnStatusChanged;
				id.Identity = ids[i];
				IdList.Children.Add(id);
			}
			if (isActive) {
				ConnectButton.Visibility = Visibility.Collapsed;
				DisconnectButton.Visibility = Visibility.Visible;
			} else {
				ConnectButton.Visibility = Visibility.Visible;
				DisconnectButton.Visibility = Visibility.Collapsed;
			}
			IdList.Height = (double)(ids.Length * 64);
			Placement();
		}

		private void Id_OnStatusChanged(bool attached) {
			bool isActive = false;
			for (int i = 0; i < IdList.Children.Count; i++) {
				IdentityItem item = IdList.Children[i] as IdentityItem;
				if (item.ToggleSwitch.Enabled) {
					isActive = true;
					break;
				}
			}
			if (isActive) {
				ConnectButton.Visibility = Visibility.Collapsed;
				DisconnectButton.Visibility = Visibility.Visible;
			} else {
				ConnectButton.Visibility = Visibility.Visible;
				DisconnectButton.Visibility = Visibility.Collapsed;
			}
		}

		private void SetLocation() {
			var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;


			var height = MainView.ActualHeight;
			IdentityMenu.MainHeight = MainView.ActualHeight;

			Rectangle trayRectangle = WinAPI.GetTrayRectangle();
			if (trayRectangle.Top < 20) {
				this.Position = "Top";
				this.Top = desktopWorkingArea.Top + _top;
				this.Left = desktopWorkingArea.Right - this.Width - _right;
				Arrow.SetValue(Canvas.TopProperty, (double)0);
				Arrow.SetValue(Canvas.LeftProperty, (double)185);
				MainMenu.Arrow.SetValue(Canvas.TopProperty, (double)0);
				MainMenu.Arrow.SetValue(Canvas.LeftProperty, (double)185);
				IdentityMenu.Arrow.SetValue(Canvas.TopProperty, (double)0);
				IdentityMenu.Arrow.SetValue(Canvas.LeftProperty, (double)185);
			} else if (trayRectangle.Left < 20) {
				this.Position = "Left";
				this.Left = _left;
				this.Top = desktopWorkingArea.Bottom - this.ActualHeight - 75;
				Arrow.SetValue(Canvas.TopProperty, height - 200);
				Arrow.SetValue(Canvas.LeftProperty, (double)0);
				MainMenu.Arrow.SetValue(Canvas.TopProperty, height - 200);
				MainMenu.Arrow.SetValue(Canvas.LeftProperty, (double)0);
				IdentityMenu.Arrow.SetValue(Canvas.TopProperty, height - 200);
				IdentityMenu.Arrow.SetValue(Canvas.LeftProperty, (double)0);
			} else if (desktopWorkingArea.Right == (double)trayRectangle.Left) {
				this.Position = "Right";
				this.Left = desktopWorkingArea.Right - this.Width - 20;
				this.Top = desktopWorkingArea.Bottom - height - 75;
				Arrow.SetValue(Canvas.TopProperty, height - 100);
				Arrow.SetValue(Canvas.LeftProperty, this.Width - 30);
				MainMenu.Arrow.SetValue(Canvas.TopProperty, height - 100);
				MainMenu.Arrow.SetValue(Canvas.LeftProperty, this.Width - 30);
				IdentityMenu.Arrow.SetValue(Canvas.TopProperty, height - 100);
				IdentityMenu.Arrow.SetValue(Canvas.LeftProperty, this.Width - 30);
			} else {
				this.Position = "Bottom";
				this.Left = desktopWorkingArea.Right - this.Width - 75;
				this.Top = desktopWorkingArea.Bottom - height;
				Arrow.SetValue(Canvas.TopProperty, height - 35);
				Arrow.SetValue(Canvas.LeftProperty, (double)185);
				MainMenu.Arrow.SetValue(Canvas.TopProperty, height - 35);
				MainMenu.Arrow.SetValue(Canvas.LeftProperty, (double)185);
				IdentityMenu.Arrow.SetValue(Canvas.TopProperty, height - 35);
				IdentityMenu.Arrow.SetValue(Canvas.LeftProperty, (double)185);
			}
		}
		public void Placement() {
			var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
			if (_isAttached) {
				Arrow.Visibility = Visibility.Visible;
				IdentityMenu.Arrow.Visibility = Visibility.Visible;
				SetLocation();
			} else {
				IdentityMenu.Arrow.Visibility = Visibility.Collapsed;
				Arrow.Visibility = Visibility.Collapsed;
			}
		}

		private void OpenIdentity(ZitiIdentity identity) {
			IdentityMenu.Identity = identity;

		}

		private void ShowMenu(object sender, MouseButtonEventArgs e) {
			MainMenu.Visibility = Visibility.Visible;
		}

		async private void AddIdentity(object sender, MouseButtonEventArgs e) {
			UIModel.HideOnLostFocus = false;
			Microsoft.Win32.OpenFileDialog jwtDialog = new Microsoft.Win32.OpenFileDialog();
			UIModel.HideOnLostFocus = true;
			jwtDialog.DefaultExt = ".jwt";
			jwtDialog.Filter = "Ziti Identities (*.jwt)|*.jwt";
			if (jwtDialog.ShowDialog() == true) {
				ShowLoad();
				string fileContent = File.ReadAllText(jwtDialog.FileName);

				try {
					Identity createdId = await serviceClient.AddIdentityAsync(System.IO.Path.GetFileName(jwtDialog.FileName), false, fileContent);

					await serviceClient.IdentityOnOffAsync(createdId.FingerPrint, true);
					if (createdId != null) {
						identities.Add(ZitiIdentity.FromClient(createdId));
						LoadIdentities(true);
					} else {
						ShowError("Identity Error", "Identity Id was null, please try again");
					}
				} catch (ServiceException se) {
					ShowError("Error Occurred", se.Message + " " + se.AdditionalInfo);
				} catch (Exception ex) {
					ShowError("Unexpected Error", "Code 2:" + ex.Message);
				}
				HideLoad();
			}
		}

		private void OnTimedEvent(object sender, EventArgs e) {
			TimeSpan span = (DateTime.Now - _startDate);
			int hours = span.Hours;
			int minutes = span.Minutes;
			int seconds = span.Seconds;
			var hoursString = (hours > 9) ? hours.ToString() : "0" + hours;
			var minutesString = (minutes > 9) ? minutes.ToString() : "0" + minutes;
			var secondsString = (seconds > 9) ? seconds.ToString() : "0" + seconds;
			ConnectedTime.Content = hoursString + ":" + minutesString + ":" + secondsString;
		}

		private void InitializeTimer(int millisAgoStarted) {
			_startDate = DateTime.Now.Subtract(new TimeSpan(0, 0, 0, 0, millisAgoStarted));
			_tunnelUptimeTimer = new System.Windows.Forms.Timer();
			_tunnelUptimeTimer.Interval = 100;
			_tunnelUptimeTimer.Tick += OnTimedEvent;
			_tunnelUptimeTimer.Enabled = true;
			_tunnelUptimeTimer.Start();
		}
		private void Connect(object sender, RoutedEventArgs e) {
			if (!_isServiceInError) {
				ShowLoad();
				this.Dispatcher.Invoke(async () => {
					//Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle);
					await DoConnectAsync();
					HideLoad();
				});
			}
		}

		async private Task DoConnectAsync() {
			try {
				serviceClient.SetTunnelState(true);
				SetNotifyIcon("green");
				ConnectButton.Visibility = Visibility.Collapsed;
				DisconnectButton.Visibility = Visibility.Visible;

				for (int i = 0; i < identities.Count; i++) {
					await serviceClient.IdentityOnOffAsync(identities[i].Fingerprint, true);
				}
				for (int i = 0; i < IdList.Children.Count; i++) {
					IdentityItem item = IdList.Children[i] as IdentityItem;
					item._identity.IsEnabled = true;
					item.RefreshUI();
				}
			} catch (ServiceException se) {
				ShowError("Error Occurred", se.Message + " " + se.AdditionalInfo);
			} catch (Exception ex) {
				ShowError("Unexpected Error", "Code 3:" + ex.Message);
			}
		}
		async private void Disconnect(object sender, RoutedEventArgs e) {
			if (!_isServiceInError) {
				ShowLoad();
				try {
					ConnectedTime.Content = "00:00:00";
					_tunnelUptimeTimer.Stop();
					serviceClient.SetTunnelState(false);
					SetNotifyIcon("white");
					ConnectButton.Visibility = Visibility.Visible;
					DisconnectButton.Visibility = Visibility.Collapsed;
					for (int i = 0; i < identities.Count; i++) {
						await serviceClient.IdentityOnOffAsync(identities[i].Fingerprint, false);
					}
					for (int i = 0; i < IdList.Children.Count; i++) {
						IdentityItem item = IdList.Children[i] as IdentityItem;
						item._identity.IsEnabled = false;
						item.RefreshUI();
					}
				} catch (ServiceException se) {
					ShowError(se.AdditionalInfo, se.Message);
				} catch (Exception ex) {
					ShowError("Unexpected Error", "Code 4:" + ex.Message);
				}
				HideLoad();
			}
		}

		private void ShowLoad() {
			LoadProgress.IsIndeterminate = true;
			LoadingScreen.Visibility = Visibility.Visible;
			((MainWindow)System.Windows.Application.Current.MainWindow).UpdateLayout();
		}

		private void HideLoad() {
			LoadingScreen.Visibility = Visibility.Collapsed;
			LoadProgress.IsIndeterminate = false;
		}

		private void FormFadeOut_Completed(object sender, EventArgs e) {
			closeCompleted = true;
			//this.Close();
		}
		private bool closeCompleted = false;
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (!closeCompleted) {
				FormFadeOut.Begin();
				e.Cancel = true;
			}
		}

		private void ShowError(String title, String message) {
			ErrorTitle.Content = title;
			ErrorDetails.Text = message;
			ErrorView.Visibility = Visibility.Visible;
		}

		private void CloseError(object sender, RoutedEventArgs e) {
			ErrorView.Visibility = Visibility.Collapsed;
			NoServiceView.Visibility = Visibility.Collapsed;
		}

		private void CloseApp(object sender, RoutedEventArgs e) {
			Application.Current.Shutdown();
		}

		private void MainUI_Deactivated(object sender, EventArgs e) {
			if (this._isAttached) {
#if DEBUG
				Debug.WriteLine("debug is enabled - windows pinned");
#else
				this.Visibility = Visibility.Collapsed;
#endif
			}
		}

		private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			Placement();
		}

		async private void Button_Click(object sender, RoutedEventArgs e) {
			await serviceClient.SetLogLevelAsync(NextLevel());
		}

		int cur = 0;
		LogLevelEnum[] levels = new LogLevelEnum[] { LogLevelEnum.FATAL, LogLevelEnum.ERROR, LogLevelEnum.WARN, LogLevelEnum.INFO, LogLevelEnum.DEBUG, LogLevelEnum.TRACE, LogLevelEnum.VERBOSE };
		public LogLevelEnum NextLevel() {
			cur++;
			if (cur > 6) {
				cur = 0;
			}
			return levels[cur];
		}

		private void IdList_LayoutUpdated(object sender, EventArgs e) {
			Placement();
		}

		string action = "stop";
		async private void Button_Click_1(object sender, RoutedEventArgs e) {
			if (action == "Stopped") {
				action = "start";
				await monitorClient.StartServicAsync();
			} else {
				action = "Running";
				await monitorClient.StopServicAsync();
			}
			Logger.Info("button 1 1 1 1!");
		}
		async private void Button_Click_2(object sender, RoutedEventArgs e) {
			Logger.Info("button 2!");
			await Task.Delay(10);
		}
    }
}
