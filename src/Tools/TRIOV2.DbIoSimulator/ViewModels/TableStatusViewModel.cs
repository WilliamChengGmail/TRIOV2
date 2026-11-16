using TRIOV2.DbIoSimulator.Core;
using TRIOV2.DbIoSimulator.Core.Schema;

namespace TRIOV2.DbIoSimulator.ViewModels;

/// <summary>
/// 單一資料表的即時監控狀態列 ViewModel (支援原地屬性更新避免 DataGrid 排序閃爍)
/// 製作者: Office of William
/// </summary>
public sealed class TableStatusViewModel : ViewModelBase
{
    private long _rowCount;
    private string _lastWriteTimeText = "—";
    private double _writeOpsPerSec;
    private string _writeRateText = "0.0 op/s";
    private string _statusText = "待命";

    public SimulatedTableType TableType { get; }
    public string TableName { get; }
    public string RoleDescription { get; }

    public long RowCount
    {
        get => _rowCount;
        private set
        {
            if (SetProperty(ref _rowCount, value))
            {
                OnPropertyChanged(nameof(RowCountText));
            }
        }
    }

    public string RowCountText => _rowCount.ToString("N0");

    public string LastWriteTimeText
    {
        get => _lastWriteTimeText;
        private set => SetProperty(ref _lastWriteTimeText, value);
    }

    public double WriteOpsPerSec
    {
        get => _writeOpsPerSec;
        private set => SetProperty(ref _writeOpsPerSec, value);
    }

    public string WriteRateText
    {
        get => _writeRateText;
        private set => SetProperty(ref _writeRateText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public TableStatusViewModel(SimulatedTableType tableType, string roleDescription)
    {
        TableType = tableType;
        TableName = tableType.ToString();
        RoleDescription = roleDescription;
    }

    public void UpdateFromSnapshot(TableMetricsSnapshot snapshot, bool isSimulationRunning)
    {
        RowCount = snapshot.TotalRows;
        LastWriteTimeText = snapshot.LastWriteTimeLocal.HasValue
            ? snapshot.LastWriteTimeLocal.Value.ToString("HH:mm:ss.fff")
            : "—";
        WriteOpsPerSec = snapshot.WriteOpsPerSec;
        WriteRateText = $"{snapshot.WriteOpsPerSec:F1} op/s";
        StatusText = !snapshot.LastWriteSuccess
            ? "寫入異常"
            : isSimulationRunning && snapshot.WriteOpsPerSec > 0
                ? "寫入中"
                : isSimulationRunning
                    ? "待機中"
                    : "已停止";
    }
}

