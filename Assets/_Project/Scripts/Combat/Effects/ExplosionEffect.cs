using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ExplosionEffect : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private int segmentCount = 40;

    [SerializeField] private float effectDuration = 0.15f;

    [Tooltip("圆环刚出现时，占最终半径的比例")]
    [Range(0f, 1f)]
    [SerializeField] private float startRadiusMultiplier = 0.15f;

    [Header("Runtime Debug")]
    [SerializeField] private float targetRadius;

    [SerializeField] private bool isPlaying;

    private LineRenderer lineRenderer;
    private UpgradeEffectVisual upgradeVisual;

    private ExplosionEffectPool ownerPool;

    private Coroutine effectCoroutine;


    private void Awake()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        ConfigureLineRenderer();
        upgradeVisual = GetComponent<UpgradeEffectVisual>();
    }


    private void OnDisable()
    {
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            effectCoroutine = null;
        }

        isPlaying = false;
        if (upgradeVisual != null) upgradeVisual.Hide();
    }


    public void SetPool(
        ExplosionEffectPool pool)
    {
        ownerPool = pool;
    }


    public void Initialize(
        Vector3 centerPosition,
        float radius)
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            effectCoroutine = null;
        }

        targetRadius =
            Mathf.Max(
                0.01f,
                radius
            );

        transform.position =
            centerPosition;

        isPlaying = true;

        lineRenderer.enabled = upgradeVisual == null || !upgradeVisual.Ready;
        if (upgradeVisual != null && upgradeVisual.Ready) upgradeVisual.BeginExplosion(centerPosition, targetRadius);

        effectCoroutine =
            StartCoroutine(
                PlayEffect()
            );
    }


    private IEnumerator PlayEffect()
    {
        if (upgradeVisual != null && upgradeVisual.Ready)
        {
            for (float elapsed = 0; elapsed < upgradeVisual.Duration; elapsed += Time.deltaTime)
            {
                if (UpgradeVfxAudio.RunEnded) break;
                upgradeVisual.Draw(elapsed);
                yield return null;
            }
            effectCoroutine = null;
            ReturnToPool();
            yield break;
        }
        float elapsedTime = 0f;

        float startRadius =
            targetRadius
            * startRadiusMultiplier;

        while (elapsedTime <
               effectDuration)
        {
            elapsedTime +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime
                    / effectDuration
                );

            float currentRadius =
                Mathf.Lerp(
                    startRadius,
                    targetRadius,
                    progress
                );

            DrawCircle(
                currentRadius
            );

            yield return null;
        }

        DrawCircle(
            targetRadius
        );

        effectCoroutine = null;

        ReturnToPool();
    }


    private void DrawCircle(
        float radius)
    {
        int safeSegmentCount =
            Mathf.Max(
                8,
                segmentCount
            );

        lineRenderer.positionCount =
            safeSegmentCount + 1;

        for (int i = 0;
             i <= safeSegmentCount;
             i++)
        {
            float normalized =
                (float)i
                / safeSegmentCount;

            float angle =
                normalized
                * Mathf.PI
                * 2f;

            Vector3 position =
                new Vector3(
                    Mathf.Cos(angle)
                    * radius,

                    Mathf.Sin(angle)
                    * radius,

                    0f
                );

            lineRenderer.SetPosition(
                i,
                position
            );
        }
    }


    private void ReturnToPool()
    {
        if (!isPlaying)
        {
            return;
        }

        isPlaying = false;

        if (lineRenderer != null)
        {
            lineRenderer.enabled =
                false;
        }

        if (ownerPool != null)
        {
            ownerPool.ReturnEffect(
                this
            );
        }
        else
        {
            gameObject.SetActive(
                false
            );
        }
    }


    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace =
            false;

        lineRenderer.loop =
            false;

        lineRenderer.enabled =
            false;
    }


    private void OnValidate()
    {
        segmentCount =
            Mathf.Max(
                8,
                segmentCount
            );

        effectDuration =
            Mathf.Max(
                0.01f,
                effectDuration
            );
    }
}