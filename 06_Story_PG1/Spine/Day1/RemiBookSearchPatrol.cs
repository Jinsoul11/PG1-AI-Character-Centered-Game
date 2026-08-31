using UnityEngine;

/// <summary>
/// Day1 窗口期：Remi 在教室路点间来回走动（脚本位移 + Animator Bool <c>Walking</c> → Body/Wander）。
/// 对话或托付演出中自动停步。由 <see cref="RemiBookQuestFlow"/> 启停。
/// </summary>
[DisallowMultipleComponent]
public class RemiBookSearchPatrol : MonoBehaviour
{
    public static RemiBookSearchPatrol Instance { get; private set; }

    private static readonly int WalkingHash = Animator.StringToHash("Walking");

    /// <summary>挂在流程物体上（若场景未放组件则运行时补一个）。</summary>
    public static void EnsureOn(MonoBehaviour host)
    {
        if (Instance != null || host == null)
            return;
        RemiBookSearchPatrol existing = host.GetComponent<RemiBookSearchPatrol>();
        if (existing == null)
            existing = host.gameObject.AddComponent<RemiBookSearchPatrol>();
    }

    [Header("目标")]
    [SerializeField] private Transform remiRoot;
    [SerializeField] private Animator remiAnimator;
    [SerializeField] private RemiInteraction remiInteraction;

    [Header("路点（世界坐标空物体；为空则用下方偏移自动生成）")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Vector3[] fallbackLocalOffsets =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(1.6f, 0f, 0.8f),
        new Vector3(-0.4f, 0f, 1.8f),
        new Vector3(-1.4f, 0f, 0.3f),
        new Vector3(0.8f, 0f, -1.0f),
    };

    [Header("移动")]
    [SerializeField] private float moveSpeed = 1.15f;
    [SerializeField] private float arriveDistance = 0.18f;
    [SerializeField] private float pauseAtPointSeconds = 0.85f;
    [SerializeField] private float turnSpeed = 10f;

    private bool _windowActive;
    private bool _walkingVisual;
    private int _index;
    private float _pauseUntil;
    private Vector3[] _runtimePoints;
    private Vector3 _anchorPosition;
    private bool _cachedApplyRootMotion;
    private bool _hasCachedApplyRootMotion;

    public bool IsWindowPatrolActive => _windowActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiBookSearchPatrol] 场景中存在多个实例，保留先激活的。", this);
            enabled = false;
            return;
        }

        Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveRefs()
    {
        if (remiRoot == null)
        {
            Remi remi = FindObjectOfType<Remi>();
            if (remi != null)
                remiRoot = remi.transform;
        }

        if (remiRoot != null)
        {
            if (remiAnimator == null)
                remiAnimator = remiRoot.GetComponentInChildren<Animator>(true);
            if (remiInteraction == null)
                remiInteraction = remiRoot.GetComponentInChildren<RemiInteraction>(true);
        }
    }

    /// <summary>教室开场结束 / Day1 窗口开始：开始找书巡逻。</summary>
    public void BeginWindowPatrol()
    {
        ResolveRefs();
        if (remiRoot == null)
        {
            Debug.LogWarning("[RemiBookSearchPatrol] 未找到 Remi，无法巡逻。");
            return;
        }

        _anchorPosition = remiRoot.position;
        BuildRuntimePoints();
        _index = 0;
        _pauseUntil = 0f;
        _windowActive = true;
        if (remiAnimator != null && !_hasCachedApplyRootMotion)
        {
            _cachedApplyRootMotion = remiAnimator.applyRootMotion;
            _hasCachedApplyRootMotion = true;
            remiAnimator.applyRootMotion = false;
        }

        SetWalkingVisual(false);
    }

    /// <summary>委托打开或日切后：停止巡逻并清 Walking。</summary>
    public void EndWindowPatrol()
    {
        _windowActive = false;
        SetWalkingVisual(false);
        if (remiAnimator != null && _hasCachedApplyRootMotion)
        {
            remiAnimator.applyRootMotion = _cachedApplyRootMotion;
            _hasCachedApplyRootMotion = false;
        }
    }

    private void Update()
    {
        if (!_windowActive || remiRoot == null)
            return;

        if (ShouldFreeze())
        {
            SetWalkingVisual(false);
            return;
        }

        if (_runtimePoints == null || _runtimePoints.Length == 0)
            return;

        if (Time.time < _pauseUntil)
        {
            SetWalkingVisual(false);
            return;
        }

        Vector3 pos = remiRoot.position;
        Vector3 dest = _runtimePoints[_index];
        dest.y = pos.y;

        Vector3 delta = dest - pos;
        delta.y = 0f;
        float distSq = delta.sqrMagnitude;
        float arrive = arriveDistance * arriveDistance;

        if (distSq <= arrive)
        {
            SetWalkingVisual(false);
            _pauseUntil = Time.time + Mathf.Max(0f, pauseAtPointSeconds);
            _index = (_index + 1) % _runtimePoints.Length;
            return;
        }

        SetWalkingVisual(true);
        Vector3 step = delta.normalized * (moveSpeed * Time.deltaTime);
        if (step.sqrMagnitude > distSq)
            step = delta;
        remiRoot.position = pos + step;

        if (delta.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(delta.normalized, Vector3.up);
            remiRoot.rotation = Quaternion.Slerp(remiRoot.rotation, look, turnSpeed * Time.deltaTime);
        }
    }

    private bool ShouldFreeze()
    {
        if (remiInteraction != null && remiInteraction.IsInDialogue)
            return true;

        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow == null)
            return false;

        if (flow.IsSequenceRunning || flow.IsAwaitingGoodbyeConfirm)
            return true;

        if (flow.State != RemiBookQuestFlow.QuestState.WindowOpen)
            return true;

        return false;
    }

    private void BuildRuntimePoints()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            int count = 0;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                    count++;
            }

            if (count > 0)
            {
                _runtimePoints = new Vector3[count];
                int w = 0;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] == null)
                        continue;
                    Vector3 p = waypoints[i].position;
                    p.y = _anchorPosition.y;
                    _runtimePoints[w++] = p;
                }

                return;
            }
        }

        Vector3[] offsets = fallbackLocalOffsets;
        if (offsets == null || offsets.Length == 0)
        {
            _runtimePoints = new[] { _anchorPosition };
            return;
        }

        _runtimePoints = new Vector3[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 o = offsets[i];
            o.y = 0f;
            _runtimePoints[i] = _anchorPosition + o;
        }
    }

    private void SetWalkingVisual(bool walking)
    {
        if (_walkingVisual == walking)
            return;
        _walkingVisual = walking;
        if (remiAnimator != null)
            remiAnimator.SetBool(WalkingHash, walking);

        // 对话姿态开启时不要被巡逻重新拉回 Wander
        if (walking && remiInteraction != null && remiInteraction.IsInDialogue)
        {
            _walkingVisual = false;
            if (remiAnimator != null)
                remiAnimator.SetBool(WalkingHash, false);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_runtimePoints == null || _runtimePoints.Length == 0)
            return;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        for (int i = 0; i < _runtimePoints.Length; i++)
        {
            Gizmos.DrawWireSphere(_runtimePoints[i] + Vector3.up * 0.05f, 0.12f);
            Vector3 next = _runtimePoints[(i + 1) % _runtimePoints.Length];
            Gizmos.DrawLine(_runtimePoints[i] + Vector3.up * 0.05f, next + Vector3.up * 0.05f);
        }
    }
#endif
}
