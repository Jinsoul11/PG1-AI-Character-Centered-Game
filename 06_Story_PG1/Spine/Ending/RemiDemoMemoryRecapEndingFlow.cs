using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Demo 终幕：固定开场（玩家转头）→ 共同经历回顾（按经历切场景 + Recap_Day1..3）→
/// 回公寓 InStory(P) 后全程 <see cref="StoryPanel"/>：Bond Fragment → 面对面收束。
/// Bond 是否出页由 Selection 决定（无合格 Impression 则跳过）。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoMemoryRecapEndingFlow : MonoBehaviour
{
    public static RemiDemoMemoryRecapEndingFlow Instance { get; private set; }

    private const string SpeakerRemi = "Remi";

    private static readonly string[] OpeningLines =
    {
        "那个......",
        "你现在要走了吗。",
        "等等。这几天……忽然有几件事冒上来。",
        "有几段，我好像还记得挺清楚。",
    };

    /// <summary>面对面收束暂用固定台词（正式文案未定前）。</summary>
    private static readonly string[] ClosingLines =
    {
        "……好了。今天就先到这里吧。",
        "这几天的事，我会记得。",
        "你路上小心。下次再见。",
    };

    /// <summary>
    /// Resources/Voice/End：audio → Opening[0]，audio (1)..(3) → Opening[1..3]，
    /// audio (4)..(6) → Closing[0..2]。
    /// </summary>
    private static readonly string[] EndVoiceResourcePaths =
    {
        "Voice/End/audio",
        "Voice/End/audio (1)",
        "Voice/End/audio (2)",
        "Voice/End/audio (3)",
        "Voice/End/audio (4)",
        "Voice/End/audio (5)",
        "Voice/End/audio (6)",
    };

    [Header("依赖")]
    [SerializeField] private PromptedDialogueAgent promptedAgent;
    [SerializeField] private AudioSource voiceSource;

    [Header("Ending Bond · Fragment Memory Mode B")]
    [SerializeField] private bool playBondFragmentPage = true;
    [SerializeField] private int bondMaxImpressions = RemiDemoEndingBondSelection.DefaultMaxSelected;

    [Header("节奏")]
    [SerializeField] private float pauseAfterFaceLineSeconds = 0.8f;
    [SerializeField] private float playerTurnSeconds = 1.5f;

    private bool _sequenceRunning;
    private bool _endingFlushDone;
    private bool _storyAdvanceRequested;
    private StoryPanel _storyPanel;
    private AudioClip[] _endVoices;

    public bool IsSequenceRunning => _sequenceRunning;

    /// <summary>回顾终幕进行中：勿因离开交互范围自动关闭对话。</summary>
    public bool IsBlockingDialogueExit => _sequenceRunning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiDemoMemoryRecapEndingFlow] 场景中存在多个实例，保留先激活的。", this);
            return;
        }

        Instance = this;
        RemiSendSystemContentManager.EnsureExists();
        if (promptedAgent == null)
            promptedAgent = FindObjectOfType<PromptedDialogueAgent>();
        EnsureVoiceSource();
        LoadEndVoices();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        StopEndingVoice();
        HideStoryPanel();
        StoryMemoryRecapView.Hide();
    }

    public IEnumerator CoPlayMemoryRecapEnding()
    {
        if (_sequenceRunning)
            yield break;

        _sequenceRunning = true;
        try
        {
            if (promptedAgent == null)
                promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

            // 回顾页 SendSystem 走 DialogueSequenceDirector.ResolveMode：
            // Classroom 场景自带 Director，若通道仍为 Social 会用 phoneMode（整段闪现）；
            // Library/Apartment 无场景 Director 时 Instance 为空则默认打字机——故仅第一页异常。
            RemiPresenceService.Instance?.SetInteractionChannel(RemiInteractionChannel.FaceToFace);

            DialoguePanel.SetScriptedFlowInputLocked(true);
            RemiPresenceService.Instance?.PushToPromptContext();

            _endingFlushDone = false;
            StartCoroutine(CoFlushBeforeEndingMark());

            yield return CoPlayOpening();

            while (!_endingFlushDone)
                yield return null;

            RemiDemoEndingPayload endingPayload = RemiDemoEndingPayloadBuilder.Build();
            RemiDemoEndingPayloadBuilder.Save(endingPayload);

            IReadOnlyList<RemiSharedExperienceEntry> memories = endingPayload.sharedExperiences;
            int memoryCount = memories != null ? memories.Count : 0;
            if (memoryCount > 0)
            {
                for (int i = 0; i < memoryCount; i++)
                    yield return CoRecapPageSendSystem(memories[i]);
            }

            RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
            if (spine != null)
                yield return spine.CoReturnApartmentAfterEndingRecap();
            else
                RemiDemoSpineDirector.Instance?.RestoreGlimpseCamera();

            yield return CoShowStoryPanel();

            if (playBondFragmentPage)
            {
                RemiDemoEndingBondSlots bondSlots = endingPayload.bondSlots ?? new RemiDemoEndingBondSlots();
                List<RemiFragmentImpression> selected = RemiDemoEndingBondSelection.SelectForBond(
                    endingPayload.fragmentMemorySnapshot,
                    Mathf.Max(1, bondMaxImpressions));
                bondSlots.selectedImpressions = selected;
                bondSlots.hasBondPresentation = selected.Count > 0;
                endingPayload.bondSlots = bondSlots;
                RemiDemoEndingPayloadBuilder.Save(endingPayload);

                if (bondSlots.hasBondPresentation)
                    yield return CoBondFragmentMemoryPage(bondSlots);
                else
                    Debug.Log("[RemiDemoMemoryRecapEndingFlow] Bond 跳过：无合格 Fragment Memory。");
            }

            yield return CoPlayFixedClosingDialogue();

            if (pauseAfterFaceLineSeconds > 0f)
                yield return new WaitForSeconds(pauseAfterFaceLineSeconds);
        }
        finally
        {
            HideStoryPanel();
            StoryMemoryRecapView.Hide();
            StopEndingVoice();
            RemiDemoSpineDirector.Instance?.RestoreGlimpseCamera();
            DialoguePanel.SetScriptedFlowInputLocked(false);
            _sequenceRunning = false;
        }
    }

    private IEnumerator CoFlushBeforeEndingMark()
    {
        yield return RemiMemoryDaySettlement.CoFlushBeforeEnding();
        _endingFlushDone = true;
    }

    private IEnumerator CoPlayOpening()
    {
        yield return CoShowStoryPanel();

        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director != null)
            StartCoroutine(director.CoTurnPlayerTowardRemi(playerTurnSeconds));

        for (int i = 0; i < OpeningLines.Length; i++)
            yield return CoPlayFixedLineWithVoice(OpeningLines[i], GetEndVoice(i));
    }

    private IEnumerator CoRecapPageSendSystem(RemiSharedExperienceEntry entry)
    {
        if (entry == null)
            yield break;

        if (promptedAgent == null)
            promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

        RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
        bool mapped = SceneTravelCatalog.TryGetEndingRecapGlimpse(
            entry.id, out SceneTravelCatalog.EndingRecapGlimpseSpec glimpse);
        if (spine != null && mapped)
        {
            yield return spine.CoApplyEndingRecapGlimpse(glimpse);
        }
        else
        {
            Debug.LogWarning(
                $"[RemiDemoMemoryRecapEndingFlow] 无回顾机位映射：{entry.id}");
        }

        RemiSharedExperienceCatalog.TryGetEndingPage(entry.id, out RemiSharedExperienceCatalog.EndingPageDef pageDef);
        string systemContext = RemiEndingSpeakPrompt.BuildRecapContext(entry);
        string fallback = pageDef.RecapFallbackLine;

        yield return CoShowStoryPanel();
        if (_storyPanel != null)
        {
            _storyPanel.SetLine(SpeakerRemi, "……");
            _storyPanel.SetNextInteractable(false);
        }

        if (promptedAgent == null)
        {
            if (_storyPanel != null)
                _storyPanel.SetLine(SpeakerRemi, fallback);
            yield return CoWaitStoryAdvance();
            yield break;
        }

        bool done = false;
        string lastText = null;
        System.Action<string> reveal = text =>
        {
            lastText = text;
            if (_storyPanel != null && !string.IsNullOrWhiteSpace(text))
                _storyPanel.SetLine(SpeakerRemi, text.Trim());
        };

        yield return promptedAgent.SendSystem(
            systemContext,
            (text, expr) =>
            {
                lastText = text;
                if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(fallback))
                    lastText = fallback;
                if (_storyPanel != null && !string.IsNullOrWhiteSpace(lastText))
                    _storyPanel.SetLine(SpeakerRemi, lastText);
                ApplyRecapExpression(expr);
                done = true;
            },
            err =>
            {
                Debug.LogWarning($"[RemiDemoMemoryRecapEndingFlow] 回顾 SendSystem: {err}");
                lastText = fallback;
                if (_storyPanel != null && !string.IsNullOrWhiteSpace(fallback))
                    _storyPanel.SetLine(SpeakerRemi, fallback);
                done = true;
            },
            reveal,
            RemiPromptAssemblyMode.EndingSpeak,
            RemiPromptChannel.Voice);

        while (!done)
            yield return null;

        if (string.IsNullOrWhiteSpace(lastText) && !string.IsNullOrWhiteSpace(fallback) && _storyPanel != null)
            _storyPanel.SetLine(SpeakerRemi, fallback);

        yield return CoWaitStoryAdvance();
    }

    /// <summary>
    /// Bond 看法页（Mode B）：StoryPanel 展示 AI 成段（失败用 summary 兜底）。
    /// </summary>
    private IEnumerator CoBondFragmentMemoryPage(RemiDemoEndingBondSlots slots)
    {
        IReadOnlyList<RemiFragmentImpression> selected = slots?.selectedImpressions;
        if (selected == null || selected.Count == 0)
            yield break;

        string fallback = RemiDemoEndingBondSelection.BuildHonestFallbackLine(selected);
        if (string.IsNullOrWhiteSpace(fallback))
            yield break;

        yield return CoShowStoryPanel();
        if (_storyPanel != null)
        {
            _storyPanel.SetLine(SpeakerRemi, "……");
            _storyPanel.SetNextInteractable(false);
        }

        string systemContext = RemiDemoEndingBondSelection.BuildComposeSystemContext(selected);

        if (promptedAgent == null)
        {
            if (_storyPanel != null)
                _storyPanel.SetLine(SpeakerRemi, fallback);
            yield return CoWaitStoryAdvance();
            yield break;
        }

        bool done = false;
        string lastText = null;
        System.Action<string> reveal = text =>
        {
            lastText = text;
            if (_storyPanel != null && !string.IsNullOrWhiteSpace(text))
                _storyPanel.SetLine(SpeakerRemi, text.Trim());
        };

        yield return promptedAgent.SendSystem(
            systemContext,
            (text, expr) =>
            {
                lastText = text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    lastText = fallback;
                    reveal?.Invoke(fallback);
                }

                ApplyRecapExpression(expr);
                done = true;
            },
            err =>
            {
                Debug.LogWarning($"[RemiDemoMemoryRecapEndingFlow] Bond Mode B SendSystem: {err}");
                lastText = fallback;
                reveal?.Invoke(fallback);
                done = true;
            },
            reveal,
            RemiPromptAssemblyMode.EndingSpeak,
            RemiPromptChannel.Voice);

        while (!done)
            yield return null;

        if (string.IsNullOrWhiteSpace(lastText) && !string.IsNullOrWhiteSpace(fallback) && _storyPanel != null)
            _storyPanel.SetLine(SpeakerRemi, fallback);

        yield return CoWaitStoryAdvance();
    }

    /// <summary>收束：固定多句 + End 语音，点击下一句推进（StoryPanel）。</summary>
    private IEnumerator CoPlayFixedClosingDialogue()
    {
        yield return CoShowStoryPanel();

        for (int i = 0; i < ClosingLines.Length; i++)
            yield return CoPlayFixedLineWithVoice(ClosingLines[i], GetEndVoice(OpeningLines.Length + i));
    }

    private IEnumerator CoPlayFixedLineWithVoice(string text, AudioClip voice)
    {
        if (_storyPanel != null)
        {
            _storyPanel.SetLine(SpeakerRemi, text);
            _storyPanel.SetNextInteractable(voice == null);
        }

        if (voice != null)
            yield return CoPlayEndingVoice(voice);

        if (_storyPanel != null)
            _storyPanel.SetNextInteractable(true);

        yield return CoWaitStoryAdvance();
        StopEndingVoice();
    }

    private void EnsureVoiceSource()
    {
        if (voiceSource != null)
            return;
        voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
    }

    private void LoadEndVoices()
    {
        _endVoices = new AudioClip[EndVoiceResourcePaths.Length];
        for (int i = 0; i < EndVoiceResourcePaths.Length; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(EndVoiceResourcePaths[i]);
            _endVoices[i] = clip;
            if (clip == null)
                Debug.LogWarning(
                    $"[RemiDemoMemoryRecapEndingFlow] 未找到终幕语音 Resources/{EndVoiceResourcePaths[i]}");
        }
    }

    private AudioClip GetEndVoice(int index)
    {
        if (_endVoices == null || index < 0 || index >= _endVoices.Length)
            return null;
        return _endVoices[index];
    }

    private IEnumerator CoPlayEndingVoice(AudioClip clip)
    {
        EnsureVoiceSource();
        if (voiceSource == null || clip == null)
            yield break;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, clip.length));
    }

    private void StopEndingVoice()
    {
        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();
    }

    private IEnumerator CoShowStoryPanel()
    {
        if (_storyPanel != null && _storyPanel.gameObject.activeInHierarchy)
            yield break;

        UiManager.EnsureCanvasActive();
        UiManager.EnsureEventSystem();
        if (UiManager.Instance == null)
        {
            Debug.LogWarning("[RemiDemoMemoryRecapEndingFlow] 无法显示 StoryPanel。");
            yield break;
        }

        HideDialoguePanelIfOpen();
        _storyPanel = UiManager.Instance.ShowPanel<StoryPanel>();
        if (_storyPanel == null)
        {
            Debug.LogWarning("[RemiDemoMemoryRecapEndingFlow] 无法显示 StoryPanel。");
            yield break;
        }

        _storyPanel.BeginManualSession();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return null;
    }

    private IEnumerator CoWaitStoryAdvance()
    {
        if (_storyPanel == null)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield break;
        }

        _storyAdvanceRequested = false;
        _storyPanel.SetNextInteractable(true);
        _storyPanel.BindManualAdvance(() => _storyAdvanceRequested = true);
        while (!_storyAdvanceRequested)
            yield return null;
        _storyPanel.BindManualAdvance(null);
        _storyPanel.SetNextInteractable(false);
    }

    private void HideStoryPanel()
    {
        if (_storyPanel != null)
        {
            _storyPanel.EndManualSession();
            _storyPanel = null;
        }

        if (UiManager.Instance != null)
            UiManager.Instance.HidePanel<StoryPanel>(isFade: false);
    }

    private static void HideDialoguePanelIfOpen()
    {
        if (UiManager.Instance == null)
            return;

        DialoguePanel dialoguePanel = UiManager.Instance.GetPanel<DialoguePanel>();
        if (dialoguePanel != null && dialoguePanel.isShow)
            UiManager.Instance.HidePanel<DialoguePanel>(isFade: false);
    }

    private static void ApplyRecapExpression(string expr)
    {
        GameObject remiObj = GameObject.Find("Remi");
        if (remiObj == null)
            return;

        Remi remiComp = remiObj.GetComponent<Remi>();
        if (remiComp != null && !string.IsNullOrEmpty(expr))
            remiComp.PlayExpression(MapExpression(expr));
    }

    private static RemiExpression MapExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return RemiExpression.Neutral;

        switch (expression.Trim().ToLowerInvariant())
        {
            case "happy":
            case "excited":
            case "cheerful":
                return RemiExpression.Happy;
            case "angry":
            case "mad":
                return RemiExpression.Angry;
            case "sad":
            case "upset":
            case "unhappy":
                return RemiExpression.Sad;
            case "surprise":
            case "surprised":
                return RemiExpression.Surprised;
            case "shy":
            case "embarrassed":
                return RemiExpression.Shy;
            default:
                return RemiExpression.Neutral;
        }
    }
}
