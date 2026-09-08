using System;
using UnityEngine;

[Serializable]
public class MissionScheduleEntry
{
    [Tooltip("任务在 GameManager SurvivalTime 的哪个时间点触发。")]
    [Min(0f)]
    [SerializeField]
    private float startTime = 60f;


    [Tooltip("非随机任务时使用的正式 Mission Type。")]
    [SerializeField]
    private MissionType missionType =
        MissionType.BeaconActivation;


    [Tooltip("任务从出现到超时允许存在多久。")]
    [Min(0.1f)]
    [SerializeField]
    private float missionDuration = 52f;


    [Tooltip("开启后，本条 Schedule 会在两个正式 Mission Type 中随机选择。")]
    [SerializeField]
    private bool randomizeType;


    public float StartTime =>
        Mathf.Max(
            0f,
            startTime
        );


    public MissionType Type =>
        missionType;


    public float Duration =>
        Mathf.Max(
            0.1f,
            missionDuration
        );


    public bool RandomizeType =>
        randomizeType;
}