# 操作手冊

版本：0.5.0  
適用：TRIOV2 效能分析（WPF 桌面工具）

## 1. 快速開始

### 已發佈版本

```text
publish\TRIOV2.PerformanceAnalysis.exe
```

直接雙擊，或：

```powershell
.\publish\TRIOV2.PerformanceAnalysis.exe
```

### 從原始碼啟動

```powershell
cd D:\TRIOV2\src\Tools\PerformanceAnalysisO
dotnet run --project src\PerformanceAnalysis.App
```

啟動後會自動載入：

```text
D:\TRIOV2\publish_tools\Logs
```

如果不存在，會顯示空白狀態，請手動選資料夾。

## 2. 掃描資料

### 單一場次

1. 在頂部路徑欄輸入或按「瀏覽」選擇：

```text
D:\TRIOV2\publish_tools\Logs\20261112_150751
```

2. 按「掃描」。
3. 只有一場時會直接進入單場次分析。

### 多場次

選擇上層資料夾：

```text
D:\TRIOV2\publish_tools\Logs
```

按「掃描」後進入總覽。

### 拖放

直接把資料夾拖進視窗，會自動帶入路徑並掃描。

### 巢狀裝置資料夾

支援：

```text
Logs\<裝置>\<場次>\
```

掃描上層即可。

## 3. 總覽

- KPI：檢測場次、裝置數量、任務種類、需要注意
- 歷次 CPU 趨勢（平均／最高）
- 檢測紀錄表
- 任務覆蓋矩陣
- 資料品質

操作：

- 雙擊列或按「開啟」進入單場次
- 表頭可排序
- 分頁「列表／任務覆蓋矩陣」可切換
- ΔCPU／ΔRAM 以同裝置基準計算

## 4. 單場次分析

由上到下：

1. 標頭：裝置、硬體、OS、時間、樣本、模式、基準狀態
2. KPI：CPU、RAM、磁碟、網路、辨識、完整率
3. 系統 CPU／記憶體圖
4. 任務執行時間軸（Gantt）
5. 任務 CPU／記憶體圖
6. 任務統計表
7. 備註與標籤
8. 診斷與資料品質
9. 原始資料（可展開）

操作：

- 滑鼠滾輪：捲動頁面
- `Ctrl` + 滾輪：縮放圖表
- 圖表可拖曳平移、右鍵選單
- 「開啟資料夾」用檔案總管開啟原始場次
- 「設為基準」將此場次設為同裝置基準

## 5. 裝置管理

側邊欄「裝置」。

### 編輯別名

1. 左側選裝置。
2. 在上方輸入別名。
3. 按「儲存別名」。

### 設定基準

1. 右側選一場。
2. 按「設為基準」。
3. 同裝置其他場次會自動取消基準。

### 移動場次（合併）

1. 右側選場次。
2. ComboBox 選目標裝置。
3. 按「移到裝置」。

### 拆分新裝置

1. 右側選場次。
2. 輸入新裝置名稱。
3. 按「拆分為新裝置」。

### 未識別裝置

沒有 `ref.txt` 或裝置名稱時會顯示：

```text
未識別裝置 (2715)
```

後四碼是指紋，用來區分不同未識別機器。

## 6. 場次比較

側邊欄「比較」。

### 同機多次比較

1. 選擇裝置。
2. 勾選場次（預設基準＋最近場次）。
3. 選擇基準。
4. 查看 ΔCPU／ΔRAM／Δ辨識。
5. 可切換疊圖指標：全系統 CPU／已用記憶體／模擬器 CPU。

### 跨機比較

1. 切換到「跨機比較」。
2. 每台裝置選一場。
3. 查看硬體規格與比較表。
4. 硬體不同時會顯示提示。

### 顏色

- 變高：紅色
- 變低：綠色
- 基準列：顯示「基準」

## 7. HTML 報告

### 匯出

1. 右上角「匯出 HTML」。
2. 選擇檔案位置。
3. 完成後自動開啟瀏覽器。

或 CLI：

```powershell
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --html "out\perf-report.html"
```

### 報告操作

- 頂部裝置切換
- 頁籤：總覽／單場次／場次比較
- 比較頁可切換同機／跨機
- 基準場次可選擇
- 淺色／深色切換
- 「列印 / PDF」用瀏覽器列印

報告是單一 `.html`，可離線開啟與分享。

## 8. 分析設定

側邊欄「設定」。

可調整：

- CPU 高負載門檻與持續秒數
- RAM 使用率門檻與可用下限
- 記憶體成長門檻與觀察秒數
- 短場次、跳秒、重複樣本、任務重啟門檻

按「儲存並重新分析」後：

1. 寫入 `Metadata\settings.json`
2. 清除解析快取
3. 重新分析目前場次
4. 更新診斷與 HTML 報告資料

「回復預設」只會帶入預設值，仍需按儲存。

## 9. 解析快取

- 設定頁可查看目前快取場次數
- 「清除快取」會清空記憶體快取
- 來源 CSV／ref.txt 變更會自動失效
- 別名、基準、移動／拆分、設定變更會主動清除
- 關閉程式後自動清除

快取不會修改原始 Logs，也不會刪除中繼資料。

## 10. CLI

```powershell
# 掃描單場次
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs\20261112_150751" --verbose

# 掃描上層
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs"

# JSON + metadata
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --json out\sessions.json --write-metadata

# HTML
dotnet run --project src\PerformanceAnalysis.Cli -- scan "D:\TRIOV2\publish_tools\Logs" --html out\perf-report.html
```

CLI 會讀取 `--metadata-dir` 或執行檔旁 `Metadata\settings.json` 的門檻設定。

## 11. 發佈

```powershell
.\publish.ps1
```

輸出：

```text
publish\TRIOV2.PerformanceAnalysis.exe
```

需要 .NET 8 的機器可用較小版本：

```powershell
.\publish.ps1 -FrameworkDependent
```

## 12. 資料位置與備份

### 原始資料（唯讀）

```text
D:\TRIOV2\publish_tools\Logs\<場次>\
```

工具不會修改。

### 中繼資料

```text
<執行檔>\Metadata\
  sessions\<sessionKey>.json
  settings.json
```

包含：

- 裝置別名
- 基準
- 移動／拆分覆寫
- 備註、標籤
- 分析門檻

備份時請一併備份 `Metadata`。

### 快取

只在記憶體，不需要備份。

## 13. 常見問題

### 看不到裝置名稱

- 缺 `ref.txt`，或舊版 ref 沒有硬體區段。
- 可在「裝置」頁手動設定別名。

### 場次被分到錯誤裝置

- 到「裝置」頁把場次移到正確裝置。
- 必要時拆分為新裝置。

### 基準沒有 Δ 值

- 確認同裝置有設定基準。
- 比較頁可另外選擇全域基準。
- 不同裝置的場次在總覽不會有 Δ 值。

### HTML 報告沒有新設定

- 儲存設定後會重新分析；重新匯出報告。
- CLI 需使用相同的 `--metadata-dir`。

### 單檔 exe 啟動較慢

- 第一次啟動需解壓原生函式庫，之後較快。

### 設定的門檻太敏感

- 提高 CPU／RAM／成長門檻，或增加持續秒數。
- 儲存後會重新分析。

## 14. 操作小技巧

- 雙擊總覽列可快速進入單場次。
- `Ctrl` + 滾輪在圖表上縮放。
- 比較頁預設會選基準＋最近場次。
- HTML 報告可用 `#view=session&session=...` 分享特定畫面。
- 匯出報告前先設定好基準與別名，報告會帶入。
