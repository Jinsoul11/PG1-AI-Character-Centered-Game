# 02_Voice / Persona

静态角色设定写入 Prompt。

| 脚本 | 职责 |
|------|------|
| `RemiCharacterPrompt` | `[CHARACTER]`：固定 identity / background_public + 阶段 personal seeds |
| `RemiBiographySeedsPolicy` | 随 `RemiDialogueDepthStage` 解锁的 biography_seeds_personal |

不再按 `CharacterType` 切换预设性格；遗留模板见 `../../99_Archived/Persona/`。
