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