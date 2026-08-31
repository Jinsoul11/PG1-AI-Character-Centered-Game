using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;

public abstract class BasePanel:MonoBehaviour
{
    private CanvasGroup canvasGroup;

    private int alphaSpeed=10;
    //用于判断当前是显示还是隐藏面板
    public bool isShow=false;

    private UnityAction hideCallBack=null;

    protected virtual void Awake()
    {
        //一开始就获取挂载的canvasgroup组件
        canvasGroup = this.GetComponent<CanvasGroup>();
        //如果忘记加该组件用代码添加
        if (canvasGroup == null)
            canvasGroup= this.gameObject.AddComponent<CanvasGroup>();
    }
    protected virtual void Start()
    {
        Init();
    }

    //初始化按钮事件监听
    public abstract void Init();
    public virtual void ShowMe()
    {
        canvasGroup.alpha = 0;
        isShow = true;
    }

    public virtual void HideMe(UnityAction callBack)
    {
        canvasGroup.alpha = 1;
        isShow = false;

        hideCallBack= callBack;
    }

    void Update() => TickCanvasGroupFade();

    /// <summary>
    /// 子类若自写 Update，必须调用本方法，否则 HideMe 淡出与销毁回调不会执行。
    /// </summary>
    protected void TickCanvasGroupFade()
    {
        if (canvasGroup == null)
            return;

        //当处于显示状态时透明度不为1会累加到1后停止变化
        if (isShow && canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += alphaSpeed * Time.deltaTime;
            if (canvasGroup.alpha >= 1)
                canvasGroup.alpha = 1;
        }
        else if (!isShow && canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= alphaSpeed * Time.deltaTime;
            if (canvasGroup.alpha <= 0)
            {
                canvasGroup.alpha = 0;
                //面板淡出完成后执行的委托
                hideCallBack?.Invoke();
            }
        }
    }
}