# TRIOV2 效能分析 — 交付說明

版本：**0.5.0**  
日期：2026-11-13  
專案：`D:\TRIOV2\Tools\PerformanceAnalysisO`  
狀態：**M0–M5 全部完成**

## 1. 交付物

| 交付物 | 位置 |
|---|---|
| 原始碼 | `src\PerformanceAnalysis.Core`、`Cli`、`App` |
| 測試 | `tests\PerformanceAnalysis.Core.Tests` |
| 單檔 exe | `publish\TRIOV2.PerformanceAnalysis.exe` |
| HTML 報告範例 | `out\perf-report.html` |
| 發佈截圖 | `out\publish-shots\` |
| 設定／快取截圖 | `out\shots-m5-settings\` |
| 發佈腳本 | `publish.ps1` |
| 文件 | `docs\01`–`08` |

## 2. 快速啟動

### 單檔 exe

```text
publish\TRIOV2.PerformanceAnalysis.exe
```

### 從原始碼

```powershell
cd D:\TRIOV2\Tools\PerformanceAnalysisO
dotnet run --project src\PerformanceAnalysis.App
```

啟動後預設載入：

```text
D:\TRIOV2\publish_tools\Logs
```

## 3. 文件導覽

| 文件 | 內容 |
|---|---|
| `docs\01-data-spec.md` | 資料格式、統計定義、metadata schema |
| `docs\02-m1-validation.md` | M1 解析驗證 |
| `docs\03-m2-ui.md` | M2 WPF 介面 |
| `docs\04-m3-ui.md` | M3 裝置、基準、比較 |
| `docs\05-m4-html-report.md` | M4 HTML 報告 |
| `docs\06-release.md` | M5 設定、快取、發佈 |
| `docs\07-acceptance.md` | **交付驗收清單** |
| `docs\08-user-guide.md` | **操作手冊（快速版）** |
| `docs\09-manual.md` | **完整使用手冊（含圖表意義與判定依據）** |

## 4. 驗收摘要

| 項目 | 結果 |
|---|---|
| Release 建置 | 0 警告、0 錯誤 |
| 測試 | 32 通過 |
| 真實場次 | 10 場 |
| 裝置分組 | 3 組（含未識別） |
| 任務種類 | 8 |
| 單次解析 | 約 0.2–0.4 秒 |
| 單檔 exe | 約 78.6 MB，已實測啟動與截圖 |
| HTML 報告 | 約 1.24 MB，離線、可切換裝置／比較 |
| 外部網路 | 無 |
| console error | 無 |

實測 `20261112_150751`：

- CPU 平均 29.4% / 最高 98.0%
- RAM 平均 10.46 GB / 最高 11.10 GB
- 磁碟峰值 72.43 MB/s
- 網路峰值 9.11 Mbps
- 辨識 800 ms
- 樣本 625 / 625

## 5. 主要功能

- 單場次／多場次／`裝置/場次` 掃描
- 容錯 CSV（BOM、Big5、註解列、重複時間戳記、缺檔）
- CPU／RAM／磁碟／網路／辨識統計
- 任務區間、Gantt、任務統計
- 裝置別名、基準、移動／拆分
- 同機多次比較、跨機比較、Δ 值
- 任務覆蓋矩陣
- 備註與標籤
- 單一 HTML 報告（離線 ECharts、深淺主題、列印）
- 可設定異常門檻
- 記憶體解析快取與清除
- 單檔自包含 exe

## 6. 系統需求

- Windows 10/11 x64
- 已發佈版本：不需要安裝 .NET
- 從原始碼：.NET 8 SDK
- 建議螢幕：1440×900 以上

## 7. 資料位置

### 原始 Logs（唯讀）

```text
D:\TRIOV2\publish_tools\Logs\<場次>\
```

### 中繼資料與設定

```text
<執行檔>\Metadata\
  sessions\<sessionKey>.json
  settings.json
```

備份時請一併備份 `Metadata`。

## 8. 已知限制

- HTML 報告一次匯出全部載入場次，尚未只匯出勾選場次。
- 場次很多時報告會變大。
- 跨機比較尚未做核心數正規化。
- 解析快取只在記憶體，重啟後第一次掃描需重新解析。
- 設定檔綁定 metadata 根目錄。
- 尚未提供程式碼簽章或安裝程式。
- 多使用者同時寫入同一 metadata 尚未加鎖。

## 9. 發佈

```powershell
.\publish.ps1
```

輸出：

```text
publish\TRIOV2.PerformanceAnalysis.exe
```

框架相依版本：

```powershell
.\publish.ps1 -FrameworkDependent
```

## 10. 後續可選

- HTML 報告只匯出勾選場次、直接 PDF
- 跨機比較核心數正規化
- 持久化解析快取（SQLite）
- 程式碼簽章與安裝程式
- 多使用者 metadata 鎖定
