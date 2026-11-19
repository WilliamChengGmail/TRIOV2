# TRIO V2 工具套件目錄說明 (Tools Directory Guide)

> **維護單位**: Office of William  
> **最後更新**: 2026-11-19  
> **核心原則**: **獨立免安裝、零相依（Standalone / Portable）**。複製本目錄至任何 Windows 目標裝置即可直接運行相關工具，無需安裝 .NET SDK 或 Python 環境。

---

## 一、 快速啟動批次檔對照表

| 批次檔名稱（根目錄） | 執行目標與路徑 | 工具類型與說明 |
| :--- | :--- | :--- |
| **`啟動拍圖辨識模擬器.bat`** | `VisionSimulator\TRIOV2.VisionSimulator.exe` | **模擬拍圖辨識器**：相機取圖、演算法推論負載注入（單次/週期/連續） |
| **`啟動效能資源監控紀錄器.bat`** | `ResourceSimulator\TRIOV2.ResourceSimulator.exe` | **效能資源監控紀錄器**：全系統與自選 Task 秒級 CPU/RAM 監控與日誌輸出 |
| **`AccountTool.bat`** | `AccountTool\AccountTool.exe` | **帳號管理工具**：帳號維護、密碼重設與 BCrypt 雜湊驗證 |
| **`DbInitializer.bat`** | `DbInitializer\DbInitializer.exe` | **資料庫初始化**：建立與修復系統 SQLite 預設資料庫結構 |
| **`DbMigrate.bat`** | `DbMigrate\DbMigrate.exe` | **資料庫遷移**：升級資料庫並對齊結構定義 |
| **`EventLogViewer.bat`** | `QueryEvents\QueryEvents.exe` | **事件日誌檢視**：查詢最近系統事件與錯誤日誌（支援參數 `%*`） |
| **`RunUsbConfigTool.bat`** | `UsbConfigTool\UsbConfigTool.exe` | **USB 設定工具**：配置與檢測外接 USB 硬體通訊 |
| **`查看UUID.bat`** | `UuidTool\UuidTool.exe` | **硬體 UUID 工具**：檢測本機主機板與 CPU 之唯一 UUID |
| **`測試IPC連線.bat`** | `ServiceTestTool\ServiceTestTool.exe` | **IPC 連線測試**：測試與特權服務之間的 Named Pipe 通訊狀態 |
| **`啟動模擬器.bat`** | `DevLauncher_bin\TRIOV2.DevLauncher.exe` | **開發啟動器**：整合式主控台與模組啟動入口 |
| **`啟動硬體模擬器.bat`** | `Simulator_bin\TRIOV2.Simulator.exe` | **底層硬體模擬器**：模擬 PLC、溫控與光學板硬體通訊 |
| **`啟動特權服務.bat`** | `..\src\TRIOV2.PrivilegedService\` | **特權系統服務**（Console Mode，需系統管理員身分） |
| **`部署到隨身碟.bat`** | `..\src\` 發佈建置腳本 | **自動化部署工具**：打包建置發佈至外接隨身碟 |

---

## 二、 開發維護腳本存放位置

依據專案規範，所有 Python (`.py`) 與 C# 腳本 (`.csx`) 資料庫初始化/遷移腳本已收納至專屬原始碼目錄：
- **原始碼路徑**：`src\Tools\Scripts\`
