using UnityEngine;

public class HitEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [SerializeField] private float lifeTime = 0.2f;
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 0.6f;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float timer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = timer / lifeTime;

        transform.localScale = Vector3.Lerp(
            Vector3.one * startScale,
            Vector3.one * endScale,
            t
        );

        if (spriteRenderer != null)
        {
            Color newColor = originalColor;
            newColor.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = newColor;
        }

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}