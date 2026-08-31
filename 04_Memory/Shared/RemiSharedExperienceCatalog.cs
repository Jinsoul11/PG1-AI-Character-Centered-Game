using UnityEngine;

/// <summary>共同经历固定 frame（Progress 事实；不含闲聊/RPG 内容）。</summary>
public static class RemiSharedExperienceCatalog
{
    public struct EndingPageDef
    {
        public int SortOrder;
        public string WhenLabel;
        public Color PlaceholderColor;
        public string RecapFallbackLine;
    }

    public static string IdKey(RemiSharedExperienceId id) =>
        id switch
        {
            RemiSharedExperienceId.Day1CommissionBook => "day1_commission_book",
            RemiSharedExperienceId.Day2LibraryCoPresence => "day2_library_co_presence",
            RemiSharedExperienceId.Day3DormDeviation => "day3_dorm_deviation",
            _ => id.ToString(),
        };

    public static string KindKey(RemiSharedExperienceId id) =>
        id switch
        {
            RemiSharedExperienceId.Day1CommissionBook => "commission",
            RemiSharedExperienceId.Day2LibraryCoPresence => "co_presence",
            _ => "deviation",
        };

    public static string DefaultFrame(RemiSharedExperienceId id) =>
        id switch
        {
            RemiSharedExperienceId.Day1CommissionBook =>
                "你请求玩家帮忙找作品展用的参考书《AI游戏入门》；玩家找到并交给你。",
            RemiSharedExperienceId.Day2LibraryCoPresence =>
                "你邀请玩家下午到图书馆一起自习查展资料。",
            RemiSharedExperienceId.Day3DormDeviation =>
                "玩家邀请你来宿舍；你破例答应，偏离了原计划在图书馆的安排。",
            _ => "",
        };

    public static bool TryParseIdKey(string idKey, out RemiSharedExperienceId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(idKey))
            return false;

        switch (idKey.Trim())
        {
            case "day1_commission_book":
                id = RemiSharedExperienceId.Day1CommissionBook;
                return true;
            case "day2_library_co_presence":
                id = RemiSharedExperienceId.Day2LibraryCoPresence;
                return true;
            case "day3_dorm_deviation":
                id = RemiSharedExperienceId.Day3DormDeviation;
                return true;
            default:
                return false;
        }
    }

    public static int GetSortOrder(RemiSharedExperienceId id) =>
        TryGetEndingPage(id, out EndingPageDef def) ? def.SortOrder : (int)id;

    public static int GetSortOrder(string idKey) =>
        TryParseIdKey(idKey, out RemiSharedExperienceId id) ? GetSortOrder(id) : int.MaxValue;

    public static bool TryGetEndingPage(RemiSharedExperienceId id, out EndingPageDef def)
    {
        switch (id)
        {
            case RemiSharedExperienceId.Day1CommissionBook:
                def = new EndingPageDef
                {
                    SortOrder = 10,
                    WhenLabel = "第 1 天 · 教室",
                    PlaceholderColor = new Color(0.32f, 0.36f, 0.42f, 1f),
                    RecapFallbackLine = "那天你帮我在教室里找到了《AI游戏入门》……对我来说，那本书很重要。",
                };
                return true;
            case RemiSharedExperienceId.Day2LibraryCoPresence:
                def = new EndingPageDef
                {
                    SortOrder = 20,
                    WhenLabel = "第 2 天 · 图书馆",
                    PlaceholderColor = new Color(0.28f, 0.32f, 0.38f, 1f),
                    RecapFallbackLine = "下午你来了图书馆。我们并坐着查资料，好像也没那么难熬。",
                };
                return true;
            case RemiSharedExperienceId.Day3DormDeviation:
                def = new EndingPageDef
                {
                    SortOrder = 30,
                    WhenLabel = "第 3 天 · 宿舍",
                    PlaceholderColor = new Color(0.36f, 0.30f, 0.34f, 1f),
                    RecapFallbackLine = "你邀请我来宿舍……我破例答应了。如果是以前，我大概不会。",
                };
                return true;
            default:
                def = default;
                return false;
        }
    }

    public static bool TryGetEndingPage(string idKey, out EndingPageDef def)
    {
        if (TryParseIdKey(idKey, out RemiSharedExperienceId id))
            return TryGetEndingPage(id, out def);
        def = default;
        return false;
    }

    /// <summary>Voice ACTIVE_MEMORY 话题别名（命中才唤醒）。</summary>
    public static string[] GetTopicAliases(string idKey)
    {
        if (!TryParseIdKey(idKey, out RemiSharedExperienceId id))
            return System.Array.Empty<string>();
        return GetTopicAliases(id);
    }

    public static string[] GetTopicAliases(RemiSharedExperienceId id) =>
        id switch
        {
            RemiSharedExperienceId.Day1CommissionBook => new[]
            {
                "找书", "参考书", "那本书", "交书", "帮我找", "作品展用的书", "委托",
                "AI游戏入门", "《AI游戏入门》",
            },
            RemiSharedExperienceId.Day2LibraryCoPresence => new[]
            {
                "图书馆", "共自习", "一起自习", "馆里", "查展资料", "下午图书馆",
            },
            RemiSharedExperienceId.Day3DormDeviation => new[]
            {
                "宿舍", "公寓", "来宿舍", "邀请我去", "破例",
            },
            _ => System.Array.Empty<string>(),
        };

    /// <summary>Remi 口述向回忆提示（可空）。</summary>
    public static string GetRemiRecall(string idKey)
    {
        if (!TryParseIdKey(idKey, out RemiSharedExperienceId id))
            return "";
        return GetRemiRecall(id);
    }

    public static string GetRemiRecall(RemiSharedExperienceId id) =>
        id switch
        {
            RemiSharedExperienceId.Day1CommissionBook =>
                "你帮我找到并交了《AI游戏入门》，对我来说很重要。",
            RemiSharedExperienceId.Day2LibraryCoPresence =>
                "我们下午在图书馆一起查过资料，并坐着自习。",
            RemiSharedExperienceId.Day3DormDeviation =>
                "你邀请我去宿舍，我破例答应了。",
            _ => "",
        };
}
