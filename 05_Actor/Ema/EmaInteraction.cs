using TMPro;
using UnityEngine;

/// <summary>
/// Ema 交互：进入范围显示提示，按 F 打开 <see cref="EmaConPanel"/>（或选项式 <see cref="EmaDialoguePanel"/>）。
/// 支持 Trigger、纯距离、或两者并用（与 <see cref="RemiInteraction"/> 一致）；若场景里为 Ema 挂了 <see cref="RemiRoleWorldUI"/>（Tip/Response），会自动驱动显隐。
/// </summary>
[DisallowMultipleComponent]
public class EmaInteraction : MonoBehaviour
{
    [Header("交互配置")]
    public float interactRange = 5f;
    public KeyCode interactKey = KeyCode.F;
    public string interactPromptText = "按F和Ema聊天";

    [Tooltip("为 true 时，只要在 interactRange 内即可交互（不依赖 Collider Trigger）。推荐开启，避免忘记做 Trigger。")]
    public bool useDistanceCheck = true;

    [Header("面板")]
    [Tooltip("开启：固定话题 + Continue 的 EmaConPanel；关闭：走 Ema + EmaDialoguePanel 节点对话。")]
    [SerializeField] private bool useStructuredConPanel = true;

    [Header("UI 引用")]
    [Tooltip("可选：Ema 头顶 RoleCanvas 上的控制器（与 Remi 同款 Tip/Response 结构即可复用本脚本）。")]
    [SerializeField] private RemiRoleWorldUI roleWorldUi;

    [Tooltip("未使用 RoleWorldUI 时，可直接拖一个世界空间 TMP 作为提示。")]
    public TMP_Text worldPromptText;

    public bool IsPlayerInRange => _isPlayerInRange;

    /// <summary>是否已按 F 打开对话面板（供 <see cref="StoryDirector"/> 等在剧情结束后刷新头顶 UI）。</summary>
    public bool IsInDialogue => _isTalking;

    private bool _isPlayerInRange;
    private bool _playerInsideTrigger;
    private bool _isTalking;

    private Transform _cachedPlayer;
    private Ema _ema;

    private void Start()
    {
        _ema = GetComponent<Ema>();
        if (_ema == null)
            _ema = GetComponentInParent<Ema>();

        if (roleWorldUi == null)
            roleWorldUi = GetComponentInChildren<RemiRoleWorldUI>(true);

        if (roleWorldUi != null)
        {
            roleWorldUi.SetTipPrompt(interactPromptText);
            roleWorldUi.ApplyState(false, false);
        }
        else if (worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        RefreshPlayerProximity();

        if (_isPlayerInRange && !_isTalking && Input.GetKeyDown(interactKey))
            StartDialogue();

        if (_isTalking && !_isPlayerInRange)
            EndDialogue();

        if (_isTalking && Input.GetKeyDown(KeyCode.Escape))
            EndDialogue();
    }

    private void RefreshPlayerProximity()
    {
        if (_cachedPlayer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            _cachedPlayer = p != null ? p.transform : null;
        }

        bool byDist = false;
        if (useDistanceCheck && _cachedPlayer != null && interactRange > 0f)
        {
            float d = Vector3.Distance(transform.position, _cachedPlayer.position);
            byDist = d <= interactRange;
        }

        bool wasInRange = _isPlayerInRange;
        _isPlayerInRange = _playerInsideTrigger || byDist;

        if (_isPlayerInRange == wasInRange || _isTalking)
            return;

        if (roleWorldUi != null)
        {
            roleWorldUi.SetTipPrompt(interactPromptText);
            roleWorldUi.ApplyState(_isPlayerInRange, _isTalking);
        }
        else if (worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(_isPlayerInRange);
            if (_isPlayerInRange)
                worldPromptText.text = interactPromptText;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInsideTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInsideTrigger = false;
    }

    public void StartDialogue()
    {
        _isTalking = true;

        Cursor.lockState = CursorLockMode.None;
        UiManager.Instance.canvasObj.SetActive(true);

        if (useStructuredConPanel)
            UiManager.Instance.ShowPanel<EmaConPanel>();
        else
        {
            if (_ema == null)
            {
                Debug.LogWarning("[EmaInteraction] useStructuredConPanel 关闭时需要 Ema 组件以绑定 EmaDialoguePanel。", this);
                _isTalking = false;
                return;
            }

            var panel = UiManager.Instance.ShowPanel<EmaDialoguePanel>();
            panel.Bind(_ema);
        }

        if (roleWorldUi != null)
            roleWorldUi.ApplyState(_isPlayerInRange, true);
        else if (worldPromptText != null)
            worldPromptText.gameObject.SetActive(false);

        var playerController = FindObjectOfType<PlayerController>();
        playerController?.SetMoveLock(true);

        var anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInParent<Animator>();
        anim?.SetBool("Talk", true);
    }

    public void EndDialogue()
    {
        _isTalking = false;

        if (useStructuredConPanel)
        {
            UiManager.Instance.HidePanel<EmaConPanel>();
            if (UiManager.Instance.canvasObj != null)
                UiManager.Instance.canvasObj.SetActive(false);
        }
        else
            UiManager.Instance.HidePanel<EmaDialoguePanel>();

        if (roleWorldUi != null)
        {
            roleWorldUi.SetTipPrompt(interactPromptText);
            roleWorldUi.ApplyState(_isPlayerInRange, false);
        }
        else if (_isPlayerInRange && worldPromptText != null)
        {
            worldPromptText.gameObject.SetActive(true);
            worldPromptText.text = interactPromptText;
        }

        var playerController = FindObjectOfType<PlayerController>();
        playerController?.SetMoveLock(false);

        var anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInParent<Animator>();
        anim?.SetBool("Talk", false);
    }
}
