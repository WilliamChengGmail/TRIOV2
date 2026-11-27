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

    public string WarningDetailText => $"技術警告 {WarningCount} 筆（細節見單場次）";

    public int FollowUpCount => Sessions.Count(session =>
        session.Health.Level is Core.Analysis.HealthLevel.Problem or Core.Analysis.HealthLevel.Attention);

    public string FollowUpCountText => FollowUpCount.ToString();

    public string FollowUpDetailText => FollowUpCount == 0 ? "沒有需要追蹤的場次" : "結論為「異常」或「注意」";

    public int ProblemCount => CountLevel(Core.Analysis.HealthLevel.Problem);

    public int AttentionCount => CountLevel(Core.Analysis.HealthLevel.Attention);

    public int GoodCount => CountLevel(Core.Analysis.HealthLevel.Good);

    public int InsufficientCount => CountLevel(Core.Analysis.HealthLevel.Insufficient);

    /// <summary>給主管看的一句話結論。</summary>
    public string SummarySentence
    {
        get
        {
            var usable = SessionCount - InsufficientCount;
            if (SessionCount == 0)
            {
                return "尚未載入任何場次。";
            }

            if (usable == 0)
            {
                return $"共 {SessionCount} 場紀錄，但都屬於資料不足（太短或缺檔），暫時無法判斷效能。";
            }

            if (ProblemCount > 0)
            {
                return $"可判斷的 {usable} 場中，有 {ProblemCount} 場發現效能問題，需要 SW 進一步檢查。";
            }

            return AttentionCount > 0
                ? $"可判斷的 {usable} 場都能正常運作，其中 {AttentionCount} 場有項目建議留意。"
                : $"可判斷的 {usable} 場效能表現皆正常。";
        }
    }

    private int CountLevel(Core.Analysis.HealthLevel level)
        => Sessions.Count(session => session.Health.Level == level);

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
