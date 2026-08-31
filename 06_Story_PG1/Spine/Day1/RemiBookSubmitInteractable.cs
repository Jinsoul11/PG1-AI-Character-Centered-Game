using UnityEngine;

/// <summary>
/// 挂在 Remi 或其身前 Trigger 上：玩家已拿到书且任务处于「等交书」时，靠近按 E 调用 <see cref="RemiBookQuestFlow.TrySubmitBookFromWorld"/>。
/// 与书本上的 <see cref="RemiBookQuestInteractable"/> 共用 E，由流程状态区分检视/提交。
/// </summary>
[DisallowMultipleComponent]
public class RemiBookSubmitInteractable : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("为 true：除本物体 Trigger 外，只要玩家在 RemiInteraction 的交互距离内也可按 E 交书（与头顶提示范围一致）。")]
    [SerializeField] private bool alsoAllowWhenInRemiInteractionRange = true;
    [Tooltip("若在与 Remi 对话中不希望 E 误触交书，可绑定；留空则同物体/父物体上查找。")]
    [SerializeField] private RemiInteraction remiInteraction;

    private bool _playerInside;

    private void Awake()
    {
        if (remiInteraction == null)
        {
            remiInteraction = GetComponent<RemiInteraction>();
            if (remiInteraction == null)
                remiInteraction = GetComponentInParent<RemiInteraction>();
            if (remiInteraction == null)
                remiInteraction = FindObjectOfType<RemiInteraction>();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow == null || !flow.HasBookForSubmission()) return;

        if (remiInteraction != null && remiInteraction.IsInDialogue)
            return;

        if (!PlayerInSubmitZone())
            return;

        flow.TrySubmitBookFromWorld(remiInteraction);
    }

    private bool PlayerInSubmitZone()
    {
        if (_playerInside) return true;
        return alsoAllowWhenInRemiInteractionRange && remiInteraction != null && remiInteraction.IsPlayerInRange;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInside = false;
    }
}
