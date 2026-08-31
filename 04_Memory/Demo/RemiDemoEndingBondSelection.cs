using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ending Bond：Selection（Weight top-K）+ Mode B 呈现。
/// 设计师取舍（注释，不进 AI prompt）：无合格 Impression 则不出 Bond 页——宁缺毋编。
/// 凡已进入 Fragment Memory 且有可呈现 summary 的条目，均可按 Weight 入选 Bond（不按标签挡 Relation/Atmosphere）。
/// 发给模型的文案见 <see cref="RemiEndingSpeakPrompt"/>。
/// </summary>
public static class RemiDemoEndingBondSelection
{
    public const int DefaultMaxSelected = 3;

    /// <summary>
    /// 按 Final Weight 取最高的可呈现印象。
    /// 仅排除空 summary；标签不参与能否进 Bond。
    /// </summary>
    public static List<RemiFragmentImpression> SelectForBond(
        IReadOnlyList<RemiFragmentImpression> impressions,
        int maxSelected = DefaultMaxSelected)
    {
        var eligible = new List<RemiFragmentImpression>();
        if (impressions == null || maxSelected <= 0)
            return eligible;

        foreach (RemiFragmentImpression impression in impressions)
        {
            if (!IsEligibleForBond(impression))
                continue;
            eligible.Add(impression);
        }

        eligible.Sort((a, b) => b.weight.CompareTo(a.weight));

        if (eligible.Count > maxSelected)
            eligible.RemoveRange(maxSelected, eligible.Count - maxSelected);

        return eligible;
    }

    /// <summary>已在 Fragment Memory 中的条目：有可见 summary 即可进 Bond。</summary>
    public static bool IsEligibleForBond(RemiFragmentImpression impression)
    {
        if (impression == null)
            return false;

        string visible = RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(impression);
        return !string.IsNullOrWhiteSpace(visible);
    }

    /// <summary>呈现失败时的工程兜底：拼接已选 summary（非 AI 创作）。</summary>
    public static string BuildHonestFallbackLine(IReadOnlyList<RemiFragmentImpression> selected)
    {
        if (selected == null || selected.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        foreach (RemiFragmentImpression impression in selected)
        {
            string line = RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(impression);
            if (!string.IsNullOrWhiteSpace(line))
                parts.Add(line.Trim().TrimEnd('。', '.', '…'));
        }

        if (parts.Count == 0)
            return string.Empty;
        if (parts.Count == 1)
            return parts[0] + "。";

        return string.Join("。", parts) + "。";
    }

    public static string BuildComposeSystemContext(IReadOnlyList<RemiFragmentImpression> selected) =>
        RemiEndingSpeakPrompt.BuildBondContext(selected);

    public static string ResolveWhenLabel(IReadOnlyList<RemiFragmentImpression> selected)
    {
        if (selected == null || selected.Count == 0)
            return "相处";

        int minDay = int.MaxValue;
        int maxDay = 0;
        foreach (RemiFragmentImpression impression in selected)
        {
            if (impression == null || impression.storyDay <= 0)
                continue;
            if (impression.storyDay < minDay)
                minDay = impression.storyDay;
            if (impression.storyDay > maxDay)
                maxDay = impression.storyDay;
        }

        if (minDay == int.MaxValue)
            return "相处";
        if (minDay == maxDay)
            return $"第 {minDay} 天起";
        return $"第 {minDay}～{maxDay} 天";
    }
}
