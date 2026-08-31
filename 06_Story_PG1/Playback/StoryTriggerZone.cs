using TMPro;
using UnityEngine;

/// <summary>
/// 进入区域触发开场剧情（一次性）。
/// 用法：
/// - 挂在门口地板/空物体上
/// - 给该物体加一个 Collider 并勾选 IsTrigger
/// - 玩家物体需要 Tag=Player
/// </summary>
[DisallowMultipleComponent]
public class StoryTriggerZone : MonoBehaviour
{
    [Header("依赖")]
    [SerializeField] private StoryDirector storyDirector;

    [Header("可选：进入提示")]
    [SerializeField] private TMP_Text worldPromptText;
    [SerializeField] private string prompt = "进入教室…";
    [SerializeField] private bool showPromptOnlyWhenInRange = false;

    private bool _triggered;

    private void Awake()
    {
        if (storyDirector == null) storyDirector = FindObjectOfType<StoryDirector>();
        if (worldPromptText != null && showPromptOnlyWhenInRange) worldPromptText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;

        if (worldPromptText != null)
        {
            worldPromptText.text = prompt;
            if (showPromptOnlyWhenInRange) worldPromptText.gameObject.SetActive(true);
        }

        storyDirector?.BeginStory();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (worldPromptText != null && showPromptOnlyWhenInRange) worldPromptText.gameObject.SetActive(false);
    }

}

