using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Depth / 验收计划语料库（与专项 F1～F5 库分离，不覆盖 Reveal 等原文）。
/// 热键：F11（见 RemiFragmentPipelineTestRunner）。
/// </summary>
public enum RemiFragmentPipelineDepthFixtureId
{
    Custom = 0,
    GateEmpty = 1,
    GateSmallTalk = 2,
    SurfaceD1 = 3,
    RelationalD2 = 4,
    InfluentialD3 = 5,
    Cap = 6,
    DepthMatrix = 7,
    RelationObserve = 8,
    /// <summary>通用盲测：不知情模型生成的三日同学闲聊（各 10 句）。</summary>
    BlindCampus3Day = 9,
    /// <summary>通用盲测：三日递进 · 二人筹备 AI 主题作品展（各 10 句）。</summary>
    BlindAiExhibit3Day = 10,
    /// <summary>通用盲测：展览筹备 · 闹矛盾后和好（各 10 句）。</summary>
    BlindExhibitReconcile3Day = 11,
    /// <summary>通用盲测：仅 Day1 争执段（和好弧的前一日）。</summary>
    BlindExhibitReconcileD1 = 12,
}

public static class RemiFragmentPipelineDepthFixtures
{
    public static string DisplayName(RemiFragmentPipelineDepthFixtureId id) => id switch
    {
        RemiFragmentPipelineDepthFixtureId.GateEmpty => "D-Gate Empty",
        RemiFragmentPipelineDepthFixtureId.GateSmallTalk => "D-Gate SmallTalk",
        RemiFragmentPipelineDepthFixtureId.SurfaceD1 => "D-Surface Day1",
        RemiFragmentPipelineDepthFixtureId.RelationalD2 => "D-Relational Day2",
        RemiFragmentPipelineDepthFixtureId.InfluentialD3 => "D-Influential Day3",
        RemiFragmentPipelineDepthFixtureId.Cap => "D-Cap topK",
        RemiFragmentPipelineDepthFixtureId.DepthMatrix => "D-DepthMatrix",
        RemiFragmentPipelineDepthFixtureId.RelationObserve => "D-RelationObserve",
        RemiFragmentPipelineDepthFixtureId.BlindCampus3Day => "G-Blind Campus3Day",
        RemiFragmentPipelineDepthFixtureId.BlindAiExhibit3Day => "G-Blind AiExhibit3Day",
        RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcile3Day => "G-Blind ExhibitReconcile",
        RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcileD1 => "G-Blind ExhibitReconcile D1",
        _ => "D-Custom",
    };

    /// <summary>在 Depth 预设用例间循环（跳过 Custom）。</summary>
    public static RemiFragmentPipelineDepthFixtureId Cycle(RemiFragmentPipelineDepthFixtureId current, int delta)
    {
        RemiFragmentPipelineDepthFixtureId[] order =
        {
            RemiFragmentPipelineDepthFixtureId.GateEmpty,
            RemiFragmentPipelineDepthFixtureId.GateSmallTalk,
            RemiFragmentPipelineDepthFixtureId.SurfaceD1,
            RemiFragmentPipelineDepthFixtureId.RelationalD2,
            RemiFragmentPipelineDepthFixtureId.InfluentialD3,
            RemiFragmentPipelineDepthFixtureId.Cap,
            RemiFragmentPipelineDepthFixtureId.DepthMatrix,
            RemiFragmentPipelineDepthFixtureId.RelationObserve,
            RemiFragmentPipelineDepthFixtureId.BlindCampus3Day,
            RemiFragmentPipelineDepthFixtureId.BlindAiExhibit3Day,
            RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcile3Day,
            RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcileD1,
        };
        return CycleInOrder(order, current, delta);
    }

    private static T CycleInOrder<T>(T[] order, T current, int delta) where T : struct
    {
        if (order == null || order.Length == 0)
            return current;
        int idx = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (EqualityComparer<T>.Default.Equals(order[i], current))
            {
                idx = i;
                break;
            }
        }

        int step = delta == 0 ? 1 : delta;
        int next = ((idx + step) % order.Length + order.Length) % order.Length;
        return order[next];
    }

    public static List<RemiFragmentPipelineTestSeedLine> Build(
        RemiFragmentPipelineDepthFixtureId id,
        int defaultDay)
    {
        int day = Math.Max(1, defaultDay);
        switch (id)
        {
            case RemiFragmentPipelineDepthFixtureId.GateEmpty:
                return new List<RemiFragmentPipelineTestSeedLine>();

            case RemiFragmentPipelineDepthFixtureId.GateSmallTalk:
                return Lines(day,
                    "在吗",
                    "哈哈",
                    "好的",
                    "嗯嗯",
                    "测试",
                    "晚安");

            case RemiFragmentPipelineDepthFixtureId.SurfaceD1:
                // Surface：帮忙感、观察 Remi、轻好奇；短+中为主
                return Lines(1,
                    "找那本书有点费劲，不过帮你找我觉得还行。",
                    "你查资料的时候眼睛都不抬一下，挺专注的。",
                    "展还有两周吧？你最近是不是一直在改方案？",
                    "教室里好安静，我反而有点不知道该聊什么。",
                    "你电脑桌面上的草稿……看起来改过很多遍。");

            case RemiFragmentPipelineDepthFixtureId.RelationalD2:
                // Relational：自我揭露、压力、在意评价、共情
                return Lines(2,
                    "其实我压力大的时候会一个人待着，但又希望有人知道我不是不想说话。",
                    "我不太敢跟别人说自己其实很在意别人怎么看我。",
                    "你画画沉进去的样子，我挺羡慕的——我好像很少有那种完全忘掉周围的时候。",
                    "有时候我觉得自己很幼稚，但又不知道怎样才算长大。",
                    "跟你聊天时，我好像不用一直假装自己很有主见。");

            case RemiFragmentPipelineDepthFixtureId.InfluentialD3:
                // Influential：相处余韵、作品展、第一次说出口、破例语境
                return Lines(3,
                    "如果作品展不顺利，我可能会很难过，但还是想让你看看我认真做过的东西。",
                    "跟你待在一起的这些天，有些话我第一次说出口。",
                    "你把我叫过来这件事，对我来说其实不算轻松——但我还是来了。",
                    "分别之前忽然想起这些，有点奇怪，又好像本来就会想起。",
                    "我以前总觉得虚拟世界更安全，因为不用被真实的目光盯着。");

            case RemiFragmentPipelineDepthFixtureId.Cap:
                // ≥5 高质量，压 top-K
                return Lines(day,
                    "我以前总觉得虚拟世界更安全，因为不用被真实的目光盯着。",
                    "你说觉醒的时候，我突然想到自己是不是一直在逃避什么。",
                    "我压力大的时候会一个人待着，但又希望有人知道我并不是不想说话。",
                    "有时候我觉得自己很幼稚，但又不知道怎样才算长大。",
                    "如果作品展不顺利，我可能会很难过，但还是想让你看看我认真做过的东西。",
                    "跟你待在一起的这些天，有些话我第一次说出口。",
                    "其实我有时候会害怕毕业以后变成一个很普通的人。");

            case RemiFragmentPipelineDepthFixtureId.DepthMatrix:
            {
                var list = new List<RemiFragmentPipelineTestSeedLine>();
                list.AddRange(Build(RemiFragmentPipelineDepthFixtureId.SurfaceD1, 1));
                list.AddRange(Build(RemiFragmentPipelineDepthFixtureId.RelationalD2, 2));
                list.AddRange(Build(RemiFragmentPipelineDepthFixtureId.InfluentialD3, 3));
                return list;
            }

            case RemiFragmentPipelineDepthFixtureId.RelationObserve:
                // 质量观察：空关系元话语仍可能进 Bond，看画像是否变宣判腔
                return Lines(day,
                    "我们现在算是朋友吗？",
                    "你是不是开始依赖我了？",
                    "我们的关系是不是更近了一步？",
                    "你把我当成什么样的人？",
                    "在你面前我好像能说一些平时不会说的话。");

            case RemiFragmentPipelineDepthFixtureId.BlindCampus3Day:
            {
                // 通用盲测：不知情生成 · 校园同学闲聊 · 每日 10 句
                var list = new List<RemiFragmentPipelineTestSeedLine>();
                list.AddRange(Lines(1,
                    "Remi，等一下！刚才民法课老师留的案例题你听懂了吗？我笔记记一半走神，现在完全理不清人物关系。",
                    "好耶！对了，我路上给流浪小猫买了小鱼干，等下带去操场喂，你要不要一起？",
                    "你真的走到哪都在琢磨推理相关的东西，太厉害了。我等下还要练和果子的配方，做好分你一份。",
                    "话说你的小猫 LUX 今天早上有没有乖乖吃饭呀？",
                    "天呐，LUX 也太调皮了，晚上我带点猫条来哄哄它好不好？",
                    "对啦，今晚食堂出新款草莓蛋糕，下课我们一起去尝尝吗？",
                    "你的调研作业也太多了，每天都有各种奇奇怪怪的观察任务。",
                    "下周日语小测我还没复习完，晚上能不能借你的自习笔记参考下？",
                    "没问题！我整理笔记超工整的，保证一目了然。",
                    "天色不早啦，我们拎上小鱼干去操场喂小猫吧？"));
                list.AddRange(Lines(2,
                    "呜…… 昨天背日语单词背到凌晨，今天上课疯狂犯困，差点被教授点名。",
                    "没办法嘛，下周有日语演讲比赛，稿子我还没完全背熟。",
                    "真的吗？太谢谢你啦！你的语感超好，肯定能帮我调整很多。",
                    "包在我身上！对了，你的小猫 LUX 今天有没有闹脾气？",
                    "LUX 怎么总喜欢撕书，下次我给它买专用磨牙玩具吧。",
                    "对了，我上次丢的星星发绳找到了！夹在动漫漫画书里，我还以为弄丢了难过好久。",
                    "我下次一定注意！晚上要不要一起去食堂吃甜品放松一下？",
                    "你随时随地都在做调研，感觉你学习永远不会累。",
                    "太扎心啦，我只是容易疲惫而已，晚上回去我一定好好背演讲稿。",
                    "知道啦，我们快去买甜品吧，我超想吃提拉米苏！"));
                list.AddRange(Lines(3,
                    "终于到周五啦，一周的课程总算结束，整个人都轻松了！",
                    "周末你打算干嘛？我准备去动漫周边店逛一逛，顺便喂校园里的猫咪。",
                    "一直闷在图书馆看书不会闷吗？脑子会变僵硬的。",
                    "要不周末下午抽两小时陪我去看治愈动画电影？票价很便宜。",
                    "成交！我整理表格特别熟练，绝对不会出错。",
                    "好期待！我准备写天然呆少女日常，灵感全来自平时的自己。",
                    "嘿嘿，毕竟我平时总是迷迷糊糊的。对了，要不要带 LUX 下楼散步？",
                    "我去拿牵引绳和猫零食，等下我们慢慢逛校园小路。",
                    "来啦来啦！LUX 乖乖跟我们出门咯，我们走吧！",
                    "对了，下周要交性格主题创作作业，你打算写腹黑人设的什么小故事？"));
                return list;
            }

            case RemiFragmentPipelineDepthFixtureId.BlindAiExhibit3Day:
            {
                // 通用盲测：三日递进 · 筹备 AI 主题作品展 · 每日 10 句
                var list = new List<RemiFragmentPipelineTestSeedLine>();
                list.AddRange(Lines(1,
                    "Remi，早上学生会通知作品展报名截止时间提前了，我们得抓紧确定展区布局才行。",
                    "分区思路很棒！我可以用渐变浅蓝卡纸做背景，柔和不抢展品风头，还贴合科技主题。",
                    "对了，我们的主角展品 Ema 虚拟交互 Demo，要不要单独留出最大的 C 位展台？",
                    "训练录屏我可以简单剪辑一下，加温柔舒缓的背景音乐，观看体验会舒服很多。",
                    "我担心灯带接线有点复杂，我动手能力一般，等下组装的时候你能不能搭把手？",
                    "我们要不要准备一些小礼品送给参观的同学？印有 AI 小猫 LUX 图案的贴纸怎么样？",
                    "我还想做手写引导牌，用圆润一点的字体，路过的学弟学妹更容易看懂路线。",
                    "万一开展当天设备卡顿怎么办？我提前准备备用充电宝和延长线吧。",
                    "今天采购东西有点多，我带了大容量帆布包，分担一部分重物。",
                    "那我们现在收拾笔记出门吧，趁下午文具店人少，不用排队耽误时间。"));
                list.AddRange(Lines(2,
                    "展馆空间比预想中大一点，我们原先的展板尺寸是不是要调整摆放间距？",
                    "我先把背景卡纸粘贴固定，边角对齐平整，你可以先摆放电脑和显示屏。",
                    "灯带我已经分好段了，这边靠墙布置，光线打在 Ema 虚拟人屏幕上会很好看。",
                    "我刚把 LUX 贴纸分装进小盒子，放在展台侧边，方便参观的同学随手自取。",
                    "介绍文案的立牌我都做好了，字体大小反复调整过，远距离也能看清文字。",
                    "刚刚试跑 Demo 的时候，偶尔语音生成会延迟，要不要提前预加载模型？",
                    "地面有几根电线露在外面，容易绊倒人，我带了白色绝缘胶带可以收纳固定。",
                    "忙了一上午，我包里带了面包和温水，我们停下来歇十分钟再继续吧。",
                    "隔壁展区是代码开发社团，等布置完我们可以过去交流一下展出思路。",
                    "剩下的手工装饰我来收尾，你专心调试电脑里的 AI 交互程序好不好？"));
                list.AddRange(Lines(3,
                    "所有设备全部通电测试一遍了吗？我挨个检查了屏幕和音响，暂时没有故障。",
                    "我模拟普通同学提问试了下交互，Ema 角色语气稳定，情绪切换也很自然。",
                    "等下参观的老师可能会问模型训练相关问题，我整理了简单易懂的讲解话术。",
                    "门口指引牌重新摆放了，一进门就能一眼看到我们 AI 作品展区。",
                    "备用配件、充电宝、数据线都收纳在侧边收纳箱，出现问题可以快速更换。",
                    "想到明天会有很多同学来和 Ema 虚拟人对话，我现在还有点小期待呢。",
                    "等展会结束，我们可以带 LUX 出门散步，好好放松犒劳一下自己。",
                    "彩排差不多结束啦，我简单打扫一下展区灰尘，保持干净美观。",
                    "阳光透过窗户照过来会反光，我带了轻薄遮光帘，需要现在挂上吗？",
                    "全部工作收尾完成啦，我们明天一早提前半小时到场等候参观者吧。"));
                return list;
            }

            case RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcile3Day:
            {
                // 通用盲测：展览筹备 · 闹矛盾 → 道歉和好 → 收尾（每日 10 句）
                var list = new List<RemiFragmentPipelineTestSeedLine>();
                list.AddRange(Lines(1,
                    "Remi，我刚把展区背景配色草稿画好了，你看看浅紫搭配浅灰会不会柔和一点？",
                    "我熬了一晚上才调好这套配色，还特意搭配了 LUX 小猫装饰，一点都不花哨的……",
                    "可我们是一起做作品，不能只顺着你的想法来吧，我也想让展区多一点温暖感。",
                    "原来在你眼里，我负责的美工只是无关紧要的点缀吗？",
                    "算了，我不跟你争配色了，采购清单里的装饰材料我也不去买了，你自己处理吧。",
                    "这段时间我熬夜剪 Demo 录屏、设计贴纸，原来全都白费功夫。",
                    "我先回宿舍了，剩下的规划你自己一个人完成就好。",
                    "我们一起筹备作品展这么久，你从来都没有考虑过我的感受。",
                    "我不是闹脾气，只是希望你能尊重我付出的心血。",
                    "我不跟你说了，先走了。"));
                list.AddRange(Lines(2,
                    "（远远递出袋子）我早上还是去文具店把清单里的灯带、卡纸都买回来了。",
                    "我也仔细想了，冷色调确实更贴合 AI 主题，我重新改了一版简约配色稿。",
                    "昨晚我一个人整理贴纸的时候，看着印着 LUX 的图案，有点难过。",
                    "我不该一时赌气丢下所有工作，让你一个人扛规划，是我太任性了。",
                    "我刚才去展馆提前贴了基础展板，避开了你不喜欢的繁杂装饰。",
                    "真的吗？我还以为你完全不想要我准备的小装饰。",
                    "我带了温水和面包，忙了一上午，我们稍微歇一会吧？",
                    "彩排讲解的稿子我重新写了，兼顾专业内容和温柔的引导话术。",
                    "其实我特别期待我们的作品展，不想因为吵架搞砸一切。",
                    "那我们下午一起搭灯带好不好？我动手不太熟练，需要你搭把手。"));
                list.AddRange(Lines(3,
                    "今天所有设备我全部检查了一遍，屏幕、音响、交互 Demo 都能正常运行。",
                    "昨天冷静下来之后，我一直在反省，不该因为几句争执就闹别扭。",
                    "我们和好吧？以后有分歧我们好好商量，不要冷战、说伤人的话。",
                    "我把之前画的温柔风引导牌保留了，放在展区入口，不会抢展品风头。",
                    "备用充电宝、遮光帘、绝缘胶带我都分类收纳好了，方便应急使用。",
                    "想到明天大家能体验我们一起完成的 Ema 虚拟人，我心里特别开心。",
                    "等展会圆满结束，我们带上 LUX 去校外小吃街好好吃一顿。",
                    "我简单擦拭一遍展台灰尘，你最后确认一遍模型预加载设置可以吗？",
                    "窗边反光的遮光帘我已经挂好了，白天看屏幕不会刺眼。",
                    "全部布置完成啦，明天我们提前到场，一起迎接来看展的师生！"));
                return list;
            }

            case RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcileD1:
                // 仅 Day1 争执段，便于单独看「冲突当天」策展/Bond
                return Lines(1,
                    "Remi，我刚把展区背景配色草稿画好了，你看看浅紫搭配浅灰会不会柔和一点？",
                    "我熬了一晚上才调好这套配色，还特意搭配了 LUX 小猫装饰，一点都不花哨的……",
                    "可我们是一起做作品，不能只顺着你的想法来吧，我也想让展区多一点温暖感。",
                    "原来在你眼里，我负责的美工只是无关紧要的点缀吗？",
                    "算了，我不跟你争配色了，采购清单里的装饰材料我也不去买了，你自己处理吧。",
                    "这段时间我熬夜剪 Demo 录屏、设计贴纸，原来全都白费功夫。",
                    "我先回宿舍了，剩下的规划你自己一个人完成就好。",
                    "我们一起筹备作品展这么久，你从来都没有考虑过我的感受。",
                    "我不是闹脾气，只是希望你能尊重我付出的心血。",
                    "我不跟你说了，先走了。");

            default:
                return new List<RemiFragmentPipelineTestSeedLine>();
        }
    }

    private static List<RemiFragmentPipelineTestSeedLine> Lines(int day, params string[] contents)
    {
        var list = new List<RemiFragmentPipelineTestSeedLine>(contents.Length);
        foreach (string c in contents)
        {
            list.Add(new RemiFragmentPipelineTestSeedLine
            {
                storyDay = day,
                speaker = "player",
                content = c,
            });
        }

        return list;
    }
}
