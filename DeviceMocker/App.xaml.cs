using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DeviceMocker.Core;
using DeviceMocker.Services;

namespace DeviceMocker
{
    public partial class App : Application
    {
        private bool _servicesInitialized;

        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterExceptionHandlers();
            AppLogger.Info("Application startup began.");
            try
            {
                ServiceLocator.Initialize();
                _servicesInitialized = true;
                base.OnStartup(e);
                MainWindow = new MainWindow();
                MainWindow.Show();
                AppLogger.Info($"Application startup completed. Version {AppConstants.AppVersion}.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Application startup failed.", ex);
                MessageBox.Show(
                    $"DeviceMocker could not start.\n\n{ex.Message}\n\nDetails were written to:\n{AppLogger.LogFile}",
                    "DeviceMocker startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.Info("Application exit began.");
            if (_servicesInitialized)
            {
                try
                {
                    ServiceLocator.ShutdownAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Application shutdown failed.", ex);
                }
            }
            base.OnExit(e);
        }

        private void RegisterExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                    AppLogger.Error("An unhandled application exception occurred.", exception);
            };
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLogger.Error("An unobserved background task exception occurred.", args.Exception);
                args.SetObserved();
            };
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error("An unexpected UI error occurred.", e.Exception);
            MessageBox.Show(
                $"DeviceMocker encountered an unexpected error but will remain open.\n\n{e.Exception.Message}",
                "DeviceMocker error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
