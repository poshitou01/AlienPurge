using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class WeaponEvolutionRevealUI : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Root References")]

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private RectTransform contentRoot;


    [Header("Visual References")]

    [SerializeField]
    private Image dimOverlay;

    [SerializeField]
    private Image accentLine;

    [SerializeField]
    private Image weaponImage;


    [Header("Text References")]

    [SerializeField]
    private TMP_Text kickerText;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text descriptionText;


    // =========================================================
    // Timing
    // =========================================================

    [Header("Reveal Timing")]

    [Min(0.01f)]
    [SerializeField]
    private float fadeInDuration =
        0.18f;

    [Min(0f)]
    [SerializeField]
    private float holdDuration =
        0.75f;

    [Min(0.01f)]
    [SerializeField]
    private float fadeOutDuration =
        0.30f;


    // =========================================================
    // Animation
    // =========================================================

    [Header("Reveal Animation")]

    [Range(0.1f, 1f)]
    [SerializeField]
    private float startScale =
        0.82f;

    [Range(1f, 1.3f)]
    [SerializeField]
    private float punchScale =
        1.06f;


    // =========================================================
    // Theme Colors
    // =========================================================

    [Header("Thunder Piercer Theme")]

    [SerializeField]
    private Color thunderAccentColor =
        new Color(
            0.15f,
            0.90f,
            1f,
            1f
        );


    [Header("Cluster Burst Theme")]

    [SerializeField]
    private Color clusterAccentColor =
        new Color(
            1f,
            0.55f,
            0.15f,
            1f
        );


    // =========================================================
    // Runtime
    // =========================================================

    private WeaponEvolutionController
        evolutionController;


    private WeaponEvolutionType
        lastSeenEvolution =
            WeaponEvolutionType.None;


    private Coroutine revealCoroutine;


    private Vector3 baseContentScale =
        Vector3.one;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();


        if (contentRoot != null)
        {
            baseContentScale =
                contentRoot.localScale;
        }


        ConfigureNoRaycast();

        HideImmediate();
    }


    private void Start()
    {
        ResolveEvolutionController();

        DetectEvolutionChange();
    }


    private void Update()
    {
        if (evolutionController == null)
        {
            ResolveEvolutionController();

            if (evolutionController == null)
            {
                return;
            }
        }


        DetectEvolutionChange();
    }


    private void OnDisable()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(
                revealCoroutine
            );

            revealCoroutine =
                null;
        }


        HideImmediate();
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup =
                GetComponent<CanvasGroup>();
        }
    }


    private void ResolveEvolutionController()
    {
        if (evolutionController != null)
        {
            return;
        }


        GameObject player =
            GameObject.FindGameObjectWithTag(
                "Player"
            );


        if (player == null)
        {
            return;
        }


        evolutionController =
            player.GetComponent<
                WeaponEvolutionController>();
    }


    // =========================================================
    // Evolution Detection
    // =========================================================

    private void DetectEvolutionChange()
    {
        if (evolutionController == null)
        {
            return;
        }


        WeaponEvolutionType currentEvolution =
            evolutionController
                .CurrentEvolution;


        if (currentEvolution
            == lastSeenEvolution)
        {
            return;
        }


        lastSeenEvolution =
            currentEvolution;


        if (currentEvolution
            == WeaponEvolutionType.None)
        {
            StopReveal();

            return;
        }


        WeaponEvolutionData data =
            evolutionController
                .CurrentEvolutionData;


        if (data == null)
        {
            return;
        }


        PlayReveal(
            data
        );
    }


    // =========================================================
    // Reveal
    // =========================================================

    private void PlayReveal(
        WeaponEvolutionData evolutionData)
    {
        if (evolutionData == null)
        {
            return;
        }


        ApplyContent(
            evolutionData
        );


        if (revealCoroutine != null)
        {
            StopCoroutine(
                revealCoroutine
            );
        }


        revealCoroutine =
            StartCoroutine(
                RevealRoutine()
            );


        // =====================================================
        // Signature Evolution Audio
        // =====================================================

        if (AudioManager.Instance != null
            && evolutionData.EvolutionSfx != null)
        {
            AudioManager.Instance
                .PlayWeaponEvolution(
                    evolutionData
                        .EvolutionSfx
                );
        }


        // =====================================================
        // Camera Impact
        // =====================================================

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance
                .PlayHeavyShake();
        }
    }


    // =========================================================
    // Content
    // =========================================================

    private void ApplyContent(
        WeaponEvolutionData evolutionData)
    {
        Color accentColor =
            GetAccentColor(
                evolutionData
                    .EvolutionType
            );


        if (kickerText != null)
        {
            kickerText.text =
                "SIGNATURE EVOLUTION";

            kickerText.color =
                Color.white;
        }


        if (titleText != null)
        {
            titleText.text =
                evolutionData
                    .DisplayName;

            titleText.color =
                accentColor;
        }


        if (descriptionText != null)
        {
            descriptionText.text =
                evolutionData
                    .Description;

            descriptionText.color =
                Color.white;
        }


        if (weaponImage != null)
        {
            Sprite weaponSprite =
                evolutionData
                    .WeaponSprite;


            weaponImage.sprite =
                weaponSprite;


            weaponImage.enabled =
                weaponSprite != null;


            weaponImage.color =
                Color.white;


            weaponImage.preserveAspect =
                true;
        }


        if (accentLine != null)
        {
            accentLine.color =
                accentColor;
        }


        if (dimOverlay != null)
        {
            Color tintedBackground =
                Color.Lerp(
                    Color.black,
                    accentColor,
                    0.18f
                );


            tintedBackground.a =
                0.55f;


            dimOverlay.color =
                tintedBackground;
        }
    }


    // =========================================================
    // Animation
    // =========================================================

    private IEnumerator RevealRoutine()
    {
        if (canvasGroup == null)
        {
            yield break;
        }


        canvasGroup.alpha =
            0f;


        if (contentRoot != null)
        {
            contentRoot.localScale =
                baseContentScale
                * startScale;
        }


        // =====================================================
        // Fade In
        // =====================================================

        float elapsed =
            0f;


        while (elapsed < fadeInDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / fadeInDuration
                );


            float eased =
                1f
                - Mathf.Pow(
                    1f - t,
                    3f
                );


            canvasGroup.alpha =
                eased;


            if (contentRoot != null)
            {
                float scale =
                    Mathf.Lerp(
                        startScale,
                        punchScale,
                        eased
                    );


                contentRoot.localScale =
                    baseContentScale
                    * scale;
            }


            yield return null;
        }


        canvasGroup.alpha =
            1f;


        // =====================================================
        // Settle
        // =====================================================

        elapsed =
            0f;


        const float settleDuration =
            0.12f;


        while (elapsed < settleDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / settleDuration
                );


            if (contentRoot != null)
            {
                float scale =
                    Mathf.Lerp(
                        punchScale,
                        1f,
                        t
                    );


                contentRoot.localScale =
                    baseContentScale
                    * scale;
            }


            yield return null;
        }


        if (contentRoot != null)
        {
            contentRoot.localScale =
                baseContentScale;
        }


        // =====================================================
        // Hold
        // =====================================================

        elapsed =
            0f;


        while (elapsed < holdDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            yield return null;
        }


        // =====================================================
        // Fade Out
        // =====================================================

        elapsed =
            0f;


        while (elapsed < fadeOutDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed
                    / fadeOutDuration
                );


            canvasGroup.alpha =
                1f - t;


            yield return null;
        }


        HideImmediate();


        revealCoroutine =
            null;
    }


    // =========================================================
    // Hide
    // =========================================================

    private void StopReveal()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(
                revealCoroutine
            );

            revealCoroutine =
                null;
        }


        HideImmediate();
    }


    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0f;


            canvasGroup.interactable =
                false;


            canvasGroup.blocksRaycasts =
                false;
        }


        if (contentRoot != null)
        {
            contentRoot.localScale =
                baseContentScale;
        }
    }


    // =========================================================
    // Theme
    // =========================================================

    private Color GetAccentColor(
        WeaponEvolutionType evolutionType)
    {
        switch (evolutionType)
        {
            case WeaponEvolutionType
                .ThunderPiercer:

                return
                    thunderAccentColor;


            case WeaponEvolutionType
                .ClusterBurst:

                return
                    clusterAccentColor;


            default:

                return
                    Color.white;
        }
    }


    // =========================================================
    // No Raycast
    // =========================================================

    private void ConfigureNoRaycast()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable =
                false;


            canvasGroup.blocksRaycasts =
                false;
        }


        SetImageRaycast(
            dimOverlay,
            false
        );


        SetImageRaycast(
            accentLine,
            false
        );


        SetImageRaycast(
            weaponImage,
            false
        );


        if (kickerText != null)
        {
            kickerText.raycastTarget =
                false;
        }


        if (titleText != null)
        {
            titleText.raycastTarget =
                false;
        }


        if (descriptionText != null)
        {
            descriptionText.raycastTarget =
                false;
        }
    }


    private void SetImageRaycast(
        Image image,
        bool value)
    {
        if (image == null)
        {
            return;
        }


        image.raycastTarget =
            value;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Refresh Evolution Controller")]
    private void DebugRefreshEvolutionController()
    {
        evolutionController =
            null;


        ResolveEvolutionController();


        Debug.Log(
            evolutionController != null
                ? "Evolution Reveal UI: "
                  + "EvolutionController found."
                : "Evolution Reveal UI: "
                  + "EvolutionController not found.",
            this
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        fadeInDuration =
            Mathf.Max(
                0.01f,
                fadeInDuration
            );


        holdDuration =
            Mathf.Max(
                0f,
                holdDuration
            );


        fadeOutDuration =
            Mathf.Max(
                0.01f,
                fadeOutDuration
            );


        startScale =
            Mathf.Clamp(
                startScale,
                0.1f,
                1f
            );


        punchScale =
            Mathf.Clamp(
                punchScale,
                1f,
                1.3f
            );
    }
}