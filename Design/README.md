# Design

本目录存放 **Off the Track / PG** 已确认的设计原则与宪章类文档（非实现说明、非排期）。

工程目录对齐（见 `../README.md`）：

| 宪章用语 | 工程通道 / 目录 |
|----------|----------------|
| Actor（演员） | **Voice** → `../02_Voice/`；角色组件 → `../05_Actor/` |
| Director（导演意图） | **Intent** → `../03_Intent/` |
| GameSystem | 世界规则 → `../01_GameSystem/` |

**PG1 Demo 故事内容**见：`../06_Story_PG1/`（`Spine/` 主线锚点 · `Optional/` 共创 · `Playback/` 演出壳）。  
双层记忆：`../04_Memory/`。  
运行时壳：`../00_Runtime/`。  
停用包：`../99_Archived/`。

## 文档索引

| 文件 | 内容 |
|------|------|
| [01-PG-Core-Principles.md](./01-PG-Core-Principles.md) | 双路径、从属关系、PG1 最小证明 |
| [02-Voice-Intent-GameSystem.md](./02-Voice-Intent-GameSystem.md) | Actor/Director 与 Voice/Intent 对齐；认知转变 |
| [03-Anchors-and-Director.md](./03-Anchors-and-Director.md) | 锚点=结构承诺；三日「何处应开 Intent」 |
| [04-Day3-Deviation-Window.md](./04-Day3-Deviation-Window.md) | 第三阶段偏离窗口（已修订） |

## 使用约定

- 改玩法或加 Intent 任务前，先对照本目录相关条目。
- 实现细节写在代码与注释；本目录只保留**可引用的原则**。
- 新增原则时：先讨论确认，再写入此处，并更新本索引。
