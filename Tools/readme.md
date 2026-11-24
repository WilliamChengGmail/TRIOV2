# TRIO V2 工具套件目錄說明 (Tools Directory Guide)

> **維護單位**: Office of William  
> **最後更新**: 2026-11-24  
> **核心原則**: **獨立免安裝、零相依（Standalone / Portable）**。全數工具採 **Self-Contained（自包含）** 獨立編譯發佈，內建獨立之 .NET 8 與 Windows Desktop Runtime。複製本目錄至任何未安裝 .NET 元件的 Windows 目標裝置即可直接運行。
> **批次發佈腳本**: `src\Tools\Scripts\Build-AllTools.ps1`（一鍵自動化發佈全部 13 項工具）

---

## 一、 快速啟動批次檔對照表

| 批次檔名稱（根目錄）                 | 執行目標與路徑                                          | 工具類型與說明                                                           |
|:-------------------------- |:------------------------------------------------ |:----------------------------------------------------------------- |
| **`啟動拍圖辨識模擬器.bat`**        | `VisionSimulator\TRIOV2.VisionSimulator.exe`     | **模擬拍圖辨識器**：相機取圖、演算法推論負載注入（單次/週期/連續）                              |
| **`啟動效能資源監控紀錄器.bat`**      | `ResourceSimulator\TRIOV2.ResourceSimulator.exe` | **效能資源監控紀錄器**：全系統與自選 Task 秒級 CPU/RAM 監控與日誌輸出                      |
| **`啟動資料庫IO模擬器.bat`**       | `DbIoSimulator\TRIOV2.DbIoSimulator.exe`         | **底層資料庫 I/O 模擬器**：模擬 SQLite 高頻、併發讀寫負載與 I/O 壓力測試                   |
| **`AccountTool.bat`**      | `AccountTool\AccountTool.exe`                    | **帳號管理工具**：帳號維護、密碼重設與 BCrypt 雜湊驗證                                 |
| **`DbInitializer.bat`**    | `DbInitializer\DbInitializer.exe`                | **資料庫初始化**：建立與修復系統 SQLite 預設資料庫結構                                 |
| **`DbMigrate.bat`**        | `DbMigrate\DbMigrate.exe`                        | **資料庫遷移**：升級資料庫並對齊結構定義                                            |
| **`EventLogViewer.bat`**   | `QueryEvents\QueryEvents.exe`                    | **事件日誌檢視**：查詢最近系統事件與錯誤日誌（支援參數 `%*`）                               |
| **`RunUsbConfigTool.bat`** | `UsbConfigTool\UsbConfigTool.exe`                | **USB 設定工具**：配置與檢測外接 USB 硬體通訊                                     |
| **`查看UUID.bat`**           | `UuidTool\UuidTool.exe`                          | **硬體 UUID 工具**：檢測本機主機板與 CPU 之唯一 UUID                              |
| **`測試IPC連線.bat`**          | `ServiceTestTool\ServiceTestTool.exe`            | **IPC 連線測試**：測試與特權服務之間的 Named Pipe 通訊狀態                           |
| **`啟動模擬器.bat`**            | `DevLauncher_bin\TRIOV2.DevLauncher.exe`         | **開發啟動器**：整合式主控台與模組啟動入口                                           |
| **`啟動硬體模擬器.bat`**          | `Simulator_bin\TRIOV2.Simulator.exe`             | **底層硬體模擬器**：模擬 PLC、溫控與光學板硬體通訊                                     |
| **`啟動特權服務.bat`**           | `PrivilegedService\TRIOV2.PrivilegedService.exe` | **特權系統服務**（Console Mode，需系統管理員身分）                                 |
| **`部署到隨身碟.bat`**           | `..\src\` 自動發佈建置腳本                               | **隨身碟部署打包**：一鍵打包 App + 特權服務 + 資料庫至外接隨身碟（Self-Contained 免安裝）       |
| **`啟動資料庫IO模擬器.bat`**       | `DbIoSimulator\TRIOV2.DbIoSimulator.exe`         | **底層資料庫 I/O 模擬器**：模擬通訊時對DB做log寫入的負荷測試 |

---

## 二、 開發維護腳本存放位置

依據專案規範，所有 Python (`.py`) 與 C# 腳本 (`.csx`) 資料庫初始化/遷移腳本已收納至專屬原始碼目錄：

- **原始碼路徑**：`src\Tools\Scripts\`

---

## 三、 補充說明

在 `Tools\部署到隨身碟.bat` 中，執行「將全部資料庫複製到 `App\Database\`」的**資料庫來源位置**與運作機制如下：

* * *

### **1、 資料庫的來源位置**

* **實體來源路徑**：  
  D:\TRIOV2\Database*.db

* **腳本中的相對路徑定義**：  
  在 Tools/部署到隨身碟.bat 中：
  bat
  set PROJECT_ROOT=%~dp0..
  copy /y "%PROJECT_ROOT%\Database\*.db" "%DEPLOY_DIR%\App\Database\"
  其中 `%~dp0..` 代表批次檔所在的 `Tools` 目錄上一層，即專案根目錄 `D:\TRIOV2`。

* * *

### **2、 複製的 5 個資料庫清單**

腳本會將 D:\TRIOV2\Database 底下的所有 SQLite 資料庫（`.db`）完整複製至隨身碟：

| 資料庫檔名                  | 資料庫用途說明                                              |
| ---------------------- | ---------------------------------------------------- |
| **`system_config.db`** | **系統全域設定**：UI 佈景主題、語系偏好、密碼複雜度策略、USB 硬體設定、系統預設參數      |
| **`system_event.db`**  | **系統事件與日誌**：操作稽核軌跡（Audit Trail）、錯誤異常紀錄、UV 燈啟閉與門板中斷紀錄 |
| **`main.db`**          | **帳號與權限**：系統使用者清單、角色權限（Role/Permission）、BCrypt 密碼雜湊  |
| **`data.db`**          | **業務與檢測資料**：儀器檢測數據、實驗批次紀錄與量測結果                       |
| **`config.db`**        | **模組配置庫**：底層模組相容配置                                   |

* * *

### **3、 為什麼是複製到隨身碟的 `App\Database\`？**

這是為了確保隨身碟在**任何一台全新目標裝置**上都能「免安裝、零設定」直接開箱運行：

1. **自適應探測（Self-Contained Portable）**： 主應用程式 src/TRIOV2.App/App.xaml.cs 在啟動時會透過 `FindProjectRoot()` 向上搜尋 `Database/` 目錄。
2. **優先讀取隨身碟本機庫**： 當在隨身碟執行 `E:\TRIOV2_Deploy\App\TRIOV2.App.exe` 時，程式會第一優先找到隨身碟自身同層的 `E:\TRIOV2_Deploy\App\Database\`，直接以此作為資料庫工作路徑。
3. **設備獨立性**： 目標電腦就算完全沒有 `D:\TRIOV2` 目錄或未安裝任何開發工具，資料讀寫全部侷限在隨身碟內部，達成真正的可攜式系統。
