using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EchoServer;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using static NetSdrClientApp.Messages.NetSdrMessageHelper;

namespace NetSdrClientApp
{
    public class NetSdrClient
    {
        private readonly ITcpClient _tcpClient;
        private readonly IUdpClient _udpClient;

        public bool IQStarted { get; set; }

        // поле, яке може містити поточний очікуваний TaskCompletionSource
        // доступ до нього робимо атомарно через Interlocked
        private TaskCompletionSource<byte[]>? responseTaskSource;

        // налаштовуваний таймаут відповіді
        private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(2);

        public NetSdrClient(ITcpClient tcpClient, IUdpClient udpClient)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));

            _tcpClient.MessageReceived += _tcpClient_MessageReceived;
            _udpClient.MessageReceived += _udpClient_MessageReceived;
        }

        public async Task ConnectAsync()
        {
            if (!_tcpClient.Connected)
            {
                _tcpClient.Connect();

                var sampleRate = BitConverter.GetBytes((long)100000).Take(5).ToArray();
                var automaticFilterMode = BitConverter.GetBytes((ushort)0).ToArray();
                var adMode = new byte[] { 0x00, 0x03 };

                var msgs = new List<byte[]>
                {
                    NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.IQOutputDataSampleRate, sampleRate),
                    NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.RFFilter, automaticFilterMode),
                    NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ADModes, adMode),
                };

                foreach (var msg in msgs)
                {
                    await SendTcpRequest(msg).ConfigureAwait(false);
                }
            }
        }

        // Залишив ім'я Disconect, як у вашому коді / тестах
        public void Disconect()
        {
            _tcpClient.Disconnect();
        }

        public async Task StartIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var iqDataMode = (byte)0x80;
            var start = (byte)0x02;
            var fifo16bitCaptureMode = (byte)0x01;
            var n = (byte)1;

            var args = new[] { iqDataMode, start, fifo16bitCaptureMode, n };
            var msg = NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ReceiverState, args);

            await SendTcpRequest(msg).ConfigureAwait(false);

            IQStarted = true;

            // Запускаємо UDP listener у фоновому таску (ненав'язливо)
            _ = _udpClient.StartListeningAsync();
        }

        public async Task StopIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var stop = (byte)0x01;
            var args = new byte[] { 0, stop, 0, 0 };
            var msg = NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ReceiverState, args);

            await SendTcpRequest(msg).ConfigureAwait(false);

            IQStarted = false;

            _udpClient.StopListening();
        }

        public async Task ChangeFrequencyAsync(long hz, int channel)
        {
            var channelArg = (byte)channel;
            var frequencyArg = BitConverter.GetBytes(hz).Take(5);
            var args = new[] { channelArg }.Concat(frequencyArg).ToArray();

            var msg = NetSdrMessageHelper.GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ReceiverFrequency, args);

            await SendTcpRequest(msg).ConfigureAwait(false);
        }

        // Обробник UDP-повідомлень (залишив як було, але без Aggregate)
        private void _udpClient_MessageReceived(object? sender, byte[] e)
        {
            try
            {
                NetSdrMessageHelper.TranslateMessage(e, out _, out _, out _, out byte[] body);

                var samples = NetSdrMessageHelper.GetSamples(16, body);

                var hex = body != null && body.Length > 0
                    ? string.Join(" ", body.Select(b => b.ToString("x2")))
                    : string.Empty;

                Console.WriteLine("Samples received: " + hex);

                // записуємо як 16-bit signed (як у вас було)
                using var fs = new FileStream("samples.bin", FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new BinaryWriter(fs);
                foreach (var sample in samples)
                {
                    sw.Write((short)sample);
                }
            }
            catch (Exception ex)
            {
                // Логування помилки для діагностики, не кидаємо далі
                Console.WriteLine("Error in UDP message handling: " + ex.Message);
            }
        }

        /// <summary>
        /// Надсилає TCP-повідомлення і чекає відповіді (потокобезпечно).
        /// Використовує атомарну установку responseTaskSource, щоб уникнути гонок.
        /// </summary>
        private async Task<byte[]> SendTcpRequest(byte[] msg, TimeSpan? timeout = null)
        {
            if (!_tcpClient.Connected)
                throw new InvalidOperationException("TCP connection is not established.");

            timeout ??= DefaultResponseTimeout;

            // створюємо локальний TCS
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Спробуємо атомарно встановити поле responseTaskSource до нашого tcs,
            // тільки якщо воно зараз null. Якщо в полі вже є інший pending запит —
            // кидаємо InvalidOperationException (логіка може бути змінена під ваші тести).
            var prev = System.Threading.Interlocked.CompareExchange(ref responseTaskSource, tcs, null);
            if (prev != null)
            {
                throw new InvalidOperationException("Another request is already pending.");
            }

            try
            {
                // Надсилаємо повідомлення
                await _tcpClient.SendMessageAsync(msg).ConfigureAwait(false);

                // Очікуємо або відповіді, або таймауту
                using var cts = new CancellationTokenSource(timeout.Value);
                try
                {
                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
                    if (completed == tcs.Task)
                    {
                        // Повертаємо результат (якщо Task завершився з виключенням - воно проброситься)
                        return await tcs.Task.ConfigureAwait(false);
                    }
                    else
                    {
                        throw new TimeoutException("Timeout waiting for TCP response.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Таймаут через CancellationTokenSource — переводимо в TimeoutException
                    throw new TimeoutException("Timeout waiting for TCP response.");
                }
            }
            finally
            {
                // Гарантовано очищаємо поле, але лише якщо воно все ще вказує на наш tcs
                System.Threading.Interlocked.CompareExchange(ref responseTaskSource, null, tcs);
            }
        }

        /// <summary>
        /// Обробник надходження TCP-повідомлень.
        /// Атомарно забираємо поточний responseTaskSource (якщо є) і SetResult.
        /// Решта повідомлень можна обробляти як unsolicited messages.
        /// </summary>
        private void _tcpClient_MessageReceived(object? sender, byte[] e)
        {
            try
            {
                // Атомарно отримати і обнулити
                var tcs = System.Threading.Interlocked.Exchange(ref responseTaskSource, null);
                if (tcs != null)
                {
                    // Безпечний SetResult
                    try
                    {
                        tcs.SetResult(e);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to set response result: " + ex.Message);
                    }
                }

                var hex = e != null && e.Length > 0
                    ? string.Join(" ", e.Select(b => b.ToString("x2")))
                    : string.Empty;

                Console.WriteLine("Response received: " + hex);

                // Тут можна додати обробку unsolicited повідомлень
            }
            catch (Exception ex)
            {
                // Логування, але не кидаємо далі
                Console.WriteLine("Error in TCP message handler: " + ex.Message);
            }
        }
    }
}
