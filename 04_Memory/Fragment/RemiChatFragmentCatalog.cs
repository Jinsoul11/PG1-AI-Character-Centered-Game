using System.Collections.Generic;

/// <summary>
/// 过程记忆模式表：关键词命中 → 多标签记忆单元（设计师摘要；不默认存原话）。
/// 标签遵循 RemiChatFragmentTagRules（可见 / 仅排序）。
/// </summary>
public static class RemiChatFragmentCatalog
{
    public struct PatternDef
    {
        public string Id;
        public string Summary;
        public string Atmosphere;
        public string BondKeyword;
        public RemiChatFragmentTag[] Tags;
        public string[] Keywords;
    }

    private static readonly PatternDef[] Patterns =
    {
        new PatternDef
        {
            Id = "virtual_world_question",
            Summary = "你总爱把「真实」和「虚拟」拿出来认真掂量，像在确认自己站在哪一边。",
            Atmosphere = "认真",
            BondKeyword = "虚拟世界",
            Tags = new[] { RemiChatFragmentTag.Identity, RemiChatFragmentTag.Moment },
            Keywords = new[] { "虚拟", "真实", "存在", "现实", "世界" },
        },
        new PatternDef
        {
            Id = "ai_consciousness_test",
            Summary = "你会用觉醒、意识这类话试探我——像在弄清楚我到底算不算「一个人」。",
            Atmosphere = "轻松",
            BondKeyword = "觉醒",
            Tags = new[] { RemiChatFragmentTag.Identity, RemiChatFragmentTag.Resonance, RemiChatFragmentTag.Relation },
            Keywords = new[] { "觉醒", "有意识", "像人", "真人", "意识", "AI" },
        },
        new PatternDef
        {
            Id = "playful_meme",
            Summary = "你偶尔会把整活和怪梗丢过来，气氛一下子就轻了。",
            Atmosphere = "轻松",
            BondKeyword = "整活",
            Tags = new[] { RemiChatFragmentTag.Resonance, RemiChatFragmentTag.Atmosphere },
            Keywords = new[] { "外星人", "梗", "整活", "V我50", "总督", "测试" },
        },
        new PatternDef
        {
            Id = "gentle_emotional",
            Summary = "你也会把难过、累、压力这类话认真说出来——至少在我面前，你不是只会开玩笑。",
            Atmosphere = "认真",
            BondKeyword = "心情",
            Tags = new[] { RemiChatFragmentTag.Identity, RemiChatFragmentTag.Moment, RemiChatFragmentTag.Atmosphere },
            Keywords = new[] { "难过", "开心", "累", "压力", "崩溃", "睡不着", "烦" },
        },
        new PatternDef
        {
            Id = "relation_lean_in",
            Summary = "你有过想再靠近一点的试探。",
            Atmosphere = "暧昧",
            BondKeyword = "",
            Tags = new[] { RemiChatFragmentTag.Relation, RemiChatFragmentTag.Atmosphere },
            Keywords = new[] { "依赖", "离不开", "只跟你说", "能不能多", "陪我" },
        },
    };

    public static IReadOnlyList<PatternDef> AllPatterns => Patterns;

    /// <summary>兼容旧名。</summary>
    public static IReadOnlyList<PatternDef> AllTopics => Patterns;

    public static bool TryGetPattern(string id, out PatternDef def)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            def = default;
            return false;
        }

        string key = id.Trim();
        foreach (PatternDef pattern in Patterns)
        {
            if (pattern.Id == key)
            {
                def = pattern;
                return true;
            }
        }

        def = default;
        return false;
    }

    /// <summary>兼容旧 API。</summary>
    public static bool TryGetTopic(string id, out PatternDef def) => TryGetPattern(id, out def);

    public static int CountKeywordHits(string text, PatternDef pattern)
    {
        if (string.IsNullOrWhiteSpace(text) || pattern.Keywords == null || pattern.Keywords.Length == 0)
            return 0;

        string lower = text.ToLowerInvariant();
        int hits = 0;
        foreach (string keyword in pattern.Keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                continue;
            if (lower.Contains(keyword.ToLowerInvariant()))
                hits++;
        }

        return hits;
    }

    public static bool HasEndingVisibleTag(PatternDef pattern)
    {
        if (pattern.Tags == null)
            return false;
        foreach (RemiChatFragmentTag tag in pattern.Tags)
        {
            if (RemiChatFragmentTagRules.IsEndingVisible(tag))
                return true;
        }

        return false;
    }
}
