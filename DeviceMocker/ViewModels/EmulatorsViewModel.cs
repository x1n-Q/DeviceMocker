using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DeviceMocker.Core;
using DeviceMocker.Helpers;
using DeviceMocker.Models;

namespace DeviceMocker.ViewModels
{
    public class EmulatorsViewModel : ViewModelBase
    {
        private const int MaxLogEntries = 80;

        private DeviceProfile? _selectedProfile;
        private string _profileName = "POS Printer Emulator";
        private EmulatorDeviceFamily _selectedDeviceFamily = EmulatorDeviceFamily.ReceiptPrinter;
        private EmulatorProtocolType _selectedProtocol = EmulatorProtocolType.EscPos;
        private DrawerLinkMode _selectedDrawerLinkMode = DrawerLinkMode.PrinterDriven;
        private EmulatorTransportType _selectedTransport = EmulatorTransportType.Tcp;
        private string _selectedSerialPort = string.Empty;
        private int _baudRate = 9600;
        private string _tcpHost = "127.0.0.1";
        private int _tcpPort = 9100;
        private bool _renderReceiptPreview = true;
        private bool _autoStartWithSelectedProfile;
        private bool _isRunning;
        private string _listenerStatus = "Stopped";
        private string _sessionSummary = "No active sessions.";
        private string _receiptPreviewText = string.Empty;
        private string _statusMessage = string.Empty;
        private string _drawerState = "Closed";

        public ObservableCollection<DeviceProfile> Profiles { get; } = new();
        public ObservableCollection<string> AvailableSerialPorts { get; } = new();
        public ObservableCollection<EmulatorSessionLog> RawLogs { get; } = new();
        public ObservableCollection<EmulatorSessionLog> ParsedLogs { get; } = new();

        public DeviceProfile? SelectedProfile
        {
            get => _selectedProfile;
            set => SetProperty(ref _selectedProfile, value);
        }

        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }

        public EmulatorDeviceFamily SelectedDeviceFamily
        {
            get => _selectedDeviceFamily;
            set => SetProperty(ref _selectedDeviceFamily, value);
        }

        public EmulatorProtocolType SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetProperty(ref _selectedProtocol, value);
        }

        public DrawerLinkMode SelectedDrawerLinkMode
        {
            get => _selectedDrawerLinkMode;
            set => SetProperty(ref _selectedDrawerLinkMode, value);
        }

        public EmulatorTransportType SelectedTransport
        {
            get => _selectedTransport;
            set
            {
                if (SetProperty(ref _selectedTransport, value))
                {
                    OnPropertyChanged(nameof(IsSerialTransport));
                    OnPropertyChanged(nameof(IsTcpTransport));
                    OnPropertyChanged(nameof(IsHttpTransport));
                }
            }
        }

        public string SelectedSerialPort
        {
            get => _selectedSerialPort;
            set => SetProperty(ref _selectedSerialPort, value);
        }

        public int BaudRate
        {
            get => _baudRate;
            set => SetProperty(ref _baudRate, value);
        }

        public string TcpHost
        {
            get => _tcpHost;
            set => SetProperty(ref _tcpHost, value);
        }

        public int TcpPort
        {
            get => _tcpPort;
            set => SetProperty(ref _tcpPort, value);
        }

        public bool RenderReceiptPreview
        {
            get => _renderReceiptPreview;
            set => SetProperty(ref _renderReceiptPreview, value);
        }

        public bool AutoStartWithSelectedProfile
        {
            get => _autoStartWithSelectedProfile;
            set => SetProperty(ref _autoStartWithSelectedProfile, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public string ListenerStatus
        {
            get => _listenerStatus;
            set => SetProperty(ref _listenerStatus, value);
        }

        public string SessionSummary
        {
            get => _sessionSummary;
            set => SetProperty(ref _sessionSummary, value);
        }

        public string ReceiptPreviewText
        {
            get => _receiptPreviewText;
            set => SetProperty(ref _receiptPreviewText, value);
        }

        public ObservableCollection<ReceiptBlock> ReceiptBlocks { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string DrawerState
        {
            get => _drawerState;
            set => SetProperty(ref _drawerState, value);
        }

        public bool IsSerialTransport => SelectedTransport == EmulatorTransportType.Serial;
        public bool IsTcpTransport => SelectedTransport == EmulatorTransportType.Tcp;
        public bool IsHttpTransport => SelectedTransport == EmulatorTransportType.Http;

        public Array DeviceFamilyOptions { get; } = Enum.GetValues(typeof(EmulatorDeviceFamily));
        public Array ProtocolOptions { get; } = Enum.GetValues(typeof(EmulatorProtocolType));
        public Array DrawerLinkModeOptions { get; } = Enum.GetValues(typeof(DrawerLinkMode));
        public Array TransportOptions { get; } = Enum.GetValues(typeof(EmulatorTransportType));

        public IReadOnlyList<PaperWidthOption> PaperWidthOptions { get; } = new List<PaperWidthOption>
        {
            new("58mm", ReceiptPaperWidth.Mm58),
            new("80mm", ReceiptPaperWidth.Mm80),
        };

        public ReceiptPaperWidth SelectedPaperWidth
        {
            get => _selectedPaperWidth;
            set => SetProperty(ref _selectedPaperWidth, value);
        }

        private ReceiptPaperWidth _selectedPaperWidth = ReceiptPaperWidth.Mm80;

        public ICommand RefreshProfilesCommand { get; }
        public ICommand LoadProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand CreateProfileCommand { get; }
        public ICommand RefreshPortsCommand { get; }
        public ICommand StartHostCommand { get; }
        public ICommand StopHostCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand MarkDrawerClosedCommand { get; }
        public ICommand SaveAutoStartCommand { get; }
        public ICommand CopyRawLogsCommand { get; }
        public ICommand CopyParsedLogsCommand { get; }

        public EmulatorsViewModel()
        {
            RefreshProfilesCommand = new AsyncRelayCommand(RefreshProfilesAsync);
            LoadProfileCommand = new RelayCommand(LoadSelectedProfile, () => SelectedProfile != null);
            SaveProfileCommand = new AsyncRelayCommand(SaveSelectedProfileAsync, () => !string.IsNullOrWhiteSpace(ProfileName));
            CreateProfileCommand = new AsyncRelayCommand(CreateProfileAsync, () => !string.IsNullOrWhiteSpace(ProfileName));
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            StartHostCommand = new AsyncRelayCommand(StartHostAsync, () => !IsRunning);
            StopHostCommand = new AsyncRelayCommand(StopHostAsync, () => IsRunning);
            ClearLogsCommand = new RelayCommand(ClearLogs);
            MarkDrawerClosedCommand = new RelayCommand(MarkDrawerClosed);
            SaveAutoStartCommand = new AsyncRelayCommand(SaveAutoStartAsync);
            CopyRawLogsCommand = new RelayCommand(() => CopyLogs(RawLogs));
            CopyParsedLogsCommand = new RelayCommand(() => CopyLogs(ParsedLogs));

            ServiceLocator.EmulatorHost.LogReceived += OnHostLogReceived;
            ServiceLocator.EmulatorHost.StateChanged += ApplySnapshot;

            ApplySettingsDefaults();
            RefreshPorts();
            ApplySnapshot();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await RefreshProfilesAsync();
            await ServiceLocator.EmulatorHost.TryAutoStartAsync();
            ApplySnapshot();
        }

        private void ApplySettingsDefaults()
        {
            var defaults = ServiceLocator.EmulatorHost.BuildProfileFromDefaults();
            ApplyProfileSettings(defaults, "POS Printer Emulator");
            AutoStartWithSelectedProfile = ServiceLocator.Settings.Current.EmulatorAutoStart;
        }

        private async Task RefreshProfilesAsync()
        {
            var existingId = SelectedProfile?.Id;
            var profiles = await ServiceLocator.ProfileManager.GetAllProfilesAsync();

            Profiles.Clear();
            foreach (var profile in profiles.OrderBy(p => p.Name))
                Profiles.Add(profile);

            if (!string.IsNullOrEmpty(existingId))
                SelectedProfile = Profiles.FirstOrDefault(p => p.Id == existingId);
            else if (Profiles.Count > 0)
                SelectedProfile = Profiles[0];

            StatusMessage = $"Loaded {Profiles.Count} profile(s).";
        }

        private void LoadSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            var settings = SelectedProfile.EmulatorSettings?.Enabled == true
                ? SelectedProfile.EmulatorSettings
                : ServiceLocator.EmulatorHost.BuildProfileFromDefaults();

            ApplyProfileSettings(settings, SelectedProfile.Name);
            AutoStartWithSelectedProfile = ServiceLocator.Settings.Current.EmulatorAutoStart
                && ServiceLocator.Settings.Current.EmulatorAutoStartProfileId == SelectedProfile.Id;
            StatusMessage = $"Loaded emulator settings from '{SelectedProfile.Name}'.";
        }

        private async Task SaveSelectedProfileAsync()
        {
            DeviceProfile profile;
            if (SelectedProfile == null)
            {
                await CreateProfileAsync();
                return;
            }

            profile = SelectedProfile;
            profile.Name = ProfileName.Trim();
            profile.Description = "POS emulator profile";
            profile.DeviceType = SelectedDeviceFamily == EmulatorDeviceFamily.CashDrawer ? DeviceType.CashDrawer : DeviceType.ReceiptPrinter;
            profile.EmulatorSettings = BuildProfileSettings();

            await ServiceLocator.ProfileManager.SaveProfileAsync(profile);
            await RefreshProfilesAsync();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
            StatusMessage = $"Saved profile '{profile.Name}'.";
        }

        private async Task CreateProfileAsync()
        {
            var profile = new DeviceProfile
            {
                Name = ProfileName.Trim(),
                Description = "POS emulator profile",
                DeviceType = SelectedDeviceFamily == EmulatorDeviceFamily.CashDrawer ? DeviceType.CashDrawer : DeviceType.ReceiptPrinter,
                EmulatorSettings = BuildProfileSettings()
            };

            await ServiceLocator.ProfileManager.SaveProfileAsync(profile);
            await RefreshProfilesAsync();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
            StatusMessage = $"Created profile '{profile.Name}'.";
        }

        private void RefreshPorts()
        {
            var current = SelectedSerialPort;
            AvailableSerialPorts.Clear();
            foreach (var port in Services.SerialOutputService.GetAvailablePorts().OrderBy(x => x))
                AvailableSerialPorts.Add(port);

            if (AvailableSerialPorts.Count > 0)
                SelectedSerialPort = AvailableSerialPorts.Contains(current) ? current : AvailableSerialPorts[0];
        }

        private async Task StartHostAsync()
        {
            try
            {
                if (SelectedTransport == EmulatorTransportType.Serial && string.IsNullOrWhiteSpace(SelectedSerialPort))
                {
                    StatusMessage = "Select a COM port before starting the serial emulator host.";
                    return;
                }

                await ServiceLocator.EmulatorHost.StartAsync(BuildProfileSettings(), ProfileName.Trim());
                StatusMessage = "Emulator host started.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Start error: {ex.Message}";
            }
            finally
            {
                ApplySnapshot();
            }
        }

        private async Task StopHostAsync()
        {
            await ServiceLocator.EmulatorHost.StopAsync();
            StatusMessage = "Emulator host stopped.";
            ApplySnapshot();
        }

        private void ClearLogs()
        {
            RawLogs.Clear();
            ParsedLogs.Clear();
            StatusMessage = "Emulator logs cleared.";
        }

        private void MarkDrawerClosed()
        {
            ServiceLocator.CashDrawerEmulator.MarkClosed("Drawer manually marked closed from emulator host.");
            ApplySnapshot();
            StatusMessage = "Drawer marked closed.";
        }

        private async Task SaveAutoStartAsync()
        {
            if (AutoStartWithSelectedProfile && SelectedProfile == null)
            {
                StatusMessage = "Select or save a profile before enabling auto-start.";
                return;
            }

            var settings = ServiceLocator.Settings.Current;
            settings.EmulatorAutoStart = AutoStartWithSelectedProfile;
            settings.EmulatorAutoStartProfileId = AutoStartWithSelectedProfile ? SelectedProfile?.Id ?? string.Empty : string.Empty;
            await ServiceLocator.Settings.SaveAsync(settings);
            StatusMessage = AutoStartWithSelectedProfile
                ? "Auto-start saved for the selected emulator profile."
                : "Emulator auto-start disabled.";
        }

        private EmulatorProfileSettings BuildProfileSettings()
        {
            return new EmulatorProfileSettings
            {
                Enabled = true,
                DeviceFamily = SelectedDeviceFamily,
                Protocol = SelectedProtocol,
                DrawerLinkMode = SelectedDrawerLinkMode,
                RenderReceiptPreview = RenderReceiptPreview,
                PaperWidth = SelectedPaperWidth,
                Endpoint = new EmulatorEndpointConfig
                {
                    Transport = SelectedTransport,
                    SerialPortName = SelectedSerialPort,
                    BaudRate = BaudRate,
                    TcpHost = TcpHost,
                    TcpPort = TcpPort,
                    HttpPort = ServiceLocator.Settings.Current.DefaultEmulatorHttpPort,
                    HttpRoute = ServiceLocator.Settings.Current.DefaultEmulatorHttpRoute,
                    AutoStart = AutoStartWithSelectedProfile,
                    EncodingMode = "RawBytes"
                }
            };
        }

        private void ApplyProfileSettings(EmulatorProfileSettings settings, string profileName)
        {
            ProfileName = profileName;
            SelectedDeviceFamily = settings.DeviceFamily;
            SelectedProtocol = settings.Protocol;
            SelectedDrawerLinkMode = settings.DrawerLinkMode;
            SelectedTransport = settings.Endpoint.Transport;
            SelectedSerialPort = settings.Endpoint.SerialPortName;
            BaudRate = settings.Endpoint.BaudRate;
            TcpHost = settings.Endpoint.TcpHost;
            TcpPort = settings.Endpoint.TcpPort;
            RenderReceiptPreview = settings.RenderReceiptPreview;
            SelectedPaperWidth = settings.PaperWidth;
        }

        private void OnHostLogReceived(EmulatorSessionLog log)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (log.Kind == EmulatorSessionLogKind.Raw)
                    InsertLog(RawLogs, log);
                else
                    InsertLog(ParsedLogs, log);
            });
        }

        private void ApplySnapshot()
        {
            var snapshot = ServiceLocator.EmulatorHost.GetSnapshot();
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsRunning = snapshot.IsRunning;
                ListenerStatus = snapshot.ListenerStatus;
                SessionSummary = snapshot.SessionSummary;
                ReceiptPreviewText = string.IsNullOrWhiteSpace(snapshot.ReceiptPreview)
                    ? "(No receipt output yet)"
                    : snapshot.ReceiptPreview;
                ReceiptBlocks.Clear();
                foreach (var block in snapshot.ReceiptBlocks)
                    ReceiptBlocks.Add(block);
                DrawerState = snapshot.IsDrawerOpen ? "Open" : "Closed";
            });
        }

        private static void InsertLog(ObservableCollection<EmulatorSessionLog> target, EmulatorSessionLog log)
        {
            target.Insert(0, log);
            while (target.Count > MaxLogEntries)
                target.RemoveAt(target.Count - 1);
        }

        private static void CopyLogs(ObservableCollection<EmulatorSessionLog> logs)
        {
            var text = string.Join(Environment.NewLine, logs.Select(log =>
                $"{log.Timestamp:HH:mm:ss.fff} [{log.Kind}] {log.Message}{(string.IsNullOrWhiteSpace(log.DataHex) ? string.Empty : " " + log.DataHex)}"));
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch
            {
            }
        }
    }

    public sealed record PaperWidthOption(string Label, ReceiptPaperWidth Value);
}
