using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ChainLightningEffect : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField]
    private float effectDuration = 0.10f;

    [Header("Runtime Debug")]
    [SerializeField]
    private bool isPlaying;

    private LineRenderer lineRenderer;

    private ChainLightningEffectPool ownerPool;

    private Coroutine returnCoroutine;


    private void Awake()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
        }
    }


    private void OnDisable()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        isPlaying = false;
    }


    public void SetPool(
        ChainLightningEffectPool pool)
    {
        ownerPool = pool;
    }


    /// <summary>
    /// 显示一段从 startPosition 到 endPosition 的电弧。
    /// </summary>
    public void Initialize(
        Vector3 startPosition,
        Vector3 endPosition)
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        isPlaying = true;

        lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(
            0,
            startPosition
        );

        lineRenderer.SetPosition(
            1,
            endPosition
        );

        lineRenderer.enabled = true;

        returnCoroutine =
            StartCoroutine(
                ReturnAfterDuration()
            );
    }


    private IEnumerator ReturnAfterDuration()
    {
        yield return new WaitForSeconds(
            effectDuration
        );

        returnCoroutine = null;

        ReturnToPool();
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
            lineRenderer.enabled = false;
        }

        if (ownerPool != null)
        {
            ownerPool.ReturnEffect(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }


    private void OnValidate()
    {
        effectDuration =
            Mathf.Max(
                0.01f,
                effectDuration
            );
    }
}