using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HitEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("命中特效默认持续时间")]
    [SerializeField] private float lifeTime = 0.2f;

    [Tooltip("命中特效开始时的尺寸")]
    [SerializeField] private float startScale = 0.2f;

    [Tooltip("命中特效结束时的尺寸")]
    [SerializeField] private float endScale = 0.6f;

    [Header("Runtime Debug")]
    [Tooltip("当前已经经过的生命周期")]
    [SerializeField] private float elapsedLifeTime;

    [Tooltip("当前 HitEffect 是否已经被回收")]
    [SerializeField] private bool isReturned;

    [Tooltip("当前 HitEffect 是否由对象池管理")]
    [SerializeField] private bool hasPool;

    private SpriteRenderer spriteRenderer;
    private HitEffectPool ownerPool;

    // 保存 Prefab 原始颜色。
    private Color originalColor = Color.white;

    // 当前这次播放实际使用的参数。
    private float runtimeLifeTime;
    private float runtimeStartScale;
    private float runtimeEndScale;
    private Color runtimeColor;
    private float runtimeStartAlpha;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogError(
                "HitEffect: SpriteRenderer component was not found.",
                this
            );

            return;
        }

        // Awake 只执行一次，因此这里保存的是 Prefab 初始颜色，
        // 不会保存到上一次播放结束时已经透明的颜色。
        originalColor = spriteRenderer.color;

        ResetRuntimeState();
    }

    private void OnEnable()
    {
        // 每次从对象池中重新启用时，
        // 先恢复默认运行状态。
        ResetRuntimeState();
    }

    /// <summary>
    /// 设置负责管理当前 HitEffect 的对象池。
    /// </summary>
    public void SetPool(HitEffectPool pool)
    {
        ownerPool = pool;
        hasPool = ownerPool != null;
    }

    /// <summary>
    /// 使用 Prefab 上配置的默认参数播放特效。
    /// </summary>
    public void Initialize()
    {
        Initialize(
            startScale,
            endScale,
            originalColor,
            originalColor.a,
            lifeTime
        );
    }

    /// <summary>
    /// 使用默认视觉参数，但覆盖生命周期。
    /// 主要用于对象池测试。
    /// </summary>
    public void Initialize(float newLifeTime)
    {
        Initialize(
            startScale,
            endScale,
            originalColor,
            originalColor.a,
            newLifeTime
        );
    }

    /// <summary>
    /// 完整初始化一次 HitEffect。
    /// 后续可用于不同尺寸、颜色和持续时间的命中特效。
    /// </summary>
    public void Initialize(
        float newStartScale,
        float newEndScale,
        Color newColor,
        float newStartAlpha,
        float newLifeTime)
    {
        elapsedLifeTime = 0f;
        isReturned = false;

        runtimeStartScale =
            Mathf.Max(0.01f, newStartScale);

        runtimeEndScale =
            Mathf.Max(0.01f, newEndScale);

        runtimeColor = newColor;

        runtimeStartAlpha =
            Mathf.Clamp01(newStartAlpha);

        runtimeLifeTime =
            Mathf.Max(0.01f, newLifeTime);

        ApplyVisualState(0f);
    }

    /// <summary>
    /// 恢复默认运行状态。
    /// </summary>
    private void ResetRuntimeState()
    {
        elapsedLifeTime = 0f;
        isReturned = false;

        runtimeLifeTime =
            Mathf.Max(0.01f, lifeTime);

        runtimeStartScale =
            Mathf.Max(0.01f, startScale);

        runtimeEndScale =
            Mathf.Max(0.01f, endScale);

        runtimeColor = originalColor;
        runtimeStartAlpha = originalColor.a;

        ApplyVisualState(0f);
    }

    private void Update()
    {
        if (isReturned)
        {
            return;
        }

        elapsedLifeTime += Time.deltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                elapsedLifeTime / runtimeLifeTime
            );

        ApplyVisualState(normalizedTime);

        if (elapsedLifeTime >= runtimeLifeTime)
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// 根据当前播放进度更新尺寸、颜色和透明度。
    /// </summary>
    private void ApplyVisualState(float normalizedTime)
    {
        float currentScale =
            Mathf.Lerp(
                runtimeStartScale,
                runtimeEndScale,
                normalizedTime
            );

        transform.localScale =
            Vector3.one * currentScale;

        if (spriteRenderer == null)
        {
            return;
        }

        Color currentColor = runtimeColor;

        currentColor.a =
            Mathf.Lerp(
                runtimeStartAlpha,
                0f,
                normalizedTime
            );

        spriteRenderer.color = currentColor;
    }

    /// <summary>
    /// 将当前 HitEffect 返回对象池。
    /// </summary>
    public void ReturnToPool()
    {
        // 生命周期结束和其他回收行为可能同时发生，
        // 使用 isReturned 防止重复回收。
        if (isReturned)
        {
            return;
        }

        isReturned = true;

        if (ownerPool != null)
        {
            ownerPool.ReturnHitEffect(this);
        }
        else
        {
            // 本阶段不再使用 Destroy。
            // 未接入对象池时先关闭对象，避免继续运行。
            Debug.LogWarning(
                "HitEffect: No owner pool was assigned. "
                + "The HitEffect will be disabled.",
                this
            );

            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        lifeTime = Mathf.Max(0.01f, lifeTime);
        startScale = Mathf.Max(0.01f, startScale);
        endScale = Mathf.Max(0.01f, endScale);
    }
}