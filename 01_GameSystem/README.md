# GameSystem

**世界规则层：改状态、管节奏、管通道，不负责台词措辞。**

对应宪章：GameSystem。Voice / Intent 产出自然语言与结构化意图；是否允许、如何落地由本目录执行。

| 子目录 | 职责 |
|--------|------|
| `Presence/` | Remi 在场、日程块、通道策略、节奏门与委托进度 |
| `Environment/` | 时段光照、RoleCanvas 世界 UI、Billboard |

**不放这里：** 台词与 Prompt → `../02_Voice/`；偏离 JSON Intent → `../03_Intent/`；角色组件与交互入口 → `../05_Actor/Remi/`；Demo Day1 找书 → `../06_Story_PG1/Spine/Day1/`。
