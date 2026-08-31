using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 自习：<c>LibaryDefaultPos</c> → <c>InStudy(R)</c> → … → <c>(4)</c> 步行前进（Walking→Wander / 停步 Idle2）；
/// 到达终点回调告别。自由聊在默认落点就地完成，开始自习后从下一站走起（不瞬移）。
/// </summary>
[DisallowMultipleComponent]
public class RemiLibraryStudyPatrol : MonoBehaviour
{
    public static RemiLibraryStudyPatrol Instance { get; private set; }

    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int StartHash = Animator.StringToHash("Start");

    public static void EnsureOn(MonoBehaviour host)
    {
        if (Instance != null || host == null)
            return;

        RemiLibraryStudyPatrol existing = host.GetComponent<RemiLibraryStudyPatrol>();
        if (existing == null)
            existing = host.gameObject.AddComponent<RemiLibraryStudyPatrol>();
    }

    [Header("目标")]
    [SerializeField] private Transform remiRoot;
    [SerializeField] private Animator remiAnimator;
    [SerializeField] private Remi remiBody;
    [SerializeField] private RemiInteraction remiInteraction;

    [Header("路点名（顺序：馆内默认位 → InStudy 系列）")]
    [SerializeField] private string[] waypointNames =
    {
        SceneTravelCatalog.LibraryStudyWaypointStart,
        SceneTravelCatalog.LibraryStudyWaypoint0,
        SceneTravelCatalog.LibraryStudyWaypoint1,
        SceneTravelCatalog.LibraryStudyWaypoint2,
        SceneTravelCatalog.LibraryStudyWaypoint3,
        SceneTravelCatalog.LibraryStudyWaypoint4,
    };

    [Header("移动")]
    [SerializeField] private float moveSpeed = 1.05f;
    [SerializeField] private float arriveDistance = 0.22f;
    [SerializeField] private float pauseAtPointSeconds = 1.5f;
    [Tooltip("起点（开走前）与中间检查点停顿；终点用 pauseAtPointSeconds。")]
    [SerializeField] private float pauseAtMidPointSeconds = 5f;
    [Tooltip("起点与中间检查点用 pauseAtMidPointSeconds；仅终点用较短停顿。")]
    [SerializeField] private bool longerPauseOnMiddlePoints = true;
    [SerializeField] private float turnSpeed = 10f;

    private bool _active;
    private bool _walkingVisual;
    private int _targetIndex;
    private float _pauseUntil;
    private Transform[] _waypoints;
    private Action _onPathCompleted;
    private bool _cachedApplyRootMotion;
    private bool _hasCachedApplyRootMotion;
    private bool _completionFired;
    private bool _awaitingFinalPause;

    public bool IsStudyPatrolActive => _active;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiLibraryStudyPatrol] 多实例，保留先激活的。", this);
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
            if (remiBody == null)
                remiBody = remiRoot.GetComponent<Remi>() ?? remiRoot.GetComponentInChildren<Remi>(true);
            if (remiAnimator == null)
                remiAnimator = remiRoot.GetComponentInChildren<Animator>(true);
            if (remiInteraction == null)
                remiInteraction = remiRoot.GetComponentInChildren<RemiInteraction>(true);
        }
    }

    /// <summary>解析并缓存路点；失败返回 false。</summary>
    public bool TryResolveWaypoints()
    {
        Scene scene = SceneManager.GetActiveScene();
        string[] names = waypointNames;
        if (names == null || names.Length == 0)
            return false;

        var found = new Transform[names.Length];
        int ok = 0;
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            Transform tf = ResolveMarker(name, scene);
            // 起点：兼容 Libary / Library 两种拼写
            if (tf == null && i == 0)
            {
                tf = ResolveMarker(SceneTravelCatalog.LibraryDefaultRemiMarkerName, scene);
                if (tf == null)
                    tf = ResolveMarker(SceneTravelCatalog.LibraryDefaultRemiMarkerNameAlt, scene);
            }

            found[i] = tf;
            if (tf != null)
                ok++;
            else if (!string.IsNullOrWhiteSpace(name))
                Debug.LogWarning($"[RemiLibraryStudyPatrol] 未找到路点「{name}」。", this);
        }

        if (ok < 2)
        {
            _waypoints = null;
            return false;
        }

        _waypoints = found;
        return true;
    }

    private static Transform ResolveMarker(string markerName, Scene scene)
    {
        if (string.IsNullOrWhiteSpace(markerName))
            return null;

        string name = markerName.Trim();
        Transform tf = SceneTravelService.TryFindSceneMarker(name, scene);
        if (tf != null)
            return tf;

        GameObject go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }

    /// <summary>
    /// 开始自习：已在起点（馆默认位）就地出发，步行前往下一站直至终点；不瞬移。
    /// </summary>
    public void BeginStudy(Action onCompleted)
    {
        ResolveRefs();
        _onPathCompleted = onCompleted;
        _completionFired = false;

        if (remiRoot == null)
        {
            Debug.LogWarning("[RemiLibraryStudyPatrol] 未找到 Remi。", this);
            FireCompleted();
            return;
        }

        if (!TryResolveWaypoints())
        {
            Debug.LogWarning("[RemiLibraryStudyPatrol] 路点不足，跳过自习走动。", this);
            FireCompleted();
            return;
        }

        // [0]=LibraryDefaultPos（闲聊位）；自习从 [1]=InStudy(R) 走起
        _targetIndex = FirstIndexAfter(0);
        if (_targetIndex < 0)
        {
            FireCompleted();
            return;
        }

        // 起点：转回书架后，用 mid 停顿再开走
        _pauseUntil = Time.time + ResolvePauseSeconds(0);
        _active = true;
        _awaitingFinalPause = false;
        if (remiAnimator != null && !_hasCachedApplyRootMotion)
        {
            _cachedApplyRootMotion = remiAnimator.applyRootMotion;
            _hasCachedApplyRootMotion = true;
            remiAnimator.applyRootMotion = false;
        }

        // 初始停步：Idle2；开走时 SetWalkingVisual 会清 Start 切 Wander
        if (remiBody != null)
            remiBody.SetDialogueBodyIdle(true);
        SetWalkingVisual(false);
    }

    public void EndStudy()
    {
        _active = false;
        _onPathCompleted = null;
        SetWalkingVisual(false);
        if (remiBody != null)
            remiBody.SetDialogueBodyIdle(false);
        if (remiAnimator != null && _hasCachedApplyRootMotion)
        {
            remiAnimator.applyRootMotion = _cachedApplyRootMotion;
            _hasCachedApplyRootMotion = false;
        }
    }

    /// <summary>点问等打断：暂停位移。</summary>
    public void SetPaused(bool paused)
    {
        if (paused)
            SetWalkingVisual(false);
    }

    private void Update()
    {
        if (!_active || remiRoot == null || _waypoints == null)
            return;

        if (ShouldFreeze())
        {
            SetWalkingVisual(false);
            return;
        }

        if (_awaitingFinalPause)
        {
            SetWalkingVisual(false);
            if (Time.time >= _pauseUntil)
            {
                _awaitingFinalPause = false;
                _active = false;
                FireCompleted();
            }

            return;
        }

        if (_targetIndex < 0 || _targetIndex >= _waypoints.Length)
            return;

        Transform destTf = _waypoints[_targetIndex];
        if (destTf == null)
        {
            int next = FirstIndexAfter(_targetIndex);
            if (next < 0)
            {
                _active = false;
                FireCompleted();
            }
            else
                _targetIndex = next;
            return;
        }

        if (Time.time < _pauseUntil)
        {
            SetWalkingVisual(false);
            return;
        }

        Vector3 pos = remiRoot.position;
        Vector3 dest = destTf.position;
        dest.y = pos.y;

        Vector3 delta = dest - pos;
        delta.y = 0f;
        float distSq = delta.sqrMagnitude;
        float arrive = arriveDistance * arriveDistance;

        if (distSq <= arrive)
        {
            SetWalkingVisual(false);
            remiRoot.position = new Vector3(dest.x, pos.y, dest.z);
            float pause = ResolvePauseSeconds(_targetIndex);
            _pauseUntil = Time.time + pause;

            int last = LastValidIndex();
            if (_targetIndex >= last)
            {
                _awaitingFinalPause = true;
                return;
            }

            int next = FirstIndexAfter(_targetIndex);
            if (next < 0)
            {
                _awaitingFinalPause = true;
                return;
            }

            _targetIndex = next;
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

    private float ResolvePauseSeconds(int arrivedIndex)
    {
        if (!longerPauseOnMiddlePoints)
            return Mathf.Max(0f, pauseAtPointSeconds);

        int last = LastValidIndex();
        // 起点 + 中间检查点：长停；仅终点短停
        if (arrivedIndex >= 0 && arrivedIndex < last)
            return Mathf.Max(0f, pauseAtMidPointSeconds);

        return Mathf.Max(0f, pauseAtPointSeconds);
    }

    private void FireCompleted()
    {
        if (_completionFired)
            return;
        _completionFired = true;
        Action cb = _onPathCompleted;
        _onPathCompleted = null;
        cb?.Invoke();
    }

    private int FirstIndexAfter(int index)
    {
        if (_waypoints == null)
            return -1;
        for (int i = index + 1; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] != null)
                return i;
        }

        return -1;
    }

    private int LastValidIndex()
    {
        if (_waypoints == null)
            return -1;
        for (int i = _waypoints.Length - 1; i >= 0; i--)
        {
            if (_waypoints[i] != null)
                return i;
        }

        return -1;
    }

    private bool ShouldFreeze()
    {
        if (remiInteraction != null && remiInteraction.IsInDialogue)
            return true;

        RemiLibraryDay2CoPresenceFlow flow = RemiLibraryDay2CoPresenceFlow.Instance;
        if (flow != null &&
            (flow.IsSequenceRunning || flow.IsInFarewell || flow.IsStudyWhisperActive))
            return true;

        return false;
    }

    private void SetWalkingVisual(bool walking)
    {
        if (_walkingVisual == walking)
            return;

        if (walking && remiInteraction != null && remiInteraction.IsInDialogue)
            return;

        _walkingVisual = walking;

        // Controller：Idle2→Wander 要 Walking && !Start；Wander→Idle2 看 Start。
        // 开走必须清 Start，否则会保持 Idle2 却脚本平移。
        if (remiAnimator != null)
        {
            if (walking)
            {
                if (remiBody != null)
                    remiBody.SetDialogueBodyIdle(false);
                else
                    remiAnimator.SetBool(StartHash, false);
                remiAnimator.SetBool(WalkingHash, true);
            }
            else
            {
                remiAnimator.SetBool(WalkingHash, false);
                if (remiBody != null)
                    remiBody.SetDialogueBodyIdle(true);
                else
                    remiAnimator.SetBool(StartHash, true);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_waypoints == null)
            return;
        Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.9f);
        Vector3? prev = null;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null)
                continue;
            Vector3 p = _waypoints[i].position + Vector3.up * 0.05f;
            Gizmos.DrawWireSphere(p, 0.14f);
            if (prev.HasValue)
                Gizmos.DrawLine(prev.Value, p);
            prev = p;
        }
    }
#endif
}
