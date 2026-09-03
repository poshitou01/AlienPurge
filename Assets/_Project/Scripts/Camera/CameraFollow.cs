using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance
    {
        get;
        private set;
    }

    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Min(0.01f)]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Map Bounds")]
    [SerializeField] private bool limitToMapBounds = true;

    [SerializeField]
    private Vector2 mapMin =
        new Vector2(-25f, -25f);

    [SerializeField]
    private Vector2 mapMax =
        new Vector2(25f, 25f);

    public Vector2 MapMin => mapMin;

    public Vector2 MapMax => mapMax;

    [Header("Camera Shake")]
    [Tooltip("是否允许播放镜头震动")]
    [SerializeField] private bool enableShake = true;

    [Tooltip("震动噪声变化速度")]
    [Min(1f)]
    [SerializeField] private float shakeFrequency = 28f;

    [Header("Light Shake")]
    [Min(0f)]
    [SerializeField] private float lightShakeStrength = 0.06f;

    [Min(0f)]
    [SerializeField] private float lightShakeDuration = 0.10f;

    [Header("Medium Shake")]
    [Min(0f)]
    [SerializeField] private float mediumShakeStrength = 0.11f;

    [Min(0f)]
    [SerializeField] private float mediumShakeDuration = 0.16f;

    [Header("Heavy Shake")]
    [Min(0f)]
    [SerializeField] private float heavyShakeStrength = 0.16f;

    [Min(0f)]
    [SerializeField] private float heavyShakeDuration = 0.22f;

    private Camera cameraComponent;

    private Vector3 followVelocity =
        Vector3.zero;

    private Vector3 smoothedPosition;

    private float activeShakeStrength;
    private float activeShakeDuration;
    private float shakeStartTime;
    private float shakeEndTime;

    private Vector2 shakeNoiseSeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "场景中存在多个 CameraFollow，"
                + "新的实例不会取代当前实例。",
                this
            );
        }
        else
        {
            Instance = this;
        }

        cameraComponent = GetComponent<Camera>();

        smoothedPosition = transform.position;

        shakeNoiseSeed = new Vector2(
            Random.Range(0f, 1000f),
            Random.Range(0f, 1000f)
        );
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateFollowPosition();

        Vector3 finalPosition =
            smoothedPosition + GetShakeOffset();

        if (limitToMapBounds
            && cameraComponent != null
            && cameraComponent.orthographic)
        {
            finalPosition =
                ClampToMapBounds(finalPosition);
        }

        transform.position = finalPosition;
    }

    /// <summary>
    /// 只计算基础跟随位置。
    /// 镜头震动不会反向影响 SmoothDamp 的起始位置，
    /// 从而避免震动结束后出现漂移或弹跳。
    /// </summary>
    private void UpdateFollowPosition()
    {
        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );

        if (limitToMapBounds
            && cameraComponent != null
            && cameraComponent.orthographic)
        {
            targetPosition =
                ClampToMapBounds(targetPosition);
        }

        smoothedPosition = Vector3.SmoothDamp(
            smoothedPosition,
            targetPosition,
            ref followVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );
    }

    /// <summary>
    /// 请求一次自定义镜头震动。
    /// 多个震动重叠时取更强强度和更长剩余时间，
    /// 不直接累加强度，避免高密度事件导致失控。
    /// </summary>
    public void Shake(
        float strength,
        float duration)
    {
        if (!enableShake)
        {
            return;
        }

        strength = Mathf.Max(0f, strength);
        duration = Mathf.Max(0f, duration);

        if (strength <= 0f || duration <= 0f)
        {
            return;
        }

        float currentTime = Time.unscaledTime;

        float remainingDuration =
            Mathf.Max(
                0f,
                shakeEndTime - currentTime
            );

        activeShakeStrength =
            Mathf.Max(
                activeShakeStrength,
                strength
            );

        float newDuration =
            Mathf.Max(
                remainingDuration,
                duration
            );

        activeShakeDuration = newDuration;
        shakeStartTime = currentTime;
        shakeEndTime = currentTime + newDuration;
    }

    public void PlayLightShake()
    {
        Shake(
            lightShakeStrength,
            lightShakeDuration
        );
    }

    public void PlayMediumShake()
    {
        Shake(
            mediumShakeStrength,
            mediumShakeDuration
        );
    }

    public void PlayHeavyShake()
    {
        Shake(
            heavyShakeStrength,
            heavyShakeDuration
        );
    }

    /// <summary>
    /// 使用平滑噪声生成二维震动。
    /// 使用 unscaledTime，游戏暂停后死亡震动仍可完成。
    /// </summary>
    private Vector3 GetShakeOffset()
    {
        float currentTime = Time.unscaledTime;

        if (currentTime >= shakeEndTime
            || activeShakeDuration <= 0f
            || activeShakeStrength <= 0f)
        {
            ClearShakeState();
            return Vector3.zero;
        }

        float elapsedTime =
            currentTime - shakeStartTime;

        float normalizedTime =
            Mathf.Clamp01(
                elapsedTime / activeShakeDuration
            );

        float strengthMultiplier =
            1f - normalizedTime;

        float noiseTime =
            currentTime * shakeFrequency;

        float noiseX =
            Mathf.PerlinNoise(
                shakeNoiseSeed.x,
                noiseTime
            ) * 2f - 1f;

        float noiseY =
            Mathf.PerlinNoise(
                shakeNoiseSeed.y,
                noiseTime
            ) * 2f - 1f;

        Vector2 noiseOffset =
            new Vector2(noiseX, noiseY);

        if (noiseOffset.sqrMagnitude > 1f)
        {
            noiseOffset.Normalize();
        }

        noiseOffset *=
            activeShakeStrength
            * strengthMultiplier;

        return new Vector3(
            noiseOffset.x,
            noiseOffset.y,
            0f
        );
    }

    private void ClearShakeState()
    {
        activeShakeStrength = 0f;
        activeShakeDuration = 0f;
        shakeStartTime = 0f;
        shakeEndTime = 0f;
    }

    private Vector3 ClampToMapBounds(
        Vector3 targetPosition)
    {
        float cameraHalfHeight =
            cameraComponent.orthographicSize;

        float cameraHalfWidth =
            cameraHalfHeight
            * cameraComponent.aspect;

        float minCameraX =
            mapMin.x + cameraHalfWidth;

        float maxCameraX =
            mapMax.x - cameraHalfWidth;

        float minCameraY =
            mapMin.y + cameraHalfHeight;

        float maxCameraY =
            mapMax.y - cameraHalfHeight;

        if (minCameraX > maxCameraX)
        {
            targetPosition.x =
                (mapMin.x + mapMax.x) * 0.5f;
        }
        else
        {
            targetPosition.x = Mathf.Clamp(
                targetPosition.x,
                minCameraX,
                maxCameraX
            );
        }

        if (minCameraY > maxCameraY)
        {
            targetPosition.y =
                (mapMin.y + mapMax.y) * 0.5f;
        }
        else
        {
            targetPosition.y = Mathf.Clamp(
                targetPosition.y,
                minCameraY,
                maxCameraY
            );
        }

        return targetPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0.01f, smoothTime);
        shakeFrequency = Mathf.Max(1f, shakeFrequency);

        lightShakeStrength =
            Mathf.Max(0f, lightShakeStrength);

        lightShakeDuration =
            Mathf.Max(0f, lightShakeDuration);

        mediumShakeStrength =
            Mathf.Max(0f, mediumShakeStrength);

        mediumShakeDuration =
            Mathf.Max(0f, mediumShakeDuration);

        heavyShakeStrength =
            Mathf.Max(0f, heavyShakeStrength);

        heavyShakeDuration =
            Mathf.Max(0f, heavyShakeDuration);
    }

    [ContextMenu("Test Light Shake")]
    private void TestLightShake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后测试镜头震动。",
                this
            );

            return;
        }

        PlayLightShake();
    }

    [ContextMenu("Test Medium Shake")]
    private void TestMediumShake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后测试镜头震动。",
                this
            );

            return;
        }

        PlayMediumShake();
    }

    [ContextMenu("Test Heavy Shake")]
    private void TestHeavyShake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后测试镜头震动。",
                this
            );

            return;
        }

        PlayHeavyShake();
    }
}