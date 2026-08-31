using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiManager 
{
    private static UiManager instance=new UiManager();
    public static UiManager Instance => instance;
    
    private Dictionary<string,BasePanel>panelDic=new Dictionary<string,BasePanel>();

    private Transform canvasTrans;
    private Image backgroundImage;
    public GameObject canvasObj;
    private static GameObject _eventSystemObj;

    public Image BackgroundImage => backgroundImage;

    public static void EnsureCanvasActive()
    {
        if (Instance.canvasObj != null && !Instance.canvasObj.activeSelf)
            Instance.canvasObj.SetActive(true);

        EnsureEventSystem();
    }

    /// <summary>场景切换会销毁场景内 EventSystem；UI Canvas 为 DDOL，需配套持久 EventSystem。</summary>
    public static void EnsureEventSystem()
    {
        if (_eventSystemObj != null)
        {
            if (!_eventSystemObj.activeSelf)
                _eventSystemObj.SetActive(true);
            SuppressSceneLocalEventSystems();
            return;
        }

#if UNITY_2023_1_OR_NEWER
        EventSystem[] existing = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        EventSystem[] existing = Object.FindObjectsOfType<EventSystem>(true);
#endif
        foreach (EventSystem es in existing)
        {
            if (es == null)
                continue;

            if (es.gameObject.scene.name == "DontDestroyOnLoad")
            {
                _eventSystemObj = es.gameObject;
                Object.DontDestroyOnLoad(_eventSystemObj);
                SuppressSceneLocalEventSystems();
                return;
            }
        }

        _eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(_eventSystemObj);

        SuppressSceneLocalEventSystems();
    }

    private static void SuppressSceneLocalEventSystems()
    {
        if (_eventSystemObj == null)
            return;

#if UNITY_2023_1_OR_NEWER
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        EventSystem[] systems = Object.FindObjectsOfType<EventSystem>(true);
#endif
        foreach (EventSystem es in systems)
        {
            if (es != null && es.gameObject != _eventSystemObj)
                es.gameObject.SetActive(false);
        }
    }

    private UiManager()
    {
        // 加载 Canvas
        canvasObj = GameObject.Instantiate(Resources.Load<GameObject>("UI/Canvas"));
        canvasTrans = canvasObj.transform;
        GameObject.DontDestroyOnLoad(canvasObj);
        EnsureEventSystem();
        // 背景图（可选）
        //GameObject bgObj = GameObject.Instantiate(Resources.Load<GameObject>("Image/Image"));
        //bgObj.transform.SetParent(canvasTrans,false);

        //backgroundImage = bgObj.GetComponent<Image>();

        //if (backgroundImage == null)
        //{
        //    Debug.LogError("background 未挂载 Image 组件");
        //}
    }
    public T ShowPanel<T>()where T : BasePanel
    {
        EnsureCanvasActive();
        string panelName=typeof(T).Name;
        
        if (panelDic.TryGetValue(panelName, out BasePanel existingBase) && existingBase != null)
        {
            T existing = existingBase as T;
            if (existing != null)
            {
                if (!existing.gameObject.activeInHierarchy)
                    existing.gameObject.SetActive(true);

                existing.ShowMe();
                return existing;
            }

            panelDic.Remove(panelName);
        }

        // 若字典中已有实例则复用，否则从 Resources 加载预制体
        GameObject prefab = Resources.Load<GameObject>("UI/" + panelName);
        GameObject panelObj = prefab != null ? GameObject.Instantiate(prefab) : null;
        if (panelObj == null)
        {
            Debug.LogError($"[UiManager] 无法加载面板预制体 UI/{panelName}");
            return null;
        }
        panelObj.transform.SetParent(canvasTrans ,false);
        // 获取面板脚本
        T panel = panelObj.GetComponent<T>();
        if (panel == null)
        {
            Debug.LogError($"[UiManager] 预制体 UI/{panelName} 缺少 {panelName} 组件（可能 Missing Script）。");
            Object.Destroy(panelObj);
            return null;
        }
        panelDic.Add(panelName, panel);

        panel.ShowMe();

        return panel;
    }

    public void HidePanel<T>(bool isFade = true)where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (!panelDic.TryGetValue(panelName, out BasePanel panel) || panel == null)
            return;

        // 隐藏时从字典移除，下次 ShowPanel 会重新实例化
        panelDic.Remove(panelName);

        if (isFade)
        {
            panel.HideMe(() =>
            {
                if (panel != null && panel.gameObject != null)
                    Object.Destroy(panel.gameObject);
            });
        }
        else
        {
            Object.Destroy(panel.gameObject);
        }
    }

    public T GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        { 
            return panelDic[panelName] as T;
        }

        return null;
    }
}
