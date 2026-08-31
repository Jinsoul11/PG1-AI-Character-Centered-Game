using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在场景中的书本上：等交书任务中且尚未拿到书时，靠近按 E 打开 <see cref="CheckPanel"/> 检视，确认后获得书并隐藏书本；
/// 拿到书后本物体上的逻辑不再响应（物体可整体 SetActive(false)）。
/// 交书请在 Remi 处使用 <see cref="RemiBookSubmitInteractable"/> 按 E。
/// </summary>
[DisallowMultipleComponent]
public class RemiBookQuestInteractable : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("除 Trigger 外，在 interactRange 内也可按 E（书架等 Trigger 过小时建议开启）。")]
    [SerializeField] private bool useDistanceCheck = true;

    [Min(0.1f)]
    [SerializeField] private float interactRange = 2.5f;

    [TextArea(2, 5)]
    [SerializeField] private string inspectDescription = "《AI游戏入门》，封面有点旧。要把它带走吗？";

    [Tooltip("确认检视后隐藏的对象；留空则隐藏本物体所在根（通常为书本根节点）。")]
    [SerializeField] private GameObject hideRootWhenPicked;

    private bool _playerInsideTrigger;
    private bool _inspectPanelSequenceRunning;
    private Transform _cachedPlayer;

    private void Start()
    {
        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow != null && flow.ShouldHideBookObjectInScene())
            ApplyHideBookVisual();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;
        if (_inspectPanelSequenceRunning) return;

        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow == null || !flow.AwaitsBookPickup()) return;
        if (!IsPlayerInInteractRange()) return;

        StartCoroutine(CoOpenInspectThenPickUp(flow));
    }

    private bool IsPlayerInInteractRange()
    {
        if (_playerInsideTrigger)
            return true;

        if (!useDistanceCheck || interactRange <= 0f)
            return false;

        Transform player = ResolvePlayerTransform();
        if (player == null)
            return false;

        return Vector3.Distance(transform.position, player.position) <= interactRange;
    }

    private Transform ResolvePlayerTransform()
    {
        if (_cachedPlayer != null)
            return _cachedPlayer;

        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged != null)
        {
            _cachedPlayer = tagged.transform;
            return _cachedPlayer;
        }

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
            _cachedPlayer = pc.transform;

        return _cachedPlayer;
    }

    private IEnumerator CoOpenInspectThenPickUp(RemiBookQuestFlow flow)
    {
        _inspectPanelSequenceRunning = true;
        UiManager.EnsureCanvasActive();
        CheckPanel panel = UiManager.Instance.ShowPanel<CheckPanel>();
        yield return null;
        if (panel != null)
        {
            panel.ConfigureForInspect(inspectDescription, () =>
            {
                OnInspectConfirmed(flow);
                _inspectPanelSequenceRunning = false;
            });
        }
        else
            _inspectPanelSequenceRunning = false;
    }

    private void OnInspectConfirmed(RemiBookQuestFlow flow)
    {
        flow.NotifyBookPickedUpFromInspect();
        ApplyHideBookVisual();
    }

    private void ApplyHideBookVisual()
    {
        GameObject root = hideRootWhenPicked != null ? hideRootWhenPicked : gameObject;
        if (root != null)
            root.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
            _playerInsideTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
            _playerInsideTrigger = false;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag(playerTag)) return true;
        if (other.GetComponent<CharacterController>() != null && other.CompareTag(playerTag)) return true;

        Transform root = other.transform.root;
        if (root != null && root.CompareTag(playerTag))
            return true;

        return other.GetComponentInParent<PlayerController>() != null;
    }
}
