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
        private CancellationToken _cancellationToken;

        public bool IsRunning => _serialPort?.IsOpen == true;
        public string PortName { get; private set; } = string.Empty;

        public Task StartAsync(EmulatorEndpointConfig config, Func<byte[], string, CancellationToken, Task> onBytesReceived, CancellationToken cancellationToken)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(config.SerialPortName))
                throw new InvalidOperationException("Serial listener requires a COM port.");

            _onBytesReceived = onBytesReceived;
            _cancellationToken = cancellationToken;
            PortName = config.SerialPortName;

            _serialPort = new SerialPort(config.SerialPortName, config.BaudRate)
            {
                ReadBufferSize = 262144,
                WriteBufferSize = 262144,
                ReadTimeout = 250,
                WriteTimeout = 500
            };
            _serialPort.Open();

            _ = Task.Run(() => ReadLoop(), cancellationToken);

            return Task.CompletedTask;
        }

        private void ReadLoop()
        {
            var buffer = new byte[4096];
            try
            {
                while (!_cancellationToken.IsCancellationRequested && _serialPort is { IsOpen: true })
                {
                    int read;
                    try
                    {
                        read = _serialPort.Read(buffer, 0, buffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        break;
                    }

                    if (read <= 0)
                        break;

                    var payload = new byte[read];
                    Array.Copy(buffer, payload, read);
                    _onBytesReceived?.Invoke(payload, $"Serial {PortName}", _cancellationToken).GetAwaiter().GetResult();
                }
            }
            catch
            {
            }
        }

        public Task StopAsync()
        {
            Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private void Stop()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
    }
}
