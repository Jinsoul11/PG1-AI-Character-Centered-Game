using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 靠近 NPC 按交互键打开对话 UI（DialoguePanel）。支持 Trigger 与纯距离两种判定。
/// 默认：将玩家锁到配置世界位姿，解锁鼠标，禁用移动与视角；结束则还原。
/// Day1 找书窗口期可改为就地互视（不传送玩家）。
/// </summary>
public class RemiInteraction : MonoBehaviour
{
    [Header("交互")]
    public float interactRange = 5f;
    public KeyCode interactKey = KeyCode.F;
    public string interactPromptText;

    [Tooltip("为 true 时，只要在 interactRange 内即可交互")]
    public bool useDistanceCheck = true;

    [Header("对话姿态（世界空间，不修改父物体）")]
    [Tooltip("若指定，则优先使用该物体的世界位置与旋转（可在场景里摆好空物体再拖引用）")]
    public Transform dialoguePoseReference;

    [Tooltip("对话开始时玩家的世界坐标（未指定 reference 时使用）")]
    public Vector3 dialogueWorldPosition = new Vector3(11.927f, 0.853f, -2.687f);

    [Tooltip("对话开始时玩家的世界欧拉角（未指定 reference 时使用；-90° 与 270° 等价）")]
    public Vector3 dialogueWorldEuler = new Vector3(0f, -90f, 0f);

    [Tooltip("是否在对话中应用上述姿态；关闭则只锁输入不切位置")]
    public bool applyDialogueWorldPose = true;

    [Tooltip("对话期间每帧强制保持世界位姿（防止其它脚本改 transform）")]
    public bool forceSnapPoseEachFrame = true;

    [Tooltip("Day1 找书 / Day2 共现 Window·Anchor：就地互视，不传送玩家到固定对话点。")]
    [SerializeField] private bool inPlaceDialogueDuringBookWindow = true;

    [Header("Remi 对话朝向")]
    [Tooltip("自由对话时 Remi 根节点 Y 轴朝向；结束对话后恢复为 remiIdleYawDegrees")]
    public bool applyRemiDialogueYaw;
    [SerializeField] private float remiDialogueYawDegrees = -90f;
    [SerializeField] private float remiIdleYawDegrees;

    [Header("视角脚本（可选，自动会搜；若有自定义鼠标视角可拖到下面）")]
    [Tooltip("对话时额外禁用的脚本（例如第三方 MouseLook）")]
    public MonoBehaviour[] extraInputConsumersToDisable;

    [Header("UI（可选）")]
    [Tooltip("RoleCanvas 上的世界 UI 控制器；未拖则自动在子物体中查找")]
    [SerializeField] private RemiRoleWorldUI roleWorldUi;

    [Tooltip("兼容旧用法：仅当未使用 RemiRoleWorldUI 时，直接控制该 TMP 显隐")]
    public TMP_Text worldPromptText;

    [Header("书本任务（可选）")]
    [Tooltip("等交书且玩家已拿到书、靠近 Remi 时，头顶提示优先显示该句（与 RemiBookSubmitInteractable 的 E 交书一致）。")]
    [SerializeField] private string bookSubmitTipWhenHolding = "按E把书交给Remi。";
    [SerializeField] private KeyCode bookSubmitKey = KeyCode.E;

    public bool IsPlayerInRange => _isPlayerInRange;

    /// <summary>是否已与 Remi 打开对话 UI（按 F 后直至关闭）。</summary>
    public bool IsInDialogue => _isTalking;

    private bool _isPlayerInRange;
    private bool _playerInsideTrigger;
    private bool _isTalking;
    /// <summary>图书馆 Day2 Anchor 短 Story 播放中：隐藏 RoleCanvas Tip，并禁止按 F。</summary>
    private bool _faceUiLockedForDay2LibraryIntro;
    /// <summary>Ending 回顾终幕：隐藏 Tip 并禁止按 F（回公寓新 Remi 也会生效）。</summary>
    private bool _faceUiLockedForEnding;

    private Transform _cachedPlayer;

    private Transform _dialoguePlayerRoot;
    private PlayerController _dialoguePlayerController;
    private bool _playerPoseSaved;

    private Vector3 _savedWorldPosition;
    private Quaternion _savedWorldRotation;

    private CharacterController _playerCharacterController;
    private bool _characterControllerWasEnabled;

    private Vector3 _resolvedDialoguePosition;
    private Quaternion _resolvedDialogueRotation;

    private bool _inplaceDialogueSession;
    private bool _remiRotationSavedForInPlace;
    private Quaternion _savedRemiRotationForInPlace;

    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;

    private readonly List<Behaviour> _behavioursDisabledForDialogue = new List<Behaviour>();
    private readonly List<bool> _behavioursPrevEnabled = new List<bool>();

    private Transform _playerCameraTransform;
    private bool _cameraIsChildOfPlayer;

    private void Start()
    {
        if (roleWorldUi == null)
            roleWorldUi = GetComponentInChildren<RemiRoleWorldUI>(true);

        if (roleWorldUi != null)
        {
            if (worldPromptText == null)
                worldPromptText = roleWorldUi.TipLine;
            roleWorldUi.SetTipPrompt(ResolveInteractionTipText());
            if (RemiDemoMemoryRecapEndingFlow.Instance != null &&
                RemiDemoMemoryRecapEndingFlow.Instance.IsBlockingDialogueExit)
            {
                _faceUiLockedForEnding = true;
                roleWorldUi.ApplyStoryPlaying(true);
            }
            else if (ShouldLockFaceUiForDay2LibraryIntro())
            {
                _faceUiLockedForDay2LibraryIntro = true;
                roleWorldUi.ApplyStoryPlaying(true);
            }
            else
            {
                roleWorldUi.ApplyState(false, false);
            }
        }
        else if (worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (TryApplyEndingFaceUiLock())
            return;

        if (TryApplyDay2LibraryIntroFaceUiLock())
            return;

        RefreshPlayerProximity();

        if (!_isTalking && _isPlayerInRange && Input.GetKeyDown(bookSubmitKey))
        {
            RemiBookQuestFlow bookFlow = RemiBookQuestFlow.Instance;
            if (bookFlow != null && bookFlow.HasBookForSubmission()
                && !bookFlow.IsSequenceRunning && !bookFlow.IsAwaitingGoodbyeConfirm)
            {
                bookFlow.TrySubmitBookFromWorld(this);
                return;
            }
        }

        if (_isPlayerInRange && !_isTalking && Input.GetKeyDown(interactKey))
        {
            if (SceneTravelPanel.IsOpen)
                return;

            RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
            // Day2 Window：按 F 先播 Remi Anchor Story，再进自由聊
            if (day2 != null && day2.TryBeginFaceApproachAnchor(this))
                return;

            // Day2 自习：按 F 固定点问（不开面板）
            if (day2 != null && day2.TryStudyWhisperAsk(this))
                return;

            StartDialogue();
        }

        if (_isTalking && !_isPlayerInRange)
        {
            if (RemiBookQuestFlow.Instance != null && RemiBookQuestFlow.Instance.IsBlockingDialogueExit)
                return;
            if (RemiLibraryDay2CoPresenceFlow.Instance != null &&
                RemiLibraryDay2CoPresenceFlow.Instance.IsBlockingDialogueExit)
                return;
            if (RemiDemoMemoryRecapEndingFlow.Instance != null &&
                RemiDemoMemoryRecapEndingFlow.Instance.IsBlockingDialogueExit)
                return;
            EndDialogue();
        }
    }

    /// <summary>
    /// Ending 终幕期间：隐藏 Tip 并禁止按 F（含回公寓后新建的 RemiInteraction）。
    /// </summary>
    private bool TryApplyEndingFaceUiLock()
    {
        bool shouldLock = RemiDemoMemoryRecapEndingFlow.Instance != null &&
                          RemiDemoMemoryRecapEndingFlow.Instance.IsBlockingDialogueExit;
        if (shouldLock)
        {
            if (!_faceUiLockedForEnding)
            {
                _faceUiLockedForEnding = true;
                if (roleWorldUi == null)
                    roleWorldUi = GetComponentInChildren<RemiRoleWorldUI>(true);
                if (roleWorldUi != null)
                    roleWorldUi.ApplyStoryPlaying(true);
                else if (worldPromptText != null)
                    worldPromptText.gameObject.SetActive(false);
            }

            return true;
        }

        if (_faceUiLockedForEnding)
        {
            _faceUiLockedForEnding = false;
            if (roleWorldUi != null)
            {
                roleWorldUi.ApplyStoryPlaying(false);
                roleWorldUi.SetTipPrompt(ResolveInteractionTipText());
                roleWorldUi.ApplyState(_isPlayerInRange, _isTalking);
            }
        }

        return false;
    }

    /// <summary>
    /// 图书馆 Day2：仅 Anchor 短 Story 播放中隐藏 Tip 并禁止按 F；
    /// Window 期可按 F（会进 Story）；Story 结束后可自由聊。
    /// </summary>
    private bool TryApplyDay2LibraryIntroFaceUiLock()
    {
        bool shouldLock = ShouldLockFaceUiForDay2LibraryIntro();
        if (shouldLock)
        {
            if (!_faceUiLockedForDay2LibraryIntro)
            {
                _faceUiLockedForDay2LibraryIntro = true;
                if (roleWorldUi != null)
                    roleWorldUi.ApplyStoryPlaying(true);
                else if (worldPromptText != null)
                    worldPromptText.gameObject.SetActive(false);
            }

            return true;
        }

        if (_faceUiLockedForDay2LibraryIntro)
        {
            _faceUiLockedForDay2LibraryIntro = false;
            if (roleWorldUi != null)
            {
                roleWorldUi.ApplyStoryPlaying(false);
                roleWorldUi.SetTipPrompt(ResolveInteractionTipText());
                roleWorldUi.ApplyState(_isPlayerInRange, _isTalking);
            }
        }

        return false;
    }

    private static bool ShouldLockFaceUiForDay2LibraryIntro()
    {
        if (SceneTravelCatalog.ResolveFromActiveScene() != SceneTravelLocation.Library)
            return false;

        // 仅 Anchor 短 Story 播放中锁 F
        RemiLibraryDay2CoPresenceStory beat = FindObjectOfType<RemiLibraryDay2CoPresenceStory>();
        if (beat != null && beat.IsPlayingAnchorIntro)
            return true;

        RemiLibraryDay2CoPresenceFlow flow = RemiLibraryDay2CoPresenceFlow.Instance;
        return flow != null && flow.IsSequenceRunning;
    }

    /// <summary>图书馆共现结束后立刻刷新 RoleCanvas（Tip / Response）。</summary>
    public void RefreshRoleWorldUiAfterStory()
    {
        _faceUiLockedForDay2LibraryIntro = false;
        if (roleWorldUi == null)
            roleWorldUi = GetComponentInChildren<RemiRoleWorldUI>(true);
        if (roleWorldUi == null)
            return;

        roleWorldUi.ApplyStoryPlaying(false);
        roleWorldUi.SetTipPrompt(ResolveInteractionTipText());
        roleWorldUi.ApplyState(_isPlayerInRange, _isTalking);
    }

    private void LateUpdate()
    {
        if (!forceSnapPoseEachFrame || !applyDialogueWorldPose || !_isTalking || !_playerPoseSaved || _dialoguePlayerRoot == null)
            return;

        Transform pt = _dialoguePlayerRoot;
        pt.SetPositionAndRotation(_resolvedDialoguePosition, _resolvedDialogueRotation);

        if (_cameraIsChildOfPlayer && _playerCameraTransform != null)
        {
            Vector3 e = _playerCameraTransform.localEulerAngles;
            e.x = 0f;
            e.z = 0f;
            _playerCameraTransform.localEulerAngles = e;
        }
    }

    private void RefreshPlayerProximity()
    {
        if (_cachedPlayer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                _cachedPlayer = p.transform;
            else
            {
                PlayerController pc = FindObjectOfType<PlayerController>();
                if (pc != null)
                    _cachedPlayer = pc.transform;
            }
        }

        bool byDist = false;
        if (useDistanceCheck && _cachedPlayer != null && interactRange > 0f)
        {
            float d = Vector3.Distance(transform.position, _cachedPlayer.position);
            byDist = d <= interactRange;
        }

        bool wasInRange = _isPlayerInRange;
        _isPlayerInRange = _playerInsideTrigger || byDist;

        if (roleWorldUi != null)
        {
            if (!_isTalking)
            {
                string tip = _isPlayerInRange ? ResolveInteractionTipText() : (interactPromptText ?? string.Empty);
                roleWorldUi.SetTipPrompt(tip);
            }

            if (_isPlayerInRange == wasInRange || _isTalking)
                return;

            roleWorldUi.ApplyState(_isPlayerInRange, _isTalking);
        }
        else if (worldPromptText != null)
        {
            if (_isPlayerInRange == wasInRange || _isTalking)
                return;

            worldPromptText.gameObject.SetActive(_isPlayerInRange);
            if (_isPlayerInRange)
                worldPromptText.text = ResolveInteractionTipText();
        }
    }

    private string ResolveInteractionTipText()
    {
        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow != null && flow.HasBookForSubmission() && !string.IsNullOrWhiteSpace(bookSubmitTipWhenHolding))
            return bookSubmitTipWhenHolding.Trim();

        RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2 != null && day2.IsQuestFeatureEnabled)
        {
            if (day2.IsStudying)
                return day2.ResolveStudyWhisperTip();
            if (day2.HasCompletedStudyFarewell)
                return "可以离开图书馆了";
            if (day2.IsInFarewell)
                return string.Empty;
        }

        return interactPromptText ?? string.Empty;
    }

    private void FlashInteractionTip(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (roleWorldUi != null)
        {
            roleWorldUi.SetTipPrompt(message.Trim());
            roleWorldUi.ApplyState(_isPlayerInRange, false);
        }
        else if (worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(true);
            worldPromptText.text = message.Trim();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInsideTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInsideTrigger = false;
    }

    public void StartDialogue()
    {
        StartDialogue(bypassOpenGates: false);
    }

    public bool StartDialogue(bool bypassOpenGates)
    {
        if (_isTalking)
            return false;

        if (!bypassOpenGates && ShouldLockFaceUiForDay2LibraryIntro())
            return false;

        // Day2 Window：任何开对话入口都先进 Anchor Story（与按 F 一致）
        if (!bypassOpenGates)
        {
            RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
            if (day2 != null && day2.TryBeginFaceApproachAnchor(this))
                return false;

            // 自习 / 告别中：面板仅告别锁；自习走点问，不在此开面板
            if (day2 != null && (day2.IsStudying || day2.IsInFarewell))
                return false;
        }

        if (!bypassOpenGates &&
            RemiBookQuestFlow.Instance != null && !RemiBookQuestFlow.Instance.CanPlayerOpenRemiDialogue())
            return false;

        if (!bypassOpenGates &&
            RemiPresenceService.Instance != null && !RemiPresenceService.Instance.CanOpenFaceToFaceDialogue())
        {
            FlashInteractionTip(RemiPresenceAvailability.GetFaceToFaceUnavailableMessage(RemiPresenceService.Instance));
            return false;
        }

        if (!bypassOpenGates &&
            RemiPresenceService.Instance != null &&
            !RemiInteractionChannelPolicy.CanPlayerUseFaceToFaceChannel(RemiPresenceService.Instance))
        {
            FlashInteractionTip(
                RemiPresenceAvailability.GetFaceToFaceNotCoLocatedMessage(RemiPresenceService.Instance));
            return false;
        }

        RemiPresenceService.Instance?.SetInteractionChannel(RemiInteractionChannel.FaceToFace);
        RemiPresenceService.Instance?.OnFaceToFaceSessionOpened();
        RemiBookQuestFlow.Instance?.NotifyFaceDialogueOpened();
        RemiLibraryDay2CoPresenceFlow.Instance?.NotifyFaceDialogueOpened();

        _isTalking = true;

        _dialoguePlayerController = null;
        if (_cachedPlayer != null)
            _dialoguePlayerController = _cachedPlayer.GetComponent<PlayerController>();
        if (_dialoguePlayerController == null)
            _dialoguePlayerController = FindObjectOfType<PlayerController>();

        _dialoguePlayerRoot = _dialoguePlayerController != null ? _dialoguePlayerController.transform : _cachedPlayer;

        _inplaceDialogueSession = ShouldUseInPlaceDialoguePose();
        if (_inplaceDialogueSession)
            ApplyInPlaceConversationFacing();
        else
        {
            ResolveDialoguePose();
            ApplyConversationWorldPose();
            ApplyRemiDialogueYaw();
        }

        SaveAndApplyCursorForDialogue(true);
        CollectAndDisableLookControls(true);
        _dialoguePlayerController?.SetMoveLock(true);
        _dialoguePlayerController?.SetLookLock(true);

        UiManager.EnsureCanvasActive();
        UiManager.Instance.ShowPanel<DialoguePanel>();
        UiManager.Instance.HidePanel<ChatHistoryPanel>();

        GetComponent<Remi>()?.SetDialogueBodyIdle(true);

        if (roleWorldUi != null)
        {
            DialogueSequenceDirector director = DialogueSequenceDirector.Instance;
            bool suppressResponse = director != null
                && !DialogueSequenceDirector.ShowsResponseText(
                    director.ResolveMode(RemiInteractionChannel.FaceToFace));
            roleWorldUi.SetSuppressResponseVisual(suppressResponse);
            roleWorldUi.ApplyState(_isPlayerInRange, true);
        }
        else if (worldPromptText != null)
            worldPromptText.gameObject.SetActive(false);

        return true;
    }

    public void EndDialogue()
    {
        if (!_isTalking)
            return;

        _isTalking = false;

        PlayerController pc = _dialoguePlayerController != null ? _dialoguePlayerController : FindObjectOfType<PlayerController>();

        if (_inplaceDialogueSession)
            RestoreInPlaceConversationFacing();
        else
        {
            RestoreRemiDialogueYaw();
            RestoreConversationWorldPose();
        }

        _inplaceDialogueSession = false;
        CollectAndDisableLookControls(false);
        SaveAndApplyCursorForDialogue(false);
        pc?.SetMoveLock(false);
        pc?.SetLookLock(false);

        _dialoguePlayerController = null;

        UiManager.Instance.HidePanel<DialoguePanel>();
        UiManager.Instance.HidePanel<ChatHistoryPanel>();

        if (roleWorldUi != null)
        {
            roleWorldUi.SetTipPrompt(ResolveInteractionTipText());
            roleWorldUi.ApplyState(_isPlayerInRange, false);
        }
        else if (_isPlayerInRange && worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(true);
            worldPromptText.text = ResolveInteractionTipText();
        }

        _playerCameraTransform = null;
        _cameraIsChildOfPlayer = false;

        GetComponent<Remi>()?.SetDialogueBodyIdle(false);

        Animator anim = GetComponent<Animator>();
        anim?.SetBool("Talk", false);

        RemiPresenceService.Instance?.EndFaceToFaceSession(RemiEpisodeEndReason.Goodbye);

        if (DeepSeekDialogueManager.Instance != null)
            DeepSeekDialogueManager.Instance.ClearFaceMessageHistory();
    }

    public void ConfigureRemiDialogueYaw(bool enabled, float dialogueYaw = -90f, float idleYaw = 0f)
    {
        applyRemiDialogueYaw = enabled;
        remiDialogueYawDegrees = dialogueYaw;
        remiIdleYawDegrees = idleYaw;
    }

    public void SetDialoguePoseReference(Transform reference)
    {
        dialoguePoseReference = reference;
        applyDialogueWorldPose = reference != null;
    }

    /// <summary>Day1 找书 / Day2 共现：就地对话，避免拽到固定机位。</summary>
    private bool ShouldUseInPlaceDialoguePose()
    {
        if (!inPlaceDialogueDuringBookWindow)
            return false;

        RemiBookQuestFlow day1 = RemiBookQuestFlow.Instance;
        if (day1 != null && day1.IsQuestFeatureEnabled &&
            (day1.State == RemiBookQuestFlow.QuestState.WindowOpen ||
             day1.State == RemiBookQuestFlow.QuestState.WaitingForBook))
            return true;

        RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2 != null && day2.IsQuestFeatureEnabled &&
            (day2.State == RemiLibraryDay2CoPresenceFlow.FlowState.WindowOpen ||
             day2.State == RemiLibraryDay2CoPresenceFlow.FlowState.AnchorStory ||
             day2.State == RemiLibraryDay2CoPresenceFlow.FlowState.FreeChat))
            return true;

        return false;
    }

    private void ApplyInPlaceConversationFacing()
    {
        _playerPoseSaved = false;
        _playerCharacterController = null;
        _remiRotationSavedForInPlace = false;

        Transform player = _dialoguePlayerRoot;
        if (player == null)
            return;

        _savedRemiRotationForInPlace = transform.rotation;
        _remiRotationSavedForInPlace = true;
        FaceHorizontalYawToward(transform, player.position);
        FaceHorizontalYawToward(player, transform.position);

        _playerCameraTransform = Camera.main != null ? Camera.main.transform : null;
        _cameraIsChildOfPlayer = _playerCameraTransform != null &&
                                   _playerCameraTransform.IsChildOf(player);
        if (_cameraIsChildOfPlayer && _playerCameraTransform != null)
        {
            Vector3 e = _playerCameraTransform.localEulerAngles;
            e.x = 0f;
            e.z = 0f;
            _playerCameraTransform.localEulerAngles = e;
        }

        Physics.SyncTransforms();
    }

    private void RestoreInPlaceConversationFacing()
    {
        if (_remiRotationSavedForInPlace)
        {
            transform.rotation = _savedRemiRotationForInPlace;
            _remiRotationSavedForInPlace = false;
        }

        _playerCameraTransform = null;
        _cameraIsChildOfPlayer = false;
        _dialoguePlayerRoot = null;
    }

    private static void FaceHorizontalYawToward(Transform self, Vector3 worldTarget)
    {
        if (self == null)
            return;

        Vector3 delta = worldTarget - self.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f)
            return;

        self.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
    }

    private void ApplyRemiDialogueYaw()
    {
        if (!applyRemiDialogueYaw)
            return;

        Vector3 euler = transform.eulerAngles;
        euler.y = remiDialogueYawDegrees;
        transform.rotation = Quaternion.Euler(euler);
    }

    private void RestoreRemiDialogueYaw()
    {
        if (!applyRemiDialogueYaw)
            return;

        Vector3 euler = transform.eulerAngles;
        euler.y = remiIdleYawDegrees;
        transform.rotation = Quaternion.Euler(euler);
    }

    private void ResolveDialoguePose()
    {
        if (dialoguePoseReference != null)
        {
            _resolvedDialoguePosition = dialoguePoseReference.position;
            _resolvedDialogueRotation = dialoguePoseReference.rotation;
        }
        else
        {
            _resolvedDialoguePosition = dialogueWorldPosition;
            _resolvedDialogueRotation = Quaternion.Euler(dialogueWorldEuler);
        }
    }

    private void ApplyConversationWorldPose()
    {
        _playerPoseSaved = false;
        _playerCharacterController = null;

        if (!applyDialogueWorldPose || _dialoguePlayerRoot == null)
            return;

        Transform pt = _dialoguePlayerRoot;

        _savedWorldPosition = pt.position;
        _savedWorldRotation = pt.rotation;
        _playerPoseSaved = true;

        _playerCharacterController = pt.GetComponent<CharacterController>();
        if (_playerCharacterController != null)
        {
            _characterControllerWasEnabled = _playerCharacterController.enabled;
            _playerCharacterController.enabled = false;
        }

        pt.SetPositionAndRotation(_resolvedDialoguePosition, _resolvedDialogueRotation);
        Physics.SyncTransforms();

        _playerCameraTransform = Camera.main != null ? Camera.main.transform : null;
        _cameraIsChildOfPlayer = _playerCameraTransform != null &&
                                   _dialoguePlayerRoot != null &&
                                   _playerCameraTransform.IsChildOf(_dialoguePlayerRoot);
        // 对话结束前保持关闭，否则 CC 每帧改位置，无法稳定在世界坐标
    }

    private void RestoreConversationWorldPose()
    {
        if (!_playerPoseSaved || _dialoguePlayerRoot == null)
        {
            _dialoguePlayerRoot = null;
            _playerCharacterController = null;
            return;
        }

        Transform pt = _dialoguePlayerRoot;

        if (_playerCharacterController != null)
            _playerCharacterController.enabled = false;

        pt.SetPositionAndRotation(_savedWorldPosition, _savedWorldRotation);
        Physics.SyncTransforms();

        if (_playerCharacterController != null)
            _playerCharacterController.enabled = _characterControllerWasEnabled;

        _playerCharacterController = null;
        _playerPoseSaved = false;
        _dialoguePlayerRoot = null;
        _playerCameraTransform = null;
        _cameraIsChildOfPlayer = false;
    }

    private void SaveAndApplyCursorForDialogue(bool entering)
    {
        if (entering)
        {
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
        }
    }

    private void CollectAndDisableLookControls(bool entering)
    {
        if (!entering)
        {
            for (int i = 0; i < _behavioursDisabledForDialogue.Count; i++)
            {
                Behaviour b = _behavioursDisabledForDialogue[i];
                if (b != null)
                    b.enabled = _behavioursPrevEnabled[i];
            }

            _behavioursDisabledForDialogue.Clear();
            _behavioursPrevEnabled.Clear();
            return;
        }

        void TryAdd(Behaviour b)
        {
            if (b == null) return;
            if (_behavioursDisabledForDialogue.Contains(b)) return;
            _behavioursDisabledForDialogue.Add(b);
            _behavioursPrevEnabled.Add(b.enabled);
            b.enabled = false;
        }

        if (_dialoguePlayerRoot != null)
        {
            foreach (Behaviour b in _dialoguePlayerRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (b is PlayerController)
                    continue;
                string typeName = b.GetType().Name;
                if (typeName == "FirstPersonCameraLook" || typeName == "SimpleCameraController")
                    TryAdd(b);
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                string typeName = b.GetType().Name;
                if (typeName == "FirstPersonCameraLook" || typeName == "SimpleCameraController")
                    TryAdd(b);
            }
        }

        if (extraInputConsumersToDisable != null)
        {
            foreach (MonoBehaviour extra in extraInputConsumersToDisable)
                TryAdd(extra);
        }
    }
}
