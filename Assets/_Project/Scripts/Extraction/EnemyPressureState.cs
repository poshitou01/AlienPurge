using System;
using UnityEngine;


public enum EnemyPressureState
{
    Normal,

    OverstayLevel1,

    ExtractionDefenseEarly,

    OverstayLevel2,

    ExtractionDefenseLate,

    Emergency
}


[Serializable]
public struct EnemyPressureProfile
{
    [Tooltip(
        "最终 Spawn Interval = "
        + "Base Spawn Interval × 此倍率。"
    )]
    [Min(0.05f)]
    [SerializeField]
    private float spawnIntervalMultiplier;


    [Tooltip(
        "最终 Max Enemy = "
        + "Base Max Enemy + 此加成。"
    )]
    [Min(0)]
    [SerializeField]
    private int maxEnemyBonus;


    public float SpawnIntervalMultiplier =>
        Mathf.Max(
            0.05f,
            spawnIntervalMultiplier
        );


    public int MaxEnemyBonus =>
        Mathf.Max(
            0,
            maxEnemyBonus
        );


    public bool IsConfigured =>
        spawnIntervalMultiplier > 0f;


    public EnemyPressureProfile(
        float intervalMultiplier,
        int enemyBonus
    )
    {
        spawnIntervalMultiplier =
            Mathf.Max(
                0.05f,
                intervalMultiplier
            );

        maxEnemyBonus =
            Mathf.Max(
                0,
                enemyBonus
            );
    }
}
