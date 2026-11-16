using System.Collections.ObjectModel;
using System.Windows.Input;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class SelectableSessionItem : ObservableObject
{
    private bool _isSelected;

    public SelectableSessionItem(SessionListItem item)
    {
        Item = item;
    }

    public SessionListItem Item { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string FolderName => Item.FolderName;

    public string DeviceDisplay => Item.DeviceDisplay;

    public string StartTimeText => Item.StartTimeText;

    public string BaselineMarker => Item.BaselineMarker;

    public string DisplayText => $"{FolderName} ・ {DeviceDisplay}";
}

public sealed class CompareViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private SelectableSessionItem? _selectedBaseline;

    public CompareViewModel(MainViewModel main, IReadOnlyList<Session> sessions)
    {
        _main = main;

        var baselineByDevice = sessions
            .GroupBy(session => session.Device.DeviceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(session => session.Metadata?.IsBaseline == true),
                StringComparer.OrdinalIgnoreCase);

        Sessions = sessions
            .OrderByDescending(session => session.StartTime)
            .Select(session => new SessionListItem(
                session,
                baselineByDevice.GetValueOrDefault(session.Device.DeviceKey)))
            .Select(item => new SelectableSessionItem(item))
            .ToList();

        Rows = new ObservableCollection<SessionListItem>();

        foreach (var item in Sessions)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectableSessionItem.IsSelected))
                {
                    RefreshRows();
                }
            };
        }

        SelectAllCommand = new RelayCommand(_ =>
        {
            foreach (var item in Sessions)
            {
                item.IsSelected = true;
            }
        });

        ClearSelectionCommand = new RelayCommand(_ =>
        {
            foreach (var item in Sessions)
            {
                item.IsSelected = false;
            }
        });

        BackCommand = main.BackCommand;
        HomeCommand = main.HomeCommand;

        SelectDefaultSessions();
        SelectedBaseline = Sessions.FirstOrDefault(item => item.Item.IsBaseline)
                           ?? Sessions.FirstOrDefault(item => item.IsSelected)
                           ?? Sessions.FirstOrDefault();
        RefreshRows();
    }

    public IReadOnlyList<SelectableSessionItem> Sessions { get; }

    public IReadOnlyList<SelectableSessionItem> BaselineCandidates => Sessions;

    public SelectableSessionItem? SelectedBaseline
    {
        get => _selectedBaseline;
        set
        {
            if (SetProperty(ref _selectedBaseline, value))
            {
                RefreshRows();
            }
        }
    }

    public ObservableCollection<SessionListItem> Rows { get; }

    public int SelectedCount => Rows.Count;

    public string SelectedCountText => $"已選 {SelectedCount} 場";

    public string BaselineSummaryText
    {
        get
        {
            if (SelectedBaseline is null)
            {
                return "尚未選擇基準場次";
            }

            return $"基準：{SelectedBaseline.FolderName}";
        }
    }

    public bool HasRows => Rows.Count > 0;

    public bool HasNoRows => !HasRows;

    public ICommand SelectAllCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand HomeCommand { get; }

    private void SelectDefaultSessions()
    {
        foreach (var group in Sessions.GroupBy(item => item.DeviceDisplay))
        {
            var ordered = group.OrderByDescending(item => item.Item.StartTime).ToList();
            var baseline = ordered.FirstOrDefault(item => item.Item.IsBaseline);
            var latestNonBaseline = ordered.FirstOrDefault(item => !item.Item.IsBaseline);

            if (baseline is not null)
            {
                baseline.IsSelected = true;
                if (latestNonBaseline is not null)
                {
                    latestNonBaseline.IsSelected = true;
                }
            }
            else
            {
                foreach (var item in ordered.Take(2))
                {
                    item.IsSelected = true;
                }
            }
        }
    }

    private void RefreshRows()
    {
        Rows.Clear();

        var baselineSession = SelectedBaseline?.Item.Session;

        foreach (var item in Sessions
                     .Where(item => item.IsSelected)
                     .OrderBy(item => item.Item.DeviceDisplay, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Item.StartTime))
        {
            Rows.Add(new SessionListItem(item.Item.Session, baselineSession));
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(BaselineSummaryText));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasNoRows));
    }
}
