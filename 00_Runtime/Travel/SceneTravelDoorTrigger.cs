using UnityEngine;

/// <summary>
/// 挂在入口门 Trigger 上：玩家进入时弹出 <see cref="SceneTravelPanel"/>。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SceneTravelDoorTrigger : MonoBehaviour
{
    [SerializeField] private SceneTravelLocation currentLocation = SceneTravelLocation.Classroom;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        currentLocation = SceneTravelCatalog.ResolveFromActiveScene();
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning("[SceneTravelDoorTrigger] Collider 未勾选 IsTrigger。", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        SceneTravelPanel.EnsureConsistentOpenState();

        if (SceneTravelPanel.IsOpen)
            return;
        if (SceneTravelPanel.IsBlockedByOtherUi())
            return;

        SceneTravelPanel.OpenFromDoor(currentLocation, this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;
        SceneTravelPanel.TryCloseFromDoor(this);
        SceneTravelPanel.EnsurePlayerUnlockedIfClosed();
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;
        if (other.GetComponentInParent<PlayerController>() != null)
            return true;
        if (other.GetComponentInParent<CharacterController>() != null)
            return true;
        return false;
    }
}
