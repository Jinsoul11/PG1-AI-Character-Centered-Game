using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 告别等：相对 Remi 固定机位。
/// 在场景里把本物体（含子 Camera）摆到相对 Remi 的绝对位置；运行时挂到 Remi 下并保持世界位姿，
/// 之后 Remi 移动/转向时机会跟着走。
/// 根物体平时建议 inactive。
/// </summary>
[DisallowMultipleComponent]
public class RemiRelativeCameraAnchor : MonoBehaviour
{
    [SerializeField] private string remiObjectName = "Remi";
    [SerializeField] private Camera overrideCamera;
    [Tooltip("挂到 Remi 时是否保持当前世界坐标（推荐：场景里摆好的构图）。")]
    [SerializeField] private bool keepWorldPoseOnAttach = true;

    private Transform _attachedRemi;
    private Transform _originalParent;
    private bool _wasInactive;
    private Camera _cachedMainCam;
    private bool _cachedMainCamEnabled;
    private AudioListener _cachedMainListener;
    private bool _cachedMainListenerEnabled;
    private Camera _activeCam;
    private bool _cinematicActive;

    public Camera ResolvedCamera
    {
        get
        {
            if (overrideCamera != null)
                return overrideCamera;
            overrideCamera = GetComponent<Camera>();
            if (overrideCamera == null)
                overrideCamera = GetComponentInChildren<Camera>(true);
            return overrideCamera;
        }
    }

    public bool IsCinematicActive => _cinematicActive;

    /// <summary>按名称在活动场景中查找（含 inactive）。</summary>
    public static RemiRelativeCameraAnchor FindInActiveScene(string rootName)
    {
        if (string.IsNullOrWhiteSpace(rootName))
            return null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        string name = rootName.Trim();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            RemiRelativeCameraAnchor found = FindNamedRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static RemiRelativeCameraAnchor FindNamedRecursive(Transform t, string name)
    {
        if (t == null)
            return null;
        if (t.name == name)
        {
            RemiRelativeCameraAnchor anchor = t.GetComponent<RemiRelativeCameraAnchor>();
            if (anchor == null)
                anchor = t.gameObject.AddComponent<RemiRelativeCameraAnchor>();
            return anchor;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            RemiRelativeCameraAnchor found = FindNamedRecursive(t.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// 挂到 Remi（保持场景摆好的世界位姿 → 相对偏移固定），并启用子 Camera 作为过场机位。
    /// </summary>
    public bool TryBeginCinematic(Transform remiRoot)
    {
        EndCinematic();

        if (remiRoot == null)
        {
            Remi remi = FindObjectOfType<Remi>();
            remiRoot = remi != null ? remi.transform : null;
        }

        if (remiRoot == null)
        {
            Debug.LogWarning("[RemiRelativeCameraAnchor] 未找到 Remi，无法挂接机位。", this);
            return false;
        }

        Camera cam = ResolvedCamera;
        if (cam == null)
        {
            Debug.LogWarning($"[RemiRelativeCameraAnchor] {name} 下未找到 Camera。", this);
            return false;
        }

        _originalParent = transform.parent;
        _wasInactive = !gameObject.activeSelf;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        transform.SetParent(remiRoot, keepWorldPoseOnAttach);
        _attachedRemi = remiRoot;

        _cachedMainCam = Camera.main;
        if (_cachedMainCam != null && _cachedMainCam != cam)
        {
            _cachedMainCamEnabled = _cachedMainCam.enabled;
            _cachedMainCam.enabled = false;
            _cachedMainListener = _cachedMainCam.GetComponent<AudioListener>();
            if (_cachedMainListener != null)
            {
                _cachedMainListenerEnabled = _cachedMainListener.enabled;
                _cachedMainListener.enabled = false;
            }
        }

        if (!cam.gameObject.activeSelf)
            cam.gameObject.SetActive(true);
        cam.enabled = true;
        _activeCam = cam;
        _cinematicActive = true;
        return true;
    }

    public void EndCinematic()
    {
        if (!_cinematicActive && _attachedRemi == null && _activeCam == null)
            return;

        if (_activeCam != null)
        {
            // 机位根关掉时子 Camera 一并隐藏
            _activeCam = null;
        }

        if (_attachedRemi != null)
        {
            transform.SetParent(_originalParent, true);
            _attachedRemi = null;
        }

        _originalParent = null;

        if (_wasInactive)
            gameObject.SetActive(false);
        _wasInactive = false;

        if (_cachedMainListener != null)
        {
            _cachedMainListener.enabled = _cachedMainListenerEnabled;
            _cachedMainListener = null;
        }

        if (_cachedMainCam != null)
        {
            _cachedMainCam.enabled = _cachedMainCamEnabled;
            _cachedMainCam = null;
        }

        _cinematicActive = false;
    }

    private void OnDisable()
    {
        if (_cinematicActive)
            EndCinematic();
    }
}
