# TRIO V2 資料庫與維護開發腳本專區 (Database & Maintenance Scripts)

> **維護單位**: Office of William  
> **放置路徑**: `src\Tools\Scripts\`  
> **用途**: 本目錄存放 TRIO V2 系統開發、資料庫初始化、舊系統資料遷移及結構審核相關的 Python (`.py`) 與 C# 腳本 (`.csx`)。

---

## 腳本清單與功能說明

### 1. 資料庫設定與安全性初始化
- **`add_close_config.py`**：向 `SystemConfig` 資料表寫入應用程式右上角關閉 (X)、`ESC` 鍵及 `Alt+F4` 關閉之啟用規則。
- **`add_login_config.py`**：向 `SystemConfig` 寫入使用者登入介面、鎖定嘗試次數（5次）及 Session Timeout 相關安全設定。
- **`recreate_useraccount.py`**：重新建立 `UserAccount` 資料表（支援 BCrypt 密碼雜湊、RoleLevel、防爆破鎖定欄位）並注入初始種子資料。

### 2. 舊機台資料遷移與匯入 (Migration)
- **`import_system_config.py`**：批次讀取舊系統 12 個 `.ini` 設定檔（馬達、光學、溫控等），轉換並匯入 `trio240plus_config.db`。
- **`import_flow_definitions.py`**：解析舊版 `.flow` 檔案中的運動與流程指令（吸吐液、混勻、PCR），匯入至 `trio240plus_main.db`。
- **`import_excel_to_db.py`**：解析舊機台歷史產出之 Excel 檢驗報告，批次轉入 `data.db`。
- **`inspect_legacy_db.py`**：分析與檢測舊系統 `userinfo.db` 的欄位定義與索引結構。
- **`verify_flowmapping.py`**：驗證 `FlowMapping` 表中的機台流程代碼與參數佈局。

### 3. 資料庫結構導出與檢核
- **`extract_schema.py`**：掃描 SQLite 資料庫，將所有資料表、欄位型別、主鍵、外鍵及索引導出為 JSON 格式供文件產出。
- **`verify_schema.py`**：自動檢查並統計所有資料庫（config/main/data/log）中的各資料表資料列數。

### 4. C# 腳本 (dotnet-script)
- **`db_migrate.csx`**：執行跨版本資料庫檔案與角色權限表之遷移程式。
- **`QueryEvents.csx`**：即時連線 `system_event.db`，查詢並格式化輸出最近 20 筆事件日誌。

### 5. 相關資產
- **`assets/FA039FF3-2D30-46BE-B021-A12C144EE108.jpg`**：機台橫直式介面重構之畫面設計參考圖。

