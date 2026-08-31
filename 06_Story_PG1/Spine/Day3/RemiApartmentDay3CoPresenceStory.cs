using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day3 公寓偏离共现：玩家传送到公寓后，全固定 Remi ↔ 玩家轮替台词；结束后进入自由面对面对话，门口离开触发终幕。
/// Remi 句语音在 Inspector「Remi 语音」里按顺序配置。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RemiApartmentDay3CoPresenceStory : MonoBehaviour
{
    public const string PrefsPlayedKey = "RemiStory_ApartmentDay3CoPresence";

    private const string SpeakerRemi = "Remi";
    private const string SpeakerPlayer = "你";

    [Serializable]
    public class RemiVoiceSlot
    {
        [TextArea(2, 5)]
        [Tooltip("对照用预览（运行时以代码台词为准）。")]
        public string linePreview;

        [Tooltip("本句 Remi 预录语音；可空。")]
        public AudioClip voice;
    }

    [Header("依赖")]
    [SerializeField] private StoryDirector storyDirector;

    [Header("触发条件")]
    [SerializeField] private bool requireSpineAwaitingApartmentVisit = true;
    [SerializeField] private bool persistPlayedFlag = true;

    [Header("剧情站位")]
    [SerializeField] private string playerStorySpawnName = SceneTravelCatalog.ApartmentInStorySpawnName;
    [SerializeField] private string remiDuringStoryMarkerName = SceneTravelCatalog.ApartmentRemiDuringStoryMarkerName;
    [SerializeField] private string remiAfterStoryMarkerName = SceneTravelCatalog.ApartmentRemiAfterStoryMarkerName;
    [SerializeField] private string playerFreeDialogueMarkerName = SceneTravelCatalog.ApartmentPlayerFreeDialogueMarkerName;

    [Header("Remi 语音（按 Remi 台词出现顺序）")]
    [SerializeField]
    private RemiVoiceSlot[] remiVoices =
    {
        new RemiVoiceSlot { linePreview = "（开门）……你真的来了。" },
        new RemiVoiceSlot
        {
            linePreview = "我本来这个点应该在图书馆。桌上那摞展稿我还没合上。",
        },
        new RemiVoiceSlot
        {
            linePreview = "作品展还有不到两周。每一句介绍我都想改到能拿出去给别人看。",
        },
        new RemiVoiceSlot { linePreview = "你总能把话绕成让人没法拒绝的句子。" },
        new RemiVoiceSlot { linePreview = "先进来吧。玄关不用换鞋——我这儿没那么多讲究。" },
        new RemiVoiceSlot
        {
            linePreview = "谈不上认路。客厅是电脑和还没来得及收拾的外卖盒；厨房基本算装饰。",
        },
        new RemiVoiceSlot
        {
            linePreview = "不过既然你来了，我就不假装自己还在图书馆了。",
        },
        new RemiVoiceSlot { linePreview = "沙发那边坐。想喝水自己倒——杯子在厨房台面上。" },
        new RemiVoiceSlot
        {
            linePreview = "……也行。至少今晚，我可以把「破例」说得理直气壮一点。",
        },
    };

    private bool _triggered;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (storyDirector == null)
            storyDirector = FindLocalStoryDirector();

        // 站位以 Catalog 为准：剧情 InStory(P/R) → 结束后 Remi ApartmentDefaultPos → 闲聊 DuringCon(P)
        playerStorySpawnName = SceneTravelCatalog.ApartmentInStorySpawnName;
        remiDuringStoryMarkerName = SceneTravelCatalog.ApartmentRemiDuringStoryMarkerName;
        remiAfterStoryMarkerName = SceneTravelCatalog.ApartmentRemiAfterStoryMarkerName;
        playerFreeDialogueMarkerName = SceneTravelCatalog.ApartmentPlayerFreeDialogueMarkerName;

        EnsureRemiVoiceSlots();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureRemiVoiceSlots();
    }
#endif

    private void EnsureRemiVoiceSlots()
    {
        string[] previews =
        {
            "（开门）……你真的来了。",
            "我本来这个点应该在图书馆。桌上那摞展稿我还没合上。",
            "作品展还有不到两周。每一句介绍我都想改到能拿出去给别人看。",
            "你总能把话绕成让人没法拒绝的句子。",
            "先进来吧。玄关不用换鞋——我这儿没那么多讲究。",
            "谈不上认路。客厅是电脑和还没来得及收拾的外卖盒；厨房基本算装饰。",
            "不过既然你来了，我就不假装自己还在图书馆了。",
            "沙发那边坐。想喝水自己倒——杯子在厨房台面上。",
            "……也行。至少今晚，我可以把「破例」说得理直气壮一点。",
        };

        if (remiVoices == null || remiVoices.Length != previews.Length)
        {
            var next = new RemiVoiceSlot[previews.Length];
            for (int i = 0; i < previews.Length; i++)
            {
                next[i] = new RemiVoiceSlot
                {
                    linePreview = previews[i],
                    voice = remiVoices != null && i < remiVoices.Length && remiVoices[i] != null
                        ? remiVoices[i].voice
                        : null,
                };
            }

            remiVoices = next;
            return;
        }

        for (int i = 0; i < previews.Length; i++)
        {
            if (remiVoices[i] == null)
                remiVoices[i] = new RemiVoiceSlot();
            remiVoices[i].linePreview = previews[i];
        }
    }

    private void OnEnable()
    {
        if (storyDirector != null)
            storyDirector.StoryFinished += OnStoryFinished;
    }

    private void OnDisable()
    {
        if (storyDirector != null)
            storyDirector.StoryFinished -= OnStoryFinished;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        TryBeginNow();
    }

    /// <summary>脊柱答应后自动传入公寓时调用；走进触发区也会走这里。成功开始则返回 true。</summary>
    public bool TryBeginNow()
    {
        if (_triggered)
            return false;
        if (!CanPlayNow(out string blockReason))
        {
            Debug.Log($"[RemiApartmentDay3CoPresenceStory] 未触发剧情: {blockReason}");
            return false;
        }

        _triggered = true;

        SceneTravelService.EnsureExists();
        // 剧情中：玩家 InStory(P)，Remi InStory(R)
        if (!SceneTravelService.TryPlacePlayerAtNamedSpawn(playerStorySpawnName))
            Debug.LogWarning($"[RemiApartmentDay3CoPresenceStory] 未找到站位 {playerStorySpawnName}，将在当前位置开始剧情。");
        PlaceRemiAtMarker(remiDuringStoryMarkerName);

        if (storyDirector == null)
            storyDirector = FindLocalStoryDirector();
        if (storyDirector == null)
        {
            Debug.LogWarning("[RemiApartmentDay3CoPresenceStory] 未绑定 StoryDirector，无法开始公寓开场。");
            _triggered = false;
            return false;
        }

        storyDirector.PrepareForTriggeredEpisode();
        storyDirector.SetLines(BuildLines());
        PrepareApartmentFreeDialoguePose();
        storyDirector.BeginStory();
        return true;
    }

    private void OnStoryFinished()
    {
        if (persistPlayedFlag)
            PlayerPrefs.SetInt(PrefsPlayedKey, 1);

        // 剧情后：Remi → ApartmentDefaultPos；玩家保持当前位置，闲聊开始时再落到 DuringCon(P)
        SceneTravelService.EnsureExists();
        PlaceRemiAtMarker(remiAfterStoryMarkerName);

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.NotifyDay3ApartmentIntroFinished();
        StoryNarrativeHintView.TryPlayCustomHint(
            "和 Remi 聊聊。聊完了可以从门口离开。", 7f);
    }

    private bool CanPlayNow(out string blockReason)
    {
        if (persistPlayedFlag && PlayerPrefs.GetInt(PrefsPlayedKey, 0) != 0)
        {
            blockReason = "本段剧情已播放过";
            return false;
        }

        if (_triggered)
        {
            blockReason = "本会话已触发过";
            return false;
        }

        if (storyDirector == null)
        {
            blockReason = "未绑定 StoryDirector";
            return false;
        }

        if (storyDirector.HasStarted)
        {
            blockReason = "StoryDirector 已开始";
            return false;
        }

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director != null && director.CurrentBeat >= RemiDemoSpineBeat.Day3ApartmentIntroDone)
        {
            blockReason = "公寓共现 intro 已完成，不再重播";
            return false;
        }

        if (requireSpineAwaitingApartmentVisit)
        {
            if (director == null || !director.IsAwaitingDay3ApartmentVisit())
            {
                blockReason = "故事进度尚未到达 Day3 公寓共现";
                return false;
            }

            blockReason = null;
            return true;
        }

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence == null)
        {
            blockReason = "RemiPresenceService 未就绪";
            return false;
        }

        if (presence.WorldTime.storyDay < 3)
        {
            blockReason = $"叙事 day 不足 (day={presence.WorldTime.storyDay})";
            return false;
        }

        blockReason = null;
        return true;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;
        if (other.GetComponentInParent<PlayerController>() != null)
            return true;
        if (other.GetComponentInParent<CharacterController>() != null)
            return true;
        return false;
    }

    private StoryDirector FindLocalStoryDirector()
    {
        Scene scene = gameObject.scene;
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                StoryDirector local = root.GetComponentInChildren<StoryDirector>(true);
                if (local != null && local.GetComponentInParent<RemiDemoSpineDirector>() == null)
                    return local;
            }
        }

        return FindObjectOfType<StoryDirector>();
    }

    private void PrepareApartmentFreeDialoguePose()
    {
        Scene scene = gameObject.scene;
        Transform remiAfter = SceneTravelService.TryFindSceneMarker(remiAfterStoryMarkerName, scene);
        Transform playerDialogue = SceneTravelService.TryFindSceneMarker(playerFreeDialogueMarkerName, scene);

        if (remiAfter == null)
            Debug.LogWarning($"[RemiApartmentDay3CoPresenceStory] 未找到 Remi 落点 {remiAfterStoryMarkerName}。");
        if (playerDialogue == null)
            Debug.LogWarning($"[RemiApartmentDay3CoPresenceStory] 未找到玩家自由对话落点 {playerFreeDialogueMarkerName}。");

        Transform remi = ResolveRemiTransform();
        if (remi != null)
            storyDirector.SetRemiRoot(remi);
        if (remiAfter != null)
            storyDirector.SetRemiAfterStoryPoint(remiAfter);

        RemiInteraction interaction = remi != null ? remi.GetComponent<RemiInteraction>() : null;
        if (interaction == null && remi != null)
            interaction = remi.GetComponentInChildren<RemiInteraction>(true);

        if (interaction != null)
        {
            if (playerDialogue != null)
                interaction.SetDialoguePoseReference(playerDialogue);
            // 保持落点 marker 朝向，不在对话时强改 Remi Y 轴（勿设 90°）。
            interaction.ConfigureRemiDialogueYaw(false);
        }
    }

    private void PlaceRemiAtMarker(string markerName)
    {
        if (string.IsNullOrWhiteSpace(markerName))
            return;

        Scene scene = gameObject.scene;
        Transform marker = SceneTravelService.TryFindSceneMarker(markerName, scene);
        Transform remi = ResolveRemiTransform();
        if (marker == null)
        {
            Debug.LogWarning($"[RemiApartmentDay3CoPresenceStory] 未找到 Remi 站位 {markerName}。");
            return;
        }

        if (remi == null)
        {
            Debug.LogWarning("[RemiApartmentDay3CoPresenceStory] 未找到 Remi，无法落位。");
            return;
        }

        remi.SetPositionAndRotation(marker.position, marker.rotation);
        remi.gameObject.SetActive(true);
        if (storyDirector != null)
            storyDirector.SetRemiRoot(remi);
    }

    private Transform ResolveRemiTransform()
    {
        Transform wired = storyDirector != null ? storyDirector.GetRemiRoot() : null;
        if (wired != null)
            return wired;

        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null)
            return interaction.transform;

        GameObject named = GameObject.Find("Remi");
        return named != null ? named.transform : null;
    }

    private List<StoryDirector.StoryLine> BuildLines()
    {
        var lines = new List<StoryDirector.StoryLine>();
        int remiVoiceIndex = 0;

        Add(lines, SpeakerRemi, "（开门）……你真的来了。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerPlayer, "你答应破例了，我就过来了。");
        Add(lines, SpeakerRemi,
            Emphasize("我本来这个点应该在图书馆。桌上那摞展稿我还没合上。", "图书馆", "展稿"),
            TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi, "作品展还有不到两周。每一句介绍我都想改到能拿出去给别人看。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerPlayer, Emphasize("所以想当面聊聊的话，只能趁今晚。", "今晚"));
        Add(lines, SpeakerRemi, "你总能把话绕成让人没法拒绝的句子。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi, "先进来吧。玄关不用换鞋——我这儿没那么多讲究。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerPlayer, "上次在图书馆看你忙展稿，这次换你带我认认路？");
        Add(lines, SpeakerRemi, "谈不上认路。客厅是电脑和还没来得及收拾的外卖盒；厨房基本算装饰。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi,
            Emphasize("不过既然你来了，我就不假装自己还在图书馆了。", "图书馆"),
            TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi, "沙发那边坐。想喝水自己倒——杯子在厨房台面上。", TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerPlayer, "好，那我不客气了。");
        Add(lines, SpeakerRemi,
            RemiDemoSpineStoryChips.FormatDay3RemiAcceptDisplay("……也行。至少今晚，我可以把「破例」说得理直气壮一点。"),
            TakeRemiVoice(ref remiVoiceIndex));

        return lines;
    }

    private AudioClip TakeRemiVoice(ref int remiVoiceIndex)
    {
        if (remiVoices == null || remiVoiceIndex < 0 || remiVoiceIndex >= remiVoices.Length)
            return null;

        RemiVoiceSlot slot = remiVoices[remiVoiceIndex++];
        return slot != null ? slot.voice : null;
    }

    private static void Add(List<StoryDirector.StoryLine> lines, string speaker, string text, AudioClip voice = null)
    {
        lines.Add(new StoryDirector.StoryLine
        {
            speakerName = speaker,
            text = text,
            voice = voice,
        });
    }

    private static string Emphasize(string plain, params string[] anchors) =>
        RemiDialogueEmphasis.Apply(plain, RemiDialogueEmphasisSpec.WithAnchors(anchors));

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Played Flag")]
    private void Editor_ClearPlayedFlag()
    {
        PlayerPrefs.DeleteKey(PrefsPlayedKey);
        _triggered = false;
        storyDirector?.ResetStoryPlaybackState();
    }
#endif

    public static void ResetProgressFlag()
    {
        PlayerPrefs.DeleteKey(PrefsPlayedKey);
    }
}
