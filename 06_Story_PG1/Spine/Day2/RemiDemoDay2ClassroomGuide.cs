using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 教室段：读档/重进教室时补齐世界状态，并在邀请送达后提示玩家去门口选图书馆。
/// 进馆不依赖玩家是否回复邀约；若回复则最多一条且 Remi 不回信。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoDay2ClassroomGuide : MonoBehaviour
{
    private const string PrefsGoToDoorHintShown = "RemiDay2_GoToDoorHintShown";

    [Header("文案")]
    [SerializeField] private float goToDoorHintDisplaySeconds = 7f;
    [SerializeField] private float goToDoorFallbackDelaySeconds = 8f;

    private Coroutine _goToDoorFallbackRoutine;

    private static RemiDemoDay2ClassroomGuide _instance;

    public static RemiDemoDay2ClassroomGuide Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
            _instance = null;
    }

    private void Start() => TryBootstrapClassroom(SceneManager.GetActiveScene());

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBootstrapClassroom(scene);

    /// <summary>手机关闭后：若仍在等玩家去图书馆，补一条门口提示（每存档一次）。</summary>
    public static void TryPromptGoToDoorAfterPhone()
    {
        if (!ShouldPromptGoToDoor())
            return;

        ResolveGuide()?.ShowGoToDoorHintOnce();
    }

    /// <summary>邀约送达后：关手机时提示去门口；一直不看手机则延迟兜底。</summary>
    public static void NotifyDay2InviteDelivered()
    {
        RemiDemoDay2ClassroomGuide guide = ResolveGuide();
        guide?.ScheduleGoToDoorFallback();
    }

    public void ScheduleGoToDoorFallback()
    {
        if (_goToDoorFallbackRoutine != null)
            StopCoroutine(_goToDoorFallbackRoutine);
        _goToDoorFallbackRoutine = StartCoroutine(CoGoToDoorFallback());
    }

    private IEnumerator CoGoToDoorFallback()
    {
        if (goToDoorFallbackDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(goToDoorFallbackDelaySeconds);

        ShowGoToDoorHintOnce();
        _goToDoorFallbackRoutine = null;
    }

    private static RemiDemoDay2ClassroomGuide ResolveGuide()
    {
        if (_instance != null)
            return _instance;

#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<RemiDemoDay2ClassroomGuide>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<RemiDemoDay2ClassroomGuide>();
#endif
    }

    public static bool ShouldPromptGoToDoor()
    {
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director == null || !director.IsAwaitingDay2LibraryVisit())
            return false;

        if (!IsClassroomScene(SceneManager.GetActiveScene()))
            return false;

        return PlayerPrefs.GetInt(PrefsGoToDoorHintShown, 0) == 0;
    }

    public void ShowGoToDoorHintOnce()
    {
        if (!ShouldPromptGoToDoor())
            return;

        PlayerPrefs.SetInt(PrefsGoToDoorHintShown, 1);
        StoryNarrativeHintView.TryPlayDay2GoToLibraryDoor(displaySeconds: goToDoorHintDisplaySeconds);
    }

    private void TryBootstrapClassroom(Scene scene)
    {
        if (!IsClassroomScene(scene))
            return;

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director == null || !director.IsDay2ClassroomPhase())
            return;

        EnsureDay2WorldState();
        RemiWorldPlacement.EnsureDay2AbsentInClassroom();

        if (director.CurrentBeat == RemiDemoSpineBeat.Day1Complete)
            director.RestorePendingDay2InviteIfNeeded();

        director.TryFlushPendingDay2Invite();

        if (director.IsAwaitingDay2LibraryVisit())
            ShowGoToDoorHintOnce();
    }

    private static void EnsureDay2WorldState()
    {
        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence == null)
            return;

        if (presence.WorldTime.storyDay < 2)
            presence.AdvanceWorldTime(RemiTimeAdvanceReason.NextDay);

        if (presence.WorldTime.phase != RemiDayPhase.Afternoon)
            presence.SetDayPhase(RemiDayPhase.Afternoon);
    }

    private static bool IsClassroomScene(Scene scene) =>
        string.Equals(scene.name, "Classroom", System.StringComparison.OrdinalIgnoreCase);

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Go-To-Door Hint Flag")]
    private void Editor_ClearGoToDoorHintFlag() =>
        PlayerPrefs.DeleteKey(PrefsGoToDoorHintShown);
#endif

    public static void ResetProgressFlags()
    {
        PlayerPrefs.DeleteKey(PrefsGoToDoorHintShown);
    }
}
