using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ExplosionEffectPool : MonoBehaviour
{
    public static ExplosionEffectPool Instance
    {
        get;
        private set;
    }


    [Header("Prefab")]
    [SerializeField]
    private ExplosionEffect effectPrefab;

    [Header("Pool Settings")]
    [SerializeField]
    private int initialPoolSize = 20;

    [SerializeField]
    private bool allowExpansion = true;

    [SerializeField]
    private int expansionAmount = 10;

    [Header("Runtime Debug")]
    [SerializeField]
    private int totalCount;

    [SerializeField]
    private int activeCount;

    [SerializeField]
    private int availableCount;


    private readonly Queue<ExplosionEffect>
        availableEffects =
            new Queue<ExplosionEffect>();

    private readonly HashSet<ExplosionEffect>
        availableSet =
            new HashSet<ExplosionEffect>();

    private readonly HashSet<ExplosionEffect>
        activeSet =
            new HashSet<ExplosionEffect>();


    private Transform availableContainer;
    private Transform activeContainer;


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CreateRuntimeContainers();

        PrewarmPool();

        RefreshDebugCounts();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    private void CreateRuntimeContainers()
    {
        GameObject availableObject =
            new GameObject(
                "AvailableEffects"
            );

        availableObject.transform
            .SetParent(
                transform,
                false
            );

        availableContainer =
            availableObject.transform;


        GameObject activeObject =
            new GameObject(
                "ActiveEffects"
            );

        activeObject.transform
            .SetParent(
                transform,
                false
            );

        activeContainer =
            activeObject.transform;
    }


    private void PrewarmPool()
    {
        if (effectPrefab == null)
        {
            Debug.LogError(
                "ExplosionEffectPool: Effect Prefab "
                + "has not been assigned.",
                this
            );

            return;
        }

        for (int i = 0;
             i < initialPoolSize;
             i++)
        {
            CreateEffect();
        }
    }


    private ExplosionEffect CreateEffect()
    {
        if (effectPrefab == null)
        {
            return null;
        }

        ExplosionEffect effect =
            Instantiate(
                effectPrefab,
                availableContainer
            );

        effect.name =
            effectPrefab.name
            + "_Pooled";

        effect.SetPool(
            this
        );

        effect.gameObject
            .SetActive(false);

        availableEffects.Enqueue(
            effect
        );

        availableSet.Add(
            effect
        );

        RefreshDebugCounts();

        return effect;
    }


    public ExplosionEffect GetEffect(
        Vector3 position,
        float radius)
    {
        if (availableEffects.Count == 0)
        {
            if (!allowExpansion)
            {
                return null;
            }

            ExpandPool();
        }

        if (availableEffects.Count == 0)
        {
            return null;
        }

        ExplosionEffect effect =
            availableEffects.Dequeue();

        availableSet.Remove(
            effect
        );

        activeSet.Add(
            effect
        );

        effect.transform
            .SetParent(
                activeContainer,
                false
            );

        effect.gameObject
            .SetActive(true);

        effect.Initialize(
            position,
            radius
        );

        RefreshDebugCounts();

        return effect;
    }


    public void ReturnEffect(
        ExplosionEffect effect)
    {
        if (effect == null)
        {
            return;
        }

        if (!activeSet.Remove(
                effect))
        {
            return;
        }

        if (availableSet.Contains(
                effect))
        {
            return;
        }

        effect.gameObject
            .SetActive(false);

        effect.transform
            .SetParent(
                availableContainer,
                false
            );

        availableEffects.Enqueue(
            effect
        );

        availableSet.Add(
            effect
        );

        RefreshDebugCounts();
    }


    private void ExpandPool()
    {
        int amount =
            Mathf.Max(
                1,
                expansionAmount
            );

        for (int i = 0;
             i < amount;
             i++)
        {
            CreateEffect();
        }
    }


    private void RefreshDebugCounts()
    {
        activeCount =
            activeSet.Count;

        availableCount =
            availableSet.Count;

        totalCount =
            activeCount
            + availableCount;
    }


    private void OnValidate()
    {
        initialPoolSize =
            Mathf.Max(
                0,
                initialPoolSize
            );

        expansionAmount =
            Mathf.Max(
                1,
                expansionAmount
            );
    }
}