using System;
using UnityEngine;

/// <summary>
/// 面对面闲聊：闲置后 Remi 提议再出现的话题组（按场景配置）。
/// 正式配置入口：<see cref="RemiSendSystemContentManager"/> 的闲置话题数组。
/// </summary>
[Serializable]
public class RemiFaceIdleTopicSet
{
    public SceneTravelLocation scene = SceneTravelLocation.Classroom;

    [TextArea(2, 4)]
    [Tooltip("固定提议兜底；SendSystem 失败或关闭 LLM 路径时使用。")]
    public string proposeLine =
        "……你要是一时不知道说什么，我们可以从这些里挑一件聊。";

    [TextArea(3, 8)]
    [Tooltip("SendSystem initiator 上下文；留空则按话题自动拼一条。")]
    public string sendSystemContext = "";

    [Tooltip("话题按钮 1（点选后写入输入框）。")]
    public string topic0 = "今天课上怎么样？";

    [Tooltip("话题按钮 2。")]
    public string topic1 = "你最近在忙什么？";

    [Tooltip("话题按钮 3；留空则隐藏该按钮。")]
    public string topic2 = "";

    public string ResolveSendSystemContext()
    {
        if (!string.IsNullOrWhiteSpace(sendSystemContext))
            return sendSystemContext.Trim();

        return BuildDefaultSendSystemContext(topic0, topic1, topic2);
    }

    public static string BuildDefaultSendSystemContext(string t0, string t1, string t2)
    {
        var parts = new System.Collections.Generic.List<string>(3);
        if (!string.IsNullOrWhiteSpace(t0)) parts.Add(t0.Trim());
        if (!string.IsNullOrWhiteSpace(t1)) parts.Add(t1.Trim());
        if (!string.IsNullOrWhiteSpace(t2)) parts.Add(t2.Trim());
        string topics = parts.Count > 0 ? string.Join("；", parts) : "日常小事";

        return
            "玩家面对面闲聊时沉默了一会儿，似乎不知道说什么。" +
            "你主动开口提议可以聊的方向，但不要替玩家选定具体话题。" +
            "可参考这些方向（不要逐条念菜单）：" + topics + "。";
    }
}

/// <summary>按当前场景解析闲置话题配置。</summary>
public static class RemiFaceIdleTopicCatalog
{
    public static RemiFaceIdleTopicSet Resolve(
        RemiFaceIdleTopicSet[] sets,
        SceneTravelLocation scene)
    {
        if (sets != null)
        {
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] != null && sets[i].scene == scene)
                    return sets[i];
            }
        }

        return CreateDefault(scene);
    }

    public static RemiFaceIdleTopicSet CreateDefault(SceneTravelLocation scene)
    {
        switch (scene)
        {
            case SceneTravelLocation.Library:
                return new RemiFaceIdleTopicSet
                {
                    scene = scene,
                    proposeLine = "……卡壳的话，不如就从这些里挑一件？",
                    topic0 = "找书的事还有什么要叮嘱的吗？",
                    topic1 = "今天在馆里查到什么了吗？",
                    topic2 = "作品展资料还顺利吗？",
                };
            case SceneTravelLocation.Apartment:
                return new RemiFaceIdleTopicSet
                {
                    scene = scene,
                    proposeLine = "……要不我们就从这些里挑一件慢慢说。",
                    topic0 = "你之后有什么打算？",
                    topic1 = "最近有什么想做的事吗？",
                    topic2 = "今晚就随便聊聊也好。",
                };
            default:
                return new RemiFaceIdleTopicSet
                {
                    scene = SceneTravelLocation.Classroom,
                    proposeLine = "……你要是一时不知道说什么，我们可以从这些里挑一件聊。",
                    topic0 = "今天课上怎么样？",
                    topic1 = "你最近在忙什么？",
                    topic2 = "",
                };
        }
    }
}
