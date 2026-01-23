using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YARG.Menu.Persistent;
using YargArchipelagoCommon;
using static YargArchipelagoCommon.CommonData.Networking;

namespace YargArchipelagoPlugin
{
    public class YargPipeClient
    {
        private Stream stream;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly ArchipelagoService APHandler;

        public event Action<CommonData.DeathLinkData> DeathLinkReceived;
        public event Action<CommonData.ActionItemData> ActionItemReceived;
        public event Action<(string SongHash, string Profile)[]> AvailableSongsReceived;

        public YargPipeClient(ArchipelagoService handler) => APHandler = handler;

        public async Task ConnectAsync()
        {
            if (APHandler.CurrentUserConfig.UsePipe)
                await ConnectAsyncPipe();
            else
                await ConnectAsyncPacket();
        }
        private async Task ConnectAsyncPipe()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    APHandler.Log("Listening for YARG AP pipe server");

                    var pipe = new NamedPipeClientStream(
                        ".",
                        APHandler.CurrentUserConfig.PipeName,
                        PipeDirection.InOut,
                        PipeOptions.Asynchronous);

                    await Task.Run(() => pipe.Connect(10000), cts.Token); // 10s timeout
                    
                    stream = pipe;
                    APHandler.Log("YARG client connected to AP Pipe Server.");

                    await ReceiveLoopAsync();
                    stream = null;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    APHandler.Log("YARG client failed to connect: " + ex.Message);
                    await Task.Delay(1000);
                }
            }
        }

        private async Task ConnectAsyncPacket()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    APHandler.Log("Listening for YARG AP packet server");

                    var client = new System.Net.Sockets.TcpClient();
                    await client.ConnectAsync(IPAddress.Parse(APHandler.CurrentUserConfig.HOST), APHandler.CurrentUserConfig.PORT);

                    stream = client.GetStream();
                    APHandler.Log("YARG client connected to AP Packet Server.");

                    await ReceiveLoopAsync();
                    stream = null;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    APHandler.Log("YARG client failed to connect: " + ex.Message);
                    await Task.Delay(1000);
                }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            if (stream == null) return;

            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true))
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null)
                        {
                            APHandler.Log("AP Client Server disconnected.");
                            break;
                        }
                        APHandler.Log("Received from AP Client: ");
                        ParseClientPacket(line);
                    }
                }
                catch (Exception ex)
                {
                    APHandler.Log("Error in YARG client receive loop: " + ex.Message);
                }
            }
        }

        public async Task SendPacketAsync(YargAPPacket packet)
        {
            if (stream == null) return;

            var json = JsonConvert.SerializeObject(packet, PacketSerializeSettings) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                APHandler.Log("Error sending packet from YARG client: " + ex.Message);
            }
        }

        internal void ParseClientPacket(string line)
        {
            try
            {
                var basePacket = JsonConvert.DeserializeObject<YargAPPacket>(line, PacketSerializeSettings);
                if (basePacket == null) return;

                if (basePacket.Message != null)
                    ToastManager.ToastMessage(basePacket.Message);

                if (basePacket.deathLinkData != null)
                    DeathLinkReceived?.Invoke(basePacket.deathLinkData);

                if (basePacket.ActionItem != null)
                    ActionItemReceived?.Invoke(basePacket.ActionItem);

                if (basePacket.AvailableSongs != null)
                    AvailableSongsReceived?.Invoke(basePacket.AvailableSongs);
            }
            catch (Exception e)
            {
                APHandler.Log($"Failed to parse client packet\n{line}\n{e}");
            }
        }

        public void Stop()
        {
            cts.Cancel();
            try { stream?.Close(); } catch { }
            try { stream?.Dispose(); } catch { }
        }
    }
}
