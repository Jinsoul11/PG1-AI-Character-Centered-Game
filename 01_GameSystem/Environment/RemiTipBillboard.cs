using UnityEngine;

/// <summary>
/// 世界空间 UI 面向主相机（Remi 头顶气泡等）。
/// </summary>
[DisallowMultipleComponent]
public class RemiTipBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockYAxis = true;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        Vector3 forward = targetCamera.transform.position - transform.position;
        if (lockYAxis)
            forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(-forward.normalized, Vector3.up);
    }
}
