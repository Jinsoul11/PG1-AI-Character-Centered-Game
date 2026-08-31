using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 图书馆共现 Anchor 开场：Remi 主动短 Story，播完进自由聊。
/// 不传送玩家/Remi。Window → AnchorStory → FreeChat（就地自动开面板）由 Flow 管理。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RemiLibraryDay2CoPresenceStory : MonoBehaviour
{
    private const string PrefsPlayedKey = "RemiStory_LibraryDay2CoPresence";
    private const string SpeakerRemi = "Remi";

    [Serializable]
    public class RemiVoiceSlot
    {
        [TextArea(2, 5)]
        public string linePreview;

        public AudioClip voice;
    }

    [Header("依赖")]
    [SerializeField] private StoryDirector storyDirector;
    [SerializeField] private RemiLibraryDay2CoPresenceFlow flow;

    [Header("触发")]
    [SerializeField] private bool persistPlayedFlag = true;
    [Tooltip("玩家进触发区时若仍在 Window，仅确保 Window 已启动（不直接播 Story）。")]
    [SerializeField] private bool bootstrapWindowOnTriggerEnter = true;

    [Header("Remi Anchor 开场（仅 Remi，宜短）")]
    [SerializeField]
    private RemiVoiceSlot[] remiVoices =
    {
        new RemiVoiceSlot
        {
            linePreview = "（小声）你来了！从那边传过来第一次确实像走错副本——我就说流程有点绕。",
        },
        new RemiVoiceSlot
        {
            linePreview = "这个位置是我下午的固定刷怪点：靠窗、插座在左脚边。叫你来，就是想让你看看我「」。",
        },
        new RemiVoiceSlot
        {
            linePreview = "好啦，有什么想说的可以先聊会，然后我要开始自习了。",
        },
    };

    private Action _onAnchorIntroFinished;
    private bool _playingAnchorIntro;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (storyDirector == null)
            storyDirector = FindLocalStoryDirector();

        if (flow == null)
            flow = GetComponent<RemiLibraryDay2CoPresenceFlow>();
        if (flow == null)
            flow = gameObject.AddComponent<RemiLibraryDay2CoPresenceFlow>();

        RemiLibraryDay2CoPresenceFlow.EnsureExists();
        EnsureRemiVoiceSlots();
    }

#if UNITY_EDITOR
    private void OnValidate() => EnsureRemiVoiceSlots();
#endif

    private void EnsureRemiVoiceSlots()
    {
        string[] previews =
        {
            "（小声）你来了！从那边传过来第一次确实像走错副本——我就说流程有点绕。",
            "这个位置是我下午的固定刷怪点：靠窗、插座在左脚边。叫你来，就是想让你看看我「下午一般在干什么」。",
            "好啦，有什么想说的可以先聊会，然后我要开始自习了",
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
        if (!bootstrapWindowOnTriggerEnter)
            return;
        if (!IsPlayerCollider(other))
            return;

        RemiLibraryDay2CoPresenceFlow.EnsureExists();
        RemiLibraryDay2CoPresenceFlow f = RemiLibraryDay2CoPresenceFlow.Instance;
        if (f != null && f.State == RemiLibraryDay2CoPresenceFlow.FlowState.Inactive)
            f.NotifyLibraryWindowStart();
    }

    /// <summary>
    /// Anchor：就地播放 Remi 短开场，不移动玩家/Remi；结束后进入可自由聊状态。
    /// </summary>
    public void PlayAnchorIntroInPlace(Action onFinished)
    {
        if (_playingAnchorIntro)
        {
            onFinished?.Invoke();
            return;
        }

        if (persistPlayedFlag && PlayerPrefs.GetInt(PrefsPlayedKey, 0) != 0)
        {
            RemiDemoSpineDirector.EnsureExists();
            RemiDemoSpineDirector.Instance?.NotifyDay2LibraryIntroFinished();
            onFinished?.Invoke();
            return;
        }

        if (storyDirector == null)
        {
            Debug.LogWarning("[RemiLibraryDay2CoPresenceStory] 无 StoryDirector。");
            onFinished?.Invoke();
            return;
        }

        _playingAnchorIntro = true;
        _onAnchorIntroFinished = onFinished;

        storyDirector.ResetStoryPlaybackState();
        storyDirector.PrepareForTriggeredEpisode();
        // 明确不散场挪位
        storyDirector.SetRemiAfterStoryPoint(null);
        storyDirector.SetLines(BuildAnchorLines());

        Transform remi = ResolveRemiTransform();
        if (remi != null)
            storyDirector.SetRemiRoot(remi);

        RemiRoleWorldUI roleUi = remi != null
            ? remi.GetComponentInChildren<RemiRoleWorldUI>(true)
            : null;
        roleUi?.ApplyStoryPlaying(true);

        storyDirector.BeginStory();
    }

    public bool IsPlayingAnchorIntro => _playingAnchorIntro;

    public StoryDirector BoundStoryDirector => storyDirector;

    private void OnStoryFinished()
    {
        if (!_playingAnchorIntro)
            return;

        _playingAnchorIntro = false;

        if (persistPlayedFlag)
            PlayerPrefs.SetInt(PrefsPlayedKey, 1);

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.NotifyDay2LibraryIntroFinished();

        Transform remi = ResolveRemiTransform();
        RemiInteraction interaction = remi != null
            ? remi.GetComponent<RemiInteraction>() ?? remi.GetComponentInChildren<RemiInteraction>(true)
            : null;
        // 不配置固定对话机位：保持就地
        if (interaction != null)
        {
            interaction.SetDialoguePoseReference(null);
            interaction.ConfigureRemiDialogueYaw(false, 0f, 0f);
            interaction.RefreshRoleWorldUiAfterStory();
        }

        Action cb = _onAnchorIntroFinished;
        _onAnchorIntroFinished = null;
        cb?.Invoke();
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

    private List<StoryDirector.StoryLine> BuildAnchorLines()
    {
        var lines = new List<StoryDirector.StoryLine>();
        int remiVoiceIndex = 0;

        Add(lines, SpeakerRemi,
            "（小声）你来了！从那边传过来第一次确实像走错副本——我就说流程有点绕。",
            TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi,
            "这个位置是我下午的固定刷怪点：靠窗、插座在左脚边。叫你来，就是想让你看看我「下午一般在干什么」。",
            TakeRemiVoice(ref remiVoiceIndex));
        Add(lines, SpeakerRemi,
            "好啦，有什么想说的可以先聊会，然后我要开始自习了。",
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

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Played Flag")]
    private void Editor_ClearPlayedFlag()
    {
        PlayerPrefs.DeleteKey(PrefsPlayedKey);
        _playingAnchorIntro = false;
        storyDirector?.ResetStoryPlaybackState();
    }
#endif
}
