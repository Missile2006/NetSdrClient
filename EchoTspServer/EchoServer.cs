using EchoServer.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer;

public class EchoServer
{
    private readonly ITcpListener _listener;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public EchoServer(ITcpListener listener, ILogger logger)
    {
        _listener = listener;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync()
    {
        _listener.Start();
        _logger.Log("Server started.");

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                ITcpClient client = await _listener.AcceptTcpClientAsync();
                _logger.Log("Client connected.");

                _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                // Listener has been closed or operation was cancelled
                break;
            }
        }
        _logger.Log("Server shutdown.");
    }

    public async Task HandleClientAsync(ITcpClient client, CancellationToken token)
    {
        using (client)
        using (INetworkStream stream = client.GetStream())
        {
            try
            {
                byte[] buffer = new byte[8192];
                int bytesRead;

                while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead, token);
                    _logger.Log($"Echoed {bytesRead} bytes to the client.");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.Log($"Error: {ex.Message}");
            }
            finally
            {
                client.Close();
                _logger.Log("Client disconnected.");
            }
        }
    }

    public void Stop()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
        _listener.Stop();
        _cancellationTokenSource.Dispose();
        _logger.Log("Server stopped.");
    }
}