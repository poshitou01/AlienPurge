using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChainLightningEffectPool : MonoBehaviour
{
    public static ChainLightningEffectPool Instance
    {
        get;
        private set;
    }


    [Header("Prefab")]
    [SerializeField]
    private ChainLightningEffect effectPrefab;

    [Header("Pool Settings")]
    [SerializeField]
    private int initialPoolSize = 16;

    [SerializeField]
    private bool allowExpansion = true;

    [SerializeField]
    private int expansionAmount = 8;

    [Header("Runtime Debug")]
    [SerializeField]
    private int availableCount;

    [SerializeField]
    private int activeCount;


    private readonly Queue<ChainLightningEffect>
        availableEffects =
            new Queue<ChainLightningEffect>();

    private readonly HashSet<ChainLightningEffect>
        availableSet =
            new HashSet<ChainLightningEffect>();

    private readonly HashSet<ChainLightningEffect>
        activeSet =
            new HashSet<ChainLightningEffect>();


    private Transform availableContainer;
    private Transform activeContainer;


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Debug.LogWarning(
                "More than one ChainLightningEffectPool exists. "
                + "The duplicate will be destroyed.",
                this
            );

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
                "ChainLightningEffectPool: "
                + "Effect Prefab has not been assigned.",
                this
            );

            return;
        }

        int amount =
            Mathf.Max(
                0,
                initialPoolSize
            );

        for (int i = 0;
             i < amount;
             i++)
        {
            CreateEffect();
        }
    }


    private ChainLightningEffect CreateEffect()
    {
        if (effectPrefab == null)
        {
            return null;
        }

        ChainLightningEffect effect =
            Instantiate(
                effectPrefab,
                availableContainer
            );

        effect.name =
            effectPrefab.name
            + "_Pooled";

        effect.SetPool(this);

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


    public ChainLightningEffect GetEffect(
        Vector3 startPosition,
        Vector3 endPosition)
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

        ChainLightningEffect effect =
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
            startPosition,
            endPosition
        );

        RefreshDebugCounts();

        return effect;
    }


    public void ReturnEffect(
        ChainLightningEffect effect)
    {
        if (effect == null)
        {
            return;
        }

        if (!activeSet.Remove(effect))
        {
            return;
        }

        if (availableSet.Contains(effect))
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
        availableCount =
            availableSet.Count;

        activeCount =
            activeSet.Count;
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