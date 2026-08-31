using System.Collections;
using UnityEngine;

/// <summary>
/// 统一对话入口：player_chat（SendPlayer）与 character_triggered（SendSystem）。
/// 固定 Voice→Intent 分层（自然语言 + expression JSON）；旧 Combined 混合已归档。
/// </summary>
[DisallowMultipleComponent]
public class PromptedDialogueAgent : MonoBehaviour
{
    public static PromptedDialogueAgent Instance { get; private set; }

    [Header("依赖")]
    [SerializeField] private DeepSeekDialogueManager dialogueManager;
    [SerializeField] private PromptContextManager contextManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        ResolveDependencies();
        RemiSendSystemDebugDirector.EnsureExists();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveDependencies()
    {
        if (dialogueManager == null)
            dialogueManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();
        if (contextManager == null)
            contextManager = PromptContextManager.Instance != null
                ? PromptContextManager.Instance
                : FindObjectOfType<PromptContextManager>();
    }

    public IEnumerator SendPlayer(
        string playerText,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null,
        RemiStageExpressionContext? stageContext = null,
        bool preserveInitiatorContext = false,
        bool voiceOnly = false,
        bool recordUserInHistory = true)
    {
        yield return SendPlayer(
            playerText,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError,
            onRevealText,
            stageContext,
            preserveInitiatorContext,
            voiceOnly,
            recordUserInHistory);
    }

    public IEnumerator SendPlayer(
        string playerText,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null,
        RemiStageExpressionContext? stageContext = null,
        bool preserveInitiatorContext = false,
        bool voiceOnly = false,
        bool recordUserInHistory = true)
    {
        ResolveDependencies();
        BeginPlayerChatTurn(preserveInitiatorContext, voiceOnly);
        RefreshPromptContext(stageContext);

        if (voiceOnly)
        {
            yield return dialogueManager.SendVoiceOnly(
                playerText,
                (text, expr, payload) =>
                {
                    onSuccess?.Invoke(text, expr, payload);
                    FinishPlayerChatTurn();
                },
                err =>
                {
                    onError?.Invoke(err);
                    FinishPlayerChatTurn();
                },
                onRevealText,
                recordUserInHistory);
            yield break;
        }

        yield return dialogueManager.SendVoiceThenIntent(
            playerText,
            (text, expr, payload) =>
            {
                onSuccess?.Invoke(text, expr, payload);
                FinishPlayerChatTurn();
            },
            err =>
            {
                onError?.Invoke(err);
                FinishPlayerChatTurn();
            },
            onRevealText,
            recordUserInHistory);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendSystem(
            initiatorContext,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError, onRevealText, RemiPromptAssemblyMode.Standard);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText,
        RemiPromptAssemblyMode assemblyMode)
    {
        yield return SendSystem(
            initiatorContext,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError, onRevealText, assemblyMode, RemiPromptChannel.Voice);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText,
        RemiPromptAssemblyMode assemblyMode,
        RemiPromptChannel channel)
    {
        yield return SendSystem(
            initiatorContext,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError, onRevealText, assemblyMode, channel);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendSystem(
            initiatorContext,
            onSuccess,
            onError,
            onRevealText,
            RemiPromptAssemblyMode.Standard,
            RemiPromptChannel.Voice);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText,
        RemiPromptAssemblyMode assemblyMode)
    {
        yield return SendSystem(
            initiatorContext,
            onSuccess,
            onError,
            onRevealText,
            assemblyMode,
            RemiPromptChannel.Voice);
    }

    public IEnumerator SendSystem(
        string initiatorContext,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText,
        RemiPromptAssemblyMode assemblyMode,
        RemiPromptChannel channel,
        RemiStageExpressionContext? stageContext = null)
    {
        ResolveDependencies();

        // Standard 默认 Voice；EndingSpeak 保持调用方指定（通常为 Voice）。
        if (assemblyMode == RemiPromptAssemblyMode.Standard &&
            channel != RemiPromptChannel.Intent)
        {
            channel = RemiPromptChannel.Voice;
        }

        BeginCharacterTriggeredTurn(initiatorContext, assemblyMode, channel, out string resolvedInitiator);
        if (assemblyMode != RemiPromptAssemblyMode.EndingSpeak)
        {
            RefreshPromptContext(stageContext ?? RemiStageExpressionContext.FaceToFaceChat);
        }

        bool useVoiceSplit = assemblyMode == RemiPromptAssemblyMode.Standard &&
                             channel == RemiPromptChannel.Voice;

        if (useVoiceSplit)
        {
            string query = resolvedInitiator != null ? resolvedInitiator.Trim() : "";
            yield return dialogueManager.SendTriggeredVoiceThenIntent(
                query,
                (text, expr, payload) =>
                {
                    onSuccess?.Invoke(text, expr, payload);
                    FinishCharacterTriggeredTurn();
                },
                err =>
                {
                    onError?.Invoke(err);
                    FinishCharacterTriggeredTurn();
                },
                onRevealText);
            yield break;
        }

        yield return dialogueManager.SendTriggeredMessage(
            (text, expr, payload) =>
            {
                onSuccess?.Invoke(text, expr, payload);
                FinishCharacterTriggeredTurn();
            },
            err =>
            {
                onError?.Invoke(err);
                FinishCharacterTriggeredTurn();
            },
            onRevealText);
    }

    /// <summary>手机通道 SendSystem：SocialChat 舞台；失败由调用方 fallback。</summary>
    public IEnumerator SendSystemSocial(
        string initiatorContext,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendSystem(
            initiatorContext,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError,
            onRevealText,
            RemiPromptAssemblyMode.Standard,
            RemiPromptChannel.Voice,
            RemiStageExpressionContext.SocialChat);
    }

    /// <summary>暂归 System 线；多角色时再扩展独立 Prompt。</summary>
    public IEnumerator SendNpc(
        string initiatorContext,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendSystem(initiatorContext, onSuccess, onError, onRevealText);
    }

    public IEnumerator SendNpc(
        string initiatorContext,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendSystem(initiatorContext, onSuccess, onError, onRevealText);
    }

    private void BeginPlayerChatTurn(bool preserveInitiatorContext, bool voiceOnly = false)
    {
        if (contextManager == null) return;
        contextManager.SetPromptAssemblyMode(RemiPromptAssemblyMode.Standard);
        contextManager.SetPromptChannel(RemiPromptChannel.Voice);
        contextManager.SetTurnKind(RemiPromptTurnKind.PlayerChat);
        if (!preserveInitiatorContext)
            contextManager.SetInitiator(PromptContextManager.InitiatorRole.Player, "");
    }

    private void FinishPlayerChatTurn()
    {
        contextManager?.ClearTurnNarrativeIntent();
        contextManager?.ClearActiveContextBlocks();
        contextManager?.SetPromptChannel(RemiPromptChannel.Voice);
    }

    private void BeginCharacterTriggeredTurn(
        string initiatorContext,
        RemiPromptAssemblyMode assemblyMode,
        RemiPromptChannel channel,
        out string resolvedInitiator)
    {
        resolvedInitiator = initiatorContext ?? string.Empty;

        if (contextManager == null)
            return;

        RemiSendSystemDebugDirector.EnsureExists();
        RemiSendSystemDebugDirector.Instance?.TryResolve(initiatorContext, out resolvedInitiator);

        // 先设组装模式，再 SetInitiator（EndingSpeak 使用更大的 director_context 预算）。
        contextManager.SetPromptAssemblyMode(assemblyMode);
        contextManager.SetPromptChannel(channel);
        contextManager.SetTurnKind(RemiPromptTurnKind.CharacterTriggered);
        contextManager.SetInitiator(PromptContextManager.InitiatorRole.System, resolvedInitiator);
        contextManager.ClearTurnNarrativeIntent();
    }

    private void FinishCharacterTriggeredTurn()
    {
        contextManager?.ClearTurnNarrativeIntent();
        contextManager?.ClearTurnEmphasis();
        contextManager?.ClearActiveContextBlocks();
        contextManager?.SetPromptAssemblyMode(RemiPromptAssemblyMode.Standard);
        contextManager?.SetPromptChannel(RemiPromptChannel.Voice);
        contextManager?.SetTurnKind(RemiPromptTurnKind.PlayerChat);
        contextManager?.SetInitiator(PromptContextManager.InitiatorRole.Player, "");
    }

    private void RefreshPromptContext(RemiStageExpressionContext? stageContextOverride)
    {
        if (dialogueManager == null || contextManager == null)
            ResolveDependencies();
        if (RemiPresenceService.Instance == null) return;
        RemiPresenceService.Instance.PushToPromptContext(stageContextOverride);
    }

    /// <summary>
    /// Day3 偏离窗口专用 Intent：单独请求，判断自由输入是否在提偏离。
    /// 不写 history、不走 Voice；失败时 <see cref="RemiDeviationDetectIntent.Result.ParseOk"/> 为 false。
    /// </summary>
    public IEnumerator CoDetectDay3DeviationPropose(
        string playerText,
        System.Action<RemiDeviationDetectIntent.Result> onDone)
    {
        ResolveDependencies();
        var result = new RemiDeviationDetectIntent.Result
        {
            ParseOk = false,
            ProposeDeviation = false,
            Target = string.Empty,
        };

        if (dialogueManager == null)
        {
            result.Error = "no_dialogue_manager";
            onDone?.Invoke(result);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(playerText))
        {
            result.Error = "empty_player";
            onDone?.Invoke(result);
            yield break;
        }

        string system = RemiDeviationDetectIntent.BuildSystemPrompt();
        string user = RemiDeviationDetectIntent.BuildUserPrompt(playerText);
        string raw = null;
        string err = null;

        yield return dialogueManager.CoCompleteRaw(
            system,
            user,
            content => raw = content,
            e => err = e,
            temperature: 0.2f);

        if (!string.IsNullOrEmpty(err))
        {
            result.Error = err;
            Debug.LogWarning($"[DeviationDetect] 请求失败：{err}");
            onDone?.Invoke(result);
            yield break;
        }

        result = RemiDeviationDetectIntent.Parse(raw);
        if (!result.ParseOk)
            Debug.LogWarning($"[DeviationDetect] 解析失败：{result.Error} | {raw}");
        else
            Debug.Log(
                $"[DeviationDetect] propose={result.ProposeDeviation} target={result.Target}");

        onDone?.Invoke(result);
    }
}
