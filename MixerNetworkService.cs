// MixerNetworkService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class MixerNetworkService
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _client?.Connected ?? false;

    public event Action? OnPacketReceived;
    public event Action<byte[]>? OnMeterDataReceived;
    public event Action<int, byte>? OnGainReceived;
    public event Action<int, bool>? OnMuteStatusReceived;
    public event Action<int>? OnPresetReceived;

    public async Task<bool> ConnectAsync(string ipAddress, int port)
    {
        if (IsConnected) return true;
        try
        {
            _client = new TcpClient { NoDelay = true };
            await _client.ConnectAsync(ipAddress, port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenForData(_cts.Token), _cts.Token);
            return true;
        }
        catch (Exception) { return false; }
    }

    public void Disconnect()
    {
        if (!IsConnected) return;
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
    }

    private async Task ListenForData(CancellationToken token)
    {
        var buffer = new List<byte>();
        var readBuffer = new byte[2048];
        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                int bytesRead = await _stream!.ReadAsync(readBuffer, 0, readBuffer.Length, token);
                if (bytesRead == 0) { Disconnect(); break; }

                buffer.AddRange(readBuffer.Take(bytesRead));

                while (true)
                {
                    int startIndex = -1;
                    for (int i = 0; i < buffer.Count - 1; i++)
                    {
                        if (buffer[i] == 0x7B && buffer[i + 1] == 0x7D) { startIndex = i; break; }
                    }

                    if (startIndex == -1) break;
                    if (startIndex > 0) buffer.RemoveRange(0, startIndex);
                    if (buffer.Count < 6) break;

                    byte dataLen = buffer[2];
                    int packetLen = 6 + dataLen;

                    if (buffer.Count < packetLen) break;
                    
                    if (buffer[packetLen - 2] == 0x7D && buffer[packetLen - 1] == 0x7B)
                    {
                        OnPacketReceived?.Invoke();
                        byte cmd = buffer[3];
                        byte[] data = buffer.GetRange(4, dataLen).ToArray();
                        ProcessPacket(cmd, data);
                        buffer.RemoveRange(0, packetLen);
                    }
                    else
                    {
                        buffer.RemoveRange(0, 1);
                    }
                }
            }
            catch (Exception) { Disconnect(); break; }
        }
    }

    private void ProcessPacket(byte cmd, byte[] data)
    {
        switch (cmd)
        {
            case 0x21: if (data.Length == 2) OnGainReceived?.Invoke(data[0], data[1]); break;
            case 0x22: if (data.Length == 2) OnMuteStatusReceived?.Invoke(data[0], data[1] == 1); break;
            case 0x23: 
                if (data.Length == 1) OnPresetReceived?.Invoke(data[0]);
                else if (data.Length == 40) OnMeterDataReceived?.Invoke(data);
                break;
        }
    }

    private async Task SendCommandAsync(byte cmd, byte[] data)
    {
        if (!IsConnected) return;
        try
        {
            byte[] packet = new byte[6 + data.Length];
            packet[0] = 0x7B; packet[1] = 0x7D; packet[2] = (byte)data.Length;
            packet[3] = cmd; Array.Copy(data, 0, packet, 4, data.Length);
            packet[4 + data.Length] = 0x7D; packet[5 + data.Length] = 0x7B;
            await _stream!.WriteAsync(packet, 0, packet.Length);
        }
        catch (Exception) { Disconnect(); }
    }
    
    public Task SetGainAsync(int channelId, byte gain) => SendCommandAsync(0x11, new byte[] { (byte)channelId, gain });
    public Task SetMuteAsync(int channelId, bool isMuted) => SendCommandAsync(0x12, new byte[] { (byte)channelId, (byte)(isMuted ? 1 : 0) });
    public Task CallPresetAsync(int presetId) => SendCommandAsync(0x13, new byte[] { (byte)presetId });
    public Task RequestGainAsync(int channelId) => SendCommandAsync(0x14, new byte[] { (byte)channelId });
    public Task RequestMuteAsync(int channelId) => SendCommandAsync(0x15, new byte[] { (byte)channelId });
    public Task RequestCurrentPresetAsync() => SendCommandAsync(0x16, new byte[0]);
    public Task RequestMeterDataAsync() => SendCommandAsync(0x17, new byte[0]);
}