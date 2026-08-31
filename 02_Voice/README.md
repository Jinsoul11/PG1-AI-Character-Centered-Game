# Voice

**Actor 通道：她怎么说（自然语言）。**

对应宪章：Actor = Voice。  
产出台词、组装 Prompt、Persona、检索上下文、SendSystem 脚本台词、LLM 传输与呈现节奏。

| 子目录 | 职责 |
|--------|------|
| `Agent/` | `PromptedDialogueAgent` 等对话编排入口 |
| `Prompt/` | Prompt 组装模式、通道、强调、载荷类型 |
| `Context/` | 回合上下文（`PromptContextManager`） |
| `Persona/` | 角色设定 Prompt |
| `WorldSnapshot/` | 只读世界快照写入 Prompt（STATE / DAY_PLAN 等） |
| `Retrieval/` | 主动记忆 / Knowledge 检索 |
| `Policy/` | 对话策略与话题范围（给 Voice 用的约束描述） |
| `SendSystem/` | 框架驱动的固定/半固定台词投递 |
| `Transport/` | LLM 传输（如 DeepSeekDialogueManager；TTS 见 `../99_Archived/TTS`） |
| `Presentation/` | 面对面 / 手机文字呈现模式（`DialogueSequenceDirector`） |

**不放这里：** 改世界状态 → `../01_GameSystem/`；Intent JSON → `../03_Intent/`；角色组件 → `../05_Actor/`；玩家 / 传送 / 面板 → `../00_Runtime/`。
