using UnityEngine;

/// <summary>
/// Demo 后台 RunTelemetry：抽象互动指标，DemoFinale 落盘；无 UI、不含聊天原文。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoRunTelemetry : MonoBehaviour
{
    public static RemiDemoRunTelemetry Instance { get; private set; }

    public const string PrefsSnapshotKey = "RemiDemoRunTelemetry";

    [SerializeField] private bool persist = true;

    private int _playerFaceMessages;
    private int _playerSocialMessages;
    private int _sharedExperiencesRecorded;
    private int _nightPhasePlayerMessages;
    private bool _finalized;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiDemoRunTelemetry));
        go.AddComponent<RemiDemoRunTelemetry>();
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
    }

    public void RecordPlayerMessage(RemiInteractionChannel channel)
    {
        if (_finalized)
            return;

        if (channel == RemiInteractionChannel.Social)
            _playerSocialMessages++;
        else
            _playerFaceMessages++;

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null && presence.StoryDayStarted)
        {
            RemiWorldTime time = presence.WorldTime;
            if (time.phase == RemiDayPhase.Night)
                _nightPhasePlayerMessages++;
        }
    }

    public void RecordSharedExperienceRecorded()
    {
        if (_finalized)
            return;
        _sharedExperiencesRecorded++;
    }

    public RemiDemoRunTelemetrySnapshot BuildLivePreview()
    {
        int total = _playerFaceMessages + _playerSocialMessages;
        RemiDemoSpineBeat beat = RemiDemoSpineDirector.Instance != null
            ? RemiDemoSpineDirector.Instance.CurrentBeat
            : RemiDemoSpineBeat.NotStarted;

        RemiPresenceService presence = RemiPresenceService.Instance;
        return new RemiDemoRunTelemetrySnapshot
        {
            playerFaceMessages = _playerFaceMessages,
            playerSocialMessages = _playerSocialMessages,
            sharedExperiencesRecorded = _sharedExperiencesRecorded,
            nightPhasePlayerMessages = _nightPhasePlayerMessages,
            totalPlayerMessages = total,
            nightMessageRatio = total > 0 ? (float)_nightPhasePlayerMessages / total : 0f,
            finalSpineBeat = (int)beat,
            finalDepthStage = presence != null ? (int)presence.DialogueDepthStage : 0,
            finalDelegationMilestones = presence != null ? presence.DelegationMilestoneCountForGate : 0,
            finalizedUnixMs = 0,
        };
    }

    public RemiDemoRunTelemetrySnapshot FinalizeAndSave()
    {
        if (_finalized)
            return LoadSaved();

        _finalized = true;

        int total = _playerFaceMessages + _playerSocialMessages;
        RemiDemoSpineBeat beat = RemiDemoSpineDirector.Instance != null
            ? RemiDemoSpineDirector.Instance.CurrentBeat
            : RemiDemoSpineBeat.DemoFinale;

        RemiPresenceService presence = RemiPresenceService.Instance;
        var snapshot = new RemiDemoRunTelemetrySnapshot
        {
            playerFaceMessages = _playerFaceMessages,
            playerSocialMessages = _playerSocialMessages,
            sharedExperiencesRecorded = _sharedExperiencesRecorded,
            nightPhasePlayerMessages = _nightPhasePlayerMessages,
            totalPlayerMessages = total,
            nightMessageRatio = total > 0 ? (float)_nightPhasePlayerMessages / total : 0f,
            finalSpineBeat = (int)beat,
            finalDepthStage = presence != null ? (int)presence.DialogueDepthStage : 0,
            finalDelegationMilestones = presence != null ? presence.DelegationMilestoneCountForGate : 0,
            finalizedUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        if (persist)
        {
            PlayerPrefs.SetString(PrefsSnapshotKey, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        Debug.Log(
            $"[RemiDemoRunTelemetry] Finalized: face={snapshot.playerFaceMessages}, social={snapshot.playerSocialMessages}, " +
            $"experiences={snapshot.sharedExperiencesRecorded}, nightRatio={snapshot.nightMessageRatio:0.##}");
        return snapshot;
    }

    public static RemiDemoRunTelemetrySnapshot LoadSaved()
    {
        if (!PlayerPrefs.HasKey(PrefsSnapshotKey))
            return null;

        try
        {
            return JsonUtility.FromJson<RemiDemoRunTelemetrySnapshot>(PlayerPrefs.GetString(PrefsSnapshotKey, ""));
        }
        catch
        {
            return null;
        }
    }

    public void ClearSession()
    {
        _playerFaceMessages = 0;
        _playerSocialMessages = 0;
        _sharedExperiencesRecorded = 0;
        _nightPhasePlayerMessages = 0;
        _finalized = false;

        if (persist && PlayerPrefs.HasKey(PrefsSnapshotKey))
        {
            PlayerPrefs.DeleteKey(PrefsSnapshotKey);
            PlayerPrefs.Save();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Log current session counters")]
    private void Editor_LogCounters()
    {
        Debug.Log(
            $"face={_playerFaceMessages}, social={_playerSocialMessages}, " +
            $"experiences={_sharedExperiencesRecorded}, night={_nightPhasePlayerMessages}, finalized={_finalized}");
    }
#endif
}
