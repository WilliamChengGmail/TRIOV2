---
trigger: always_on
---
# Artifact Output Location & Naming Rules

當你需要生成計畫檔、任務檔或導覽檔時，必須將檔案強制儲存於專案的 `docs/agent/` 目錄下。
目錄如果不存在，請先建立該目錄，**嚴禁直接放在專案根目錄**。

為了避免檔案被覆蓋並留存紀錄，所有產出檔案的檔名均**必須加入當前時間格式 (`YYYYMMDDHHmm`)**：

- **實作計畫**：`docs/agent/implementation_plan_{YYYYMMDDHHmm}.md`
  *(範例：`docs/agent/implementation_plan_202608121410.md`)*
- **執行導覽**：`docs/agent/walkthrough_{YYYYMMDDHHmm}.md`
  *(範例：`docs/agent/walkthrough_202608121410.md`)*
- **任務拆解**：`docs/agent/task_{YYYYMMDDHHmm}.md`
  *(範例：`docs/agent/task_202608121410.md`)*