// MainViewModel.cs (FINAL WITH DUAL POLLING TIMERS)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MixerNetworkService _mixerService;
    private readonly Dictionary<int, MixerChannel> _allChannels = new Dictionary<int, MixerChannel>();
    private bool _isUpdatingFromMixer = false;
    private readonly DispatcherTimer _indicatorTimer;
    
    // ================= DUA TIMER POLLING TERPISAH =================
    private readonly DispatcherTimer _fastPollingTimer; // Untuk Fader & Meter
    private readonly DispatcherTimer _slowPollingTimer; // Untuk Mute
    // ============================================================
    
    private string _mixerIpAddress = "172.16.24.3";
    private string _connectionStatus = "Disconnected";
    private bool _isConnected;
    private Preset? _selectedPreset;
    private Brush _dataReceivedIndicatorBrush = Brushes.Gray;

    public ObservableCollection<MixerChannel> DisplayChannels { get; } = new ObservableCollection<MixerChannel>();
    public MixerChannel LrChannel { get; private set; }
    public ObservableCollection<Preset> Presets { get; } = new ObservableCollection<Preset>();

    public string MixerIpAddress { get => _mixerIpAddress; set { _mixerIpAddress = value; OnPropertyChanged(); } }
    public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; OnPropertyChanged(); } }
    public bool IsConnected { get => _isConnected; set { _isConnected = value; OnPropertyChanged(); } }
    public Preset? SelectedPreset { get => _selectedPreset; set { _selectedPreset = value; OnPropertyChanged(); } }
    
    public Brush DataReceivedIndicatorBrush { get => _dataReceivedIndicatorBrush; set { _dataReceivedIndicatorBrush = value; OnPropertyChanged(); } }
    
    public ICommand ConnectCommand { get; }
    public ICommand ChangeLayerCommand { get; }
    public ICommand MuteCommand { get; }
    public ICommand CallPresetCommand { get; }
    
    private static readonly string[] GainDbMap = new string[]
    {
        "-oo", "-80.0", "-79.0", "-78.0", "-77.0", "-76.0", "-75.0", "-74.0", "-73.0", "-72.0", "-71.0", "-70.0", "-69.0", "-68.0", "-67.0", "-66.0",
        "-65.0", "-64.0", "-63.0", "-62.0", "-61.0", "-60.0", "-59.0", "-58.0", "-57.0", "-56.0", "-55.0", "-54.0", "-53.0", "-52.0", "-51.0", "-50.0",
        "-49.0", "-48.0", "-47.0", "-46.3", "-45.6", "-44.9", "-44.2", "-43.5", "-42.8", "-42.1", "-41.4", "-40.7", "-40.0", "-39.7", "-39.4", "-39.1",
        "-38.8", "-38.5", "-38.2", "-37.9", "-37.6", "-37.3", "-37.0", "-36.7", "-36.4", "-36.1", "-35.8", "-35.5", "-35.2", "-34.8", "-34.4", "-34.0",
        "-33.6", "-33.2", "-32.8", "-32.4", "-32.0", "-31.6", "-31.2", "-30.8", "-30.4", "-30.0", "-29.6", "-29.2", "-28.8", "-28.4", "-28.0", "-27.6",
        "-27.2", "-26.8", "-26.4", "-26.0", "-25.6", "-25.2", "-24.8", "-24.4", "-24.0", "-23.6", "-23.2", "-22.8", "-22.4", "-22.0", "-21.6", "-21.2",
        "-20.8", "-20.6", "-20.4", "-20.2", "-20.0", "-19.7", "-19.4", "-19.1", "-18.8", "-18.5", "-18.2", "-17.9", "-17.6", "-17.3", "-17.0", "-16.7",
        "-16.4", "-16.1", "-15.8", "-15.5", "-15.2", "-14.9", "-14.6", "-14.3", "-14.0", "-13.7", "-13.4", "-13.1", "-12.8", "-12.5", "-12.2", "-11.9",
        "-11.6", "-11.3", "-11.0", "-10.7", "-10.4", "-10.1", "-9.8", "-9.6", "-9.4", "-9.2", "-9.0", "-8.8", "-8.6", "-8.4", "-8.2", "-8.0",
        "-7.8", "-7.6", "-7.4", "-7.2", "-7.0", "-6.8", "-6.6", "-6.4", "-6.2", "-6.0", "-5.9", "-5.8", "-5.7", "-5.6", "-5.5", "-5.4",
        "-5.3", "-5.2", "-5.1", "-5.0", "-4.8", "-4.6", "-4.4", "-4.2", "-4.0", "-3.8", "-3.6", "-3.4", "-3.2", "-3.0", "-2.8", "-2.6",
        "-2.4", "-2.2", "-2.0", "-1.8", "-1.6", "-1.4", "-1.2", "-1.0", "-0.9", "-0.8", "-0.7", "-0.6", "-0.5", "-0.4", "-0.3", "-0.2",
        "-0.1", "0.0", "0.2", "0.4", "0.6", "0.8", "1.0", "1.2", "1.4", "1.6", "1.8", "2.0", "2.2", "2.4", "2.6", "2.8",
        "3.0", "3.2", "3.4", "3.6", "3.8", "4.0", "4.1", "4.2", "4.3", "4.4", "4.5", "4.6", "4.7", "4.8", "4.9", "5.0",
        "5.1", "5.2", "5.3", "5.4", "5.5", "5.6", "5.7", "5.8", "5.9", "6.0", "6.1", "6.2", "6.3", "6.4", "6.5", "6.6",
        "6.8", "7.0", "7.2", "7.4", "7.6", "7.8", "8.0", "8.2", "8.4", "8.6", "8.8", "9.0", "9.2", "9.4", "9.6", "9.8", "10.0"
    };
    private readonly List<int> _meterChannelOrder;

    public MainViewModel()
    {
        _mixerService = new MixerNetworkService();
        _mixerService.OnPacketReceived += () => Application.Current.Dispatcher.Invoke(FlashIndicator);
        _mixerService.OnMeterDataReceived += (data) => Application.Current.Dispatcher.Invoke(() => OnMeterDataReceived(data));
        _mixerService.OnGainReceived += (id, val) => Application.Current.Dispatcher.Invoke(() => OnGainReceived(id, val));
        _mixerService.OnMuteStatusReceived += (id, val) => Application.Current.Dispatcher.Invoke(() => OnMuteStatusReceived(id, val));
        _mixerService.OnPresetReceived += (id) => Application.Current.Dispatcher.Invoke(() => OnPresetReceived(id));
        
        _indicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _indicatorTimer.Tick += (s, e) => { DataReceivedIndicatorBrush = Brushes.Gray; _indicatorTimer.Stop(); };

        // Inisialisasi dua timer
        _fastPollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _fastPollingTimer.Tick += FastPollingTimer_Tick;
        _slowPollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _slowPollingTimer.Tick += SlowPollingTimer_Tick;

        InitializeChannels();
        _meterChannelOrder = CreateMeterChannelOrder();
        InitializePresets();
        LrChannel = _allChannels[20];
        LoadLayer("IN1-8");

        ConnectCommand = new RelayCommand(async _ => await ToggleConnection());
        ChangeLayerCommand = new RelayCommand(layerName => LoadLayer(layerName?.ToString() ?? "IN1-8"));
        MuteCommand = new RelayCommand(async channel => await ToggleMute(channel as MixerChannel));
        CallPresetCommand = new RelayCommand(async _ => await CallPreset(), _ => SelectedPreset != null && IsConnected);
    }
    
    public void PausePolling() => _fastPollingTimer.Stop();
    public async Task EndUiInteraction(MixerChannel? channel)
    {
        if (!IsConnected) return;
        if (channel != null) await _mixerService.SetGainAsync(channel.ChannelId, channel.GainByte);
        await Task.Delay(200);
        _fastPollingTimer.Start();
    }

    private async void FastPollingTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsConnected) return;
        await _mixerService.RequestMeterDataAsync();
        await _mixerService.RequestGainAsync(LrChannel.ChannelId);
        foreach (var channel in DisplayChannels)
        {
            await _mixerService.RequestGainAsync(channel.ChannelId);
        }
    }

    private async void SlowPollingTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsConnected) return;
        await _mixerService.RequestMuteAsync(LrChannel.ChannelId);
        foreach (var channel in DisplayChannels)
        {
            await _mixerService.RequestMuteAsync(channel.ChannelId);
        }
    }
    
    private void FlashIndicator() { DataReceivedIndicatorBrush = Brushes.LimeGreen; _indicatorTimer.Start(); }
    private void InitializeChannels()
    {
        for (int i = 0; i <= 34; i++)
        {
            var channel = new MixerChannel { ChannelId = i, GainDbText = "-oo dB" };
            channel.PropertyChanged += OnChannelPropertyChanged; _allChannels.Add(i, channel);
        }
        for (int i = 0; i < 16; i++) _allChannels[i].DisplayName = $"IN {i + 1}";
        _allChannels[16].DisplayName = "IN 17-18"; _allChannels[17].DisplayName = "IN 19-20";
        _allChannels[18].DisplayName = "IN 21-22"; _allChannels[19].DisplayName = "IN 23-24";
        _allChannels[20].DisplayName = "L/R";
        for (int i = 0; i < 8; i++) _allChannels[21 + i].DisplayName = $"AUX {i + 1}";
        for (int i = 0; i < 4; i++) _allChannels[31 + i].DisplayName = $"DCA {i + 1}";
    }
    private List<int> CreateMeterChannelOrder()
    {
        var order = new List<int>();
        order.AddRange(Enumerable.Range(0, 16)); order.AddRange(Enumerable.Range(16, 4));
        order.Add(20); order.Add(20); order.AddRange(Enumerable.Range(21, 8));
        return order;
    }
    private void InitializePresets()
    {
        Presets.Add(new Preset { Id = 0, Name = "Factory Preset" });
        for (int i = 1; i <= 20; i++) Presets.Add(new Preset { Id = i, Name = $"User {i:D2}" });
        SelectedPreset = Presets.FirstOrDefault();
    }
    private async void OnChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingFromMixer) return;
        var channel = sender as MixerChannel;
        if (channel == null) return;
        if (e.PropertyName == nameof(MixerChannel.GainByte))
        {
            if (channel.GainByte < GainDbMap.Length) channel.GainDbText = GainDbMap[channel.GainByte] + " dB";
            if(IsConnected) await _mixerService.SetGainAsync(channel.ChannelId, channel.GainByte);
        }
    }
    private async Task ToggleConnection()
    {
        if (IsConnected) 
        { 
            _fastPollingTimer.Stop();
            _slowPollingTimer.Stop();
            _mixerService.Disconnect(); 
            ConnectionStatus = "Disconnected"; 
            IsConnected = false; 
        }
        else
        {
            ConnectionStatus = "Connecting...";
            bool success = await _mixerService.ConnectAsync(MixerIpAddress, 9761);
            ConnectionStatus = success ? "Connected" : "Failed to Connect";
            IsConnected = success;
            if (success)
            {
                await _mixerService.RequestCurrentPresetAsync();
                _fastPollingTimer.Start();
                _slowPollingTimer.Start();
            }
        }
    }
    private void LoadLayer(string layerName)
    {
        DisplayChannels.Clear();
        switch (layerName)
        {
            case "IN1-8": for (int i = 0; i < 8; i++) DisplayChannels.Add(_allChannels[i]); break;
            case "IN9-16": for (int i = 8; i < 16; i++) DisplayChannels.Add(_allChannels[i]); break;
            case "IN17-24": for (int i = 16; i < 20; i++) DisplayChannels.Add(_allChannels[i]); for (int i = 31; i < 35; i++) DisplayChannels.Add(_allChannels[i]); break;
            case "OUTPUT": for (int i = 21; i < 29; i++) DisplayChannels.Add(_allChannels[i]); break;
        }
    }

    private async Task ToggleMute(MixerChannel? channel)
    {
        if (channel == null || !IsConnected) return;
        
        _slowPollingTimer.Stop(); // Jeda timer lambat (khusus mute)
        
        bool newMuteState = !channel.IsMuted;
        await _mixerService.SetMuteAsync(channel.ChannelId, newMuteState);
        channel.IsMuted = newMuteState;

        await Task.Delay(100); // Jeda singkat
        
        _slowPollingTimer.Start(); // Lanjutkan timer lambat
    }

    private async Task CallPreset() { if (SelectedPreset != null && IsConnected) await _mixerService.CallPresetAsync(SelectedPreset.Id); }
    
    private void OnMeterDataReceived(byte[] meterData)
    {
        for(int i=0; i < Math.Min(meterData.Length, _meterChannelOrder.Count); i++)
        {
            int channelId = _meterChannelOrder[i];
            if (_allChannels.ContainsKey(channelId))
            {
                byte rawValue = meterData[i];
                _allChannels[channelId].MeterLevel = (rawValue / 255.0) * 100.0;
            }
        }
    }
    private void OnGainReceived(int channelId, byte gainValue)
    {
        if (_allChannels.TryGetValue(channelId, out var channel))
        {
            // Cek prioritas UI tetap penting untuk fader
            if (IsMouseCapturedOnSlider()) return;

            _isUpdatingFromMixer = true;
            channel.GainByte = gainValue;
            if (gainValue < GainDbMap.Length) channel.GainDbText = GainDbMap[gainValue] + " dB";
            _isUpdatingFromMixer = false;
        }
    }

    // Fungsi helper untuk mengecek apakah mouse sedang menekan slider
    private bool IsMouseCapturedOnSlider()
    {
        return Mouse.Captured is System.Windows.Controls.Primitives.Thumb;
    }

    private void OnMuteStatusReceived(int channelId, bool isMuted)
    {
        if (_allChannels.TryGetValue(channelId, out var channel))
        {
            _isUpdatingFromMixer = true;
            channel.IsMuted = isMuted;
            _isUpdatingFromMixer = false;
        }
    }
    private void OnPresetReceived(int presetId)
    {
        _isUpdatingFromMixer = true;
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == presetId);
        _isUpdatingFromMixer = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}