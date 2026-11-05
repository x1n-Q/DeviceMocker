using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using DeviceMocker.Models;

namespace DeviceMocker.Services
{
    public sealed class SerialEmulatorListener : IDisposable
    {
        private SerialPort? _serialPort;
        private Func<byte[], string, CancellationToken, Task>? _onBytesReceived;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _readLoopTask;

        public bool IsRunning => _serialPort?.IsOpen == true;
        public string PortName { get; private set; } = string.Empty;

        public async Task StartAsync(EmulatorEndpointConfig config, Func<byte[], string, CancellationToken, Task> onBytesReceived, CancellationToken cancellationToken)
        {
            await StopAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(config.SerialPortName))
                throw new InvalidOperationException("Serial listener requires a COM port.");

            _onBytesReceived = onBytesReceived;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            PortName = config.SerialPortName;

            _serialPort = new SerialPort(config.SerialPortName, config.BaudRate)
            {
                ReadBufferSize = 262144,
                WriteBufferSize = 262144,
                ReadTimeout = 250,
                WriteTimeout = 500
            };
            try
            {
                _serialPort.Open();
                _readLoopTask = Task.Run(() => ReadLoopAsync(_cancellationTokenSource.Token));
            }
            catch
            {
                await StopAsync().ConfigureAwait(false);
                throw;
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            try
            {
                while (!cancellationToken.IsCancellationRequested && _serialPort is { IsOpen: true } port)
                {
                    int read;
                    try
                    {
                        read = port.Read(buffer, 0, buffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            AppLogger.Error($"Serial listener on '{PortName}' stopped unexpectedly.", ex);
                        break;
                    }

                    if (read <= 0)
                        break;

                    var payload = new byte[read];
                    Array.Copy(buffer, payload, read);
                    if (_onBytesReceived != null)
                        await _onBytesReceived(payload, $"Serial {PortName}", cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Serial listener on '{PortName}' failed.", ex);
            }
        }

        public async Task StopAsync()
        {
            var cancellationTokenSource = _cancellationTokenSource;
            var serialPort = _serialPort;
            var readLoopTask = _readLoopTask;

            _cancellationTokenSource = null;
            _serialPort = null;
            _readLoopTask = null;

            cancellationTokenSource?.Cancel();
            try
            {
                if (serialPort?.IsOpen == true)
                    serialPort.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Unable to close serial listener on '{PortName}'.", ex);
            }

            if (readLoopTask != null)
            {
                try
                {
                    await readLoopTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    AppLogger.Info($"Timed out while stopping serial listener on '{PortName}'.");
                }
            }

            serialPort?.Dispose();
            cancellationTokenSource?.Dispose();
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}
