using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prompt 动态内容管理器：按 <see cref="RemiPromptTurnKind"/> 编排。
/// Voice（Player / System）：RELATIONSHIP + CURRENT_CONTEXT + ACTIVE_*；System 另附 [TURN]。
/// Intent：不拼世界块（CONTRACT 仅由 Composer 提供）。
/// </summary>
[DisallowMultipleComponent]
public class PromptContextManager : MonoBehaviour
{
    /// <summary>发起者（Prompt 层）；NPC 暂映射为 System 线，待多角色时再扩展。</summary>
    public enum InitiatorRole
    {
        System = 0,
        [Obsolete("PG1 归入 System 线；保留枚举值兼容 Inspector。")]
        NPC = 1,
        Player = 2,
    }

    public static PromptContextManager Instance { get; private set; }

    [Header("当前 LLM 轮次")]
    [SerializeField] private RemiPromptTurnKind currentTurnKind = RemiPromptTurnKind.PlayerChat;

    [Header("动态段落（运行时会变化）")]
    [SerializeField] private InitiatorRole initiatorRole = InitiatorRole.Player;

    [TextArea(2, 6)]
    [SerializeField] private string initiatorContext = "";

    [TextArea(2, 4)]
    [SerializeField] private string beatNarrativeIntent = "";

    [TextArea(2, 4)]
    [SerializeField] private string turnNarrativeIntent = "";

    [Header("当轮 UI 强调（仅 CharacterTriggered）")]
    [SerializeField] private bool turnEmphasisWholeLine;
    [SerializeField] private string[] turnEmphasisAnchors = Array.Empty<string>();

    [TextArea(2, 6)]
    [SerializeField] private string sceneContext = "";

    [TextArea(2, 8)]
    [SerializeField] private string dayPlanContext = "";

    [TextArea(2, 6)]
    [SerializeField] private string policyContext = "";

    [TextArea(2, 6)]
    [SerializeField] private string memoryExperiencesContext = "";

    [Header("Voice · 按需唤醒（运行时）")]
    [TextArea(2, 8)]
    [SerializeField] private string activeKnowledgeBlock = "";
    [TextArea(2, 8)]
    [SerializeField] private string activeMemoryBlock = "";

    [Header("简单预算（防止 prompt 膨胀，按字符裁剪）")]
    [SerializeField] private int maxInitiatorContextChars = 260;
    [SerializeField] private int maxEndingInitiatorContextChars = 2000;
    [SerializeField] private int maxNarrativeIntentChars = 320;
    [SerializeField] private int maxDayPlanContextChars = 520;
    [SerializeField] private int maxSceneContextChars = 220;
    [SerializeField] private int maxPolicyContextChars = 1024;
    [SerializeField] private int maxMemoryExperiencesContextChars = 640;
    [SerializeField] private int maxActiveContextChars = 480;

    private RemiPromptAssemblyMode _assemblyMode = RemiPromptAssemblyMode.Standard;
    private RemiPromptChannel _promptChannel = RemiPromptChannel.Voice;
    private readonly List<string> _stickyKnowledgeIds = new List<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public RemiPromptTurnKind CurrentTurnKind => currentTurnKind;

    public RemiPromptAssemblyMode AssemblyMode => _assemblyMode;

    public RemiPromptChannel PromptChannel => _promptChannel;

    public IReadOnlyList<string> StickyKnowledgeIds => _stickyKnowledgeIds;

    public void SetPromptAssemblyMode(RemiPromptAssemblyMode mode) => _assemblyMode = mode;

    public void SetPromptChannel(RemiPromptChannel channel) => _promptChannel = channel;

    public void SetTurnKind(RemiPromptTurnKind kind) => currentTurnKind = kind;

    public void SetActiveKnowledgeBlock(string block) =>
        activeKnowledgeBlock = ClampText(block, maxActiveContextChars);

    public void SetActiveMemoryBlock(string block) =>
        activeMemoryBlock = ClampText(block, maxActiveContextChars);

    public void SetStickyKnowledgeIds(IReadOnlyList<string> ids)
    {
        _stickyKnowledgeIds.Clear();
        if (ids == null)
            return;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
                _stickyKnowledgeIds.Add(ids[i].Trim());
        }
    }

    public void ClearActiveContextBlocks()
    {
        activeKnowledgeBlock = "";
        activeMemoryBlock = "";
    }

    public void SetInitiator(InitiatorRole role, string context)
    {
        initiatorRole = NormalizeInitiatorRole(role);
        int maxChars = _assemblyMode == RemiPromptAssemblyMode.EndingSpeak
            ? maxEndingInitiatorContextChars
            : maxInitiatorContextChars;
        initiatorContext = ClampText(context, maxChars);
    }

    /// <summary>段级叙事意图：绑 Quest/Episode；player_chat 跨多轮保留直至清除。</summary>
    public void SetBeatNarrativeIntent(string intent)
    {
        beatNarrativeIntent = ClampText(intent, maxNarrativeIntentChars);
    }

    /// <summary>当轮叙事裁定：PlayerChat Voice 用（如偏离接受）；SendSystem 不写入。结束后清除。</summary>
    public void SetTurnNarrativeIntent(string intent)
    {
        turnNarrativeIntent = ClampText(intent, maxNarrativeIntentChars);
    }

    public void ClearBeatNarrativeIntent() => beatNarrativeIntent = "";

    public void ClearTurnNarrativeIntent() => turnNarrativeIntent = "";

    public void SetTurnEmphasis(RemiDialogueEmphasisSpec spec)
    {
        turnEmphasisWholeLine = spec.WholeLine;
        if (spec.Anchors == null || spec.Anchors.Count == 0)
        {
            turnEmphasisAnchors = Array.Empty<string>();
            return;
        }

        var copy = new string[spec.Anchors.Count];
        for (int i = 0; i < spec.Anchors.Count; i++)
            copy[i] = spec.Anchors[i];
        turnEmphasisAnchors = copy;
    }

    public RemiDialogueEmphasisSpec GetTurnEmphasisSpec()
    {
        if (turnEmphasisWholeLine)
            return RemiDialogueEmphasisSpec.Whole;
        if (turnEmphasisAnchors == null || turnEmphasisAnchors.Length == 0)
            return RemiDialogueEmphasisSpec.None;
        return RemiDialogueEmphasisSpec.WithAnchors(turnEmphasisAnchors);
    }

    public void ClearTurnEmphasis()
    {
        turnEmphasisWholeLine = false;
        turnEmphasisAnchors = Array.Empty<string>();
    }

    public void ClearAllNarrativeIntent()
    {
        beatNarrativeIntent = "";
        turnNarrativeIntent = "";
    }

    public void SetSceneContext(string context)
    {
        sceneContext = ClampText(context, maxSceneContextChars);
    }

    public void SetDayPlanContext(string context)
    {
        dayPlanContext = ClampText(context, maxDayPlanContextChars);
    }

    public void SetPolicyContext(string context)
    {
        policyContext = ClampText(context, maxPolicyContextChars);
    }

    public void SetMemoryExperiencesContext(string context)
    {
        memoryExperiencesContext = ClampText(context, maxMemoryExperiencesContextChars);
    }

    public InitiatorRole GetInitiatorRole() => initiatorRole;

    public string GetInitiatorContext() => initiatorContext ?? string.Empty;

    public bool HasNarrativeIntent =>
        !string.IsNullOrWhiteSpace(beatNarrativeIntent) ||
        !string.IsNullOrWhiteSpace(turnNarrativeIntent);

    /// <summary>
    /// 动态上下文。
    /// Voice：RELATIONSHIP → CURRENT_CONTEXT → ACTIVE_KNOWLEDGE → ACTIVE_MEMORY；
    /// CharacterTriggered 另附 [TURN]（director_context）。
    /// Intent / 其它：空（旧 Combined 的 DAY_PLAN/STATE/POLICY/MEMORY 已归档）。
    /// </summary>
    public string BuildDynamicContextPrompt()
    {
        return BuildDynamicContextPrompt(currentTurnKind);
    }

    public string BuildDynamicContextPrompt(RemiPromptTurnKind turnKind)
    {
        var p = new System.Text.StringBuilder();

        // Ending 呈现：只要 [TURN]（内含身份+记忆）。
        if (_assemblyMode == RemiPromptAssemblyMode.EndingSpeak)
        {
            AppendTurnBlock(p, turnKind);
            return p.ToString().TrimEnd();
        }

        if (_promptChannel != RemiPromptChannel.Voice)
            return string.Empty;

        // Voice：角色认知顺序 — 关系 → 时空 → 知识 → 共同经历（System 再加 TURN）
        string relationship = RemiPromptBuilder.BuildRelationshipBlock(RemiPresenceService.Instance);
        if (!string.IsNullOrWhiteSpace(relationship))
        {
            p.Append("[RELATIONSHIP]\n");
            p.Append(relationship.Trim());
            p.Append("\n\n");
        }

        string current = RemiPromptBuilder.BuildActorCurrentContextBlock(RemiPresenceService.Instance);
        if (!string.IsNullOrWhiteSpace(current))
        {
            p.Append("[CURRENT_CONTEXT]\n");
            p.Append(current.Trim());
            p.Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(activeKnowledgeBlock))
        {
            p.Append(activeKnowledgeBlock.Trim());
            p.Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(activeMemoryBlock))
        {
            p.Append(activeMemoryBlock.Trim());
            p.Append("\n\n");
        }

        if (turnKind == RemiPromptTurnKind.CharacterTriggered)
            AppendTurnBlock(p, turnKind);

        return p.ToString().TrimEnd();
    }

    private void AppendTurnBlock(System.Text.StringBuilder p, RemiPromptTurnKind turnKind)
    {
        p.Append("[TURN]\n");
        p.Append("mode: ");
        p.Append(turnKind == RemiPromptTurnKind.CharacterTriggered
            ? "character_triggered"
            : "player_chat");
        p.Append('\n');
        p.Append("initiator: ");
        p.Append(turnKind == RemiPromptTurnKind.CharacterTriggered ? "system" : "player");
        p.Append('\n');

        if (!string.IsNullOrWhiteSpace(initiatorContext))
        {
            p.Append("director_context:\n");
            // EndingSpeak：保留换行，便于 [身份]/[记忆] 结构。
            if (_assemblyMode == RemiPromptAssemblyMode.EndingSpeak)
                p.Append(initiatorContext.Trim());
            else
                p.Append(initiatorContext.Trim().Replace('\n', ' '));
            p.Append('\n');
        }

        p.Append('\n');
    }

    /// <summary>[INTENT]：beat（跨轮）+ turn（当轮，PlayerChat 裁定）。SendSystem 不拼此段。</summary>
    public string BuildNarrativeIntentPrompt()
    {
        bool hasBeat = !string.IsNullOrWhiteSpace(beatNarrativeIntent);
        bool hasTurn = !string.IsNullOrWhiteSpace(turnNarrativeIntent);
        if (!hasBeat && !hasTurn)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.Append("[INTENT]\n");
        if (hasBeat)
            sb.Append(beatNarrativeIntent.Trim());
        if (hasBeat && hasTurn)
            sb.Append('\n');
        if (hasTurn)
        {
            if (hasBeat)
                sb.Append("turn: ");
            sb.Append(turnNarrativeIntent.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    private static InitiatorRole NormalizeInitiatorRole(InitiatorRole role) =>
        role == InitiatorRole.NPC ? InitiatorRole.System : role;

    private static string ClampText(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string t = text.Trim();
        if (maxChars <= 0) return t;
        if (t.Length <= maxChars) return t;
        return t.Substring(0, maxChars).TrimEnd() + "…";
    }
}
