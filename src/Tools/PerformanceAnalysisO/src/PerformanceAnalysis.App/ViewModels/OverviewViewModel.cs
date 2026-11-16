using System.Windows.Input;
using PerformanceAnalysis.App.Infrastructure;
using PerformanceAnalysis.Core.Models;

namespace PerformanceAnalysis.App.ViewModels;

public sealed class OverviewViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public OverviewViewModel(MainViewModel main, IReadOnlyList<Session> sessions)
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
            .ToList();

        TrendSessions = Sessions
            .OrderBy(session => session.StartTime)
            .ToList();

        TaskCatalog = sessions
            .SelectMany(session => session.Tasks.Select(task => task.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        CoverageRows = BuildCoverageRows(sessions, TaskCatalog);

        OpenSessionCommand = new RelayCommand(parameter => _main.OpenSession(parameter));
        SetBaselineCommand = new RelayCommand(parameter =>
        {
            if (parameter is SessionListItem item)
            {
                _ = _main.SetBaselineAsync(item.Session);
            }
        });
        RefreshCommand = main.ScanCommand;
    }

    public IReadOnlyList<SessionListItem> Sessions { get; }

    public IReadOnlyList<SessionListItem> TrendSessions { get; }

    public IReadOnlyList<string> TaskCatalog { get; }

    public IReadOnlyList<CoverageRowViewModel> CoverageRows { get; }

    public ICommand OpenSessionCommand { get; }

    public ICommand SetBaselineCommand { get; }

    public ICommand RefreshCommand { get; }

    public int SessionCount => Sessions.Count;

    public int DeviceCount => Sessions
        .Select(session => session.DeviceKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int TaskCount => TaskCatalog.Count;

    public int WarningCount => Sessions.Sum(session => session.WarningCount);

    public string SessionCountText => SessionCount.ToString();

    public string DeviceCountText => DeviceCount.ToString();

    public string TaskCountText => TaskCount.ToString();

    public string WarningCountText => WarningCount == 0 ? "0" : WarningCount.ToString();

    public string SessionDetailText => "已載入的監測場次";

    public string DeviceDetailText => "依裝置指紋分組";

    public string TaskDetailText => "跨場次任務種類";

    public string WarningDetailText => WarningCount == 0 ? "沒有需要注意的項目" : "缺少檔案／資料品質／任務未執行";

    public string SourcePath => _main.SourcePath;

    public string LatestSessionText
        => Sessions.Count == 0
            ? "—"
            : Sessions.Max(session => session.StartTime).ToString("yyyy-MM-dd HH:mm:ss");

    private static IReadOnlyList<CoverageRowViewModel> BuildCoverageRows(
        IReadOnlyList<Session> sessions,
        IReadOnlyList<string> taskCatalog)
    {
        return sessions
            .OrderByDescending(session => session.StartTime)
            .Select(session =>
            {
                var cells = taskCatalog
                    .Select(taskName =>
                    {
                        var task = session.Tasks.FirstOrDefault(candidate =>
                            string.Equals(candidate.Name, taskName, StringComparison.OrdinalIgnoreCase));

                        if (task is null)
                        {
                            return new CoverageCellViewModel(
                                taskName,
                                hasRun: false,
                                "—",
                                "此場次沒有此任務");
                        }

                        if (task.Runs.Count == 0 || !task.Aggregate.Cpu.HasData)
                        {
                            return new CoverageCellViewModel(
                                taskName,
                                hasRun: false,
                                "未執行",
                                "任務全程未進入執行中狀態");
                        }

                        return new CoverageCellViewModel(
                            taskName,
                            hasRun: true,
                            $"{task.Aggregate.Cpu.Mean:0.0}%",
                            $"執行 {Formatting.Duration(task.Aggregate.ActiveDuration)}，"
                            + $"CPU 平均 {task.Aggregate.Cpu.Mean:0.0}% / 最高 {task.Aggregate.Cpu.Max:0.0}%");
                    })
                    .ToList();

                return new CoverageRowViewModel(
                    session.FolderName,
                    new SessionListItem(session).DeviceDisplay,
                    session.StartTime.ToString("MM-dd HH:mm:ss"),
                    cells);
            })
            .ToList();
    }
}
