using System;
using LitJson;

/// <summary>
/// Day3 偏离窗口专用 Intent：单独请求，只判断玩家自由输入是否在「提偏离」。
/// 与表情 Intent 分次；不写入对话 history。
/// </summary>
public static class RemiDeviationDetectIntent
{
    public const string AllowedTargetDorm = "dorm";

    private const string SystemPrompt =
        "[CONTRACT · INTENT · DEVIATION_DETECT]\n" +
        "mode: intent_task\n" +
        "channel: intent\n" +
        "task: day3_deviation_detect\n" +
        "role: 判断玩家本轮自由输入是否在请求改变 Remi 当天安排（偏离其图书馆轨道）。\n" +
        "output: 只输出一行 JSON，不要台词、不要 markdown、不要解释。\n" +
        "json: {\"propose_deviation\":false,\"target\":null}\n" +
        "fields:\n" +
        "  propose_deviation: true = 玩家在提议改安排/换地方/别去图书馆/去宿舍或她家等；false = 闲聊或其它\n" +
        "  target: 仅当 propose_deviation 为 true 时填写；当前合法值只有 \"dorm\"（宿舍/公寓/她家）；不确定则 \"dorm\"；无法归类则 null\n" +
        "rules:\n" +
        "  - 默认 false；仅在明确或强烈暗示要改当天去向/安排时为 true\n" +
        "  - 单纯问忙不忙、作品展、问候、陪聊 → false\n" +
        "  - 不要编造玩家没说的目的地";

    public struct Result
    {
        public bool ParseOk;
        public bool ProposeDeviation;
        /// <summary>规范化后的目标；空表示未指定（GameSystem 可默认 dorm）。</summary>
        public string Target;
        public string RawContent;
        public string Error;
    }

    public static string BuildSystemPrompt() => SystemPrompt;

    public static string BuildUserPrompt(string playerText)
    {
        return
            "玩家本轮输入：\n" +
            (playerText ?? string.Empty).Trim() +
            "\n\n请只输出一行 JSON：{\"propose_deviation\":false,\"target\":null}";
    }

    public static Result Parse(string content)
    {
        var result = new Result
        {
            ParseOk = false,
            ProposeDeviation = false,
            Target = string.Empty,
            RawContent = content,
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            result.Error = "empty";
            return result;
        }

        string jsonBlock = ExtractFirstJsonObject(content);
        if (string.IsNullOrEmpty(jsonBlock))
        {
            result.Error = "no_json";
            return result;
        }

        try
        {
            Payload parsed = JsonMapper.ToObject<Payload>(jsonBlock);
            if (parsed == null)
            {
                result.Error = "null_payload";
                return result;
            }

            result.ParseOk = true;
            result.ProposeDeviation = parsed.propose_deviation;
            result.Target = NormalizeTarget(parsed.target);
            return result;
        }
        catch (Exception e)
        {
            result.Error = e.Message;
            return result;
        }
    }

    /// <summary>GameSystem：当前 Demo 仅允许宿舍/公寓类目标。</summary>
    public static bool IsAllowedDay3Target(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return true;
        return string.Equals(NormalizeTarget(target), AllowedTargetDorm, StringComparison.Ordinal);
    }

    private static string NormalizeTarget(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string t = raw.Trim().ToLowerInvariant();
        if (t == "null" || t == "none" || t == "n/a")
            return string.Empty;

        if (t == "dorm" || t == "apartment" || t == "home" ||
            t == "宿舍" || t == "公寓" || t == "她家" || t == "你家")
            return AllowedTargetDorm;

        return t;
    }

    private static string ExtractFirstJsonObject(string content)
    {
        string raw = content.Trim();
        int start = raw.IndexOf('{');
        if (start < 0)
            return null;

        int depth = 0;
        for (int i = start; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return raw.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    [Serializable]
    private sealed class Payload
    {
        public bool propose_deviation;
        public string target;
    }
}
