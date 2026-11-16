# 交付驗收清單

版本：0.5.0  
日期：2026-11-13  
專案：`D:\TRIOV2\Tools\PerformanceAnalysisO`

## 1. 建置與測試

| 項目 | 結果 | 證據 |
|---|---|---|
| Release 建置 | 成功，0 警告 0 錯誤 | `dotnet build PerformanceAnalysis.sln -c Release` |
| 單元／整合測試 | 32 通過 | `dotnet test PerformanceAnalysis.sln` |
| 真實資料場次 | 10 場 | `D:\TRIOV2\publish_tools\Logs` |
| 解析耗時 | 約 0.2–0.4 秒 | CLI／WPF 狀態列 |
| 單檔發佈 | 成功 | `publish\TRIOV2.PerformanceAnalysis.exe`（約 78.6 MB） |

## 2. 資料解析

| 驗收項目 | 結果 |
|---|---|
| 單一場次資料夾 | 通過 |
| 上層多場次資料夾 | 通過（10 場） |
| `裝置/場次` 巢狀結構 | 通過（掃描器支援） |
| UTF-8 BOM | 通過 |
| Big5 回退 | 通過（測試） |
| `#` 註解／摘要列 | 通過 |
| 欄位數不一致 | 補齊／截斷並記錄 |
| 缺 `ref.txt` | 可分析，標記 `NO_REF` |
| 缺任務紀錄 | 可分析，標記 `NO_TASK_LOGS` |
| 重複時間戳記 | 統計用原始樣本、圖表按秒平均 |
| `ref.txt` 統計比對 | `20261112_150751` CPU/RAM 與 ref 一致 |

## 3. 單場次分析

| 驗收項目 | 結果 |
|---|---|
| 裝置、CPU、RAM、OS、Build | 通過 |
| KPI：CPU／RAM／磁碟／網路／辨識／完整率 | 通過 |
| 系統 CPU 時間序列 | 通過 |
| 系統記憶體時間序列 | 通過 |
| 任務 Gantt | 通過 |
| 任務 CPU／記憶體圖 | 通過 |
| 任務統計表 | 通過 |
| 診斷與資料品質 | 通過 |
| 原始系統資料表 | 通過 |
| 備註與標籤儲存 | 通過 |
| 基準徽章與差異 | 通過 |

實測 `20261112_150751`：

- CPU 平均 29.4%、最高 98.0%
- RAM 平均 10.46 GB、最高 11.10 GB
- 磁碟峰值 72.43 MB/s
- 網路峰值 9.11 Mbps
- 辨識 800 ms
- 樣本 625 / 預期 625
- 任務 6 個

## 4. 裝置管理與比較

| 驗收項目 | 結果 |
|---|---|
| 裝置別名 | 通過 |
| 設定基準 | 通過 |
| 移動場次到其他裝置 | 通過 |
| 拆分為新裝置 | 通過 |
| 未知裝置短指紋 | 通過（如 `未識別裝置 (2715)`） |
| 手動裝置覆寫優先於 ref.txt | 通過（測試） |
| 同機多次比較 | 通過 |
| 跨機比較 | 通過 |
| 比較基準選擇 | 通過 |
| ΔCPU／ΔRAM／Δ辨識 | 通過 |
| 任務覆蓋矩陣 | 通過 |
| 場次備註與標籤 | 通過 |

驗證案例（暫存 metadata）：

- `LAB-A`：1 場，基準 `20261112_150751`
- `未識別裝置 (2715)`：2 場
- `未識別裝置 (ff75)`：6 場
- `NEW-LAB-B`：1 場

## 5. HTML 報告

| 驗收項目 | 結果 |
|---|---|
| WPF「匯出 HTML」 | 通過（按鈕已啟用） |
| CLI `--html` | 通過 |
| 單一檔案、離線 | 通過 |
| 裝置切換 | 通過 |
| 單場次檢視 | 通過 |
| 同機多次比較 | 通過 |
| 跨機比較 | 通過 |
| 深色／淺色 | 通過 |
| 列印 / PDF 樣式 | CSS 已提供 |
| 無 console error | 通過 |
| 無外部網路請求 | 通過（只載入本機 HTML） |
| 檔案大小 | 約 1.24 MB（10 場） |

報告樣本：`out\perf-report.html`

## 6. 設定與快取

| 驗收項目 | 結果 |
|---|---|
| 10 項門檻設定 | 通過 |
| 儲存後重新分析 | 通過 |
| 設定檔位置 | `<執行檔>\Metadata\settings.json` |
| CLI 讀取同一份設定 | 通過 |
| CPU 高負載診斷 | 通過（測試） |
| RAM 過高診斷 | 通過（測試） |
| 記憶體成長診斷 | 通過（測試；已修正 GB→MB 單位） |
| 任務重啟診斷 | 通過（測試） |
| 解析快取 | 通過（10 場） |
| 快取 fingerprint 失效 | 通過（測試） |
| 快取清除 | 通過（設定頁） |

## 7. 發佈

| 驗收項目 | 結果 |
|---|---|
| 自包含單檔 | `publish\TRIOV2.PerformanceAnalysis.exe` |
| 大小 | 78,619,481 bytes（約 78.6 MB） |
| 免安裝 .NET | 是 |
| 啟動 | 通過 |
| 自動掃描 10 場 | 通過 |
| 總覽／單場次／裝置／比較／設定 | 通過 |
| 發佈版截圖 | `out\publish-shots\` |
| PDB 是否包含 | 否 |

發佈指令：

```powershell
.\publish.ps1
```

## 8. 截圖證據

```text
out\publish-shots\overview.png
out\publish-shots\overview-coverage.png
out\publish-shots\session.png
out\publish-shots\session-middle.png
out\publish-shots\session-bottom.png
out\publish-shots\devices.png
out\publish-shots\compare.png
out\publish-shots\settings.png
```

設定與門檻驗證：

```text
out\shots-m5-settings\settings.png
out\shots-m5-settings\overview.png
```

## 9. 已知限制

1. HTML 報告一次包含全部載入場次，尚未提供只匯出勾選場次。
2. 場次很多時 HTML 會變大；尚未自動拆分。
3. 跨機比較尚未做核心數正規化。
4. 解析快取只在記憶體，程式重啟後第一次掃描仍需重新解析。
5. 設定檔綁定 metadata 根目錄。
6. 尚未提供程式碼簽章或安裝程式。
7. 多使用者同時寫入同一 metadata 尚未加鎖。

## 10. 驗收結論

- M0–M5 全部完成。
- 32 個測試通過。
- Release 建置 0 警告。
- 單檔 exe 可獨立執行。
- HTML 報告可離線操作與列印。
- 可直接交付使用。
