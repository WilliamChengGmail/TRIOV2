D:\TRIOV2\
├── Tools\                        <-- 純執行環境：可執行檔、二進位目錄與 .bat
│   ├── DevLauncher_bin\         (內含 TRIOV2.DevLauncher.exe)
│   ├── Simulator_bin\           (內含 TRIOV2.Simulator.exe)
│   ├── DeployStaging\           (發佈目錄)
│   ├── 啟動模擬器.bat
│   ├── 啟動硬體模擬器.bat
│   ├── 啟動特權服務.bat
│   ├── AccountTool.bat
│   ├── EventLogViewer.bat
│   ├── RunUsbConfigTool.bat
│   ├── 查看UUID.bat
│   ├── 測試IPC連線.bat
│   └── 部署到隨身碟.bat
│
└── src\Tools\                    <-- 專案原始碼開發目錄（所有 tool 專案原始碼）
    ├── AccountTool\
    ├── DbInitializer\
    ├── DbMigrate\
    ├── DevLauncher\
    ├── PerformanceAnalysisO\
    ├── QueryEvents\
    ├── ServiceTestTool\
    ├── TRIOV2.DbIoSimulator\
    ├── TRIOV2.PerformanceAnalyzer\
    ├── TRIOV2.ResourceSimulator\
    ├── TRIOV2.Simulator\
    ├── UsbConfigTool\
    └── UuidTool\