# Third-Party Assets

本仓库（尤其是 `Assets/Resourse/`，拼写为 Resourse）包含用于 **PG1 Demo 演示** 的第三方资源。

**这些资源不在 PG1 代码许可（MIT）或设计文档许可（CC BY 4.0）覆盖范围内。**  
每项资源仍受其**原始许可 / 购买协议**约束；公开分发或再授权前，请自行核对最新条款。

> **重要：** Unity Asset Store 资源通常受 [Unity Asset Store EULA](https://unity.com/legal/as-terms) 约束。  
> 一般允许在你的游戏产品中使用，但**通常不允许**把源资源作为独立内容放入公开仓库供他人随意下载。  
> 若计划公开完整工程，请先确认各包是否允许随仓库分发，必要时将 Asset Store 内容排除出公开范围。

路径均相对于仓库中的 `Assets/Resourse/`。

---

## Inventory（`Assets/Resourse/`）

| Asset / Path | Source (as identified) | License | Attribution / Notes |
|---|---|---|---|
| `JP_School classroom_V2/` | [Japanese School Classroom](https://assetstore.unity.com/packages/3d/environments/japanese-school-classroom-18392) by **SbbUtutuya** | Unity Asset Store EULA (Single Entity) | See package `README.txt`. Contact: sbbututuya@gmail.com |
| `JP_Apartment/` | [Japanese Apartment](https://assetstore.unity.com/) by **SbbUtutuya** (package README) | Unity Asset Store EULA (expected) | See package `README.txt`. Contact: sbbututuya@gmail.com |
| `JP_School library/` | Likely [Japanese School Library Set](https://assetstore.unity.com/packages/3d/props/interior/japanese-school-library-set-41667) by **SbbUtutuya** | Unity Asset Store EULA (expected) | No in-folder README; confirm purchase page before redistribution |
| `Suriyun/` (character **Ai**, anims, emotions, UI) | [Ai](https://assetstore.unity.com/packages/3d/characters/humanoids/humans/ai-80561) by **SURIYUN** | Unity Asset Store EULA (Single Entity) | Used as Remi presentation mesh in Demo. See `Suriyun/README.txt`. Site: www.suriyun.com |
| `Suriyun/UnityChanShader/` | Unity-Chan Toon Shader (bundled with Suriyun Ai) | **Unity-Chan License (UCL)** © UTJ/UCL | See `Suriyun/ShaderLicense.txt` and http://unity-chan.com/contents/guideline_en/ |
| `Ema/Characters/Mia/` | Appears to be **Mia Lock** (or related) character pack from Asset Store | Unity Asset Store EULA (expected) | No LICENSE in folder; verify exact package SKU and EULA before open redistribution |
| `Font/SourceHanSansCN/` | [Source Han Sans](https://github.com/adobe-fonts/source-han-sans) (Adobe) | **SIL Open Font License 1.1** | Full text: `Font/SourceHanSansCN/LICENSE.txt`. Reserved Font Name “Source” |
| `Tool/TextMesh Pro/` | Unity TextMesh Pro package samples / fonts | Mixed (see below) | Bundled TMP resources for UI text |
| `Tool/TextMesh Pro/Fonts/LiberationSans*` | Liberation Sans | **SIL Open Font License 1.1** | See `LiberationSans - OFL.txt` |
| `Tool/TextMesh Pro/Sprites/` (EmojiOne sample) | [EmojiOne](https://www.emojione.com/) | See EmojiOne / JoyPixels terms | See `EmojiOne Attribution.txt`; review current licensing before commercial use |
| `Scripts/LitJson/` | [LitJSON](https://github.com/LitJSON/litjson) | Authors disclaim copyright (public-domain style); see upstream COPYING | Header refers to COPYING; that file is **not** present in this tree — restore from upstream if redistributing |
| `Scripts/Json/` (`JsonMgr.cs` etc.) | Project utility wrappers | Treat as **project code** (MIT) unless noted otherwise | Not a third-party art asset |
| `VisualNovelDialogueGUI_PNG/` | Dialogue / choice UI PNG set | **Unknown / TODO** | No LICENSE or README in folder; do not assume MIT/CC BY |

---

## License notes by category

### 1. Environment packs (SbbUtutuya)

Classroom / Apartment / Library 场景与道具来自同一类 Asset Store 环境包。  
包内 `README.txt` 标明作者与联系方式，**不**等于可开放再分发。  
公开仓库前建议：`.gitignore` 排除这些目录，或仅私有分发完整工程。

### 2. Characters (Suriyun Ai / Ema·Mia)

- **Remi** 的视觉模型来自 Suriyun **Ai** 包 + Unity-Chan Shader（UCL）。  
- **Ema** 使用 `Ema/Characters/Mia/` 角色资源；许可需按购买记录核对。  
角色命名（Remi / Ema）属于项目设定；**模型文件本身**仍属第三方。

### 3. Fonts

Source Han Sans CN 与 Liberation Sans 均为 **SIL OFL 1.1**：可嵌入与随软件分发，但字体本身不得单独出售，且须保留 OFL 与保留字体名限制。

### 4. Tools / libraries

- TextMesh Pro：随 Unity 提供；其中第三方字体/表情样本见上表。  
- LitJSON：上游为宽松/公共领域式声明；请补齐 COPYING 并遵守原文。  
- `JsonMgr` 等：按项目 MIT 代码处理。

### 5. UI PNGs (`VisualNovelDialogueGUI_PNG`)

目前**未找到**明确许可文件。在查清来源前：

- 不要写入「本仓库全部资源 MIT/CC BY」；  
- 公开分发前应补全来源与授权，或移除/替换。

---

## How to update this file

新增或替换 `Resourse` 内资源时，请同步更新本表，至少填写：

1. 路径  
2. 来源 URL / 作者  
3. 许可名称  
4. 署名要求或限制  

许可不明时，写明 **`TODO — License unknown`**，在澄清前不要对外宣称可自由再分发。

---

## Related project licenses

| Layer | File |
|-------|------|
| Code | [`LICENSE`](./LICENSE) (MIT) |
| Design / written docs | [`LICENSE-DOCUMENTATION.md`](./LICENSE-DOCUMENTATION.md) (CC BY 4.0) |
| Third-party assets | **This file** |
