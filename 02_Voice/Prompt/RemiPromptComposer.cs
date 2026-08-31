using System.Collections.Generic;

/// <summary>
/// Remi System Prompt 编排：共享人格段 + 分轨 CONTRACT（Player / System / Ending）× 通道（Voice / Intent）。
/// </summary>
public static class RemiPromptComposer
{
    private const string PlayerContractVoice =
        "[CONTRACT · PLAYER · VOICE]\n" +
        "mode: player_chat\n" +
        "channel: voice\n" +
        "role: 以 Remi 身份回应玩家本轮聊天。\n" +
        "output: 只输出对玩家说的自然语言台词\n" +
        "rules:\n" +
        "  - 语气随 [CHARACTER] 与 [RELATIONSHIP]\n" +
        "  - 可引用本轮 [ACTIVE_KNOWLEDGE] / [ACTIVE_MEMORY]（若有)\n" +
        "  - 若有 [INTENT]，必须落实其中的当轮裁定（如接受偏离时的态度），不要否认已裁定事实\n";

    private const string PlayerContractIntent =
        "[CONTRACT · PLAYER · INTENT]\n" +
        "mode: player_chat\n" +
        "channel: intent\n" +
        "role: 根据玩家输入与 Remi 已说出的台词，选择Remi当轮面部表情。\n" +
        "output: 只输出一行 JSON。\n" +
        "json: {\"expression\":\"Neutral\"}\n" +
        "fields:\n" +
        "  expression: Happy / Angry / Sad / Surprise / Shy / Neutral\n" +
        "rules:\n" +
        "  - 默认 Neutral；仅在台词与情境有明显情绪时选用其它值";

    private const string SystemContractVoice =
        "[CONTRACT · SYSTEM · VOICE]\n" +
        "mode: character_triggered\n" +
        "channel: voice\n" +
        "role: Remi 本轮先开口（导演/剧情触发；user 仅为占位，无玩家本条输入）。\n" +
        "output: 只输出对玩家说的自然语言台词\n" +
        "rules:\n" +
        "  - 必须落实 [TURN] director_context\n" +
        "  - 语气随 [CHARACTER] 与 [RELATIONSHIP]\n" +
        "  - 可引用本轮 [ACTIVE_KNOWLEDGE] / [ACTIVE_MEMORY]（若有）\n";

    private const string EndingSpeakContractVoice =
        "[CONTRACT · ENDING · VOICE]\n" +
        "mode: character_triggered\n" +
        "channel: voice\n" +
        "role: Remi 本轮先开口（导演/剧情触发；user 仅为占位，无玩家本条输入）。\n" +
        "output: 只输出对玩家说的自然语言台词；不要 JSON、不要 markdown、不要标签、不要解释。\n" +
        "rules:\n" +
        "  - 必须落实 [TURN] director_context\n" +
        "  - 不要输出 expression 或任何结构化字段\n";

    public static string BuildContractBlock(
        RemiPromptTurnKind turnKind,
        RemiPromptChannel channel = RemiPromptChannel.Voice)
    {
        if (turnKind == RemiPromptTurnKind.CharacterTriggered)
            return SystemContractVoice;

        return channel == RemiPromptChannel.Intent
            ? PlayerContractIntent
            : PlayerContractVoice;
    }

    /// <summary>Intent 通道 user 消息：玩家输入 + Voice 已产出台词。</summary>
    public static string BuildIntentUserPrompt(string playerText, string voiceSpeech)
    {
        return
            "本轮玩家输入：\n" +
            (playerText ?? "").Trim() +
            "\n\nRemi 已对玩家说出的台词：\n" +
            (voiceSpeech ?? "").Trim() +
            "\n\n请只输出一行 JSON：{\"expression\":\"Neutral\"}";
    }

    /// <summary>拼接完整 API system 消息。</summary>
    public static string BuildFullSystemPrompt(
        RemiPromptTurnKind turnKind,
        string characterBlock,
        string dynamicContext,
        string narrativeIntentBlock,
        RemiPromptAssemblyMode assemblyMode = RemiPromptAssemblyMode.Standard,
        RemiPromptChannel channel = RemiPromptChannel.Voice)
    {
        // SendSystem / Ending：只用 director_context，不拼独立 [INTENT] 文案段。
        bool includeNarrativeIntent = turnKind != RemiPromptTurnKind.CharacterTriggered;

        if (assemblyMode == RemiPromptAssemblyMode.EndingSpeak)
        {
            var endingParts = new List<string> { EndingSpeakContractVoice };
            if (!string.IsNullOrWhiteSpace(dynamicContext))
                endingParts.Add(dynamicContext.TrimEnd());
            return string.Join("\n", endingParts);
        }

        // Intent：仅 CONTRACT。
        if (channel == RemiPromptChannel.Intent)
            return PlayerContractIntent;

        // Voice：CONTRACT → CHARACTER → RELATIONSHIP/CURRENT/ACTIVE_*（+ System 的 TURN）
        string voiceContract = turnKind == RemiPromptTurnKind.CharacterTriggered
            ? SystemContractVoice
            : PlayerContractVoice;
        var voiceParts = new List<string>
        {
            voiceContract,
            characterBlock,
        };
        if (!string.IsNullOrWhiteSpace(dynamicContext))
            voiceParts.Add(dynamicContext.TrimEnd());
        if (includeNarrativeIntent && !string.IsNullOrWhiteSpace(narrativeIntentBlock))
            voiceParts.Add(narrativeIntentBlock.TrimEnd());
        return string.Join("\n", voiceParts);
    }
}
