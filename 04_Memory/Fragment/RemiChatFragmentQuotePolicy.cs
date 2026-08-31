/// <summary>
/// Quote 圣物策略（PG 宪法 B）：非一等公民；须经评分→审核→引用资格。
/// 玩家可见上限 = 经过重述的准原句（当前落地：只用 summary，永不露出 raw quote）。
/// </summary>
public static class RemiChatFragmentQuotePolicy
{
    /// <summary>每局 Ending 最多允许几处「准原句级」引用（预留；当前实现为 0 条 raw）。</summary>
    public const int MaxSacredCitesPerEnding = 1;

    public static bool HasCiteEligibility(RemiChatFragmentEntry entry) =>
        entry != null && entry.quoteCiteEligible && !string.IsNullOrWhiteSpace(entry.quote);

    public static bool HasCiteEligibility(RemiFragmentImpression impression) =>
        impression != null && impression.quoteCiteEligible && !string.IsNullOrWhiteSpace(impression.quote);

    public static string ResolvePlayerVisibleLine(RemiChatFragmentEntry entry)
    {
        if (entry == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.summary))
            return entry.summary.Trim();
        return string.Empty;
    }

    public static string ResolvePlayerVisibleLine(RemiFragmentImpression impression)
    {
        if (impression == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(impression.summary))
            return impression.summary.Trim();
        return string.Empty;
    }
}
