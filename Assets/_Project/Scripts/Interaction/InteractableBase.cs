using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]//同一个gameobject上不能重复添加
public abstract  class InteractableBase : MonoBehaviour//抽象类
{//给游戏里面所有的可交互物体指定统一规则的父类

    [Header("Interaction")]

    [Tooltip("显示给玩家的交互动作文字。不要在这里填写 [E]，按键提示由 PlayerInteractor 统一添加。")]
    [SerializeField] private string interactionPrompt = "交互";

    public string InteractionPrompt=>interactionPrompt;

    public Vector2 WorldPosition=>transform.position;

    /// <summary>
    /// 当前对象是否允许玩家进行交互。
    ///
    /// 默认条件：
    /// 对象自身启用，并且 GameObject 处于激活状态。
    ///
    /// Beacon、Alien Core 等具体对象以后可以继续重写这个判断。
    /// </summary>
    public virtual bool CanInteract(PlayerInteractor interactor)//virtual的意思 父类提供一个默认实现，但是子类可以修改
    {
        return isActiveAndEnabled&&gameObject.activeInHierarchy;
    }//这个component是否处于启用状态   这个gameobject在整个hierarchy中实际是不是激活的

    /// <summary>
    /// 玩家真正执行交互时调用。
    ///
    /// 具体行为由派生类决定：
    /// Beacon -> StartActivation()
    /// Alien Core -> Recover()
    /// </summary>
    public abstract void Interact(PlayerInteractor interactor);
    //规定可以被玩家交互的对象都必须提供一个interact

}
