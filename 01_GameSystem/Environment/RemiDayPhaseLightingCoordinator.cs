using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景全局协调：订阅 <see cref="RemiPresenceService.DayPhaseChanged"/>，
/// 在时段变化或场景加载后对当前场景的 <see cref="RemiSceneDayPhaseLighting"/> 应用对应预设。
/// </summary>
[DisallowMultipleComponent]
public class RemiDayPhaseLightingCoordinator : MonoBehaviour
{
    public static RemiDayPhaseLightingCoordinator Instance { get; private set; }

    private RemiSceneDayPhaseLighting _activeProfile;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(RemiDayPhaseLightingCoordinator));
        go.AddComponent<RemiDayPhaseLightingCoordinator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnEnable()
    {
        SubscribePresence();
    }

    private void Start()
    {
        SubscribePresence();
        ApplyCurrentPhase();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribePresence();
        if (Instance == this)
            Instance = null;
    }

    private void SubscribePresence()
    {
        if (RemiPresenceService.Instance == null)
            return;
        RemiPresenceService.Instance.DayPhaseChanged -= OnDayPhaseChanged;
        RemiPresenceService.Instance.DayPhaseChanged += OnDayPhaseChanged;
    }

    private void UnsubscribePresence()
    {
        if (RemiPresenceService.Instance == null)
            return;
        RemiPresenceService.Instance.DayPhaseChanged -= OnDayPhaseChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _activeProfile = null;
        ApplyCurrentPhase();
    }

    private void OnDayPhaseChanged(RemiDayPhase phase)
    {
        if (_activeProfile == null)
            _activeProfile = FindActiveProfile();
        _activeProfile?.ApplyPhase(phase);
    }

    public static void Register(RemiSceneDayPhaseLighting profile)
    {
        if (profile == null)
            return;
        EnsureExists();
        Instance._activeProfile = profile;
        Instance.ApplyCurrentPhase();
    }

    public static void Unregister(RemiSceneDayPhaseLighting profile)
    {
        if (Instance == null || profile == null)
            return;
        if (Instance._activeProfile == profile)
            Instance._activeProfile = null;
    }

    private void ApplyCurrentPhase()
    {
        if (_activeProfile == null)
            _activeProfile = FindActiveProfile();

        RemiDayPhase phase = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.phase
            : RemiDayPhase.Morning;

        _activeProfile?.ApplyPhase(phase);
    }

    private static RemiSceneDayPhaseLighting FindActiveProfile()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<RemiSceneDayPhaseLighting>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<RemiSceneDayPhaseLighting>();
#endif
    }
}
