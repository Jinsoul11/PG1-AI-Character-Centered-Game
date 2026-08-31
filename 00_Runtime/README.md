# 00_Runtime

引擎胶水：玩家、UI 壳、场景旅行（**非 PG 语义**）。

| 子目录 | 职责 |
|--------|------|
| `Player/` | `PlayerController`、移动锁 |
| `Travel/` | 跨场景传送门、落点、过场遮罩 |
| `UI/` | `UiManager` + 各 Panel |

剧情演出壳见 `../06_Story_PG1/Playback/`。  
PG 语义（Voice / Intent / Presence）不放这里。
