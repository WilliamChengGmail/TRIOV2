# 實作計畫：[簡短描述任務名稱，例如：新增 Google OAuth2 登入功能]

> **狀態**：`[ ] 待審閱 (Pending Review)` | `[ ] 已核可 (Approved)` | `[ ] 進行中 (In Progress)` | `[ ] 已完成 (Completed)`  
> **建立時間**：YYYY-MM-DD  
> **負責 AI / 開發者**：Office of William  
> **相關 Task / Issue**：#123  

---

## 1. 任務目標與範疇 (Goal & Scope)

### 1.1 核心目標 (Core Goal)
- 簡要說明本次修改要解決的問題或實現的新需求。

### 1.2 非目標 / 不在本次範圍 (Out of Scope)
- 明確列出本次**不處理**的項目，避免需求蔓延 (Scope Creep)。

---

## 2. 變更影響評估 (Impact Analysis)

### 2.1 檔案異動清單 (Proposed Changes)
| 操作類型 | 檔案路徑 (File Path) | 變更說明 (Description) |
| :--- | :--- | :--- |
| **新增** | `src/auth/oauth2.ts` | 處理 OAuth2 驗證邏輯與 Token 交換 |
| **修改** | `src/routes/user.ts` | 新增 `/auth/google/callback` 路由 |
| **修改** | `src/config/env.ts` | 新增 `GOOGLE_CLIENT_ID` 等環境變數配置 |
| **刪除** | `src/legacy/old_auth.ts` | 清理舊版驗證邏輯 |

### 2.2 API / 資料庫架構變更 (Schema & API Changes)
* **API 變更**：
  * `POST /api/v1/auth/google` (新增) - 傳入 `id_token` 回傳應用程式 JWT。
* **資料庫變更**：
  * `users` 資料表新增 `google_id` (VARCHAR, NULLABLE) 與 `avatar_url` 欄位。

### 2.3 目錄與檔案放置規則 (Directory & File Placement Rules)

> 本專案對「工具」相關檔案有明確的目錄慣例，撰寫 2.1 檔案異動清單時請依此規則填寫路徑。
> 詳細規則請參閱 `.agents\rules\tools_directory.md`。

| 類型 | 放置位置 | 說明 |
| :--- | :--- | :--- |
| **自研工具原始碼 / 專案** | `src\Tools\<工具名稱>\` | 自行開發的工具（例如：效能模擬器），其專案原始碼一律放在 `src\Tools\` 之下，與一般 `src` 模組一同版控管理。 |
| **可執行檔 / 相依檔案 / 執行用資料夾** | `Tools\` | 建置後產生的執行檔、執行所需的相依檔案（DLL、設定檔、資源等）或資料夾，統一放在專案根目錄的 `Tools\`，供直接執行使用。 |

**規則重點：**

- `src\Tools\`：**開發階段**的原始碼，受版控管理。
- `Tools\`：**建置階段**的執行產物（角色等同一般專案的 `bin\`）。當建置/輸出未指定路徑時，一律輸出到 `Tools\`，不要散落在專案其他目錄。
- 2.1 檔案異動清單的「檔案路徑」欄位，必須符合上表規則。

**範例：**

| 操作類型 | 檔案路徑 | 變更說明 |
| :--- | :--- | :--- |
| 新增 | `src\Tools\PerfSimulator\main.py` | 自研「效能模擬器」原始碼專案 |
| 新增 | `Tools\PerfSimulator.exe` | 效能模擬器建置後的執行檔（輸出未指定時，依規則放至 `Tools\`） |

### 2.4 Git 操作與 Commit 規範 (Git & Commit Rules)

> 執行本計畫時，所有 Git 操作與 Commit 訊息必須遵守下列規範。
> 詳細規則請參閱 `.agents\rules\git_commit_rules.md`。

**操作權限規範：**

- 僅在**使用者明確指示**（主動通知）時，才可執行 `git add`、`git commit`、`git push` 等寫入操作。
- 使用者未指示前，AI 不得自行 commit；可先備妥「變更檔案清單」與「Commit 訊息草稿」供使用者確認。

**Commit 訊息規範：**

- Commit 訊息必須為**完整的中文變更說明**，說明本次做了什麼、為什麼做。
- 必須**適度排版（多行）**，禁止將所有說明擠在同一行。

**格式範例：**

```text
feat(auth): 新增 Google OAuth2 登入流程

變更內容：
- 新增 src/auth/oauth2.ts：OAuth2 驗證與 Token 交換邏輯
- 修改 src/routes/user.ts：新增 /auth/google/callback 路由
- 修改 src/config/env.ts：新增 GOOGLE_CLIENT_ID 環境變數

驗證方式：
- 單元測試：npm run test src/auth/oauth2.test.ts
```

**不良範例（禁止）：**

```text
fix: 修登入功能還改了路由和env設定
```

---

## 3. 具體實作步驟 (Step-by-Step Implementation)

### 階段一：環境配置與資料層準備 (Phase 1: Setup & DB)
* [ ] **Step 1.1**：更新 `.env.example` 並定義環境變數型態檢查。
* [ ] **Step 1.2**：撰寫 DB Migration 腳本（新增 `google_id` 欄位）。

### 階段二：核心邏輯與 API 開發 (Phase 2: Core Logic)
* [ ] **Step 2.1**：實作 `GoogleAuthService`，整合 Google API Client。
* [ ] **Step 2.2**：在 `UserController` 新增第三方登入回呼 (Callback) 處理。
* [ ] **Step 2.3**：更新 JWT 生成邏輯，支援第三方使用者綁定。

### 階段三：前端/調用端整合 (Phase 3: Integration)
* [ ] **Step 3.1**：於前端登入頁面新增 Google 登入按鈕元件。
* [ ] **Step 3.2**：處理登入成功後的狀態儲存與頁面重導向。

---

## 4. 風險評估與邊界條件 (Risks & Edge Cases)

| 潛在風險 / 邊界情況 | 影響程度 | 應對與補救措施 (Mitigation) |
| :--- | :---: | :--- |
| **Email 衝突**：使用者已有帳密帳號，又用同 Email 的 Google 登入 | 中 | 自動進行帳號綁定，並發送安全通知信件 |
| **Google API 服務中斷** | 高 | 捕獲 API 異常並回傳友善錯誤提示（如 `503 Service Unavailable`） |
| **Token 洩漏風險** | 高 | 嚴禁在 Log 中印出 `id_token` 或 `access_token` 明文 |

---

## 5. 驗證與測試計畫 (Verification Plan)

### 5.1 自動化測試 (Automated Tests)
* [ ] **單元測試 (Unit Tests)**：`npm run test src/auth/oauth2.test.ts`
* [ ] **整合測試 (Integration Tests)**：模擬 OAuth2 回呼流程，確認正確回傳 JWT。

### 5.2 手動驗證清單 (Manual Check)
- [ ] 點擊 Google 登入按鈕能正確跳出授權視窗。
- [ ] 新使用者透過 Google 登入後，資料庫成功建立記錄。
- [ ] 舊使用者透過 Google 登入後，成功綁定 `google_id`。

---

## 6. 審閱意見記錄 (Review Feedback)

> **審閱者備註區**：（審閱人在批覆或提出修正建議時可寫在此處）
> - [ ] *範例：請在 Step 2.1 補充 Redis 快取 Token 的機制。*