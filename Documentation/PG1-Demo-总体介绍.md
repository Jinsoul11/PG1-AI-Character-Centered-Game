# PG1 Demo 总体介绍

本文说明 **Off the Track · PG1 Demo** 跑通了什么、玩家经历怎样的三日流程，以及工程如何初步落地 `Design/` 与本文档夹中的设想。  
细节实现以代码与 `Design/` 宪章为准；本文偏「读完能串起来」。

---

## 1. Demo 要证明什么

PG1 的最小证明（见 `Design/01-PG-Core-Principles.md`）：

> 在一个有限的游戏结构中，AI 角色能否让玩家感到「我正在和她共同经历一段关系」——角色连续存在，玩家的参与留下真实痕迹。

因此 Demo **不追求**开放大世界或完全自主剧情，而追求一条可复述的闭环：

**关系阶段 → 进入她的轨道 → 一次合法偏离 → 记忆留下痕迹。**

核心设计句：

> **固定结构，开放表现** —— GameSystem 保证「一定会发生什么」；Voice / Intent 负责「她今天怎么看、怎么说」。

关 AI 时，固定台词与 SendSystem fallback 仍能走通三日锚点；开 AI 时，态度与措辞可变，结构结果不变。

---

## 2. 总体结构：三日锚点 + 终幕

| 阶段 | GameSystem 承诺（结构） | 关系权限（体验） |
|------|-------------------------|------------------|
| **Day1 委托** | 一定出现并完成一次协作找书 | 进入她的**事务** |
| **Day2 共现** | 一定给玩家进入她下午生活轨道的机会 | 进入她的**生活** |
| **Day3 偏离** | 一定发生一次合法的真轨道改写 | 参与一次**改变她的安排** |
| **Ending** | 共同经历回顾 +（可选）个人印象 Bond | 「我们一起经历过」可被说出 |

脊柱进度由 `RemiDemoSpineBeat` 线性推进（节选）：

`NotStarted → Day1BookSubmitted → Day1Complete → Day2InviteDelivered → Day2LibraryIntroDone → Day2Complete → Day3InviteReady → Day3DeviationAccepted → Day3ApartmentIntroDone → Day3Complete → DemoFinale`

总导演入口：`06_Story_PG1/Spine/Director/RemiDemoSpineDirector.cs`。

内容包分层（`06_Story_PG1/README.md`）：

| 目录 | 职责 |
|------|------|
| `Spine/` | 必经主线锚点（关 AI 也要能走通） |
| `Optional/` | 非必经共创空间（扩展优先放这里） |
| `Playback/` | 演出壳（StoryDirector、黑屏、Hint） |

---

## 3. 玩家流程（按日）

### Day1 · 教室 · 委托（Commission）

1. 教室开场 → 面对面窗口打开（可聊、可「回头见」、超时/轮次有保底）。
2. Remi 提出找作品展参考书《AI游戏入门》（Voice / SendSystem，失败有固定句）。
3. 玩家在教室世界找到书 → 检视拾取 → 交给 Remi → 致谢。
4. 过场：黑屏 + 图书馆一瞥（她回馆继续自己的事）→ 切到 **Day2 教室**（Remi 不在场）→ 手机送达图书馆邀约 → 写入 **Day2 起点档**。

共同经历写入：`day1_commission_book`（框架事实，非 LLM 编造）。

主文件：`Spine/Day1/RemiBookQuestFlow.cs`。

---

### Day2 · 教室 → 图书馆 · 共现（CoPresence）

1. 教室：读手机邀约；可简短回复后锁输入；引导出门前往图书馆。
2. 图书馆状态机大致为：  
   **Window → Anchor 短开场（固定 Remi 短剧）→ FreeChat → Studying（巡逻 + Whisper）→ Farewell**。
3. Anchor / 共现成立后登记共同经历与故事锚点。
4. 离馆收束：告别机位（如需）→ Remi 回公寓一瞥 → **Day3 教室** → 偏离窗口准备 + Day3 手机 nudge → **Day3 起点档**。

共同经历写入：`day2_library_co_presence`。

主文件：`Spine/Day2/RemiLibraryDay2CoPresenceFlow.cs`、`RemiLibraryDay2CoPresenceStory.cs`。

---

### Day3 · 偏离窗口 → 公寓共在（Deviation）

偏离不是「去公寓按钮」，而是关系权限：

> 我可以参与一次对她安排的改写。

**窗口**由 GameSystem 打开/关闭；手机与当面是**同一事件的双入口**。

进入方式（结果固定，入口开放）：

| 入口 | 做法 |
|------|------|
| 自由输入 | Intent：`RemiDeviationDetectIntent` 判断是否在提偏离 → 校验通过 → Voice 表演接受 |
| 固定 Chip | 「今晚方便来宿舍聊聊吗？」类确认 |
| 保底 | Remi **主动提案** + Chip「那走吧」；**不伪造玩家发言**；关闭自由输入后只确认 |

采纳后：日程偏离到宿舍 → 黑屏过场进公寓 → 固定公寓 intro（Remi ↔ 玩家轮替）→ **当面自由聊** → 门口离开触发终幕。

共同经历写入：`day3_dorm_deviation`。

主文件：`RemiDemoSpineDirector`（窗口 / 保底 / 采纳）、`Spine/Day3/RemiApartmentDay3CoPresenceStory.cs`。  
原则：`Design/04-Day3-Deviation-Window.md`。

---

### Ending · 共同经历回顾 + Bond

1. 开场固定句（玩家转头看 Remi）。
2. 按已登记的 **Shared Experience** 翻页：切到对应场景机位 + EndingSpeak 生成 1～2 句回忆（失败用 Catalog 兜底句）。
3. 回公寓 `InStory` 站位，全程 StoryPanel。
4. （可选）从 Fragment Memory 精选印象，生成一段对玩家的印象画像（Bond）；无合格印象则跳过。
5. 固定收束台词 + 黑屏旁白 → `DemoFinale`。

主文件：`Spine/Ending/RemiDemoMemoryRecapEndingFlow.cs`、`04_Memory/Demo/RemiEndingSpeakPrompt.cs`。

---

## 4. 设想如何被初步实现

下面按设想条目对照 Demo 落地，对应 `Design/` 与共同创作方向。

### 4.1 固定结构 × 开放表现

| 层 | 工程对应 | Demo 中的角色 |
|----|----------|----------------|
| **GameSystem** | `01_GameSystem/Presence/` | 世界时间、日程、锚点、偏离窗口、共位、执行传送与改轨 |
| **Intent** | `02_Voice` 内 Intent 通道 / Detect | 结构化判断（如偏离检测、表情）；**不能**直接改世界 |
| **Voice** | `PromptedDialogueAgent`、SendSystem、LLM | 自然语言台词与态度表现 |

原则落地句：

> AI 提出可能性；GameSystem 决定可能性能否成为现实。  
> 不要求 AI 100% 可靠；要求 AI 的不可靠性不会破坏游戏的可靠性。

三日锚点「一定发生」；「她怎么说、怎么看你」优先交给 Voice / Intent，失败走固定 fallback。

---

### 4.2 双通道：当面与手机

- **当面**：共位时 `DialoguePanel` + `RemiInteraction`（F）。
- **手机**：分离时 `PhoneAppPanel`（邀约、nudge、偏离提案、朋友圈）。
- **互斥**：`RemiInteractionChannelPolicy` —— 同场景优先面对面；分离才允许手机打字，避免双通道同时写乱上下文。
- Day3 偏离刻意做成**双入口、单事件**，避免「手机线 / 当面线」两套剧情。

---

### 4.3 双层记忆：共同经历 + 碎片印象

这是「共同创作」在 Demo 里最硬的落点：

| 层 | 存什么 | 谁写 | Ending 怎么用 |
|----|--------|------|----------------|
| **Shared Experience** | 三日框架事实（找书 / 共自习 / 宿舍偏离） | 仅框架事件写入，LLM 不可编造 | 回顾页**人人相同的骨架** |
| **Fragment Memory** | 闲聊沉淀出的印象摘要（权重、话题别名等） | Archive → Curator → Unit → Analyzer → FM | Bond 页**每局不同的痕迹** |

流水线与日结：`04_Memory/Fragment/`、`RemiMemoryDaySettlement`。  
共同经历目录：`04_Memory/Shared/RemiSharedExperienceCatalog.cs`。

玩家带走的理想感受：

> 「她本来有自己的安排……但因为我，今天没有按原计划耗下去。」  
> 以及：「我们一起做过的几件事，她还记得；而聊天里留下的印象，也变成她嘴里的一句。」

而不是：「我解锁了偏离功能。」

---

### 4.4 在场与生活轨道（Presence）

`RemiPresenceService` 维护叙事时钟、地点、活动、日块（Routine / Window / Anchor / Return）、故事锚点与 episode（委托 / 共现 / 偏离会话）。

- Day1：委托钉在教室，保证找书期共位与面对面可用。  
- Day2：共现占格，玩家进入「她下午在图书馆」的轨道。  
- Day3：`ApplyScheduleOverride` → `DeviationSession`，日程可见地改到宿舍；自由聊结束后偏离不因「回头见」提前清掉（收束交给门口 Ending）。

物理在场服务于关系权限，而不是为展示传送技术本身。

---

### 4.5 自由输入进入连续性

自由输入的价值不在「能随便说话」，而在能进入角色与世界的连续性：

- 闲聊进入 Archive / Fragment，日后可召回、可进 Ending Bond。  
- Day3 自由输入可被 Intent 识别为「合法偏离提案」，经 GameSystem 校验后改轨。  
- 保底路径保证锚点成立，但不伪造「玩家说过的话」。

---

### 4.6 进度与存档（实验层）

- **Day2 / Day3 起点档**：`RemiDemoDaySaveService` —— 读档可回到当日可玩起点，并尽量保留当面 Conversation1 与 LLM history。  
- **Dialogue Archive** 跨天保留（新游戏才清），服务记忆管线，不随 UI 清历史而消失。  
- Spine / 流程 Prefs、共同经历、Fragment 管线 JSON、手机会话与朋友圈状态落在 `persistentDataPath`。

Demo 存档是实验便利，不是最终产品存档方案。

---

## 5. 工程地图（从哪读起）

| 关注点 | 入口 |
|--------|------|
| 设计原则 | `GameScripts/Design/` |
| 三日故事内容 | `GameScripts/06_Story_PG1/` |
| 在场 / 通道 / 日程 | `GameScripts/01_GameSystem/Presence/` |
| 对话与 Prompt | `GameScripts/02_Voice/` |
| 共同经历 + Fragment | `GameScripts/04_Memory/` |
| Remi 交互入口 | `GameScripts/05_Actor/Remi/` |
| UI / 旅行 / 玩家壳 | `GameScripts/00_Runtime/` |

---

## 6. 当前边界（诚实说明）

Demo 已初步验证的：

- 三日结构性承诺可走通（含关 AI fallback）。  
- 当面 / 手机双通道与共位互斥。  
- Shared Experience 固定骨架 + Fragment 可变痕迹的 Ending。  
- Day3「结果固定、入口开放、不伪造玩家发言」的偏离窗口。

仍属实验 / 未完全按宪章铺开的：

- Intent 族多数仍偏窄（如表情）；「应开 Intent」的态度判断尚未全部加宽（见 `Design/03`）。  
- `Optional/` 共创事件空间基本预留。  
- 公寓 intro 等大量仍为固定台词；部分 Voice 机会尚未全部打开。  
- 存档、结算时机、召回质量仍在打磨。

这些不影响 Demo 作为 **「有限结构里能否感到共同经历」** 的最小证明，但说明它离完整 PG 产品形态还有距离。

---

## 7. 一句话收束

**PG1 Demo** 用 GameSystem 钉死三日关系权限的升级（事务 → 生活 → 改写安排），用 Voice / Intent 填态度与措辞，用「共同经历 + 碎片印象」把玩家痕迹写进终幕——从而初步实现「固定结构，开放表现」与「我和她之间发生了什么」可被记住的设想。
