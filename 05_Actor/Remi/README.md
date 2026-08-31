# Actor / Remi

Remi 角色组件（从 `Remi/She` 迁入）与相关资源。

| 内容 | 职责 |
|------|------|
| `Remi` | 角色根：表情、本地兜底台词等 |
| `RemiInteraction` | 面对面 / 对话入口与回合编排 |
| `RemiResponseTextLayout` | 头顶 Response 布局 |
| `RemiResetExpressionOnExit` | 离开交互时复位表情 |
| `Social/` | 朋友圈 Moments |
| `*.controller` | Remi / Ema Animator Controller |

类名未改；GUID 随 `.meta` 保留。世界规则见 `../../01_GameSystem/`；台词通道见 `../../02_Voice/`。
