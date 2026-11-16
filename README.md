# TRIOV2

TRIOV2 系統開發專案。

## 專案結構

| 資料夾 | 用途 |
|---|---|
| `src\` | 主程式原始碼（App、Core、Data、PrivilegedService） |
| `src\Tools\` | 所有開發工具的**原始碼**（一個工具一個資料夾） |
| `Tools\` | 建置產生的**執行檔/批次檔** + 相依檔案（dll 等），每個工具一個資料夾存放 |
| `Database\` | 開發用 SQLite 資料庫 |
| `docs\` | 架構與設計文件 |
| `WorkLog\` | 工作紀錄 |
| `DocumentsAndInformation\` | 開發歷程文件 |

## 工具一覽

| 工具 | 原始碼（src\Tools） | 執行檔資料夾（Tools） | 說明 |
|---|---|---|---|
| AccountTool | `src\Tools\AccountTool\` | `Tools\AccountTool\` | 帳號密碼測試工具 |
| DbInitializer | `src\Tools\DbInitializer\` | `Tools\DbInitializer\` | 資料庫初始化 |
| DbMigrate | `src\Tools\DbMigrate\` | `Tools\DbMigrate\` | 資料庫遷移 |
| DevLauncher | `src\Tools\DevLauncher\` | `Tools\DevLauncher\` | 開發啟動器（模擬器面板） |
| QueryEvents | `src\Tools\QueryEvents\` | `Tools\QueryEvents\` | 事件日誌查閱 |
| ServiceTestTool | `src\Tools\ServiceTestTool\` | `Tools\ServiceTestTool\` | 特權服務 IPC 連線測試 |
| Simulator（TRIOV2.Simulator） | `src\Tools\TRIOV2.Simulator\` | `Tools\Simulator\` | 硬體模擬器 |
| UsbConfigTool | `src\Tools\UsbConfigTool\` | `Tools\UsbConfigTool\` | USB 設定工具 |
| UuidTool | `src\Tools\UuidTool\` | `Tools\UuidTool\` | 安裝 UUID 查詢 |
| TestDataSeeder | `src\Tools\TestDataSeeder\` | — | 測試資料種子（Python 腳本，無編譯產出） |
| Scripts | `src\Tools\Scripts\` | — | 資料庫維護 Python 腳本 |
| PerformanceAnalysisO | `src\Tools\PerformanceAnalysisO\` | — | 效能分析（開發中） |

## 啟動批次檔

批次檔統一放在 `Tools\` 根目錄，透過 `%~dp0` 連結子目錄內的執行檔。

| 批次檔（Tools\） | 執行目標 | 說明 |
|---|---|---|
| `AccountTool.bat` | `AccountTool\AccountTool.exe` | 帳號密碼測試工具 |
| `DbInitializer.bat` | `DbInitializer\DbInitializer.exe` | 資料庫初始化 |
| `DbMigrate.bat` | `DbMigrate\DbMigrate.exe` | 資料庫遷移 |
| `EventLogViewer.bat` | `QueryEvents\QueryEvents.exe` | 事件日誌查閱（支援傳參） |
| `RunUsbConfigTool.bat` | `UsbConfigTool\UsbConfigTool.exe` | USB 設定工具 |
| `查看UUID.bat` | `UuidTool\UuidTool.exe` | 查詢安裝 UUID |
| `測試IPC連線.bat` | `ServiceTestTool\ServiceTestTool.exe` | IPC 連線測試（模擬 App 端） |
| `啟動模擬器.bat` | `DevLauncher\TRIOV2.DevLauncher.exe` | 啟動開發啟動器 |
| `啟動硬體模擬器.bat` | `Simulator\TRIOV2.Simulator.exe` | 啟動硬體模擬器 |
| `啟動特權服務.bat` | `src\TRIOV2.PrivilegedService\`（dotnet run） | 特權服務（**需以系統管理員執行**） |
| `部署到隨身碟.bat` | 專案根目錄（`%~dp0..`） | 發布 App + Service（win-x64 self-contained）到 USB |

## Tools 資料夾其他內容

| 資料夾 | 說明 |
|---|---|
| `DeployStaging\` | 部署暫存（發布產物，可由部署 bat 重新產生） |
| `Screenshots\` | 模擬器截圖 |
| `temp\` | 暫存區（舊發布檔、開發用 DB、QueryUvDoor） |
