using System.Collections.Generic;

/// <summary>Demo 手写知识卡（Voice ACTIVE_KNOWLEDGE 数据源）。</summary>
public static class RemiKnowledgeCatalog
{
    private static readonly RemiKnowledgeCard[] Cards =
    {
        new RemiKnowledgeCard
        {
            id = "student_exhibition",
            aliases = new[]
            {
                "作品展", "学生展", "展览", "筹备", "方案", "改方案", "查资料", "展资料",
            },
            sceneTags = new[] { "library", "busy", "free" },
            episodeKinds = new[] { "commission", "co_presence" },
            remiView = "最近在准备学生作品展，常查资料、改方案，有点忙。",
            priority = 3,
        },
        new RemiKnowledgeCard
        {
            id = "library_place",
            aliases = new[]
            {
                "图书馆", "自习", "馆里", "查资料的地方",
            },
            sceneTags = new[] { "library", "free", "busy" },
            episodeKinds = new[] { "co_presence" },
            remiView = "图书馆对她来说适合安静查展资料、自习。",
            priority = 2,
        },
        new RemiKnowledgeCard
        {
            id = "ema_classmate",
            aliases = new[]
            {
                "Ema", "艾玛", "ema", "同学"
            },
            sceneTags = new[] { "classroom", "library", "busy", "free" },
            episodeKinds = new[] { "commission", "co_presence" },
            remiView = "Ema 是同学，常一起筹备作品展、商量分工；人挺靠谱。",
            priority = 4,
        },
        new RemiKnowledgeCard
        {
            id = "pet_lulu",
            aliases = new[]
            {
                "露露", "lulu", "LuLu", "宠物",
            },
            sceneTags = new[] { "free", "busy" },
            episodeKinds = new[] { "co_presence" },
            remiView = "露露是她养的宠物；闲下来会抱着露露看治愈番。",
            priority = 3,
        },

        // —— Relational（二阶段话题）——
        new RemiKnowledgeCard
        {
            id = "yuri_anime_entry",
            aliases = new[]
            {
                "百合", "百合动漫", "百合番", "二次元", "动漫", "入坑", "大一", "看番",
            },
            sceneTags = new[] { "library", "free", "busy" },
            episodeKinds = new[] { "co_presence" },
            remiView = "大一的时候看了《citrus 柑橘味香气》这部番，从此广泛涉猎百合题材的作品",
            priority = 4,
        },
        new RemiKnowledgeCard
        {
            id = "transfer_past",
            aliases = new[]
            {
                "转学", "以前", "原来学校", "以前的学校", "搬家", "转来", "以前班级",
            },
            sceneTags = new[] { "classroom", "library", "free" },
            episodeKinds = new[] { "co_presence" },
            remiView = "转到新班级后，先是碰到了艾玛，又碰到了玩家，感觉很幸运",
            priority = 4,
        },

        // —— Influential（三阶段话题）——
        new RemiKnowledgeCard
        {
            id = "ai_game_research",
            aliases = new[]
            {
                "AI", "人工智能", "游戏", "AI游戏", "结合", "研究方向",
                "AI游戏入门", "《AI游戏入门》",
            },
            sceneTags = new[] { "library", "busy", "free" },
            episodeKinds = new[] { "commission", "co_presence" },
            remiView = "接触人工智能后，开始想把 AI 和游戏结合起来，在做一些小的尝试",
            priority = 5,
        },
        new RemiKnowledgeCard
        {
            id = "near_term_plans",
            aliases = new[]
            {
                "之后", "打算", "展之后", "展后", "未来", "近期", "接下来", "以后想",
            },
            sceneTags = new[] { "free", "busy" },
            episodeKinds = new[] { "co_presence" },
            remiView = "近期仍以作品展为重；展后想把 AI 与游戏的想法再往前推一点，坚持以AI角色为基础的AI游戏方向。",
            priority = 4,
        },
    };

    public static IReadOnlyList<RemiKnowledgeCard> GetAll() => Cards;
}
