using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>手机 App · 设置页（与联系/动态并列）。</summary>
public class PhoneSettingsView : MonoBehaviour
{
    [SerializeField] private RectTransform pageRoot;
    [SerializeField] private TMP_Text presenceStatusLine;
    [SerializeField] private TMP_Text hintLine;

    /// <summary>手动 UI：绑定 Page Root 与文案；不再自动建设置页。</summary>
    public void EnsureBuilt(RectTransform parent)
    {
        if (pageRoot != null)
            return;
        Debug.LogError("[PhoneSettingsView] 请绑定 Page Root（已取消自动生成设置页）。", this);
    }

    public void SetVisible(bool visible)
    {
        if (pageRoot != null)
            pageRoot.gameObject.SetActive(visible);
        if (visible)
            Refresh();
    }

    public void Refresh()
    {
        if (presenceStatusLine == null) return;
        RemiPresenceService p = RemiPresenceService.Instance;
        if (p == null)
        {
            presenceStatusLine.text = "Remi · 状态未知";
            return;
        }

        string phase = RemiWorldTimeFormat.PhaseShortName(p.CurrentPhase);
        string loc = LocationName(p.CurrentLocation);
        string act = ActivityName(p.CurrentActivity);
        string episode = EpisodeName(p.CurrentEpisodeKind);
        string track = p.TrackAlignment == RemiTrackAlignment.OnTrack ? "在轨" : "偏离";
        string overrideNote = p.IsScheduleOverridden
            ? $"\n（临时：{LocationName(p.OverrideLocation)} · {ActivityName(p.OverrideActivity)}）"
            : string.Empty;
        string occupyNote = p.EpisodeOccupiesPhase ? " · 占满本时段" : string.Empty;

        presenceStatusLine.text =
            $"Remi 当前 · 第{p.WorldTime.storyDay}天 {phase}\n{loc} · {act} · {track} · {episode}{occupyNote}{overrideNote}";
    }

    private static string LocationName(RemiLocation loc) =>
        loc switch
        {
            RemiLocation.Classroom => "教室",
            RemiLocation.Library => "图书馆",
            RemiLocation.Dorm => "宿舍",
            _ => loc.ToString(),
        };

    private static string ActivityName(RemiActivity act) =>
        act switch
        {
            RemiActivity.InClass => "上课",
            RemiActivity.Free => "空闲",
            RemiActivity.AtDorm => "在宿舍",
            RemiActivity.Cooking => "做饭",
            RemiActivity.Busy => "忙碌",
            RemiActivity.Sleeping => "休息",
            _ => act.ToString(),
        };

    private static string EpisodeName(RemiPhaseEpisodeKind kind) =>
        kind switch
        {
            RemiPhaseEpisodeKind.Commission => "委托",
            RemiPhaseEpisodeKind.CoPresence => "共现",
            RemiPhaseEpisodeKind.BeatInterlude => "插曲",
            RemiPhaseEpisodeKind.DeviationSession => "偏离会话",
            _ => "日常",
        };
}
