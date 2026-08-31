using UnityEngine;

/// <summary>ESC 打开手机 App（联系 / 动态 / 设置）；面对面或剧情中不响应。</summary>
[DisallowMultipleComponent]
public class PhoneAppController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private bool blockWhileFaceDialogue = true;
    [SerializeField] private bool blockWhileStoryPanel = true;

    private PlayerController _player;

    private void Start()
    {
        _player = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey))
            return;

        PhoneAppAccess.EnsureLoaded();
        if (!PhoneAppAccess.IsUnlocked)
            return;

        if (blockWhileFaceDialogue && IsInFaceDialogue())
            return;

        if (blockWhileStoryPanel && IsStoryPanelOpen())
            return;

        if (SceneTravelPanel.IsOpen)
            return;

        PhoneAppPanel.Toggle();
    }

    private static bool IsInFaceDialogue()
    {
        RemiInteraction ri = FindObjectOfType<RemiInteraction>();
        return ri != null && ri.IsInDialogue;
    }

    private static bool IsStoryPanelOpen()
    {
        StoryPanel sp = UiManager.Instance.GetPanel<StoryPanel>();
        return sp != null && sp.gameObject.activeInHierarchy;
    }
}
