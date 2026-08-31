using System;

/// <summary>
/// 过程记忆关系功能标签（Meaning 分类）。
/// Bond Mode B：凡进入 Fragment Memory 且有 summary 的条目均可按 Weight 供稿，不再按标签挡 Relation/Atmosphere。
/// 标签仍供理解层分类与调试；旧「页1/页2」槽位 API 保留供遗留路径。
/// </summary>
public enum RemiChatFragmentTag
{
    Identity = 1,
    Moment = 2,
    Resonance = 3,
    Relation = 4,
    Atmosphere = 5,
}

/// <summary>标签辅助与遗留 Ending 槽位契约。</summary>
public static class RemiChatFragmentTagRules
{
    /// <summary>遗留：旧模板可见三件套。Bond Mode B 已不再用此限制供稿。</summary>
    public static bool IsEndingVisible(RemiChatFragmentTag tag) =>
        tag == RemiChatFragmentTag.Identity
        || tag == RemiChatFragmentTag.Moment
        || tag == RemiChatFragmentTag.Resonance;

    /// <summary>遗留：+2 第 1 页 Identity / Moment。</summary>
    public static bool IsBondPage1Source(RemiChatFragmentTag tag) =>
        tag == RemiChatFragmentTag.Identity || tag == RemiChatFragmentTag.Moment;

    /// <summary>遗留：+2 第 2 页 Resonance 润色。</summary>
    public static bool IsBondPage2PolishSource(RemiChatFragmentTag tag) =>
        tag == RemiChatFragmentTag.Resonance;

    public static bool TryParse(string raw, out RemiChatFragmentTag tag)
    {
        tag = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out tag);
    }

    public static string ToKey(RemiChatFragmentTag tag) => tag.ToString();
}
