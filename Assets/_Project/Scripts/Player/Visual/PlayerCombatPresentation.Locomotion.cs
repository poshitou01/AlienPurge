using System.Collections.Generic;
using UnityEngine;

public sealed partial class PlayerCombatPresentation
{
    [Header("Authored locomotion (scene opt-in)")]
    [Tooltip("Body direction and frames come from the original movement Animator; mouse aim only controls the weapon.")]
    public bool animatorLocomotion;
    public Pose[] walkPoses;
    readonly Dictionary<Sprite,Pose> animatedPoses=new Dictionary<Sprite,Pose>();
    readonly Dictionary<Sprite,int> animatedDirections=new Dictionary<Sprite,int>();
    Pose animatedPose;

    void CacheAnimatedPoses()
    {
        if(!animatorLocomotion)return;
        for(int i=0;i<poses.Length;i++)RegisterAnimatedPose(poses[i],i);
        if(walkPoses!=null)
            foreach(var pose in walkPoses)
                for(int i=0;i<poses.Length;i++)
                    if(pose.direction==poses[i].direction){RegisterAnimatedPose(pose,i);break;}
    }
    void RegisterAnimatedPose(Pose pose,int direction)
    {
        if(pose.idle==null)return;
        animatedPoses[pose.idle]=pose;animatedDirections[pose.idle]=direction;
        CacheHandMesh(pose.idle,pose);
    }
    Pose ResolveAnimatedPose()
    {
        if(bodyRenderer.sprite!=null&&animatedPoses.TryGetValue(bodyRenderer.sprite,out var pose))
        {animatedPose=pose;sector=animatedDirections[bodyRenderer.sprite];}
        else if(animatedPose==null)animatedPose=poses[sector];
        return animatedPose;
    }
}
