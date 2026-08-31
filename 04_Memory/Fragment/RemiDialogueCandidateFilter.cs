using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Candidate Filter（系统降噪）：决定「有没有必要送给 Curator」，不做「值不值得记住」判断。
/// 纯规则，无 AI。
/// </summary>
public static class RemiDialogueCandidateFilter
{
    public const int DefaultMinContentLength = 4;

    /// <summary>筛选结果：通过的 Archive 条目 + 通过原因（调试）。</summary>
    public struct Candidate
    {
        public RemiDialogueArchiveEntry Entry;
        public string KeepReason;
    }

    /// <summary>被拒原因（调试）。</summary>
    public struct Rejection
    {
        public RemiDialogueArchiveEntry Entry;
        public string Reason;
    }

    public sealed class FilterResult
    {
        public readonly List<Candidate> Kept = new List<Candidate>();
        public readonly List<Rejection> Rejected = new List<Rejection>();

        public int KeptCount => Kept.Count;
        public int RejectedCount => Rejected.Count;
    }

    private static readonly string[] ExactNoisePhrases =
    {
        "谢谢", "感谢", "多谢", "谢了",
        "收到", "好的", "好", "嗯", "嗯嗯", "哦", "噢", "喔",
        "ok", "OK", "Ok", "okay", "Okay",
        "哈哈", "哈哈哈", "哈哈哈哈", "嘿嘿", "呵呵",
        "在吗", "你好", "早上好", "晚安", "拜拜", "再见",
        "今天星期几", "几点了", "现在几点",
        "测试", "test", "Test",
        "1", "？", "?", "。", ".",
    };

    private static readonly Regex PureLaughter = new Regex(
        @"^[哈嘿呵嘻呜哇啊哦噢喔额嗯哟]+$",
        RegexOptions.Compiled);

    private static readonly Regex PurePunctuationOrEmoji = new Regex(
        @"^[\s\p{P}\p{S}]+$",
        RegexOptions.Compiled);

    /// <summary>对指定叙事日跑降噪；默认只保留玩家 FreeChat。</summary>
    public static FilterResult FilterStoryDay(
        int storyDay,
        bool playerFreeChatOnly = true,
        int minContentLength = DefaultMinContentLength)
    {
        RemiDialogueArchive.EnsureExists();
        IReadOnlyList<RemiDialogueArchiveEntry> dayEntries =
            RemiDialogueArchive.Instance != null
                ? RemiDialogueArchive.Instance.GetEntriesForStoryDay(storyDay)
                : Array.Empty<RemiDialogueArchiveEntry>();

        return FilterEntries(dayEntries, playerFreeChatOnly, minContentLength);
    }

    public static FilterResult FilterEntries(
        IReadOnlyList<RemiDialogueArchiveEntry> entries,
        bool playerFreeChatOnly = true,
        int minContentLength = DefaultMinContentLength)
    {
        var result = new FilterResult();
        if (entries == null || entries.Count == 0)
            return result;

        foreach (RemiDialogueArchiveEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (!TryPass(entry, playerFreeChatOnly, minContentLength, out string keepReason, out string rejectReason))
            {
                result.Rejected.Add(new Rejection { Entry = entry, Reason = rejectReason });
                continue;
            }

            result.Kept.Add(new Candidate { Entry = entry, KeepReason = keepReason });
        }

        return result;
    }

    /// <summary>仅返回通过筛选的条目列表（供日结 Curator 输入）。</summary>
    public static List<RemiDialogueArchiveEntry> SelectCandidatesForStoryDay(
        int storyDay,
        bool playerFreeChatOnly = true,
        int minContentLength = DefaultMinContentLength)
    {
        FilterResult filtered = FilterStoryDay(storyDay, playerFreeChatOnly, minContentLength);
        var list = new List<RemiDialogueArchiveEntry>(filtered.KeptCount);
        foreach (Candidate c in filtered.Kept)
        {
            if (c.Entry != null)
                list.Add(c.Entry);
        }

        return list;
    }

    public static bool TryPass(
        RemiDialogueArchiveEntry entry,
        bool playerFreeChatOnly,
        int minContentLength,
        out string keepReason,
        out string rejectReason)
    {
        keepReason = "";
        rejectReason = "";

        if (entry == null || string.IsNullOrWhiteSpace(entry.content))
        {
            rejectReason = "empty";
            return false;
        }

        if (entry.SourceKind != RemiDialogueArchiveSource.FreeChat)
        {
            rejectReason = "source_not_free_chat";
            return false;
        }

        string speaker = RemiDialogueArchive.NormalizeSpeaker(entry.speaker);
        if (playerFreeChatOnly)
        {
            if (!string.Equals(speaker, "player", StringComparison.OrdinalIgnoreCase))
            {
                rejectReason = "speaker_not_player";
                return false;
            }
        }
        else if (string.Equals(speaker, "system", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(speaker, "Ema", StringComparison.OrdinalIgnoreCase))
        {
            rejectReason = "speaker_excluded";
            return false;
        }

        string text = entry.content.Trim();
        if (text.Length < minContentLength)
        {
            rejectReason = "too_short";
            return false;
        }

        if (IsExactNoise(text))
        {
            rejectReason = "exact_noise";
            return false;
        }

        if (PureLaughter.IsMatch(text))
        {
            rejectReason = "pure_laughter";
            return false;
        }

        if (PurePunctuationOrEmoji.IsMatch(text))
        {
            rejectReason = "pure_punct_or_symbol";
            return false;
        }

        if (IsWeakAck(text))
        {
            rejectReason = "weak_ack";
            return false;
        }

        keepReason = BuildKeepReason(text);
        return true;
    }

    private static bool IsExactNoise(string text)
    {
        foreach (string phrase in ExactNoisePhrases)
        {
            if (string.Equals(text, phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>短附和且几乎无信息量（可含少量标点）。</summary>
    private static bool IsWeakAck(string text)
    {
        string compact = Regex.Replace(text, @"[\s\p{P}]+", "");
        if (compact.Length == 0)
            return true;
        if (compact.Length > 6)
            return false;

        string[] weak =
        {
            "好的", "好啊", "好吧", "行", "行啊", "嗯嗯", "哦哦", "知道了", "了解",
            "没事", "没有", "是的", "不是", "对", "不对", "可以", "不可以",
            "哈哈", "哈哈哈", "收到", "谢谢",
        };
        foreach (string w in weak)
        {
            if (string.Equals(compact, w, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string BuildKeepReason(string text)
    {
        if (text.Length >= 24)
            return "long_text";
        if (ContainsAny(text, "我", "自己", "觉得", "感觉", "其实", "一直", "想做", "适合", "未来", "过去"))
            return "self_or_reflective";
        if (ContainsAny(text, "难过", "开心", "累", "压力", "害怕", "喜欢", "讨厌", "烦", "崩溃"))
            return "emotion_cue";
        if (text.Contains("？") || text.Contains("?"))
            return "question";
        return "contentful";
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (string n in needles)
        {
            if (!string.IsNullOrEmpty(n) && text.Contains(n))
                return true;
        }

        return false;
    }
}
