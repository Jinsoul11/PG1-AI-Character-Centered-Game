using UnityEngine;

/// <summary>
/// Remi 表情（与 RemiController_Suriyun Face 层 Expression 一致）：
/// 1–5 五种表情；7 = Neutral（Face_SmileB）；待机 Default = 6。
/// </summary>
public enum RemiExpression
{
    Happy = 1,
    Angry = 2,
    Sad = 3,
    Surprised = 4,
    Shy = 5,

    /// <summary>AI Neutral：播 Face_SmileB；播完后回落 Expression=6（Default）。</summary>
    Neutral = 7,
}

/// <summary>
/// PG1 专属 Remi 逻辑类（继承 MonoBehaviour，挂载到 Remi 角色对象上）
/// 核心职责：静态 Prompt（C 输出协议 + [CHARACTER] 身份块）、拼接 System Prompt。
/// 动态 A / 动态 B / 动态 C 由 <see cref="RemiPresenceService"/> 写入 <see cref="PromptContextManager"/>。
/// </summary>
public class Remi : MonoBehaviour
{
    #region 状态管理（私有，仅内部维护）
    /// <summary>
    /// 静态 [CHARACTER]：身份与公共背景 + 阶段 seeds。
    /// </summary>
    private string characterPromptBlock;

    /// <summary>
    /// 拼接 API system 消息：按 <see cref="PromptContextManager.CurrentTurnKind"/> 选 PLAYER / SYSTEM 合约。
    /// 调用前须已由 <see cref="RemiPresenceService.PushToPromptContext"/> 刷新动态块。
    /// </summary>
    public string GetFinalSystemPrompt()
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        RemiPromptTurnKind turnKind = ctx != null
            ? ctx.CurrentTurnKind
            : RemiPromptTurnKind.PlayerChat;

        string dynamicContext = ctx != null
            ? ctx.BuildDynamicContextPrompt(turnKind)
            : "";

        // SendSystem（CharacterTriggered）不拼 [INTENT] 文案段；PlayerChat 仍可带当轮裁定。
        string narrativeIntentBlock = "";
        if (ctx != null && turnKind != RemiPromptTurnKind.CharacterTriggered)
            narrativeIntentBlock = ctx.BuildNarrativeIntentPrompt();

        RefreshCharacterPromptBlock();

        return RemiPromptComposer.BuildFullSystemPrompt(
            turnKind,
            characterPromptBlock,
            dynamicContext,
            narrativeIntentBlock,
            ctx != null ? ctx.AssemblyMode : RemiPromptAssemblyMode.Standard,
            ctx != null ? ctx.PromptChannel : RemiPromptChannel.Voice);
    }

    public Animator animator;

    private static readonly int AnimatorStartHash = Animator.StringToHash("Start");
    private static readonly int AnimatorWalkingHash = Animator.StringToHash("Walking");

    #endregion

    #region 生命周期（初始化）
    private void Awake()
    {
        animator = this.GetComponent<Animator>();
        RefreshCharacterPromptBlock();
    }
    #endregion

    #region 核心对外接口（供其他系统调用）

    private void RefreshCharacterPromptBlock()
    {
        RemiDialogueDepthStage stage = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.DialogueDepthStage
            : RemiDialogueDepthStage.Surface;
        characterPromptBlock = "[CHARACTER]\n" + RemiCharacterPrompt.BuildBlock(stage);
    }

    /// <summary>
    /// Body 层：<paramref name="dialogueOpen"/> 为 false 时待在待机1（三循环），为 true 时进入待机2（对话面板）。
    /// 与 Animator 上的 Bool 参数 <c>Start</c> 一致；打开对话时同时清 <c>Walking</c>，以便从 Wander 直切 Idle2。
    /// </summary>
    public void SetDialogueBodyIdle(bool dialogueOpen)
    {
        if (animator == null) return;
        if (dialogueOpen)
            animator.SetBool(AnimatorWalkingHash, false);
        animator.SetBool(AnimatorStartHash, dialogueOpen);
    }

    /// <summary>Face 层待机：Default 状态对应的 Expression 值。</summary>
    public const int ExpressionIdle = 6;

    /// <summary>
    /// 角色选择播放动画
    /// </summary>
    public void PlayExpression(RemiExpression expression)
    {
        if (animator == null) return;
        this.animator.SetInteger("Expression", (int)expression);
    }

    /// <summary>
    /// Animation Event / StateMachineBehaviour：表情播完后回到 Default（Expression=6）。
    /// </summary>
    public void ResetExpressionEvent()
    {
        if (animator == null) return;
        this.animator.SetInteger("Expression", ExpressionIdle);
    }

    /// <summary>
    /// 重置所有状态（供UI调试调用）
    /// </summary>
    public void ResetAllState()
    {
        if (RemiPresenceService.Instance != null)
            RemiPresenceService.Instance.ResetRelationshipStateForDebug();
        if (DeepSeekDialogueManager.Instance != null)
            DeepSeekDialogueManager.Instance.ResetPromptLogTracking();
        Debug.Log("[Remi] 已重置 Presence 关系/节奏状态（养成由 Presence 负责）");
    }

    [System.Obsolete("PG1 已移除 E 值；请改调 RemiPresenceService / DialogueDepthStage。")]
    public void ChangeE(float e)
    {
        Debug.LogWarning($"[Remi] ChangeE({e}) 已废弃，情绪养成由 RemiPresenceService 负责。");
    }
    #endregion
}
