# M5 設定、快取與發佈

日期：2026-11-13  
版本：v0.5.0

## 1. 分析設定

側邊欄「設定」可調整異常門檻：

| 設定 | 預設 | 說明 |
|---|---:|---|
| CPU 高負載門檻 | 90% | CPU 使用率大於等於此值 |
| CPU 持續秒數 | 10 | 連續超過門檻達此秒數才列為異常 |
| 記憶體使用率門檻 | 90% | RAM 使用率大於等於此值 |
| 可用記憶體下限 | 1 GB | 可用 RAM 低於此值時提示 |
| 記憶體成長門檻 | 20 MB/min | 線性回歸斜率超過此值時提示 |
| 記憶體觀察秒數 | 300 | 場次長度需達此秒數才判斷成長 |
| 短場次門檻 | 10 秒 | 場次短於此秒數時提示 |
| 跳秒門檻 | 2 秒 | 相鄰樣本間隔超過此值時提示 |
| 重複樣本比例 | 1% | 同一秒重複樣本超過此比例時提示 |
| 任務重啟門檻 | 3 次 | 同一任務重啟次數超過此值時提示 |

儲存位置：

```
<執行檔>\Metadata\settings.json
```

- 儲存後會清除解析快取並重新分析目前載入的場次。
- CLI 會從 `--metadata-dir`（或執行檔旁 `Metadata`）讀取同一份設定。
- 若沒有設定檔，使用預設值。

新增診斷代碼：

| 代碼 | 說明 |
|---|---|
| `CPU_HIGH_SUSTAINED` | CPU 連續超過門檻 |
| `RAM_HIGH` | RAM 使用率過高或可用 RAM 過低 |
| `MEMORY_GROWTH` | 記憶體成長斜率過高 |
| `TASK_RESTARTS` | 任務重啟次數過多 |

## 2. 解析快取

- 快取已解析的 `Session` 物件，存在**記憶體**。
- 快取 key 由來源 CSV 與 `ref.txt` 的檔名、大小、修改時間計算 SHA-256。
- 來源檔案變更會自動失效。
- 變更裝置別名、基準、拆分／移動或分析設定時，會主動清除快取。
- 設定頁可查看目前快取場次數並「清除快取」。
- 關閉程式後自動清除，不影響中繼資料、別名、基準或備註。

> 目前不提供跨程式啟始的持久化快取；若場次數量很大，可後續加入 SQLite/JSON 索引。

## 3. 單檔 exe 發佈

### 自包含單檔（建議）

```powershell
.\publish.ps1
```

輸出：

```
publish\TRIOV2.PerformanceAnalysis.exe
```

- `win-x64`
- 自包含 .NET 8 + WPF + SkiaSharp/ScottPlot
- 單檔、免安裝 .NET
- 約 75–80 MB
- 第一次啟動會解壓原生函式庫到暫存目錄

### 框架相依單檔（較小）

目標機需安裝 .NET 8 Desktop Runtime：

```powershell
.\publish.ps1 -FrameworkDependent
```

### 指定 Runtime

```powershell
.\publish.ps1 -Runtime win-arm64
```

### 驗證

```powershell
publish\TRIOV2.PerformanceAnalysis.exe --metadata-dir "out\publish-metadata" --screenshot "out\publish-shots"
```

已驗證：

- 單檔 exe 可啟動。
- 自動掃描 10 場。
- 總覽、單場次、裝置、比較、設定頁正常。
- 匯出 HTML 使用內嵌資源，不需要額外檔案。
- 發佈目錄不需要 PDB。

## 4. 版本

```
0.5.0
```

專案：

```
D:\TRIOV2\src\Tools\PerformanceAnalysisO
```

## 5. 測試

```text
dotnet build PerformanceAnalysis.sln -c Release
建置成功，0 警告、0 錯誤

dotnet test PerformanceAnalysis.sln
已通過! 失敗: 0，通過: 32，總計: 32
```

新增測試：

- 分析門檻：CPU 持續高負載、RAM 過高、記憶體成長、任務重啟
- 設定檔：儲存／讀取、缺檔、壞檔
- 解析快取：fingerprint 穩定性與來源變更失效

## 6. 已知限制

- 快取只在記憶體，程式重啟後第一次掃描仍需重新解析。
- 設定檔綁定 metadata 根目錄；不同 `--metadata-dir` 會有各自設定。
- 自包含單檔較大；若環境已安裝 .NET 8，可用框架相依版本。
- 尚未設定程式碼簽章或安裝程式。
- 尚未提供多使用者同時寫入同一 metadata 的鎖定機制。
