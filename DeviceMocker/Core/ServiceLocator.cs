using DeviceMocker.Devices.CashDrawer;
using DeviceMocker.Devices.MagstripeReader;
using DeviceMocker.Devices.PosPanel;
using DeviceMocker.Devices.RfidReader;
using DeviceMocker.Devices.Scale;
using DeviceMocker.Devices.Scanner;
using DeviceMocker.Devices.SequenceBuilder;
using DeviceMocker.Devices.SerialDevice;
using DeviceMocker.Devices.VirtualKeyboard;
using DeviceMocker.Interfaces;
using DeviceMocker.Services;

namespace DeviceMocker.Core
{
    public static class ServiceLocator
    {
        // Services
        public static ILoggerService Logger { get; private set; } = null!;
        public static IStorageService Storage { get; private set; } = null!;
        public static SettingsService Settings { get; private set; } = null!;
        public static ProfileManager ProfileManager { get; private set; } = null!;
        public static CashDrawerEmulator CashDrawerEmulator { get; private set; } = null!;
        public static EmulatorHostService EmulatorHost { get; private set; } = null!;

        // Output Channels
        public static KeyboardOutputService KeyboardOutput { get; private set; } = null!;
        public static SerialOutputService SerialOutput { get; private set; } = null!;
        public static TcpOutputService TcpOutput { get; private set; } = null!;
        public static UdpOutputService UdpOutput { get; private set; } = null!;
        public static HttpOutputService HttpOutput { get; private set; } = null!;
        public static OutputChannelManager ChannelManager { get; private set; } = null!;
        public static InputRouter Router { get; private set; } = null!;

        // Devices
        public static DeviceManager DeviceManager { get; private set; } = null!;
        public static ScannerDevice ScannerDevice { get; private set; } = null!;
        public static VirtualKeyboardDevice VirtualKeyboardDevice { get; private set; } = null!;
        public static PosPanelDevice PosPanelDevice { get; private set; } = null!;
        public static SerialDeviceSimulator SerialDeviceSimulator { get; private set; } = null!;
        public static ScaleDevice ScaleDevice { get; private set; } = null!;
        public static RfidReaderDevice RfidReaderDevice { get; private set; } = null!;
        public static MagstripeReaderDevice MagstripeReaderDevice { get; private set; } = null!;
        public static SequenceBuilderDevice SequenceBuilderDevice { get; private set; } = null!;
        public static CashDrawerDevice CashDrawerDevice { get; private set; } = null!;

        public static void Initialize()
        {
            AppConstants.EnsureDirectories();

            // Core services
            Logger = new LoggerService();
            Storage = new JsonStorageService();
            Settings = new SettingsService(Storage);
            ProfileManager = new ProfileManager(Storage, AppConstants.ProfilesFolder);
            Settings.LoadAsync().GetAwaiter().GetResult();
            CashDrawerEmulator = new CashDrawerEmulator();
            EmulatorHost = new EmulatorHostService(Settings, ProfileManager, CashDrawerEmulator);

            // Output channels
            KeyboardOutput = new KeyboardOutputService();
            SerialOutput = new SerialOutputService();
            TcpOutput = new TcpOutputService();
            UdpOutput = new UdpOutputService();
            HttpOutput = new HttpOutputService();

            ChannelManager = new OutputChannelManager();
            ChannelManager.Register(KeyboardOutput);
            ChannelManager.Register(SerialOutput);
            ChannelManager.Register(TcpOutput);
            ChannelManager.Register(UdpOutput);
            ChannelManager.Register(HttpOutput);

            // Router
            Router = new InputRouter(ChannelManager, Logger);

            // Device modules
            ScannerDevice = new ScannerDevice(Router);
            VirtualKeyboardDevice = new VirtualKeyboardDevice(Router);
            PosPanelDevice = new PosPanelDevice(Router);
            SerialDeviceSimulator = new SerialDeviceSimulator(Router);
            ScaleDevice = new ScaleDevice(Router);
            RfidReaderDevice = new RfidReaderDevice(Router);
            MagstripeReaderDevice = new MagstripeReaderDevice(Router);
            SequenceBuilderDevice = new SequenceBuilderDevice(Router);
            CashDrawerDevice = new CashDrawerDevice(Router);

            DeviceManager = new DeviceManager();
            DeviceManager.Register(ScannerDevice);
            DeviceManager.Register(VirtualKeyboardDevice);
            DeviceManager.Register(PosPanelDevice);
            DeviceManager.Register(SerialDeviceSimulator);
            DeviceManager.Register(ScaleDevice);
            DeviceManager.Register(RfidReaderDevice);
            DeviceManager.Register(MagstripeReaderDevice);
            DeviceManager.Register(SequenceBuilderDevice);
            DeviceManager.Register(CashDrawerDevice);
        }

        public static async Task ShutdownAsync()
        {
            await EmulatorHost.StopAsync().ConfigureAwait(false);
            SerialOutput.Dispose();
        }
    }
}
