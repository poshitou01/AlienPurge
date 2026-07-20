using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class PoolingStressTester : MonoBehaviour
{
    /*
     * Profiler Marker
     *
     * 这些标记会直接显示在 Unity Profiler 的 CPU Usage 中，
     * 用于准确区分：
     *
     * 1. HitEffect 第一次扩容；
     * 2. HitEffect 稳定复用；
     * 3. ExperienceOrb 第一次扩容；
     * 4. ExperienceOrb 稳定复用。
     */
    private const string HitEffectColdExpansionSample =
        "Stage22.HitEffect.ColdExpansion";

    private const string HitEffectWarmReuseSample =
        "Stage22.HitEffect.WarmReuse";

    private const string ExperienceOrbColdExpansionSample =
        "Stage22.ExperienceOrb.ColdExpansion";

    private const string ExperienceOrbWarmReuseSample =
        "Stage22.ExperienceOrb.WarmReuse";

    [Header("Pool References")]
    [Tooltip("场景中的 HitEffectPool")]
    [SerializeField] private HitEffectPool hitEffectPool;

    [Tooltip("场景中的 ExperienceOrbPool")]
    [SerializeField] private ExperienceOrbPool experienceOrbPool;

    [Header("Spawn Area")]
    [Tooltip("测试对象生成区域的中心。未赋值时使用当前对象位置")]
    [SerializeField] private Transform spawnCenter;

    [Tooltip("测试对象围绕中心生成的随机半径")]
    [SerializeField] private float spawnRadius = 3f;

    [Header("HitEffect Stress Test")]
    [Tooltip("一次压力测试请求的 HitEffect 数量")]
    [SerializeField] private int hitEffectRequestCount = 60;

    [Tooltip("压力测试 HitEffect 的持续时间")]
    [SerializeField] private float hitEffectTestLifeTime = 0.5f;

    [Header("ExperienceOrb Stress Test")]
    [Tooltip("一次压力测试请求的 ExperienceOrb 数量")]
    [SerializeField] private int experienceOrbRequestCount = 50;

    [Tooltip("测试经验球提供的经验值")]
    [SerializeField] private int testExperienceAmount = 1;

    [Header("Profiler Test Hotkeys")]
    [Tooltip("运行 HitEffect Burst 测试")]
    [SerializeField]
    private KeyCode hitEffectBurstKey =
        KeyCode.F6;

    [Tooltip("运行 ExperienceOrb Burst 测试")]
    [SerializeField]
    private KeyCode experienceOrbBurstKey =
        KeyCode.F7;

    [Tooltip("回收测试器记录的 ExperienceOrb")]
    [SerializeField]
    private KeyCode returnExperienceOrbsKey =
        KeyCode.F8;

    [Header("Runtime Debug")]
    [Tooltip("当前由测试器记录的经验球数量")]
    [SerializeField] private int trackedExperienceOrbCount;

    [Tooltip("HitEffect 压力测试是否正在运行")]
    [SerializeField] private bool hitEffectTestRunning;

    private readonly List<ExperienceOrb>
        trackedExperienceOrbs =
            new List<ExperienceOrb>();

    private Coroutine hitEffectStressCoroutine;

    /// <summary>
    /// 测试对象的生成中心。
    /// Spawn Center 未赋值时使用当前对象的位置。
    /// </summary>
    private Vector3 TestCenterPosition
    {
        get
        {
            if (spawnCenter != null)
            {
                return spawnCenter.position;
            }

            return transform.position;
        }
    }

    private void Awake()
    {
        ResolvePoolReferences();
    }

    private void Update()
    {
        /*
         * 使用快捷键触发测试，
         * 避免通过 Inspector 右键菜单产生大量 EditorLoop 开销。
         *
         * F6：HitEffect Burst
         * F7：ExperienceOrb Burst
         * F8：回收测试 ExperienceOrb
         */

        if (Input.GetKeyDown(hitEffectBurstKey))
        {
            TestHitEffectBurst();
        }

        if (Input.GetKeyDown(experienceOrbBurstKey))
        {
            TestExperienceOrbBurst();
        }

        if (Input.GetKeyDown(returnExperienceOrbsKey))
        {
            ReturnTrackedExperienceOrbs();
        }
    }

    /// <summary>
    /// 自动取得当前场景中的对象池引用。
    /// Inspector 已经赋值时不会覆盖现有引用。
    /// </summary>
    private void ResolvePoolReferences()
    {
        if (hitEffectPool == null)
        {
            hitEffectPool =
                HitEffectPool.Instance;
        }

        if (experienceOrbPool == null)
        {
            experienceOrbPool =
                ExperienceOrbPool.Instance;
        }
    }

    /// <summary>
    /// 在测试区域内取得一个随机生成位置。
    /// </summary>
    private Vector3 GetRandomTestPosition()
    {
        Vector2 randomOffset =
            Random.insideUnitCircle
            * spawnRadius;

        return TestCenterPosition
            + new Vector3(
                randomOffset.x,
                randomOffset.y,
                0f
            );
    }

    // =========================================================
    // HitEffect 压力测试
    // =========================================================

    /// <summary>
    /// 同一帧请求大量 HitEffect。
    ///
    /// 可以通过两种方式调用：
    /// 1. Game 窗口按 F6；
    /// 2. Inspector 组件菜单中手动执行。
    /// </summary>
    [ContextMenu("Debug/Test HitEffect Burst")]
    private void TestHitEffectBurst()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PoolingStressTester: 请进入 Play Mode "
                + "后再进行 HitEffect 压力测试。",
                this
            );

            return;
        }

        ResolvePoolReferences();

        if (hitEffectPool == null)
        {
            Debug.LogError(
                "PoolingStressTester: 没有找到 HitEffectPool。",
                this
            );

            return;
        }

        if (hitEffectTestRunning)
        {
            Debug.LogWarning(
                "PoolingStressTester: HitEffect 压力测试 "
                + "当前仍在运行。",
                this
            );

            return;
        }

        hitEffectStressCoroutine =
            StartCoroutine(
                HitEffectBurstRoutine()
            );
    }

    private IEnumerator HitEffectBurstRoutine()
    {
        hitEffectTestRunning = true;

        int successfulRequestCount = 0;

        int totalBefore =
            hitEffectPool.TotalCount;

        int activeBefore =
            hitEffectPool.ActiveCount;

        int availableBefore =
            hitEffectPool.AvailableCount;

        /*
         * 当前可用数量不足以满足请求时，
         * 本次测试必然触发对象池扩容。
         */
        bool requiresExpansion =
            availableBefore < hitEffectRequestCount;

        string testType =
            requiresExpansion
                ? "Cold Expansion"
                : "Warm Reuse";

        string selectedSampleName =
            requiresExpansion
                ? HitEffectColdExpansionSample
                : HitEffectWarmReuseSample;

        int testFrameCount = Time.frameCount;

        /*
         * Profiler Sample 只包围真正的批量取出过程。
         *
         * Debug.Log 和等待回收不会包含在 Marker 中，
         * 可以降低日志字符串分配对测试结果的干扰。
         */
        Profiler.BeginSample(selectedSampleName);

        try
        {
            for (int i = 0;
                 i < hitEffectRequestCount;
                 i++)
            {
                HitEffect hitEffect =
                    hitEffectPool.GetHitEffect(
                        GetRandomTestPosition(),
                        Quaternion.identity
                    );

                if (hitEffect == null)
                {
                    continue;
                }

                hitEffect.Initialize(
                    hitEffectTestLifeTime
                );

                successfulRequestCount++;
            }
        }
        finally
        {
            Profiler.EndSample();
        }

        Debug.Log(
            "===== HitEffect Burst Test: After Checkout =====\n"
            + "Test Type: "
            + testType
            + "\nProfiler Sample: "
            + selectedSampleName
            + "\nFrame Count: "
            + testFrameCount
            + "\nRequested: "
            + hitEffectRequestCount
            + "\nSuccessful: "
            + successfulRequestCount
            + "\nBefore Total / Active / Available: "
            + totalBefore
            + " / "
            + activeBefore
            + " / "
            + availableBefore
            + "\nAfter Total / Active / Available: "
            + hitEffectPool.TotalCount
            + " / "
            + hitEffectPool.ActiveCount
            + " / "
            + hitEffectPool.AvailableCount,
            this
        );

        /*
         * 使用真实时间等待。
         *
         * 即使升级面板或其他系统将 Time.timeScale 设为 0，
         * 测试协程也能够继续完成。
         */
        yield return new WaitForSecondsRealtime(
            hitEffectTestLifeTime + 0.2f
        );

        Debug.Log(
            "===== HitEffect Burst Test: After Return =====\n"
            + "Total Count: "
            + hitEffectPool.TotalCount
            + "\nActive Count: "
            + hitEffectPool.ActiveCount
            + "\nAvailable Count: "
            + hitEffectPool.AvailableCount,
            this
        );

        hitEffectTestRunning = false;
        hitEffectStressCoroutine = null;
    }

    // =========================================================
    // ExperienceOrb 压力测试
    // =========================================================

    /// <summary>
    /// 同一帧请求大量 ExperienceOrb。
    ///
    /// 经验球不会自动返回对象池，需要：
    /// 1. 玩家拾取；
    /// 2. 按 F8；
    /// 3. 执行 Return Tracked Experience Orbs。
    /// </summary>
    [ContextMenu("Debug/Test ExperienceOrb Burst")]
    private void TestExperienceOrbBurst()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PoolingStressTester: 请进入 Play Mode "
                + "后再进行 ExperienceOrb 压力测试。",
                this
            );

            return;
        }

        ResolvePoolReferences();

        if (experienceOrbPool == null)
        {
            Debug.LogError(
                "PoolingStressTester: 没有找到 ExperienceOrbPool。",
                this
            );

            return;
        }

        /*
         * 移除已经被玩家拾取或已经关闭的旧引用。
         */
        RemoveInactiveTrackedOrbs();

        if (trackedExperienceOrbs.Count > 0)
        {
            Debug.LogWarning(
                "PoolingStressTester: 当前仍记录着 "
                + trackedExperienceOrbs.Count
                + " 个未回收的测试经验球。"
                + "请先按 F8 或执行 "
                + "Return Tracked Experience Orbs。",
                this
            );

            return;
        }

        int successfulRequestCount = 0;

        int totalBefore =
            experienceOrbPool.TotalCount;

        int activeBefore =
            experienceOrbPool.ActiveCount;

        int availableBefore =
            experienceOrbPool.AvailableCount;

        bool requiresExpansion =
            availableBefore < experienceOrbRequestCount;

        string testType =
            requiresExpansion
                ? "Cold Expansion"
                : "Warm Reuse";

        string selectedSampleName =
            requiresExpansion
                ? ExperienceOrbColdExpansionSample
                : ExperienceOrbWarmReuseSample;

        int testFrameCount = Time.frameCount;

        Profiler.BeginSample(selectedSampleName);

        try
        {
            for (int i = 0;
                 i < experienceOrbRequestCount;
                 i++)
            {
                ExperienceOrb experienceOrb =
                    experienceOrbPool.GetExperienceOrb(
                        GetRandomTestPosition(),
                        Quaternion.identity,
                        testExperienceAmount
                    );

                if (experienceOrb == null)
                {
                    continue;
                }

                trackedExperienceOrbs.Add(
                    experienceOrb
                );

                successfulRequestCount++;
            }
        }
        finally
        {
            Profiler.EndSample();
        }

        trackedExperienceOrbCount =
            trackedExperienceOrbs.Count;

        Debug.Log(
            "===== ExperienceOrb Burst Test: After Checkout =====\n"
            + "Test Type: "
            + testType
            + "\nProfiler Sample: "
            + selectedSampleName
            + "\nFrame Count: "
            + testFrameCount
            + "\nRequested: "
            + experienceOrbRequestCount
            + "\nSuccessful: "
            + successfulRequestCount
            + "\nBefore Total / Active / Available: "
            + totalBefore
            + " / "
            + activeBefore
            + " / "
            + availableBefore
            + "\nAfter Total / Active / Available: "
            + experienceOrbPool.TotalCount
            + " / "
            + experienceOrbPool.ActiveCount
            + " / "
            + experienceOrbPool.AvailableCount,
            this
        );
    }

    /// <summary>
    /// 回收测试器生成并且仍然处于激活状态的经验球。
    /// </summary>
    [ContextMenu("Debug/Return Tracked Experience Orbs")]
    private void ReturnTrackedExperienceOrbs()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PoolingStressTester: 请进入 Play Mode "
                + "后再回收测试经验球。",
                this
            );

            return;
        }

        ResolvePoolReferences();

        if (experienceOrbPool == null)
        {
            Debug.LogError(
                "PoolingStressTester: 没有找到 ExperienceOrbPool。",
                this
            );

            return;
        }

        int returnedCount = 0;

        for (int i = 0;
             i < trackedExperienceOrbs.Count;
             i++)
        {
            ExperienceOrb experienceOrb =
                trackedExperienceOrbs[i];

            if (experienceOrb == null)
            {
                continue;
            }

            /*
             * 已经被 Player 拾取的经验球会被关闭，
             * 此时不应再次调用回收。
             */
            if (!experienceOrb.gameObject.activeSelf)
            {
                continue;
            }

            experienceOrb.ReturnToPool();
            returnedCount++;
        }

        trackedExperienceOrbs.Clear();
        trackedExperienceOrbCount = 0;

        Debug.Log(
            "===== ExperienceOrb Burst Test: After Return =====\n"
            + "Returned By Tester: "
            + returnedCount
            + "\nTotal Count: "
            + experienceOrbPool.TotalCount
            + "\nActive Count: "
            + experienceOrbPool.ActiveCount
            + "\nAvailable Count: "
            + experienceOrbPool.AvailableCount,
            this
        );
    }

    // =========================================================
    // 重复回收保护测试
    // =========================================================

    /// <summary>
    /// 测试 HitEffectPool 的第二层重复回收保护。
    ///
    /// 此方法不调用 HitEffect.ReturnToPool，
    /// 而是直接连续调用对象池两次，
    /// 从而绕过 HitEffect 内部 isReturned 的第一层保护。
    /// </summary>
    [ContextMenu("Debug/Test HitEffect Duplicate Return")]
    private void TestHitEffectDuplicateReturn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PoolingStressTester: 请进入 Play Mode "
                + "后再测试重复回收。",
                this
            );

            return;
        }

        ResolvePoolReferences();

        if (hitEffectPool == null)
        {
            Debug.LogError(
                "PoolingStressTester: 没有找到 HitEffectPool。",
                this
            );

            return;
        }

        HitEffect hitEffect =
            hitEffectPool.GetHitEffect(
                TestCenterPosition,
                Quaternion.identity
            );

        if (hitEffect == null)
        {
            return;
        }

        hitEffect.Initialize(1f);

        // 第一次回收应成功。
        hitEffectPool.ReturnHitEffect(
            hitEffect
        );

        // 第二次回收应被 HashSet 检测并阻止。
        hitEffectPool.ReturnHitEffect(
            hitEffect
        );

        Debug.Log(
            "PoolingStressTester: HitEffect 重复回收测试已执行。"
            + "第二次回收应产生预期 Warning，"
            + "对象池数量不应发生异常。",
            this
        );
    }

    /// <summary>
    /// 测试 ExperienceOrbPool 的第二层重复回收保护。
    /// </summary>
    [ContextMenu("Debug/Test ExperienceOrb Duplicate Return")]
    private void TestExperienceOrbDuplicateReturn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PoolingStressTester: 请进入 Play Mode "
                + "后再测试重复回收。",
                this
            );

            return;
        }

        ResolvePoolReferences();

        if (experienceOrbPool == null)
        {
            Debug.LogError(
                "PoolingStressTester: 没有找到 ExperienceOrbPool。",
                this
            );

            return;
        }

        ExperienceOrb experienceOrb =
            experienceOrbPool.GetExperienceOrb(
                TestCenterPosition,
                Quaternion.identity,
                1
            );

        if (experienceOrb == null)
        {
            return;
        }

        // 第一次回收应成功。
        experienceOrbPool.ReturnExperienceOrb(
            experienceOrb
        );

        // 第二次回收应被 HashSet 检测并阻止。
        experienceOrbPool.ReturnExperienceOrb(
            experienceOrb
        );

        Debug.Log(
            "PoolingStressTester: ExperienceOrb 重复回收测试已执行。"
            + "第二次回收应产生预期 Warning，"
            + "对象池数量不应发生异常。",
            this
        );
    }

    // =========================================================
    // 辅助方法
    // =========================================================

    /// <summary>
    /// 从测试记录中移除已经被回收或拾取的经验球。
    /// </summary>
    private void RemoveInactiveTrackedOrbs()
    {
        for (int i =
                 trackedExperienceOrbs.Count - 1;
             i >= 0;
             i--)
        {
            ExperienceOrb experienceOrb =
                trackedExperienceOrbs[i];

            if (experienceOrb == null
                || !experienceOrb.gameObject.activeSelf)
            {
                trackedExperienceOrbs.RemoveAt(i);
            }
        }

        trackedExperienceOrbCount =
            trackedExperienceOrbs.Count;
    }

    private void OnDisable()
    {
        /*
         * 测试器被关闭时不强制回收对象。
         *
         * HitEffect 会按照生命周期自动回收；
         * ExperienceOrb 可以由 Player 拾取或在重新启用后按 F8 回收。
         */

        hitEffectTestRunning = false;

        if (hitEffectStressCoroutine != null)
        {
            StopCoroutine(
                hitEffectStressCoroutine
            );

            hitEffectStressCoroutine = null;
        }
    }

    private void OnValidate()
    {
        spawnRadius =
            Mathf.Max(0f, spawnRadius);

        hitEffectRequestCount =
            Mathf.Max(1, hitEffectRequestCount);

        hitEffectTestLifeTime =
            Mathf.Max(
                0.05f,
                hitEffectTestLifeTime
            );

        experienceOrbRequestCount =
            Mathf.Max(
                1,
                experienceOrbRequestCount
            );

        testExperienceAmount =
            Mathf.Max(
                1,
                testExperienceAmount
            );
    }
}