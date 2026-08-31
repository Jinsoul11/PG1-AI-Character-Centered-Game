using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Day2 自习点问：按 F 从题库抽一句固定问答，不开 DialoguePanel；冷却后可再问。
/// </summary>
[DisallowMultipleComponent]
public class RemiLibraryDay2StudyWhisper : MonoBehaviour
{
    [Serializable]
    public class WhisperEntry
    {
        [Tooltip("可选：日志/调试用；不显示在玩家输入框。")]
        public string playerLinePreview;

        [TextArea(2, 4)]
        public string remiReply;
    }

    public static RemiLibraryDay2StudyWhisper Instance { get; private set; }

    public static void EnsureOn(MonoBehaviour host)
    {
        if (host == null)
            return;
        if (Instance != null)
            return;

        RemiLibraryDay2StudyWhisper existing = host.GetComponent<RemiLibraryDay2StudyWhisper>();
        if (existing == null)
            existing = host.gameObject.AddComponent<RemiLibraryDay2StudyWhisper>();
    }

    [Header("题库")]
    [SerializeField]
    private WhisperEntry[] entries =
    {
        new WhisperEntry
        {
            playerLinePreview = "展还顺利吗？",
            remiReply = "（小声）资料比我想的碎……不过靠窗这格视野好，能撑住。",
        },
        new WhisperEntry
        {
            playerLinePreview = "需要帮忙吗？",
            remiReply = "现在不用。你当观察者就好——真卡住了我再 whisper 你。",
        },
        new WhisperEntry
        {
            playerLinePreview = "你一直待在这儿？",
            remiReply = "下午的固定刷怪点。插座在左脚边，别踩到线。",
        },
        new WhisperEntry
        {
            playerLinePreview = "晚上还去教室吗？",
            remiReply = "今天大概就泡在这儿。你先忙你的也行。",
        },
        new WhisperEntry
        {
            playerLinePreview = "累不累？",
            remiReply = "有一点。但比昨天找书那场好撑多了。",
        },
        new WhisperEntry
        {
            playerLinePreview = "我可以看你写什么吗？",
            remiReply = "可以瞄一眼，别出声就行……馆里还是要小声。",
        },
    };

    [Header("节奏")]
    [SerializeField] private float cooldownSeconds = 10f;
    [SerializeField] private float replyDisplaySeconds = 4.2f;
    [Tooltip("整段自习最多点问次数；0 = 不限制。")]
    [SerializeField] private int maxAsksPerStudy = 4;

    [Header("Tip")]
    [SerializeField] private string tipReady = "按 F 小声问一句";
    [SerializeField] private string tipCooldown = "她在忙…";
    [SerializeField] private string tipExhausted = "先让她专心自习";

    private readonly List<int> _remainingIndices = new List<int>(8);
    private int _asksThisStudy;
    private float _cooldownUntil;
    private bool _askActive;
    private Coroutine _askRoutine;

    public bool IsAskActive => _askActive;

    public bool IsOnCooldown => Time.unscaledTime < _cooldownUntil;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>告别等打断：立刻收起点问气泡。</summary>
    public void AbortAskIfActive()
    {
        if (_askRoutine != null)
        {
            StopCoroutine(_askRoutine);
            _askRoutine = null;
        }

        _askActive = false;
        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        RemiRoleWorldUI roleUi = ResolveRoleUi(interaction);
        if (roleUi != null)
        {
            roleUi.SetWhisperReplyVisible(false);
            roleUi.SetResponseText(string.Empty);
            bool inRange = interaction != null && interaction.IsPlayerInRange;
            roleUi.ApplyState(inRange, false);
        }
    }

    /// <summary>每次进入 Studying 时重置题库与次数。</summary>
    public void ResetForNewStudy()
    {
        AbortAskIfActive();
        _asksThisStudy = 0;
        _cooldownUntil = 0f;
        RebuildRemainingPool();
    }

    public string ResolveTipPrompt()
    {
        if (_askActive)
            return string.Empty;
        if (maxAsksPerStudy > 0 && _asksThisStudy >= maxAsksPerStudy)
            return tipExhausted;
        if (IsOnCooldown)
            return tipCooldown;
        return tipReady;
    }

    public bool CanAcceptAsk()
    {
        if (_askActive)
            return false;
        if (maxAsksPerStudy > 0 && _asksThisStudy >= maxAsksPerStudy)
            return false;
        if (IsOnCooldown)
            return false;
        return HasAnyReply();
    }

    /// <summary>自习中按 F：开始一次点问。成功返回 true。</summary>
    public bool TryBeginAsk(RemiInteraction interaction)
    {
        RemiLibraryDay2CoPresenceFlow flow = RemiLibraryDay2CoPresenceFlow.Instance;
        if (flow == null || !flow.IsStudying || flow.IsSequenceRunning || flow.IsInFarewell)
            return false;
        if (!CanAcceptAsk())
            return false;

        WhisperEntry entry = TakeRandomEntry();
        if (entry == null || string.IsNullOrWhiteSpace(entry.remiReply))
            return false;

        _asksThisStudy++;
        if (_askRoutine != null)
            StopCoroutine(_askRoutine);
        _askRoutine = StartCoroutine(CoPlayAsk(interaction, entry.remiReply.Trim()));
        return true;
    }

    private IEnumerator CoPlayAsk(RemiInteraction interaction, string remiReply)
    {
        _askActive = true;

        RemiRoleWorldUI roleUi = ResolveRoleUi(interaction);
        if (roleUi != null)
        {
            roleUi.SetResponseText(remiReply);
            roleUi.SetWhisperReplyVisible(true);
            roleUi.ApplyState(true, false);
        }

        float hold = Mathf.Max(0.5f, replyDisplaySeconds);
        float t = 0f;
        while (t < hold)
        {
            // 自习被打断（告别）则提前收
            RemiLibraryDay2CoPresenceFlow flow = RemiLibraryDay2CoPresenceFlow.Instance;
            if (flow == null || !flow.IsStudying)
                break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (roleUi != null)
        {
            roleUi.SetWhisperReplyVisible(false);
            roleUi.SetResponseText(string.Empty);
            bool inRange = interaction != null && interaction.IsPlayerInRange;
            roleUi.SetTipPrompt(ResolveTipPrompt());
            roleUi.ApplyState(inRange, false);
        }

        _askActive = false;
        _askRoutine = null;
        _cooldownUntil = Time.unscaledTime + Mathf.Max(0f, cooldownSeconds);

        if (interaction != null)
            interaction.RefreshRoleWorldUiAfterStory();
    }

    private void RebuildRemainingPool()
    {
        _remainingIndices.Clear();
        if (entries == null)
            return;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && !string.IsNullOrWhiteSpace(entries[i].remiReply))
                _remainingIndices.Add(i);
        }
    }

    private bool HasAnyReply()
    {
        if (_remainingIndices.Count > 0)
            return true;
        if (entries == null)
            return false;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && !string.IsNullOrWhiteSpace(entries[i].remiReply))
                return true;
        }

        return false;
    }

    private WhisperEntry TakeRandomEntry()
    {
        if (_remainingIndices.Count == 0)
            RebuildRemainingPool();
        if (_remainingIndices.Count == 0)
            return null;

        int pick = UnityEngine.Random.Range(0, _remainingIndices.Count);
        int index = _remainingIndices[pick];
        _remainingIndices.RemoveAt(pick);
        return entries[index];
    }

    private static RemiRoleWorldUI ResolveRoleUi(RemiInteraction interaction)
    {
        if (interaction != null)
        {
            RemiRoleWorldUI ui = interaction.GetComponentInChildren<RemiRoleWorldUI>(true);
            if (ui != null)
                return ui;
        }

        Remi remi = FindObjectOfType<Remi>();
        return remi != null ? remi.GetComponentInChildren<RemiRoleWorldUI>(true) : null;
    }
}
