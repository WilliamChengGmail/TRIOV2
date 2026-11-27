# TRIOV2.PerformanceAnalyzer — TRIO V2 硬體效能數據分析器

依「效能資源監控紀錄器」產生的測試紀錄，判定每台電腦的硬體是否足以執行 TRIO V2：
**通過／接近上限／建議升級／資料不足**，並附白話說明與升級方向。

- **使用手冊：[docs/user-manual.md](docs/user-manual.md)**（依角色分章：主管 5 分鐘、測試人員、SW 深入分析）
- 單場測試的效能問題診斷（記憶體洩漏、程式重啟等）請用 `src\Tools\PerformanceAnalysisO`。

## 建置與執行

```powershell
dotnet build -c Release
dotnet run -c Release -- "D:\TRIOV2\publish_tools\Logs"
```

## 發佈（單檔自包含 exe）

```powershell
D:\TRIOV2\publish_tools\Publish_PerformanceAnalyzer.ps1
```

輸出：`D:\TRIOV2\publish_tools\TRIOV2.PerformanceAnalyzer.exe`（腳本會先關閉執行中的同名程式）。

## 命令列參數

| 參數 | 用途 |
|---|---|
| `<資料夾>` | 啟動時載入的紀錄資料夾 |
| `--export <資料夾>` | 輸出 HTML、CSV、Markdown 報告後結束 |
| `--screenshot <資料夾>` | 輸出四個分頁截圖後結束（製作文件用） |

## 結構

```
Core/        解析（CSV、ref.txt）、掃描、分組、判定（SizingAdvisorEngine）、白話總結（FleetSummary）、匯出、門檻保存
Models/      場次、設備、任務、判定與門檻模型
ViewModels/  主畫面 ViewModel
Views/       主視窗（四個分頁）
Controls/    跨設備長條圖、秒級互動時間軸
docs/        使用手冊與截圖
```
