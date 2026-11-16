---
trigger: always_on
---
# Git 操作與 Commit 規範 (Git & Commit Rules)

所有 Git 操作與 Commit 訊息必須遵守下列規範：

## 操作權限規範

- 僅在**使用者明確指示（主動通知）**時，才可執行 `git add`、`git commit`、`git push` 等寫入操作。
- 使用者未指示前，AI 不得自行 commit；可先備妥「變更檔案清單」與「Commit 訊息草稿」供使用者確認。

## Commit 訊息規範

- Commit 訊息必須為**完整的中文變更說明**，說明本次做了什麼、為什麼做。
- 必須**適度排版（多行）**，禁止將所有說明擠在同一行。

## 格式範例

```text
feat(auth): 新增 Google OAuth2 登入流程

變更內容：
- 新增 src/auth/oauth2.ts：OAuth2 驗證與 Token 交換邏輯
- 修改 src/routes/user.ts：新增 /auth/google/callback 路由
- 修改 src/config/env.ts：新增 GOOGLE_CLIENT_ID 環境變數

驗證方式：
- 單元測試：npm run test src/auth/oauth2.test.ts
```

## 不良範例（禁止）

```text
fix: 修登入功能還改了路由和env設定
```
