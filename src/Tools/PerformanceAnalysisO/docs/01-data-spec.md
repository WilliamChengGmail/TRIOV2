# TRIOV2 效能分析 — 資料規格（M0）

版本：0.1  
最後更新：2026-11-12

## 1. 目的

本文件定義 TRIOV2 記錄器產出的 Logs 如何被 `PerformanceAnalysis.Core` 解析、正規化與統計。  
分析工具只讀取原始資料，不修改任何 Logs 檔案。

## 2. 目錄結構

```
Logs\
  <yyyyMMdd_HHmmss>\                  ← 一次監測場次；資料夾名 = 開始時間
    System_BenchmarkLog.csv           ← 全系統 1 Hz 取樣
    ref.txt                           ← 結束時產生的摘要與硬體/OS 資訊（可能缺少）
    <task>\
      <task>_BenchmarkLog.csv         ← 任務 1 Hz 取樣
```

也支援多裝置歸檔：

```
Logs\
  <device-name>\
    <yyyyMMdd_HHmmss>\
      ...
```

掃描器會自動判斷單場次、上層目錄與 `裝置/場次` 巢狀結構。  
最大掃描深度預設為 2 層。

## 3. CSV 格式

### 3.1 共通規則

- 編碼：優先 UTF-8（含 BOM）；其次 UTF-16；最後以 Big5（cp950）嘗試。
- 分隔符：逗號。
- 欄位可加雙引號；`""` 表示一個雙引號。
- 空白行忽略。
- 以 `#` 開頭的行視為註解／摘要，忽略。
- 欄位數與表頭不同時：不足補空字串、過多截斷，並計入 `malformedRowCount`。
- 數值以小數點 `.` 解析（InvariantCulture）。
- 時間格式：`yyyy-MM-dd HH:mm:ss`。

### 3.2 System_BenchmarkLog.csv

| 正規欄位 | 中文表頭 | 型別 | 說明 |
|---|---|---|---|
| Sequence | 樣本序號 | int | 顯示用，不作為排序依據 |
| Timestamp | 時間戳記 | datetime | 1 Hz |
| CpuTotalPercent | 全系統CPU(%) | double | 全系統 CPU 使用率 |
| CpuSimulatorPercent | 模擬器CPU(%) | double | 模擬器 CPU 使用率 |
| CpuFreePercent | 剩餘空閒CPU(%) | double | 空閒 CPU |
| MemoryUsedGb | 已用記憶體(GB) | double | 已用 RAM |
| MemoryAvailableGb | 可用記憶體(GB) | double | 可用 RAM |
| MemoryUsagePercent | 記憶體使用率(%) | double | RAM 使用率 |
| FrameBufferMb | 模擬影像緩衝(MB) | double | 影像緩衝，非 GPU 使用率 |
| RecognitionState | 底層辨識狀態 | string | 例：待機、辨識運算中 |
| RecognitionDurationMs | 辨識運算時長(ms) | double | 0 表示無資料 |
| OperationMode | 運作模式 | string | 例：單次拍圖辨識、極限連續高壓 |

### 3.3 `<task>_BenchmarkLog.csv`

| 正規欄位 | 中文表頭 | 型別 | 說明 |
|---|---|---|---|
| Sequence | 樣本序號 | int | 顯示用 |
| Timestamp | 時間戳記 | datetime | 1 Hz |
| ProcessName | 處理程序名稱 | string | 空值時使用任務名稱 |
| Pid | PID | int | 0 表示未執行 |
| State | 狀態 | enum | 執行中 / 尚未執行 / 其他 |
| CpuPercent | CPU使用率(%) | double | 任務 CPU |
| MemoryMb | 記憶體使用量(MB) | double | Working set |
| DiskKbPerSecond | 磁碟讀寫速率(KB/s) | double | 任務 I/O |
| NetworkKbps | 網路傳輸速率(Kbps) | double | 任務網路 |
| PowerUsage | 電源用量 | string | 低／中／高／無 |
| PowerTrend | 電源用量趨勢 | string | 上升中／下降中／低／無 |

狀態映射：

| 原始值 | 正規值 |
|---|---|
| 執行中 | Running |
| 尚未執行 | NotRunning |
| 已結束、已停止、未執行 | NotRunning |
| 其他 | Unknown（計入診斷） |

## 4. ref.txt

`ref.txt` 是使用者可讀的摘要，不是穩定 API；解析失敗不影響 CSV 分析。

解析規則：

- `►` 開頭或結尾為 `：` / `:` 的行視為區段標題。
- 區段內的 `key : value` 以第一個冒號切分。
- `- [GPU ...]` 行單獨收集。
- 標頭欄位：`量測時間`、`樣本數量`、`紀錄時長`、`紀錄目錄`。
- 摘要區段：`全系統處理器 CPU 使用率`、`全系統實體記憶體 RAM 使用量`。
- 硬體區段：`CPU 處理器資訊`、`記憶體 (RAM) 資訊`、`GPU 資訊`、`作業系統資訊`。

擷取欄位：

- CPU：型號、核心數目、邏輯處理器、L1/L2/L3。
- RAM：已安裝容量、速度、插槽。
- OS：作業系統名稱、版本、OS 組建、裝置名稱、系統類型。
- GPU：所有 GPU 行。

## 5. 時間與統計定義

| 項目 | 定義 |
|---|---|
| 場次開始 | 第一筆有效樣本時間 |
| 場次結束 | 最後一筆有效樣本時間 |
| 場次時長 | `結束 − 開始`（不含結束秒） |
| 預期樣本數 | `round(時長秒數) + 1` |
| 資料完整率 | 不重複時間戳記數 ÷ 預期樣本數，限制在 0–1 |
| 最大跳秒 | 相鄰不重複時間戳記的最大差值 |
| CPU 平均／最高／最低 | 使用原始樣本（含同秒重複） |
| CPU 高負載占比 | `CPU ≥ 80%` 或 `≥ 90%` 的樣本數 ÷ 總樣本數 |
| RAM 成長率 | 每秒平均後做最小平方法，換算 MB/min |
| 任務執行區間 | 連續 `Running`；中斷 > 2 秒或 PID 改變則切分 |
| 任務執行時間 | 區間為閉區間，單筆樣本視為 1 秒 |
| 任務執行率 | 任務執行時間 ÷ 場次時長（場次時長為 0 時以 1 秒計） |
| 任務 CPU 秒數 | 每個 Running 秒的平均 CPU% ÷ 100 後加總 |
| 任務磁碟總量 | 每個 Running 秒的平均 KB/s 加總 ÷ 1024（MB） |
| 任務網路總量 | 每個 Running 秒的平均 Kbps 加總 ÷ 8 ÷ 1024（MB） |
| 任務重啟次數 | 執行區間數 − 1 |
| 狀態翻跳 | Running ↔ 非 Running 的切換次數 |

同秒重複樣本策略：

- 統計（min/max/p95/平均）使用原始樣本，避免掩蓋峰值。
- 時間序列圖表按秒平均，避免鋸齒。
- 診斷會記錄重複筆數。

## 6. 裝置識別

優先順序：

1. `session.json` 的 `deviceId` / `deviceName`。
2. `ref.txt` 的 `裝置名稱` + 硬體。
3. 掃描時的裝置資料夾名稱（`Logs/<device>/<session>`）。
4. `未識別裝置`。

指紋組成：

```
deviceName | CPU 型號 | 核心數 | RAM GB | OS Build | GPU
```

`DeviceKey`：

- 有 `session.json` 的 `deviceId` → 直接使用。
- 否則 → `dev-` + 指紋 SHA-256 前 12 碼。

> 沒有 `ref.txt` 的場次會落在「未識別裝置」；可在 UI 手動合併／指定。  
> 硬體相同但裝置名稱不同的機器不會自動合併，避免誤判。

## 7. session.json（中繼資料）

存放位置：

```
<分析器執行檔>\Metadata\sessions\<sessionKey>.json
```

- 不寫入原始 Logs 資料夾。
- 由紀錄器寫入，或由分析器以 `--write-metadata` 產生 `"source": "derived"`。
- 寫入採暫存檔 + 原子搬移。
- 目錄可由 `--metadata-dir` 覆寫。

`sessionKey`：

```
SHA-256(firstTimestamp | lastTimestamp | systemSampleCount | systemCsvLength) 前 16 碼
```

不含路徑與資料夾名稱，因此 Logs 搬移後仍可配對。

Schema：

```json
{
  "schemaVersion": 1,
  "sessionKey": "3f9c2a7d8b1e4c60",
  "sourceFolderName": "20261112_150751",
  "source": "recorder",
  "deviceId": "sha256(machine-guid)",
  "deviceName": "DESKTOP-S136SQE",
  "alias": "實驗室 A 機",
  "startTime": "2026-11-12T15:07:51+08:00",
  "endTime": "2026-11-12T15:18:16+08:00",
  "mode": "單次拍圖辨識",
  "tasks": ["consumables_benchmark", "SimAssaying"],
  "hardware": {
    "cpu": "Intel(R) Core(TM) Ultra 7 255H",
    "cores": 4,
    "logicalCpus": 4,
    "ramGB": 16,
    "ramSpeed": "4800 MT/s",
    "osName": "Microsoft Windows 10 專業版",
    "osVersion": "22H2",
    "osBuild": "19045.6466",
    "gpu": "VMware SVGA 3D"
  },
  "labels": ["實驗室A", "版本v2.3"],
  "isBaseline": true,
  "deviceOverridden": false,
  "notes": "夜間測試",
  "updatedAt": "2026-11-12T15:19:02+08:00"
}
```

補充：

- `isBaseline`：同一 `deviceId` 最多一個 `true`。由 UI「設為基準」維護。
- `deviceOverridden`：手動移動／拆分場次時設為 `true`。此時 `deviceName` 優先於 `ref.txt`，即使名稱是「未識別裝置」。
- `notes`：自由文字備註。
- `labels`：標籤陣列，UI 以逗號分隔編輯。

## 8. 診斷代碼

| 代碼 | 等級 | 說明 |
|---|---|---|
| CSV_READ_FAILED | Error | 檔案無法讀取 |
| CSV_EMPTY | Error | 沒有資料列 |
| MISSING_TIMESTAMP | Error | 缺少時間戳記欄位 |
| MISSING_COLUMN | Warning | 缺少預期欄位，以 0 處理 |
| INVALID_ROWS | Warning | 時間戳記無效而略過 |
| CSV_COMMENTS | Info | 忽略的註解／摘要行數 |
| CSV_MALFORMED | Warning | 欄位數不一致 |
| NO_REF | Warning | 缺少 ref.txt |
| NO_SYSTEM_LOG | Warning | 缺少系統 CSV |
| NO_TASK_LOGS | Warning | 沒有任務 CSV |
| NO_SAMPLES | Error | 完全沒有有效樣本 |
| SHORT_SESSION | Warning | 場次 < 10 秒 |
| SAMPLE_GAP | Warning | 紀錄中斷 > 2 秒 |
| DUPLICATE_TIMESTAMPS | Warning | 同秒重複樣本 |
| TASK_NEVER_RUN | Warning | 任務全程未執行 |
| UNKNOWN_STATE | Warning | 未知狀態值 |
| REF_MISMATCH | Warning | ref.txt 與實算統計不一致 |
| METADATA_MISSING | Info | 沒有 session.json |
| METADATA_WRITE_FAILED | Warning | 中繼資料寫入失敗 |
| PATH_NOT_FOUND | Error | 掃描路徑不存在 |
| NO_SESSIONS_FOUND | Warning | 找不到場次 |

## 9. CLI

```powershell
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs"
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs\20261112_150751" --verbose
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --json out\sessions.json --write-metadata
```

選項：

- `--json <file>`：輸出完整分析 JSON。
- `--write-metadata`：將推斷的中繼資料寫入 `Metadata\sessions`。
- `--metadata-dir <path>`：覆寫中繼資料根目錄。
- `--verbose`：顯示任務明細。
- `--show-info`：顯示 Info 診斷。

## 10. 已知限制

- 沒有 GPU 使用率、溫度、風扇、NPU 負載；目前只有影像緩衝。
- `ref.txt` 格式可能因版本不同而缺少硬體區段。
- 同秒重複樣本無法還原真實取樣時刻；採上述統計策略。
- 任務 CPU 加總可能超過全系統 CPU，介面需標註，不直接相加。
- 沒有 `ref.txt` 且無裝置資料夾時，裝置歸屬可能不明。
