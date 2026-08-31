/// <summary>
/// 记忆 summary 角色指代清洗：避免「对方」在 Remi 第一人称呈现时主语翻转。
/// 「对方」在策展语境里通常指 Remi，统一写成 Remi。
/// </summary>
public static class RemiFragmentSummarySanitize
{
    public static string ReplaceAmbiguousOtherParty(string summary)
    {
        if (string.IsNullOrEmpty(summary))
            return summary ?? "";

        // 先长后短
        return summary
            .Replace("对方的", "Remi的")
            .Replace("对方", "Remi");
    }
}
