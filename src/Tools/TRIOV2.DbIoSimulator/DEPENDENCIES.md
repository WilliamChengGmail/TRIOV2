# DbIoSimulator 相依清單

- **工具名稱**：TRIO V2 底層資料庫 I/O 模擬器 (TRIOV2.DbIoSimulator)
- **維護單位**：Office of William
- **發佈模式**：Self-Contained (自包含獨立運行，免安裝 .NET Runtime / .NET Desktop Runtime)
- **入口**：`啟動資料庫IO模擬器.bat` 或 `DbIoSimulator\TRIOV2.DbIoSimulator.exe`
- **專屬相依**：
  - `DbIoSimulator\Microsoft.Data.Sqlite.dll`
  - `DbIoSimulator\e_sqlite3.dll`
  - `DbIoSimulator\coreclr.dll`
  - `DbIoSimulator\PresentationCore.dll`
  - `DbIoSimulator\PresentationFramework.dll`
  - `DbIoSimulator\WindowsBase.dll`
  - `DbIoSimulator\wpfgfx_cor3.dll`
- **共用相依**：無
- **最小複製集合**：
  - `啟動資料庫IO模擬器.bat`
  - `DbIoSimulator\`
- **結構要求**：`啟動資料庫IO模擬器.bat` 必須與 `DbIoSimulator\` 資料夾置於同一層。

