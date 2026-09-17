using System.Collections;
using UnityEngine;


[DisallowMultipleComponent]
public class WeaponEvolutionFlashController : MonoBehaviour
{
    // =========================================================
    // Evolution
    // =========================================================

    [Header("Evolution Reference")]

    [SerializeField]
    private WeaponEvolutionController
        evolutionController;


    // =========================================================
    // Visual References
    // =========================================================

    [Header("Player Visual")]

    [Tooltip(
        "Player/VisualRoot/BodyVisual 上的 SpriteRenderer。"
    )]
    [SerializeField]
    private SpriteRenderer bodyRenderer;


    [Header("Weapon Visual")]

    [Tooltip(
        "CurrentWeapon/WeaponVisual 上的 SpriteRenderer。"
    )]
    [SerializeField]
    private SpriteRenderer weaponRenderer;


    // =========================================================
    // Thunder Feedback
    // =========================================================

    [Header("Thunder Piercer Flash")]

    [SerializeField]
    private Color thunderFlashColor =
        new Color(
            0.25f,
            0.95f,
            1f,
            1f
        );

    [Range(1, 6)]
    [SerializeField]
    private int thunderPulseCount =
        3;

    [Min(0.01f)]
    [SerializeField]
    private float thunderPulseDuration =
        0.07f;

    [Min(0f)]
    [SerializeField]
    private float thunderPulseGap =
        0.035f;


    [Header("Thunder Piercer Afterglow")]

    [Min(0f)]
    [SerializeField]
    private float thunderAfterglowDuration =
        0.55f;

    [Range(0f, 1f)]
    [SerializeField]
    private float thunderAfterglowStrength =
        0.35f;


    // =========================================================
    // Cluster Feedback
    // =========================================================

    [Header("Cluster Burst Flash")]

    [SerializeField]
    private Color clusterFlashColor =
        new Color(
            1f,
            0.55f,
            0.18f,
            1f
        );

    [Range(1, 6)]
    [SerializeField]
    private int clusterPulseCount =
        2;

    [Min(0.01f)]
    [SerializeField]
    private float clusterPulseDuration =
        0.11f;

    [Min(0f)]
    [SerializeField]
    private float clusterPulseGap =
        0.055f;


    [Header("Cluster Burst Afterglow")]

    [Min(0f)]
    [SerializeField]
    private float clusterAfterglowDuration =
        0.75f;

    [Range(0f, 1f)]
    [SerializeField]
    private float clusterAfterglowStrength =
        0.42f;


    // =========================================================
    // Flash Intensity
    // =========================================================

    [Header("Flash Intensity")]

    [Range(0f, 1f)]
    [SerializeField]
    private float bodyFlashStrength =
        0.55f;

    [Range(0f, 1f)]
    [SerializeField]
    private float weaponFlashStrength =
        0.90f;


    // =========================================================
    // Runtime State
    // =========================================================

    private WeaponEvolutionType
        lastEvolution =
            WeaponEvolutionType.None;


    private Coroutine flashCoroutine;


    private Color originalBodyColor =
        Color.white;

    private Color originalWeaponColor =
        Color.white;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        CacheCurrentColors();
    }


    private void Start()
    {
        if (evolutionController != null)
        {
            lastEvolution =
                evolutionController
                    .CurrentEvolution;
        }
    }


    private void Update()
    {
        if (evolutionController == null)
        {
            ResolveReferences();

            if (evolutionController == null)
            {
                return;
            }
        }


        DetectEvolutionChange();
    }


    private void OnDisable()
    {
        StopFlash();

        RestoreOriginalColors();
    }


    // =========================================================
    // Reference Resolution
    // =========================================================

    private void ResolveReferences()
    {
        if (evolutionController == null)
        {
            evolutionController =
                GetComponent<
                    WeaponEvolutionController>();
        }
    }


    // =========================================================
    // Color Cache
    // =========================================================

    /// <summary>
    /// 每次真正开始 Evolution Flash 之前重新记录一次颜色。
    ///
    /// 这样不会永远假设 SpriteRenderer 一定是白色，
    /// 也可以避免未来加入别的视觉系统以后直接覆盖它。
    /// </summary>
    private void CacheCurrentColors()
    {
        if (bodyRenderer != null)
        {
            originalBodyColor =
                bodyRenderer.color;
        }


        if (weaponRenderer != null)
        {
            originalWeaponColor =
                weaponRenderer.color;
        }
    }


    // =========================================================
    // Evolution Detection
    // =========================================================

    private void DetectEvolutionChange()
    {
        WeaponEvolutionType currentEvolution =
            evolutionController
                .CurrentEvolution;


        if (currentEvolution
            == lastEvolution)
        {
            return;
        }


        lastEvolution =
            currentEvolution;


        if (currentEvolution
            == WeaponEvolutionType.None)
        {
            StopFlash();

            RestoreOriginalColors();

            return;
        }


        PlayEvolutionFlash(
            currentEvolution
        );
    }


    // =========================================================
    // Flash Entry
    // =========================================================

    private void PlayEvolutionFlash(
        WeaponEvolutionType evolutionType)
    {
        StopFlash();

        CacheCurrentColors();


        switch (evolutionType)
        {
            case WeaponEvolutionType
                .ThunderPiercer:

                flashCoroutine =
                    StartCoroutine(
                        FlashRoutine(
                            thunderFlashColor,
                            thunderPulseCount,
                            thunderPulseDuration,
                            thunderPulseGap,
                            thunderAfterglowDuration,
                            thunderAfterglowStrength
                        )
                    );

                break;


            case WeaponEvolutionType
                .ClusterBurst:

                flashCoroutine =
                    StartCoroutine(
                        FlashRoutine(
                            clusterFlashColor,
                            clusterPulseCount,
                            clusterPulseDuration,
                            clusterPulseGap,
                            clusterAfterglowDuration,
                            clusterAfterglowStrength
                        )
                    );

                break;
        }
    }


    // =========================================================
    // Complete Flash Sequence
    // =========================================================

    private IEnumerator FlashRoutine(
        Color flashColor,
        int pulseCount,
        float pulseDuration,
        float pulseGap,
        float afterglowDuration,
        float afterglowStrength)
    {
        pulseCount =
            Mathf.Max(
                1,
                pulseCount
            );


        // =====================================================
        // Pulse Phase
        // =====================================================

        for (int pulseIndex = 0;
             pulseIndex < pulseCount;
             pulseIndex++)
        {
            yield return
                PlaySinglePulse(
                    flashColor,
                    pulseDuration
                );


            RestoreOriginalColors();


            if (pulseIndex
                    < pulseCount - 1
                && pulseGap > 0f)
            {
                yield return
                    WaitUnscaled(
                        pulseGap
                    );
            }
        }


        // =====================================================
        // Signature Afterglow
        // =====================================================

        if (afterglowDuration > 0f
            && afterglowStrength > 0f)
        {
            yield return
                PlayAfterglow(
                    flashColor,
                    afterglowDuration,
                    afterglowStrength
                );
        }


        RestoreOriginalColors();


        flashCoroutine =
            null;
    }


    // =========================================================
    // Pulse
    // =========================================================

    private IEnumerator PlaySinglePulse(
        Color flashColor,
        float pulseDuration)
    {
        float halfDuration =
            Mathf.Max(
                0.01f,
                pulseDuration * 0.5f
            );


        // =====================================================
        // Flash In
        // =====================================================

        float elapsed =
            0f;


        while (elapsed < halfDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / halfDuration
                );


            ApplyFlashColor(
                flashColor,
                t
            );


            yield return null;
        }


        // =====================================================
        // Flash Out
        // =====================================================

        elapsed =
            0f;


        while (elapsed < halfDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / halfDuration
                );


            ApplyFlashColor(
                flashColor,
                1f - t
            );


            yield return null;
        }
    }


    // =========================================================
    // Afterglow
    // =========================================================

    /// <summary>
    /// Pulse结束以后，
    /// 武器保留一小段主题色残光。
    ///
    /// 这里只作用于 WeaponRenderer，
    /// 不继续让整个 Player Body 发亮，
    /// 避免长时间遮挡角色美术。
    /// </summary>
    private IEnumerator PlayAfterglow(
        Color glowColor,
        float duration,
        float strength)
    {
        if (weaponRenderer == null)
        {
            yield break;
        }


        duration =
            Mathf.Max(
                0.01f,
                duration
            );


        strength =
            Mathf.Clamp01(
                strength
            );


        Color glowTarget =
            Color.Lerp(
                originalWeaponColor,
                glowColor,
                strength
            );


        glowTarget.a =
            originalWeaponColor.a;


        float elapsed =
            0f;


        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / duration
                );


            // 前段保留感更明显，
            // 后段快速收回，
            // 比完全线性消失更像能量余辉。
            float fade =
                1f
                - t;


            fade =
                fade * fade;


            weaponRenderer.color =
                Color.Lerp(
                    originalWeaponColor,
                    glowTarget,
                    fade
                );


            yield return null;
        }


        weaponRenderer.color =
            originalWeaponColor;
    }


    // =========================================================
    // Flash Color
    // =========================================================

    private void ApplyFlashColor(
        Color flashColor,
        float strength)
    {
        strength =
            Mathf.Clamp01(
                strength
            );


        if (bodyRenderer != null)
        {
            Color bodyTarget =
                Color.Lerp(
                    originalBodyColor,
                    flashColor,
                    bodyFlashStrength
                );


            bodyTarget.a =
                originalBodyColor.a;


            bodyRenderer.color =
                Color.Lerp(
                    originalBodyColor,
                    bodyTarget,
                    strength
                );
        }


        if (weaponRenderer != null)
        {
            Color weaponTarget =
                Color.Lerp(
                    originalWeaponColor,
                    flashColor,
                    weaponFlashStrength
                );


            weaponTarget.a =
                originalWeaponColor.a;


            weaponRenderer.color =
                Color.Lerp(
                    originalWeaponColor,
                    weaponTarget,
                    strength
                );
        }
    }


    // =========================================================
    // Restore
    // =========================================================

    private void RestoreOriginalColors()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.color =
                originalBodyColor;
        }


        if (weaponRenderer != null)
        {
            weaponRenderer.color =
                originalWeaponColor;
        }
    }


    private void StopFlash()
    {
        if (flashCoroutine == null)
        {
            return;
        }


        StopCoroutine(
            flashCoroutine
        );


        flashCoroutine =
            null;


        RestoreOriginalColors();
    }


    // =========================================================
    // Unscaled Wait
    // =========================================================

    private IEnumerator WaitUnscaled(
        float duration)
    {
        float elapsed =
            0f;


        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            yield return null;
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Test Thunder Flash")]
    private void DebugTestThunderFlash()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Please enter Play Mode first.",
                this
            );

            return;
        }


        PlayEvolutionFlash(
            WeaponEvolutionType
                .ThunderPiercer
        );
    }


    [ContextMenu(
        "Debug/Test Cluster Flash")]
    private void DebugTestClusterFlash()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Please enter Play Mode first.",
                this
            );

            return;
        }


        PlayEvolutionFlash(
            WeaponEvolutionType
                .ClusterBurst
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        thunderPulseCount =
            Mathf.Clamp(
                thunderPulseCount,
                1,
                6
            );


        thunderPulseDuration =
            Mathf.Max(
                0.01f,
                thunderPulseDuration
            );


        thunderPulseGap =
            Mathf.Max(
                0f,
                thunderPulseGap
            );


        thunderAfterglowDuration =
            Mathf.Max(
                0f,
                thunderAfterglowDuration
            );


        thunderAfterglowStrength =
            Mathf.Clamp01(
                thunderAfterglowStrength
            );


        clusterPulseCount =
            Mathf.Clamp(
                clusterPulseCount,
                1,
                6
            );


        clusterPulseDuration =
            Mathf.Max(
                0.01f,
                clusterPulseDuration
            );


        clusterPulseGap =
            Mathf.Max(
                0f,
                clusterPulseGap
            );


        clusterAfterglowDuration =
            Mathf.Max(
                0f,
                clusterAfterglowDuration
            );


        clusterAfterglowStrength =
            Mathf.Clamp01(
                clusterAfterglowStrength
            );


        bodyFlashStrength =
            Mathf.Clamp01(
                bodyFlashStrength
            );


        weaponFlashStrength =
            Mathf.Clamp01(
                weaponFlashStrength
            );
    }
}