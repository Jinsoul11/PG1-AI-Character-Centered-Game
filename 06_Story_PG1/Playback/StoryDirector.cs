using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小剧情驱动器：固定台词 + 预录语音 + 镜头看向说话人。
/// 目标：Demo 简洁、稳定；不依赖实时 TTS。
/// Remi 剧情后的主动搭话与左侧叙事提示由 <see cref="RemiFrontApproachTrigger"/> 负责（建议挂进 <see cref="enableOnFinish"/>；提示在 Remi 语音/打字结束后出现）。
/// </summary>
[DisallowMultipleComponent]
public class StoryDirector : MonoBehaviour
{
    [Serializable]
    public class StoryLine
    {
        public string speakerName;

        [TextArea(2, 6)]
        public string text;

        [Tooltip("预录语音（可为空）。不为空时会在播放完后才允许进入下一句。")]
        public AudioClip voice;

        [Tooltip("可选：触发该目标 Animator 的 Trigger（例如 Talk/Smile 等）。留空则不触发。")]
        public string animatorTrigger;

        [Header("可选：本句触发大家看向摄像机")]
        public bool makeCharactersLookAtCamera;
    }

    [Header("剧情内容")]
    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();

    /// <summary>只读访问剧情行（供 DialoguePanel 等后续生成推荐问题文案）。</summary>
    public IReadOnlyList<StoryLine> Lines => lines;

    [Header("镜头/声音")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private AudioSource voiceSource;

    [Header("剧情期间：接管玩家控制（推荐开启）")]
    [SerializeField] private bool takeoverPlayerControl = true;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private MonoBehaviour[] disablePlayerBehavioursDuringStory; // 例如 FirstPersonCameraLook

    [Header("剧情开始时：锁定玩家位置/朝向（统一在这里做）")]
    [SerializeField] private bool lockPlayerYawOnBegin = true;
    [SerializeField] private float playerYawDegrees = 0f;
    [SerializeField] private bool lockPlayerYOnBegin = true;
    [SerializeField] private float playerY = 0f;

    [Header("UI")]
    [SerializeField] private bool showStoryPanelOnStart = true;

    [Header("黑屏文字开场（教室开场等）")]
    [Tooltip("为 true 时先播黑屏旁白，再进入下方固定台词。")]
    [SerializeField] private bool playBlackScreenIntro = true;
    [SerializeField] private List<string> blackScreenIntroLines = new List<string>
    {
        "转学后的第一个上午，教室里还很安静。",
        "老师刚布置了本学期最重要的任务——约两周后的学生作品展。",
        "每组须组队完成一个主题展区：选题、设计、布展，都要在规定时间内落地。",
        "讲台边，Remi 和 Ema 正围着分工表商量展区筹备。",
        "你走进教室。她们还在讨论，似乎没注意到门口——",
    };

    [Header("镜头推进（可选）")]
    [SerializeField] private bool moveCameraOnBegin = true;
    [Tooltip("剧情开始时，镜头从当前点缓慢移动到该位置（可为空）。")]
    [SerializeField] private Transform cameraApproachTarget;
    [SerializeField] private float cameraApproachDuration = 2.5f;
    [SerializeField] private AnimationCurve cameraApproachEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("镜头推进时实际移动的根节点。主摄像机是玩家子物体时应设置为玩家根节点（CameraRig）。为空则自动推断。")]
    [SerializeField] private Transform cameraRigRoot;
    [Tooltip("镜头推进时是否旋转玩家根节点以匹配 cameraApproachTarget.rotation。若你希望剧情期间保持玩家Yaw=0，请关闭。")]
    [SerializeField] private bool rotateRigDuringApproach = false;

    [Header("剧情结束后解锁交互")]
    [Tooltip("剧情开始时会被 SetEnabled(false)，结束时设回 true。可放入 EmaInteraction、RemiFrontApproachTrigger 等。")]
    [SerializeField] private MonoBehaviour[] enableOnFinish;
    [SerializeField] private MonoBehaviour[] disableDuringStory; // 开场禁用的交互脚本

    [Header("剧情结束后：角色散开站位（可选）")]
    [SerializeField] private Transform remiAfterStoryPoint;
    [SerializeField] private Transform emaAfterStoryPoint;
    [SerializeField] private Transform remiRoot;
    [SerializeField] private Transform emaRoot;

    [Header("剧情期间：Remi 世界 UI")]
    [Tooltip("演出时隐藏 Remi 的 RoleCanvas（头顶 Tip/Response），避免与剧情 UI 叠在一起；结束后再显示并按交互状态刷新。")]
    [SerializeField] private bool hideRemiRoleCanvasDuringStory = true;
    [Tooltip("留空则从 remiRoot 下自动查找 RemiRoleWorldUI")]
    [SerializeField] private RemiRoleWorldUI remiRoleWorldUi;

    [Header("剧情期间：Ema 世界 UI")]
    [Tooltip("演出时隐藏 Ema 的 RoleCanvas（头顶 Tip/Response），避免与剧情 UI 叠在一起；结束后再显示并按交互状态刷新。")]
    [SerializeField] private bool hideEmaRoleCanvasDuringStory = true;
    [Tooltip("留空则从 emaRoot 下自动查找 RemiRoleWorldUI（Ema 可复用同款组件）")]
    [SerializeField] private RemiRoleWorldUI emaRoleWorldUi;

    [Header("看向摄像机（可选）")]
    [Tooltip("需要在某句台词触发时，转向摄像机的角色根节点/头部节点。")]
    [SerializeField] private Transform[] charactersLookAtCamera;
    [SerializeField] private float charactersLookAtTurnSpeed = 8f;

    [Header("剧情结束 · Presence")]
    [Tooltip("教室开场专用：写入 StoryClassroomOpened。")]
    [SerializeField] private bool applyStoryClassroomOpenedOnFinish = true;
    [Tooltip("教室开场播完后写入 PlayerPrefs，再次进入教室不再自动重播黑屏/开场剧情。")]
    [SerializeField] private bool persistClassroomOpeningPlayed = true;
    [Tooltip("共现场景专用：结束时确保 CoPresence episode（占满 phase）。")]
    [SerializeField] private bool ensureCoPresenceEpisodeOnFinish;

    public const string PrefsClassroomOpeningPlayed = "RemiStory_ClassroomOpening";

    [Header("教室开场结束 · 手机")]
    [SerializeField] private bool unlockPhoneAfterClassroomStory = true;
    [TextArea(1, 2)]
    [SerializeField] private string phoneUnlockHintMessage = "已添加 Remi 和 Ema 为联系人！";

    [Header("自动播放")]
    [Tooltip("无语音时每句最短等待（秒）。")]
    [SerializeField] private float autoPlayMinDelayNoVoice = 1.2f;
    [Tooltip("无语音时按字数追加的等待（秒/字）。")]
    [SerializeField] private float autoPlaySecondsPerChar = 0.04f;
    [Tooltip("无语音时每句最长等待（秒）。")]
    [SerializeField] private float autoPlayMaxDelayNoVoice = 12f;
    [Tooltip("有语音的句子播完后，再隔这么久自动下一句。")]
    [SerializeField] private float autoPlayGapAfterVoice = 0.25f;

    private int _idx = -1;
    private bool _waitingVoice;
    private bool _finished;
    private bool _started;
    private StoryPanel _panel;
    private RemiRoleWorldUI _resolvedRemiRoleUi;
    private RemiRoleWorldUI _resolvedEmaRoleUi;

    private bool _autoPlayEnabled;
    private Coroutine _autoAdvanceCo;

    public bool AutoPlayEnabled => _autoPlayEnabled;
    public bool HasStarted => _started;
    public bool IsFinished => _finished;

    /// <summary>剧情正常结束或跳过后触发（可用于衔接自由对话 SendSystem 等）。</summary>
    public event Action StoryFinished;

    /// <summary>触发前注入台词（如分支剧情）；会替换 Inspector 中的 lines。</summary>
    public void SetLines(List<StoryLine> newLines)
    {
        lines = newLines ?? new List<StoryLine>();
    }

    /// <summary>调试或重复播放前重置（一般一次性剧情不必调用）。</summary>
    public void ResetStoryPlaybackState()
    {
        _started = false;
        _finished = false;
        _idx = -1;
        _waitingVoice = false;
    }

    /// <summary>触发前注入黑屏开场旁白（替换 Inspector 默认列表）。</summary>
    public void SetBlackScreenIntroLines(List<string> introLines)
    {
        blackScreenIntroLines = introLines ?? new List<string>();
    }

    /// <summary>脊柱/过场用：不触发教室开场、不自动 CoPresence。</summary>
    public void ConfigureAsOverlayBeatDirector()
    {
        showStoryPanelOnStart = false;
        playBlackScreenIntro = false;
        applyStoryClassroomOpenedOnFinish = false;
        ensureCoPresenceEpisodeOnFinish = false;
        moveCameraOnBegin = false;
    }

    /// <summary>场景内 Trigger 触发剧情前：跳过黑屏/镜头推进，并解析玩家引用与锁定参数。</summary>
    public void PrepareForTriggeredEpisode()
    {
        playBlackScreenIntro = false;
        moveCameraOnBegin = false;
        takeoverPlayerControl = true;

        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (cameraRigRoot == null && playerController != null)
            cameraRigRoot = playerController.transform;

        if (playerController != null)
        {
            if (lockPlayerYawOnBegin)
                playerYawDegrees = playerController.transform.eulerAngles.y;
            if (lockPlayerYOnBegin)
                playerY = playerController.transform.position.y;
        }

        EnsureFirstPersonLookDisabledDuringStory();
    }

    private void EnsureFirstPersonLookDisabledDuringStory()
    {
        if (disablePlayerBehavioursDuringStory != null && disablePlayerBehavioursDuringStory.Length > 0)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        foreach (Behaviour b in cam.GetComponents<Behaviour>())
        {
            if (b == null || b == playerController)
                continue;
            if (b.GetType().Name == "FirstPersonCameraLook")
            {
                disablePlayerBehavioursDuringStory = new MonoBehaviour[] { b as MonoBehaviour };
                break;
            }
        }
    }

    public Transform GetRemiRoot() => remiRoot;

    public void SetRemiRoot(Transform root)
    {
        remiRoot = root;
        _resolvedRemiRoleUi = null;
    }

    public void SetRemiAfterStoryPoint(Transform point) => remiAfterStoryPoint = point;

    /// <summary>由 <see cref="StoryPanel"/> 的「自动播放」开关调用。</summary>
    public void SetAutoPlay(bool enabled)
    {
        _autoPlayEnabled = enabled;
        if (!enabled)
            StopAutoAdvanceRoutine();
    }

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (voiceSource == null)
        {
            voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        }

        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if ((disablePlayerBehavioursDuringStory == null || disablePlayerBehavioursDuringStory.Length == 0) && targetCamera != null)
        {
            foreach (Behaviour b in targetCamera.GetComponents<Behaviour>())
            {
                if (b == null || b == playerController)
                    continue;
                if (b.GetType().Name == "FirstPersonCameraLook")
                {
                    disablePlayerBehavioursDuringStory = new MonoBehaviour[] { b as MonoBehaviour };
                    break;
                }
            }
        }

        if (cameraRigRoot == null)
        {
            // 优先用玩家根节点；否则用相机父物体；最后退化为相机自身
            if (playerController != null) cameraRigRoot = playerController.transform;
            else if (targetCamera != null && targetCamera.transform.parent != null) cameraRigRoot = targetCamera.transform.parent;
            else if (targetCamera != null) cameraRigRoot = targetCamera.transform;
        }
    }

    private void Start()
    {
        if (!showStoryPanelOnStart)
            return;

        if (ShouldSkipAutoStartOpening())
        {
            // 旧档迁移：补写已播标记，避免仅靠手机解锁判断
            if (persistClassroomOpeningPlayed && PlayerPrefs.GetInt(PrefsClassroomOpeningPlayed, 0) == 0)
            {
                PlayerPrefs.SetInt(PrefsClassroomOpeningPlayed, 1);
                PlayerPrefs.Save();
            }

            ApplyAlreadyCompletedOpeningState();
            return;
        }

        BeginStory();
    }

    /// <summary>
    /// 教室开场已播过（PlayerPrefs / 脊柱进度），再次加载教室时不再黑屏+开场台词。
    /// </summary>
    private bool ShouldSkipAutoStartOpening()
    {
        if (persistClassroomOpeningPlayed &&
            PlayerPrefs.GetInt(PrefsClassroomOpeningPlayed, 0) != 0)
            return true;

        // 进度已过开场（交书或更后）时也跳过，避免旧档无 Prefs 仍重播
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
        if (spine != null && spine.CurrentBeat >= RemiDemoSpineBeat.Day1BookSubmitted)
            return true;

        if (PhoneAppAccess.IsUnlocked && applyStoryClassroomOpenedOnFinish)
            return true;

        return false;
    }

    /// <summary>二次进入教室：直接进入开场后的可交互状态，不重播过场。</summary>
    private void ApplyAlreadyCompletedOpeningState()
    {
        _started = true;
        _finished = true;
        _idx = lines != null ? lines.Count : 0;
        _waitingVoice = false;
        _autoPlayEnabled = false;

        if (remiRoot != null && remiAfterStoryPoint != null)
        {
            remiRoot.position = remiAfterStoryPoint.position;
            remiRoot.rotation = remiAfterStoryPoint.rotation;
        }

        if (emaRoot != null && emaAfterStoryPoint != null)
        {
            emaRoot.position = emaAfterStoryPoint.position;
            emaRoot.rotation = emaAfterStoryPoint.rotation;
        }

        SetRemiRoleCanvasForStoryPlaying(false);
        SetEmaRoleCanvasForStoryPlaying(false);
        SetEnabled(disableDuringStory, true);
        SetEnabled(enableOnFinish, true);

        if (takeoverPlayerControl)
        {
            playerController?.SetMoveLock(false);
            playerController?.SetLookLock(false);
            SetEnabled(disablePlayerBehavioursDuringStory, true);
        }

        RemiWorldPlacement.EnsureDay2AbsentInClassroom();
    }

    /// <summary>
    /// 外部触发开始剧情（例如：玩家开门后）。
    /// </summary>
    public void BeginStory()
    {
        if (_started) return;
        _started = true;
        _finished = false;
        _waitingVoice = false;
        _idx = -1;

        // 开场先禁用交互（避免玩家直接跳进自由对话）
        SetEnabled(disableDuringStory, false);
        SetEnabled(enableOnFinish, false);

        if (takeoverPlayerControl)
        {
            // 锁定玩家移动与水平转向，并禁用俯仰视角脚本，确保镜头推进/演出不被玩家输入打断
            playerController?.SetMoveLock(true);
            playerController?.SetLookLock(true);
            SetEnabled(disablePlayerBehavioursDuringStory, false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 统一在 StoryDirector 里锁定玩家朝向/高度（避免 StoryTriggerZone 重复控制造成 90° 问题）
        if (cameraRigRoot != null)
        {
            if (lockPlayerYawOnBegin)
            {
                Vector3 euler = cameraRigRoot.eulerAngles;
                euler.y = playerYawDegrees;
                cameraRigRoot.eulerAngles = euler;
            }
            if (lockPlayerYOnBegin)
            {
                Vector3 pos = cameraRigRoot.position;
                pos.y = playerY;
                cameraRigRoot.position = pos;
            }
        }

        SetRemiRoleCanvasForStoryPlaying(true);
        SetEmaRoleCanvasForStoryPlaying(true);

        StartCoroutine(CoBeginStoryFlow());
    }

    private IEnumerator CoBeginStoryFlow()
    {
        if (ShouldPlayBlackScreenIntro())
            yield return StoryBlackScreenInterlude.Play(blackScreenIntroLines);

        if (_finished)
            yield break;

        UiManager.Instance.canvasObj.SetActive(true);
        UiManager.EnsureEventSystem();
        _panel = UiManager.Instance.ShowPanel<StoryPanel>();
        if (_panel == null)
        {
            Debug.LogError("[StoryDirector] 无法显示 StoryPanel，开场剧情中止。");
            yield break;
        }
        _panel.Bind(this);
        BeginMainStoryPresentation();
    }

    private bool ShouldPlayBlackScreenIntro() =>
        playBlackScreenIntro && blackScreenIntroLines != null && blackScreenIntroLines.Count > 0;

    private void BeginMainStoryPresentation()
    {
        if (moveCameraOnBegin && targetCamera != null && cameraApproachTarget != null)
        {
            StartCoroutine(MoveCameraTo(cameraApproachTarget.position, cameraApproachTarget.rotation, cameraApproachDuration));
        }

        _idx = -1;
        Next();
    }

    private void Update()
    {
        if (_finished)
            return;

        if (_started && _panel != null && _panel.gameObject.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                Next();
        }
    }

    public void Next()
    {
        if (_finished) return;
        if (_waitingVoice) return; // 播放中不允许下一句（保证节奏稳定）

        StopAutoAdvanceRoutine();

        _idx++;
        if (_idx >= lines.Count)
        {
            FinishStory();
            return;
        }

        PlayLine(lines[_idx]);
    }

    public void Skip()
    {
        if (_finished) return;
        StopAutoAdvanceRoutine();
        StopAllCoroutines();
        if (voiceSource != null) voiceSource.Stop();
        FinishStory();
    }

    private void PlayLine(StoryLine line)
    {
        StopAutoAdvanceRoutine();

        if (_panel != null)
        {
            _panel.SetLine(line.speakerName, line.text);
            _panel.SetNextInteractable(line.voice == null);
        }

        if (line.makeCharactersLookAtCamera)
        {
            StartCoroutine(TurnCharactersToCameraOnce());
        }

        if (line.voice != null && voiceSource != null)
        {
            StartCoroutine(PlayVoiceAndUnlockNext(line.voice));
        }
        else if (_autoPlayEnabled)
        {
            ScheduleAutoAdvance(ReadDelaySecondsForLine(line.text));
        }
    }

    private IEnumerator PlayVoiceAndUnlockNext(AudioClip clip)
    {
        _waitingVoice = true;
        if (_panel != null) _panel.SetNextInteractable(false);

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();

        yield return new WaitForSeconds(Mathf.Max(0f, clip.length));

        _waitingVoice = false;
        if (_panel != null) _panel.SetNextInteractable(true);
        if (_autoPlayEnabled)
            ScheduleAutoAdvance(Mathf.Max(0f, autoPlayGapAfterVoice));
    }

    private float ReadDelaySecondsForLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return autoPlayMinDelayNoVoice;
        float t = autoPlayMinDelayNoVoice + text.Length * autoPlaySecondsPerChar;
        return Mathf.Clamp(t, autoPlayMinDelayNoVoice, autoPlayMaxDelayNoVoice);
    }

    private void ScheduleAutoAdvance(float delaySeconds)
    {
        if (!_autoPlayEnabled || _finished) return;
        StopAutoAdvanceRoutine();
        _autoAdvanceCo = StartCoroutine(CoAutoAdvanceAfter(delaySeconds));
    }

    private void StopAutoAdvanceRoutine()
    {
        if (_autoAdvanceCo == null) return;
        StopCoroutine(_autoAdvanceCo);
        _autoAdvanceCo = null;
    }

    private IEnumerator CoAutoAdvanceAfter(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        _autoAdvanceCo = null;
        if (_finished || !_autoPlayEnabled) yield break;
        Next();
    }

    private void FinishStory()
    {
        if (_finished) return;
        _finished = true;

        StopAutoAdvanceRoutine();
        _autoPlayEnabled = false;
        if (_panel != null)
            _panel.SetAutoPlayToggleVisual(false);

        if (voiceSource != null) voiceSource.Stop();

        // 立即销毁：StoryPanel 曾因自写 Update 打断淡出；跳过时也避免面板残留
        UiManager.Instance.HidePanel<StoryPanel>(isFade: false);

        // 角色散开（用于从“剧情演出”过渡到“可交互自由对话”）
        if (remiRoot != null && remiAfterStoryPoint != null)
        {
            remiRoot.position = remiAfterStoryPoint.position;
            remiRoot.rotation = remiAfterStoryPoint.rotation;
        }
        if (emaRoot != null && emaAfterStoryPoint != null)
        {
            emaRoot.position = emaAfterStoryPoint.position;
            emaRoot.rotation = emaAfterStoryPoint.rotation;
        }

        SetRemiRoleCanvasForStoryPlaying(false);
        SetEmaRoleCanvasForStoryPlaying(false);

        // 解锁交互
        SetEnabled(disableDuringStory, true);
        SetEnabled(enableOnFinish, true);

        if (takeoverPlayerControl)
        {
            playerController?.SetMoveLock(false);
            playerController?.SetLookLock(false);
            SetEnabled(disablePlayerBehavioursDuringStory, true);
            // 是否锁回鼠标由你自由对话阶段决定；这里不强制锁定
        }

        if (applyStoryClassroomOpenedOnFinish)
        {
            if (persistClassroomOpeningPlayed)
            {
                PlayerPrefs.SetInt(PrefsClassroomOpeningPlayed, 1);
                PlayerPrefs.Save();
            }

            RemiPresenceService.Instance?.ApplyPresenceEvent(RemiPresenceEventKind.StoryClassroomOpened);
            if (unlockPhoneAfterClassroomStory)
            {
                PhoneAppAccess.Unlock();
                StoryNarrativeHintView.TryPlayAfterPhoneContactsAdded(phoneUnlockHintMessage);
            }
        }

        if (ensureCoPresenceEpisodeOnFinish)
            RemiPresenceService.Instance?.BeginCoPresenceEpisode(occupiesPhase: true);

        StoryFinished?.Invoke();
    }

    private void SetRemiRoleCanvasForStoryPlaying(bool storyPlaying)
    {
        if (!hideRemiRoleCanvasDuringStory) return;

        if (_resolvedRemiRoleUi == null)
        {
            _resolvedRemiRoleUi = remiRoleWorldUi;
            if (_resolvedRemiRoleUi == null && remiRoot != null)
                _resolvedRemiRoleUi = remiRoot.GetComponentInChildren<RemiRoleWorldUI>(true);
        }

        if (_resolvedRemiRoleUi == null) return;

        _resolvedRemiRoleUi.gameObject.SetActive(true);
        _resolvedRemiRoleUi.ApplyStoryPlaying(storyPlaying);
        if (storyPlaying)
            return;

        RemiInteraction interaction = remiRoot != null ? remiRoot.GetComponent<RemiInteraction>() : null;
        if (interaction != null)
            _resolvedRemiRoleUi.ApplyState(interaction.IsPlayerInRange, interaction.IsInDialogue);
        else
            _resolvedRemiRoleUi.ApplyState(false, false);
    }

    private void SetEmaRoleCanvasForStoryPlaying(bool storyPlaying)
    {
        if (!hideEmaRoleCanvasDuringStory) return;

        if (_resolvedEmaRoleUi == null)
        {
            _resolvedEmaRoleUi = emaRoleWorldUi;
            if (_resolvedEmaRoleUi == null && emaRoot != null)
                _resolvedEmaRoleUi = emaRoot.GetComponentInChildren<RemiRoleWorldUI>(true);
        }

        if (_resolvedEmaRoleUi == null) return;

        _resolvedEmaRoleUi.gameObject.SetActive(true);
        _resolvedEmaRoleUi.ApplyStoryPlaying(storyPlaying);
        if (storyPlaying)
            return;

        EmaInteraction interaction = emaRoot != null ? emaRoot.GetComponentInChildren<EmaInteraction>(true) : null;
        if (interaction != null)
            _resolvedEmaRoleUi.ApplyState(interaction.IsPlayerInRange, interaction.IsInDialogue);
        else
            _resolvedEmaRoleUi.ApplyState(false, false);
    }

    private static void SetEnabled(MonoBehaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].enabled = enabled;
        }
    }

    private IEnumerator MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        if (targetCamera == null) yield break;
        if (cameraRigRoot == null) yield break;
        duration = Mathf.Max(0.01f, duration);

        // 目标是让“相机本身”到达 targetPos/targetRot，但实际移动/旋转的是 cameraRigRoot
        Vector3 startRigPos = cameraRigRoot.position;
        Quaternion startRigRot = cameraRigRoot.rotation;

        Quaternion cameraLocalRot = targetCamera.transform.localRotation;
        Vector3 cameraLocalPos = targetCamera.transform.localPosition;

        Quaternion desiredRigRot = startRigRot;
        if (rotateRigDuringApproach)
        {
            // rigRot * cameraLocalRot = targetRot  => rigRot = targetRot * inv(cameraLocalRot)
            desiredRigRot = targetRot * Quaternion.Inverse(cameraLocalRot);
        }
        // rigPos + rigRot * cameraLocalPos = targetPos => rigPos = targetPos - rigRot * cameraLocalPos
        Vector3 desiredRigPos = targetPos - (desiredRigRot * cameraLocalPos);

        float t = 0f;
        while (t < duration && !_finished)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = cameraApproachEase != null ? cameraApproachEase.Evaluate(u) : u;

            cameraRigRoot.position = Vector3.Lerp(startRigPos, desiredRigPos, eased);
            if (rotateRigDuringApproach)
            {
                cameraRigRoot.rotation = Quaternion.Slerp(startRigRot, desiredRigRot, eased);
            }

            yield return null;
        }
    }

    private IEnumerator TurnCharactersToCameraOnce()
    {
        if (targetCamera == null) yield break;
        if (charactersLookAtCamera == null || charactersLookAtCamera.Length == 0) yield break;

        // 让角色在短时间内平滑朝向摄像机（只转Y轴，避免低头仰头怪异）
        float t = 0f;
        const float maxSeconds = 0.6f;
        while (t < maxSeconds && !_finished)
        {
            t += Time.deltaTime;
            foreach (var tr in charactersLookAtCamera)
            {
                if (tr == null) continue;
                Vector3 dir = targetCamera.transform.position - tr.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) continue;
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, Time.deltaTime * charactersLookAtTurnSpeed);
            }
            yield return null;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Classroom Opening Played Flag")]
    private void Editor_ClearClassroomOpeningPlayedFlag()
    {
        PlayerPrefs.DeleteKey(PrefsClassroomOpeningPlayed);
        ResetStoryPlaybackState();
        Debug.Log("[StoryDirector] Cleared RemiStory_ClassroomOpening; next Play can auto-start opening again.");
    }
#endif
}

