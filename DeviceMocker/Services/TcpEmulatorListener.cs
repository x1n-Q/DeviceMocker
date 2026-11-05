using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DeviceMocker.Models;

namespace DeviceMocker.Services
{
    public sealed class TcpEmulatorListener : IDisposable
    {
        private readonly ConcurrentDictionary<string, string> _activeSessions = new();
        private readonly ConcurrentDictionary<string, TcpClient> _activeClients = new();
        private readonly ConcurrentDictionary<string, Task> _clientTasks = new();
        private Func<byte[], string, CancellationToken, Task>? _onBytesReceived;
        private CancellationTokenSource? _cancellationTokenSource;
        private System.Net.Sockets.TcpListener? _listener;
        private Task? _acceptLoopTask;

        public event Action<int, string>? SessionChanged;

        public bool IsRunning => _listener != null;

        public async Task StartAsync(EmulatorEndpointConfig config, Func<byte[], string, CancellationToken, Task> onBytesReceived, CancellationToken cancellationToken)
        {
            await StopAsync().ConfigureAwait(false);

            if (config.TcpPort <= 0)
                throw new InvalidOperationException("TCP listener requires a valid port.");

            _onBytesReceived = onBytesReceived;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var address = ResolveAddress(config.TcpHost);
            _listener = new System.Net.Sockets.TcpListener(address, config.TcpPort);
            try
            {
                _listener.Start();
                _acceptLoopTask = AcceptLoopAsync(_cancellationTokenSource.Token);
            }
            catch
            {
                await StopAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task StopAsync()
        {
            var cancellationTokenSource = _cancellationTokenSource;
            var listener = _listener;
            var acceptLoopTask = _acceptLoopTask;

            _cancellationTokenSource = null;
            _listener = null;
            _acceptLoopTask = null;

            cancellationTokenSource?.Cancel();
            listener?.Stop();
            foreach (var client in _activeClients.Values)
                client.Dispose();

            var tasks = _clientTasks.Values.ToArray();
            if (acceptLoopTask != null)
                tasks = tasks.Append(acceptLoopTask).ToArray();

            if (tasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    AppLogger.Info("Timed out while stopping TCP listener sessions.");
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                }
            }

            _activeClients.Clear();
            _activeSessions.Clear();
            _clientTasks.Clear();
            cancellationTokenSource?.Dispose();
            SessionChanged?.Invoke(0, "No active TCP sessions.");
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    var sessionId = Guid.NewGuid().ToString("N")[..8];
                    var task = HandleClientAsync(sessionId, client, cancellationToken);
                    _clientTasks[sessionId] = task;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    AppLogger.Error("TCP accept loop stopped unexpectedly.", ex);
            }
        }

        private async Task HandleClientAsync(string sessionId, TcpClient client, CancellationToken cancellationToken)
        {
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "tcp-client";

            _activeSessions[sessionId] = endpoint;
            _activeClients[sessionId] = client;
            SessionChanged?.Invoke(_activeSessions.Count, $"{_activeSessions.Count} active TCP session(s). Last: {endpoint}");

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        if (read <= 0)
                            break;

                        if (_onBytesReceived != null)
                        {
                            var payload = new byte[read];
                            Array.Copy(buffer, payload, read);
                            await _onBytesReceived(payload, endpoint, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    AppLogger.Error($"TCP session '{endpoint}' stopped unexpectedly.", ex);
            }
            finally
            {
                _activeClients.TryRemove(sessionId, out _);
                _activeSessions.TryRemove(sessionId, out _);
                _clientTasks.TryRemove(sessionId, out _);
                var summary = _activeSessions.IsEmpty
                    ? "No active TCP sessions."
                    : $"{_activeSessions.Count} active TCP session(s).";
                SessionChanged?.Invoke(_activeSessions.Count, summary);
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        private static IPAddress ResolveAddress(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || host == "*" || host == "0.0.0.0")
                return IPAddress.Any;

            return IPAddress.TryParse(host, out var address) ? address : IPAddress.Loopback;
        }
    }
}
