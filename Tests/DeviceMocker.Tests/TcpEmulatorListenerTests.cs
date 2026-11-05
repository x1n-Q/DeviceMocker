using System.Net;
using System.Net.Sockets;
using DeviceMocker.Models;
using DeviceMocker.Services;

namespace DeviceMocker.Tests;

public sealed class TcpEmulatorListenerTests
{
    [Fact]
    public async Task Listener_ReceivesPayloadAndStopsCleanly()
    {
        var port = ReservePort();
        using var listener = new TcpEmulatorListener();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = new EmulatorEndpointConfig
        {
            Transport = EmulatorTransportType.Tcp,
            TcpHost = "127.0.0.1",
            TcpPort = port
        };

        await listener.StartAsync(
            config,
            (bytes, _, _) =>
            {
                received.TrySetResult(bytes);
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationTokenSource.Token);
        var payload = new byte[] { 0x1B, 0x40, 0x41, 0x0A };
        await client.GetStream().WriteAsync(payload, cancellationTokenSource.Token);

        Assert.Equal(payload, await received.Task.WaitAsync(cancellationTokenSource.Token));
        await listener.StopAsync();
        Assert.False(listener.IsRunning);
    }

    private static int ReservePort()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        return port;
    }
}
