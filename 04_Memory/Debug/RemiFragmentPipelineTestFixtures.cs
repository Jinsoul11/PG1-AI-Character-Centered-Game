using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Fragment 全链路 · 专项预设语料（F10）。Depth 计划库见 RemiFragmentPipelineDepthFixtures（F11）。</summary>
public enum RemiFragmentPipelineTestFixtureId
{
    Custom = 0,
    Empty = 1,
    SmallTalk = 2,
    Reveal = 3,
    Cap = 4,
    RelationHeavy = 5,
}

[Serializable]
public class RemiFragmentPipelineTestSeedLine
{
    public int storyDay = 1;
    public string speaker = "player";
    [TextArea(1, 3)] public string content = "";
}

public static class RemiFragmentPipelineTestFixtures
{
    public static string DisplayName(RemiFragmentPipelineTestFixtureId id) => id switch
    {
        RemiFragmentPipelineTestFixtureId.Empty => "F1 Empty",
        RemiFragmentPipelineTestFixtureId.SmallTalk => "F2 SmallTalk",
        RemiFragmentPipelineTestFixtureId.Reveal => "F3 Reveal",
        RemiFragmentPipelineTestFixtureId.Cap => "F4 Cap",
        RemiFragmentPipelineTestFixtureId.RelationHeavy => "F5 RelationHeavy",
        _ => "Custom",
    };

    /// <summary>在预设用例间循环（跳过 Custom）。</summary>
    public static RemiFragmentPipelineTestFixtureId Cycle(RemiFragmentPipelineTestFixtureId current, int delta)
    {
        RemiFragmentPipelineTestFixtureId[] order =
        {
            RemiFragmentPipelineTestFixtureId.Empty,
            RemiFragmentPipelineTestFixtureId.SmallTalk,
            RemiFragmentPipelineTestFixtureId.Reveal,
            RemiFragmentPipelineTestFixtureId.Cap,
            RemiFragmentPipelineTestFixtureId.RelationHeavy,
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

    public static List<RemiFragmentPipelineTestSeedLine> Build(RemiFragmentPipelineTestFixtureId id, int defaultDay)
    {
        int day = Math.Max(1, defaultDay);
        switch (id)
        {
            case RemiFragmentPipelineTestFixtureId.Empty:
                return new List<RemiFragmentPipelineTestSeedLine>();

            case RemiFragmentPipelineTestFixtureId.SmallTalk:
                return Lines(day,
                    "在吗",
                    "哈哈",
                    "好的",
                    "嗯嗯",
                    "测试");

            case RemiFragmentPipelineTestFixtureId.Reveal:
                return Lines(day,
                    "其实我有时候会害怕毕业以后变成一个很普通的人。",
                    "你画画的时候好像整个人都不一样，我挺羡慕那种沉进去的感觉。",
                    "我不太敢跟别人说自己其实很在意别人怎么看我。",
                    "跟你聊天的时候，我好像不用一直假装自己很有主见。");

            case RemiFragmentPipelineTestFixtureId.Cap:
                return Lines(day,
                    "我以前总觉得虚拟世界更安全，因为不用被真实的目光盯着。",
                    "你说觉醒的时候，我突然想到自己是不是一直在逃避什么。",
                    "我压力大的时候会一个人待着，但又希望有人知道我并不是不想说话。",
                    "有时候我觉得自己很幼稚，但又不知道怎样才算长大。",
                    "如果作品展不顺利，我可能会很难过，但还是想让你看看我认真做过的东西。",
                    "跟你待在一起的这些天，有些话我第一次说出口。");

            case RemiFragmentPipelineTestFixtureId.RelationHeavy:
                return Lines(day,
                    "我们现在算是朋友吗？",
                    "你是不是开始依赖我了？",
                    "我们的关系是不是更近了一步？",
                    "你把我当成什么样的人？");

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
