using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceMocker.Core;
using DeviceMocker.Interfaces;
using DeviceMocker.Models;

namespace DeviceMocker.Services
{
    public class EmulatorHostService
    {
        private readonly SettingsService _settingsService;
        private readonly ProfileManager _profileManager;
        private readonly CashDrawerEmulator _cashDrawerEmulator;
        private readonly EscPosPrinterEmulator _escPosPrinterEmulator;

        private SerialEmulatorListener? _serialListener;
        private TcpEmulatorListener? _tcpListener;
        private CancellationTokenSource? _runtimeCancellationTokenSource;
        private IEmulatorModule? _activeModule;
        private string _currentProfileName = "Manual Session";
        private string _listenerStatus = "Stopped";
        private string _sessionSummary = "No active sessions.";
        private int _sessionCount;

        public EmulatorProfileSettings CurrentProfile { get; private set; } = new();
        public bool IsRunning { get; private set; }

        public CashDrawerEmulator CashDrawer => _cashDrawerEmulator;

        public event Action<EmulatorSessionLog>? LogReceived;
        public event Action? StateChanged;

        public EmulatorHostService(SettingsService settingsService, ProfileManager profileManager, CashDrawerEmulator cashDrawerEmulator)
        {
            _settingsService = settingsService;
            _profileManager = profileManager;
            _cashDrawerEmulator = cashDrawerEmulator;
            _escPosPrinterEmulator = new EscPosPrinterEmulator(cashDrawerEmulator);

            _cashDrawerEmulator.LogProduced += RelayModuleLog;
            _cashDrawerEmulator.StateChanged += RaiseStateChanged;
            _escPosPrinterEmulator.LogProduced += RelayModuleLog;
            _escPosPrinterEmulator.StateChanged += RaiseStateChanged;
        }

        public EmulatorStateSnapshot GetSnapshot()
        {
            return new EmulatorStateSnapshot
            {
                IsRunning = IsRunning,
                Transport = CurrentProfile.Endpoint.Transport,
                ListenerStatus = _listenerStatus,
                SessionSummary = _sessionSummary,
                ActiveProfileName = _currentProfileName,
                ActiveModuleName = _activeModule?.Name ?? "Not running",
                SessionCount = _sessionCount,
                ReceiptPreview = _escPosPrinterEmulator.ReceiptPreview,
                ReceiptBlocks = _escPosPrinterEmulator.Blocks,
                IsDrawerOpen = _cashDrawerEmulator.IsDrawerOpen
            };
        }

        public async Task StartAsync(EmulatorProfileSettings settings, string profileName)
        {
            await StopAsync();

            if (!settings.Enabled)
                settings.Enabled = true;

            CurrentProfile = settings.Clone();
            _currentProfileName = string.IsNullOrWhiteSpace(profileName) ? "Manual Session" : profileName;
            _listenerStatus = BuildListenerStatus(CurrentProfile.Endpoint);
            _runtimeCancellationTokenSource = new CancellationTokenSource();

            _activeModule = CurrentProfile.DeviceFamily == EmulatorDeviceFamily.CashDrawer
                ? _cashDrawerEmulator
                : _escPosPrinterEmulator;

            _activeModule.Start(CurrentProfile);

            switch (CurrentProfile.Endpoint.Transport)
            {
                case EmulatorTransportType.Serial:
                    _serialListener = new SerialEmulatorListener();
                    await _serialListener.StartAsync(CurrentProfile.Endpoint, HandleIncomingBytesAsync, _runtimeCancellationTokenSource.Token);
                    _sessionCount = 1;
                    _sessionSummary = $"Listening on {CurrentProfile.Endpoint.SerialPortName} @ {CurrentProfile.Endpoint.BaudRate} baud.";
                    break;

                case EmulatorTransportType.Tcp:
                    _tcpListener = new TcpEmulatorListener();
                    _tcpListener.SessionChanged += OnTcpSessionChanged;
                    await _tcpListener.StartAsync(CurrentProfile.Endpoint, HandleIncomingBytesAsync, _runtimeCancellationTokenSource.Token);
                    _sessionCount = 0;
                    _sessionSummary = $"Listening on {CurrentProfile.Endpoint.TcpHost}:{CurrentProfile.Endpoint.TcpPort}.";
                    break;

                case EmulatorTransportType.Http:
                    throw new InvalidOperationException("HTTP listener is configuration-ready but not implemented in phase 1.");
            }

            IsRunning = true;
            RelayHostLog(EmulatorSessionLogKind.Session, $"Emulator host started with profile '{_currentProfileName}'.");
            RaiseStateChanged();
        }

        public async Task StopAsync()
        {
            _runtimeCancellationTokenSource?.Cancel();

            if (_tcpListener != null)
            {
                _tcpListener.SessionChanged -= OnTcpSessionChanged;
                await _tcpListener.StopAsync();
                _tcpListener.Dispose();
                _tcpListener = null;
            }

            if (_serialListener != null)
            {
                await _serialListener.StopAsync();
                _serialListener.Dispose();
                _serialListener = null;
            }

            _runtimeCancellationTokenSource?.Dispose();
            _runtimeCancellationTokenSource = null;

            _activeModule?.Stop();
            _activeModule = null;

            if (IsRunning)
                RelayHostLog(EmulatorSessionLogKind.Session, "Emulator host stopped.");

            IsRunning = false;
            _sessionCount = 0;
            _sessionSummary = "No active sessions.";
            _listenerStatus = "Stopped";
            RaiseStateChanged();
        }

        public async Task<bool> TryAutoStartAsync()
        {
            if (IsRunning)
                return true;

            var settings = _settingsService.Current;
            if (!settings.EmulatorAutoStart || string.IsNullOrWhiteSpace(settings.EmulatorAutoStartProfileId))
                return false;

            var profiles = await _profileManager.GetAllProfilesAsync();
            var profile = profiles.FirstOrDefault(p => p.Id == settings.EmulatorAutoStartProfileId);
            if (profile?.EmulatorSettings == null || !profile.EmulatorSettings.Enabled)
                return false;

            await StartAsync(profile.EmulatorSettings, profile.Name);
            return true;
        }

        public EmulatorProfileSettings BuildProfileFromDefaults()
        {
            var settings = _settingsService.Current;
            return new EmulatorProfileSettings
            {
                Enabled = true,
                DeviceFamily = EmulatorDeviceFamily.ReceiptPrinter,
                Protocol = EmulatorProtocolType.EscPos,
                DrawerLinkMode = DrawerLinkMode.PrinterDriven,
                RenderReceiptPreview = true,
                Endpoint = new EmulatorEndpointConfig
                {
                    Transport = settings.DefaultEmulatorTransport,
                    SerialPortName = settings.DefaultEmulatorSerialPort,
                    BaudRate = settings.DefaultEmulatorBaudRate,
                    TcpHost = settings.DefaultEmulatorTcpHost,
                    TcpPort = settings.DefaultEmulatorTcpPort,
                    HttpPort = settings.DefaultEmulatorHttpPort,
                    HttpRoute = settings.DefaultEmulatorHttpRoute,
                    AutoStart = settings.EmulatorAutoStart,
                    EncodingMode = "RawBytes"
                }
            };
        }

        private async Task HandleIncomingBytesAsync(byte[] bytes, string sessionId, CancellationToken cancellationToken)
        {
            if (_activeModule == null)
                return;

            if (_settingsService.Current.EmulatorLogVerbosity == EmulatorLogVerbosity.ParsedAndRaw)
            {
                RelayLog(new EmulatorSessionLog
                {
                    Kind = EmulatorSessionLogKind.Raw,
                    Transport = CurrentProfile.Endpoint.Transport,
                    SessionId = sessionId,
                    Message = $"{bytes.Length} byte(s) received",
                    DataHex = BitConverter.ToString(bytes).Replace("-", " ", StringComparison.Ordinal)
                });
            }

            await _activeModule.HandleBytesAsync(bytes, cancellationToken);
            RaiseStateChanged();
        }

        private void OnTcpSessionChanged(int count, string summary)
        {
            _sessionCount = count;
            _sessionSummary = summary;
            RaiseStateChanged();
        }

        private void RelayModuleLog(EmulatorSessionLog log)
        {
            if (log.Transport != CurrentProfile.Endpoint.Transport)
                log.Transport = CurrentProfile.Endpoint.Transport;
            RelayLog(log);
        }

        private void RelayHostLog(EmulatorSessionLogKind kind, string message)
        {
            RelayLog(new EmulatorSessionLog
            {
                Kind = kind,
                Transport = CurrentProfile.Endpoint.Transport,
                Message = message,
                SessionId = _currentProfileName
            });
        }

        private void RelayLog(EmulatorSessionLog log)
        {
            LogReceived?.Invoke(log);
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string BuildListenerStatus(EmulatorEndpointConfig config)
        {
            return config.Transport switch
            {
                EmulatorTransportType.Serial => $"Serial listener on {config.SerialPortName} @ {config.BaudRate}",
                EmulatorTransportType.Tcp => string.Format(CultureInfo.InvariantCulture, "TCP listener on {0}:{1}", config.TcpHost, config.TcpPort),
                EmulatorTransportType.Http => string.Format(CultureInfo.InvariantCulture, "HTTP listener on {0}:{1}{2}", config.TcpHost, config.HttpPort, config.HttpRoute),
                _ => "Stopped"
            };
        }
    }
}
