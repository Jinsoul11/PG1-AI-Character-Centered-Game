using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// 日结固化 Fragment 话题别名：从 evidence / quote / summary 抽取，供 Voice 别名强命中。
/// </summary>
public static class RemiFragmentTopicAliasBuilder
{
    public const int MinAliasLength = 2;
    public const int MaxAliasLength = 16;
    public const int MaxAliases = 15;

    private static readonly HashSet<string> GenericBlacklist = new HashSet<string>(StringComparer.Ordinal)
    {
        "嗯", "啊", "哦", "呃", "哈", "哈哈", "哈哈哈", "好的", "好", "对", "是的", "是",
        "没有", "不是", "不知道", "可能", "也许", "大概", "然后", "就是", "那个", "这个",
        "什么", "怎么", "为什么", "可以", "不行", "谢谢", "没事", "好吧", "算了",
        "玩家", "remi", "对方", "今天", "昨天", "明天", "一下", "一点", "真的", "其实",
    };

    private static readonly Regex SplitPattern = new Regex(
        @"[\s,，。！？!?、；;：:\.\-\(\)（）""'\[\]【】]+",
        RegexOptions.Compiled);

    public static void ApplyFromUnit(RemiFragmentImpression impression, RemiFragmentUnit unit)
    {
        if (impression == null)
            return;

        var sources = new List<string>();
        if (unit != null)
        {
            if (unit.evidence != null)
            {
                foreach (string e in unit.evidence)
                    AddSource(sources, e);
            }

            AddSource(sources, unit.quoteCandidate);
        }

        AddSource(sources, impression.summary);
        AddSource(sources, impression.quote);

        impression.topicAliases = BuildAliasList(sources);
        impression.recallEligible = impression.topicAliases != null && impression.topicAliases.Count > 0;
    }

    /// <summary>读档旧数据无 aliases 时，从 summary / quote 懒补。</summary>
    public static void EnsureRecallEligible(RemiFragmentImpression impression)
    {
        if (impression == null)
            return;
        if (impression.recallEligible &&
            impression.topicAliases != null &&
            impression.topicAliases.Count > 0)
            return;

        var sources = new List<string>();
        AddSource(sources, impression.summary);
        AddSource(sources, impression.quote);
        impression.topicAliases = BuildAliasList(sources);
        impression.recallEligible = impression.topicAliases != null && impression.topicAliases.Count > 0;
    }

    private static List<string> BuildAliasList(List<string> sources)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new List<string>();

        void TryAdd(string raw)
        {
            string normalized = NormalizeAlias(raw);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (normalized.Length < MinAliasLength || normalized.Length > MaxAliasLength)
                return;
            if (GenericBlacklist.Contains(normalized))
                return;
            if (!seen.Add(normalized))
                return;
            aliases.Add(raw.Trim());
            if (aliases.Count >= MaxAliases)
                return;
        }

        foreach (string source in sources)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            string trimmed = source.Trim();
            TryAdd(trimmed);

            if (aliases.Count >= MaxAliases)
                break;

            string[] parts = SplitPattern.Split(trimmed);
            foreach (string part in parts)
            {
                if (aliases.Count >= MaxAliases)
                    break;
                TryAdd(part);
            }
        }

        return aliases;
    }

    private static void AddSource(List<string> list, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        list.Add(text.Trim());
    }

    private static string NormalizeAlias(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        return text.Trim().ToLowerInvariant().Replace(" ", "").Replace("　", "");
    }
}
