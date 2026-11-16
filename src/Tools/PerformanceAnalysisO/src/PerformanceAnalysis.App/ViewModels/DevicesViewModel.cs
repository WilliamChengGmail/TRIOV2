using System.Windows.Input;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class DeviceGroupViewModel
{
    public DeviceGroupViewModel(
        string deviceId,
        string displayName,
        string hardwareText,
        string alias,
        IReadOnlyList<SessionListItem> sessions,
        string baselineText,
        string baselineSessionName)
    {
        DeviceId = deviceId;
        DisplayName = displayName;
        HardwareText = hardwareText;
        Alias = alias;
        Sessions = sessions;
        BaselineText = baselineText;
        BaselineSessionName = baselineSessionName;
    }

    public string DeviceId { get; }

    public string DisplayName { get; }

    public string HardwareText { get; }

    public string Alias { get; }

    public IReadOnlyList<SessionListItem> Sessions { get; }

    public string BaselineText { get; }

    public string BaselineSessionName { get; }

    public int SessionCount => Sessions.Count;

    public string SessionCountText => SessionCount.ToString();

    public bool HasBaseline => Sessions.Any(session => session.IsBaseline);
}

public sealed class DevicesViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private DeviceGroupViewModel? _selectedDevice;
    private SessionListItem? _selectedSession;
    private DeviceGroupViewModel? _selectedTargetDevice;
    private string _aliasText = string.Empty;
    private string _newDeviceName = string.Empty;

    public DevicesViewModel(MainViewModel main, IReadOnlyList<Session> sessions)
    {
        _main = main;

        Devices = sessions
            .GroupBy(session => session.Device.DeviceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var baseline = group.FirstOrDefault(session => session.Metadata?.IsBaseline == true);
                var sessionItems = group
                    .OrderByDescending(session => session.StartTime)
                    .Select(session => new SessionListItem(session, baseline))
                    .ToList();
                var representative = group.First().Device;
                var displayName = new SessionListItem(group.First()).DeviceDisplay;

                return new DeviceGroupViewModel(
                    representative.DeviceKey,
                    displayName,
                    BuildHardwareText(representative),
                    representative.Alias ?? string.Empty,
                    sessionItems,
                    baseline is null ? "—" : "★",
                    baseline?.FolderName ?? "—");
            })
            .OrderByDescending(device => device.SessionCount)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SaveAliasCommand = new RelayCommand(_ => _ = SaveAliasAsync());
        SetBaselineCommand = new RelayCommand(_ => _ = SetBaselineAsync());
        MoveSessionCommand = new RelayCommand(_ => _ = MoveSessionAsync());
        SplitSessionCommand = new RelayCommand(_ => _ = SplitSessionAsync());
        BackCommand = main.BackCommand;
        HomeCommand = main.HomeCommand;
        RefreshCommand = main.ScanCommand;

        SelectedDevice = Devices.FirstOrDefault();
        SelectedTargetDevice = Devices.FirstOrDefault(device =>
            !string.Equals(device.DeviceId, SelectedDevice?.DeviceId, StringComparison.OrdinalIgnoreCase))
            ?? Devices.FirstOrDefault();
    }

    public IReadOnlyList<DeviceGroupViewModel> Devices { get; }

    public IReadOnlyList<SessionListItem> Sessions => SelectedDevice?.Sessions ?? Array.Empty<SessionListItem>();

    public DeviceGroupViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                AliasText = value?.Alias ?? string.Empty;
                SelectedSession = value?.Sessions.FirstOrDefault();
                OnPropertyChanged(nameof(Sessions));
                OnPropertyChanged(nameof(HasSelectedDevice));
                OnPropertyChanged(nameof(SelectedDeviceHeader));
                OnPropertyChanged(nameof(SelectedDeviceHardware));
                OnPropertyChanged(nameof(SelectedDeviceBaseline));
            }
        }
    }

    public SessionListItem? SelectedSession
    {
        get => _selectedSession;
        set => SetProperty(ref _selectedSession, value);
    }

    public DeviceGroupViewModel? SelectedTargetDevice
    {
        get => _selectedTargetDevice;
        set => SetProperty(ref _selectedTargetDevice, value);
    }

    public string AliasText
    {
        get => _aliasText;
        set => SetProperty(ref _aliasText, value);
    }

    public string NewDeviceName
    {
        get => _newDeviceName;
        set => SetProperty(ref _newDeviceName, value);
    }

    public bool HasSelectedDevice => SelectedDevice is not null;

    public string SelectedDeviceHeader => SelectedDevice is null ? "選擇裝置" : SelectedDevice.DisplayName;

    public string SelectedDeviceHardware => SelectedDevice?.HardwareText ?? "—";

    public string SelectedDeviceBaseline => SelectedDevice?.BaselineSessionName ?? "—";

    public ICommand SaveAliasCommand { get; }

    public ICommand SetBaselineCommand { get; }

    public ICommand MoveSessionCommand { get; }

    public ICommand SplitSessionCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand HomeCommand { get; }

    public ICommand RefreshCommand { get; }

    private async Task SaveAliasAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        await _main.UpdateDeviceAliasAsync(SelectedDevice.DeviceId, AliasText);
    }

    private async Task SetBaselineAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        await _main.SetBaselineAsync(SelectedSession.Session);
    }

    private async Task MoveSessionAsync()
    {
        if (SelectedSession is null || SelectedTargetDevice is null)
        {
            return;
        }

        await _main.MoveSessionAsync(SelectedSession.Session, SelectedTargetDevice.DeviceId);
    }

    private async Task SplitSessionAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        await _main.SplitSessionAsync(SelectedSession.Session, NewDeviceName);
    }

    private static string BuildHardwareText(DeviceProfile device)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(device.CpuModel))
        {
            parts.Add(device.CpuModel!);
        }

        if (device.Cores is int cores)
        {
            parts.Add($"{cores}C");
        }

        if (device.RamGb is double ram)
        {
            parts.Add($"{ram:0.#} GB");
        }

        if (!string.IsNullOrWhiteSpace(device.OsName))
        {
            parts.Add(device.OsName!);
        }

        if (!string.IsNullOrWhiteSpace(device.OsBuild))
        {
            parts.Add($"Build {device.OsBuild}");
        }

        return parts.Count == 0 ? "硬體資訊不完整" : string.Join(" ・ ", parts);
    }
}
