// MixerChannel.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class MixerChannel : INotifyPropertyChanged
{
    private byte _gainByte;
    private bool _isMuted;
    private double _meterLevel;
    private string? _gainDbText;

    public int ChannelId { get; set; }
    public string? DisplayName { get; set; }

    public byte GainByte
    {
        get => _gainByte;
        set { if (_gainByte != value) { _gainByte = value; OnPropertyChanged(); } }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set { if (_isMuted != value) { _isMuted = value; OnPropertyChanged(); } }
    }

    public double MeterLevel
    {
        get => _meterLevel;
        set { if (_meterLevel != value) { _meterLevel = value; OnPropertyChanged(); } }
    }
    
    public string? GainDbText
    {
        get => _gainDbText;
        set { if (_gainDbText != value) { _gainDbText = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}