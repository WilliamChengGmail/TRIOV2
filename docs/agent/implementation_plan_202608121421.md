# 實作計畫：TRIO2026 專案複製與橫式螢幕改版 (TRIOV2)

> **狀態**：`[x] 已核可 (Approved)`  
> **建立時間**：2026-08-12 14:12 (更新時間：2026-08-12 14:20)  
> **負責 AI / 開發者**：Office of William  
> **關聯專案**：`D:\TRIO2026` -> `d:\TRIOV2`  

---

## 1. 任務目標與範疇 (Goal & Scope)

### 1.1 核心目標 (Core Goal)
1. **完整專案複製與 100% 獨立性**：從 `D:\TRIO2026\` 複製所有相關原始碼 (`src/`)、方案檔 (`TRIO2026.sln`)、腳本、文件、工具、資料庫腳本、驅動/支援套件至目前專案 `d:\TRIOV2\` 中。**TRIOV2 系統所有呼叫、引用或執行的檔案必須全部來自於 `d:\TRIOV2\` 專案目錄下**，不依賴 `D:\TRIO2026\` 之任何絕對路徑（`D:\TRIO2026\` 僅作為備份與參考來源）。
2. **直式轉橫式螢幕 (Portrait to Landscape Conversion, 1920×1080)**：
   - 原 `TRIO2026` 採用直式螢幕設計（視窗基準尺寸為 `600×960`）。
   - 本次改版需將 `TRIOV2` 調整為 **`1920×1080`** 橫式螢幕設計。
   - 重新排版與優化所有頁面 (`Views/Pages`) 及覆蓋控制項 (`Controls`)，以符合 `1920×1080` 橫式顯示器的視覺與觸控操作體驗。

### 1.2 非目標 / 不在本次範圍 (Out of Scope)
- 不變更後端商業邏輯與核心服務邏輯流程 (`TRIO2026.Core`, `TRIO2026.Data`, `TRIO2026.PrivilegedService`)，但所有相依檔案均完整複製至 `d:\TRIOV2\` 專案中。
- 不修改與硬體/硬體驅動直接關聯的 API 協定，僅進行 UI 展示層 (XAML/View) 的轉置與佈局重構。

---

## 2. 變更影響評估 (Impact Analysis)

### 2.1 檔案複製與過濾清單
| 項目類別 | 來源/檔案路徑 (D:\TRIO2026) | 複製至目標路徑 (d:\TRIOV2) | 說明與過濾規則 |
| :--- | :--- | :--- | :--- |
| **原始碼** | `src/` | `src/` | 包含 `TRIO2026.App`, `TRIO2026.Core`, `TRIO2026.Data`, `TRIO2026.PrivilegedService`, `Tools` |
| **方案檔與設定** | `TRIO2026.sln`, `global.json`, `.env` | 同名檔案 | .NET Solution 與 SDK、環境配置檔 |
| **部署與打包腳本** | `Deploy_ASCII.ps1`, `Deploy_SelfContained.ps1`, `deploy_packager.py` | 同名檔案 | 自動化部署與打包腳本 |
| **資料庫與工具** | `Database/`, `Database_Backup/`, `Tools/` | 同名目錄 | 專案資料庫腳本、測試備份檔與相依工具組 |
| **文件與日誌範例**| `DocumentsAndInformation/`, `docs/`, `WorkLog/`, `Windows 11 IoT Enterprise LTSC/` | 同名目錄 | 專案規格說明文件與設定工具 |
| **排除項目** | `.git/`, `.vs/`, `bin/`, `obj/`, `publish_*/`, `tmp/` | *(不複製)* | 僅排除 Git 歷程、IDE 快取與舊編譯產出物 |

### 2.2 UI 橫式改版重點檔案 (`src/TRIO2026.App/`)
| 檔案路徑 | 調整類型 | 變更說明 (Description) |
| :--- | :--- | :--- |
| [AppShell.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/AppShell.xaml) | **[MODIFY]** | 將主框架 Border 的 `Width="600" Height="960"` 修改為 **`Width="1920" Height="1080"`** (Viewbox 基底) |
| [MenuPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/MenuPage.xaml) | **[MODIFY]** | 將直向選單按鈕配置調整為橫向 `1920×1080` 多欄/格狀網格佈局 |
| [LoginPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/LoginPage.xaml) | **[MODIFY]** | 將登入輸入框與觸控鍵盤佈局調整為左右雙欄橫向排列 |
| [AccountManagementPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/AccountManagementPage.xaml) | **[MODIFY]** | 調整帳號清單 DataGrid 與操作按鈕群組之橫向延伸與比例 |
| [DataListPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/DataListPage.xaml) | **[MODIFY]** | 數據清單與篩選器改為橫向佈局 |
| [DataDetailPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/DataDetailPage.xaml) | **[MODIFY]** | 詳細數據圖表與參數面板改為左右分欄顯示 |
| [UvDecontaminationPage.xaml](file:///d:/TRIOV2/src/TRIO2026.App/Views/Pages/UvDecontaminationPage.xaml) | **[MODIFY]** | 消毒進度計與控制按鈕改為橫向平行佈局 |
| `Controls/*.xaml` | **[MODIFY]** | 觸控鍵盤 (TouchKeyboardOverlay, NumericKeypadOverlay) 與懸浮對話框改為適應 1920 寬度之橫向尺寸 |

---

## 3. 具體實作步驟 (Step-by-Step Implementation Plan)

### 階段一：專案檔案複製與獨立化 (Phase 1: Project Copy & Standalone Setup)
- [ ] **Step 1.1**：將 `D:\TRIO2026\` 中除編譯產物外的所有檔案、工具、資料庫與文件複製至 `d:\TRIOV2\`。
- [ ] **Step 1.2**：檢查並確認 `d:\TRIOV2\` 內不包含任何指向 `D:\TRIO2026\` 的寫死絕對路徑。
- [ ] **Step 1.3**：進行基本編譯驗證，執行 `dotnet build` 確保專案在 `d:\TRIOV2\` 能無錯構建。

### 階段二：UI 視覺軸向與基底解析度調整 (Phase 2: Base Resolution & Frame Setup)
- [ ] **Step 2.1**：修改 `src/TRIO2026.App/Views/AppShell.xaml`，把主邊界解析度改為 **`1920×1080`**。
- [ ] **Step 2.2**：檢查並調整 `SplashWindow.xaml` 等啟動視窗尺寸，符合 1920x1080 橫式顯示。

### 階段三：頁面與控制項橫式佈局重構 (Phase 3: Pages & Controls Layout Redesign)
- [ ] **Step 3.1**：重構 `MenuPage.xaml` 主選單畫面，按鈕改為橫向格狀與列隊配置。
- [ ] **Step 3.2**：重構 `LoginPage.xaml` 登入頁與 `Controls/LoginOverlay.xaml`。
- [ ] **Step 3.3**：重構 `AccountManagementPage.xaml` 使用者管理介面。
- [ ] **Step 3.4**：重構 `DataListPage.xaml` 及 `DataDetailPage.xaml` 資料列表與詳情。
- [ ] **Step 3.5**：重構 `UvDecontaminationPage.xaml` 消毒流程頁面。
- [ ] **Step 3.6**：調校觸控鍵盤 (`TouchKeyboardOverlay`, `NumericKeypadOverlay`) 尺寸與按鈕間距，使其適合 1920x1080 橫向螢幕操作。

### 階段四：建置測試與介面驗證 (Phase 4: Build, Test & Verification)
- [ ] **Step 4.1**：執行專案完整建置 (`dotnet build`)，確認全數 C# 與 XAML 檔案無語法錯誤。
- [ ] **Step 4.2**：啟動應用程式，驗證各頁面在 `1920×1080` 及 Viewbox 等比自動縮放下的畫面顯示狀況。

---

## 4. 風險評估與邊界條件 (Risks & Mitigation)

| 潛在風險 / 邊界情況 | 影響程度 | 應對與補救措施 (Mitigation) |
| :--- | :---: | :--- |
| **複製遺漏相依工具或模組** | 中 | 複製後全盤搜尋專案檔與組態，確保無缺漏並可獨立運作 |
| **直轉橫 (1920x1080) 後元件重疊或過度拉伸** | 高 | 善用 WPF `Grid` 的 Star/Auto 列寬與 `ScrollViewer` 區域保護，避免固定像素死板設定 |
| **觸控鍵盤遮蔽輸入框** | 高 | 將鍵盤 Overlay 及彈窗設計為懸浮底欄或動態開關，確保輸入欄位可見 |

---

## 5. 驗證與測試計畫 (Verification Plan)

### 5.1 自動化測試 / 建置驗證 (Automated Verification)
- **命令**：`dotnet build d:\TRIOV2\TRIO2026.sln --configuration Debug`
- **目標**：確認全專案 0 Error、0 Breaking change。

### 5.2 手動驗證清單 (Manual Inspection)
- [ ] 確認 `d:\TRIOV2` 專案目錄擁有完整的原始碼、工具、資料庫與專案結構。
- [ ] 執行應用程式後，確認視窗呈現橫式 `1920×1080`（或依比例全螢幕展延）無變形。
- [ ] 逐一切換所有 Pages (`InitPage`, `MenuPage`, `LoginPage`, `AccountManagementPage`, `DataListPage`, `DataDetailPage`, `UvDecontaminationPage`)，確認無元件重疊或切割現象。
- [ ] 觸發虛擬鍵盤與彈窗對話框，確認放置與大小與橫式 1920x1080 畫面完美匹配。

---

## 6. 審閱意見記錄 (Review Feedback)

> **審閱者備註區**：
> - [x] *使用者指定：螢幕尺寸更改為 1920x1080 作為設計。*
> - [x] *使用者指定：所有相依檔案/資料夾皆複製到 d:\TRIOV2，確保完全獨立運作，D:\TRIO2026 僅作為參考備份。*
