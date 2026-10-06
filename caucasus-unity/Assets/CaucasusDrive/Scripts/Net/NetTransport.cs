using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CaucasusDrive
{
    /// <summary>Канал клиента до сервера комнаты: UDP (хост в телефоне или свой сервер) или WebSocket (Cloudflare Worker).</summary>
    public interface ITransport
    {
        void Send(byte[] data);
        void Keepalive();                       // UDP: пакет PING; WebSocket: текст «p» (Cloudflare отвечает сам, не будя комнату)
        bool Poll(out byte[] data);             // пустой массив — «жив» (ответ на keepalive)
        string Error { get; }                   // null, пока всё в порядке
        bool Open { get; }
        void Close();
    }

    public class UdpTransport : ITransport
    {
        readonly UdpClient udp = new UdpClient(0);
        readonly IPEndPoint server;
        public UdpTransport(IPEndPoint server) { this.server = server; }
        public string Error => null;
        public bool Open => true;
        public void Send(byte[] d) { try { udp.Send(d, d.Length, server); } catch (Exception) { } }
        public void Keepalive() { Send(NetP.Pack(NetP.Ping)); }
        public bool Poll(out byte[] data)
        {
            data = null;
            while (true)
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                try { if (udp.Available <= 0) return false; data = udp.Receive(ref ep); }
                catch (SocketException) { continue; }
                catch (Exception) { return false; }
                if (ep.Address.Equals(server.Address) && ep.Port == server.Port) return true;
            }
        }
        public void Close() { try { udp.Close(); } catch (Exception) { } }
    }

    /// <summary>WebSocket: каждое бинарное сообщение — один пакет нашего протокола. Приём идёт в фоне, главный поток только забирает очередь.</summary>
    public class WsTransport : ITransport
    {
        readonly ClientWebSocket ws = new ClientWebSocket();
        readonly ConcurrentQueue<byte[]> rx = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        volatile string error;
        volatile bool open;
        public string Error => error;
        public bool Open => open;

        public WsTransport(string url)
        {
            Task.Run(async () =>
            {
                try
                {
                    await ws.ConnectAsync(new Uri(url), cts.Token).ConfigureAwait(false);
                    open = true;
                    await ReceiveLoop().ConfigureAwait(false);
                }
                catch (Exception e) { if (!cts.IsCancellationRequested) error = "Нет связи с сервером: " + Short(e); }
            });
        }

        static string Short(Exception e) { var m = e.InnerException != null ? e.InnerException.Message : e.Message; return m.Length > 70 ? m.Substring(0, 70) : m; }

        async Task ReceiveLoop()
        {
            var buf = new byte[8192];
            var acc = new System.IO.MemoryStream();
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                acc.SetLength(0);
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close) { open = false; if (error == null) error = "Сервер закрыл соединение"; return; }
                    acc.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);
                if (r.MessageType == WebSocketMessageType.Text) rx.Enqueue(new byte[0]);       // «p»
                else if (acc.Length > 0 && rx.Count < 512) rx.Enqueue(acc.ToArray());
            }
            open = false;
            if (error == null && !cts.IsCancellationRequested) error = "Связь потеряна";
        }

        async Task SendAsync(byte[] d, WebSocketMessageType type)
        {
            await sendLock.WaitAsync().ConfigureAwait(false);
            try { if (ws.State == WebSocketState.Open) await ws.SendAsync(new ArraySegment<byte>(d), type, true, cts.Token).ConfigureAwait(false); }
            catch (Exception e) { if (!cts.IsCancellationRequested) error = "Ошибка отправки: " + Short(e); }
            finally { sendLock.Release(); }
        }

        public void Send(byte[] d) { if (open) { var _ = SendAsync(d, WebSocketMessageType.Binary); } }
        public void Keepalive() { if (open) { var _ = SendAsync(new byte[] { (byte)'p' }, WebSocketMessageType.Text); } }
        public bool Poll(out byte[] data) { return rx.TryDequeue(out data); }
        public void Close()
        {
            try { cts.Cancel(); if (ws.State == WebSocketState.Open) ws.Abort(); ws.Dispose(); } catch (Exception) { }
            open = false;
        }
    }
}
