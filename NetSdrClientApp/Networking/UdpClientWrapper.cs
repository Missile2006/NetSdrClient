using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;

[ExcludeFromCodeCoverage]
public class UdpClientWrapper : IUdpClient, IDisposable
{
    private readonly IPEndPoint _localEndPoint;
    private CancellationTokenSource? _cts;
    private UdpClient? _udpClient;

    public event EventHandler<byte[]>? MessageReceived;

    public UdpClientWrapper(int port)
    {
        _localEndPoint = new IPEndPoint(IPAddress.Any, port);
    }

    public async Task StartListeningAsync()
    {
        // Якщо вже слухаємо — не запускаємо повторно
        if (_cts != null)
        {
            Console.WriteLine("Already listening.");
            return;
        }

        _cts = new CancellationTokenSource();

        Console.WriteLine("Start listening for UDP messages...");
        try
        {
            _udpClient = new UdpClient(_localEndPoint);

            while (!_cts.Token.IsCancellationRequested)
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync(_cts.Token);
                MessageReceived?.Invoke(this, result.Buffer);
                Console.WriteLine($"Received from {result.RemoteEndPoint}");
            }
        }
        catch (OperationCanceledException)
        {
            // нормальна ситуація при StopListening
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving message: {ex.Message}");
        }
        finally
        {
            Cleanup();
        }
    }

    public void StopListening()
    {
        StopInternal();
    }

    public void Exit()
    {
        StopInternal();
    }

    private void StopInternal()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        Cleanup();

        Console.WriteLine("Stopped listening for UDP messages.");
    }

    /// <summary>
    /// Коректно Dispose-ить ресурси.
    /// </summary>
    private void Cleanup()
    {
        try
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        catch { }

        _udpClient = null;

        try
        {
            _cts?.Dispose();
        }
        catch { }

        _cts = null;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_localEndPoint.Address.ToString(), _localEndPoint.Port);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not UdpClientWrapper other) return false;

        return _localEndPoint.Address.Equals(other._localEndPoint.Address)
               && _localEndPoint.Port == other._localEndPoint.Port;
    }

    public void Dispose()
    {
        StopInternal();
        GC.SuppressFinalize(this);
    }
}
