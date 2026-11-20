using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using TRIO2026.Core.Interfaces;

namespace TRIO2026.App.Services;

/// <summary>
/// UV 硬體服務的模擬實作（開發/測試用）
/// 
/// 實作 TCP Client 與 Tools/TRIOV2.Simulator (TCP Server: 127.0.0.1:5020) 連線，
/// 進行雙向的狀態同步與事件觸發。
/// 若無法連線，則退回單純的 Log 模擬模式，且連線建立後自動補發狀態握手（Handshake）。
/// 
/// 製作者: Office of William
/// </summary>
public class MockUvHardwareService : IUvHardwareService, IDisposable
{
    public event EventHandler? DoorOpened;
    public event EventHandler? DoorClosed;

    public bool IsDoorOpen { get; private set; }
    public bool IsUvRunning { get; private set; }
    public bool IsUvPaused { get; private set; }

    private int _remainingSeconds;
    private int _totalDurationSeconds;
    private DateTime _lastTickUtc = DateTime.UtcNow;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public MockUvHardwareService()
    {
        _cts = new CancellationTokenSource();
        // 在背景嘗試連接 Simulator
        _ = Task.Run(ConnectToSimulatorAsync);
    }

    private async Task ConnectToSimulatorAsync()
    {
        while (!_cts!.IsCancellationRequested)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("127.0.0.1", 5020);
                _stream = _client.GetStream();
                Debug.WriteLine("[MockUV] 已連線至 Simulator (127.0.0.1:5020)");

                // ── 連線建立成功：執行雙向握手狀態同步 ──
                await SynchronizeWithSimulatorAsync();

                // 開始接收迴圈
                using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
                while (_client.Connected && !_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    await ProcessSimulatorMessageAsync(line);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MockUV] 連線中斷或失敗: {ex.Message}");
            }
            finally
            {
                _stream?.Dispose();
                _client?.Close();
                _stream = null;
                _client = null;
            }

            if (!_cts.IsCancellationRequested)
            {
                await Task.Delay(3000);
            }
        }
    }

    /// <summary>連線剛建立時，主動向 Simulator 同步狀態</summary>
    private async Task SynchronizeWithSimulatorAsync()
    {
        try
        {
            // 1. 若 UV 正在照射中，補送當前剩餘秒數與暫停狀態
            if (IsUvRunning && _remainingSeconds > 0)
            {
                Debug.WriteLine($"[MockUV] 連線建立，向 Simulator 補發當前 UV 照射狀態: 剩餘 {_remainingSeconds}s (Paused={IsUvPaused})");
                await SendCommandAsync("StartUV", $"\"Duration\": {_remainingSeconds}, \"IsPaused\": {(IsUvPaused ? "true" : "false")}");
            }
            else
            {
                await SendCommandAsync("StopUV");
            }

            // 2. 請求 Simulator 回報當前門板狀態
            await SendCommandAsync("QueryDoor");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MockUV] 狀態同步失敗: {ex.Message}");
        }
    }

    private async Task ProcessSimulatorMessageAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Event", out var eventProp))
            {
                var evt = eventProp.GetString();
                if (evt == "DoorOpened")
                {
                    SimulateDoorOpen();
                }
                else if (evt == "DoorClosed")
                {
                    SimulateDoorClose();
                }
                else if (evt == "SyncDoor")
                {
                    if (doc.RootElement.TryGetProperty("IsDoorOpen", out var openProp))
                    {
                        var open = openProp.GetBoolean();
                        if (open != IsDoorOpen)
                        {
                            if (open) SimulateDoorOpen();
                            else SimulateDoorClose();
                        }
                    }
                }
                else if (evt == "QueryStatus")
                {
                    // Simulator 詢問當前狀態，立即回傳
                    if (IsUvRunning && _remainingSeconds > 0)
                    {
                        await SendCommandAsync("StartUV", $"\"Duration\": {_remainingSeconds}, \"IsPaused\": {(IsUvPaused ? "true" : "false")}");
                    }
                    else
                    {
                        await SendCommandAsync("StopUV");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MockUV] 解析 Simulator 訊息失敗: {ex.Message}");
        }
    }

    private async Task SendCommandAsync(string command, string? extraJson = null)
    {
        if (_client == null || !_client.Connected || _stream == null)
        {
            Debug.WriteLine($"[MockUV] Simulator 未連線，保持內部狀態。命令: {command}");
            return;
        }

        await _sendLock.WaitAsync();
        try
        {
            string json;
            if (extraJson != null)
                json = $"{{\"Command\": \"{command}\", {extraJson}}}\n";
            else
                json = $"{{\"Command\": \"{command}\"}}\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MockUV] 發送命令失敗: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<bool> StartUvLampAsync(int durationSeconds)
    {
        Debug.WriteLine($"[MockUV] 請求啟動 UV 燈 (Duration={durationSeconds}s)");
        IsUvRunning = true;
        IsUvPaused = false;
        _totalDurationSeconds = durationSeconds;
        _remainingSeconds = durationSeconds;
        _lastTickUtc = DateTime.UtcNow;

        await SendCommandAsync("StartUV", $"\"Duration\": {durationSeconds}, \"IsPaused\": false");
        return true;
    }

    public async Task<bool> StopUvLampAsync()
    {
        Debug.WriteLine("[MockUV] 請求停止 UV 燈");
        IsUvRunning = false;
        IsUvPaused = false;
        _remainingSeconds = 0;

        await SendCommandAsync("StopUV");
        return true;
    }

    /// <summary>更新當前倒數剩餘秒數（由 ViewModel 每秒定時回報，確保同步準確性）</summary>
    public void UpdateRemainingSeconds(int seconds)
    {
        _remainingSeconds = seconds;
        _lastTickUtc = DateTime.UtcNow;
        if (seconds <= 0)
        {
            IsUvRunning = false;
            IsUvPaused = false;
        }
    }

    /// <summary>模擬門板開啟（也可由 UI/開發直接呼叫）</summary>
    public void SimulateDoorOpen()
    {
        Debug.WriteLine("[MockUV] 模擬門板開啟");
        IsDoorOpen = true;
        if (IsUvRunning)
        {
            IsUvPaused = true;
        }

        try
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                DoorOpened?.Invoke(this, EventArgs.Empty);
            });
        }
        catch { }
    }

    /// <summary>模擬門板關閉（也可由 UI/開發直接呼叫）</summary>
    public void SimulateDoorClose()
    {
        Debug.WriteLine("[MockUV] 模擬門板關閉");
        IsDoorOpen = false;
        if (IsUvRunning)
        {
            IsUvPaused = false;
            _lastTickUtc = DateTime.UtcNow;
        }

        try
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                DoorClosed?.Invoke(this, EventArgs.Empty);
            });
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Close();
        _client?.Dispose();
        _sendLock.Dispose();
    }
}
