# TRIOV2 Performance Analysis

TRIOV2 記錄器產出的硬體效能資料分析工具。

> 交付總覽與文件索引：`DELIVERY.md`

目前進度：**M0–M5 全部完成**；0.6 新增給非技術讀者的**結論燈號**（良好／注意／異常／資料不足）與白話說明。

> **使用手冊：`docs/10-user-manual.md`**（依角色分章：主管 5 分鐘、測試人員、SW 深入分析）

## 需求

- .NET 8 SDK
- Windows

## 建置與測試

```powershell
dotnet build PerformanceAnalysis.sln
dotnet test PerformanceAnalysis.sln
```

## 啟動 WPF 應用程式

```powershell
dotnet run --project src\PerformanceAnalysis.App
```

啟動後會自動載入預設路徑：

```
D:\TRIOV2\publish_tools\Logs
```

也可以：

- 在頂部路徑欄輸入或瀏覽資料夾，按「掃描」。
- 直接把資料夾拖曳到視窗。
- 指定單一場次：

```powershell
dotnet run --project src\PerformanceAnalysis.App -- --path "D:\TRIOV2\publish_tools\Logs\20261112_150751"
```

- 直接開啟裝置管理或比較頁：

```powershell
dotnet run --project src\PerformanceAnalysis.App -- --page devices
dotnet run --project src\PerformanceAnalysis.App -- --page compare
```

- 指定中繼資料資料夾（預設為執行檔旁的 `Metadata`）：

```powershell
dotnet run --project src\PerformanceAnalysis.App -- --metadata-dir "D:\TRIOV2\Metadata"
```

- 開發／驗收用：指定基準場次或執行一組裝置操作（會寫入指定的 metadata 目錄）：

```powershell
dotnet run --project src\PerformanceAnalysis.App -- --metadata-dir "out\test-metadata" --set-baseline "20261112_150751" --screenshot "out\shots"
dotnet run --project src\PerformanceAnalysis.App -- --metadata-dir "out\test-metadata" --demo-ops 1 --screenshot "out\shots-ops"
```

## 目前介面

### 總覽（多場次）

- KPI：檢測場次、裝置數量、任務種類、需要注意
- 歷次測試 CPU 平均／最高趨勢圖
- 檢測紀錄表：裝置、時間、時長、CPU、RAM、ΔCPU、ΔRAM、任務、警告、基準
- 任務覆蓋矩陣：場次 × 任務，顯示每次任務的 CPU 平均或未執行
- 雙擊列或按「開啟」進入單場次

### 裝置管理（M3）

- 裝置列表：別名、場次數、硬體、基準
- 編輯裝置別名
- 將場次移到其他裝置（合併）
- 將場次拆分為新裝置
- 設定基準場次

### 場次比較（M3）

- 勾選 2 場以上進行比較
- 可選擇比較基準場次（跨裝置亦可）
- 比較 CPU 平均／最高、RAM 平均／最高、磁碟、網路、辨識、完整率
- ΔCPU／ΔRAM／Δ辨識：與基準的差異，正向紅色、負向綠色
- 預設自動選取每台裝置的基準與最近場次

### 單場次分析

- 標頭：裝置、硬體、OS、時間範圍、模式、警告摘要、基準徽章
- 基準比較：顯示與同裝置基準的 CPU／RAM／辨識差異
- KPI：CPU、RAM、磁碟峰值、網路峰值、辨識時長、資料完整率
- 系統 CPU／記憶體時間序列圖
- 任務執行時間軸（Gantt）
- 任務 CPU／記憶體圖
- 任務統計表
- 備註與標籤編輯（儲存在中繼資料）
- 診斷與資料品質：警告／錯誤優先，資訊可展開
- 原始資料（系統樣本，可展開）

圖表操作：

- 滑鼠滾輪：捲動頁面
- `Ctrl` + 滑鼠滾輪：縮放圖表
- 圖表支援拖曳平移與右鍵選單

### 截圖模式（開發／驗收用）

```powershell
dotnet run --project src\PerformanceAnalysis.App -- --screenshot "out\shots"
```

會輸出：

- `overview.png`：總覽（列表）
- `overview-coverage.png`：總覽（任務覆蓋矩陣）
- `session.png`：單場次頂部
- `session-middle.png`：任務時間軸與圖表
- `session-bottom.png`：任務統計、備註、診斷、原始資料
- `devices.png`：裝置管理
- `compare.png`：場次比較
- `settings.png`：分析設定
- `back.png`：返回總覽

## 匯出 HTML 報告

WPF 應用程式右上角有「匯出 HTML」按鈕；匯出後會自動開啟瀏覽器。

- 單一 `.html` 檔案，可離線開啟。
- 可切換裝置、場次。
- 同機多次比較、跨機比較。
- 淺色／深色主題、列印 / PDF。

CLI 也可以直接產生：

```powershell
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --html "out\perf-report.html"
```

報告輸出範例：`out\perf-report.html`。

## 快速使用（CLI）

```powershell
# 單一場次
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs\20261112_150751" --verbose

# 上層目錄（多場次）
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs"

# 輸出 JSON 並寫入推斷的中繼資料
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --json out\sessions.json --write-metadata

# 輸出單一 HTML 報告
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --html out\perf-report.html
```

## 專案結構

```
src/
  PerformanceAnalysis.Core/   解析、統計、診斷、中繼資料（純 .NET）
  PerformanceAnalysis.Cli/    驗證與批次輸出的命令列工具
  PerformanceAnalysis.App/    WPF 分析介面（M2–M4）
tests/
  PerformanceAnalysis.Core.Tests/
docs/
  01-data-spec.md             資料格式、統計定義、session.json schema
  02-m1-validation.md         M1 驗證結果
  03-m2-ui.md                 M2 介面與操作說明
  04-m3-ui.md                 M3 裝置管理、比較、基準
  05-m4-html-report.md        M4 HTML 報告
  06-release.md               M5 設定、快取、單檔發佈
  07-acceptance.md            交付驗收清單
  08-user-guide.md            操作手冊（快速版）
  09-manual.md                完整技術手冊（0.5 版）
  10-user-manual.md           使用手冊（0.6，依角色分章）
  images/                     手冊截圖
```

## 中繼資料位置

WPF 應用與 CLI 會在**執行檔旁**建立：

```
Metadata\
  sessions\<sessionKey>.json
```

不寫入原始 Logs。WPF 與 CLI 都可用 `--metadata-dir` 覆寫。

分析門檻設定：

```
<執行檔>\Metadata\settings.json
```

可由側邊欄「設定」調整；CLI 會讀取同一份設定。

## 已完成

- 容錯 CSV 解析（BOM、Big5、註解列、引號、欄位數不一致）
- `System_BenchmarkLog.csv` 與任務 CSV 解析
- `ref.txt` 摘要與硬體解析
- 單場次／多場次／`裝置/場次` 掃描
- 統計：avg / min / max / p50 / p95 / stddev、CPU 高負載占比、記憶體成長率
- 任務區間切分（PID 變更、跳秒、狀態翻跳）
- 裝置指紋與 `sessionKey`
- 中繼資料讀寫（不進原始資料夾）
- 診斷：缺檔、重複時間戳記、跳秒、任務未執行、ref 不一致
- WPF 總覽與單場次分析介面
- ScottPlot 時間序列圖、任務 Gantt、深色主題
- 裝置管理：別名、基準、移動／合併、拆分
- 場次比較：勾選場次、可選基準、ΔCPU／ΔRAM／Δ辨識
- 任務覆蓋矩陣
- 備註與標籤
- 單一 HTML 報告：裝置切換、單場次、同機／跨機比較、深淺主題、列印
- ECharts 內嵌、離線可用、LTTB 降採樣
- 可設定異常門檻（CPU、RAM、成長率、跳秒、重複、任務重啟）
- 記憶體解析快取與清除管理
- 單檔自包含 exe 發佈
- 結論燈號與白話說明（`HealthAssessor`）：WPF 總覽／單場次、HTML 報告、CLI 皆顯示
- 38 個單元／整合測試（含現有 10 場真實資料）

## 發佈

```powershell
.\publish.ps1
```

輸出：

```
publish\TRIOV2.PerformanceAnalysis.exe
```

自包含單檔，約 75–80 MB。需要較小檔案時：

```powershell
.\publish.ps1 -FrameworkDependent
```

## 後續可選

五個階段已完成。後續可考慮：

- HTML 報告只匯出勾選場次、直接 PDF
- 跨機比較核心數正規化
- 持久化解析快取（SQLite）
- 程式碼簽章與安裝程式
- 多使用者 metadata 鎖定
