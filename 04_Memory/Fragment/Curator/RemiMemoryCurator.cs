using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;
using UnityEngine;

/// <summary>
/// Memory Curator（Pipeline 第三段）：LLM 理解「今天哪些交流值得成为未来的回忆」。
/// 输入 = Candidate Filter 产出；输出 = Fragment Candidate（写入 CuratorStore）。
/// 不合并 Analyzer，不写 Fragment Unit / Fragment Memory。
/// </summary>
[DisallowMultipleComponent]
public class RemiMemoryCurator : MonoBehaviour
{
    public static RemiMemoryCurator Instance { get; private set; }

    [SerializeField] private DeepSeekDialogueManager dialogueManager;
    [SerializeField] [Range(1, 8)] private int maxCandidatesPerDay = 3;
    [SerializeField] private float temperature = 0.3f;

    private bool _running;

    public bool IsRunning => _running;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiMemoryCurator));
        go.AddComponent<RemiMemoryCurator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RemiMemoryCuratorStore.EnsureExists();
        if (dialogueManager == null)
            dialogueManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();
    }

    /// <summary>对指定叙事日跑 Curator（先 Filter，再 LLM）。</summary>
    public IEnumerator CoCurateStoryDay(
        int storyDay,
        System.Action<RemiMemoryCuratorDayResult> onDone = null)
    {
        if (_running)
        {
            Debug.LogWarning("[RemiMemoryCurator] 已有策展任务在跑，跳过。");
            yield break;
        }

        _running = true;
        RemiMemoryCuratorDayResult result = null;
        try
        {
            yield return CoCurateStoryDayInternal(storyDay, r => result = r);
        }
        finally
        {
            _running = false;
        }

        onDone?.Invoke(result);
    }

    private IEnumerator CoCurateStoryDayInternal(
        int storyDay,
        System.Action<RemiMemoryCuratorDayResult> onDone)
    {
        var result = new RemiMemoryCuratorDayResult
        {
            storyDay = storyDay,
            curatedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        List<RemiDialogueArchiveEntry> filtered =
            RemiDialogueCandidateFilter.SelectCandidatesForStoryDay(storyDay);
        result.inputCandidateCount = filtered.Count;

        if (filtered.Count == 0)
        {
            result.success = true;
            result.error = "";
            RemiMemoryCuratorStore.EnsureExists();
            RemiMemoryCuratorStore.Instance?.UpsertDay(result);
            Debug.Log($"[RemiMemoryCurator] Day {storyDay}: Filter 无候选，跳过 LLM。");
            onDone?.Invoke(result);
            yield break;
        }

        if (dialogueManager == null)
            dialogueManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();

        if (dialogueManager == null)
        {
            result.success = false;
            result.error = "DeepSeekDialogueManager 未找到";
            RemiMemoryCuratorStore.EnsureExists();
            RemiMemoryCuratorStore.Instance?.UpsertDay(result);
            onDone?.Invoke(result);
            yield break;
        }

        string systemPrompt = BuildSystemPrompt(maxCandidatesPerDay);
        string userPrompt = BuildUserPrompt(storyDay, filtered);

        string raw = null;
        string error = null;
        yield return dialogueManager.CoCompleteRaw(
            systemPrompt,
            userPrompt,
            text => raw = text,
            err => error = err,
            temperature);

        result.rawResponse = raw ?? "";

        if (!string.IsNullOrEmpty(error))
        {
            result.success = false;
            result.error = error;
            RemiMemoryCuratorStore.EnsureExists();
            RemiMemoryCuratorStore.Instance?.UpsertDay(result);
            Debug.LogWarning($"[RemiMemoryCurator] Day {storyDay} LLM 失败: {error}");
            onDone?.Invoke(result);
            yield break;
        }

        if (!TryParseCandidates(raw, out List<RemiMemoryCuratorCandidate> parsed, out string parseError))
        {
            result.success = false;
            result.error = parseError ?? "JSON 解析失败";
            RemiMemoryCuratorStore.EnsureExists();
            RemiMemoryCuratorStore.Instance?.UpsertDay(result);
            Debug.LogWarning($"[RemiMemoryCurator] Day {storyDay} 解析失败: {result.error}\n{raw}");
            onDone?.Invoke(result);
            yield break;
        }

        foreach (RemiMemoryCuratorCandidate c in parsed)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.summary))
                continue;
            c.summary = RemiFragmentSummarySanitize.ReplaceAmbiguousOtherParty(c.summary.Trim());
            result.candidates.Add(c);
            if (result.candidates.Count >= maxCandidatesPerDay)
                break;
        }

        result.success = true;
        RemiMemoryCuratorStore.EnsureExists();
        RemiMemoryCuratorStore.Instance?.UpsertDay(result);
        Debug.Log(
            $"[RemiMemoryCurator] Day {storyDay}: input={filtered.Count} → " +
            $"candidates={result.candidates.Count} (max={maxCandidatesPerDay})");
        onDone?.Invoke(result);
    }

    private static string BuildSystemPrompt(int maxCandidates)
    {
        return
            "你是关系向游戏的 Memory Curator（策展人）。\n" +
            "任务：从给定的当日玩家发言候选中，判断哪些交流值得成为未来的回忆。\n" +
            //"你只做「理解 / 提出候选记忆」，不做最终分类定案，不写给玩家看的文学旁白。\n" +
            "原则：\n" +
            "- 优先：自我揭露、情绪峰值、独特观点、可形成印象的瞬间。\n" +
            "- 忽略：寒暄、无信息附和、纯测试句。\n" +
            "- summary 必须是重述（第三人称）；指人只用「玩家」或「Remi」，禁止用「对方」。\n" +
            "- evidence 引用候选中的原句（可截断），仅供内部。\n" +
            "- candidateTags 只能从：Identity, Moment, Resonance, Relation, Atmosphere中选。\n" +
            $"- 最多输出 {maxCandidates} 条；若都不值得，返回空数组。\n" +
            "- 只输出一个 JSON 对象，不要 Markdown，不要其它文字。\n" +
            "格式：\n" +
            "{\"candidates\":[{\"summary\":\"...\",\"reason\":\"...\",\"confidence\":0.0," +
            "\"evidence\":[\"...\"],\"candidateTags\":[\"Identity\",\"Moment\"]}]}";
    }

    private static string BuildUserPrompt(int storyDay, List<RemiDialogueArchiveEntry> filtered)
    {
        var sb = new StringBuilder(512);
        sb.Append("storyDay=").Append(storyDay).Append('\n');
        sb.Append("candidates_from_filter:\n");
        for (int i = 0; i < filtered.Count; i++)
        {
            RemiDialogueArchiveEntry e = filtered[i];
            if (e == null)
                continue;
            sb.Append(i + 1).Append(". [").Append(e.ChannelKind).Append("] ");
            sb.Append(e.content?.Trim() ?? "").Append('\n');
        }

        sb.Append("\n请策展并只返回 JSON。");
        return sb.ToString();
    }

    public static bool TryParseCandidates(
        string raw,
        out List<RemiMemoryCuratorCandidate> candidates,
        out string error)
    {
        candidates = new List<RemiMemoryCuratorCandidate>();
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "空响应";
            return false;
        }

        string json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "未找到 JSON 对象";
            return false;
        }

        try
        {
            JsonData root = JsonMapper.ToObject(json);
            if (root == null || !root.IsObject)
            {
                error = "根节点不是对象";
                return false;
            }

            if (!root.ContainsKey("candidates") || root["candidates"] == null)
            {
                // 允许空策展
                return true;
            }

            JsonData arr = root["candidates"];
            if (!arr.IsArray)
            {
                error = "candidates 不是数组";
                return false;
            }

            for (int i = 0; i < arr.Count; i++)
            {
                JsonData item = arr[i];
                if (item == null || !item.IsObject)
                    continue;

                var c = new RemiMemoryCuratorCandidate
                {
                    summary = ReadString(item, "summary"),
                    reason = ReadString(item, "reason"),
                    confidence = ReadFloat(item, "confidence"),
                };
                ReadStringArray(item, "evidence", c.evidence);
                ReadStringArray(item, "candidateTags", c.candidateTags);
                NormalizeTags(c.candidateTags);
                if (!string.IsNullOrWhiteSpace(c.summary))
                    candidates.Add(c);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void NormalizeTags(List<string> tags)
    {
        if (tags == null)
            return;
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (!RemiChatFragmentTagRules.TryParse(tags[i], out RemiChatFragmentTag tag))
            {
                tags.RemoveAt(i);
                continue;
            }

            tags[i] = RemiChatFragmentTagRules.ToKey(tag);
        }
    }

    private static string ReadString(JsonData obj, string key)
    {
        if (obj == null || !obj.ContainsKey(key) || obj[key] == null)
            return "";
        return obj[key].ToString();
    }

    private static float ReadFloat(JsonData obj, string key)
    {
        string s = ReadString(obj, key);
        if (float.TryParse(s, out float v))
            return Mathf.Clamp01(v);
        return 0f;
    }

    private static void ReadStringArray(JsonData obj, string key, List<string> target)
    {
        if (target == null || obj == null || !obj.ContainsKey(key) || obj[key] == null)
            return;
        JsonData arr = obj[key];
        if (!arr.IsArray)
            return;
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] == null)
                continue;
            string s = arr[i].ToString();
            if (!string.IsNullOrWhiteSpace(s))
                target.Add(s.Trim());
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        string text = raw.Trim();
        Match fence = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fence.Success)
            text = fence.Groups[1].Value.Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        return text.Substring(start, end - start + 1);
    }
}
