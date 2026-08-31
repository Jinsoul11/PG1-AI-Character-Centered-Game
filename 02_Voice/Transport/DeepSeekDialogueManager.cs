using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using LitJson;
using System.Text.RegularExpressions;

// 对话消息模型（适配Seek API）
public class ChatMessage
{
    public string role; // system/user/assistant
    public string content;

    public ChatMessage()
    {
    }

    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

// Seek API返回结果的根模型
[System.Serializable]
public class SeekResponse
{
    public string id;
    public string object_type;
    public long created;
    public string model;
    public List<SeekChoice> choices;
    public SeekUsage usage;
}

// 回复选项模型（匹配JSON的choices项）
[System.Serializable]
public class SeekChoice
{
    public int index;
    public SeekMessage message; // 消息体（单独定义，避免复用ChatMessage的潜在问题）
    public object logprobs; // JSON中logprobs是null，用object兼容
    public string finish_reason;
}

// 消息体模型（独立定义，避免和原有ChatMessage冲突）
[System.Serializable]
public class SeekMessage
{
    public string role;
    public string content; // 核心回复内容
}

// 用量统计模型（完全匹配JSON的usage字段）
[System.Serializable]
public class SeekUsage
{
    public int prompt_tokens;
    public int completion_tokens;
    public int total_tokens;
    public SeekPromptTokensDetails prompt_tokens_details; // 新增子字段
    public int prompt_cache_hit_tokens;
    public int prompt_cache_miss_tokens;
    public string system_fingerprint; // 新增字段
}

// 用量统计子模型（匹配prompt_tokens_details）
[System.Serializable]
public class SeekPromptTokensDetails
{
    public int cached_tokens;
}

// 大模型在 content 中返回的结构化结果（第一行 JSON + 可选自然语言 speech）
[System.Serializable]
public class ModelReplyPayload
{
    public string expression;   // Happy / Angry / Sad / Surprise / Shy / Neutral
    public string text;         // 兼容旧字段
    public string speech;       // PG1：展示台词（优先于 text）
    /// <summary>SendSystem 可选：逗号分隔锚点，或 * 表示整句加粗。</summary>
    public string emphasis;
    public bool memory_write;
    public string memory_content;
    public string memory_type;
    public ModelReplyMetaPayload meta;
}

// 请求体模型（用于 LitJson 序列化，避免手写 JSON 转义错误）
[System.Serializable]
public class SeekChatRequest
{
    public string model;
    public List<SeekChatRequestMessage> messages = new List<SeekChatRequestMessage>();
    public float temperature = 0.7f;
}

[System.Serializable]
public class SeekChatRequestMessage
{
    public string role;
    public string content;

    public SeekChatRequestMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

public class DeepSeekDialogueManager : MonoBehaviour
{
    [Header("Seek配置")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string apiUrl = "https://api.seek.com/v1/chat/completions";
    [SerializeField] private string model = "seek-chat";
    [SerializeField] private int maxHistoryCount = 20; // 每通道最大 user/assistant 条数（不含 system）
    [SerializeField] private int maxFaceHistoryCount = 16;
    [SerializeField] private int maxSocialHistoryCount = 12;

    [Header("文本呈现")]
    [Tooltip("若拖入场景中的 Director，则使用其上的打字机速度与模式。")]
    [SerializeField] private DialogueSequenceDirector utteranceSequenceDirector;
    [Min(0.1f)]
    [SerializeField] private float typewriterCharsPerSecondFallback = 18f;

    // 面对面 LLM 上下文（落盘）；社媒仅在手机打开期间使用独立会话缓冲。
    private readonly List<ChatMessage> messageHistory = new List<ChatMessage>();
    private readonly List<ChatMessage> _socialSessionHistory = new List<ChatMessage>();

    /// <summary>玩家轮次已打印过 Prompt 的关系阶段（阶段变化后首次 player 请求再打印）。</summary>
    private RemiDialogueDepthStage? _playerPromptLoggedStage;

    /// <summary>关联 Remi；换模后若 Inspector 未拖引用，会在首次发消息时 <see cref="TryResolveRemi"/>。</summary>
    public Remi remi;

    private const string FallbackLocalLineWhenNoRemi =
        "……（未找到 Remi 组件：请在角色根物体上挂载 Remi，或在 DeepSeekDialogueManager 上指定引用。）";

    private const string FallbackLineOnRequestFailure = "……";

    // 单例
    public static DeepSeekDialogueManager Instance { get; private set; }

    private bool TryResolveRemi()
    {
        if (remi != null) return true;
        remi = FindObjectOfType<Remi>();
        return remi != null;
    }

    private string GetLocalDialogueSafe()
    {
        if (remi == null && !TryResolveRemi())
            return FallbackLocalLineWhenNoRemi;
        return FallbackLineOnRequestFailure;
    }

    /// <summary>当面 LLM history 落盘键（与 ChatHistoryPanel 的 Conversation1 分离）。</summary>
    public const string MessageHistorySaveKey = "RemiLlmMessageHistory";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // System 预制体子物体：必须先脱父，否则 DDOL 无效，切场景随 Classroom 一起销毁。
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            LoadMessageHistoryFromDisk();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        TryResolveRemi();
        Debug.Log("Seek对话管理器初始化完成");
    }

    private void OnApplicationQuit()
    {
        PersistMessageHistoryToDisk();
        RemiDemoDaySaveService.FlushLiveConversationIntoLatestSlot();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 发送带情绪的用户消息（核心接口，简化参数）
    /// </summary>
    /// <param name="userInput">用户输入</param>
    /// <param name="onSuccess">成功回调：展示文本、expression、完整 JSON 载荷（可 Commit）</param>
    public IEnumerator SendMessageWithEmotion(
        string userInput,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendMessageInternal(
            userInput,
            recordUserInHistory: true,
            recordAssistantInHistory: true,
            onSuccess,
            onError,
            onRevealText);
    }

    /// <summary>
    /// Player 仅 Voice：自然语言写入 history，expression 固定 Neutral（不跑 Intent）。
    /// </summary>
    public IEnumerator SendVoiceOnly(
        string userInput,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null,
        bool recordUserInHistory = true)
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        if (ctx == null)
        {
            onError?.Invoke("配置错误：未找到 PromptContextManager。");
            yield break;
        }

        ctx.SetPromptChannel(RemiPromptChannel.Voice);
        yield return RemiActiveContextRetriever.CoPrepareVoiceContext(userInput);
        if (TryResolveRemi())
            MaybeLogLlmRequest(remi.GetFinalSystemPrompt(), userInput, isPlayerTurn: true);

        var voiceRound = new ChatRoundResult();
        yield return CoChatRound(
            userInput,
            recordUserInHistory: recordUserInHistory,
            recordAssistantInHistory: true,
            temperature: 0.7f,
            voiceRound,
            skipPromptLog: true);

        if (!voiceRound.ok)
        {
            ctx.SetPromptChannel(RemiPromptChannel.Voice);
            string localDialogue = GetLocalDialogueSafe();
            onError?.Invoke(voiceRound.error ?? "Voice 请求失败");
            yield return PresentRemiReplyDisplay(localDialogue, "Neutral", null, onSuccess, onRevealText);
            yield break;
        }

        string voiceText = voiceRound.displayText;
        const string expression = "Neutral";
        ModelReplyPayload payload = voiceRound.payload ?? new ModelReplyPayload();
        payload.speech = voiceText;
        payload.expression = expression;

        LogVoiceIntentReply(voiceText, expression, voiceOnly: true);
        ctx.SetPromptChannel(RemiPromptChannel.Voice);

        RemiPromptTurnKind turnKind = ctx.CurrentTurnKind;
        RemiDialogueEmphasisSpec directorEmphasis = ctx.GetTurnEmphasisSpec();
        string payloadEmphasis = payload.emphasis;
        voiceText = RemiDialogueEmphasis.FormatSpeechForTurn(
            voiceText,
            turnKind,
            payloadEmphasis,
            directorEmphasis);

        yield return PresentRemiReplyDisplay(voiceText, expression, payload, onSuccess, onRevealText);
    }

    /// <summary>
    /// Player 分层：Voice（自然语言，写入 history）→ Intent（expression JSON，不写 history）→ 呈现。
    /// Intent 失败时 expression 回落 Neutral。
    /// </summary>
    public IEnumerator SendVoiceThenIntent(
        string userInput,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null,
        bool recordUserInHistory = true)
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        if (ctx == null)
        {
            onError?.Invoke("配置错误：未找到 PromptContextManager。");
            yield break;
        }

        // 1) Voice
        ctx.SetPromptChannel(RemiPromptChannel.Voice);
        yield return RemiActiveContextRetriever.CoPrepareVoiceContext(userInput);
        string voiceSystemForLog = TryResolveRemi() ? remi.GetFinalSystemPrompt() : "";
        var voiceRound = new ChatRoundResult();
        yield return CoChatRound(
            userInput,
            recordUserInHistory: recordUserInHistory,
            recordAssistantInHistory: true,
            temperature: 0.7f,
            voiceRound,
            skipPromptLog: true);

        if (!voiceRound.ok)
        {
            MaybeLogVoiceIntentPair(voiceSystemForLog, userInput, intentSystem: null, intentUser: null);
            ctx.SetPromptChannel(RemiPromptChannel.Voice);
            string localDialogue = GetLocalDialogueSafe();
            onError?.Invoke(voiceRound.error ?? "Voice 请求失败");
            yield return PresentRemiReplyDisplay(localDialogue, "Neutral", null, onSuccess, onRevealText);
            yield break;
        }

        string voiceText = voiceRound.displayText;
        // Voice 通道不采信模型自带的 expression
        string expression = "Neutral";
        ModelReplyPayload payload = voiceRound.payload ?? new ModelReplyPayload();
        payload.speech = voiceText;
        payload.expression = expression;

        // 2) Intent（失败仅影响表情）
        ctx.SetPromptChannel(RemiPromptChannel.Intent);
        string intentExpr = null;
        string intentError = null;
        string intentSystemForLog = null;
        string intentUserForLog = null;

        if (!TryResolveRemi())
        {
            intentError = "未找到 Remi";
        }
        else
        {
            intentSystemForLog = remi.GetFinalSystemPrompt();
            intentUserForLog = RemiPromptComposer.BuildIntentUserPrompt(userInput, voiceText);
            MaybeLogVoiceIntentPair(voiceSystemForLog, userInput, intentSystemForLog, intentUserForLog);

            yield return CoCompleteRaw(
                intentSystemForLog,
                intentUserForLog,
                content => { intentExpr = ParseExpressionFromIntentContent(content); },
                err => { intentError = err; },
                temperature: 0.3f);
        }

        if (intentSystemForLog == null)
            MaybeLogVoiceIntentPair(voiceSystemForLog, userInput, intentSystem: null, intentUser: null);

        if (!string.IsNullOrEmpty(intentExpr))
        {
            expression = intentExpr;
            payload.expression = expression;
        }
        else if (!string.IsNullOrEmpty(intentError))
        {
            Debug.LogWarning($"[Seek] Intent 失败，expression=Neutral：{intentError}");
        }

        LogVoiceIntentReply(voiceText, expression);

        ctx.SetPromptChannel(RemiPromptChannel.Voice);

        RemiPromptTurnKind turnKind = ctx.CurrentTurnKind;
        RemiDialogueEmphasisSpec directorEmphasis = ctx.GetTurnEmphasisSpec();
        string payloadEmphasis = payload != null ? payload.emphasis : null;
        voiceText = RemiDialogueEmphasis.FormatSpeechForTurn(
            voiceText,
            turnKind,
            payloadEmphasis,
            directorEmphasis);

        yield return PresentRemiReplyDisplay(voiceText, expression, payload, onSuccess, onRevealText);
    }

    private sealed class ChatRoundResult
    {
        public bool ok;
        public string error;
        public string displayText;
        public string expression;
        public ModelReplyPayload payload;
    }

    private static string ParseExpressionFromIntentContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        string raw = RemoveThoughtMarkers(content).Trim();
        if (string.IsNullOrEmpty(raw))
            return null;

        // 容忍 markdown 代码块或前后杂音：取首个 {...}
        int start = raw.IndexOf('{');
        if (start < 0)
            return null;

        int braceDepth = 0;
        int end = -1;
        for (int i = start; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{') braceDepth++;
            else if (c == '}')
            {
                braceDepth--;
                if (braceDepth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end < 0)
            return null;

        string jsonBlock = raw.Substring(start, end - start + 1);
        try
        {
            ModelReplyPayload parsed = JsonMapper.ToObject<ModelReplyPayload>(jsonBlock);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.expression))
                return null;
            return parsed.expression.Trim();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Seek] Intent JSON 解析失败：{e.Message} | {jsonBlock}");
            return null;
        }
    }

    /// <summary>单轮 chat：请求 + 解析 + 可选写入 history；不播 TTS。</summary>
    private IEnumerator CoChatRound(
        string userInput,
        bool recordUserInHistory,
        bool recordAssistantInHistory,
        float temperature,
        ChatRoundResult result,
        bool skipPromptLog = false)
    {
        result.ok = false;
        result.error = null;
        result.displayText = null;
        result.expression = "Neutral";
        result.payload = null;

        if (!TryResolveRemi())
        {
            result.error = "配置错误：未找到 Remi 角色（DeepSeekDialogueManager.remi）。";
            yield break;
        }

        List<ChatMessage> activeHistory = GetActiveHistoryList();
        int historyCap = GetActiveHistoryCap();

        string finalSystemPrompt = remi.GetFinalSystemPrompt();
        if (!skipPromptLog)
            MaybeLogLlmRequest(finalSystemPrompt, userInput, recordUserInHistory);

        if (recordUserInHistory)
            RemiDemoRunTelemetry.EnsureExists();
        if (recordUserInHistory && RemiDemoRunTelemetry.Instance != null)
            RemiDemoRunTelemetry.Instance.RecordPlayerMessage(ResolveHistoryChannel());

        List<ChatMessage> requestMessages = new List<ChatMessage>();
        requestMessages.Add(new ChatMessage("system", finalSystemPrompt));
        requestMessages.AddRange(activeHistory);
        requestMessages.Add(new ChatMessage("user", userInput));

        if (requestMessages.Count > historyCap + 1)
            requestMessages.RemoveRange(1, requestMessages.Count - (historyCap + 1));

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.error = "API Key 未配置";
            yield break;
        }

        var req = new SeekChatRequest
        {
            model = model,
            temperature = temperature
        };
        foreach (var m in requestMessages)
            req.messages.Add(new SeekChatRequestMessage(m.role, m.content));

        string jsonBody = JsonMapper.ToJson(req);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string responseBody = request.downloadHandler?.text ?? "";
                if (!string.IsNullOrEmpty(responseBody))
                    Debug.LogWarning($"[Seek] 请求失败，服务端返回：{responseBody}");
                result.error = $"API请求失败：{request.error}";
                yield break;
            }

            string response = request.downloadHandler.text;
            string displayText;
            string expression;
            ModelReplyPayload payload;
            ParseStructuredAIReply(
                response,
                out displayText,
                out expression,
                out payload,
                logParseResult: !skipPromptLog);

            if (recordUserInHistory)
                activeHistory.Add(new ChatMessage("user", userInput));

            if (recordAssistantInHistory)
            {
                activeHistory.Add(new ChatMessage("assistant", RemiDialogueEmphasis.StripRichText(displayText)));
                TrimHistoryList(activeHistory, historyCap);
            }

            if (recordUserInHistory || recordAssistantInHistory)
                PersistMessageHistoryToDisk();

            result.ok = true;
            result.displayText = displayText;
            result.expression = expression;
            result.payload = payload;
        }
    }

    /// <summary>兼容仅关心文本与表情的调用方。</summary>
    public IEnumerator SendMessageWithEmotion(
        string userInput,
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendMessageWithEmotion(
            userInput,
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError,
            onRevealText);
    }

    /// <summary>
    /// 工具/策展用原始补全：不走 Remi 人设、不写聊天 history、不播 TTS。
    /// Memory Curator / Analyzer 等系统 LLM 调用入口。
    /// </summary>
    public IEnumerator CoCompleteRaw(
        string systemPrompt,
        string userPrompt,
        System.Action<string> onSuccess,
        System.Action<string> onError,
        float temperature = 0.3f)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("API Key 未配置");
            yield break;
        }

        var req = new SeekChatRequest
        {
            model = model,
            temperature = temperature,
        };
        req.messages.Add(new SeekChatRequestMessage("system", systemPrompt ?? ""));
        req.messages.Add(new SeekChatRequestMessage("user", userPrompt ?? ""));

        string jsonBody = JsonMapper.ToJson(req);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler?.text ?? "";
                if (!string.IsNullOrEmpty(body))
                    Debug.LogWarning($"[Seek/Raw] 请求失败：{body}");
                onError?.Invoke($"API请求失败：{request.error}");
                yield break;
            }

            string response = request.downloadHandler?.text ?? "";
            if (!TryExtractAssistantContent(response, out string content, out string parseError))
            {
                onError?.Invoke(parseError ?? "解析失败");
                yield break;
            }

            onSuccess?.Invoke(content);
        }
    }

    private static bool TryExtractAssistantContent(string response, out string content, out string error)
    {
        content = null;
        error = null;
        try
        {
            SeekResponse parsed = JsonMapper.ToObject<SeekResponse>(response);
            if (parsed?.choices == null || parsed.choices.Count == 0 ||
                parsed.choices[0]?.message == null)
            {
                error = "choices/message 为空";
                return false;
            }

            content = parsed.choices[0].message.content ?? "";
            return true;
        }
        catch (System.Exception ex)
        {
            error = $"解析异常：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// system/npc 主动触发：需要让模型“生成第一句话”，但不希望把占位符输入记到历史里。
    /// </summary>
    public IEnumerator SendTriggeredMessage(
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendMessageInternal(
            "（触发生成）",
            recordUserInHistory: false,
            recordAssistantInHistory: false,
            onSuccess,
            onError,
            onRevealText);
    }

    public IEnumerator SendTriggeredMessage(
        System.Action<string, string> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        yield return SendTriggeredMessage(
            (text, expr, _) => onSuccess?.Invoke(text, expr),
            onError,
            onRevealText);
    }

    /// <summary>
    /// CharacterTriggered 分层：Voice（自然语言）→ Intent（expression）→ 呈现。
    /// 与 SendPlayer 的 Voice 动态架构一致；占位 user 不入 history。
    /// </summary>
    public IEnumerator SendTriggeredVoiceThenIntent(
        string activeContextQuery,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        if (ctx == null)
        {
            onError?.Invoke("配置错误：未找到 PromptContextManager。");
            yield break;
        }

        ctx.SetPromptChannel(RemiPromptChannel.Voice);
        yield return RemiActiveContextRetriever.CoPrepareVoiceContext(activeContextQuery ?? "");
        string voiceSystemForLog = TryResolveRemi() ? remi.GetFinalSystemPrompt() : "";
        var voiceRound = new ChatRoundResult();
        yield return CoChatRound(
            "（触发生成）",
            recordUserInHistory: false,
            recordAssistantInHistory: false,
            temperature: 0.7f,
            voiceRound,
            skipPromptLog: true);

        if (!voiceRound.ok)
        {
            MaybeLogVoiceIntentPair(
                voiceSystemForLog,
                "（触发生成）",
                intentSystem: null,
                intentUser: null,
                characterTriggered: true);
            ctx.SetPromptChannel(RemiPromptChannel.Voice);
            string localDialogue = GetLocalDialogueSafe();
            onError?.Invoke(voiceRound.error ?? "Voice 请求失败");
            yield return PresentRemiReplyDisplay(localDialogue, "Neutral", null, onSuccess, onRevealText);
            yield break;
        }

        string voiceText = voiceRound.displayText;
        string expression = "Neutral";
        ModelReplyPayload payload = voiceRound.payload ?? new ModelReplyPayload();
        payload.speech = voiceText;
        payload.expression = expression;

        ctx.SetPromptChannel(RemiPromptChannel.Intent);
        string intentExpr = null;
        string intentError = null;
        string intentSystemForLog = null;
        string intentUserForLog = null;

        if (!TryResolveRemi())
        {
            intentError = "未找到 Remi";
        }
        else
        {
            intentSystemForLog = remi.GetFinalSystemPrompt();
            intentUserForLog = RemiPromptComposer.BuildIntentUserPrompt(
                "（系统触发，无玩家本条输入）",
                voiceText);
            MaybeLogVoiceIntentPair(
                voiceSystemForLog,
                "（触发生成）",
                intentSystemForLog,
                intentUserForLog,
                characterTriggered: true);

            yield return CoCompleteRaw(
                intentSystemForLog,
                intentUserForLog,
                content => { intentExpr = ParseExpressionFromIntentContent(content); },
                err => { intentError = err; },
                temperature: 0.3f);
        }

        if (intentSystemForLog == null)
            MaybeLogVoiceIntentPair(
                voiceSystemForLog,
                "（触发生成）",
                intentSystem: null,
                intentUser: null,
                characterTriggered: true);

        if (!string.IsNullOrEmpty(intentExpr))
        {
            expression = intentExpr;
            payload.expression = expression;
        }
        else if (!string.IsNullOrEmpty(intentError))
        {
            Debug.LogWarning($"[Seek] System Intent 失败，expression=Neutral：{intentError}");
        }

        LogVoiceIntentReply(voiceText, expression);

        ctx.SetPromptChannel(RemiPromptChannel.Voice);

        RemiPromptTurnKind turnKind = ctx.CurrentTurnKind;
        RemiDialogueEmphasisSpec directorEmphasis = ctx.GetTurnEmphasisSpec();
        string payloadEmphasis = payload != null ? payload.emphasis : null;
        voiceText = RemiDialogueEmphasis.FormatSpeechForTurn(
            voiceText,
            turnKind,
            payloadEmphasis,
            directorEmphasis);

        yield return PresentRemiReplyDisplay(voiceText, expression, payload, onSuccess, onRevealText);
    }

    private IEnumerator SendMessageInternal(
        string userInput,
        bool recordUserInHistory,
        bool recordAssistantInHistory,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onError,
        System.Action<string> onRevealText = null)
    {
        var round = new ChatRoundResult();
        yield return CoChatRound(
            userInput,
            recordUserInHistory,
            recordAssistantInHistory,
            temperature: 0.7f,
            round);

        if (!round.ok)
        {
            if (round.error != null && round.error.Contains("未找到 Remi"))
            {
                Debug.LogError(
                    "[DeepSeekDialogueManager] remi 为空：场景内需有挂载 Remi 组件的角色，或在 DeepSeekDialogueManager Inspector 中拖入 Remi。");
                onError?.Invoke(round.error);
                yield break;
            }

            string localDialogue = GetLocalDialogueSafe();
            onError?.Invoke($"{round.error ?? "API请求失败"}，已使用本地兜底台词");
            yield return PresentRemiReplyDisplay(localDialogue, "Neutral", null, onSuccess, onRevealText);
            yield break;
        }

        string displayText = round.displayText;
        string expression = round.expression;
        ModelReplyPayload payload = round.payload;

        RemiPromptTurnKind turnKind = PromptContextManager.Instance != null
            ? PromptContextManager.Instance.CurrentTurnKind
            : (recordUserInHistory ? RemiPromptTurnKind.PlayerChat : RemiPromptTurnKind.CharacterTriggered);
        RemiDialogueEmphasisSpec directorEmphasis = PromptContextManager.Instance != null
            ? PromptContextManager.Instance.GetTurnEmphasisSpec()
            : RemiDialogueEmphasisSpec.None;
        string payloadEmphasis = payload != null ? payload.emphasis : null;
        displayText = RemiDialogueEmphasis.FormatSpeechForTurn(
            displayText,
            turnKind,
            payloadEmphasis,
            directorEmphasis);

        yield return PresentRemiReplyDisplay(displayText, expression, payload, onSuccess, onRevealText);
    }

    private DialogueSequenceDirector ResolveSequenceDirector()
    {
        if (utteranceSequenceDirector != null)
            return utteranceSequenceDirector;
        return DialogueSequenceDirector.Instance;
    }

    private RemiDialoguePresentationMode ResolvePresentationMode()
    {
        DialogueSequenceDirector dir = ResolveSequenceDirector();
        if (dir != null)
            return dir.ResolveMode();
        return RemiDialoguePresentationMode.TextTypewriterNoVoice;
    }

    private float ResolveTypewriterCps()
    {
        DialogueSequenceDirector dir = ResolveSequenceDirector();
        if (dir != null)
            return dir.TypewriterCharsPerSecond;
        return typewriterCharsPerSecondFallback;
    }

    /// <summary>按 <see cref="DialogueSequenceDirector"/> 呈现模式显示 Remi 回复（Demo 无 TTS）。</summary>
    private IEnumerator PresentRemiReplyDisplay(
        string text,
        string expression,
        ModelReplyPayload payload,
        System.Action<string, string, ModelReplyPayload> onSuccess,
        System.Action<string> onRevealText)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onSuccess?.Invoke(string.Empty, expression, payload);
            yield break;
        }

        RemiDialoguePresentationMode mode = ResolvePresentationMode();
        string trimmed = text.Trim();

        switch (mode)
        {
            case RemiDialoguePresentationMode.TextInstantNoVoice:
                onRevealText?.Invoke(trimmed);
                break;

            default:
                yield return CoRevealTextOnly(trimmed, onRevealText, ResolveTypewriterCps(), useTypewriter: true);
                break;
        }

        onSuccess?.Invoke(text, expression, payload);
    }

    private IEnumerator CoRevealTextOnly(
        string text,
        System.Action<string> onReveal,
        float charsPerSecond,
        bool useTypewriter)
    {
        if (onReveal == null)
            yield break;

        if (!useTypewriter)
        {
            onReveal.Invoke(text);
            yield break;
        }

        yield return CoRevealSegmentFixedCps(text, string.Empty, onReveal, 0, charsPerSecond);
    }

    private IEnumerator CoRevealSegmentFixedCps(
        string segment,
        string prefix,
        System.Action<string> onReveal,
        int alreadyShown,
        float charsPerSecond)
    {
        charsPerSecond = Mathf.Max(0.01f, charsPerSecond);
        float interval = 1f / charsPerSecond;
        for (int i = alreadyShown + 1; i <= segment.Length; i++)
        {
            onReveal?.Invoke(prefix + segment.Substring(0, i));
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void ParseStructuredAIReply(
        string response,
        out string displayText,
        out string expression,
        out ModelReplyPayload parsedPayload,
        bool logParseResult = true)
    {
        displayText = GetLocalDialogueSafe();
        expression = "Neutral";
        parsedPayload = null;

        try
        {
            SeekResponse SeekResponse = JsonMapper.ToObject<SeekResponse>(response);

            // 2) 逐层空引用校验（核心！避免NullReferenceException）
            if (SeekResponse == null)
            {
                Debug.LogError("解析后SeekResponse为null");
                return;
            }
            if (SeekResponse.choices == null || SeekResponse.choices.Count == 0)
            {
                Debug.LogError("解析后choices为空列表");
                return;
            }
            SeekChoice firstChoice = SeekResponse.choices[0];
            if (firstChoice == null || firstChoice.message == null)
            {
                Debug.LogError("第一个choice或message为null");
                return;
            }

            // 3) 提取回复内容（空值兜底）
            string rawContent = firstChoice.message.content ?? string.Empty;
            rawContent = rawContent.Trim();
            if (string.IsNullOrEmpty(rawContent))
            {
                return;
            }

            // 4) 清洗常见“思考/标签”内容（借鉴 Starry 的思路）
            rawContent = RemoveThoughtMarkers(rawContent).Trim();
            if (string.IsNullOrEmpty(rawContent))
            {
                return;
            }

            // 5) 如果不是以 '{' 开头，就当成纯文本回复
            if (!rawContent.StartsWith("{"))
            {
                displayText = rawContent;
                if (logParseResult)
                    Debug.Log($"[Seek] 非 JSON 开头，按纯文本处理 expression={expression}, text={displayText}");
                return;
            }

            // 6. 查找首个完整 JSON 块：从开头的 '{' 到匹配的第一个 '}'
            int braceDepth = 0;
            int jsonEndIndex = -1;
            for (int i = 0; i < rawContent.Length; i++)
            {
                char c = rawContent[i];
                if (c == '{') braceDepth++;
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        jsonEndIndex = i;
                        break;
                    }
                }
            }

            if (jsonEndIndex < 0)
            {
                // 没找到完整 JSON，整体按纯文本
                displayText = rawContent;
                if (logParseResult)
                    Debug.LogWarning($"未找到完整 JSON 块，按纯文本处理：{rawContent}");
                return;
            }

            string jsonBlock = rawContent.Substring(0, jsonEndIndex + 1).Trim();
            string textPart = rawContent.Substring(jsonEndIndex + 1).Trim();

            try
            {
                parsedPayload = JsonMapper.ToObject<ModelReplyPayload>(jsonBlock);
                if (parsedPayload != null)
                {
                    if (!string.IsNullOrEmpty(parsedPayload.expression))
                        expression = parsedPayload.expression;

                    string jsonSpeech = !string.IsNullOrEmpty(parsedPayload.speech)
                        ? parsedPayload.speech
                        : parsedPayload.text;
                    if (string.IsNullOrEmpty(textPart) && !string.IsNullOrEmpty(jsonSpeech))
                        displayText = jsonSpeech;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"解析 JSON 块失败，将使用默认表情和文本：{e.Message} | jsonBlock：{jsonBlock}");
            }

            if (!string.IsNullOrEmpty(textPart))
                displayText = textPart;

            if (logParseResult)
                Debug.Log($"[Seek] 解析完成 expression={expression}, text={displayText}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析AI回复失败 → 异常类型：{e.GetType().Name} | 异常信息：{e.Message} | 堆栈：{e.StackTrace}");
            // 出错时沿用默认兜底
            displayText = GetLocalDialogueSafe();
            expression = "Neutral";
        }
    }

    /// <summary>Voice / Voice+Intent 合成后打印（台词 + expression）。</summary>
    private static void LogVoiceIntentReply(string speech, string expression, bool voiceOnly = false)
    {
        string expr = string.IsNullOrWhiteSpace(expression) ? "Neutral" : expression.Trim();
        string line = speech ?? "";
        string json =
            "{\"speech\":\"" + EscapeJsonForLog(line) + "\",\"expression\":\"" + EscapeJsonForLog(expr) + "\"}";
        string tag = voiceOnly ? "Voice" : "Voice+Intent";
        Debug.Log($"[Seek] 解析完成（{tag}）\n{json}\n{line}");
    }

    private static string EscapeJsonForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    // 移除模型可能返回的思考/标签块（例如 <think>...</think>）
    private static string RemoveThoughtMarkers(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        // 兼容大小写与跨行
        return Regex.Replace(content, @"<think>[\s\S]*?</think>\s*", "", RegexOptions.IgnoreCase);
    }

    /// <summary>重置「玩家阶段首次打印」追踪（调试 / 新游戏）。</summary>
    public void ResetPromptLogTracking() => _playerPromptLoggedStage = null;

    private void MaybeLogLlmRequest(string systemPrompt, string userInput, bool isPlayerTurn)
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        RemiPromptTurnKind turnKind = ctx != null
            ? ctx.CurrentTurnKind
            : (isPlayerTurn ? RemiPromptTurnKind.PlayerChat : RemiPromptTurnKind.CharacterTriggered);

        if (turnKind == RemiPromptTurnKind.CharacterTriggered)
        {
            LogFullLlmRequest("character_triggered", systemPrompt, userInput);
            return;
        }

        if (!isPlayerTurn)
            return;

        RemiDialogueDepthStage stage = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.DialogueDepthStage
            : RemiDialogueDepthStage.Surface;

        if (_playerPromptLoggedStage.HasValue && _playerPromptLoggedStage.Value == stage)
            return;

        _playerPromptLoggedStage = stage;
        LogFullLlmRequest($"player_chat·{RemiInteractionRhythm.StageDisplayName(stage)}", systemPrompt, userInput);
    }

    /// <summary>
    /// Voice+Intent 同轮：合并进同一次 Debug.Log。
    /// player_chat：仍按关系档首次打印；character_triggered（SendSystem）：每次都打印。
    /// </summary>
    private void MaybeLogVoiceIntentPair(
        string voiceSystem,
        string playerInput,
        string intentSystem,
        string intentUser,
        bool characterTriggered = false)
    {
        string triggerLabel;
        if (characterTriggered)
        {
            triggerLabel = "character_triggered·Voice+Intent";
        }
        else
        {
            RemiDialogueDepthStage stage = RemiPresenceService.Instance != null
                ? RemiPresenceService.Instance.DialogueDepthStage
                : RemiDialogueDepthStage.Surface;

            if (_playerPromptLoggedStage.HasValue && _playerPromptLoggedStage.Value == stage)
                return;

            _playerPromptLoggedStage = stage;
            triggerLabel = "player_chat·Voice+Intent·" + RemiInteractionRhythm.StageDisplayName(stage);
        }

        var sb = new StringBuilder(2048);
        sb.Append("[Remi LLM] 完整请求（trigger=").Append(triggerLabel).Append("）\n");
        sb.Append("=== VOICE ===\n");
        sb.Append("--- user ---\n").Append(playerInput ?? "").Append('\n');
        sb.Append("--- system ---\n").Append(voiceSystem ?? "").Append('\n');

        if (!string.IsNullOrEmpty(intentSystem) || !string.IsNullOrEmpty(intentUser))
        {
            sb.Append("=== INTENT ===\n");
            sb.Append("--- user ---\n").Append(intentUser ?? "").Append('\n');
            sb.Append("--- system ---\n").Append(intentSystem ?? "");
        }
        else
        {
            sb.Append("=== INTENT ===\n（未发出：Voice 失败或未解析 Remi）");
        }

        Debug.Log(sb.ToString());
    }

    private static void LogFullLlmRequest(string trigger, string systemPrompt, string userInput)
    {
        Debug.Log(
            $"[Remi LLM] 完整请求（trigger={trigger}）\n" +
            $"--- user ---\n{userInput}\n" +
            $"--- system ---\n{systemPrompt}");
    }

    private RemiInteractionChannel ResolveHistoryChannel()
    {
        if (RemiPresenceService.Instance != null)
            return RemiPresenceService.Instance.CurrentChannel;
        return RemiInteractionChannel.FaceToFace;
    }

    private List<ChatMessage> GetActiveHistoryList() =>
        ResolveHistoryChannel() == RemiInteractionChannel.Social
            ? _socialSessionHistory
            : messageHistory;

    private int GetActiveHistoryCap()
    {
        if (maxFaceHistoryCount > 0 && maxSocialHistoryCount > 0)
        {
            return ResolveHistoryChannel() == RemiInteractionChannel.Social
                ? maxSocialHistoryCount
                : maxFaceHistoryCount;
        }

        return maxHistoryCount;
    }

    private static void TrimHistoryList(List<ChatMessage> list, int cap)
    {
        if (cap <= 0 || list.Count <= cap) return;
        list.RemoveRange(0, list.Count - cap);
    }

    /// <summary>当前面对面 LLM 对话记录副本。</summary>
    public List<ChatMessage> GetMessageHistory() => new List<ChatMessage>(messageHistory);

    public List<ChatMessage> GetFaceMessageHistory() => GetMessageHistory();

    public List<ChatMessage> GetSocialMessageHistory() => new List<ChatMessage>(_socialSessionHistory);

    /// <summary>清空面对面 LLM history（落盘）。</summary>
    public void ClearSessionMessageHistory()
    {
        messageHistory.Clear();
        PersistMessageHistoryToDisk();
    }

    /// <summary>面对面会话结束：采集片段后清空 history。</summary>
    public void EndSessionCaptureAndClear(RemiInteractionChannel channel)
    {
        if (channel == RemiInteractionChannel.Social)
        {
            EndSocialSessionCaptureAndClear();
            return;
        }

        RemiChatFragmentCapture.TryCaptureSession(channel, messageHistory);
        ClearSessionMessageHistory();
    }

    /// <summary>面对面 session 结束（goodbye）时调用。</summary>
    public void ClearFaceMessageHistory() =>
        EndSessionCaptureAndClear(RemiInteractionChannel.FaceToFace);

    /// <summary>关闭手机：仅清社媒会话缓冲，不影响面对面 history。</summary>
    public void ClearSocialMessageHistory() => EndSocialSessionCaptureAndClear();

    private void EndSocialSessionCaptureAndClear()
    {
        RemiChatFragmentCapture.TryCaptureSession(
            RemiInteractionChannel.Social,
            _socialSessionHistory);
        _socialSessionHistory.Clear();
    }

    /// <summary>调试 / 新游戏：清空面对面 LLM history（不采集片段）。</summary>
    public void ClearMessageHistory()
    {
        messageHistory.Clear();
        _socialSessionHistory.Clear();
        PersistMessageHistoryToDisk();
        ResetPromptLogTracking();
    }

    /// <summary>将面对面 history 写入磁盘。</summary>
    public void PersistMessageHistoryToDisk()
    {
        if (JsonMgr.Instance == null)
            return;
        JsonMgr.Instance.SaveData(new List<ChatMessage>(messageHistory), MessageHistorySaveKey);
    }

    /// <summary>从磁盘加载面对面 history（读档 / 冷启动）。</summary>
    public void LoadMessageHistoryFromDisk()
    {
        messageHistory.Clear();
        if (JsonMgr.Instance == null)
            return;

        List<ChatMessage> loaded = JsonMgr.Instance.LoadData<List<ChatMessage>>(MessageHistorySaveKey);
        if (loaded == null || loaded.Count == 0)
            return;

        for (int i = 0; i < loaded.Count; i++)
        {
            ChatMessage m = loaded[i];
            if (m == null || string.IsNullOrWhiteSpace(m.role))
                continue;
            messageHistory.Add(new ChatMessage(m.role.Trim(), m.content ?? string.Empty));
        }
    }

    /// <summary>用外部列表替换面对面 history 并落盘（日起点读档）。</summary>
    public void ReplaceMessageHistory(IList<ChatMessage> messages)
    {
        messageHistory.Clear();
        if (messages != null)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                ChatMessage m = messages[i];
                if (m == null || string.IsNullOrWhiteSpace(m.role))
                    continue;
                messageHistory.Add(new ChatMessage(m.role.Trim(), m.content ?? string.Empty));
            }
        }

        PersistMessageHistoryToDisk();
    }

    // 兼容旧接口（只关心文本，不关心 expression）
    public IEnumerator SendMessageToAI(string userInput, System.Action<string> onSuccess, System.Action<string> onError)
    {
        return SendMessageWithEmotion(
            userInput,
            (text, expr) => { onSuccess?.Invoke(text); },
            onError,
            null);
    }

}