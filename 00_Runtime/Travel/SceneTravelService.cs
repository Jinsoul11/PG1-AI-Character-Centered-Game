using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景加载与玩家落点；首次传送前将玩家根物体设为 DontDestroyOnLoad。
/// </summary>
[DisallowMultipleComponent]
public class SceneTravelService : MonoBehaviour
{
    public static SceneTravelService Instance { get; private set; }

    private static Transform _persistentPlayerRoot;
    private static string _pendingSpawnPointName;
    private static string _pendingTravelSubtitle;
    private Coroutine _travelRoutine;

    /// <summary>下次场景加载完成后优先使用的落点物体名（用一次即清空）。</summary>
    public static void SetPendingSpawnPointName(string spawnName)
    {
        _pendingSpawnPointName = string.IsNullOrWhiteSpace(spawnName) ? null : spawnName.Trim();
    }

    /// <summary>下次传送过场副标题（用一次即清空；空则使用默认文案）。</summary>
    public static void SetPendingTravelSubtitle(string subtitle)
    {
        _pendingTravelSubtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
    }

    /// <summary>由 <see cref="PlayerController"/> 在 Awake 注册，确保场景切换前已 DontDestroyOnLoad。</summary>
    public static void RegisterPlayerTransform(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        if (IsPlayerRootAlive(_persistentPlayerRoot))
        {
            if (_persistentPlayerRoot == playerRoot)
                return;

            SuppressSceneLocalPlayerDuplicate(playerRoot);
            return;
        }

        RememberPersistentPlayer(playerRoot);
    }

    /// <summary>获取跨场景保留的主玩家（避免场景内嵌 Player 占位被误用）。</summary>
    public static PlayerController GetPlayerController()
    {
        EnsurePersistentPlayer();
        return ResolvePlayerController();
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(SceneTravelService));
        go.AddComponent<SceneTravelService>();
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

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePersistentPlayer();
        SuppressAllSceneLocalPlayerDuplicates();
        EnsureSingleActiveAudioListener();
        UiManager.EnsureEventSystem();
        SceneTravelPanel.EnsureConsistentOpenState();
        SceneTravelPanel.EnsurePlayerUnlockedIfClosed();
    }

    public void TravelTo(SceneTravelLocation destination)
    {
        // Day2 图书馆共现结束后：离开馆（切场景）即触发日切过场，落点固定教室。
        if (TryBeginDay2LibraryLeaveInterlude())
            return;

        // Day3 公寓 intro 结束后：门口「离开」拦截传送，先播 Ending。
        if (TryBeginDay3ApartmentLeaveInterlude())
            return;

        if (_travelRoutine != null)
            StopCoroutine(_travelRoutine);

        _travelRoutine = StartCoroutine(CoTravelTo(destination));
    }

    /// <summary>
    /// 当前在图书馆且脊柱可播 Day2 收束时，拦截普通传送，改走黑屏日切回教室。
    /// </summary>
    private static bool TryBeginDay2LibraryLeaveInterlude()
    {
        if (SceneTravelCatalog.ResolveFromActiveScene() != SceneTravelLocation.Library)
            return false;

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director == null || !director.CanPlayDay2Ending())
            return false;

        _pendingTravelSubtitle = null;
        director.TryPlayDay2Ending();
        return true;
    }

    /// <summary>
    /// 当前在公寓且脊柱可播 Day3 Ending 时，拦截普通传送，改走回顾终幕。
    /// </summary>
    private static bool TryBeginDay3ApartmentLeaveInterlude()
    {
        if (SceneTravelCatalog.ResolveFromActiveScene() != SceneTravelLocation.Apartment)
            return false;

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director == null || !director.IsApartmentLeaveEndingReady())
            return false;

        _pendingTravelSubtitle = null;
        director.TryPlayDay3ApartmentEnding();
        return true;
    }

    private IEnumerator CoTravelTo(SceneTravelLocation destination)
    {
        EnsurePersistentPlayer();
        SceneTravelTransitionOverlay.EnsureExists();

        PlayerController player = ResolvePlayerController();
        player?.SetMoveLock(true);
        player?.SetLookLock(true);

        string sceneName = SceneTravelCatalog.GetSceneName(destination);
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        if (loadOp == null)
        {
            Debug.LogError($"[SceneTravel] 无法加载场景 {sceneName}，请确认已加入 Build Settings。");
            player?.SetMoveLock(false);
            player?.SetLookLock(false);
            _travelRoutine = null;
            yield break;
        }

        loadOp.allowSceneActivation = false;
        string subtitle = _pendingTravelSubtitle;
        _pendingTravelSubtitle = null;
        yield return SceneTravelTransitionOverlay.Instance.PlayLoadTransition(destination, loadOp, subtitle);

        if (loadOp.isDone)
        {
            PlacePlayerForDestination(destination, SceneManager.GetActiveScene());
            SuppressAllSceneLocalPlayerDuplicates();
            EnsureSingleActiveAudioListener();
        }

        player = ResolvePlayerController();
        player?.SetMoveLock(false);
        player?.SetLookLock(false);
        SceneTravelPanel.EnsureConsistentOpenState();
        SceneTravelPanel.EnsurePlayerUnlockedIfClosed();
        _travelRoutine = null;
    }

    private static PlayerController ResolvePlayerController()
    {
        Transform playerRoot = ResolvePlayerTransform();
        if (playerRoot != null)
            return playerRoot.GetComponent<PlayerController>();

        return null;
    }

    private static void EnsurePersistentPlayer()
    {
        if (IsPlayerRootAlive(_persistentPlayerRoot))
            return;

        _persistentPlayerRoot = null;
        Transform resolved = ResolvePlayerTransform();
        if (resolved != null)
            RememberPersistentPlayer(resolved);
    }

    private static bool IsPlayerRootAlive(Transform root)
    {
        if (root == null)
            return false;

        return root.GetComponent<PlayerController>() != null;
    }

    private static Transform ResolvePlayerTransform()
    {
        if (IsPlayerRootAlive(_persistentPlayerRoot))
            return _persistentPlayerRoot;

        PlayerController[] controllers = FindAllPlayerControllers();
        if (controllers == null || controllers.Length == 0)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        if (controllers.Length == 1)
            return controllers[0].transform;

        foreach (PlayerController controller in controllers)
        {
            if (controller == null)
                continue;

            if (controller.gameObject.scene.name == "DontDestroyOnLoad")
                return controller.transform;
        }

        return controllers[0].transform;
    }

    private static PlayerController[] FindAllPlayerControllers()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<PlayerController>(true);
#endif
    }

    private static void RememberPersistentPlayer(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        playerRoot.SetParent(null, true);
        _persistentPlayerRoot = playerRoot;
        DontDestroyOnLoad(_persistentPlayerRoot.gameObject);
    }

    /// <summary>在当前或指定场景中按名称查找站位空物体。</summary>
    public static Transform TryFindSceneMarker(string markerName, Scene scene = default)
    {
        Scene targetScene = scene.IsValid() && scene.isLoaded ? scene : SceneManager.GetActiveScene();
        return FindSpawnTransform(targetScene, markerName);
    }

    /// <summary>将玩家摆到当前（或指定）场景中的命名落点。</summary>
    public static bool TryPlacePlayerAtNamedSpawn(string spawnName, Scene scene = default)
    {
        EnsurePersistentPlayer();
        Transform playerRoot = ResolvePlayerTransform();
        if (playerRoot == null)
        {
            Debug.LogWarning("[SceneTravel] 未找到玩家，无法设置落点。");
            return false;
        }

        Scene targetScene = scene.IsValid() && scene.isLoaded ? scene : SceneManager.GetActiveScene();
        Transform spawn = FindSpawnTransform(targetScene, spawnName);
        if (spawn == null)
        {
            Debug.LogWarning($"[SceneTravel] 场景 {targetScene.name} 中未找到落点 {spawnName}。");
            return false;
        }

        CharacterController cc = playerRoot.GetComponentInChildren<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        playerRoot.SetPositionAndRotation(spawn.position, spawn.rotation);

        if (cc != null)
            cc.enabled = true;

        Physics.SyncTransforms();
        return true;
    }

    private static void PlacePlayerForDestination(SceneTravelLocation destination, Scene scene)
    {
        string spawnName = !string.IsNullOrEmpty(_pendingSpawnPointName)
            ? _pendingSpawnPointName
            : SceneTravelCatalog.GetSpawnPointName(destination);
        _pendingSpawnPointName = null;

        TryPlacePlayerAtNamedSpawn(spawnName, scene);
    }

    private static void SuppressSceneLocalPlayerDuplicate(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        foreach (AudioListener listener in playerRoot.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        playerRoot.gameObject.SetActive(false);
    }

    private static void SuppressAllSceneLocalPlayerDuplicates()
    {
        EnsurePersistentPlayer();
        foreach (PlayerController controller in FindAllPlayerControllers())
        {
            if (controller == null)
                continue;

            Transform root = controller.transform;
            if (IsPlayerRootAlive(_persistentPlayerRoot) && root != _persistentPlayerRoot)
                SuppressSceneLocalPlayerDuplicate(root);
        }
    }

    private static void EnsureSingleActiveAudioListener()
    {
#if UNITY_2023_1_OR_NEWER
        AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
#else
        AudioListener[] listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>(true);
#endif
        if (listeners == null || listeners.Length <= 1)
            return;

        AudioListener keep = null;
        if (IsPlayerRootAlive(_persistentPlayerRoot))
            keep = _persistentPlayerRoot.GetComponentInChildren<AudioListener>(true);

        if (keep == null)
        {
            foreach (AudioListener listener in listeners)
            {
                if (listener != null && listener.gameObject.scene.name == "DontDestroyOnLoad")
                {
                    keep = listener;
                    break;
                }
            }
        }

        if (keep == null)
            keep = listeners[0];

        foreach (AudioListener listener in listeners)
        {
            if (listener != null)
                listener.enabled = listener == keep;
        }
    }

    private static Transform FindSpawnTransform(Scene scene, string spawnName)
    {
        if (string.IsNullOrWhiteSpace(spawnName))
            return null;

        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                if (string.Equals(root.name, spawnName, StringComparison.Ordinal))
                    return root.transform;

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child != root.transform &&
                        string.Equals(child.name, spawnName, StringComparison.Ordinal))
                        return child;
                }
            }
        }

        GameObject fallback = GameObject.Find(spawnName);
        return fallback != null ? fallback.transform : null;
    }
}
