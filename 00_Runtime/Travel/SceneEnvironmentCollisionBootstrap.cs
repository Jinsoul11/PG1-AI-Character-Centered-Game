using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载后为房间 wall / floor / outside 网格补齐 MeshCollider，
/// 避免 CharacterController 穿墙（教室/公寓场景原先只有地板碰撞）。
/// </summary>
public static class SceneEnvironmentCollisionBootstrap
{
    private static readonly string[] TargetNames = { "wall", "floor", "outside" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterFirstSceneLoad()
    {
        EnsureColliders(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureColliders(scene);
    }

    private static void EnsureColliders(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
            EnsureUnder(roots[r].transform);
    }

    private static void EnsureUnder(Transform root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;
            if (!IsTargetName(mf.gameObject.name))
                continue;

            MeshCollider col = mf.GetComponent<MeshCollider>();
            if (col == null)
                col = mf.gameObject.AddComponent<MeshCollider>();

            if (col.sharedMesh == null)
                col.sharedMesh = mf.sharedMesh;

            col.convex = false;
            col.isTrigger = false;
            col.enabled = true;
        }
    }

    private static bool IsTargetName(string name)
    {
        for (int i = 0; i < TargetNames.Length; i++)
        {
            if (string.Equals(name, TargetNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
